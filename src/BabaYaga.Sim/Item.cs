namespace BabaYaga.Sim;

/// <summary>Quality tiers a dropped base can land on; tries run unique→set→rare→magic,
/// a failed magic falls to superior/normal/low (P2 §4.1).</summary>
public enum ItemQuality
{
    LowQuality,
    Normal,
    Superior,
    Magic,
    Rare,
    Set,
    Unique,
}

/// <summary>One concrete rolled stat line on a finished item. The Stat vocabulary is
/// open (like DamageKind in the skill table): affixes, uniques and runewords all land
/// their grants here as flat/percent lines the presentation and stat math read.</summary>
public sealed record ItemMod(string Stat, int Value);

/// <summary>
/// A generated item instance — the shape every roll/generator file produces, holding
/// no rules of its own. White items are LowQuality/Normal/Superior (P2 §4.5 cond. 2).
/// </summary>
public sealed class Item
{
    public ItemBaseRow Base { get; init; }
    public int ItemLevel { get; init; }
    public ItemQuality Quality { get; set; } = ItemQuality.Normal;
    public bool Ethereal { get; init; }

    /// <summary>Socket cells rolled for this item (P2 §4.5); SocketedRunes lists rune
    /// ids in insertion order — a runeword needs the row's order exactly (cond. 4).</summary>
    public int Sockets { get; set; }
    public List<string> SocketedRunes { get; } = [];

    public string? RunewordId { get; set; }
    public string? UniqueId { get; set; }
    public string? SetId { get; set; }
    public string? PrefixName { get; set; }
    public string? SuffixName { get; set; }

    /// <summary>Rolled grants (affix/unique/set/runeword mods all aggregate here).</summary>
    public List<ItemMod> Mods { get; } = [];

    /// <summary>Ids of the affixes rolled onto this item, in draw order — the
    /// generation record the group-exclusion rule and presentation are checked against.</summary>
    public List<string> AffixIds { get; } = [];

    /// <summary>Level requirement a rare/crafted item gains from its affixes (P2 §4.3);
    /// 0 for white items, whose requirements are the base's own.</summary>
    public int LevelRequirement { get; set; }

    public int RequiredStrength { get; set; }
    public int RequiredDexterity { get; set; }

    public int CurrentDurability { get; set; }
    public int MaxDurability => Base.Durability;

    public Item(ItemBaseRow baseRow, int itemLevel)
    {
        Base = baseRow;
        ItemLevel = itemLevel;
        RequiredStrength = baseRow.ReqStr;
        RequiredDexterity = baseRow.ReqDex;
        CurrentDurability = baseRow.Durability;
    }

    public bool IsWhite => Quality is ItemQuality.LowQuality or ItemQuality.Normal or ItemQuality.Superior;

    public int ModSum(string stat) => Mods.Where(m => m.Stat == stat).Sum(m => m.Value);

    /// <summary>Display name: unique/set/runeword names replace the base; magic and
    /// rare items compose prefix + base + suffix (presentation only, P2 §4.2).</summary>
    public string DisplayName =>
        RunewordId ?? UniqueId ?? SetId
        ?? $"{PrefixName ?? ""} {Base.Name} {SuffixName ?? ""}".Replace("  ", " ").Trim();
}

/// <summary>
/// Item-level and affix-level math (P2 §4.2). ilvl is never a base property — every
/// source (drop, champion, unique kill, gamble, cube) computes it; alvl gates which
/// affix pools the item may draw from.
/// </summary>
public static class ItemLevels
{
    /// <summary>Special kinds a killing monster can be (P2 §4.2 ilvl sources).</summary>
    public enum KillKind { Drop, Champion, Unique }

    /// <summary>drop = monster level; champion +2; unique/super-unique +3 (P2 §4.2).</summary>
    public static int FromKill(int monsterLevel, KillKind kind) => ClampIlvl(kind switch
    {
        KillKind.Drop => monsterLevel,
        KillKind.Champion => monsterLevel + 2,
        KillKind.Unique => monsterLevel + 3,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    });

    /// <summary>cube: a recipe's fixed lvl, else floor(plvl%×clvl) + floor(ilvl%×input ilvl),
    /// clamped 1..99 (P2 §4.2). The recipe engine (M2 leg) feeds its parameters here.</summary>
    public static int FromCube(int fixedLvl, int playerPercent, int charLevel, int ilvlPercent, int inputIlvl) =>
        fixedLvl >= 0
            ? ClampIlvl(fixedLvl)
            : Math.Clamp(playerPercent * charLevel / 100 + ilvlPercent * inputIlvl / 100, 1, 99);

    public static int ClampIlvl(int ilvl) => Math.Clamp(ilvl, 1, 99);

    /// <summary>
    /// alvl (P2 §4.2): L = max(ilvl, qlvl); alvl = L − floor(qlvl/2) while
    /// L &lt; 99 − floor(qlvl/2); from there alvl = 2L − 99 (the steeper near-99 band);
    /// clamped 1..99. Magic-level-add bases take alvl = L directly.
    /// </summary>
    public static int AffixLevel(int ilvl, int qlvl, bool magicLevelAdd)
    {
        int l = Math.Max(ClampIlvl(ilvl), qlvl);
        if (magicLevelAdd) return Math.Clamp(l, 1, 99);
        int halfQ = qlvl / 2;
        int raw = l < 99 - halfQ ? l - halfQ : 2 * l - 99;
        return Math.Clamp(raw, 1, 99);
    }
}
