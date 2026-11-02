namespace BabaYaga.Sim;

/// <summary>
/// THE HERO CELL-ADDRESSABLE EQUIP DOOR (MC 11952, M5 hero-equip slice): wear
/// the item ANCHORED at a pack-grid cell. THE CONTROL-SCHEME FENCE (declared,
/// the ARCH no-cursor law): D2 equips through cursor DRAG — pick the item up,
/// drop it on the paper-doll box; this clone is COMMAND-DRIVEN with NO cursor
/// state (docs/ARCHITECTURE.md, the ARCH law). A grid-cell click (or the CLI
/// twin "equip &lt;x&gt; &lt;y&gt;") addressing the item to wear is therefore the
/// DECLARED control-scheme translation of D2's drag-to-wear; the GATES per the
/// oracle stay exactly where the engine has them. The exchange/refusal twins
/// cite out/T0b-mechanics-spec-3.md §6.7b (T2: "equipping over a worn item
/// EXCHANGES (displaced item returns to the hero)"; "rejection makes a sound,
/// nothing half-applies") — the merc door (GameSession.HirelingGear.cs) is the
/// in-repo precedent this door mirrors.
/// GATE ORDER, every check a MIRROR of an existing path (zero new rules):
/// RaiseIfDead FIRST (the row-64 sibling idiom, the gate speaks first in every
/// verb) → the cell is ON the grid (the give door's range line, verbatim) →
/// an item is ANCHORED there (the give door's FindCarried line) → the base's
/// kind maps to a slot via EquipSlots.For (the row-68 table EquipLastDrop
/// reads — nothing wearable answers "'X' is not wearable" BY NAME, the
/// EquipLastDrop message mirrored) → the slot is the FIRST one its type
/// allows (EquipLastDrop's own pick, OrderBy(enum-int).First — no new
/// slot-choosing rule) → an OCCUPIED slot EXCHANGES (the §6.7b T2 law the
/// merc door already ships: room for the displaced item is proven BEFORE
/// anything moves, and a pack without a free cell refuses BY NAME with
/// nothing half-applied — the stash-Take atomicity idiom).
/// STATE READINGS pinned: the SAME-AREA gate is NOT APPLICABLE here — the
/// hero wears on HIMSELF, there is no second holder to be near (the merc
/// gate exists because the merc stands somewhere; quote the difference, do
/// not copy it). The fixups ride EquipLastDrop's own lines (Grid/Carried/
/// CarriedSpots leave, NO belt re-point and NO LastLooted retreat: an
/// equipped item stays the last looted — the audit D-6 note pins that
/// reading; only potions belt and potions never pass the wearable gate, so
/// no belted reference can leave the pack through this door). The displaced
/// item re-enters THROUGH TryCarry (the give-door exchange line, LastLooted
/// included). The door touches NO RNG stream — every equip consumes zero
/// rolls (the HARD RNG law); the save needs NO bump (the worn block and the
/// grid already persist, v7 UNCHANGED).
/// </summary>
public sealed partial class GameSession
{
    /// <summary>Wear the item anchored at pack cell (x, y) — x the ROW, y the
    /// COLUMN, the same order the CLI line "equip &lt;x&gt; &lt;y&gt;" and the report's
    /// InvRow/InvCol carry — into the first slot its type allows (EquipSlots.For
    /// — the existing mapping, zero new rules). An occupied slot EXCHANGES: the
    /// displaced item lands in the first free pack cell (§6.7b T2 via the
    /// merc-door precedent); a pack without room for it refuses BY NAME with
    /// nothing half-applied. The report is the GearMoveReport shape — anchors
    /// session-read, never guessed.</summary>
    public GearMoveReport EquipFromCell(int x, int y)
    {
        RaiseIfDead(); // the row-64 gate speaks FIRST, like every sibling verb
        if (x < 0 || x >= InventoryGrid.Rows || y < 0 || y >= InventoryGrid.Columns)
            throw new SessionRejectedException($"inventory cell {x},{y} is outside the pack grid");
        var item = FindCarried(x, y)
            ?? throw new SessionRejectedException($"the pack holds no item anchored at cell {x},{y}");
        var slots = EquipSlots.For(item.Base);
        if (slots.Count == 0)
            throw new SessionRejectedException($"'{item.DisplayName}' is not wearable");
        var slot = slots.OrderBy(s => (int)s).First(); // EquipLastDrop's own pick law

        // THE EXCHANGE (§6.7b T2, the give-door reading): room for the displaced
        // item is proven BEFORE the slot empties or the pack moves — a refusal
        // leaves nothing half-applied.
        string? exBase = null, exName = null;
        int exRow = -1, exCol = -1;
        Item? worn = null;
        if (Equipment[slot] is { } wornItem)
        {
            if (!PackHasRoomFor(wornItem))
                throw new SessionRejectedException(
                    $"the exchange on '{slot}' refused — the pack has no free cell for the displaced '{wornItem.DisplayName}'");
            worn = wornItem;
            exBase = worn.Base.Id;
            exName = worn.DisplayName;
        }

        // Apply — EquipLastDrop's leave lines verbatim (no belt re-point, no
        // LastLooted retreat: the equipped item stays the last looted, D-6).
        Grid.Remove(item);
        Carried.Remove(item);
        CarriedSpots.Remove(item);
        if (!Equipment.Equip(item, slot))
            throw new SessionRejectedException("the equipment panel refused the item"); // (the slot came from EquipSlots — unreachable)
        if (worn is not null)
        {
            if (!TryCarry(worn))
                throw new InvalidOperationException(
                    $"the displaced '{worn.DisplayName}' found no cell after the room was proven"); // (the check above makes this unreachable)
            (exRow, exCol) = CarriedSpots[worn];
        }
        return new GearMoveReport(slot.ToString(), item.Base.Id, item.DisplayName, x, y,
            exBase, exName, exRow, exCol);
    }
}
