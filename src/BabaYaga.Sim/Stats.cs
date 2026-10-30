namespace BabaYaga.Sim;

/// <summary>Weapon groups for the per-point attribute damage bonus (P1 §1.6).</summary>
public enum WeaponGroup
{
    /// Swords, axes, maces, sceptres, wands, staves, polearms, spears, orbs: 1% dmg per Str.
    BladeOrBluntOrStaff,
    /// Hammers and mauls: 1.1% dmg per Str.
    HammerOrMaul,
    /// Daggers, throwing knives/axes, javelins, claws: 0.75% per Str AND per Dex.
    LightOrThrownOrClaw,
    /// Bows and crossbows: 1% dmg per Dex, none from Str.
    BowOrCrossbow,
}

/// <summary>
/// One class row: starting primaries + per-level/per-point growth rates + the block
/// class factor (P1 §1.2–1.3, §1.5). Numbers for Koldun are ORIGINAL Baba Yaga design,
/// not a copy of any reference-table row.
/// </summary>
public sealed record ClassProfile(
    string Key,
    int StartStr, int StartDex, int StartEner, int StartVit,
    int LifePerLevel, int ManaPerLevel,
    int LifePerVit, int ManaPerEner,
    int BlockClassFactor)
{
    /// Life at level 1 = Vitality + the flat 30 (P1 §1.2).
    public int LifeAtLevel1 => StartVit + Stats.LifeFlatAtLevel1;

    /// Starting mana is the Energy (P1 §1.2).
    public int ManaAtLevel1 => StartEner;
}

/// <summary>
/// Primary attributes → derived stats: life, mana, attack rating, per-weapon attribute
/// damage (P1 §1.2–1.4, §1.6). Multipliers floor to integer; item-sourced attributes
/// join after the Increase-Max multiplier (P1 §1.3).
/// </summary>
public static class Stats
{
    /// Flat life every class starts with (P1 §1.2).
    public const int LifeFlatAtLevel1 = 30;

    /// The foundation's first playable class: a Slavic hedge-witch who haggles with the
    /// forest. Design row (original): lean mana-caster with a middling frame.
    public static ClassProfile Koldun { get; } = new(
        Key: "koldun",
        StartStr: 15, StartDex: 20, StartEner: 30, StartVit: 20,
        LifePerLevel: 2, ManaPerLevel: 2,
        LifePerVit: 2, ManaPerEner: 2,
        BlockClassFactor: 20);

    public static ClassProfile For(string classKey) => classKey.ToLowerInvariant() switch
    {
        "koldun" => Koldun,
        _ => throw new ArgumentException($"unknown class key '{classKey}' (built so far: koldun)"),
    };

    /// base_life = life@1 + LifePerLevel×(level−1) + LifePerVit×allocated Vit + flat "+Life".
    public static long BaseLife(ClassProfile c, int level, int vitPoints, long flatLife) =>
        c.LifeAtLevel1 + (long)c.LifePerLevel * (level - 1) + (long)c.LifePerVit * vitPoints + flatLife;

    /// max_life = (base_life) × (1 + IncreaseMaxLife%) + LifePerVit × Vit-from-items (P1 §1.3).
    public static long MaxLife(
        ClassProfile c, int level, int vitPoints, int vitFromItems, long flatLife, int increaseMaxLifePercent) =>
        BaseLife(c, level, vitPoints, flatLife) * (100 + increaseMaxLifePercent) / 100
        + (long)c.LifePerVit * vitFromItems;

    /// base_mana = mana@1 + ManaPerLevel×(level−1) + ManaPerEner×allocated Ener + flat "+Mana".
    public static long BaseMana(ClassProfile c, int level, int enerPoints, long flatMana) =>
        c.ManaAtLevel1 + (long)c.ManaPerLevel * (level - 1) + (long)c.ManaPerEner * enerPoints + flatMana;

    /// max_mana = (base_mana) × (1 + IncreaseMaxMana%) + ManaPerEner × Ener-from-items (P1 §1.3).
    public static long MaxMana(
        ClassProfile c, int level, int enerPoints, int enerFromItems, long flatMana, int increaseMaxManaPercent) =>
        BaseMana(c, level, enerPoints, flatMana) * (100 + increaseMaxManaPercent) / 100
        + (long)c.ManaPerEner * enerFromItems;

    /// AR = weapon AR + 5×Dex + flats, then × (1 + %AR) (P1 §1.4).
    public static long AttackRating(int totalDex, long weaponBaseAr, long flatAr, int arPercent) =>
        (weaponBaseAr + 5L * totalDex + flatAr) * (100 + arPercent) / 100;

    /// Attribute damage bonus in % for a weapon group, integer math per point (P1 §1.6):
    /// 1%/Str; 1.1%/Str; 0.75% each per Str+Dex; 1%/Dex for bows.
    public static int WeaponAttributeDamagePercent(WeaponGroup group, int strength, int dexterity) =>
        group switch
        {
            WeaponGroup.BladeOrBluntOrStaff => strength,
            WeaponGroup.HammerOrMaul => 11 * strength / 10,
            WeaponGroup.LightOrThrownOrClaw => 3 * (strength + dexterity) / 4,
            WeaponGroup.BowOrCrossbow => dexterity,
            _ => throw new ArgumentOutOfRangeException(nameof(group)),
        };
}
