namespace BabaYaga.Sim;

/// <summary>
/// The first active-skill slice (LEDGER row 78, M4-S7): the public Cast verb that
/// makes the Mana pool REAL — the pool was a formula + potion target until the
/// koldun started paying it to weave. Sibling of the core file's command
/// contract: every gate either passes or throws SessionRejectedException with
/// nothing half-applied. Gates fire IN ORDER — dead (the eighth gated verb, the
/// row-64 gate speaks before every other precondition), unknown weave, class
/// (the loaded table must be the koldun's — other classes stay at the sword
/// until their own wave), prereq unmet per the data file, the weave never
/// learned (the existing SpendSkill state), the pool below the ORIGINAL-row
/// cost. EFFECT: ember damage is a uniform(min..max) roll on the seeded combat
/// stream; the data's synergy percent-per-hard-point multiplies it as
/// roll × (100 + Σ percent) / 100 — ONE integer floor at the end, never a
/// per-line rounding; the kill then runs the SAME KillFlow chain Attack pays
/// (table XP + drop queue unchanged); mana falls by exactly the cost.
/// ELEMENTAL WEAVING NEVER MISSES — there is deliberately no to-hit roll:
/// the never-miss rule is verified behaviour of the D2 oracle (a RULE, not a
/// number — cf. the run's T0b-mechanics-spec P1 §1.4, where the to-hit roll is
/// the weapon-attack convention; spells sit outside that convention). Monster
/// elemental RESISTANCE is not this beat's row (rows 26–27 stay BUILDING) —
/// the rolled ember lands whole on the target's Life. Mana REGEN is LIVE (row 79,
/// M4-S10b, amended spec §1.8): RestTick restores the pool by the D2 quantum; the
/// belt's napitek-manny stays the instant refill beside it. LIFE never regens — the
/// potion economy is the design (D2 rule).
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The only weaving class until the 6-class wave lands its own
    /// tables: the gate reads the LOADED table's classKey, so a session built
    /// on a foreign weave table (or, later, a foreign profile) never Casts.</summary>
    public const string WeaverKey = "koldun";

    /// <summary>What one cast did — the headless cast leg prints its CAST line
    /// from these values (mana before/after, the floored ember, the target's
    /// Life after the weave, killed). TargetLife is the engaged Life AS IT
    /// LANDS (≤ 0 on the killing weave), read before the engagement clears.</summary>
    public sealed record CastReport(
        string SkillId, long ManaBefore, long ManaAfter, long Damage, long TargetLife, bool Killed);

    /// <summary>Weaves a named skill at a named monster row. Gates in order:
    /// DEAD gate (eighth verb) → unknown weave → foreign weave table → data
    /// prereq unmet → the weave learned with no point spent → pool below cost.
    /// Then the effect: engagement exactly as Attack holds it (switching target
    /// abandons the current fight), the ember roll + synergy floor, the kill
    /// chain through KillFlow, and the mana debit. A survivor of a cast does
    /// NOT counter this beat — hit-recovery and the counter timing join with
    /// rows 23–25 (frames), not with the spell.</summary>
    public CastReport Cast(string skillId, string monsterKey)
    {
        RaiseIfDead(); // the eighth gated verb: the dead hero never weaves (row 64)
        var def = SkillData.Find(skillId)
            ?? throw new SessionRejectedException($"there is no weave named '{skillId}'");
        if (SkillData.ClassKey != WeaverKey)
            throw new SessionRejectedException("the koldun weaves; the others swing");
        foreach (var pre in def.Prereqs)
            if (Skills.HardPoints(pre.SkillId) < pre.Points)
                throw new SessionRejectedException(
                    $"'{skillId}' waits for {pre.Points} point(s) in '{pre.SkillId}' first");
        if (!Skills.Knows(skillId))
            throw new SessionRejectedException(
                $"'{skillId}' was never woven — spend a skill point into it first");
        if (Character.Mana < def.ManaCost)
            throw new SessionRejectedException(
                $"too little mana to weave '{skillId}' (cost {def.ManaCost}, pool {Character.Mana})");

        var row = Monsters.Rows.FirstOrDefault(r => r.Key == monsterKey)
            ?? throw new SessionRejectedException($"unknown monster '{monsterKey}'");
        if (Engaged is not null && EngagedKey != monsterKey) { Engaged = null; EngagedKey = null; }
        var area = Areas.Get(CurrentArea);
        Engaged ??= Spawn(row, area);

        long manaBefore = Character.Mana;
        // Elemental NEVER MISSES: no RollHit here — the never-miss rule is the
        // D2-verified behaviour for elemental spells (the run's oracle study,
        // cf. T0b-mechanics-spec P1 §1.4 for the weapon-attack to-hit
        // convention this deliberately does not use).
        long roll = CombatRng.NextInRange(def.DamageMin, def.DamageMax);
        // Synergy math (P1 §2.2 read as design): hard-point percent applies to
        // the whole roll and floors ONCE at the single division below.
        long damage = roll * (100 + Skills.SynergyBonusPercent(skillId)) / 100;
        Engaged.Life -= damage; // resistance rows (26–27) join later, not here
        bool killed = Engaged.Life <= 0;
        long xp = 0;
        if (killed)
        {
            // The SAME table chain the physical strike pays (KillFlow idiom):
            // XpTable.KillXp through Character.GrantXp — no spell XP tax.
            xp = Ladder.KillXp(Engaged.BaseXp, Engaged.KillType, 1, Character.Level, Engaged.Level);
            Character.GrantXp(xp, Ladder);
        }
        _flow.OnKill(DropRng, new CombatResolver.StrikeOutcome(true, false, damage, killed, xp),
            EngagedKind, EngagedAffixKeys, area.Level, AreaSeed(area), TcKeyFor(area.Level),
            MagicFind, GoldFind); // non-kill: OnKill returns null and consumes nothing
        Character.Mana -= def.ManaCost;

        long targetLife = Engaged.Life;
        if (killed) { Engaged = null; EngagedKey = null; }
        return new CastReport(skillId, manaBefore, Character.Mana, damage, targetLife, killed);
    }
}
