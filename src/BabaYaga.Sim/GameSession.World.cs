namespace BabaYaga.Sim;

/// <summary>
/// The world half of the session (sibling of the core file's character/economy
/// commands): standing stones, lit-stone travel, and the quest rites with their
/// reward payouts (P3 §6.3–6.4). Same contract as the core file — every command
/// either fully applies or is rejected with no state change.
/// </summary>
public sealed partial class GameSession
{
    public void DiscoverWaypoint(string areaKey)
    {
        if (!Waypoints.HasNode(areaKey)) throw new SessionRejectedException($"no standing stone in '{areaKey}'");
        if (!Waypoints.Discover(areaKey)) throw new SessionRejectedException($"stone in '{areaKey}' is already lit");
    }

    public void TravelTo(string areaKey)
    {
        if (!Areas.Areas.Any(a => a.Key == areaKey)) throw new SessionRejectedException($"unknown area '{areaKey}'");
        if (!Waypoints.CanTravel(CurrentArea, areaKey))
            throw new SessionRejectedException($"no lit-stone path from '{CurrentArea}' to '{areaKey}'");
        CurrentArea = areaKey;
        Engaged = null;
        EngagedKey = null;
    }

    public void QuestAccept(string key)
    {
        var quest = QuestFor(key);
        if (!Quests.TryActivate(quest.Row.Key)) // unknown → QuestFor threw already
            throw new SessionRejectedException($"rite '{key}' cannot be accepted now (prereqs, or already begun)");
    }

    public void QuestStep(string key)
    {
        var quest = QuestFor(key);
        if (!quest.TryMarkCompletable())
            throw new SessionRejectedException($"rite '{key}' is not active — nothing to complete");
    }

    public void QuestComplete(string key)
    {
        var quest = QuestFor(key);
        if (!quest.TryRequestReward())
            throw new SessionRejectedException($"rite '{key}' is not completable yet");
        quest.TryClaimReward();
        ApplyReward(quest.Row);
    }

    private QuestState QuestFor(string key) =>
        Quests.Quests.FirstOrDefault(q => q.Row.Key == key)
        ?? throw new SessionRejectedException($"unknown rite '{key}'");

    private void ApplyReward(QuestRow row)
    {
        switch (row.RewardKind)
        {
            case QuestRewardKind.LifePotion: Character.FlatLife += PotionOfLifeFlat * row.RewardAmount; break;
            case QuestRewardKind.WaypointUnlock when Waypoints.HasNode(row.GoalAreaKey): Waypoints.Discover(row.GoalAreaKey); break;
            // ponytail: a quest skill point cannot reach Character.SkillPoints —
            // the foundation exposes no public grant hook (its row 2 half lands
            // with the skills UI). Banked + dumped here; the spend engine is untouched.
            case QuestRewardKind.SkillPoint: QuestSkillRewards += row.RewardAmount; break;
            case QuestRewardKind.StashTab: StashTabs += row.RewardAmount; break; // stash itself is row 74
            default: OtherRewards += row.RewardAmount; break;
        }
    }

    // ---- persistence ----

    public string Dump() => SaveSerializer.Dump(this);
    public void Save(string path) => SaveSerializer.SaveFile(this, path);
    public static GameSession Load(string path, string dataDir) => SaveSerializer.LoadSession(path, dataDir);

    // ---- serializer restore hooks (SaveSerializer is the only caller) ----

    internal void RestorePurse(long amount) => Purse = new Purse(amount);
    internal void SetArea(string key) => CurrentArea = key;
    internal void SetEngaged(string? key, long life)
    {
        EngagedKey = key;
        if (key is null) { Engaged = null; return; }
        // Ordinary band: spawn resolution is the row's own level (P3 §5.1).
        Engaged = Monsters.Spawn(Monsters.Get(key), Monsters.Get(key).OwnLevel);
        Engaged.Life = life;
    }
}
