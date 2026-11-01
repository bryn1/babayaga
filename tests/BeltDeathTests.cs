namespace BabaYaga.Sim.Tests;

/// <summary>
/// M4-S6 HERO DEATH + RESPAWN (LEDGER row 64, card/11836): TakeDamage bringing
/// Life to 0 sets the public IsDead (the session enters DEAD). While DEAD the
/// pinned mutating verbs — Attack, UsePotion, Buy, SellLast, TravelTo, Loot,
/// Gamble — ALL throw SessionRejectedException("you are dead") through ONE gate,
/// checked FIRST so the dead hero hears the death, never a per-verb precondition.
/// Respawn clears DEAD, restores Life to MaxLife (single-player stone-resurrection
/// reads full life), relocates the hero to the last discovered TOWN through the
/// lit stones (adjacency bypassed — resurrection rides the stones, not the road),
/// and applies the XP penalty THROUGH the built-M3 DeathPenalty API only: the
/// tests assert equality against that helper's own output, never a re-derived
/// percent. The band rides New's optional difficulty parameter (default Ordinary
/// — the M4 demo band), so BOTH the 0 % and the 10 % row are demonstrated.
/// DEAD persists through the save with NO format bump: the v4 file already
/// carries the Life pool, and death is read FROM it (Life 0 ⇒ dead).
/// Koldun level-1 anchors: MaxLife 50 (BeltPotionTests).
/// </summary>
public class BeltDeathTests
{
    private static GameSession Fresh(ulong seed = 9) => GameSession.New(seed, "koldun", TestData.DataDir());

    /// A dead hero standing away from town: travel legal-verb to mchy, die there.
    private static GameSession DeadInMchy(ulong seed = 9)
    {
        var s = Fresh();
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        s.Character.TakeDamage(s.Character.Life); // exact to 0 — TakeDamage is the pinned trigger
        return s;
    }

    // ---- death: TakeDamage to 0 sets IsDead ----

    [Fact]
    public void TakeDamage_ToZeroLife_SetsIsDead_PoolFloorsAtZero()
    {
        var s = Fresh();
        Assert.False(s.IsDead); // a fresh hero is alive…
        Assert.Equal(s.Character.MaxLife, s.Character.Life); // …at full life

        s.Character.TakeDamage(s.Character.Life - 1); // one life left — still alive
        Assert.False(s.IsDead);

        s.Character.TakeDamage(5); // overshoot: the pool floors, the hero dies
        Assert.Equal(0, s.Character.Life);
        Assert.True(s.IsDead);
        Assert.True(s.Character.IsDead); // the session enters DEAD with its hero
    }

    // ---- the DEAD gate: every pinned mutating verb refuses, nothing half-applies ----

