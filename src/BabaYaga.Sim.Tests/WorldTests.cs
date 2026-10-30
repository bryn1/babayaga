namespace BabaYaga.Sim.Tests;

/// <summary>
/// World & progression (P3 §6.1–6.6): party XP pot with the 87/256 bonus and the
/// proximity rule, the death penalty bands with the corpse return, the waypoint
/// graph's discovered-only travel, and the quest state machine's order contract.
/// </summary>
public class WorldTests
{
    private static string Data(string file) => Path.Combine(TestData.DataDir(), file);
    private static AreaGraph Areas() => AreaGraph.Load(Data("act1-areas.json"));

    // ---- party XP sharing (§6.6) ------------------------------------------------

    [Fact]
    public void XpShare_PotGrowsBy87_256_PerExtraMemberInRange()
    {
        Assert.Equal(1000, XpShare.Pot(1000, 1));                                  // no bonus solo
        Assert.Equal(1339, XpShare.Pot(1000, 2));                                  // ×343/256 floored
        Assert.Equal(1679, XpShare.Pot(1000, 3));                                  // ×430/256 floored
        Assert.Equal(0, XpShare.Pot(1000, 0));                                     // no in-range members
    }

    [Fact]
    public void XpShare_TwoEqualMembers_ExactSplit_Conserved()
    {
        var ladder = TestData.Ladder();
        var party = new[]
        {
            new PartyMember(TestData.Koldun(), new WorldPos(0, 0)),
            new PartyMember(TestData.Koldun(), new WorldPos(3, 3)),
        };
        var shares = XpShare.Shares(
            ladder, 1000, KillType.Normal, 1, new WorldPos(0, 0), party);
        // pot = 1339, both level 1: each floor(1339×1/2) = 669; sum 1338 ≤ pot, ≥ pot−1.
        Assert.Equal(new[] { 669L, 669L }, shares);
        Assert.True(shares.Sum() <= 1339 && shares.Sum() >= 1339 - 1);
    }

    [Fact]
    public void XpShare_LevelWeighted_HigherLevelTakesMore()
    {
        var ladder = TestData.Ladder();
        var senior = TestData.Koldun();
        senior.GrantXp(500, ladder); // level 2
        var party = new[]
        {
            new PartyMember(senior, new WorldPos(0, 0)),
            new PartyMember(TestData.Koldun(), new WorldPos(1, 0)),
        };
        var shares = XpShare.Shares(
            ladder, 1000, KillType.Normal, 1, new WorldPos(0, 0), party);
        // pot 1339: level-2 share 1339×2/3=892 (gap 100 %, ratio 1024); level-1 1339×1/3=446.
        Assert.Equal(new[] { 892L, 446L }, shares);
        Assert.True(shares[0] > shares[1]);
        Assert.True(shares.Sum() <= 1339);
    }

    [Fact]
    public void XpShare_ProximityAndAliveness_BoundaryExactly80()
    {
        var ladder = TestData.Ladder();
        var party = new[]
        {
            new PartyMember(TestData.Koldun(), new WorldPos(80, 0)),   // in: Chebyshev == 80
            new PartyMember(TestData.Koldun(), new WorldPos(81, 0)),   // out: one unit too far
            new PartyMember(TestData.Koldun(), new WorldPos(2, 0), Alive: false), // dead: out
        };
        var shares = XpShare.Shares(
            ladder, 1000, KillType.Normal, 1, new WorldPos(0, 0), party);
        Assert.Equal(1000, shares[0]); // only member in range: pot = the kill itself
        Assert.Equal(0, shares[1]);
        Assert.Equal(0, shares[2]);
    }

    // ---- death penalty (§6.6) ---------------------------------------------------

    [Fact]
    public void DeathPenalty_Bands0_5_10_OfCurrentLevelSpan()
    {
        var ladder = TestData.Ladder();
        // Level 5 spans 1845−1331 = 514 XP; stand one short of the top.
        Assert.Equal(0, DeathPenalty.PenaltyPercent(Difficulty.Ordinary));
        Assert.Equal(5, DeathPenalty.PenaltyPercent(Difficulty.Nightmare));
        Assert.Equal(10, DeathPenalty.PenaltyPercent(Difficulty.Hell));

        var ordinary = DeathPenalty.Apply(ladder, 1844, 5, Difficulty.Ordinary);
        Assert.Equal((1844L, 0L, 0L), (ordinary.XpAfter, ordinary.Lost, ordinary.CorpseXp));

        var nightmare = DeathPenalty.Apply(ladder, 1844, 5, Difficulty.Nightmare);
        Assert.Equal(1844 - 25, nightmare.XpAfter); // 514×5/100 = 25.7 → 25
        Assert.Equal(18, nightmare.CorpseXp);        // ¾ of 25

        var hell = DeathPenalty.Apply(ladder, 1844, 5, Difficulty.Hell);
        Assert.Equal(1844 - 51, hell.XpAfter);       // 514×10/100 = 51.4 → 51
        Assert.Equal(38, hell.CorpseXp);             // ¾ of 51
    }

