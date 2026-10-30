using System.Text.Json;

namespace BabaYaga.Sim;

/// The quest's place in the state machine (P3 §6.4): legal moves touch the next
/// phase only — inactive → active → completable → reward-pending → done.
public enum QuestPhase
{
    Inactive,
    Active,
    Completable,
    RewardPending,
    Done,
}

/// What finishing a quest pays (P3 §6.4 reward kinds, Baba Yaga flavour).
public enum QuestRewardKind
{
    SkillPoint,
    LifePotion,       // one-time +20 base life, once per difficulty
    WaypointUnlock,
    StashTab,
    CubeUnlock,
    VendorDiscount,
    QuiverUpgrade,
    BeltUpgrade,
    RespecRite,
}

/// One quest row from data/act1-quests.json.
public sealed record QuestRow(
    string Key,
    string Name,
    int Act,
    int Order,
    QuestRewardKind RewardKind,
    int RewardAmount,
    string GoalAreaKey,
    IReadOnlyList<string> PrereqKeys);

/// <summary>
/// One quest's machine: every transition is a Try-method that returns false when
/// called out of order — the phase never skips and never reverses (DoD f).
/// </summary>
public sealed class QuestState
{
    public QuestRow Row { get; }
    public QuestPhase Phase { get; private set; } = QuestPhase.Inactive;

    public QuestState(QuestRow row) => Row = row;

    /// inactive → active (the village elder sets you on it).
    public bool TryActivate() => Advance(QuestPhase.Inactive, QuestPhase.Active);

    /// active → completable (the goal flag flipped: the pelt is in the pack).
    public bool TryMarkCompletable() => Advance(QuestPhase.Active, QuestPhase.Completable);

    /// completable → reward-pending (the teller knows you finished, not yet paid).
    public bool TryRequestReward() => Advance(QuestPhase.Completable, QuestPhase.RewardPending);

    /// reward-pending → done; true only on the phase move, reward paid once.
    public bool TryClaimReward() => Advance(QuestPhase.RewardPending, QuestPhase.Done);

    private bool Advance(QuestPhase from, QuestPhase to)
    {
        if (Phase != from) return false;
        Phase = to;
        return true;
    }
}

/// <summary>
/// An act's quest log (P3 §6.4): rows loaded from JSON, one machine per quest, and
/// the act ordering rule — a quest cannot be started before its prerequisite
/// quests are DONE (the boss quest stays locked behind its side quests).
/// </summary>
public sealed class QuestLog
{
    private readonly Dictionary<string, QuestState> _quests;

    public IReadOnlyList<QuestState> Quests { get; }

    private QuestLog(List<QuestState> quests)
    {
        Quests = quests;
        _quests = quests.ToDictionary(q => q.Row.Key);
    }

    /// Parses and validates: unique keys, reward kinds known, prereqs resolvable
    /// and acyclic (an order-sorted greedy pass must drain every row).
    public static QuestLog Load(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var rows = new List<QuestRow>();
        foreach (var q in doc.RootElement.GetProperty("quests").EnumerateArray())
        {
            var prereqs = new List<string>();
            foreach (var p in q.GetProperty("prereqs").EnumerateArray())
                prereqs.Add(p.GetString() ?? throw new InvalidDataException("empty prereq"));
            string reward = q.GetProperty("rewardKind").GetString()
                ?? throw new InvalidDataException("quest without rewardKind");
            if (!Enum.TryParse<QuestRewardKind>(reward, ignoreCase: true, out var kind))
                throw new InvalidDataException($"unknown reward kind '{reward}'");
            rows.Add(new QuestRow(
                Key: q.GetProperty("key").GetString() ?? throw new InvalidDataException("quest without key"),
                Name: q.GetProperty("name").GetString() ?? "",
                Act: q.GetProperty("act").GetInt32(),
                Order: q.GetProperty("order").GetInt32(),
                RewardKind: kind,
                RewardAmount: q.GetProperty("rewardAmount").GetInt32(),
                GoalAreaKey: q.GetProperty("goalAreaKey").GetString() ?? "",
                PrereqKeys: prereqs));
        }
        if (rows.GroupBy(r => r.Key).Any(g => g.Count() > 1))
            throw new InvalidDataException("duplicate quest key");
        var keys = rows.Select(r => r.Key).ToHashSet();
        foreach (var r in rows)
            foreach (var p in r.PrereqKeys)
                if (!keys.Contains(p))
                    throw new InvalidDataException($"quest '{r.Key}' prereq '{p}' unknown");
        // Acyclic check: repeatedly drain rows whose prereqs are all drained.
        var remaining = rows.ToList();
        var drained = new HashSet<string>();
        while (remaining.Count > 0)
        {
            var ready = remaining.Where(r => r.PrereqKeys.All(drained.Contains)).ToList();
            if (ready.Count == 0) throw new InvalidDataException("quest prereq cycle");
            foreach (var r in ready)
            {
                drained.Add(r.Key);
                remaining.Remove(r);
            }
        }
        return new QuestLog(rows.OrderBy(r => r.Order).Select(r => new QuestState(r)).ToList());
    }

    public QuestState Get(string key) => _quests[key];
    public QuestPhase PhaseOf(string key) => _quests[key].Phase;

    /// Start a quest: false unless it is Inactive AND every prereq quest is Done.
    public bool TryActivate(string key)
    {
        var q = _quests[key];
        if (!q.Row.PrereqKeys.All(p => PhaseOf(p) == QuestPhase.Done)) return false;
        return q.TryActivate();
    }
}
