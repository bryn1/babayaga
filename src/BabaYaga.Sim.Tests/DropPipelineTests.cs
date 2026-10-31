namespace BabaYaga.Sim.Tests;

/// <summary>
/// The drop pipeline (P2 §4.9): the TreasureClass walk caps at 6 outputs incl.
/// gold, forced (negative-picks) classes never draw NoDrop, gold piles follow
/// mul/256 × the killer's GoldFind, Magic Find moves quality only (never
/// quantity), and KillFlow composes kill → XP + DeathDropEvent → walk → DropQueue.
/// </summary>
public class DropPipelineTests
{
    private static string Data(string file) => Path.Combine(TestData.DataDir(), file);

    private static ItemBaseTable Bases() => ItemBaseTable.Load(Data("item-bases.json"));
    private static AffixPool Affixes() => AffixPool.Load(Data("affixes.json"));
    private static UniqueSetTable Uniques() => UniqueSetTable.Load(Data("uniques.json"));
    private static ItemForge Forge() => new(Bases(), Affixes(), Uniques());
    private static TreasureClassTable RepoClasses() => TreasureClassTable.Load(Data("treasureclasses.json"));

    /// <summary>Write a throwaway classes file (WorldTests' temp-file idiom).</summary>
    private static string TempClasses(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"tc-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, body);
        return path;
    }

