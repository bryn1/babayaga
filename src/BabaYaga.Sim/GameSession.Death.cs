namespace BabaYaga.Sim;

/// <summary>
/// Hero death and stone-resurrection (LEDGER row 64, M4-S6): the DEAD state as
/// one session-wide gate plus the Respawn door. Death is READ FROM THE LIFE POOL:
/// TakeDamage bringing Life to 0 sets IsDead (pinned trigger — the floor and the
/// flag are the same fact), so the v4 save that already persists Life carries
/// DEAD across a round trip with NO format bump. While DEAD every mutating verb
/// in the pinned set refuses through the ONE gate below (RaiseIfDead, checked
/// FIRST in each: Attack, UsePotion, Buy, SellLast, TravelTo, Loot, Gamble, and
/// — since the casting slice (row 78) — Cast, the EIGHTH gated verb);
/// stone-lighting, rites and belt upkeep stay open — a dead hero may still tidy.
/// The XP penalty runs THROUGH the built-M3 DeathPenalty API only — the percent
/// is never re-derived here (0/5/10 % by band, D2-parity-proven by that module).
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The DEAD flag (pinned public read): true once TakeDamage has
    /// brought Life to 0. Derived from the pool — the session enters DEAD with
    /// its hero, and a save-restored corpse re-enters DEAD on its own.</summary>
    public bool IsDead => Character.IsDead;

    /// <summary>The ONE death gate every gated verb calls first: the dead hero
    /// hears the death, never a per-verb precondition.</summary>
    private void RaiseIfDead()
    {
        if (IsDead) throw new SessionRejectedException("you are dead");
    }

    /// <summary>Stone-resurrection (pinned): clears DEAD, restores Life to
    /// MaxLife, and relocates the hero to the last discovered town. While alive
    /// the door is a REJECTION, never a free heal — and a second raise after a
    /// resurrection cannot charge the XP penalty twice.</summary>
    public void Respawn()
    {
        if (!IsDead)
            throw new SessionRejectedException("you are not dead — nothing to resurrect");

        // The penalty THROUGH DeathPenalty.Apply ONLY — the helper's own XpAfter
        // lands, its percent never re-derived here. The band is New's optional
        // difficulty (default Ordinary: the M4 demo world pays 0 %).
        var penalty = DeathPenalty.Apply(Ladder, Character.Xp, Character.Level, Band);
        Character.ApplyDeathXp(penalty.XpAfter);

        // Single-player stone-resurrection READS FULL LIFE (pinned): the stones
        // raise the hedge-witch at Max — no corpse run gates the raising (the ¾
        // corpse recovery is row 64's later beat, not this door).
        Character.Life = Character.MaxLife;

        // Resurrection rides the LIT STONES, not the road: TravelTo's CanTravel
        // needs a lit stone AT the hero's own area, and the hero can die in a
        // stone-less area (pokrzywlasy, widlan). The adjacency check is therefore
        // BYPASSED here on purpose — same state move TravelTo makes, no road.
        // Act I has exactly one town and its stone lights on arrival (ctor), so
        // the last discovered town is Areas.TownKey.
        string home = Areas.TownKey;
        if (!Waypoints.IsDiscovered(home))
            throw new SessionRejectedException($"the stone in '{home}' is dark — nowhere to resurrect");
        CurrentArea = home;
        Engaged = null;
        EngagedKey = null;
    }
}
