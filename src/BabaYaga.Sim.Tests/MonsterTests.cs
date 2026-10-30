namespace BabaYaga.Sim.Tests;

/// <summary>
/// The monster stat-table shape (P3 §5.1): loader validation, the
/// table(level) × own-percent multiplication with integer floors, the
/// difficulty spawn-level stamping (row 19), the combat-Monster factory, and
/// the DeathDropEvent the sim emits on a kill.
/// </summary>
public class MonsterTests
{
    private static MonsterTable Table() =>
        MonsterTable.Load(Path.Combine(TestData.DataDir(), "monsters.json"));

    [Fact]
    public void Load_MonsterRows_AllOriginalKeysBossFlagged()
    {
        var table = Table();
        Assert.Equal(12, table.Rows.Count);
        var leszy = table.Get("leszy");
        Assert.True(leszy.IsBoss);
        Assert.True(leszy.KeepsOwnLevel);
        Assert.Equal(AiBehaviour.Charge, table.Get("kretownik").Ai);
        Assert.Equal(AiBehaviour.Ranged, table.Get("wierzbowiczka").Ai);
        Assert.Equal(AiBehaviour.Avoid, table.Get("ponurak").Ai);
    }

    [Fact]
    public void StatsAt_BaseTimesGrowthTimesOwnPercent_Floored()
    {
        var table = Table();
        var bobun = table.Get("bobun"); // baseLife 48, own lifePercent 160, xpPercent 100
        var st = table.StatsAt(bobun, 10);
        // growth row at level 10: life 180, xp 130. lifePct = 180×160/100 = 288
        // → life = 48×288/100 = 138.24 → 138; xp = 130×100/100 = 130.
        Assert.Equal(138, st.Life);
        Assert.Equal(130, st.Xp);
        // toHit = 30 × (135×75/100 = 101)/100 = 30.3 → 30
        Assert.Equal(30, st.ToHit);
    }

    [Fact]
    public void StatsAt_LevelOne_GrowthRowStartsAtLevelOne()
    {
        var table = Table();
        var leszun = table.Get("leszun"); // baseLife 26, all percents 100
        var st = table.StatsAt(leszun, 3); // level 3 sits on the startLevel-1 row
        Assert.Equal(26, st.Life);
        Assert.Equal(40, st.Xp);
    }

    [Fact]
    public void ResolveSpawnLevel_OrdinaryOwnLevel_HigherBandsUseArea_HigherDifficulties()
    {
        var table = Table();
        var areas = AreaGraph.Load(Path.Combine(TestData.DataDir(), "act1-areas.json"));
        var mchy = areas.Get("mchy"); // level 2 / nm 9 / hell 24
        var pawn = table.Get("pokrzywnik"); // own level 1, not boss
        Assert.Equal(1, MonsterTable.ResolveSpawnLevel(pawn, Difficulty.Ordinary, mchy));
        Assert.Equal(9, MonsterTable.ResolveSpawnLevel(pawn, Difficulty.Nightmare, mchy));
        Assert.Equal(24, MonsterTable.ResolveSpawnLevel(pawn, Difficulty.Hell, mchy));

        var boss = table.Get("leszy"); // boss keeps its own court level in every band
        Assert.Equal(12, MonsterTable.ResolveSpawnLevel(boss, Difficulty.Hell, mchy));
        Assert.Equal(12, MonsterTable.ResolveSpawnLevel(boss, Difficulty.Nightmare, mchy));
    }

    [Fact]
    public void Spawn_HandsFoundationMonster_AtFullTableStats()
    {
        var table = Table();
        var strzyga = table.Get("strzyga"); // resist 20
        var m = table.Spawn(strzyga, 6);
        var st = table.StatsAt(strzyga, 6);
        Assert.Equal(st.Life, m.Life);
        Assert.Equal(st.Xp, m.BaseXp);
        Assert.Equal(KillType.Normal, m.KillType);
        Assert.Equal("Strzyga", m.Name);
        Assert.Equal(20, m.ElementalResistPercent);
        Assert.Equal(80, m.TakeElemental(100)); // 20 % resist eats 20
    }

    [Fact]
    public void Load_MalformedTables_Rejected()
    {
        // Unsorted growth rows, an unknown ai key and a duplicate key each reject.
        var good = File.ReadAllText(Path.Combine(TestData.DataDir(), "monsters.json"));

        var unsorted = good.Replace("\"startLevel\": 5,", "\"startLevel\": 1,"); // two rows start at 1
        AssertThrowsInvalid(unsorted);

        var badAi = good.Replace("\"ai\": \"melee\"", "\"ai\": \"flyme\"");
        AssertThrowsInvalid(badAi);

        var dup = good.Replace("\"key\": \"leszun\"", "\"key\": \"pokrzywnik\"");
        AssertThrowsInvalid(dup);
    }

    private static void AssertThrowsInvalid(string json)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            Assert.Throws<InvalidDataException>(() => MonsterTable.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DeathDropEvent_KindBumpAffixKindAndSeed()
    {
        var plain = DeathDropEvent.ForKill(SpecialKind.Plain, Array.Empty<string>(), 7, 90210);
        Assert.Equal(("plain", 7, 90210), (plain.AffixKind, plain.Ilvl, plain.AreaSeed));

        var champ = DeathDropEvent.ForKill(
            SpecialKind.Champion, new[] { "gruzacz" }, 7, 90210);
        Assert.Equal(("gruzacz", 9, 90210), (champ.AffixKind, champ.Ilvl, champ.AreaSeed));

        var unique = DeathDropEvent.ForKill(
            SpecialKind.Unique, new[] { "krewopij", "gruboskora" }, 7, 90210);
        Assert.Equal(("krewopij", 10, 90210), (unique.AffixKind, unique.Ilvl, unique.AreaSeed));
    }
}