    [Fact]
    public void Walk_NineForcedPicks_CapsAtSixItems()
    {
        var path = TempClasses("""
            { "classes": [ { "key": "test9", "name": "Test", "picks": -9, "minIlvl": 5, "entries": [
                { "weight": 1, "kind": "item", "baseId": "palica" } ] } ] }
            """);
        try
        {
            var table = TreasureClassTable.Load(path);
            var drop = table.Walk(new Rng(3UL), "test9", ilvl: 5, forge: Forge());
            Assert.Equal(6, drop.Items.Count); // ≤6 outputs per kill (P2 §4.9)
            Assert.All(drop.Items, i => Assert.Equal("palica", i.Base.Id));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Walk_ForcedClassWithNoDropEntry_FailsTheLoad()
    {
        var path = TempClasses("""
            { "classes": [ { "key": "bad", "name": "Bad", "picks": -1, "entries": [
                { "weight": 1, "kind": "nodrop" },
                { "weight": 1, "kind": "item", "baseId": "palica" } ] } ] }
            """);
        try
        {
            Assert.Throws<InvalidDataException>(() => TreasureClassTable.Load(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Walk_ForcedPicks_AlwaysLandItems_AcrossSeeds()
    {
        var path = TempClasses("""
            { "classes": [ { "key": "forced", "name": "Forced", "picks": -3, "minIlvl": 1, "entries": [
                { "weight": 1, "kind": "item", "baseId": "watan" },
                { "weight": 1, "kind": "item", "baseId": "lapcie" } ] } ] }
            """);
        try
        {
            var table = TreasureClassTable.Load(path);
            var forge = Forge();
            for (ulong seed = 1; seed <= 10; seed++)
            {
                var drop = table.Walk(new Rng(seed), "forced", ilvl: 1, forge: forge);
                Assert.Equal(3, drop.Items.Count); // guaranteed walk, never a dry roll
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Walk_Gold_FollowsMulOver256_TimesKillerGoldFind()
    {
        var path = TempClasses("""
            { "classes": [ { "key": "purse", "name": "Purse", "picks": -4,
                "gold": { "nuggets": 8, "multiplier": 256 }, "entries": [
                { "weight": 1, "kind": "gold" } ] } ] }
            """);
        try
        {
            var table = TreasureClassTable.Load(path);
            var plain = table.Walk(new Rng(1UL), "purse", ilvl: 1, goldFind: 0, forge: Forge());
            Assert.Equal(4 * 8, plain.Gold); // 4 draws × 8×256/256 × 100/100
            var finder = table.Walk(new Rng(1UL), "purse", ilvl: 1, goldFind: 100, forge: Forge());
            Assert.Equal(4 * 16, finder.Gold); // ×(1+GF) per pile (P2 §4.8)
            // Gold ignores MF exactly as the walk does (P2 §4.1 MF-quality-only rule):
            var withMf = table.Walk(new Rng(1UL), "purse", ilvl: 1, magicFind: 400, goldFind: 100, forge: Forge());
            Assert.Equal(finder.Gold, withMf.Gold);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Walk_MagicFind_NeverChangesQuantity()
    {
        var path = TempClasses("""
            { "classes": [ { "key": "mf", "name": "MF", "picks": -4, "minIlvl": 12, "entries": [
                { "weight": 1, "kind": "item", "baseId": "palica" },
                { "weight": 1, "kind": "item", "baseId": "szyszak" } ] } ] }
            """);
        try
        {
            var table = TreasureClassTable.Load(path);
            var forge = Forge();
            for (ulong seed = 1; seed <= 12; seed++)
            {
                // MF reads nothing of the walk: same seed, same draws, same count —
                // it only moves the c-formula odds inside each item (P2 §4.8).
                var plain = table.Walk(new Rng(seed), "mf", ilvl: 12, magicFind: 0, forge: forge);
                var lucky = table.Walk(new Rng(seed), "mf", ilvl: 12, magicFind: 400, forge: forge);
                Assert.Equal(4, plain.Items.Count);
                Assert.Equal(plain.Items.Count, lucky.Items.Count);
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void KillFlow_Kill_EmitsXpAndDeathDropEvent_IntoTheQueue()
    {
        var queue = new DropQueue();
        var flow = new KillFlow(RepoClasses(), Forge(), queue);
        var ladder = TestData.Ladder();
        var hero = TestData.Koldun();
        hero.WeaponBaseAr = 100;
        var victim = new Monster
        {
            Name = "Pokrzywnik", Level = 1, Life = 5, BaseXp = 100,
            AttackRating = 50, Defense = 10,
        };
        var (outcome, result) = flow.Strike(
            new Rng(11UL), new Rng(12UL), hero, victim, ladder,
            damageMin: 50, damageMax: 50, tcKey: "lup_lezny",
            kind: SpecialKind.Plain, affixKeys: Array.Empty<string>(),
            areaLevel: 2, areaSeed: 77);
        Assert.True(outcome.Killed);
        Assert.NotNull(result);
        Assert.Equal(outcome.XpGranted, result!.XpGranted);
        Assert.True(outcome.XpGranted > 0);            // the XpTable chain paid
        Assert.Equal(hero.Xp, outcome.XpGranted);      // …into the character
        Assert.Equal("plain", result.Event.AffixKind);
        Assert.Equal(2, result.Event.Ilvl);            // plain kill: area level, no bump
        Assert.Equal(77, result.Event.AreaSeed);
        Assert.Equal(result.Drop.Items.Count, queue.Pending);
        Assert.Equal(result.Drop.Gold, queue.Gold);
    }

    [Fact]
    public void KillFlow_ChampionKill_IlvlBumpsByTwo_Survivor_EmitsNothing()
    {
        var queue = new DropQueue();
        var flow = new KillFlow(RepoClasses(), Forge(), queue);
        var ladder = TestData.Ladder();
        var hero = TestData.Koldun();
        hero.WeaponBaseAr = 100;

        var tough = new Monster
        {
            Name = "Leszun", Level = 3, Life = 1000, BaseXp = 100,
            AttackRating = 50, Defense = 10,
        };
        var (outcome, result) = flow.Strike(
            new Rng(5UL), new Rng(6UL), hero, tough, ladder,
            damageMin: 2, damageMax: 3, tcKey: "lup_lezny",
            kind: SpecialKind.Champion, affixKeys: new[] { "lodowaty" },
            areaLevel: 4, areaSeed: 9);
        Assert.False(outcome.Killed);
        Assert.Null(result);
        Assert.Equal(0, queue.Pending);
        Assert.Equal(0, hero.Xp);

        // The bump math itself, on an event made for a champion and a unique kill:
        Assert.Equal(6, DeathDropEvent.ForKill(SpecialKind.Champion, [], 4, 0).Ilvl);   // +2
        Assert.Equal(7, DeathDropEvent.ForKill(SpecialKind.Unique, [], 4, 0).Ilvl);     // +3
    }
}
