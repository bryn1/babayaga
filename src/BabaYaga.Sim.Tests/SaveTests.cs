namespace BabaYaga.Sim.Tests;

/// <summary>
/// Save/load (T0b P3 §6: waypoint/quest state persists with the character):
/// a whole session serialises to JSON at a caller-given path and reloads with a
/// byte-identical dump — carried items, equipment, pending drops, quest phases,
/// discovered stones, purse and reward pools included. The save→mutate→load
/// round-trip reverses the mutation; corrupt or foreign-version files are refused.
/// </summary>
public class SaveTests
{
    /// <summary>A session worked through the journey ops — the state a save must hold.</summary>
    private static GameSession Journeyish(ulong seed = 7)
    {
        var s = GameSession.New(seed, "koldun", TestData.DataDir());
        s.LevelTo(5);
        s.SpendSkill("iskra");
        s.Attack("pokrzywnik", 5);
        Assert.True(s.Loot(10).Taken > 0); // seed-picked so the walk dropped something
        s.EquipLastDrop();
        s.Buy("jantar");
        s.SellLast();
        s.Gamble();
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        s.QuestAccept("wrzeciono");
        s.QuestStep("wrzeciono");
        s.QuestComplete("wrzeciono");
        return s;
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"save-{Guid.NewGuid():N}.json");

