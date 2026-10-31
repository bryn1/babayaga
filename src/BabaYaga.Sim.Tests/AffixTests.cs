namespace BabaYaga.Sim.Tests;

/// <summary>
/// The §4.2 affix contract: alvl window tiering, the one-affix-per-group exclusion,
/// the configurable magic affix-count table, and rolled mod ranges.
/// </summary>
public class AffixTests
{
    private static AffixPool Pool() => AffixPool.Load(Path.Combine(TestData.DataDir(), "affixes.json"));
    private static ItemBaseTable Bases() => ItemBaseTable.Load(Path.Combine(TestData.DataDir(), "item-bases.json"));

    [Fact]
    public void Pool_AlvlWindow_TiersEligibility()
    {
        var pool = Pool();
        var szpula = Bases().For("szpula"); // qlvl 5 → ilvl 6 has alvl 4, ilvl 40 has alvl 38
        var low = pool.Eligible(true, alvl: 4, szpula, forRare: false, usedGroups: []).Select(a => a.Id);
        Assert.Contains("wilkowy", low);   // 1..30
        Assert.DoesNotContain("daleki", low);   // 25..70 — not at alvl 4
        Assert.DoesNotContain("kragowy", low);  // 15..99 — not at alvl 4
        var high = pool.Eligible(true, alvl: 38, szpula, forRare: false, usedGroups: []).Select(a => a.Id);
        Assert.Contains("daleki", high);
        Assert.DoesNotContain("wilkowy", high);  // its maxLevel 30 closed the window
    }

    [Fact]
    public void Pool_GroupExclusion_OneAffixPerGroupPerItem()
    {
        var pool = Pool();
        var szpula = Bases().For("szpula");
        // At alvl 20 both Popielny and Kragowy stand (group "dmgphys"); a used group vanishes.
        var both = pool.Eligible(true, 20, szpula, forRare: false, usedGroups: []).Where(a => a.Group == "dmgphys");
        Assert.Equal(2, both.Count());
        var after = pool.Eligible(true, 20, szpula, forRare: false, usedGroups: ["dmgphys"]);
        Assert.DoesNotContain(after, a => a.Group == "dmgphys");
    }

    [Fact]
    public void MagicCounts_Within1To2PerSide_SecondAffixStartsAtIlvl27()
    {
        var rng = new Rng(23UL);
        int seenTwo = 0;
        for (int i = 0; i < 1000; i++)
        {
            var (p, s) = AffixPool.MagicAffixCounts(rng, ilvl: 10);
            Assert.InRange(p, 0, 2);
            Assert.InRange(s, 0, 2);
            Assert.InRange(p + s, 1, 4);
            Assert.True(p <= 1 && s <= 1, $"below ilvl 27 no side may double: {p}+{s}");
        }
        for (int i = 0; i < 1000; i++)
        {
            var (p, s) = AffixPool.MagicAffixCounts(rng, ilvl: 50);
            Assert.InRange(p + s, 1, 4);
            if (p == 2 || s == 2) seenTwo++;
        }
        Assert.True(seenTwo > 100, $"second-affix steps must fire at ilvl 50, saw {seenTwo}");
    }

    [Fact]
    public void MagicItem_AffixCountsAndModRanges_HoldAcrossSeeds()
    {
        var pool = Pool();
        var table = Bases();
        var byId = pool.All.ToDictionary(a => a.Id);
        foreach (ulong seed in Enumerable.Range(1, 120).Select(i => (ulong)i * 31)
                     .Append(9UL))
        {
            var item = AffixPool.BuildMagic(new Rng(seed), table.For("szyszak"), ilvl: 30, pool);
            Assert.Equal(ItemQuality.Magic, item.Quality);
            Assert.True(item.AffixIds.Count is >= 1 and <= 4, "magic items roll 1..4 affixes");
            Assert.Equal(item.AffixIds.Count, item.AffixIds.Distinct().Count()); // no id twice
            foreach (var id in item.AffixIds)
            {
                var def = byId[id];
                Assert.True(def.Level <= 27 && 27 <= def.MaxLevel, $"{id} outside szyszak ilvl-30 alvl 27");
            }
            foreach (var mod in item.Mods)
            {
                // Every rolled value sits in the declared range of an affix that grants it.
                Assert.Contains(item.AffixIds.Select(id => byId[id])
                    .SelectMany(a => a.Mods).Where(m => m.Stat == mod.Stat),
                    r => mod.Value >= r.Min && mod.Value <= r.Max);
            }
        }
    }

    [Fact]
    public void MagicItem_NameComposesPrefix_Base_Suffix()
    {
        var pool = Pool();
        var szpula = Bases().For("szpula");
        // Seeded hunt for a both-sided magic item (szyszak-sized counts make it rare at ilvl 10).
        foreach (ulong seed in Enumerable.Range(1, 200).Select(i => (ulong)i))
        {
            var item = AffixPool.BuildMagic(new Rng(seed), szpula, ilvl: 50, pool);
            if (item.PrefixName is not null && item.SuffixName is not null)
            {
                Assert.StartsWith(item.PrefixName, item.DisplayName);
                Assert.Contains("Szpula", item.DisplayName);
                Assert.EndsWith(item.SuffixName, item.DisplayName);
                return;
            }
        }
        Assert.Fail("no prefix+suffix magic item in 200 seeds at ilvl 50");
    }

    [Fact]
    public void AffixTypeFilters_WeaponAffixNeverOnArmor()
    {
        var pool = Pool();
        var watan = Bases().For("watan"); // body armor
        foreach (ulong seed in Enumerable.Range(1, 150).Select(i => (ulong)i * 7))
        {
            var item = AffixPool.BuildMagic(new Rng(seed), watan, ilvl: 40, pool);
            Assert.DoesNotContain("wilkowy", item.AffixIds); // weapon-only ar affix
            Assert.DoesNotContain("s_millstone", item.AffixIds); // weapon-only dmg affix
        }
    }
}
