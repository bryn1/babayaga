using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>
/// One row of the per-level growth table (P3 §5.1, monlvl shape): the stat percents
/// and the absolute XP column in force from StartLevel up to the next row.
/// </summary>
public readonly record struct LevelScaleRow(
    int StartLevel,
    int LifePercent,
    int DefensePercent,
    int ToHitPercent,
    int DamagePercent,
    long XpValue);

/// <summary>
/// The monster table loaded from data/monsters.json (P3 §5.1–5.2): base rows plus
/// the per-level growth rows, the spawn-level resolution (Normal uses the monster's
/// own level; the higher bands stamp ordinary monsters with the AREA's monster level,
/// bosses and keeps-own-level rows keep their own), and a factory handing the
/// foundation's combat Monster fully scaled stats.
/// </summary>
public sealed class MonsterTable
{
    private readonly Dictionary<string, MonsterBaseRow> _rows;
    private readonly LevelScaleRow[] _scales;

    public IReadOnlyList<MonsterBaseRow> Rows { get; }
    public IReadOnlyList<LevelScaleRow> Scales => _scales;

    private MonsterTable(List<MonsterBaseRow> rows, LevelScaleRow[] scales)
    {
        Rows = rows;
        _rows = rows.ToDictionary(r => r.Key);
        _scales = scales;
    }

    /// Parses and validates: unique keys, known ai keys, levels ≥ 1, percent columns
    /// positive, growth rows sorted with the first covering level 1.
    public static MonsterTable Load(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var scales = new List<LevelScaleRow>();
        foreach (var s in doc.RootElement.GetProperty("growth").EnumerateArray())
            scales.Add(new LevelScaleRow(
                StartLevel: s.GetProperty("startLevel").GetInt32(),
                LifePercent: s.GetProperty("lifePercent").GetInt32(),
                DefensePercent: s.GetProperty("defensePercent").GetInt32(),
                ToHitPercent: s.GetProperty("toHitPercent").GetInt32(),
                DamagePercent: s.GetProperty("damagePercent").GetInt32(),
                XpValue: s.GetProperty("xpValue").GetInt64()));
        if (scales.Count == 0 || scales[0].StartLevel != 1)
            throw new InvalidDataException("growth table must start at level 1");
        for (int i = 1; i < scales.Count; i++)
            if (scales[i].StartLevel <= scales[i - 1].StartLevel)
                throw new InvalidDataException($"growth row {i} not sorted above level {scales[i - 1].StartLevel}");

        var rows = new List<MonsterBaseRow>();
        foreach (var m in doc.RootElement.GetProperty("monsters").EnumerateArray())
        {
            string key = m.GetProperty("key").GetString()
                ?? throw new InvalidDataException("monster without key");
            string ai = m.GetProperty("ai").GetString() ?? "";
            rows.Add(new MonsterBaseRow(
                Key: key,
                Name: m.GetProperty("name").GetString() ?? key,
                Ai: ParseAi(ai),
                OwnLevel: m.GetProperty("ownLevel").GetInt32(),
                IsBoss: m.GetProperty("boss").GetBoolean(),
                KeepsOwnLevel: m.GetProperty("keepsOwnLevel").GetBoolean(),
                BaseLife: m.GetProperty("baseLife").GetInt64(),
                BaseDefense: m.GetProperty("baseDefense").GetInt64(),
                BaseToHit: m.GetProperty("baseToHit").GetInt64(),
                DamageMin: m.GetProperty("damageMin").GetInt32(),
                DamageMax: m.GetProperty("damageMax").GetInt32(),
                LifePercent: m.GetProperty("lifePercent").GetInt32(),
                DefensePercent: m.GetProperty("defensePercent").GetInt32(),
                ToHitPercent: m.GetProperty("toHitPercent").GetInt32(),
                DamagePercent: m.GetProperty("damagePercent").GetInt32(),
                XpPercent: m.GetProperty("xpPercent").GetInt32(),
                ResistPercent: m.GetProperty("resistPercent").GetInt32()));
        }
        if (rows.Count == 0) throw new InvalidDataException("no monster rows");
        if (rows.GroupBy(r => r.Key).Any(g => g.Count() > 1))
            throw new InvalidDataException("duplicate monster key");
        if (rows.Any(r => r.OwnLevel < 1 || r.LifePercent <= 0 || r.XpPercent <= 0))
            throw new InvalidDataException("monster row with level/percent out of range");
        return new MonsterTable(rows, scales.ToArray());
    }

    private static AiBehaviour ParseAi(string ai) => ai.ToLowerInvariant() switch
    {
        "melee" => AiBehaviour.Melee,
        "ranged" => AiBehaviour.Ranged,
        "charge" => AiBehaviour.Charge,
        "avoid" => AiBehaviour.Avoid,
        _ => throw new InvalidDataException($"unknown ai key '{ai}'"),
    };

    public MonsterBaseRow Get(string key) => _rows[key];

    /// The growth row in force at a level (last StartLevel at or below it).
    public LevelScaleRow ScaleFor(int level)
    {
        var row = _scales[0];
        foreach (var s in _scales)
        {
            if (s.StartLevel > level) break;
            row = s;
        }
        return row;
    }

    public SpawnStats StatsAt(MonsterBaseRow row, int level) =>
        MonsterBase.ApplyScale(row, ScaleFor(level));

    /// Spawn-level resolution (P3 §5.1): Ordinary always uses the monster's own
    /// level; Nightmare/Hell stamp ordinary monsters with the area's monster level,
    /// while bosses and keeps-own-level rows keep their own.
    public static int ResolveSpawnLevel(MonsterBaseRow row, Difficulty difficulty, AreaRow area)
    {
        if (difficulty == Difficulty.Ordinary || row.IsBoss || row.KeepsOwnLevel)
            return row.OwnLevel;
        return difficulty == Difficulty.Nightmare ? area.MonLvlNightmare : area.MonLvlHell;
    }

    /// The foundation combat unit at full table stats (kill-type Normal; specials
    /// wrap this with their bonus multipliers in PackAffix/UniqueMonster).
    public Monster Spawn(MonsterBaseRow row, int level)
    {
        var st = StatsAt(row, level);
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
