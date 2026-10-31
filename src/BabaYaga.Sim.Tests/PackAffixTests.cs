namespace BabaYaga.Sim.Tests;

/// <summary>
/// Champion packs and uniques (P3 §5.2–5.4): the 4:1 special-group roll, the
/// same-seed pack-composition determinism (DoD c), the band bonus tables, the
/// 1/2/3 unique modifier roll, and the reduced-bonus minion escort.
/// </summary>
public class PackAffixTests
{
    private static PackData Packs() => PackData.Load(Path.Combine(TestData.DataDir(), "packs.json"));
    private static MonsterTable Table() => MonsterTable.Load(Path.Combine(TestData.DataDir(), "monsters.json"));

    [Fact]
    public void Load_PoolsAndBonusTables_ShapeAsDesigned()
    {
        var data = Packs();
        Assert.Equal((4, 1), (data.UniqueWeight, data.ChampionWeight));
        Assert.Equal(8, data.ChampionAffixes.Count);
        Assert.Equal(10, data.UniqueAffixes.Count);
        Assert.Equal((5, 9), (data.ChampionPackMin, data.ChampionPackMax));
        Assert.Equal((3, 6), (data.UniqueMinionMin, data.UniqueMinionMax));
    }

    [Fact]
    public void Compose_SameSeed_IdenticalPackComposition()
    {
        // DoD (c): one seed replays the identical encounter — kind, leader, size,
        // leader affixes and minion affixes — across two independent loads.
        var table = Table();
        foreach (ulong seed in new ulong[] { 7, 20261030, 424242 })
        {
            var a = SpecialGroups.Compose(new Rng(seed), Packs(), table, Difficulty.Nightmare);
            var b = SpecialGroups.Compose(new Rng(seed), Packs(), table, Difficulty.Nightmare);
            Assert.Equal(a.Kind, b.Kind);
            Assert.Equal(a.LeaderKey, b.LeaderKey);
            Assert.Equal(a.UnitCount, b.UnitCount);
            Assert.True(a.LeaderAffixKeys.SequenceEqual(b.LeaderAffixKeys));
            Assert.True(a.MinionAffixKeys.SequenceEqual(b.MinionAffixKeys));
        }
    }

    [Fact]
    public void Compose_OneSeed_NotAConstant_SignaturesDiverge()
    {
        var rng = new Rng(31337);
        var data = Packs();
        var table = Table();
        var signatures = Enumerable.Range(0, 40)
            .Select(_ =>
            {
                var e = SpecialGroups.Compose(rng, data, table, Difficulty.Ordinary);
                return $"{e.Kind}|{e.LeaderKey}|{e.UnitCount}|{string.Join(",", e.LeaderAffixKeys)}";
            })
            .Distinct()
            .Count();
        Assert.True(signatures > 2, $"only {signatures} distinct signatures — rolls never fire");
    }

    [Fact]
    public void RollKind_FourToOne_UniqueWithMinionsBeatsChampionPack()
    {
        var data = Packs();
        int unique = 0;
        for (uint seed = 0; seed < 2000; seed++)
            if (SpecialGroups.RollKind(new Rng(seed), data) == SpecialKind.Unique) unique++;
        // Expected 80 %; a sane band of 74–86 % proves the weights, not the RNG.
        Assert.InRange(unique, 1480, 1720);
    }

    [Fact]
    public void ChampionPack_SizeRange_SingleFamily_NonBossLeader()
    {
        var rng = new Rng(5150);
        var data = Packs();
        var table = Table();
        for (int i = 0; i < 300; i++)
        {
            var e = SpecialGroups.Compose(rng, data, table, Difficulty.Hell);
            if (e.Kind != SpecialKind.Champion) continue;
            Assert.InRange(e.UnitCount, 5, 9);
            Assert.Single(e.LeaderAffixKeys);
            Assert.Contains(e.LeaderAffixKeys[0], data.ChampionAffixes.Select(a => a.Key));
            Assert.NotEqual("leszy", e.LeaderKey);
        }
    }

    [Fact]
    public void BonusTables_ByBand_LifeArLevelXpRows()
    {
        var data = Packs();
        Assert.Equal(new SpecialBonus(200, 67, 2, 3), data.ChampionBonus(Difficulty.Ordinary));
        Assert.Equal(new SpecialBonus(100, 49, 2, 3), data.ChampionBonus(Difficulty.Hell));
        Assert.Equal(new SpecialBonus(300, 90, 3, 5), data.UniqueBonus(Difficulty.Ordinary));
        Assert.Equal(new SpecialBonus(100, 66, 3, 5), data.UniqueBonus(Difficulty.Hell));
        Assert.Equal(new SpecialBonus(100, 45, 3, 5), data.MinionBonus(Difficulty.Ordinary));
        Assert.Equal(new SpecialBonus(50, 33, 3, 5), data.MinionBonus(Difficulty.Hell));
    }

