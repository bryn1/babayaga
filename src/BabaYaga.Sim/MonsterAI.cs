namespace BabaYaga.Sim;

/// <summary>
/// The four behaviour families the sim runs (P3 §5.5): plain closing melee, a
/// ranged attacker that keeps a distance band, a charger that commits to closing,
/// and a skittish avoider that retreats when pressed.
/// </summary>
public enum AiBehaviour
{
    Melee,
    Ranged,
    Charge,
    Avoid,
}

/// <summary>One step the AI takes this beat on its position.</summary>
public enum AiAction
{
    Approach,
    Attack,
    Withdraw,
    Hold,
}

/// <summary>
/// A behaviour's tunables: melee/charge strike at AttackRange; ranged strikes only
/// inside [KeepMin, KeepMax] and herds the target back into that band; the avoider
/// flees within FleeDistance or once hurt below FleeAtHpPercent. FleeAtHpPercent = 0
/// means "never breaks off" — melee and charge never do.
/// </summary>
public sealed record AiProfile(
    AiBehaviour Behaviour,
    int AttackRange,
    int KeepMin,
    int KeepMax,
    int FleeDistance,
    int FleeAtHpPercent)
{
    public static AiProfile Default(AiBehaviour behaviour) => behaviour switch
    {
        AiBehaviour.Melee => new(AiBehaviour.Melee, AttackRange: 1, 0, 0, 0, FleeAtHpPercent: 0),
        AiBehaviour.Ranged => new(AiBehaviour.Ranged, AttackRange: 0, KeepMin: 2, KeepMax: 6, 0, 0),
        AiBehaviour.Charge => new(AiBehaviour.Charge, AttackRange: 1, 0, 0, 0, FleeAtHpPercent: 0),
        AiBehaviour.Avoid => new(AiBehaviour.Avoid, 0, 0, 0, FleeDistance: 4, FleeAtHpPercent: 0),
        _ => throw new ArgumentOutOfRangeException(nameof(behaviour)),
    };
}

/// <summary>
/// Pure per-beat AI decisions over coordinates (P3 §5.5): no state, no rolls — the
/// same inputs always yield the same action, so replays and tests are exact.
/// </summary>
public static class MonsterAi
{
    /// The one decision for the beat. hpPercent is the monster's life share; the
    /// avoider (only) reacts to being hurt, the rest fight to the last life.
    public static AiAction Decide(AiProfile p, WorldPos self, WorldPos target, int hpPercent = 100)
    {
        int d = self.DistanceTo(target);
        switch (p.Behaviour)
        {
            case AiBehaviour.Melee:
                return d <= p.AttackRange ? AiAction.Attack : AiAction.Approach;

            case AiBehaviour.Ranged:
                if (d < p.KeepMin) return AiAction.Withdraw;   // too close: back up
                if (d > p.KeepMax) return AiAction.Approach;   // out of band: close in
                return AiAction.Attack;                        // inside the band: shoot

            case AiBehaviour.Charge:
                return d <= p.AttackRange ? AiAction.Attack : AiAction.Approach;

            case AiBehaviour.Avoid:
                bool pressed = d <= p.FleeDistance || hpPercent <= p.FleeAtHpPercent && hpPercent > 0;
                return pressed ? AiAction.Withdraw : AiAction.Hold;

            default:
                throw new ArgumentOutOfRangeException(nameof(p));
        }
    }

    /// One unit of closure toward the target (Chebyshev step: each axis moves 1 or 0).
    public static WorldPos StepToward(WorldPos from, WorldPos to) => new(
        from.X + Math.Sign(to.X - from.X),
        from.Y + Math.Sign(to.Y - from.Y));

    /// One unit of retreat away from the threat (the exact inverse step).
    public static WorldPos StepAway(WorldPos from, WorldPos threat) => new(
        from.X - Math.Sign(threat.X - from.X),
        from.Y - Math.Sign(threat.Y - from.Y));
}
