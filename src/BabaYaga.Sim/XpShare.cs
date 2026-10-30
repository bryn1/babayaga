namespace BabaYaga.Sim;

/// One body at a kill site: its level, where it stood, and whether it lives.
public sealed record PartyMember(Character Hero, WorldPos Position, bool Alive = true);

/// <summary>
/// Party XP sharing (P3 §6.6): the living party members in range of the kill pool
/// their shares. The pot grows by 87/256 of the kill XP per member beyond the
/// first; each member takes pot × own level ÷ Σ levels, then their own level-gap
/// and ExpRatio apply (XpTable owns those tables — reused, never restated).
/// Shares only ever floor, so the payout never exceeds the pot (conservation).
/// </summary>
public static class XpShare
{
    /// Party members share a kill only within this distance of it (coord units).
    public const int ShareDistance = 80;

    /// The pot bonus per member beyond the first, in 256ths of the kill XP.
    public const int PartyBonusOver256 = 87;

    /// The pot: kill XP plus 87/256 of it for each member past the first (floored).
    public static long Pot(long killXp, int membersInRange)
    {
        if (killXp < 0) throw new ArgumentOutOfRangeException(nameof(killXp));
        if (membersInRange <= 0) return 0;
        return killXp * (256L + (long)PartyBonusOver256 * (membersInRange - 1)) / 256;
    }

    /// The per-input-order payouts: a member only shares while ALIVE and within
    /// ShareDistance of the kill spot; everyone else is paid 0.
    public static long[] Shares(
        XpTable ladder,
        long monsterBaseXp,
        KillType killType,
        int monsterLevel,
        WorldPos killSpot,
        IReadOnlyList<PartyMember> party)
    {
        var shares = new long[party.Count];
        var eligible = party
            .Select((m, i) => (Member: m, Index: i))
            .Where(t => t.Member.Alive
                && t.Member.Position.DistanceTo(killSpot) <= ShareDistance)
            .ToArray();
        if (eligible.Length == 0) return shares;

        long killXp = monsterBaseXp * (long)killType / 100;
        long pot = Pot(killXp, eligible.Length);
        int levelSum = eligible.Sum(t => t.Member.Hero.Level);
        foreach (var (member, index) in eligible)
        {
            long share = pot * member.Hero.Level / levelSum;
            share = share * XpTable.LevelGapFactorPercent(member.Hero.Level, monsterLevel) / 100;
            share = share * ladder.ExpRatioForLevel(member.Hero.Level) / 1024;
            shares[index] = share;
        }
        return shares;
    }
}
