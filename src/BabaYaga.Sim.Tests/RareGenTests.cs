namespace BabaYaga.Sim.Tests;

/// <summary>
/// The §4.3 rare contract: the ilvl ≥ 27 gate, 3+3 affix counts under one-per-group
/// exclusion, the rare-allowed-only pool, and the level-requirement template
/// (highest affix req + 10 + 3 per affix, at most 98).
/// </summary>
public class RareGenTests
{
    private static AffixPool Pool() => AffixPool.Load(Path.Combine(TestData.DataDir(), "affixes.json"));
    private static ItemBaseTable Bases() => ItemBaseTable.Load(Path.Combine(TestData.DataDir(), "item-bases.json"));

    [Fact]
    public void Rare_GatedBelowIlvl27()
    {
        var ok = RareGen.TryBuild(new Rng(1UL), Bases().For("szpula"), ilvl: 26, Pool(), out var item);
        Assert.False(ok);
        Assert.Null(item);
    }

    [Fact]
    public void Rare_RollsThreePlusThree_WithinGroupLimits()
    {
        var pool = Pool();
        var szpula = Bases().For("szpula");
        var byId = pool.All.ToDictionary(a => a.Id);
        for (ulong seed = 1; seed <= 200; seed++)
        {
            Assert.True(RareGen.TryBuild(new Rng(seed * 97), szpula, ilvl: 40, pool, out var built));
            var item = built!;
            Assert.Equal(ItemQuality.Rare, item.Quality);
            int prefixes = item.AffixIds.Count(id => byId[id].Prefix);
            int suffixes = item.AffixIds.Count(id => !byId[id].Prefix);
            Assert.InRange(prefixes, 1, 3); // spec counts: 3+3 with a healthy pool, never more
            Assert.InRange(suffixes, 1, 3);
            var groups = item.AffixIds.Select(id => byId[id].Group).ToList();
            Assert.Equal(groups.Count, groups.Distinct().Count()); // no forbidden group collision
        }
    }

    [Fact]
    public void Rare_UsesRareAllowedPoolOnly()
    {
        var pool = Pool();
        var szpula = Bases().For("szpula");
        for (ulong seed = 1; seed <= 300; seed++)
        {
            if (RareGen.TryBuild(new Rng(seed * 101), szpula, ilvl: 70, pool, out var built))
            {
                Assert.DoesNotContain("sielany", built!.AffixIds);   // magic-only prefix
                Assert.DoesNotContain("s_kosciel", built.AffixIds);  // magic-only suffix
            }
        }
        // ...while the same affixes do reach magic items at their alvl window.
        bool anySielany = false;
        foreach (ulong seed in Enumerable.Range(1, 400).Select(i => (ulong)i * 13))
        {
            var magic = AffixPool.BuildMagic(new Rng(seed), Bases().For("watan"), ilvl: 70, pool);
            if (magic.AffixIds.Contains("sielany")) anySielany = true;
        }
        Assert.True(anySielany, "Sielany (magic-only, alvl 36..85) must reach magic items");
    }

    [Fact]
    public void Rare_LevelRequirement_TemplateExact_And_Capped()
    {
        Assert.Equal(28, RareGen.LevelRequirement(highestAffixLevelReq: 0, affixCount: 6));  // 0+10+18
        Assert.Equal(52, RareGen.LevelRequirement(24, 6));                                    // 24+10+18
        Assert.Equal(98, RareGen.LevelRequirement(90, 6));                                    // cap 98
        Assert.Equal(25, RareGen.LevelRequirement(0, 5));
    }

    [Fact]
    public void Rare_BuiltCarriesRecomputedRequirement()
    {
        var pool = Pool();
        var byId = pool.All.ToDictionary(a => a.Id);
        for (ulong seed = 1; seed <= 120; seed++)
        {
            Assert.True(RareGen.TryBuild(new Rng(seed * 53), Bases().For("zdzar"), ilvl: 40, pool, out var built));
            var item = built!;
            int highest = item.AffixIds.Select(id => byId[id].LevelRequirement).DefaultIfEmpty(0).Max();
            Assert.Equal(
                RareGen.LevelRequirement(highest, item.AffixIds.Count),
                item.LevelRequirement);
        }
    }
}
