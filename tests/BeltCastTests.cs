namespace BabaYaga.Sim.Tests;

/// <summary>
/// M4-S7 ACTIVE SKILL — ISKRA CASTS (LEDGER row 78, card/11839): the public
/// GameSession.Cast(skillId, monsterKey) verb. Gates fire IN ORDER, each an
/// ORIGINAL-phrased SessionRejectedException: (1) RaiseIfDead FIRST — Cast is
/// the EIGHTH gated verb (dead + a bogus skill id still hears "you are dead");
/// (2) unknown skill; (3) class gate — the loaded weave table must be the
/// koldun's ("the koldun weaves; the others swing"; asserted through a foreign
/// classKey on a copy of the weave table, the one state a single-class build
/// can reach); (4) prereq unmet per data/koldun-skills.json; (5) no skill point
/// spent into the weave (the existing SpendSkill state); (6) mana below the
/// cost — "too little mana to weave". EFFECT: ember damage = uniform(min..max)
/// on the seeded CombatRng (the tests replay Rng(seed) for the exact value);
/// synergy percent-per-point from the DATA FILE (read here, never hardcoded)
/// multiplies the roll as ONE integer floor at the end; elemental weaving
/// NEVER MISSES — no to-hit roll (the D2-verified RULE, not a number); the
/// kill runs the SAME KillFlow chain Attack pays (XP equal to the table output
/// for that kind, drops unchanged); mana falls by exactly the cost. Mana
/// REGEN is NOT this beat — the belt stays the only refill (LEDGER row 79).
/// Koldun level-1 anchors: MaxLife 50, MaxMana 30 (BeltPotionTests).
/// </summary>
public class BeltCastTests
{
    private static GameSession Fresh(ulong seed = 9) => GameSession.New(seed, "koldun", TestData.DataDir());

    /// <summary>The mana cost the DATA FILE sets for a skill — the source of
    /// truth the assertions read, never a constant repeated here.</summary>
    private static long CostOf(string skillId) =>
        TestData.KoldunSkills().Find(skillId)!.ManaCost;

