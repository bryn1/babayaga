namespace BabaYaga.Sim.Tests;

/// <summary>
/// M4-S11 HIRELINGS — the AMENDED spec §6.7 lives (LEDGER rows 65–66, card/11886;
/// adoption contract out/T0b-sec6-7-amendment.md §5 + out/T0b-mechanics-spec-3.md
/// §6.7 AMENDED; owner D2-oracle grant 2026-10-30, verbatim on the card: "…all the
/// tests and verifications against diablo 2 lod…"). Contract under test:
///   • hire price  Gold × (100 + 15 × levels_above_bracket) / 100 (§6.7 carried
///     forward unchanged; the clone hires its rookie at ORIGINAL-DESIGN level 1,
///     so at hire levels_above = max(0, 1−bracket) = 0 and price = hireGold);
///   • merc XP     full chain AT ITS OWN LEVEL on the merc's own kill,
///     floor(×86/256) of the CAPPED figure on hero kills, per-kill cap 1/64 of the
///     span its current level takes (ladder XpAt(L) = Exp/Lvl × L² × (L+1), witness
///     §2: E=120, L=85 → span/64 = 41,441), level ≤ hero and ≤ 98;
///   • revive      min(15 × L² / 2, 50000) gold AT TOWN, same merc restored at full
///     life (witnesses §3 R2: 41,070 at L74; capped from L82); NO corpse rule;
///   • regen       merc life per frame = max × DamageRegen ÷ 4096 in 256ths
///     (DamageRegen 4, the Acts-1–3 monstats witness — the exact constant is the
///     named GAP of amendment §4 Q3, the BEHAVIOUR is VERIFIED), dead mercs never;
///   • targeting   a countering monster strikes the NEAREST valid target among
///     hero + alive mercs (ties go to the incumbent hero — with no merc hired the
///     choice is always hero, pinned byte-for-byte by the RNG-identity rows).
/// Expected values derive from the SPEC FORMULAS WRITTEN OUT below, never read
/// back from the implementation (the BeltRegenTests SpecQuantum256 idiom).
/// </summary>
public class HirelingTests
{
    private static GameSession Fresh(ulong seed = 11) => GameSession.New(seed, "koldun", TestData.DataDir());

    // ---- the §6.7 formulas, written OUT (the spec text quoted at each) ----

    // "hire price gold*(100+15*levels_above_bracket)/100" (carried forward, §6.7).
    private static long SpecPrice(long gold, int levelsAboveBracket) =>
        gold * (100 + 15 * levelsAboveBracket) / 100;

    // "XP ladder Exp/Lvl × L² × (L+1)" (§2 witness: E 120 at L85 → 74,562,000).
    private static long SpecXpAt(int expPerLevel, int level) =>
        (long)expPerLevel * level * level * (level + 1);
    // "1/64 of the experience its current level takes" (d2r.org/mechanics/experience):
    // the span is XpAt(L+1) − XpAt(L); the cap floors it at /64.
    private static long SpecPerKillCap(int expPerLevel, int level) =>
        (SpecXpAt(expPerLevel, level + 1) - SpecXpAt(expPerLevel, level)) / 64;
    // "86/256 of the capped figure on your/minion kills" (§6.7 AMENDED, verbatim).
    private static long SpecShared(long capped) => capped * 86 / 256;
    // The full kill chain at the merc's OWN LEVEL is XpTable's own P1 §1.7 row
    // (base × killtype/100 × (players+1)/2 × gap/100 × ExpRatio/1024) — restated
    // here from that quoted formula for the single-player Normal case below.
    private static long SpecChain(XpTable ladder, long baseXp, int mercLevel, int monsterLevel) =>
        baseXp * 100 / 100 * (1 + 1) / 2
        * XpTable.LevelGapFactorPercent(mercLevel, monsterLevel) / 100
        * ladder.ExpRatioForLevel(mercLevel) / 1024;

    // "cost = min(15 × L² ÷ 2, 50,000) gold" (§3 R1/R2, code + server witnesses).
    private static long SpecReviveCost(int level) => Math.Min(15L * level * level / 2, 50000);

