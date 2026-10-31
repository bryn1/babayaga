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
            var text = File.ReadAllText(path).Replace("\"version\": 1", "\"version\": 99");
            Assert.NotEqual(text, File.ReadAllText(path)); // the tamper landed
            File.WriteAllText(path, text);
            Assert.Throws<InvalidDataException>(() => GameSession.Load(path, TestData.DataDir()));
        }
        finally { File.Delete(path); }
    }
}
