using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>One prerequisite edge: the skill needs at least this many hard points in SkillId (P1 §2.1).</summary>
public sealed record SkillPrereq(string SkillId, int Points);

/// <summary>One synergy edge: each HARD point in SourceId adds PercentPerPoint % to the
/// skill's synergy line; +skill bonuses never inflate it (P1 §2.2).</summary>
public sealed record SkillSynergy(string SourceId, int PercentPerPoint);

/// <summary>One skill row of a class skill table (data/koldun-skills.json shape).
/// Mechanical fields only — presentation reads names/flavor later.</summary>
public sealed record SkillDef(
    string Id,
    string Name,
    string Tree,
    int Row,
    int Col,
    int MaxPoints,
    string DamageKind,
    int DamageMin,
    int DamageMax,
    IReadOnlyList<SkillPrereq> Prereqs,
    IReadOnlyList<SkillSynergy> Synergies,
    int ManaCost)
{
    /// Grid tier is the 1-based row; tier N unlocks at character level 6×(N−1) (P1 §2.1).
    public int Tier => Row + 1;

    public int LevelRequirement => 6 * Row;
}

/// <summary>A whole class's skill grid, one JSON file per class (the content source of truth).</summary>
public sealed class ClassSkillData
{
    public required string ClassKey { get; init; }
    public required string ClassName { get; init; }
    public required IReadOnlyList<SkillDef> Skills { get; init; }

    public SkillDef? Find(string id) => Skills.FirstOrDefault(s => s.Id == id);

    public IReadOnlyList<string> TreeNames { get; init; } = [];
}

/// <summary>
/// Loads and validates a class skill table. The validation IS the content gate:
/// unique ids, grid cells unique per tree, rows 0..7, prereq/synergy sources exist,
/// prereq edges point to an earlier tier, at most 3 prereqs (P1 §2.1), and each row's
/// max points equal the tier cap (3/2/2/1 by tier — the original Baba Yaga design rule).
/// </summary>
public static class SkillDataLoader
{
    /// Hard-point cap per grid tier (original design: deep rows are specialised).
    public static int MaxPointsForTier(int tier) => tier <= 1 ? 3 : tier == 2 ? 2 : tier == 3 ? 2 : 1;

    public static ClassSkillData Load(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;
        var classKey = root.GetProperty("classKey").GetString()!;
        var className = root.GetProperty("className").GetString()!;

        var skills = new List<SkillDef>();
        foreach (var s in root.GetProperty("skills").EnumerateArray())
        {
            var id = s.GetProperty("id").GetString()!;
            var prereqs = s.GetProperty("prereqs").EnumerateArray()
                .Select(p => new SkillPrereq(p.GetProperty("skillId").GetString()!, p.GetProperty("points").GetInt32()))
                .ToList();
            var synergies = s.GetProperty("synergies").EnumerateArray()
                .Select(y => new SkillSynergy(y.GetProperty("sourceId").GetString()!, y.GetProperty("percentPerPoint").GetInt32()))
                .ToList();
            skills.Add(new SkillDef(
                id,
                s.GetProperty("name").GetString()!,
                s.GetProperty("tree").GetString()!,
                s.GetProperty("row").GetInt32(),
                s.GetProperty("col").GetInt32(),
                s.GetProperty("maxPoints").GetInt32(),
                s.TryGetProperty("damageKind", out var dk) ? dk.GetString()! : "none",
                s.TryGetProperty("damageMin", out var dmin) ? dmin.GetInt32() : 0,
                s.TryGetProperty("damageMax", out var dmax) ? dmax.GetInt32() : 0,
                prereqs,
                synergies,
                s.TryGetProperty("manaCost", out var mc) ? mc.GetInt32() : 0));
        }
        var trees = root.GetProperty("trees").EnumerateArray()
            .Select(t => t.GetProperty("id").GetString()!).ToList();

        Validate(classKey, trees, skills);
        return new ClassSkillData { ClassKey = classKey, ClassName = className, Skills = skills, TreeNames = trees };
    }

    private static void Validate(string classKey, IReadOnlyList<string> trees, List<SkillDef> skills)
    {
        var byId = new HashSet<string>();
        var cells = new HashSet<(string, int, int)>();
        foreach (var s in skills)
        {
            if (!byId.Add(s.Id))
                throw new InvalidDataException($"{classKey}: duplicate skill id '{s.Id}'");
            if (!trees.Contains(s.Tree))
                throw new InvalidDataException($"{classKey}: skill '{s.Id}' names unknown tree '{s.Tree}'");
            if (s.Row is < 0 or > 7 || s.Col is < 0 or > 7)
                throw new InvalidDataException($"{classKey}: skill '{s.Id}' outside the 8x8 grid");
            if (!cells.Add((s.Tree, s.Row, s.Col)))
                throw new InvalidDataException($"{classKey}: two skills share cell {s.Tree} r{s.Row}c{s.Col}");
            if (s.MaxPoints != MaxPointsForTier(s.Tier))
                throw new InvalidDataException(
                    $"{classKey}: skill '{s.Id}' maxPoints {s.MaxPoints} breaks the tier-{s.Tier} cap {MaxPointsForTier(s.Tier)}");
            if (s.DamageMin > s.DamageMax || s.DamageMin < 0)
                throw new InvalidDataException($"{classKey}: skill '{s.Id}' damage range invalid");
            if (s.ManaCost < 0)
                throw new InvalidDataException($"{classKey}: skill '{s.Id}' manaCost {s.ManaCost} is negative");
        }
        foreach (var s in skills)
        {
            if (s.Prereqs.Count > 3)
                throw new InvalidDataException($"{classKey}: skill '{s.Id}' has more than 3 prerequisites");
            foreach (var p in s.Prereqs)
            {
                var pre = skills.FirstOrDefault(x => x.Id == p.SkillId)
                    ?? throw new InvalidDataException($"{classKey}: skill '{s.Id}' prereq '{p.SkillId}' does not exist");
                if (p.Points < 1)
                    throw new InvalidDataException($"{classKey}: skill '{s.Id}' prereq '{p.SkillId}' needs at least 1 point");
                if (pre.Row >= s.Row)
                    throw new InvalidDataException($"{classKey}: skill '{s.Id}' prereq '{p.SkillId}' is not on an earlier row");
            }
            foreach (var y in s.Synergies)
            {
                if (y.SourceId == s.Id || skills.All(x => x.Id != y.SourceId))
                    throw new InvalidDataException($"{classKey}: skill '{s.Id}' synergy source '{y.SourceId}' invalid");
            }
        }
    }
}
