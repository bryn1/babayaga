using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>One mod line of an affix definition: rolled uniform min..max per item (P2 §4.2).</summary>
public sealed record AffixModRange(string Stat, int Min, int Max);

/// <summary>One affix row of data/affixes.json. Level/MaxLevel are the inclusive
/// alvl window (P2 §4.2 tiering); Group is the item-wide exclusion key (one affix
/// per group per item); RareAllowed gates the §4.3 rare pool; Itypes match the
/// base's itype-or-equiv ("*" = any).</summary>
public sealed record AffixDef(
    string Id,
    string Name,
    bool Prefix,
    string Group,
    int Level,
    int MaxLevel,
    int LevelRequirement,
    int Frequency,
    bool RareAllowed,
    IReadOnlyList<string> Itypes,
    IReadOnlyList<AffixModRange> Mods)
{
    public bool Fits(ItemBaseRow baseRow) =>
        Itypes.Count == 0 || Itypes.Contains("*") || Itypes.Any(baseRow.MatchesType);
}

/// <summary>
/// The prefix/suffix pool engine (P2 §4.2): loads data/affixes.json, filters by the
/// alvl window + base type + rare gate + one-affix-per-group exclusion, rolls the
/// magic affix-count table, and assembles a magic item. Weighted pick uses the row
/// frequency as the weight.
/// </summary>
public sealed class AffixPool
{
    public required IReadOnlyList<AffixDef> All { get; init; }

    public static AffixPool Load(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var defs = new List<AffixDef>();
        foreach (var e in doc.RootElement.GetProperty("affixes").EnumerateArray())
        {
            defs.Add(new AffixDef(
                e.GetProperty("id").GetString()!,
                e.GetProperty("name").GetString()!,
                e.GetProperty("prefix").GetBoolean(),
                e.GetProperty("group").GetString()!,
                e.GetProperty("level").GetInt32(),
                e.GetProperty("maxLevel").GetInt32(),
                e.GetProperty("levelReq").GetInt32(),
                e.GetProperty("frequency").GetInt32(),
                e.GetProperty("rareAllowed").GetBoolean(),
                e.GetProperty("itypes").EnumerateArray().Select(t => t.GetString()!).ToList(),
                e.GetProperty("mods").EnumerateArray().Select(ReadModRange).ToList()));
        }
        Validate(defs);
        return new AffixPool { All = defs };
    }

    /// <summary>Shared mod-range reader — affixes, uniques, runes, gems and runeword
    /// grants all use this one {stat,min,max} JSON shape.</summary>
    public static AffixModRange ReadModRange(JsonElement m) => new(
        m.GetProperty("stat").GetString()!,
        m.GetProperty("min").GetInt32(),
        m.GetProperty("max").GetInt32());

