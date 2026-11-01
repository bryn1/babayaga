namespace BabaYaga.Sim.Tests;

/// <summary>
/// M4-S14 HERO DEATH KILLS THE HIRELING (card/11900; spec-3 §6.7b AMENDMENT,
/// MC 11898 — out/T0b-sec6-7b-amendment.md §2: official-era rule VERbatim
/// "if your character dies, your Hireling dies as well", D2 engine path
/// PLRMODE_StartXY_Dead → KillPlayerPets: the HIREABLE pet entry is FORCED
/// DEAD but KEPT — the captain's revive stays payable on the SAME mercenary).
/// The law as landed: at the hero's death transition (the TakeDamage life-floor
/// that flips IsDead — the v4 doctrine, IsDead derives from Life == 0) a LIVING
/// merc's life goes to 0 through its own TakeDamage floor — ONE deterministic
/// fact, no rolls, no gold; an already-dead merc is untouched (NO double-state —
/// its dump block stays byte-equal); Revive is the UNCHANGED door (town, cost
/// min(15·L²/2, 50000), the same merc at full life, entry kept); Respawn raises
/// the hero home at full life but does NOT carry a dead merc — the corpse
/// stays where it fell (§6.7b: "there is no corpse-carry"); Dismiss of a
/// dead-merc entry stays the no-refund FORFEIT it already was (state reading:
/// Dismiss paid NOTHING back before this beat too — no change landed for it).
/// NO save-format bump of its own: the merc block already persists Life (v6;
/// v7, card/11903, added the gear block beside it — this row rides the re-pin),
/// so a dead-dead pair round-trips and the stone still leaves her dead after the load.
/// RNG law: with NO merc the hero-death volley costs NOTHING — the capture
/// constants below were taken THIS SESSION from the UNMODIFIED base build
/// (wt-s14 @ a50d34e, the same idiom as HirelingTests' base captures).
/// RED-FIRST: on the unmodified sim the law rows below fail — hero death left
/// the mercenary ALIVE and Respawn carried her home (amendment §2 state reading).
/// </summary>
public class HirelingDeathTests
{
    private static GameSession Fresh(ulong seed = 11) => GameSession.New(seed, "koldun", TestData.DataDir());

    private static GameSession TownMerc(ulong seed = 11)
    {
        var s = Fresh(seed);
        s.Hire("strzelczyni");
        return s;
    }

    // "cost = min(15 × L² ÷ 2, 50,000) gold" (§3 R1/R2, carried — the formula
    // written OUT, the HirelingTests idiom; never read back from the door).
    private static long SpecReviveCost(int level) => Math.Min(15L * level * level / 2, 50000);

    /// <summary>THE death transition, RULE-TRUE: the hero dies to real counter
    /// damage (the one damage door in the rules — CounterStrike through Attack;
    /// the Character.Life setter is the authoring door, never this trigger).
    /// The merc stands back (CombatDistance 4 > the hero's pinned 1) so EVERY
    /// counter reaches the hero (§6.7 targeting), the merc stays alive right up
    /// to the instant the law must take it. Level 5 through the ladder verb
    /// (no RNG, no gold), mchy's ponurak row, the DeathLeg's own 2048 cap.</summary>
    private const int DeathCap = 2048;
    private static void StandAtMchyFight(GameSession s)
    {
        if (s.Merc is not null) s.Merc.CombatDistance = 4;
        if (s.Level < 5) s.LevelTo(5);
        if (!s.Waypoints.IsDiscovered("mchy")) s.DiscoverWaypoint("mchy");
        if (s.CurrentArea != "mchy") s.TravelTo("mchy");
    }

    private static void KillHeroByCounters(GameSession s)
    {
        StandAtMchyFight(s);
        int strikes = 0;
        for (; strikes < DeathCap && !s.IsDead; strikes++) s.Attack("ponurak", 1);
        Assert.True(s.IsDead, $"hero survived {strikes} counters — no death transition happened");
    }