    [Fact]
    public void ApplySpecial_BandBonusPlusAffixPercents()
    {
        var data = Packs();
        var zarliwy = data.ChampionAffixes.First(a => a.Key == "zarliwy"); // dmg +20 %
        var st = new SpawnStats(Life: 100, Defense: 50, ToHit: 100, DamageMin: 5, DamageMax: 10, Xp: 40);
        var buffed = SpecialGroups.ApplySpecial(st, data.ChampionBonus(Difficulty.Ordinary), new[] { zarliwy });
        Assert.Equal(300, buffed.Life);   // 100 × (100+200)/100
        Assert.Equal(167, buffed.ToHit);  // 100 × (100+67)/100
        Assert.Equal(12, buffed.DamageMax); // 10 × (100+20)/100
        Assert.Equal(120, buffed.Xp);     // 40 × 3
        Assert.Equal(50, buffed.Defense); // the bonus row never touches defense
    }

    [Fact]
    public void UniqueRoller_OneTwoThreeModifiers_ByBand_NoDuplicates()
    {
        var data = Packs();
        Assert.Equal(1, UniqueRoller.AffixCountFor(Difficulty.Ordinary));
        Assert.Equal(3, UniqueRoller.AffixCountFor(Difficulty.Hell));
        var rng = new Rng(606);
        for (int i = 0; i < 200; i++)
        {
            foreach (var band in new[] { Difficulty.Ordinary, Difficulty.Nightmare, Difficulty.Hell })
            {
                var rolled = UniqueRoller.RollAffixes(rng, band, data.UniqueAffixes);
                Assert.Equal(UniqueRoller.AffixCountFor(band), rolled.Count);
                Assert.Equal(rolled.Count, rolled.Select(a => a.Key).Distinct().Count());
            }
        }
    }

    /// <summary>DA round-2 P2-N1: the unique pool's passesToMinions:false rows (chciwy)
    /// stay on the leader too — the LIVE path must honour the flag the champion path
    /// already reads. Seed 42 is DA's observed reproduction (chciwy rolled, minions
    /// carried it pre-fix).</summary>
    [Fact]
    public void Minions_PassesToMinionsFalse_AffixStaysOnLeader_LivePath()
    {
        var data = Packs();
        var chciwy = data.UniqueAffixes.First(a => a.Key == "chciwy"); // passes:false behaviourOnly:false
        Assert.Empty(MonsterMinions.InheritedAffixes(new[] { chciwy }));

        var enc = SpecialGroups.ComposeFor(new Rng(42), data, Difficulty.Ordinary, "pokrzywnik");
        Assert.Equal(SpecialKind.Unique, enc.Kind);
        Assert.Contains("chciwy", enc.LeaderAffixKeys);
        Assert.DoesNotContain("chciwy", enc.MinionAffixKeys);
    }

    [Fact]
    public void Minions_EscortSizeReducedBonuses_BehaviourAffixesStayOnLeader()
    {
        var data = Packs();
        var table = Table();
        var pawn = table.Get("wierzbowiczka");
        var cichy = data.ChampionAffixes.First(a => a.Key == "cichy");   // behaviour-only
        var krewopij = data.UniqueAffixes.First(a => a.Key == "krewopij"); // passes

        Assert.Empty(MonsterMinions.InheritedAffixes(new[] { cichy }));
        Assert.Single(MonsterMinions.InheritedAffixes(new[] { cichy, krewopij }));

        var rng = new Rng(88);
        for (int i = 0; i < 100; i++)
            Assert.InRange(MonsterMinions.RollCount(rng, data), 3, 6);

        // A minion at spawn level 4, Ordinary band: stats tabled at 4+3=7 (growth
        // life 130, xp 70), life 20×(130×90/100)/100 = 23, then ×(100+100)/100 = 46;
        // xp 70×110/100 = 77, then ×5 = 385. KillType stays Normal — the multiplier
        // already lives in BaseXp (no double-count in the chain).
        var minion = MonsterMinions.SpawnOne(
            table, pawn, 4, data.MinionBonus(Difficulty.Ordinary), new[] { krewopij });
        Assert.Equal(7, minion.Level);
        Assert.Equal(51, minion.Life);  // 23 × (100+100+lifeAdd25)/100 → 23×225/100 = 51
        Assert.Equal(385, minion.BaseXp);
        Assert.Equal(KillType.Normal, minion.KillType);
    }
}
