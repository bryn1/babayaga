namespace BabaYaga.Sim;

/// <summary>
/// The combat half of the session, split out of the core file by concern
/// (audit MC 11907 §5 names the combat command doors inside the core's mix):
/// the strike volley with its report, the plain-engagement Spawn the volley
/// rides (the sibling of World's Special encounter), and the ONE counter a
/// surviving monster pays — rolled on the third stream so the player-side
/// streams keep their offsets. Same contract as the core file: each command
/// fully applies or is rejected, and the DEAD gate opens the volley.
/// </summary>
public sealed partial class GameSession
{
    public sealed record AttackReport(int Strikes, int Kills, long Xp, int DropsPending, long GoldPending);

    /// Strikes the named monster row (fresh spawn per kill; switching target
    /// abandons the current fight — no flee rules exist yet). Each kill runs the
    /// KillFlow chain: XP already paid by the resolver, event + walk + queue.
    /// A monster that survives the strike counters once (row 69 — EnemyRng only).
    /// The DEAD gate opens the command and stops the volley: a counter that lands
    /// the killing blow ends this call mid-times, and no strike is paid from a
    /// corpse — the report carries the strikes that actually happened.
    public AttackReport Attack(string monsterKey, int times)
    {
        RaiseIfDead();
        if (times < 1) throw new SessionRejectedException("attack times must be at least 1");
        var row = Monsters.Rows.FirstOrDefault(r => r.Key == monsterKey)
            ?? throw new SessionRejectedException($"unknown monster '{monsterKey}'");
        if (Engaged is not null && EngagedKey != monsterKey) { Engaged = null; EngagedKey = null; }
        var area = Areas.Get(CurrentArea);
        int strikes = 0, kills = 0;
        long xp = 0;
        for (int i = 0; i < times; i++)
        {
            if (IsDead) break; // died to a counter: the DEAD gate stops the volley mid-call
            Engaged ??= Spawn(row, area);
            var (outcome, _) = _flow.Strike(CombatRng, DropRng, Character, Engaged, Ladder,
                AttackDamageMin, AttackDamageMax, TcKeyFor(area.Level), EngagedKind, EngagedAffixKeys,
                area.Level, AreaSeed(area), MagicFind, GoldFind);
            strikes++;
            if (outcome.Killed)
            {
                kills++;
                xp += outcome.XpGranted;
                ShareKillWithHireling(Engaged!); // §6.7: the alive merc takes 86/256 of the capped chain (no RNG)
                Engaged = null; EngagedKey = null;
            }
            else CounterStrike(Engaged!); // the survivor hits back once (row 69)
        }
        return new AttackReport(strikes, kills, xp, Drops.Pending, Drops.Gold);
    }

    private Monster Spawn(MonsterBaseRow row, AreaRow area)
    {
        EngagedKey = row.Key;
        EngagedKind = SpecialKind.Plain;
        EngagedAffixKeys = [];
        Engaged = Monsters.Spawn(row, MonsterTable.ResolveSpawnLevel(row, Difficulty.Ordinary, area));
        return Engaged;
    }

    /// One counter by the engaged survivor (row 69): its OWN to-hit row and damage
    /// dice (P3 §5.1 columns), rolled on the third stream so player-side streams
    /// keep their offsets — the same separation DropRng exists for. The defense
    /// term is the hero's own Attack Rating (P1 §1.4 convention); a counter that
    /// empties the Life pool kills the hero — the DEAD gate and Respawn live in
    /// GameSession.Death.cs (row 64).
    private void CounterStrike(Monster attacker)
    {
        if (attacker.DamageMax < 1) return; // dice-less foundation builds never counter
        // §6.7 (AMENDED) targeting: the counter reaches the NEAREST valid body
        // among hero + alive mercs — the pure choice lives with the AI's other
        // pure decisions (MonsterAi.AttacksHireling). With no merc hired the
        // answer is always the hero and this block costs nothing (hard RNG law).
        // The defence-term convention carries over unchanged: the target's own
        // AttackRating is its defence term (P1 §1.4, hero and merc alike).
        if (MonsterAi.AttacksHireling(Merc, Merc?.CombatDistance ?? HeroCombatDistance, HeroCombatDistance))
        {
            var m = Merc!;
            if (!CombatResolver.RollHit(EnemyRng, attacker.AttackRating, m.AttackRating,
                    attacker.Level, m.Level))
                return;
            m.TakeDamage(EnemyRng.NextInRange(attacker.DamageMin, attacker.DamageMax));
            return;
        }
        if (!CombatResolver.RollHit(EnemyRng, attacker.AttackRating, Character.AttackRating,
                attacker.Level, Character.Level))
            return;
        Character.TakeDamage(EnemyRng.NextInRange(attacker.DamageMin, attacker.DamageMax));
        // §6.7b THE LAW (MC 11898): the ONE hero-death transition — the life
        // floor above just flipped IsDead (v4 doctrine: IsDead derives from Life
        // at 0) — and the hireling dies WITH him in the same instant (D2
        // KillPlayerPets; amendment §2). Consumes no roll and moves no gold, so
        // the no-merc stream identity and the gold captures stand untouched.
        if (IsDead) KillHirelingOnHeroDeath();
    }
}
