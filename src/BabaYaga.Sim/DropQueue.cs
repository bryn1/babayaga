namespace BabaYaga.Sim;

/// <summary>
/// The pending pile a kill's walk left on the ground (P2 §4.9 step 6 hand-off):
/// dropped items in draw order plus the gold piles, until the killer loots them.
/// Looting takes from the front and skips (never removes) what the carrier cannot
/// take; gold is collected as one pile when the carrier next loots.
/// </summary>
public sealed class DropQueue
{
    private readonly List<Item> _items = [];

    public long Gold { get; private set; }
    public int Pending => _items.Count;
    public IReadOnlyList<Item> PendingItems => _items;

    /// The most recent drop that landed here — what "equip lastdrop" reads.
    public Item? LastDrop { get; private set; }

    public void Enqueue(TreasureClassDrop drop)
    {
        _items.AddRange(drop.Items);
        if (drop.Items.Count > 0) LastDrop = drop.Items[^1];
        Gold += drop.Gold;
    }

    /// Takes at most count items from the front for which accept(item) is true;
    /// refused items stay in the queue (a full grid never swallows a drop).
    public List<Item> TakeUpTo(int count, Func<Item, bool> accept)
    {
        var taken = new List<Item>();
        for (int i = 0; i < _items.Count && taken.Count < count;)
        {
            if (accept(_items[i]))
            {
                taken.Add(_items[i]);
                _items.RemoveAt(i);
            }
            else i++;
        }
        return taken;
    }

    /// Moves the whole gold pile out (into the purse); the queue keeps none.
    public long CollectGold()
    {
        var gold = Gold;
        Gold = 0;
        return gold;
    }
}