    private static void Validate(List<AffixDef> defs)    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in defs)
        {
            if (!seen.Add(a.Id))
                throw new InvalidDataException($"duplicate affix id '{a.Id}'");
            if (a.Level < 1 || a.MaxLevel > 99 || a.MaxLevel < a.Level)
                throw new InvalidDataException($"affix '{a.Id}': alvl window {a.Level}..{a.MaxLevel} invalid (1..99)");
            if (a.Frequency < 1)
                throw new InvalidDataException($"affix '{a.Id}': frequency must weight ≥1");
            foreach (var m in a.Mods)
            {
                if (m.Max < m.Min)
                    throw new InvalidDataException($"affix '{a.Id}': mod '{m.Stat}' max below min");
            }
        }
    }

    /// <summary>Pool filtered to one draw: side, alvl window, base fit, rare gate,
    /// and the item-wide used-group exclusion (P2 §4.2 "one affix per group per item").</summary>
    public IReadOnlyList<AffixDef> Eligible(
        bool prefix, int alvl, ItemBaseRow baseRow, bool forRare, IReadOnlyCollection<string> usedGroups)
    {
        var q = All.Where(a => a.Prefix == prefix
            && a.Level <= alvl && alvl <= a.MaxLevel
            && a.Fits(baseRow)
            && (!forRare || a.RareAllowed)
            && !usedGroups.Contains(a.Group));
        return q.ToList();
    }

    /// <summary>Frequency-weighted pick; null when no affix is eligible.</summary>
    public static AffixDef? Pick(Rng rng, IReadOnlyList<AffixDef> eligible) =>
        PickWeighted(rng, eligible, a => a.Frequency);

    /// <summary>Generic weight draw — affixes by frequency (this file), uniques and
    /// set pieces by rarity class (SetUniqueItems.cs) both roll through it.</summary>
    public static T? PickWeighted<T>(Rng rng, IReadOnlyList<T> pool, Func<T, int> weight)
    {
        if (pool.Count == 0) return default;
        int total = pool.Sum(weight);
        int roll = rng.NextInt(total);
        foreach (var t in pool)
        {
            roll -= weight(t);
            if (roll < 0) return t;
        }
        return pool[^1]; // unreachable: roll < total
    }

    /// <summary>Roll every mod line of one affix instance (P2 §4.2 nummods-style ranges).</summary>
    public static List<ItemMod> RollMods(Rng rng, AffixDef affix) =>
        affix.Mods.Select(m => new ItemMod(m.Stat, rng.NextInRange(m.Min, m.Max))).ToList();

    /// <summary>
    /// Magic affix-count table (P2 §4.2, the fetched GAP's configurable rows):
    /// the leading affix is 50/50 prefix-vs-suffix at any ilvl; the second affix
    /// on each side steps in with probability 0 → 25% → 50% at the ilvl 27 / 42
    /// breakpoints. Result bounds: 1..2 prefixes + 0..2 suffixes, total 1..4.
    /// </summary>
    public static (int Prefixes, int Suffixes) MagicAffixCounts(Rng rng, int ilvl)
    {
        bool leadIsPrefix = rng.NextPercent() < 50;
        int prefixes = leadIsPrefix ? 1 : 0;
        int suffixes = leadIsPrefix ? 0 : 1;
        int secondChance = ilvl >= 42 ? 50 : ilvl >= 27 ? 25 : 0;
        if (rng.NextPercent() < secondChance) prefixes++;
        if (rng.NextPercent() < secondChance) suffixes++;
        return (prefixes, suffixes);
    }

    /// <summary>Attach one rolled affix to the item: name side, mods, used-group set.</summary>
    public static void Apply(Rng rng, Item item, AffixDef affix)
    {
        item.Mods.AddRange(RollMods(rng, affix));
        item.AffixIds.Add(affix.Id);
        if (affix.Prefix) item.PrefixName = affix.Name;
        else item.SuffixName = affix.Name;
    }

    /// <summary>
    /// Build a magic item end to end: count roll → alvl/type/group-filtered picks →
    /// rolled mods (P2 §4.2). White items keep the base's own requirements.
    /// </summary>
    public static Item BuildMagic(Rng rng, ItemBaseRow baseRow, int ilvl, AffixPool pool)
    {
        int alvl = ItemLevels.AffixLevel(ilvl, baseRow.Qlvl, baseRow.MagicLevelAdd);
        var item = new Item(baseRow, ilvl) { Quality = ItemQuality.Magic };
        var used = new HashSet<string>(StringComparer.Ordinal);
        var (pCount, sCount) = MagicAffixCounts(rng, ilvl);
        for (int i = 0; i < pCount; i++)
        {
            var affix = Pick(rng, pool.Eligible(true, alvl, baseRow, forRare: false, used));
            if (affix is null) break;
            used.Add(affix.Group);
            Apply(rng, item, affix);
        }
        for (int i = 0; i < sCount; i++)
        {
            var affix = Pick(rng, pool.Eligible(false, alvl, baseRow, forRare: false, used));
            if (affix is null) break;
            used.Add(affix.Group);
            Apply(rng, item, affix);
        }
        return item;
    }
}
