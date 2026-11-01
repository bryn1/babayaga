namespace BabaYaga.Sim;

/// <summary>
/// THE TOWN STASH, multi-tab (LEDGER row 74, M4-S12). D2 truth adopted (owner
/// oracle grant 2026-10-30): the chest stands ONLY in town — every stash verb
/// gates DEAD first (the row-64 RaiseIfDead idiom, the belt's message shape),
/// then on the SAME town concept the death beat resurrects into (Areas.TownKey
/// — grodzisko, the one isTown area; the Hire gate of rows 65–66 is the idiom,
/// and Respawn's "last discovered town is Areas.TownKey" is how the town is
/// known). It has MULTIPLE TABS: tab 1 stands with the chest, and extra tabs
/// unlock through rewards — glowa's StashTab reward opens tab 2 LIVE, paid via
/// UnlockNextStashTabs from the reward branch in GameSession.World. Store/Take
/// move items between the carried pack and a tab reusing the EXISTING grid
/// machinery — per-tab storage is one InventoryGrid placed through TryPlace,
/// the tab fill first-fits exactly like TryCarry first-fits the pack, and a
/// taken item re-enters the pack THROUGH TryCarry. No new placement engine.
///
/// ORIGINAL DESIGN (not D2-derived): the tab grid carries the SAME 4×10 dims
/// as the player pack (InventoryGrid.Rows/Columns). D2's 10×4 stash grid is
/// the oracle-era shape, but its adoption awaits its own data row.
///
/// State readings, pinned:
///  • NO swap-on-occupied: storing never displaces what stands, the pack cell
///    is never taken from its holder, and a tab with no free cell for the
///    footprint refuses BY NAME ("full" names the probe's own footprint —
///    first-fit, never compaction). The swap drag is a later beat, not here.
///  • GOLD IS NOT STORABLE: the purse is a balance and the oracle-era data
///    carries no stash-gold semantics — StoreGold refuses BY NAME (the door
///    exists so the reading is answerable, not silently absent).
///  • Multi-cell items address their ANCHOR cell (row, col) on both grids —
///    the same coordinate truth CarriedSpots keeps for the pack.
///  • Gate order mirrors Hire: DEAD → town → tab → coords → content.
/// Same contract as every session door: fully applies, or throws with nothing
/// half-applied. Neither door touches any RNG stream (the HARD RNG law), and
/// the Sim still never answers "which cell?" outside these command reports
/// (ARCH v8 — the panel's presentation-side re-derivation stands untouched).
/// </summary>
public sealed partial class GameSession
{
    /// One tab's storage: its own pack-shaped InventoryGrid + the lockstep
    /// item/spot lists (the core file's Carried/CarriedSpots idiom, per tab).
    internal sealed class StashTab
    {
        public InventoryGrid Grid { get; } = new();
        public List<Item> Items { get; } = [];
        internal Dictionary<Item, (int Row, int Col)> Spots { get; } =
            new(ReferenceEqualityComparer.Instance);
    }

    /// Tabs materialize their storage on FIRST use — an opened empty tab costs
    /// nothing and never lands in a save (the unlock truth already rides
    /// StashTabs, so emptiness has nothing to report).
    private readonly Dictionary<int, StashTab> _stashTabs = new();

    /// True when the tab stands open: tab 1 rides with the town chest, tab N+1
    /// opens through N paid StashTab rewards (UnlockNextStashTabs).
    public bool IsStashTabUnlocked(int tab) => tab >= 1 && tab <= StashTabs + 1;

    /// What one move did — TabRow/TabCol are the tab cells the item occupies
    /// (or left), InvRow/InvCol the pack cells it came from (or landed on),
    /// always the ANCHOR. The headless leg prints its STASH lines from here.
    public sealed record StashMoveReport(
        string Base, string Name, int Tab, int TabRow, int TabCol, int InvRow, int InvCol);

    /// Moves the carried item anchored at pack cell (invX, invY) into the tab,
    /// first-fitting its own grid. Gates: DEAD → town → tab unlocked → pack
    /// coords in range → the pack holds an item at those exact anchor cells →
    /// the tab has a free cell for the footprint (full refuses by name — no
    /// swap, no compaction). Belt and LastLooted fixups mirror SellLast/
    /// UsePotion, so no door dangles a reference the pack no longer holds.
    public StashMoveReport Store(int invX, int invY, int tab)
    {
        RaiseIfDead();
        if (CurrentArea != Areas.TownKey)
            throw new SessionRejectedException(
                $"the stash stands in town — the chest is not in '{CurrentArea}'");
        if (!IsStashTabUnlocked(tab))
            throw new SessionRejectedException(
                $"stash tab {tab} is locked — rewards open further tabs");
        if (invX < 0 || invX >= InventoryGrid.Rows || invY < 0 || invY >= InventoryGrid.Columns)
            throw new SessionRejectedException(
                $"inventory cell {invX},{invY} is outside the pack grid");
        var item = FindCarried(invX, invY)
            ?? throw new SessionRejectedException(
                $"the pack holds no item anchored at cell {invX},{invY}");

        var t = TabStorage(tab);
        for (int r = 0; r < InventoryGrid.Rows; r++)
            for (int c = 0; c < InventoryGrid.Columns; c++)
                if (t.Grid.CanPlace(item, r, c))
                {
                    t.Grid.TryPlace(item, r, c);
                    t.Items.Add(item);
                    t.Spots[item] = (r, c);
                    RemoveFromCarried(item); // the shared leaves-the-pack order — belt re-point + LastLooted retreat (audit D-6)
                    return new StashMoveReport(item.Base.Id, item.DisplayName, tab, r, c, invX, invY);
                }
        throw new SessionRejectedException(
            $"stash tab {tab} is full — no cells left for '{item.DisplayName}'");
    }

