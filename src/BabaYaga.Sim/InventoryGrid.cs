namespace BabaYaga.Sim;

/// <summary>The ten equipment-panel slots (row 68's panel; weapon-set switching is
/// presentation and joins with the UI milestone, not the model).</summary>
public enum EquipSlot
{
    Head,
    Amulet,
    Body,
    Weapon,
    Offhand,
    RingLeft,
    RingRight,
    Gloves,
    Belt,
    Boots,
}

/// <summary>Which panel slots a base may occupy, derived from its item type.</summary>
public static class EquipSlots
{
    public static IReadOnlySet<EquipSlot> For(ItemBaseRow baseRow) => baseRow.Itype switch
    {
        "helms" or "circlets" => [EquipSlot.Head],
        "amulets" => [EquipSlot.Amulet],
        "body" => [EquipSlot.Body],
        "shields" => [EquipSlot.Offhand],
        "rings" => [EquipSlot.RingLeft, EquipSlot.RingRight],
        "gloves" => [EquipSlot.Gloves],
        "belts" => [EquipSlot.Belt],
        "boots" => [EquipSlot.Boots],
        "swords" or "axes" or "blunt" or "polearms" or "bows" or "staves" or "orbs" => [EquipSlot.Weapon],
        _ => new HashSet<EquipSlot>(), // gems, runes, misc: never worn
    };
}

/// <summary>The ten-slot paper doll. Equipping into a slot the base's type does not
/// fit is rejected; each slot holds one item (re-equip replaces).</summary>
public sealed class EquipmentPanel
{
    private readonly Dictionary<EquipSlot, Item> _items = new();

    public IReadOnlyDictionary<EquipSlot, Item> Equipped => _items;

    public Item? this[EquipSlot slot] => _items.GetValueOrDefault(slot);

    public bool Equip(Item item, EquipSlot slot)
    {
        if (!EquipSlots.For(item.Base).Contains(slot)) return false;
        _items[slot] = item;
        return true;
    }

    public bool Unequip(EquipSlot slot) => _items.Remove(slot);

    /// <summary>Pieces of a set currently worn — the count the §4.4 bonus tiers read.</summary>
    public int EquippedSetPieces(string setName) =>
        _items.Values.Count(i => i.SetId == setName);
}

/// <summary>
/// The inventory: 4 rows × 10 columns (P3 §7). Items cover their base's w×h cells;
/// placement is rejected when the footprint would overlap, run off the grid, or
/// when the item is already placed.
/// </summary>
public sealed class InventoryGrid
{
    public const int Rows = 4;
    public const int Columns = 10;

    private readonly HashSet<(int, int)> _occupied = new();
    private readonly Dictionary<Item, (int Row, int Col)> _placed = new();

    public int PlacedCount => _placed.Count;
    public int FreeCells => Rows * Columns - _occupied.Count;

    /// <summary>True when the item's footprint fits at (row, col) — bounds checked,
    /// row-first, all cells free.</summary>
    public bool CanPlace(Item item, int row, int col)
    {
        if (_placed.ContainsKey(item)) return false;
        for (int r = row; r < row + item.Base.Height; r++)
            for (int c = col; c < col + item.Base.Width; c++)
                if (r < 0 || r >= Rows || c < 0 || c >= Columns || _occupied.Contains((r, c)))
                    return false;
        return true;
    }

    public bool TryPlace(Item item, int row, int col)
    {
        if (!CanPlace(item, row, col)) return false;
        for (int r = row; r < row + item.Base.Height; r++)
            for (int c = col; c < col + item.Base.Width; c++)
                _occupied.Add((r, c));
        _placed[item] = (row, col);
        return true;
    }

    /// <summary>False when the item is not in the grid (no silent no-ops).</summary>
    public bool Remove(Item item)
    {
        if (!_placed.Remove(item, out var at)) return false;
        for (int r = at.Row; r < at.Row + item.Base.Height; r++)
            for (int c = at.Col; c < at.Col + item.Base.Width; c++)
                _occupied.Remove((r, c));
        return true;
    }
}
