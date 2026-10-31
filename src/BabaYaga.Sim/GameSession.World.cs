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
            // The rite's skill point lands in the real pool (P1 §1.1, DA round-1 P1-2):
            // GrantSkillPoints is the single grant door; TrySpendSkillPoint the debit.
            case QuestRewardKind.SkillPoint: Character.GrantSkillPoints(row.RewardAmount); break;
            case QuestRewardKind.StashTab: StashTabs += row.RewardAmount; break; // stash itself is row 74
            default: OtherRewards += row.RewardAmount; break;
        }
    }

    // ---- special encounters (P3 §5.2) ----

    /// What kind of encounter the engaged monster is (DA round-1 P2-a): the CLI's
    /// special command sets it; a plain Spawn or a save-restored engagement resets
    /// it — and Attack hands it to the kill chain so the ilvl bump and XP tables run.
    internal SpecialKind EngagedKind { get; set; } = SpecialKind.Plain;
    internal IReadOnlyList<string> EngagedAffixKeys { get; set; } = [];

    /// <summary>A rolled special group as the CLI reports it: kind, leader, total
    /// unit count, the leader's affix keys and the keys its escort carries, and
    /// the bonus-scaled leader stats the following attack must fight.</summary>
    public sealed record SpecialReport(
        string Kind, string Leader, int Units,
        IReadOnlyList<string> Affixes, IReadOnlyList<string> MinionAffixes,
        long Life, long BaseXp);

    /// <summary>Roll the seeded 4:1 unique:champion group over the named row
    /// (PackAffix/UniqueMonster code), engage its bonus-scaled leader, and hand
    /// the kind to the kill chain — attack the returned monster to pay the
    /// band's XP multiplier and drop ilvl bump (DA round-1 P2-a live path).</summary>
    public SpecialReport Special(string monsterKey)
    {
        // Named rejection like Attack — never the raw KeyNotFoundException text
        // through the CLI (DA round-2 P4-2).
        var row = Monsters.Rows.FirstOrDefault(r => r.Key == monsterKey)
            ?? throw new SessionRejectedException($"unknown monster '{monsterKey}'");
        if (row.IsBoss)
            throw new SessionRejectedException($"'{monsterKey}' is a boss — bosses never lead packs (P3 §5.2)");
        var area = Areas.Get(CurrentArea);
        var enc = SpecialGroups.ComposeFor(CombatRng, Packs, Difficulty.Ordinary, row.Key);
        var bonus = enc.Kind == SpecialKind.Unique
            ? Packs.UniqueBonus(Difficulty.Ordinary)
            : Packs.ChampionBonus(Difficulty.Ordinary);
        var affixRows = enc.LeaderAffixKeys.Select(k => SpecialGroups.FindAffix(Packs, k)).ToArray();
        Engaged = MonsterMinions.SpawnOne(
            Monsters, row, MonsterTable.ResolveSpawnLevel(row, Difficulty.Ordinary, area), bonus, affixRows);
        EngagedKey = row.Key;
        EngagedKind = enc.Kind;
        EngagedAffixKeys = enc.LeaderAffixKeys;
        return new SpecialReport(enc.Kind.ToString(), row.Name, enc.UnitCount,
            enc.LeaderAffixKeys, enc.MinionAffixKeys, Engaged.Life, Engaged.BaseXp);
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
        EngagedKind = SpecialKind.Plain; // the save file stores no special state
        EngagedAffixKeys = [];
        if (key is null) { Engaged = null; return; }
        // Ordinary band: spawn resolution is the row's own level (P3 §5.1).
        Engaged = Monsters.Spawn(Monsters.Get(key), Monsters.Get(key).OwnLevel);
        Engaged.Life = life;
    }
}