    // "monster life a frame = maximum life × DamageRegen ÷ 4096" (§4 Q2) — in the
    // Regen.cs 256ths idiom; DamageRegen 4 is the Acts-1–3 monstats witness (Q1).
    private const int SpecDamageRegen = 4;
    private static long SpecMercQuantum256(long maxLife) => maxLife * 256 * SpecDamageRegen / 4096;
    private static int SpecMercFrames(long missing, long maxLife) =>
        (int)Math.Ceiling(missing * 256.0 / SpecMercQuantum256(maxLife));

    // ---- helpers: the real town session the captain lives in ----

    private static GameSession TownWithMerc(ulong seed = 11)
    {
        var s = Fresh(seed);
        s.Hire("strzelczyni");
        return s;
    }

    private static (ulong Combat, ulong Drop, ulong Enemy) RngWords(GameSession s)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(s.Dump());
        var r = doc.RootElement;
        return (r.GetProperty("combatRngState").GetUInt64(),
                r.GetProperty("dropRngState").GetUInt64(),
                r.GetProperty("enemyRngState").GetUInt64());
    }

    // ================= hire price — exact at bracket / +2 / +5 =================

    [Theory]
    [InlineData("strzelczyni", 3, 100)]   // the V100 Rogue Normal bracket row (row 14)
    [InlineData("strzelczyni", 5, 130)]   // +2 above: 100×130/100
    [InlineData("strzelczyni", 8, 175)]   // +5 above: 100×175/100
    [InlineData("witez", 9, 250)]
    [InlineData("witez", 11, 325)]        // 250×130/100
    [InlineData("witez", 14, 437)]        // 250×175/100 = 437.5, integer floor
    [InlineData("rzutca", 28, 500)]
    [InlineData("rzutca", 30, 650)]   // 500×130/100
    [InlineData("rzutca", 33, 875)]
    public void HirePrice_MatchesTheSpecFormula_AtBracketPlusTwoPlusFive(
        string id, int level, long expected)
    {
        var s = Fresh();
        var row = s.Hirelings.For(id);
        long above = Math.Max(0, level - row.Bracket.Level);
        Assert.Equal(SpecPrice(row.HireGold, (int)above), expected); // the derivation is the spec one
        Assert.Equal(expected, HirelingTable.HirePrice(row, level));
    }

    [Fact]
    public void HirePrice_ALevel1Rookie_IsExactlyTheRowGold_AboveClampedAtZero()
    {
        var s = TownWithMerc(); // the clone's ORIGINAL-DESIGN hire level is 1
        var row = s.Hirelings.For("strzelczyni");
        Assert.Equal(1, s.Merc!.Level);
        Assert.Equal(row.HireGold, HirelingTable.HirePrice(row, 1)); // levels_above max(0, 1−3) = 0
        Assert.Equal(900, s.Gold);                                   // 1000 − price 100, session-read
    }

    /// The pinned §6.7 rookie below its bracket row: the engine-literal growth
    /// (bracket + nLevelUps × column, NEGATIVE nLevelUps below the bracket — the
    /// D2MOO line subtracts verbatim) lands the L1 strzelczyni at AR 10−24 → the
    /// ORIGINAL-DESIGN ≥0 clamp, i.e. an almost-hitting rookie. That is the
    /// adoption reading STATED as a gap; scenarios where the mercenary must
    /// actually fight author it up to its bracket row (3) through the public
    /// state door (the Character.Life authoring doctrine) — named at each use.
    private static Hireling BracketMerc(GameSession s, string id = "strzelczyni")
    {
        if (s.Merc is null || s.Merc.Id != id) s.Hire(id);
        s.Merc!.Level = s.Hirelings.For(id).Bracket.Level; // raise to the adopted row's level
        s.Merc.Xp = HirelingXp.XpAt(s.Merc.ExpPerLevel, s.Merc.Level); // keep the ladder consistent
        return s.Merc;
    }

    // ================= revive cost — L1 / L74 / the 50000 cap =================

    [Theory]
    [InlineData(1, 7)]       // 15·1/2 = 7 (integer floor)
    [InlineData(74, 41070)]  // the §3 R2 server witness: 15·74²/2 = 41,070
    [InlineData(81, 49207)]  // raw 49,207 — still below the cap
    [InlineData(82, 50000)]  // raw 50,430 → CAPPED — "41,070 at level 74; 50,000 at 82 and above"
    [InlineData(99, 50000)]
    public void ReviveCost_IsTheSpecMin_AtLevelOneAtL74AndPastTheCap(int level, long expected)
    {
        Assert.Equal(SpecReviveCost(level), expected);
        Assert.Equal(expected, Hireling.ReviveCost(level));
    }

    [Fact]
    public void Revive_PaysTheCostAtTown_RestoresTheSameMercAtFullLife()
    {
        var s = TownWithMerc();
        s.Merc!.TakeDamage(s.Merc.MaxLife); // killed in the field, carried state
        Assert.True(s.Merc.IsDead);
        long gold = s.Gold;

        var r = s.Revive("strzelczyni");
        Assert.Equal(SpecReviveCost(s.Merc.Level), r.Cost); // 7 at L1
        Assert.Equal(gold - 7, s.Gold);
        Assert.False(s.Merc.IsDead);
        Assert.Equal(s.Merc.MaxLife, s.Merc.Life); // "a revive brings the same mercenary back at full life" (R5)
    }

    [Fact]
    public void Revive_AliveMercAndWrongNameAndAwayFromTown_AllRefused()
    {
        var s = TownWithMerc();
        Assert.Throws<SessionRejectedException>(() => s.Revive("strzelczyni")); // not dead — no free heal
        Assert.Throws<SessionRejectedException>(() => s.Revive("witez"));       // no such hireling out

        s.Dismiss();
        s.Hire("strzelczyni");
        s.Merc!.TakeDamage(s.Merc.MaxLife);
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy"); // dead merc, hero away from the captain — §3 R3 says "nearest captain" pays
        long gold = s.Gold;
        Assert.Throws<SessionRejectedException>(() => s.Revive("strzelczyni"));
        Assert.Equal(gold, s.Gold); // rejection spent nothing (the command contract)
    }

    // ================= XP chain — own kill, hero kill, the 1/64 cap ============

    [Fact]
    public void OwnKill_PaysTheFullChain_CappedAtOne64thOfTheLevelSpan()
    {
        var s = Fresh();
        s.LevelTo(6); // the hero out-levels the merc so the level gate is open
        s.Hire("strzelczyni");
        var m = BracketMerc(s); // fighting mercenary (see BracketMerc's stated gap note)
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        var row = s.Monsters.Rows.First(r => r.Key == "pokrzywnik");

        // Spawn the probe EXACTLY as the session's Spawn helper does at mchy, then
        // derive the expectation from the spec formulas written out.
        var area = s.Areas.Get("mchy");
        var probe = s.Monsters.Spawn(row, MonsterTable.ResolveSpawnLevel(row, Difficulty.Ordinary, area));
        long full = SpecChain(s.Ladder, probe.BaseXp, m.Level, probe.Level);
        long cap = SpecPerKillCap(s.Hirelings.For("strzelczyni").ExpPerLevel, m.Level);

        long xpBefore = m.Xp;
        long heroXpBefore = s.Character.Xp;
        GameSession.MercStrikeReport r = default!;
        for (int i = 0; i < 200; i++)
        {
            r = s.MercStrike("pokrzywnik", 1);
            if (r.Kills > 0) break;
        }
        Assert.Equal(1, r.Kills);
        Assert.Equal(Math.Min(full, cap), m.Xp - xpBefore); // exactly one capped full chain
        Assert.Equal(heroXpBefore, s.Character.Xp); // the merc's own kill pays the HERO nothing (D2 pot)
    }

    [Fact]
    public void HeroKill_PaysTheMerc_Floor86_256OfTheCappedFigure_MercNeverLevelsAboveHero()
    {
        var s = Fresh();
        s.LevelTo(5);
        s.Hire("strzelczyni");
        s.Merc!.CombatDistance = 4; // stand back: the hero fights, the merc watches (targeting row pins the closer case)
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        var monRow = s.Monsters.Rows.First(r => r.Key == "pokrzywnik");
        var area = s.Areas.Get("mchy");
        var probe = s.Monsters.Spawn(monRow, MonsterTable.ResolveSpawnLevel(monRow, Difficulty.Ordinary, area));
        long full = SpecChain(s.Ladder, probe.BaseXp, s.Merc.Level, probe.Level);
        long capped = Math.Min(full, SpecPerKillCap(s.Hirelings.For("strzelczyni").ExpPerLevel, s.Merc.Level));

        long xpBefore = s.Merc.Xp;
        for (int i = 0; i < 200; i++)
        {
            var a = s.Attack("pokrzywnik", 1);
            if (a.Kills > 0) break;
        }
        Assert.Equal(SpecShared(capped), s.Merc.Xp - xpBefore); // §6.7 AMENDED: "86/256 of the capped figure"
    }

    [Fact]
    public void XpCap_TheBigChain_BitesAtOne64th_AndSharedFloorsOffTheCappedFigure()
    {
        // The cap biting on a BIG chain (synthetic monster well above any row):
        // a level-74 rzutca (E 120, the Barbarian V100 Exp/Lvl witness).
        var s = Fresh();
        var rzutca = s.Hirelings.For("rzutca");
        var merc = new Hireling(rzutca) { Level = 74, Xp = SpecXpAt(rzutca.ExpPerLevel, 74) };
        var big = new Monster { Name = "synthetic", Level = 70, BaseXp = 5_000_000, DamageMin = 1, DamageMax = 2 };
        long full = SpecChain(s.Ladder, big.BaseXp, 74, big.Level);
        long cap = SpecPerKillCap(rzutca.ExpPerLevel, 74);
        Assert.True(full > cap, "the chain must outgrow the cap — the test is not vacuous");

        Assert.Equal(cap, HirelingXp.OwnKillGain(s.Ladder, merc, big, heroLevel: 90));
        Assert.Equal(SpecShared(cap), HirelingXp.SharedKillGain(s.Ladder, merc, big, heroLevel: 90));
    }

    [Fact]
    public void LevelCap_XpFlood_TheMercStaysAtHeroLevel_AndAtNinetyEight()
    {
        var s = Fresh();
        var strzel = s.Hirelings.For("strzelczyni");
        var big = new Monster { Name = "synthetic", Level = 1, BaseXp = 500_000, DamageMin = 1, DamageMax = 2 };

        var merc = new Hireling(strzel);
        for (int i = 0; i < 10_000; i++)
        {
            long gain = HirelingXp.OwnKillGain(s.Ladder, merc, big, heroLevel: 5);
            if (gain == 0) break;
            merc.Gain(gain, heroLevel: 5);
            Assert.InRange(merc.Level, 1, 5); // "merc level ≤ hero level" — never violated
        }
        Assert.Equal(5, merc.Level);                                  // the flood parks AT the hero, not past
        Assert.Equal(0, HirelingXp.OwnKillGain(s.Ladder, merc, big, 5)); // nothing at your level or above

        var merc98 = new Hireling(strzel) { Level = 97, Xp = SpecXpAt(strzel.ExpPerLevel, 97) }; // authoring door: near-cap, ladder-consistent
        var bigHigh = new Monster { Name = "synthetic", Level = 96, BaseXp = 500_000, DamageMin = 1, DamageMax = 2 };
        for (int i = 0; i < 10_000; i++)
        {
            long gain = HirelingXp.OwnKillGain(s.Ladder, merc98, bigHigh, heroLevel: 99);
            if (gain == 0) break;
            merc98.Gain(gain, heroLevel: 99);
        }
        Assert.Equal(98, merc98.Level); // "cap level 98" (§6.7 AMENDED) outlives any hero
    }

    // ================= regen — the exact quantum to full, dead never ==========

    [Fact]
    public void MercRegen_FramesToFull_AreTheSpecQuantum_TheFrameShortStillDry()
    {
        var s = TownWithMerc();
        var m = s.Merc!;
        m.TakeDamage(15); // a fresh L1 strzelczyni's clamped max 40 (the §6.7 life minimum) minus 15
        Assert.Equal(40, m.MaxLife);
        Assert.Equal(25, m.Life);
        Assert.Equal(10, SpecMercQuantum256(40)); // 40×4×256/4096 — DamageRegen 4 witness

        s.RestTick(SpecMercFrames(15, 40) - 1); // 383 frames: (384−1)·10 = 3830 < 3840 ₂₅₆
        Assert.True(m.Life < m.MaxLife, "one spec-frame short and the pool is ALREADY full");
        s.RestTick(1);
        Assert.Equal(m.MaxLife, m.Life); // the boundary frame lands it EXACTLY
    }

    [Fact]
    public void MercRegen_DeadMerc_RegensNothing()
    {
        var s = TownWithMerc();
        var m = s.Merc!;
        m.TakeDamage(m.MaxLife);
        Assert.True(m.IsDead);
        s.RestTick(30_000);
        Assert.Equal(0, m.Life); // dead merc: no attack no regen (RaiseIfDead/alive gates, §6.7 AMENDED)
    }

    [Fact]
    public void MercRegen_MovesNoManaAndNoHeroLife_HeroRuleStands()
    {
        var s = TownWithMerc();
        s.Character.TakeDamage(10);
        long life = s.Character.Life;
        long mana = s.Character.Mana;
        s.Merc!.TakeDamage(20);
        s.RestTick(1000);
        Assert.Equal(life, s.Character.Life);   // LIFE STILL NEVER regens for the hero (§1.8)
        Assert.Equal(mana, s.Character.Mana);   // pool already full — the mana tick paid its fixpoint
        Assert.True(s.Merc.Life > 20);          // the merc healed while the hero did not
    }

    // ================= targeting — nearest of hero + alive mercs ==============

    [Theory]
    [InlineData(false, 0, 1, false)] // no merc at all: the hero is the target — always (RNG law)
    [InlineData(true, 0, 1, true)]   // merc engaged in front (distance 0) intercepts
    [InlineData(true, 1, 1, false)]  // tie goes to the incumbent hero — determinism, not coin-flip
    [InlineData(true, 4, 1, false)]  // merc standing back: hero stays the nearest
    public void Targeting_NearestValidTargetWins(bool mercExists, int mercDistance, int heroDistance, bool expectMerc)
    {
        var strzel = GameSession.New(1, "koldun", TestData.DataDir()).Hirelings.For("strzelczyni");
        var alive = mercExists ? new Hireling(strzel) : null;
        Assert.Equal(expectMerc, MonsterAi.AttacksHireling(alive, mercDistance, heroDistance));
        if (alive is not null)
        {
            alive.TakeDamage(alive.MaxLife);
            Assert.False(MonsterAi.AttacksHireling(alive, mercDistance, heroDistance)); // dead: not a target
        }
    }

    [Fact]
    public void Targeting_CloserMerc_TakesEveryCounter_TheHeroStaysUntouched()
    {
        var s = TownWithMerc();
        BracketMerc(s);           // a fighting bracket row (stated-gap note at its definition)
        s.Merc!.CombatDistance = 0; // the D2-shape default: the merc engages in front
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        long heroLife = s.Character.Life;
        long mercLife = s.Merc.Life;

        s.Attack("pokrzywnik", 12); // counters run on the row-69 stream the whole volley

        Assert.True(s.Merc.Life < mercLife, "no counter ever reached the merc standing nearest");
        Assert.Equal(heroLife, s.Character.Life); // hero untouched while the alive merc is closest
    }

    [Fact]
    public void Targeting_MercStandingBack_TheHeroKeepsTakingCounters()
    {
        var s = TownWithMerc();
        s.Merc!.CombatDistance = 4;
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        long mercLife = s.Merc.Life;

        s.Attack("ponurak", 40);

        Assert.Equal(mercLife, s.Merc.Life); // nothing bypassed the nearer hero to the standing-back merc
    }

    // ================= hire / dismiss / travel gates ===========================

    [Fact]
    public void Hire_OutsideTown_IsRefused_NoGoldMoves()
    {
        var s = Fresh();
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        long gold = s.Gold;
        var e = Assert.Throws<SessionRejectedException>(() => s.Hire("strzelczyni"));
        Assert.Contains("town", e.Message);
        Assert.Equal(gold, s.Gold);
        Assert.Null(s.Merc);
    }

    [Fact]
    public void Hire_SecondLivingMerc_IsRefused_DeadOneIsReplaceable()
    {
        var s = TownWithMerc();
        var e = Assert.Throws<SessionRejectedException>(() => s.Hire("strzelczyni"));
        Assert.Contains("dismiss", e.Message); // the one-merc gate names the doors out
        Assert.Equal(900, s.Gold);             // the refusal spent nothing

        s.Merc!.TakeDamage(s.Merc.MaxLife);    // a corpse is not a riding mercenary
        s.Hire("strzelczyni");                 // replacing a dead hireling is allowed (§3 R3)
        Assert.Equal(1, s.Merc.Level);         // the replacement is a fresh rookie — forfeited (R3)
        Assert.Equal(800, s.Gold);             // 900 − price again
    }

    [Theory]
    [InlineData("witez", 2)]   // act 2 is NOT-BUILT: gated by the acts, LEDGER row 66
    [InlineData("rzutca", 5)]  // act 5 likewise
    public void Hire_UnbuiltActRows_AreGatedByIdentity(string id, int rowAct)
    {
        var s = Fresh();
        Assert.Equal(rowAct, s.Hirelings.For(id).Act);
        var e = Assert.Throws<SessionRejectedException>(() => s.Hire(id));
        Assert.Contains("act", e.Message);
        Assert.Null(s.Merc);
    }

    [Fact]
    public void Hire_UnknownId_IsRefusedByName()
    {
        var s = Fresh();
        var e = Assert.Throws<SessionRejectedException>(() => s.Hire("nieboze"));
        Assert.Contains("nieboze", e.Message);
    }

    [Fact]
    public void Dismiss_PaysNothingBack_NoRefundTheLoDRule()
    {
        var s = TownWithMerc();
        long gold = s.Gold; // 900 after the 100 hire price
        Assert.Equal("strzelczyni", s.Dismiss());
        Assert.Equal(gold, s.Gold); // "no refund" — the gold the captain took stays taken
        Assert.Null(s.Merc);
        Assert.Throws<SessionRejectedException>(() => s.Dismiss()); // nothing to dismiss twice
    }

    [Fact]
    public void Travel_MercFollows_TheAreaMirrorMovesWithTheHero()
    {
        var s = TownWithMerc();
        Assert.Equal("grodzisko", s.Merc!.Area); // hired where the captain stands
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        Assert.Equal("mchy", s.Merc.Area); // travel = merc follows (the area mirror field)
    }

    [Fact]
    public void MercStrike_DeadMerc_Refuses_AndHireRejectsFromADeadHero()
    {
        var s = TownWithMerc();
        s.Merc!.TakeDamage(s.Merc.MaxLife);
        var e = Assert.Throws<SessionRejectedException>(() => s.MercStrike("pokrzywnik", 1));
        Assert.Contains("dead", e.Message); // dead merc: no attack (§6.7 AMENDED)

        s.Character.TakeDamage(s.Character.Life); // the DEAD gate speaks before everything (row 64)
        Assert.Throws<SessionRejectedException>(() => s.Hire("strzelczyni"));
    }

    // ================= no-merc RNG identity (HARD RNG LAW) ====================
    // Constants captured FROM THE BASE BUILD (852a8c9 CLI, the deploy checkout,
    // 2026-11-01) for the script: new koldun / levelto 5 / attack pokrzywnik 6 /
    // attack ponurak 4 / loot 20 / dump. With NO merc hired, the combat streams
    // (targeting over an empty merc set = hero always) and the RestTick merc-skip
    // must cost NOTHING — every field below byte-identical, or the plant fired.

    private static readonly (ulong Combat, ulong Drop, ulong Enemy, long Gold, long Xp, long Life, long Mana)[]
        BaseCaptured =
    {
        (12766910044730021206, 18169567857193309145, 2471983214592377893, 1000, 7991, 47, 30), // seed 42
        (9181221420660047335, 8530417555526910480, 9150909328627857974, 1004, 8031, 50, 30),   // seed 43
    };

    [Theory]
    [InlineData(42)]
    [InlineData(43)]
    public void NoMerc_CombatSequence_RngWordsAndGolds_AreByteIdenticalToBase(ulong seed)
    {
        var s = GameSession.New(seed, "koldun", TestData.DataDir());
        s.LevelTo(5);
        s.Attack("pokrzywnik", 6);
        s.Attack("ponurak", 4);
        s.Loot(20);

        var words = RngWords(s);
        var captured = BaseCaptured[seed == 42 ? 0 : 1];
        Assert.Equal(captured.Combat, words.Combat);
        Assert.Equal(captured.Drop, words.Drop);
        Assert.Equal(captured.Enemy, words.Enemy);
        Assert.Equal(captured.Gold, s.Gold);
        Assert.Equal(captured.Xp, s.Character.Xp);
        Assert.Equal(captured.Life, s.Character.Life); // the counter volley's exact damage too
        Assert.Equal(captured.Mana, s.Character.Mana);

        // The RestTick merc-skip: no merc → nothing observable (the mana fill
        // beside it is S10b's OWN pinned quantum — the BeltRegenTests rows hold
        // it on this build; no RNG, no gold, no life may move here).
        s.RestTick(1000);
        var after = RngWords(s);
        Assert.Equal(words, after);
        Assert.Equal(captured.Gold, s.Gold);
        Assert.Equal(captured.Life, s.Character.Life);
        Assert.Equal(38, s.Character.Mana); // the §1.8 fill to MaxMana (quantum unchanged, belt suite)
        Assert.Null(s.Merc);
        using var doc = System.Text.Json.JsonDocument.Parse(s.Dump());
        Assert.True(doc.RootElement.GetProperty("merc").ValueKind
            is System.Text.Json.JsonValueKind.Null); // the v5 block says "none" — it is there, empty
    }

    [Fact]
    public void NoMerc_TargetingOverEmptyMercSet_ProducesTheHeroOnlyRolls()
    {
        // The plant (c) shape: targeting that reaches for a merc that does not
        // exist changes WHICH defence the counter rolls against — this pins the
        // exact counter outcome of the scripted base volley by life value alone.
        var s = GameSession.New(42, "koldun", TestData.DataDir());
        s.LevelTo(5);
        s.Attack("pokrzywnik", 6);
        s.Attack("ponurak", 4);
        Assert.Equal(47, s.Character.Life); // base capture: 47, never any other
    }

    // ================= save v5 — the merc block ===============================

    [Fact]
    public void Save_V5_MercBlock_RoundTripsByteIdentical_ExistingPoolsIncluded()
    {
        var s = TownWithMerc();
        s.Merc!.TakeDamage(12);
        s.Merc.CombatDistance = 2;
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        string path = Path.Combine(Path.GetTempPath(), $"hire-{Guid.NewGuid():N}.json");
        try
        {
            s.Save(path);
            var t = GameSession.Load(path, TestData.DataDir());
            Assert.NotNull(t.Merc);
            Assert.Equal(s.Merc.Id, t.Merc!.Id);
            Assert.Equal(s.Merc.Level, t.Merc.Level);
            Assert.Equal(s.Merc.Xp, t.Merc.Xp);
            Assert.Equal(s.Merc.Life, t.Merc.Life);
            Assert.Equal(s.Merc.Area, t.Merc.Area);
            Assert.Equal(s.Merc.CombatDistance, t.Merc.CombatDistance);
            Assert.Equal(s.Gold, t.Gold);
            Assert.Equal(s.Dump(), t.Dump()); // the whole session rides, dump-stable
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Save_V4_IsRefusedByName_LiveSessionUntouched()
    {
        // The SaveTests version idiom, copied: a foreign-version save is refused.
        var s = TownWithMerc();
        string path = Path.Combine(Path.GetTempPath(), $"hire-old-{Guid.NewGuid():N}.json");
        try
        {
            s.Save(path);
            var downgraded = File.ReadAllText(path)
                .Replace($"\"version\": {SaveSerializer.SaveVersion}", "\"version\": 4");
            File.WriteAllText(path, downgraded);
            Assert.Equal(5, SaveSerializer.SaveVersion); // this build speaks v5 (the merc block)
            var e = Assert.Throws<InvalidDataException>(() => GameSession.Load(path, TestData.DataDir()));
            Assert.Contains("save version 4 unsupported", e.Message);
        }
        finally { File.Delete(path); }
    }
}
