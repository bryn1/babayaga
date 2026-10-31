namespace BabaYaga.Sim;

/// <summary>
/// Minion logic (P3 §5.4): a unique leads 3–6 minions; minions take the REDUCED
/// bonus row (life +100/75/50 %, AR +45/37/33 %, level +3, XP ×5 from data/packs.json)
/// and carry only the leader's non-behavioural modifiers — effects that change
/// behaviour never pass to minions.
/// </summary>
public static class MonsterMinions
{
    /// The unique's escort size, rolled inside the data range (3–6 in ours).
    public static int RollCount(Rng rng, PackData data) =>
        rng.NextInRange(data.UniqueMinionMin, data.UniqueMinionMax);

    /// What minions never carry (P3 §5.4): behaviour-only rows, and rows the data
    /// flags passesToMinions:false — the same flag the champion path reads (DA
    /// round-2 P2-N1: one filter, both pools honour it).
    public static IReadOnlyList<PackAffixRow> InheritedAffixes(IReadOnlyList<PackAffixRow> leaderAffixes) =>
        leaderAffixes.Where(a => !a.BehaviourOnly && a.PassesToMinions).ToArray();

    /// Spawn the leader's escort at the band's minion bonus: stats are tabled at
    /// spawn level + the bonus level add, then the reduced percent row multiplies
    /// (same order as the champion path: table first, then special multipliers).
    public static List<Monster> Spawn(
        MonsterTable table, MonsterBaseRow row, int spawnLevel,
        PackData data, Difficulty difficulty, IReadOnlyList<PackAffixRow> leaderAffixes, int count)
    {
        var bonus = data.MinionBonus(difficulty);
        var inherited = InheritedAffixes(leaderAffixes);
        var minions = new List<Monster>(count);
        for (int i = 0; i < count; i++) minions.Add(SpawnOne(table, row, spawnLevel, bonus, inherited));
        return minions;
    }

    /// One special unit (leader or minion) at its band bonus. The XP multiplier is
    /// the bonus table's column (champion ×3, unique/minion ×5 — P3 §5.2), so the
    /// unit's KillType stays Normal and the foundation chain never double-counts it.
    public static Monster SpawnOne(
        MonsterTable table, MonsterBaseRow row, int spawnLevel,
        SpecialBonus bonus, IReadOnlyList<PackAffixRow> affixes)
    {
        int level = spawnLevel + bonus.LevelAdd;
        var st = SpecialGroups.ApplySpecial(table.StatsAt(row, level), bonus, affixes);
        return new Monster
        {
            Name = row.Name,
            Level = level,
            Life = st.Life,
            BaseXp = st.Xp,
            KillType = KillType.Normal,
            AttackRating = st.ToHit,
            Defense = st.Defense,
            ElementalResistPercent = row.ResistPercent,
        };
    }
}
