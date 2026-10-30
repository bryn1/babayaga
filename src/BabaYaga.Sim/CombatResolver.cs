namespace BabaYaga.Sim;

/// <summary>
/// Combat resolution over the Sim types: dual-ratio to-hit with the 5/95 clamp (P1 §1.4),
/// block with the Dex−15 term, class factor and 75% cap (P1 §1.5), the physical damage
/// pipeline weapon-ED → flat → %ED → SrcDam (P1 §1.6), the crit-family roll order
/// (P2 §3.3), elemental damage against the 75/95 resist cap with the −100 floor (P2 §3.1),
/// and the kill → XP-chain grant through XpTable/Character. Every roll consumes the
/// seeded Rng, so a whole fight replays identically from its seed.
/// </summary>
public static class CombatResolver
{
    /// To-hit is never below 5% and never above 95% (P1 §1.4).
    public const int MinChance = 5;
    public const int MaxChance = 95;

    /// Elemental resist cap: base 75, raised by +max-resist modifiers to at most 95 (P2 §3.1).
    public const int BaseResistCap = 75;
    public const int ResistCapCeiling = 95;
    public const int ResistFloor = -100;

    /// Block chance never passes 75% (P1 §1.5).
    public const int BlockCap = 75;

    /// chance_to_hit = 100 × 2AR/(AR+DEF) × alvl/(alvl+dlvl), integer floors, clamp 5..95 (P1 §1.4).
    public static int ChanceToHit(long attackRating, long defense, int attackerLevel, int defenderLevel)
    {
        long arDef = attackRating + defense;
        if (arDef <= 0) return MinChance;
        int ratioPart = (int)(200L * attackRating / arDef);
        int levelPart = (int)((long)ratioPart * attackerLevel / (attackerLevel + defenderLevel));
        return Math.Clamp(levelPart, MinChance, MaxChance);
    }

    /// chance_to_block = floor((shield + bonus + classFactor) × (Dex−15) / (2×level)),
    /// capped 75; while running it is cut to one third AFTER the cap (max 25);
    /// no shield means no block at all (P1 §1.5).
    public static int ChanceToBlock(
        int shieldBlockBase, int blockBonus, int classFactor, int totalDex, int characterLevel, bool running)
    {
        if (shieldBlockBase <= 0) return 0;
        long chance = (long)(shieldBlockBase + blockBonus + classFactor)
            * Math.Max(0, totalDex - 15) / (2L * characterLevel);
        chance = Math.Min(chance, BlockCap);
        if (running) chance /= 3;
        return (int)chance;
    }

    /// A melee attack against a running character always hits; block is still rolled (P1 §1.4).
    public static bool RollHit(Rng rng, long attackRating, long defense,
        int attackerLevel, int defenderLevel, bool defenderIsRunning = false) =>
        defenderIsRunning || rng.NextPercent() < ChanceToHit(attackRating, defense, attackerLevel, defenderLevel);

    public static bool RollBlock(Rng rng, int chanceToBlock) =>
        chanceToBlock > 0 && rng.NextPercent() < chanceToBlock;

    /// Crit family rolls in a fixed order — mastery crit, then Critical Strike, then
    /// Deadly Strike — and any success doubles the physical damage exactly once (P2 §3.3).
    public static bool RollDoubled(Rng rng, int critPercent, int criticalStrikePercent, int deadlyStrikePercent) =>
        rng.NextPercent() < critPercent
        || rng.NextPercent() < criticalStrikePercent
        || rng.NextPercent() < deadlyStrikePercent;

