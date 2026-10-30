namespace BabaYaga.Sim;

/// <summary>
/// One monster's base row (P3 §5.1, monstats shape): identity, the AI key, its own
/// level (the Normal-era spawn level), the boss / keeps-own-level flags, the base
/// stats the growth table multiplies, its OWN percent columns, and one elemental
/// resistance. All rows are ORIGINAL Baba Yaga content (data/monsters.json);
/// the reference tables were read for SHAPE only (R2 clean-room wall).
/// </summary>
public sealed record MonsterBaseRow(
    string Key,
    string Name,
    AiBehaviour Ai,
    int OwnLevel,
    bool IsBoss,
    bool KeepsOwnLevel,
    long BaseLife,
    long BaseDefense,
    long BaseToHit,
    int DamageMin,
    int DamageMax,
    int LifePercent,
    int DefensePercent,
    int ToHitPercent,
    int DamagePercent,
    int XpPercent,
    int ResistPercent);

/// <summary>
/// A monster's final stats at a resolved spawn level (P3 §5.1):
/// final = base × growth-table(level) × own percent ÷ 100 (integer floor per stat).
/// </summary>
public readonly record struct SpawnStats(
    long Life,
    long Defense,
    long ToHit,
    int DamageMin,
    int DamageMax,
    long Xp);

/// <summary>The stat math the row type owns; one call site, one reading place.</summary>
public static class MonsterBase
{
    /// Applies one growth-table row and the monster's own percent columns.
    public static SpawnStats ApplyScale(MonsterBaseRow row, LevelScaleRow scale)
    {
        long lifePct = (long)scale.LifePercent * row.LifePercent / 100;
        long defPct = (long)scale.DefensePercent * row.DefensePercent / 100;
        long toHitPct = (long)scale.ToHitPercent * row.ToHitPercent / 100;
        long dmgPct = (long)scale.DamagePercent * row.DamagePercent / 100;
        return new SpawnStats(
            Life: row.BaseLife * lifePct / 100,
            Defense: row.BaseDefense * defPct / 100,
            ToHit: row.BaseToHit * toHitPct / 100,
            DamageMin: (int)(row.DamageMin * dmgPct / 100),
            DamageMax: (int)(row.DamageMax * dmgPct / 100),
            // XP is the growth row's own level-column × the monster's XP percent (P3 §5.1).
            Xp: scale.XpValue * row.XpPercent / 100);
    }
}