    [Fact]
    public void DeathPenalty_NeverBelowLevelStart_LevelOneLosesNothing()
    {
        var ladder = TestData.Ladder();
        var floor = DeathPenalty.Apply(ladder, 1340, 5, Difficulty.Hell); // only 9 above the start
        Assert.Equal(1331, floor.XpAfter); // floor at the level-5 start, not below
        Assert.Equal(9, floor.Lost);
        Assert.Equal(6, floor.CorpseXp);   // ¾ of what actually came off

        var first = DeathPenalty.Apply(ladder, 400, 1, Difficulty.Hell);
        Assert.Equal((400L, 0L, 0L), (first.XpAfter, first.Lost, first.CorpseXp));
    }

    // ---- waypoints (§6.3) --------------------------------------------------------

    [Fact]
    public void Waypoints_TravelOnlyToDiscovered_TownIsHub_PathMustBeLitBetween()
    {
        var wps = WaypointGraph.Load(Areas(), Data("waypoints.json"));

        // Nothing lit yet: no travel anywhere.
        Assert.False(wps.CanTravel("grodzisko", "mchy"));

        wps.Discover("grodzisko"); // the town stone lights on arrival
        Assert.True(wps.IsDiscovered("grodzisko"));
        Assert.False(wps.CanTravel("grodzisko", "czelusc")); // target dark

        wps.Discover("czelusc");
        Assert.True(wps.CanTravel("grodzisko", "czelusc")); // hub reaches any lit stone
        Assert.True(wps.CanTravel("czelusc", "grodzisko"));

        // Stone-to-stone through the dark is refused: mchy→kopcowisko (both lit)
        // would have to cross unlit Krecie Korytarze or Widłany Jar.
        wps.Discover("mchy");
        wps.Discover("kopcowisko");
        Assert.False(wps.CanTravel("mchy", "kopcowisko"));
        wps.Discover("krecie");
        Assert.True(wps.CanTravel("mchy", "kopcowisko")); // now the path mchy→krecie→… is lit
        Assert.False(wps.CanTravel("mchy", "pokrzywlasy")); // no waypoint there at all
    }

    [Fact]
    public void Waypoints_DiscoveryIsPermanent()
    {
        var wps = WaypointGraph.Load(Areas(), Data("waypoints.json"));
        Assert.True(wps.Discover("mchy"));
        Assert.False(wps.Discover("mchy"));   // relit reports "already lit"
        Assert.True(wps.IsDiscovered("mchy")); // …and never goes dark
        Assert.Throws<ArgumentException>(() => wps.Discover("pokrzywlasy")); // no stone there
    }

    // ---- quests (§6.4) -----------------------------------------------------------

    [Fact]
    public void QuestState_MachineRejectsOutOfOrder_Transitions()
    {
        var q = new QuestState(new QuestRow("x", "x", 1, 1, QuestRewardKind.SkillPoint, 1, "", Array.Empty<string>()));
        Assert.Equal(QuestPhase.Inactive, q.Phase);
        Assert.False(q.TryMarkCompletable()); // cannot finish what never started
        Assert.False(q.TryRequestReward());
        Assert.False(q.TryClaimReward());    // cannot collect a reward that was never earned

        Assert.True(q.TryActivate());
        Assert.False(q.TryActivate());        // already active
        Assert.False(q.TryClaimReward());     // skipping reward-pending is refused

        Assert.True(q.TryMarkCompletable());
        Assert.True(q.TryRequestReward());
        Assert.True(q.TryClaimReward());
        Assert.Equal(QuestPhase.Done, q.Phase);
        Assert.False(q.TryActivate());        // Done is terminal
        Assert.False(q.TryRequestReward());
    }

    [Fact]
    public void QuestLog_PrereqsGateStarts_CycleAndBadRefsRejected()
    {
        var log = QuestLog.Load(Data("act1-quests.json"));
        Assert.False(log.TryActivate("glowa")); // needs Wrzeciono + Dary done

        foreach (var key in new[] { "gesi", "wrzeciono", "dary" })
        {
            Assert.True(log.TryActivate(key));
            Assert.True(log.Get(key).TryMarkCompletable());
            Assert.True(log.Get(key).TryRequestReward());
            Assert.True(log.Get(key).TryClaimReward());
        }
        Assert.True(log.TryActivate("glowa")); // act boss unlocks only now

        // A prereq cycle fails the load — hand-written two-quest ring.
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """
                { "quests": [
                  { "key": "a", "name": "A", "act": 1, "order": 1, "rewardKind": "SkillPoint",
                    "rewardAmount": 1, "goalAreaKey": "", "prereqs": ["b"] },
                  { "key": "b", "name": "B", "act": 1, "order": 2, "rewardKind": "SkillPoint",
                    "rewardAmount": 1, "goalAreaKey": "", "prereqs": ["a"] }
                ] }
                """);
            Assert.Throws<InvalidDataException>(() => QuestLog.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
