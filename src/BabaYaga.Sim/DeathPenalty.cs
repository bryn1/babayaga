namespace BabaYaga.Sim;

/// The ledger of one death: the XP you stand on after, what actually came off,
/// and what waits on the corpse for the ¾ recovery run.
public readonly record struct DeathResult(long XpAfter, long Lost, long CorpseXp);

/// <summary>
/// The death penalty (P3 §6.6): the cost is 0 / 5 / 10 percent of the XP SPAN the
/// current level takes, band by band (Ordinary / Nightmare / Hell); the XP never
/// drops below the level's start, level 1 loses nothing, and three quarters of
/// what came off stays on the corpse until it is recovered. Character is never
/// mutated here — the pure numbers are computed and the caller re-grants the
/// corpse share through Character.GrantXp when the body is reached.
/// </summary>
public static class DeathPenalty
{
    /// The band's cost of the current level's span, in percent (P3 §6.6).
    public static int PenaltyPercent(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Ordinary => 0,
        Difficulty.Nightmare => 5,
        Difficulty.Hell => 10,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
    };

    public static DeathResult Apply(XpTable ladder, long xp, int level, Difficulty difficulty)
    {
        if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp));
        long levelStart = ladder.TotalXpForLevel(level);
        int percent = PenaltyPercent(difficulty);
        if (percent == 0 || level <= 1)
            return new DeathResult(xp, 0, 0);

        long span = ladder.TotalXpForLevel(level + 1) - levelStart;
        long after = Math.Max(xp - span * percent / 100, levelStart);
        long lost = xp - after;
        return new DeathResult(after, lost, lost * 3 / 4);
    }
}
