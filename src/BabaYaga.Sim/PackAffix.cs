using System.Text.Json;

namespace BabaYaga.Sim;

/// What kind of special group a spawn roll produced (P3 §5.2).
public enum SpecialKind
{
    Plain,
    Champion,
    Unique,
}

/// <summary>
/// One affix row from data/packs.json — the shape shared by the champion-family
/// pool and the unique-modifier pool (P3 §5.2–5.3): named percent modifiers, an
/// optional element, and the two inheritance flags (a behaviour-only affix never
/// passes to minions, P3 §5.4). All keys/names are ORIGINAL Baba Yaga content.
/// </summary>
public sealed record PackAffixRow(
    string Key,
    string Name,
    string Element,
    int ElementPercent,
    int LifePercentAdd,
    int ArPercentAdd,
    int DamagePercentAdd,
    int ResistPercent,
    int SpeedPercent,
    int SizePercent,
    int CrushPercent,
    bool PassesToMinions,
    bool BehaviourOnly);

/// <summary>The band row of a special-group bonus table (P3 §5.2 life/AR/level/XP).</summary>
public readonly record struct SpecialBonus(
    int LifePercentAdd,
    int ArPercentAdd,
    int LevelAdd,
    int XpMultiplier);

/// <summary>
/// Everything loaded from data/packs.json (P3 §5.2–5.4): the special-group roll
/// weights, the pack/minion size ranges, the champion/unique/minion bonus tables
/// per difficulty band, and the two affix pools. The ONLY reader of that file's shape.
/// </summary>
public sealed class PackData
{
    public int UniqueWeight { get; private init; }
    public int ChampionWeight { get; private init; }
    public int ChampionPackMin { get; private init; }
    public int ChampionPackMax { get; private init; }
    public int UniqueMinionMin { get; private init; }
    public int UniqueMinionMax { get; private init; }
    public IReadOnlyList<PackAffixRow> ChampionAffixes { get; private init; } = [];
    public IReadOnlyList<PackAffixRow> UniqueAffixes { get; private init; } = [];
    private SpecialBonus[] ChampionBonusByBand { get; init; } = [];
    private SpecialBonus[] UniqueBonusByBand { get; init; } = [];
    private SpecialBonus[] MinionBonusByBand { get; init; } = [];

    public SpecialBonus ChampionBonus(Difficulty d) => ChampionBonusByBand[(int)d];
    public SpecialBonus UniqueBonus(Difficulty d) => UniqueBonusByBand[(int)d];
    public SpecialBonus MinionBonus(Difficulty d) => MinionBonusByBand[(int)d];

    public static PackData Load(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;
        var roll = root.GetProperty("specialGroupRoll");
        var packSize = root.GetProperty("championPackSize");
        var minions = root.GetProperty("uniqueMinions");
        var data = new PackData
        {
            UniqueWeight = roll.GetProperty("uniqueWeight").GetInt32(),
            ChampionWeight = roll.GetProperty("championWeight").GetInt32(),
            ChampionPackMin = packSize.GetProperty("min").GetInt32(),
            ChampionPackMax = packSize.GetProperty("max").GetInt32(),
            UniqueMinionMin = minions.GetProperty("min").GetInt32(),
            UniqueMinionMax = minions.GetProperty("max").GetInt32(),
            ChampionAffixes = ReadAffixes(root.GetProperty("championAffixes")),
            UniqueAffixes = ReadAffixes(root.GetProperty("uniqueAffixes")),
            ChampionBonusByBand = ReadBonusTable(root.GetProperty("championBonus"), "championBonus"),
            UniqueBonusByBand = ReadBonusTable(root.GetProperty("uniqueBonus"), "uniqueBonus"),
            MinionBonusByBand = ReadBonusTable(root.GetProperty("minionBonus"), "minionBonus"),
        };
        if (data.UniqueWeight <= 0 || data.ChampionWeight <= 0)
            throw new InvalidDataException("special-group weights must be positive");
        if (data.ChampionPackMin < 1 || data.ChampionPackMax < data.ChampionPackMin)
            throw new InvalidDataException("champion pack size range invalid");
        if (data.UniqueMinionMin < 1 || data.UniqueMinionMax < data.UniqueMinionMin)
            throw new InvalidDataException("unique minion size range invalid");
        if (data.ChampionAffixes.Count == 0 || data.UniqueAffixes.Count == 0)
            throw new InvalidDataException("an affix pool is empty");
        return data;
    }

    private static IReadOnlyList<PackAffixRow> ReadAffixes(JsonElement arr)
    {
        var list = new List<PackAffixRow>();
        foreach (var a in arr.EnumerateArray())
            list.Add(new PackAffixRow(
                Key: a.GetProperty("key").GetString() ?? throw new InvalidDataException("affix without key"),
                Name: a.GetProperty("name").GetString() ?? "",
                Element: a.GetProperty("element").GetString() ?? "",
                ElementPercent: a.GetProperty("elementPercent").GetInt32(),
                LifePercentAdd: a.GetProperty("lifePercentAdd").GetInt32(),
                ArPercentAdd: a.GetProperty("arPercentAdd").GetInt32(),
                DamagePercentAdd: a.GetProperty("damagePercentAdd").GetInt32(),
                ResistPercent: a.GetProperty("resistPercent").GetInt32(),
                SpeedPercent: a.GetProperty("speedPercent").GetInt32(),
                SizePercent: a.GetProperty("sizePercent").GetInt32(),
                CrushPercent: a.GetProperty("crushPercent").GetInt32(),
                PassesToMinions: a.GetProperty("passesToMinions").GetBoolean(),
                BehaviourOnly: a.GetProperty("behaviourOnly").GetBoolean()));
        if (list.GroupBy(r => r.Key).Any(g => g.Count() > 1))
            throw new InvalidDataException("duplicate affix key");
        return list;
    }