    /// hit_phys = (floor(roll × (1+weaponED)) + flat) × (1 + max(−90%, ΣED) + offweaponED)
    /// × SrcDam/128, doubled once on a crit-family success (P1 §1.6, P2 §3.3).
    public static long PhysicalDamage(
        Rng rng,
        int damageMin,
        int damageMax,
        int weaponEnhancedPercent = 0,
        long flatDamage = 0,
        int enhancedDamageSumPercent = 0,
        int offWeaponEnhancedPercent = 0,
        int srcDam = 128,
        bool doubled = false)
    {
        long roll = rng.NextInRange(damageMin, damageMax);
        long afterWeaponEd = roll * (100 + Math.Max(-100, weaponEnhancedPercent)) / 100 + flatDamage;
        long afterEd = afterWeaponEd * (100 + Math.Max(-90, enhancedDamageSumPercent) + offWeaponEnhancedPercent) / 100;
        long result = afterEd * srcDam / 128;
        if (doubled) result *= 2;
        return result;
    }

    /// Player-side elemental: damage × (100 − min(resist, cap)) / 100 with the resist
    /// floored at −100; cap is 75, raised by +max-resist modifiers to at most 95 (P2 §3.1).
    public static long ElementalDamage(long damage, int resistPercent, int maxResistBonusPercent = 0)
    {
        int cap = Math.Min(ResistCapCeiling, BaseResistCap + Math.Max(0, maxResistBonusPercent));
        int resist = Math.Clamp(resistPercent, ResistFloor, cap);
        return damage * (100 - resist) / 100;
    }

    /// What one resolved player strike did; XpGranted is nonzero only on the kill that paid.
    public sealed record StrikeOutcome(bool Hit, bool Blocked, long Damage, bool Killed, long XpGranted);

    /// Player attack against a monster: to-hit roll (running rule honoured), crit-family
    /// roll, physical pipeline, life subtraction, and on kill the full XP chain paid to
    /// the attacker through XpTable.KillXp + Character.GrantXp.
    public static StrikeOutcome ResolvePlayerStrike(
        Rng rng,
        Character attacker,
        Monster defender,
        int damageMin,
        int damageMax,
        int weaponEnhancedPercent = 0,
        long flatDamage = 0,
        int enhancedDamageSumPercent = 0,
        int critPercent = 0,
        int criticalStrikePercent = 0,
        int deadlyStrikePercent = 0,
        int players = 1,
        int bonusExperiencePercent = 0,
        bool defenderIsRunning = false,
        XpTable? ladder = null)
    {
        if (!RollHit(rng, attacker.AttackRating, defender.Defense, attacker.Level, defender.Level, defenderIsRunning))
            return new StrikeOutcome(false, false, 0, false, 0);
        bool doubled = RollDoubled(rng, critPercent, criticalStrikePercent, deadlyStrikePercent);
        long damage = PhysicalDamage(rng, damageMin, damageMax, weaponEnhancedPercent,
            flatDamage, enhancedDamageSumPercent, doubled: doubled);
        defender.Life -= damage;
        bool killed = defender.Life <= 0;
        long xp = 0;
        if (killed && ladder is not null)
        {
            xp = ladder.KillXp(defender.BaseXp, defender.KillType, players,
                attacker.Level, defender.Level, bonusExperiencePercent);
            attacker.GrantXp(xp, ladder);
        }
        return new StrikeOutcome(true, false, damage, killed, xp);
    }
}

/// <summary>
/// Minimal foundation monster: the combat rows the resolver reads — level, life, AR/DEF,
/// one elemental resistance (per-element rows arrive with the data phase), and its
/// kill-XP row. Monster-side resistances are NOT capped; ≥100 is immune (P2 §3.1).
/// </summary>
public sealed class Monster
{
    public required string Name { get; init; }
    public int Level { get; init; } = 1;
    public long Life { get; set; }
    public long BaseXp { get; init; }
    public KillType KillType { get; init; } = KillType.Normal;
    public long AttackRating { get; init; }
    public long Defense { get; init; } = 10;
    public int ElementalResistPercent { get; init; }

    public bool IsDead => Life <= 0;

    /// Elemental hit against this monster: immune (resist ≥ 100) takes none, else the
    /// uncapped monster resist applies (P2 §3.1).
    public long TakeElemental(long damage)
    {
        if (ElementalResistPercent >= 100) return 0;
        long dealt = damage * (100 - ElementalResistPercent) / 100;
        Life -= dealt;
        return dealt;
    }
}