    /// <summary>The engaged monster's live Life read through the public surface:
    /// the session's own Dump() JSON (camelCase engagedLife) — the JourneyLeg
    /// mirror idiom, no Sim internal touched.</summary>
    private static long EngagedLife(GameSession s)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(s.Dump());
        return doc.RootElement.GetProperty("engagedLife").GetInt64();
    }

    /// <summary>A session whose LOADED weave table carries a foreign classKey —
    /// a copy of the repo data dir with koldun-skills.json's classKey swapped.
    /// This is the "not the koldun's weaving" state the class gate reads; the
    /// koldun remains the only constructible profile until the 6-class wave.</summary>
    private static GameSession ForeignWeaveTable(ulong seed = 9)
    {
        var src = TestData.DataDir();
        var tmp = Path.Combine(Path.GetTempPath(), $"cast-foreign-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmp);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(tmp, Path.GetFileName(f)));
        var skills = Path.Combine(tmp, "koldun-skills.json");
        File.WriteAllText(skills, File.ReadAllText(skills)
            .Replace("\"classKey\": \"koldun\"", "\"classKey\": \"zbrojnik\""));
        return GameSession.New(seed, "koldun", tmp); // profile builds; the TABLE is foreign
    }

    private static GameSession DeadInMchy(ulong seed = 9)
    {
        var s = Fresh();
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        s.Character.TakeDamage(s.Character.Life);
        return s;
    }

    /// <summary>Learn a weave the legal door: one reward point into the skill
    /// (the Gesi-rite idiom of probe-p12, shortened to the point + SpendSkill).</summary>
    private static void LearnOne(GameSession s, string skillId)
    {
        s.Character.GrantSkillPoints(1);
        s.SpendSkill(skillId);
    }

    // ---- gate 2: unknown skill (before every other precondition but death) ----

    [Fact]
    public void Cast_UnknownSkill_ThrowsNamedRejection_ChangesNothing()
    {
        var s = Fresh();
        var e = Assert.Throws<SessionRejectedException>(() => s.Cast("popielny-kamyk", "pokrzywnik"));
        Assert.Contains("popielny-kamyk", e.Message);
        Assert.Equal(s.Character.Mana, s.Character.MaxMana); // nothing spent…
        Assert.Null(s.EngagedKey); // …nothing engaged
    }

    // ---- gate 3: class gate — only the koldun's weave table can be woven ----

    [Fact]
    public void Cast_ForeignWeaveTable_ThrowsTheClassLine()
    {
        var s = ForeignWeaveTable();
        LearnOne(s, "iskra"); // the point spends — the TABLE gate speaks at Cast
        var e = Assert.Throws<SessionRejectedException>(() => s.Cast("iskra", "pokrzywnik"));
        Assert.Equal("the koldun weaves; the others swing", e.Message);
        Assert.Equal(s.Character.MaxMana, s.Character.Mana); // the weave was never paid for
    }

    [Fact]
    public void Cast_UnknownSkill_SpeaksBefore_TheClassGate()
    {
        var s = ForeignWeaveTable();
        var e = Assert.Throws<SessionRejectedException>(() => s.Cast("popielny-kamyk", "pokrzywnik"));
        Assert.DoesNotContain("weaves", e.Message); // gate ORDER: unknown first, class second
        Assert.Contains("popielny-kamyk", e.Message);
    }

    // ---- gates 4 + 5: prereq first, then the spent-point state ----

    [Fact]
    public void Cast_PointsNeverSpent_ThrowsUnspentRejection()
    {
        var s = Fresh(); // a fresh koldun knows nothing
        var e = Assert.Throws<SessionRejectedException>(() => s.Cast("iskra", "pokrzywnik"));
        Assert.Contains("iskra", e.Message);
        Assert.Contains("never woven", e.Message);
    }

    [Fact]
    public void Cast_PrereqGate_SpeaksBefore_TheSpendingGate_ExpectedFromData()
    {
        // zarzewie has no points AND its prereq (iskra) is unmet: with the gate
        // order pinned, the PREREQ line must speak — and the point count it names
        // is read from data/koldun-skills.json, not hardcoded here.
        var pre = TestData.KoldunSkills().Find("zarzewie")!.Prereqs.Single();
        var s = Fresh();
        s.Character.Mana = 0; // gate 4/5 precede mana: the message stays the prereq one
        var e = Assert.Throws<SessionRejectedException>(() => s.Cast("zarzewie", "pokrzywnik"));
        Assert.Contains(pre.SkillId, e.Message);
        Assert.Contains("waits", e.Message);
        Assert.DoesNotContain("mana", e.Message);
    }

    // ---- gate 6: the mana pool ----

    [Fact]
    public void Cast_TooLittleMana_ThrowsTooLittleManaToWeave_ChangesNothing()
    {
        var s = Fresh();
        LearnOne(s, "iskra");
        long cost = CostOf("iskra");
        Assert.True(cost > 0, "the data file owes iskra a real mana cost — the test is not vacuous");
        s.Character.Mana = cost - 1; // the exact below-cost boundary
        var xp0 = s.Character.Xp;

        var e = Assert.Throws<SessionRejectedException>(() => s.Cast("iskra", "pokrzywnik"));
        Assert.Contains("too little mana to weave", e.Message);

        Assert.Equal(cost - 1, s.Character.Mana); // pool untouched…
        Assert.Null(s.EngagedKey); // …no engagement spawned…
        Assert.Equal(xp0, s.Character.Xp); // …and no XP moved
    }

    [Fact]
    public void Cast_ManaExactlyTheCost_IsAccepted_PoolLandsAtZero()
    {
        var s = Fresh();
        LearnOne(s, "iskra");
        s.Character.Mana = CostOf("iskra"); // the boundary: cost exactly is NOT too little

        var r = s.Cast("iskra", "pokrzywnik");

        Assert.Equal(0, r.ManaAfter);
        Assert.Equal(0, s.Character.Mana);
    }

    // ---- effect: mana cost and the ember roll ----

    [Fact]
    public void Cast_LowersManaExactlyTheDataCost()
    {
        var s = Fresh(3);
        LearnOne(s, "iskra");
        long before = s.Character.Mana, cost = CostOf("iskra");

        var r = s.Cast("iskra", "pokrzywnik");

        Assert.Equal(before, r.ManaBefore);
        Assert.Equal(before - cost, r.ManaAfter);
        Assert.Equal(before - cost, s.Character.Mana); // the SESSION reads the drop…
        Assert.Equal(before - 2 * cost, s.Cast("iskra", "pokrzywnik").ManaAfter); // …and twice
    }

    [Fact]
    public void Cast_EmberRoll_MatchesTheSeededStream_ExactValueOnce()
    {
        // CombatRng = Rng(seed); a fresh session's first Cast takes the stream's
        // FIRST roll — replayed here through the public Rng, nothing hardcoded.
        const ulong seed = 42;
        var def = TestData.KoldunSkills().Find("iskra")!;
        long expected = new Rng(seed).NextInRange(def.DamageMin, def.DamageMax);

        var s = Fresh(seed);
        LearnOne(s, "iskra");
        var r = s.Cast("iskra", "pokrzywnik");

        Assert.Equal(expected, r.Damage);
    }

    [Theory]
    [InlineData(1UL)] [InlineData(2UL)] [InlineData(3UL)] [InlineData(4UL)] [InlineData(5UL)]
    [InlineData(6UL)] [InlineData(7UL)] [InlineData(8UL)] [InlineData(9UL)] [InlineData(10UL)]
    [InlineData(11UL)] [InlineData(12UL)] [InlineData(13UL)] [InlineData(14UL)] [InlineData(15UL)]
    [InlineData(16UL)] [InlineData(17UL)] [InlineData(18UL)] [InlineData(19UL)] [InlineData(20UL)]
    public void Cast_EmberRoll_StaysInsideTheDataRange_AcrossTwentySeeds(ulong seed)
    {
        var def = TestData.KoldunSkills().Find("iskra")!;
        var s = Fresh(seed);
        LearnOne(s, "iskra");
        var r = s.Cast("iskra", "pokrzywnik");
        Assert.InRange(r.Damage, def.DamageMin, def.DamageMax); // seed-fixed range, uniform
    }

    [Fact]
    public void Cast_LowersMonsterLifeByExactlyTheRolledEmber()
    {
        var s = Fresh(5);
        LearnOne(s, "iskra");
        var area = s.Areas.Get(s.CurrentArea);
        var row = s.Monsters.Get("pokrzywnik");
        long spawnLife = s.Monsters.Spawn(row,
            MonsterTable.ResolveSpawnLevel(row, Difficulty.Ordinary, area)).Life;

        var r = s.Cast("iskra", "pokrzywnik");

        Assert.False(r.Killed);
        Assert.Equal(spawnLife - r.Damage, r.TargetLife); // the report's own arithmetic…
        Assert.Equal(spawnLife - r.Damage, EngagedLife(s)); // …equals the session's mirror
    }

    // ---- synergy math: percent-per-point from the data file, ONE floor ----

    [Fact]
    public void Cast_SynergyMath_OneSourcePoint_FloorsOnce_ExpectedFromData()
    {
        const ulong seed = 7;
        var data = TestData.KoldunSkills();
        var rz = data.Find("zarzewie")!;
        var syn = rz.Synergies.Single(y => y.SourceId == "iskra");
        // level 6 unlocks the row-1 weave; 5 granted points fund iskra 1 + zarzewie 1
        var s = Fresh(seed);
        s.LevelTo(6);
        s.SpendSkill("iskra");
        s.SpendSkill("zarzewie");
        s.Character.Mana = s.Character.MaxMana;
        long expected = new Rng(seed).NextInRange(rz.DamageMin, rz.DamageMax)
            * (100 + syn.PercentPerPoint * 1) / 100; // multiply first, floor ONCE

        var r = s.Cast("zarzewie", "pokrzywnik");

        Assert.Equal(expected, r.Damage);
        Assert.Equal(s.Character.MaxMana - rz.ManaCost, r.ManaAfter);
    }

    [Fact]
    public void Cast_SynergyMath_ThreeSourcePoints_ExpectedFromData()
    {
        const ulong seed = 11;
        var data = TestData.KoldunSkills();
        var rz = data.Find("zarzewie")!;
        var syn = rz.Synergies.Single(y => y.SourceId == "iskra");
        var s = Fresh(seed);
        s.LevelTo(6);
        s.SpendSkill("iskra"); s.SpendSkill("iskra"); s.SpendSkill("iskra"); // tier cap 3
        s.SpendSkill("zarzewie");
        s.Character.Mana = s.Character.MaxMana;
        long expected = new Rng(seed).NextInRange(rz.DamageMin, rz.DamageMax)
            * (100 + syn.PercentPerPoint * 3) / 100; // 3 hard points × the data's percent

        var r = s.Cast("zarzewie", "pokrzywnik");

        Assert.Equal(expected, r.Damage);
    }

    // ---- the kill runs the SAME KillFlow chain Attack pays ----

    [Fact]
    public void CastKill_XpEqualsTheKillFlowTableOutput_ForThatKind()
    {
        var s = Fresh(3);
        LearnOne(s, "iskra");
        var area = s.Areas.Get(s.CurrentArea);
        var row = s.Monsters.Get("pokrzywnik");
        int monLevel = MonsterTable.ResolveSpawnLevel(row, Difficulty.Ordinary, area);
        long expected = s.Ladder.KillXp(s.Monsters.StatsAt(row, monLevel).Xp,
            KillType.Normal, 1, s.Character.Level, monLevel); // the data-derived table XP
        Assert.True(expected > 0, "the table owes XP for this kind — the test is not vacuous");

        long xp0 = s.Character.Xp;
        var r = s.Cast("iskra", "pokrzywnik");
        for (int cast = 2; !r.Killed && cast <= 8; cast++) r = s.Cast("iskra", "pokrzywnik");

        Assert.True(r.Killed, "the seed-fixed casts must fell the pokrzywnik");
        Assert.Equal(expected, s.Character.Xp - xp0); // ONE kill's table XP — no to-hit miss tax
        Assert.Null(s.EngagedKey); // the kill clears the engagement, as Attack does
        Assert.True(s.Loot(1).Taken <= 1); // the drop queue is live after a cast-kill
    }

    // ---- gate 1: Cast is the EIGHTH gated verb — the dead gate speaks first ----

    [Fact]
    public void WhileDead_EightGatedVerbs_AllThrowYouAreDead_CastIncluded()
    {
        var s = DeadInMchy();
        (string Name, Action Verb)[] verbs =
        [
            ("Attack", () => s.Attack("pokrzywnik", 1)),
            ("UsePotion", () => s.UsePotion(1)),
            ("Buy", () => s.Buy(GameSession.LifePotionId)),
            ("SellLast", () => s.SellLast()),
            ("TravelTo", () => s.TravelTo("grodzisko")),
            ("Loot", () => s.Loot(1)),
            ("Gamble", () => s.Gamble()),
            ("Cast", () => s.Cast("iskra", "pokrzywnik")), // THE EIGHTH VERB
        ];
        foreach (var (name, verb) in verbs)
        {
            try
            {
                verb();
                Assert.Fail($"{name} accepted while dead");
            }
            catch (SessionRejectedException e)
            {
                Assert.Equal("you are dead", e.Message); // the gate speaks FIRST (row 64 contract)
            }
        }
    }

    [Fact]
    public void WhileDead_Cast_GateSpeaksBefore_EveryPrecondition()
    {
        var s = DeadInMchy();
        // dead + unknown skill + non-spent + zero mana: the DEAD line still answers
        var e = Assert.Throws<SessionRejectedException>(() => s.Cast("popielny-kamyk", "pokrzywnik"));
        Assert.Equal("you are dead", e.Message);
    }

    // ---- the mana pool already rides the v4 save: no format bump for casting ----

    [Fact]
    public void Cast_LoweredMana_SurvivesSaveRoundTrip_SaveCarriesVersion6()
    {
        var s = Fresh(3);
        LearnOne(s, "iskra");
        var r = s.Cast("iskra", "pokrzywnik");
        Assert.Equal(s.Character.MaxMana - CostOf("iskra"), r.ManaAfter);

        var before = s.Dump();
        var path = Path.Combine(Path.GetTempPath(), $"cast-{Guid.NewGuid():N}.json");
        try
        {
            s.Save(path);
            var loaded = GameSession.Load(path, TestData.DataDir());
            Assert.Equal(before, loaded.Dump()); // byte-identical round trip (foundation contract)
            Assert.Equal(r.ManaAfter, loaded.Character.Mana); // the spent pool carries…
            Assert.Equal(6, SaveSerializer.SaveVersion); // …unchanged by casting — v6's only new field is the stash block (row 74, card/11893; the 88b70b2 re-pin idiom)
        }
        finally { File.Delete(path); }
    }
}
