namespace BabaYaga.Sim;

/// <summary>Rejected skill spend; the Reason is the reviewable rejection text.</summary>
public sealed class SkillSpendRejectedException(string reason) : Exception(reason)
{
    public string Reason => Message;
}

/// <summary>
/// One character's skill grid: hard points per skill, spend validation (row level
/// requirement 6×tier-step, prerequisite point edges, per-tier point cap, skill-point
/// budget), and the two level reads every skill line uses — own lines read the
/// effective level (hard points + all +skill bonuses), synergy lines read hard points
/// only (P1 §2.1–2.2). A rejected spend never moves state.
/// </summary>
public sealed class SkillTree
{
    private readonly ClassSkillData _data;
    private readonly Dictionary<string, int> _hardPoints = new();

    /// Flat "+X to Skills" pool from items/quests (M2 content); own lines read it,
    /// synergy lines never do (P1 §2.2).
    public int PlusSkillLevels { get; set; }

    public SkillTree(ClassSkillData data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        foreach (var skill in data.Skills)
            _hardPoints[skill.Id] = 0;
    }

    public int HardPoints(string skillId) => _hardPoints.TryGetValue(skillId, out var p)
        ? p
        : throw new ArgumentException($"unknown skill '{skillId}' for class '{_data.ClassKey}'");

    /// Level an own-line of this skill reads: hard points + every +skills bonus (P1 §2.2).
    public int EffectiveLevel(string skillId) => HardPoints(skillId) + PlusSkillLevels;

    /// Synergy line bonus in %: Σ source hard points × per-point %, hard points only —
    /// no +skill moves it (P1 §2.2).
    public int SynergyBonusPercent(string skillId)
    {
        var def = Require(skillId);
        int sum = 0;
        foreach (var edge in def.Synergies)
            sum += HardPoints(edge.SourceId) * edge.PercentPerPoint;
        return sum;
    }

    public bool Knows(string skillId) => HardPoints(skillId) > 0;

    /// Spends exactly one hard point on the skill. Throws SkillSpendRejectedException
    /// naming the failed rule — unknown skill, row level requirement, an unmet
    /// prerequisite, the per-tier point cap, or an empty skill-point budget.
    public void Spend(Character character, string skillId)
    {
        var def = Require(skillId);
        if (character.Level < def.LevelRequirement)
            throw new SkillSpendRejectedException(
                $"'{skillId}' needs character level {def.LevelRequirement}, character is {character.Level}");
        foreach (var pre in def.Prereqs)
        {
            if (HardPoints(pre.SkillId) < pre.Points)
                throw new SkillSpendRejectedException(
                    $"'{skillId}' requires {pre.Points} point(s) in '{pre.SkillId}', it has {HardPoints(pre.SkillId)}");
        }
        int cap = SkillDataLoader.MaxPointsForTier(def.Tier);
        if (HardPoints(skillId) + 1 > cap)
            throw new SkillSpendRejectedException(
                $"'{skillId}' is capped at {cap} point(s) at tier {def.Tier}");
        if (!character.TrySpendSkillPoint())
            throw new SkillSpendRejectedException("no skill point available");
        _hardPoints[skillId]++;
    }

    private SkillDef Require(string skillId) =>
        _data.Find(skillId) ?? throw new ArgumentException($"unknown skill '{skillId}' for class '{_data.ClassKey}'");
}
