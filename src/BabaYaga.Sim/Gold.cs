namespace BabaYaga.Sim;

/// <summary>
/// Gold math (P2 §4.8). A pile is the treasure class's own gold × mul/256, then
/// × (1 + the KILLER's GoldFind%), floored at each step — party GF does not count.
/// Magic Find is deliberately absent from this signature: MF is a quality-only
/// modifier and never touches item or gold quantity (P2 §4.1, §4.8).
/// </summary>
public static class Gold
{
    public static long Pile(long tcGold, int tcMultiplier, int killerGoldFindPercent) =>
        tcGold * Math.Max(tcMultiplier, 0) / 256 * (100 + Math.Max(killerGoldFindPercent, 0)) / 100;
}

/// <summary>A character's carried coin. The vendor's insufficient-gold rule
/// (P2 §4.8) runs exclusively through TrySpend — no negative balance, ever.</summary>
public sealed class Purse
{
    public long Amount { get; private set; }

    public Purse(long start = 0)
    {
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), "purse cannot start in debt");
        Amount = start;
    }

    public void Receive(long gold)
    {
        if (gold < 0) throw new ArgumentOutOfRangeException(nameof(gold));
        Amount += gold;
    }

    /// <summary>False (and no change) when the purse cannot cover the price.</summary>
    public bool TrySpend(long gold)
    {
        if (gold < 0 || Amount < gold) return false;
        Amount -= gold;
        return true;
    }
}