    [Fact]
    public void WhileDead_EveryGatedVerb_ThrowsYouAreDead()
    {
        var s = DeadInMchy();
        Assert.True(s.IsDead);

        (string Name, Action Verb)[] verbs =
        [
            ("Attack", () => s.Attack("pokrzywnik", 1)),
            ("UsePotion", () => s.UsePotion(1)),
            ("Buy", () => s.Buy(GameSession.LifePotionId)),
            ("SellLast", () => s.SellLast()),
            ("TravelTo", () => s.TravelTo("grodzisko")),
            ("Loot", () => s.Loot(1)),
            ("Gamble", () => s.Gamble()),
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
                Assert.Equal("you are dead", e.Message); // the gate speaks FIRST, before any precondition
            }
        }
    }

    [Fact]
    public void WhileDead_GatedVerbs_ChangeNothing()
    {
        var s = DeadInMchy();
        var (gold, carried, xp, area) = (s.Gold, s.Carried.Count, s.Character.Xp, s.CurrentArea);

        Assert.Throws<SessionRejectedException>(() => s.Attack("pokrzywnik", 4));
        Assert.Throws<SessionRejectedException>(() => s.Buy(GameSession.LifePotionId));
        Assert.Throws<SessionRejectedException>(() => s.Loot(5));

        Assert.Equal(gold, s.Gold);
        Assert.Equal(carried, s.Carried.Count);
        Assert.Equal(xp, s.Character.Xp);
        Assert.Equal(area, s.CurrentArea);
    }

    [Fact]
    public void TheSameVerbs_WhileAlive_AreAccepted_TheThrowIsTheDeadGate()
    {
        var s = Fresh(); // same Fresh seed, alive, in town
        s.Loot(1); // legal alive (empty pile, zero taken)
        Assert.True(s.Buy(GameSession.LifePotionId) > 0);
        Assert.True(s.SellLast() >= 0);
        s.Attack("pokrzywnik", 1);
        s.Gamble(); // none of these throws while the hero stands
    }

    [Fact]
    public void RealCounterDamage_CanKillTheHero_SetsIsDead()
    {
        var s = Fresh(42);
        int round = 0;
        for (; round < 200 && !s.IsDead; round++)
            s.Attack("pokrzywnik", 8); // counters grind the pool (measured: death ≈ strike 549); the volley stops at death
        Assert.True(s.IsDead, $"hero survived {round} × 8 strikes (life {s.Character.Life})");
        Assert.Equal(0, s.Character.Life);
    }

    // ---- Respawn: cleared, full life, last-discovered town, penalty via the helper ----

    [Fact]
    public void Respawn_ClearsDead_FullLife_RelocatesToLastDiscoveredTown()
    {
        var s = DeadInMchy();
        Assert.Equal("mchy", s.CurrentArea); // died away from home

        s.Respawn();

        Assert.False(s.IsDead);
        Assert.Equal(s.Character.MaxLife, s.Character.Life); // stone-resurrection reads full life
        Assert.Equal(s.Areas.TownKey, s.CurrentArea); // the last discovered town carried him home
        Assert.True(s.Waypoints.IsDiscovered(s.Areas.TownKey)); // he rode a LIT stone
    }

    [Fact]
    public void Respawn_WhileAlive_Throws_NotADoubleHeal_NoDoublePenalty()
    {
        var s = Fresh();
        var e = Assert.Throws<SessionRejectedException>(() => s.Respawn());
        Assert.Contains("not dead", e.Message);
        Assert.Equal(s.Character.MaxLife, s.Character.Life); // untouched

        var dead = DeadInMchy();
        dead.Respawn();
        Assert.Throws<SessionRejectedException>(() => dead.Respawn()); // a second raise is refused
    }

    [Fact]
    public void Respawn_AfterDeath_TheSessionIsLive_Again()
    {
        var s = DeadInMchy();
        s.Respawn();

        Assert.True(s.Buy(GameSession.LifePotionId) > 0); // the economy speaks again
        Assert.True(s.Attack("pokrzywnik", 1).Strikes >= 1); // and the strikes
    }

    [Fact]
    public void Respawn_NormalBand_XpUnchanged_EqualToDeathPenaltyHelperOutput()
    {
        var s = Fresh(); // default band = Ordinary: the M4 demo world
        s.LevelTo(5);
        s.Character.GrantXp(1234, s.Ladder); // sit mid-span of level 5
        long xp0 = s.Character.Xp;
        var expect = DeathPenalty.Apply(s.Ladder, xp0, s.Character.Level, Difficulty.Ordinary);
        Assert.Equal(xp0, expect.XpAfter); // the helper's own 0 % row, for the assert below

        s.Character.TakeDamage(s.Character.Life);
        s.Respawn();

        Assert.Equal(xp0, s.Character.Xp); // normal xp unchanged…
        Assert.Equal(expect.XpAfter, s.Character.Xp); // …and equal to the helper, by construction
    }

    [Fact]
    public void Respawn_HellBand_XpEqualsDeathPenaltyHelperOutput_NoLevelDrop()
    {
        var s = GameSession.New(9, "koldun", TestData.DataDir(), Difficulty.Hell); // band selectable at New
        s.LevelTo(5);
        s.Character.GrantXp(1234, s.Ladder);
        long xp0 = s.Character.Xp;
        int level0 = s.Character.Level;

        var expect = DeathPenalty.Apply(s.Ladder, xp0, level0, Difficulty.Hell);
        Assert.True(expect.Lost > 0, "the helper owes a loss on this input — the test is not vacuous");

        s.Character.TakeDamage(s.Character.Life);
        s.Respawn();

        // EXACTLY what DeathPenalty's own helper computes for that xp — never 10 % re-derived by hand:
        Assert.Equal(expect.XpAfter, s.Character.Xp);
        Assert.Equal(xp0 - expect.Lost, s.Character.Xp);
        Assert.Equal(level0, s.Character.Level); // the helper clamps at the level start: no de-level
        Assert.False(s.IsDead);
    }

    // ---- DEAD rides the save: v4 persists the Life pool; death is read FROM it ----

    [Fact]
    public void DeadSession_SurvivesSaveRoundTrip_StillRefuses_RespawnsFromLoad()
    {
        var s = DeadInMchy();
        var before = s.Dump();
        var path = Path.Combine(Path.GetTempPath(), $"death-{Guid.NewGuid():N}.json");
        try
        {
            s.Save(path);
            var loaded = GameSession.Load(path, TestData.DataDir());

            Assert.Equal(before, loaded.Dump()); // byte-identical round trip (foundation contract)
            Assert.True(loaded.IsDead); // DEAD survived — persisted as the v4 Life pool of 0
            Assert.Equal(0, loaded.Character.Life);
            Assert.Equal("mchy", loaded.CurrentArea);
            var e = Assert.Throws<SessionRejectedException>(() => loaded.Attack("pokrzywnik", 1));
            Assert.Equal("you are dead", e.Message);

            loaded.Respawn(); // and the stone raises him straight off the save
            Assert.False(loaded.IsDead);
            Assert.Equal(loaded.Character.MaxLife, loaded.Character.Life);
            Assert.Equal(loaded.Areas.TownKey, loaded.CurrentArea);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SaveFormat_StaysVersion6_StashBlockOnly_DeadDerivedFromThePersistedLife()
    {
        // The version decision (card/11836) held through v4; v5 (card/11886,
        // §6.7 rows 65–66) added the hireling block; v6 (card/11893, row 74)
        // exists ONLY for the stash block — DEAD stays
        // derived from the persisted Life exactly as before, every older field
        // keeps its dump position (the new blocks are appended last), and a
        // foreign version, 4 and 5 included, is refused (the version-refusal idiom),
        // never silently half-restored.
        Assert.Equal(6, SaveSerializer.SaveVersion);
        var s = Fresh();
        Assert.Contains("\"life\":50", s.Dump().Replace(" ", "")); // alive: the pool is the truth
        s.Character.TakeDamage(s.Character.Life);
        Assert.Contains("\"life\":0", s.Dump().Replace(" ", "")); // dead: the same field carries it
    }
}
