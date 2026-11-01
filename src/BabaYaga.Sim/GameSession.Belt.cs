namespace BabaYaga.Sim;

/// <summary>
/// The potion belt (LEDGER row 69, M4-S5): four cells of SESSION STATE, each
/// referencing a carried potion instance. Sibling of the core file's command
/// contract — every belt verb either fully applies or is rejected with nothing
/// half-applied. UsePotion consumes the carried copy AND the slot and raises the
/// matching pool by 40 % of Max (integer floor), CLAMPED to Max; a pool already
/// at Max is a rejection, never a silent waste. XP is never touched here, and
/// neither is the SpendSkill/mana formula (pinned by BeltPotionTests). Slots are
/// 1–4 for callers (0-based for the Belt array itself).
/// </summary>
public sealed partial class GameSession
{
    /// <summary>Original-world potion rows (data/item-bases.json): life restores
    /// Life, mana restores Mana; both are Vendor.Standard stock in town.</summary>
    public const string LifePotionId = "napitek-zycia";
    public const string ManaPotionId = "napitek-manny";

    /// The belt's four cells (the hotkey row belt_1..belt_4 maps onto these).
    public const int BeltSlots = 4;

    /// Fraction of Max a potion restores — integer floor math (×40/100).
    public const int PotionRestorePercent = 40;

    /// <summary>Cell i (0-based) holds the carried potion hotkey belt_{i+1} uses,
    /// or null. The reference IS the carried instance — a potion leaves both the
    /// pack and the slot in one consumption; doors that empty carried (SellLast)
    /// unbelt it, so a slot never dangles.</summary>
    public Item?[] Belt { get; } = new Item?[BeltSlots];

    /// What one potion did — the headless belt leg prints its POTION_USE line
    /// from these values (slot, before, after, max; Kind is "life" or "mana").
    public sealed record PotionUseReport(int Slot, string Kind, long Before, long After, long Max);

    /// Places the carried item at carriedIndex into belt slot (1–4). Rejections:
    /// slot or index outside range, an occupied slot, an item already belted, and
    /// any non-potion (the belt holds potions only — wrong kind).
    public void AddToBelt(int carriedIndex, int slot)
    {
        var cell = SlotIndex(slot);
        if (carriedIndex < 0 || carriedIndex >= Carried.Count)
            throw new SessionRejectedException($"carried index {carriedIndex} is outside the pack");
        var item = Carried[carriedIndex];
        PotionKindOf(item, require: true);
        if (Belt[cell] is not null)
            throw new SessionRejectedException($"belt slot {slot} already holds '{Belt[cell]!.DisplayName}'");
        if (Array.IndexOf(Belt, item) >= 0)
            throw new SessionRejectedException($"'{item.DisplayName}' is already on the belt");
        Belt[cell] = item;
    }

    /// Lifts the potion out of belt slot (1–4) — the slot frees, the carried copy
    /// stays in the pack. Rejections: bad slot index, empty slot.
    public Item RemoveFromBelt(int slot)
    {
        var cell = SlotIndex(slot);
        var item = Belt[cell] ?? throw new SessionRejectedException($"belt slot {slot} is empty");
        Belt[cell] = null;
        return item;
    }

    /// Drinks the potion in belt slot (1–4): raises Life (napitek-zycia) or Mana
    /// (napitek-manny) by 40 % of Max clamped to Max, then consumes the carried
    /// copy and frees the slot. Rejections: bad slot, empty slot, a slot item
    /// that is not a known potion, and a pool ALREADY at Max (waste is refused).
    public PotionUseReport UsePotion(int slot)
    {
        RaiseIfDead(); // no healing from a corpse: the belt waits for the stone (Respawn)
        var cell = SlotIndex(slot);
        var item = Belt[cell] ?? throw new SessionRejectedException($"belt slot {slot} is empty");
        string kind = PotionKindOf(item, require: true)!; // require: true throws instead of returning null

        bool life = kind == "life";
        long max = life ? Character.MaxLife : Character.MaxMana;
        long before = life ? Character.Life : Character.Mana;
        if (before >= max)
            throw new SessionRejectedException($"{kind} is already at max {max}");

        long after = Math.Min(before + max * PotionRestorePercent / 100, max);
        if (life) Character.Life = after; else Character.Mana = after;

        Belt[cell] = null;
        RemoveFromCarried(item); // the shared leaves-the-pack order (audit D-6); Unbelt is a no-op on the just-freed slot
        return new PotionUseReport(slot, kind, before, after, max);
    }

    /// Un-belts a carried item leaving the pack by a non-potion door (SellLast) —
    /// the belt never keeps a reference the pack no longer holds.
    internal void Unbelt(Item item)
    {
        for (int i = 0; i < BeltSlots; i++)
            if (ReferenceEquals(Belt[i], item)) Belt[i] = null;
    }

    /// <summary>Serializer restore hook (SaveSerializer is the only caller, the
    /// same pattern as SetArea/RestorePurse): slots re-point at the REBUILT
    /// carried list — a loaded belt must never reference pre-save instances,
    /// or UsePotion would consume a phantom and leave the real copy behind.</summary>
    internal void RestoreBelt(IReadOnlyList<int> carriedIndices)
    {
        if (carriedIndices.Count != BeltSlots)
            throw new InvalidDataException($"belt list holds {carriedIndices.Count} entries, expected {BeltSlots}");
        for (int i = 0; i < BeltSlots; i++)
        {
            int idx = carriedIndices[i];
            if (idx == -1) { Belt[i] = null; continue; }
            if (idx >= Carried.Count)
                throw new InvalidDataException($"belt slot {i + 1} points to carried index {idx} outside the pack");
            Belt[i] = Carried[idx];
        }
    }

    /// "life"/"mana" for a known potion row; require=false reports null instead
    /// of throwing (SellLast-side checks never use it; belt doors always do).
    private static string? PotionKindOf(Item item, bool require)
    {
        string? kind = item.Base.Id switch
        {
            LifePotionId => "life",
            ManaPotionId => "mana",
            _ => null,
        };
        if (kind is null && require)
            throw new SessionRejectedException($"'{item.DisplayName}' is not a potion");
        return kind;
    }

    private static int SlotIndex(int slot)
    {
        if (slot < 1 || slot > BeltSlots)
            throw new SessionRejectedException($"belt slot {slot} is outside 1–{BeltSlots}");
        return slot - 1;
    }
}