    [Fact]
    public void SaveThenLoad_DumpIsByteIdentical()
    {
        var session = Journeyish();
        var before = session.Dump();
        var path = TempPath();
        try
        {
            session.Save(path);
            var loaded = GameSession.Load(path, TestData.DataDir());
            Assert.Equal(before, loaded.Dump());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SaveMutateLoad_ReversesTheMutation()
    {
        var session = Journeyish();
        var original = session.Dump();
        var path = TempPath();
        try
        {
            session.Save(path);
            session.LevelTo(9);                    // mutate: xp + points
            session.QuestAccept("gesi");           // mutate: quest phase
            Assert.NotEqual(original, session.Dump());

            var restored = GameSession.Load(path, TestData.DataDir());
            Assert.Equal(original, restored.Dump()); // the load undoes both
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SaveFile_WritesTheSameBytes_Twice()
    {
        var session = Journeyish();
        var pathA = TempPath();
        var pathB = TempPath();
        try
        {
            session.Save(pathA);
            session.Save(pathB);
            Assert.Equal(File.ReadAllBytes(pathA), File.ReadAllBytes(pathB));
        }
        finally { File.Delete(pathA); File.Delete(pathB); }
    }

    [Fact]
    public void RoundTrip_PreservesItemsEquipmentAndQuestReward()
    {
        var session = Journeyish();
        var path = TempPath();
        try
        {
            session.Save(path);
            var restored = GameSession.Load(path, TestData.DataDir());

            Assert.Equal(session.Carried.Count, restored.Carried.Count);
            foreach (var (a, b) in session.Carried.Zip(restored.Carried))
            {
                Assert.Equal(a.Base.Id, b.Base.Id);
                Assert.Equal(a.Quality, b.Quality);
                Assert.Equal(a.Mods, b.Mods); // every rolled stat line survives
            }
            Assert.Equal(
                session.Equipment.Equipped.Keys.OrderBy(k => (int)k),
                restored.Equipment.Equipped.Keys.OrderBy(k => (int)k));
            Assert.Equal(session.Purse.Amount, restored.Purse.Amount);
            // LifePotion reward (+20 flat life, P3 §6.4) paid once, and survives the round-trip:
            Assert.Equal(20, restored.Character.FlatLife);
            Assert.True(restored.Waypoints.IsDiscovered("mchy"));
            Assert.Equal(QuestPhase.Done, restored.Quests.PhaseOf("wrzeciono"));
            Assert.Equal(QuestPhase.Inactive, restored.Quests.PhaseOf("gesi")); // the mutation stayed undone
        }
        finally { File.Delete(path); }
    }

    /// <summary>The DA round-2 P1-N1 seam: level 5 grants 4 points, 4 are spent into
    /// hard points, and the completed Gesi rite pays the fifth (probe-p12's bank).
    /// Mirrors the CLI directions (a) save with the quest point UNSPENT and (b) spend
    /// it first — the state pre-fix made even the SAVE unloadable.</summary>
    private static GameSession QuestPointSession(bool spendTheQuestPoint)
    {
        var s = GameSession.New(2, "koldun", TestData.DataDir());
        s.LevelTo(5);
        s.SpendSkill("iskra"); s.SpendSkill("iskra"); s.SpendSkill("iskra");
        s.SpendSkill("kolo");
        s.QuestAccept("gesi"); s.QuestStep("gesi"); s.QuestComplete("gesi"); // pays 1 pt
        if (spendTheQuestPoint) s.SpendSkill("kolo");                        // HARDPOINTS:2 state
        return s;
    }

    /// <summary>DA round-2 P1-N1 (a): an UNSPENT quest point survives save/load —
    /// the pool comes from the save, not re-derived from level grants (quest
    /// rewards deliberately skip phase replay, so re-derivation loses it).</summary>
    [Fact]
    public void SaveLoad_QuestSkillPointUnspent_SurvivesAndSpendsAfterLoad()
    {
        var session = QuestPointSession(spendTheQuestPoint: false);
        Assert.Equal(1, session.Character.SkillPoints);
        var before = session.Dump();
        var path = TempPath();
        try
        {
            session.Save(path);
            var restored = GameSession.Load(path, TestData.DataDir());
            Assert.Equal(before, restored.Dump());
            restored.SpendSkill("kolo");                       // the CLI direction (a) leg
            Assert.Equal(2, restored.Skills.HardPoints("kolo"));
            Assert.Equal(0, restored.Character.SkillPoints);
        }
        finally { File.Delete(path); }
    }

    /// <summary>DA round-2 P1-N1 (b), the worse variant: the ALREADY-SPENT quest point
    /// (total hard points exceed the level grants) must load CLEANLY — pre-fix the
    /// whole legal save refused to restore — and the dump is byte-equal.</summary>
    [Fact]
    public void SaveLoad_QuestSkillPointAlreadySpent_LoadsCleanly_DumpEqual()
    {
        var session = QuestPointSession(spendTheQuestPoint: true);
        Assert.Equal(0, session.Character.SkillPoints);
        Assert.Equal(2, session.Skills.HardPoints("kolo"));    // 5 total > 4 level grants
        var before = session.Dump();
        var path = TempPath();
        try
        {
            session.Save(path);
            var restored = GameSession.Load(path, TestData.DataDir());
            Assert.Equal(before, restored.Dump());
        }
        finally { File.Delete(path); }
    }

    /// <summary>DA round-2 P4-4: the upgrade story pinned — a hand-built real
    /// version-1 save is refused by name and leaves the live session untouched.</summary>
    [Fact]
    public void Load_RefusesHandBuiltVersionOne_SessionUntouched()
    {
        var session = Journeyish();
        var untouched = session.Dump();
        var path = TempPath();
        try
        {
            session.Save(path);
            var text = File.ReadAllText(path)
                .Replace($"\"version\": {SaveSerializer.SaveVersion}", "\"version\": 1");
            Assert.NotEqual(text, File.ReadAllText(path));     // the downgrade landed
            File.WriteAllText(path, text);
            var e = Assert.Throws<InvalidDataException>(() => GameSession.Load(path, TestData.DataDir()));
            Assert.Contains("save version 1 unsupported", e.Message);
            Assert.Equal(untouched, session.Dump());           // refusal touched nothing
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_RefusesCorruptJson_AndForeignVersion()
    {
        var garbage = TempPath();
        File.WriteAllText(garbage, "{ this is not json ");
        try
        {
            Assert.ThrowsAny<Exception>(() => GameSession.Load(garbage, TestData.DataDir()));
        }
        finally { File.Delete(garbage); }

        var session = Journeyish();
        var path = TempPath();
        try
        {
            session.Save(path);
            var text = File.ReadAllText(path)
                .Replace($"\"version\": {SaveSerializer.SaveVersion}", "\"version\": 99");
            Assert.NotEqual(text, File.ReadAllText(path)); // the tamper landed
            File.WriteAllText(path, text);
            Assert.Throws<InvalidDataException>(() => GameSession.Load(path, TestData.DataDir()));
        }
        finally { File.Delete(path); }
    }
}