    private static string MercRaw(GameSession s)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(s.Dump());
        return doc.RootElement.GetProperty("merc").GetRawText();
    }

    private static (ulong Combat, ulong Drop, ulong Enemy) RngWords(GameSession s)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(s.Dump());
        var r = doc.RootElement;
        return (r.GetProperty("combatRngState").GetUInt64(),
                r.GetProperty("dropRngState").GetUInt64(),
                r.GetProperty("enemyRngState").GetUInt64());
    }

    // ---- THE LAW — hero death kills the living hireling, same transition ----

    [Fact]
    public void HeroDeath_LivingMerc_DiesWithTheHero_InTheSameTransition()
    {
        var s = TownMerc();
        var m = s.Merc!;
        m.CombatDistance = 4;                           // every counter reaches the HERO (nearest)
        if (s.Level < 5) s.LevelTo(5);                  // the ladder verb: no RNG, no gold
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");

        int strikes = 0;
        long before = m.Life;                           // her life the moment the killing counter lands
        for (; strikes < DeathCap && !s.IsDead; strikes++)
        {
            Assert.False(m.IsDead, $"merc died at strike {strikes}, BEFORE the hero — targeting leak");
            before = m.Life;
            s.Attack("ponurak", 1);
        }

        Assert.True(s.IsDead);                          // the transition happened
        Assert.True(before > 0);                        // she STOOD alive the instant he fell
        Assert.Equal(0, m.Life);                        // RED pre-fix: the shipped sim left her ALIVE here
        Assert.True(m.IsDead);                          // DEAD derives from the same pool floor
    }

    // ---- already-dead: NO double-state, the block byte-equal ----

    [Fact]
    public void HeroDeath_DeadMerc_IsUntouched_ItsDumpBlockIsByteEqual()
    {
        var s = TownMerc();
        var m = s.Merc!;
        m.TakeDamage(m.MaxLife);            // she fell in fight BEFORE the hero
        Assert.True(m.IsDead);
        StandAtMchyFight(s);                // the corpse rides the EXISTING travel mirror (untouched this card)
        string blockBefore = MercRaw(s);

        KillHeroByCounters(s);              // the hero dies with the corpse standing

        Assert.True(s.IsDead);
        Assert.Equal(0, m.Life);            // floor, not minus-something — no re-kill, no re-state
        Assert.Equal(blockBefore, MercRaw(s)); // byte-equal dump block: the law changed NOTHING
    }

    // ---- revive after the hero's own death — the UNCHANGED door ----

    [Fact]
    public void Revive_AfterHeroDeath_PaysTheExactFormula_RestoresLifeKeepsEntry()
    {
        var s = TownMerc();
        var m = s.Merc!;

        KillHeroByCounters(s);              // the law: she died WITH him
        Assert.Equal(0, m.Life);
        s.Respawn();                        // the stone raises the hero home
        Assert.Equal(s.Areas.TownKey, s.CurrentArea);

        int level = m.Level;                // read at the revive instant — the grind may have leveled her (XP law, not this law)
        long xp = m.Xp;
        long goldBefore = s.Gold;
        var r = s.Revive("strzelczyni");    // the captain's door, entry KEPT (KillPlayerPets keeps it)
        long spec = SpecReviveCost(level);
        Assert.Equal(spec, r.Cost);                              // min(15·L²/2, 50000), written OUT above
        Assert.Equal(goldBefore - spec, s.Gold);
        Assert.Equal(m.MaxLife, r.Life);                       // full life…
        Assert.Equal(level, m.Level);                          // …same mercenary, level and XP intact
        Assert.Equal(xp, m.Xp);
        Assert.False(m.IsDead);
    }

    // ---- respawn: hero home full life — the merc STAYS dead, corpse in field ----

    [Fact]
    public void Respawn_AfterHeroDeath_HeroHomeFullLife_MercStaysDead_AreaUnmoved()
    {
        var s = TownMerc();
        var m = s.Merc!;

        KillHeroByCounters(s);
        string fellAt = m.Area;                                // she fell where she stood: mchy
        Assert.Equal("mchy", fellAt);
        Assert.Equal(0, m.Life);                               // the law already took her

        s.Respawn();

        Assert.False(s.IsDead);
        Assert.Equal(s.Character.MaxLife, s.Character.Life);  // the stone raises the hero at full
        Assert.Equal(s.Areas.TownKey, s.CurrentArea);
        Assert.Equal(0, m.Life);                               // RED pre-fix: shipped shipped her home ALIVE
        Assert.Equal(fellAt, m.Area);                          // no corpse-carry — the corpse stays in the field
    }

    // ---- RNG law: no-merc hero death costs NOTHING (base captures, §2 rows) ----
    // Constants captured THIS SESSION from the UNMODIFIED base build (wt-s14 @
    // a50d34e) running the script below verbatim: new koldun / levelto 5 / way
    // mchy / attack ponurak ×1 until dead (708 and 440 strikes — DEAD both).

    private static readonly (ulong Combat, ulong Drop, ulong Enemy, long Gold)[] BaseDeathCaptured =
    {
        (12512223059361791000, 1206270472369931075, 1559689999132351293, 1000), // seed 42
        (8850941034609448501, 9217431915075197834, 7044541831456611887, 1000),  // seed 43
    };

    [Theory]
    [InlineData(42UL)]
    [InlineData(43UL)]
    public void NoMerc_HeroDeath_RngWordsAndGold_AreByteIdenticalToBase(ulong seed)
    {
        var s = Fresh(seed);                                   // NO merc hired — the hook is inert
        s.LevelTo(5);
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        int strikes = 0;
        for (; strikes < DeathCap && !s.IsDead; strikes++) s.Attack("ponurak", 1);
        Assert.True(s.IsDead);

        var words = RngWords(s);
        var baseRow = BaseDeathCaptured[seed == 42 ? 0 : 1];
        Assert.Equal(baseRow.Combat, words.Combat);            // the hook rolls NOTHING
        Assert.Equal(baseRow.Drop, words.Drop);
        Assert.Equal(baseRow.Enemy, words.Enemy);
        Assert.Equal(baseRow.Gold, s.Gold);                    // and moves no gold
    }

    // ---- dismiss AFTER the hero's death = forfeit, no refund, gone ----
    // State reading: Dismiss already paid NOTHING back before this card — the
    // behavior is pinned here for the dead-merc entry specifically; no change
    // to the door landed (the LoD no-refund rule, amendment D3 forfeit).

    [Fact]
    public void Dismiss_AfterHeroDeath_IsTheForfeit_NoRefund_TheEntryIsGone()
    {
        var s = TownMerc();
        KillHeroByCounters(s);                                 // she died with him (law)
        s.Respawn();                                           // hero alive, in town, corpse behind

        long goldBefore = s.Gold;
        Assert.Equal("strzelczyni", s.Dismiss());              // the dead entry dismisses at the captain
        Assert.Equal(goldBefore, s.Gold);                      // FORFEIT — nothing back, the hire coin stays spent
        Assert.Null(s.Merc);
        var e = Assert.Throws<SessionRejectedException>(() => s.Revive("strzelczyni"));
        Assert.Contains("no hireling", e.Message);             // gone — the captain has no one to raise
    }

    // ---- the LAW needed no save change: Life rode the merc block (state reading) --

    [Fact]
    public void SaveRoundTrip_AfterHeroDeath_CarriesDeadDead_NoBump_StoneLeavesHerDead()
    {
        var s = TownMerc();
        var m = s.Merc!;
        KillHeroByCounters(s);                                 // hero 0, merc 0 — both dead at save time
        string path = Path.Combine(Path.GetTempPath(), $"hdeath-{Guid.NewGuid():N}.json");
        try
        {
            s.Save(path);
            var t = GameSession.Load(path, TestData.DataDir());
            Assert.Equal(7, SaveSerializer.SaveVersion);       // v7 (card/11903) bumped for the GEAR block — the LAW row itself needed no bump: the merc block already persisted Life (the b3d74f8 re-pin idiom)
            Assert.True(t.IsDead);                             // DEAD rode the v4 Life pool as always
            Assert.NotNull(t.Merc);
            Assert.Equal(0, t.Merc!.Life);                     // RED pre-fix: the base saved her ALIVE through
            Assert.True(t.Merc.IsDead);
            Assert.Equal(m.Area, t.Merc.Area);
            Assert.Equal(m.Xp, t.Merc.Xp);

            t.Respawn();                                       // the stone off the save…
            Assert.False(t.IsDead);
            Assert.Equal(0, t.Merc.Life);                      // …raises the hero and STILL leaves her dead
        }
        finally { File.Delete(path); }
    }
}