    /// One bonus table = exactly the three difficulty bands, each named once.
    private static SpecialBonus[] ReadBonusTable(JsonElement arr, string table)
    {
        var rows = new SpecialBonus[3];
        var seen = new bool[3];
        foreach (var b in arr.EnumerateArray())
        {
            string band = b.GetProperty("difficulty").GetString() ?? "";
            int i = band.ToLowerInvariant() switch
            {
                "ordinary" => 0,
                "nightmare" => 1,
                "hell" => 2,
                _ => throw new InvalidDataException($"{table}: unknown band '{band}'"),
            };
            if (seen[i]) throw new InvalidDataException($"{table}: band '{band}' listed twice");
            seen[i] = true;
            rows[i] = new SpecialBonus(
                LifePercentAdd: b.GetProperty("lifePercentAdd").GetInt32(),
                ArPercentAdd: b.GetProperty("arPercentAdd").GetInt32(),
                LevelAdd: b.GetProperty("levelAdd").GetInt32(),
                XpMultiplier: b.GetProperty("xpMultiplier").GetInt32());
        }
        if (seen.Any(s => !s))
            throw new InvalidDataException($"{table}: a difficulty band is missing");
        return rows;
    }
}

/// <summary>
/// The special-group spawn roll and composition (P3 §5.2–5.4): four fifths of
/// special groups are uniques leading minions, the fifth a champion pack; every
/// roll consumes the seeded Rng, so a same-seed replay rebuilds the identical
/// encounter — the DoD determinism leg.
/// </summary>
public static class SpecialGroups
{
    /// The group encounter as rolled: leader row, total unit count (leader
    /// included), leader affix keys, and the affix keys its minions carry.
    public sealed record Encounter(
        SpecialKind Kind,
        string LeaderKey,
        int UnitCount,
        IReadOnlyList<string> LeaderAffixKeys,
        IReadOnlyList<string> MinionAffixKeys);

    /// 4:1 unique-with-minions vs champion pack (P3 §5.2).
    public static SpecialKind RollKind(Rng rng, PackData data) =>
        rng.NextInt(data.UniqueWeight + data.ChampionWeight) < data.UniqueWeight
            ? SpecialKind.Unique
            : SpecialKind.Champion;

    /// Compose one special group: kind, leader row, size, affixes. Ordinary rows
    /// only — boss rows never lead packs.
    public static Encounter Compose(Rng rng, PackData data, MonsterTable table, Difficulty difficulty)
    {
        var candidates = table.Rows.Where(r => !r.IsBoss).ToArray();
        var leader = candidates[rng.NextInt(candidates.Length)];
        return RollKind(rng, data) switch
        {
            SpecialKind.Unique => UniqueEncounter(rng, data, difficulty, leader.Key),
            _ => ChampionEncounter(rng, data, leader.Key),
        };
    }

    private static Encounter UniqueEncounter(Rng rng, PackData data, Difficulty difficulty, string leaderKey)
    {
        var rolled = UniqueRoller.RollAffixes(rng, difficulty, data.UniqueAffixes);
        int minions = MonsterMinions.RollCount(rng, data);
        return new Encounter(
            Kind: SpecialKind.Unique,
            LeaderKey: leaderKey,
            UnitCount: 1 + minions,
            LeaderAffixKeys: rolled.Select(a => a.Key).ToArray(),
            MinionAffixKeys: MonsterMinions.InheritedAffixes(rolled).Select(a => a.Key).ToArray());
    }

    private static Encounter ChampionEncounter(Rng rng, PackData data, string leaderKey)
    {
        var family = data.ChampionAffixes[rng.NextInt(data.ChampionAffixes.Count)];
        int size = rng.NextInRange(data.ChampionPackMin, data.ChampionPackMax);
        // A champion pack is one family: members carry the affix unless it is behaviour-only.
        var minionKeys = family.PassesToMinions ? new[] { family.Key } : Array.Empty<string>();
        return new Encounter(
            Kind: SpecialKind.Champion,
            LeaderKey: leaderKey,
            UnitCount: size,
            LeaderAffixKeys: new[] { family.Key },
            MinionAffixKeys: minionKeys);
    }

    /// Fold a special bonus + affix percents onto scaled stats (P3 §5.2): life and
    /// AR take the bonus table, damage the affix percents, XP the kill-kind multiplier.
    public static SpawnStats ApplySpecial(
        SpawnStats st, SpecialBonus bonus, IReadOnlyList<PackAffixRow> affixes)
    {
        int lifeAdd = bonus.LifePercentAdd, arAdd = bonus.ArPercentAdd, dmgAdd = 0;
        foreach (var a in affixes)
        {
            lifeAdd += a.LifePercentAdd;
            arAdd += a.ArPercentAdd;
            dmgAdd += a.DamagePercentAdd;
        }
        return new SpawnStats(
            Life: st.Life * (100 + lifeAdd) / 100,
            Defense: st.Defense,
            ToHit: st.ToHit * (100 + arAdd) / 100,
            DamageMin: st.DamageMin * (100 + dmgAdd) / 100,
            DamageMax: st.DamageMax * (100 + dmgAdd) / 100,
            Xp: st.Xp * bonus.XpMultiplier);
    }

    /// Look up an affix key across both pools (encounters store keys, call sites need rows).
    public static PackAffixRow FindAffix(PackData data, string key) =>
        data.ChampionAffixes.Concat(data.UniqueAffixes).First(a => a.Key == key);
}
