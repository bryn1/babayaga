using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>Kill-kind XP multipliers in percent (P1 §1.7: champion ×3, unique/unique-minion ×5).</summary>
public enum KillType
{
    Normal = 100,
    Champion = 300,
    Unique = 500,
}

/// <summary>
/// The XP ladder loaded from data/xp-ladder.json: per-level thresholds, ExpRatio curve,
/// level-up grants, the level-gap tables in both directions, and the whole kill-XP chain
/// xp = base × killtype × (players+1)/2 × gap × ExpRatio/1024 × (1+bonus%), flooring
/// every step (P1 §1.7). Lvl 99 gains nothing (ratio 0 there).
/// </summary>
public sealed class XpTable
{
    private readonly long[] _totalXpToReach; // indexed by level (1..MaxLevel)
    private readonly int[] _expRatio;        // indexed by level (1..MaxLevel)

    public int MaxLevel { get; }
    public int StatGrantPerLevel { get; }
    public int SkillGrantPerLevel { get; }

    private XpTable(long[] toReach, int[] ratio, int maxLevel, int statGrant, int skillGrant)
    {
        _totalXpToReach = toReach;
        _expRatio = ratio;
        MaxLevel = maxLevel;
        StatGrantPerLevel = statGrant;
        SkillGrantPerLevel = skillGrant;
    }

    /// Parses and validates a ladder file: every level 1..maxLevel present, level 1 at 0
    /// XP, thresholds strictly increasing, ratios in 0..1024.
    public static XpTable Load(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;
        int statGrant = root.GetProperty("statGrantPerLevel").GetInt32();
        int skillGrant = root.GetProperty("skillGrantPerLevel").GetInt32();
        int maxLevel = root.GetProperty("maxLevel").GetInt32();
        if (maxLevel < 2) throw new InvalidDataException("maxLevel below 2");

        var toReach = new long[maxLevel + 1];
        var ratio = new int[maxLevel + 1];
        long prevXp = -1;
        int seen = 0;
        foreach (var row in root.GetProperty("levels").EnumerateArray())
        {
            int lvl = row.GetProperty("level").GetInt32();
            long xp = row.GetProperty("totalXp").GetInt64();
            int r = row.GetProperty("expRatio").GetInt32();
            if (lvl < 1 || lvl > maxLevel)
                throw new InvalidDataException($"ladder level {lvl} outside 1..{maxLevel}");
            if (xp <= prevXp)
                throw new InvalidDataException($"ladder level {lvl} threshold not above the previous row");
            if (r < 0 || r > 1024)
                throw new InvalidDataException($"ladder level {lvl} ExpRatio {r} outside 0..1024");
            toReach[lvl] = xp;
            ratio[lvl] = r;
            prevXp = xp;
            seen++;
        }
        if (seen != maxLevel)
            throw new InvalidDataException($"ladder lists {seen} rows, expected every level 1..{maxLevel}");
        if (toReach[1] != 0)
            throw new InvalidDataException("level 1 threshold must be 0 XP");
        return new XpTable(toReach, ratio, maxLevel, statGrant, skillGrant);
    }

    /// Total XP needed to stand at this level (level 1 = 0; above max clamps to the cap).
    public long TotalXpForLevel(int level) =>
        level <= 1 ? 0 : _totalXpToReach[Math.Min(level, MaxLevel)];

    /// The level a character with this much total XP stands at.
    public int LevelForXp(long xp)
    {
        int lvl = 1;
        while (lvl < MaxLevel && xp >= _totalXpToReach[lvl + 1]) lvl++;
        return lvl;
    }

    public int ExpRatioForLevel(int level) => _expRatio[Math.Clamp(level, 1, MaxLevel)];

    /// Level-gap factor in percent, both directions (P1 §1.7): a monster at/below you
    /// pays full within 5 levels then 81/62/43/24/5 % per further 5-level band; above you
    /// (under clvl 25) 88/68/36/15/2 %; from clvl 25 it is your level ÷ its level.
    public static int LevelGapFactorPercent(int characterLevel, int monsterLevel)
    {
        if (monsterLevel <= characterLevel)
        {
            int below = characterLevel - monsterLevel;
            return below <= 5 ? 100
                : below <= 10 ? 81
                : below <= 15 ? 62
                : below <= 20 ? 43
                : below <= 25 ? 24
                : 5;
        }
        if (characterLevel < 25)
        {
            int above = monsterLevel - characterLevel;
            return above <= 5 ? 88 : above <= 10 ? 68 : above <= 15 ? 36 : above <= 20 ? 15 : 2;
        }
        return 100 * characterLevel / monsterLevel;
    }

    /// The full kill-XP chain with an integer floor at every step (P1 §1.7).
    /// players = the game's player count, 1..8 (each extra player adds ½, to 4.5×).
    public long KillXp(
        long monsterBaseXp, KillType killType, int players,
        int characterLevel, int monsterLevel, int bonusExperiencePercent = 0)
    {
        if (monsterBaseXp < 0) throw new ArgumentOutOfRangeException(nameof(monsterBaseXp));
        players = Math.Clamp(players, 1, 8);
        long xp = monsterBaseXp;
        xp = xp * (long)killType / 100;
        xp = xp * (players + 1) / 2;
        xp = xp * LevelGapFactorPercent(characterLevel, monsterLevel) / 100;
        xp = xp * ExpRatioForLevel(characterLevel) / 1024;
        xp = xp * (100 + bonusExperiencePercent) / 100;
        return xp;
    }
}