    /// Lifts the item anchored at tab cell (x, y) back into the pack THROUGH
    /// TryCarry (the pack first-fits itself around the carried set; the landing
    /// cells ride the report). The room is proven BEFORE anything moves, so a
    /// full pack refuses with the item still stashed — never a swap, never a
    /// half-applied lift. Gates: DEAD → town → tab unlocked → tab coords in
    /// range → the tab holds an item at those exact anchor cells → pack room.
    public StashMoveReport Take(int tab, int x, int y)
    {
        RaiseIfDead();
        if (CurrentArea != Areas.TownKey)
            throw new SessionRejectedException(
                $"the stash stands in town — the chest is not in '{CurrentArea}'");
        if (!IsStashTabUnlocked(tab))
            throw new SessionRejectedException(
                $"stash tab {tab} is locked — rewards open further tabs");
        if (x < 0 || x >= InventoryGrid.Rows || y < 0 || y >= InventoryGrid.Columns)
            throw new SessionRejectedException(
                $"stash cell {x},{y} in tab {tab} is outside the tab grid");
        var t = TabStorage(tab);
        var item = FindStashed(t, x, y)
            ?? throw new SessionRejectedException(
                $"tab {tab} holds no item anchored at cell {x},{y}");
        if (!PackHasRoomFor(item))
            throw new SessionRejectedException(
                $"the pack has no cell left for '{item.DisplayName}'");

        t.Grid.Remove(item);
        t.Items.Remove(item);
        t.Spots.Remove(item);
        TryCarry(item); // the existing door: pack placement, CarriedSpots, LastLooted
        var (ir, ic) = CarriedSpots[item];
        return new StashMoveReport(item.Base.Id, item.DisplayName, tab, x, y, ir, ic);
    }

    /// The reward side (GameSession.World's StashTab branch) pays through here.
    /// The tab COUNT is the unlock truth, so opening an already-counted tab is
    /// idempotent state — and the count pays ONCE because the quest FSM refuses
    /// a second QuestComplete BEFORE ApplyReward runs: the existing REFUSED-
    /// throw idiom BeltQuestTests pins on gesi's skill point (asserted on tab 2
    /// itself in BeltStashTests). RewardAmount tabs open in order, tab 2 first.
    internal void UnlockNextStashTabs(int amount)
    {
        for (int n = 0; n < amount; n++) StashTabs++;
    }

    /// Gold has NO stash semantics in the oracle-era data (the purse is a
    /// balance and no gold item exists to place into a cell) — the door stands
    /// so the reading is refused BY NAME, never silently absent.
    public void StoreGold(long amount) =>
        throw new SessionRejectedException(
            $"gold is not storable — the stash holds items, not coin ({amount} refused)");

    // ---- shared helpers ----

    private StashTab TabStorage(int tab) =>
        _stashTabs.TryGetValue(tab, out var t) ? t : (_stashTabs[tab] = new StashTab());

    /// The serializer's read surface (SaveSerializer is the only caller outside
    /// this file — the same pattern as the internal CarriedSpots it mirrors).
    internal IReadOnlyDictionary<int, StashTab> StashForSave => _stashTabs;

    /// Serializer restore hook: the tab's storage, created if the save opens it.
    internal StashTab StashTabForSave(int tab) => TabStorage(tab);

    private Item? FindCarried(int row, int col)
    {
        foreach (var (item, spot) in CarriedSpots)
            if (spot == (row, col)) return item;
        return null;
    }

    private static Item? FindStashed(StashTab tab, int row, int col)
    {
        foreach (var (item, spot) in tab.Spots)
            if (spot == (row, col)) return item;
        return null;
    }

    /// True when some pack cell fits the footprint — the same first-fit scan
    /// TryCarry runs, asked BEFORE the tab gives the item up (atomicity).
    private bool PackHasRoomFor(Item item)
    {
        for (int r = 0; r < InventoryGrid.Rows; r++)
            for (int c = 0; c < InventoryGrid.Columns; c++)
                if (Grid.CanPlace(item, r, c)) return true;
        return false;
    }
}
