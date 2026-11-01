namespace BabaYaga.Sim.Tests;

/// <summary>
/// M4-S10b MANA REGEN — the amended spec §1.8 lives (LEDGER row 79, card/11874;
/// amendment MC 11873, sources out/T0b-sec1-8-amendment.md). Contract under test:
/// public GameSession.RestTick(int frames) — the explicit headless rest driver
/// (ORIGINAL DESIGN, not D2-derived; the 25-frames-per-second seconds convention is
/// ORIGINAL DESIGN too). Per frame, per amended §1.8 rows M1–M3 + R1–R2: the quantum
/// is `max(1, floor(maxMana in 256ths ÷ (25 × ManaRegen)))` with charstats
/// ManaRegen = 120 for every class, times (100 + %Regenerate Mana) ÷ 100 floored
/// AGAIN (second floor; no item grants the bonus today, so ×100/100), ADDED to the
/// pool kept in 256ths with EXACT accumulation (R2: "two floors on the per-frame
/// quantum, none on the accumulation"), clamped to the missing pool (M3). LIFE NEVER
/// MOVES (L1: "Your life does not regenerate on its own" — the potion economy IS the
/// design; Replenish-Life is a future own row). DEAD never regens (RaiseIfDead FIRST,
/// row-64 gate). NO difficulty term (D2: the old §1.8 phrase "difficulty penalties"
/// was leech's divisor — RETIRED, CONTRADICTED). rng streams untouched. The expected
/// frames-to-full is computed here from the SPEC FORMULA WRITTEN OUT, never read
/// back from the implementation. Koldun level-1 anchors: MaxMana 30 (BeltPotionTests).
/// </summary>
public class BeltRegenTests
{
    private static GameSession Fresh(ulong seed = 9) => GameSession.New(seed, "koldun", TestData.DataDir());

    // ---- the spec formula, written OUT (§1.8 AMENDED, M1–M3: quantum in 256ths,
    // min 1; %RegenMana 0 today ⇒ the second floor is an identity) ----
    private static long SpecQuantum256(long maxMana) => Math.Max(1, maxMana * 256 / (25 * 120));
    private static long SpecManaAfterFrames(long maxMana, int frames) =>
        Math.Min(maxMana, frames * SpecQuantum256(maxMana) / 256);
    private static int SpecFramesToFull(long maxMana) =>
        (int)Math.Ceiling(maxMana * 256.0 / SpecQuantum256(maxMana));

    private static GameSession DeadInMchy(ulong seed = 9)
    {
        var s = Fresh();
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        s.Character.TakeDamage(s.Character.Life);
        return s;
    }

    /// <summary>The three rng stream state words as the v2+ save/dump carries them
    /// (camelCase dump keys) — the rest tick must leave all of them alone.</summary>
    private static (ulong Combat, ulong Drop, ulong Enemy) RngWords(GameSession s)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(s.Dump());
        var r = doc.RootElement;
        return (r.GetProperty("combatRngState").GetUInt64(),
                r.GetProperty("dropRngState").GetUInt64(),
                r.GetProperty("enemyRngState").GetUInt64());
    }

    // ---- quantum exactness: frames-to-full for the koldun's small pool, per the
    // spec formula written out above (§1.8 M1/M2 + M6's worked shape: small pools
    // floor-noticeably slower because the quantum floor bites) ----

    [Fact]
    public void Rest_FromEmpty_PoolFullOnExactlyTheSpecFrame_AOneFrameShortStillDry()
    {
        var s = Fresh();
        long max = s.Character.MaxMana;
        Assert.True(max > 0, "the koldun owes a real pool — the test is not vacuous");
        int exact = SpecFramesToFull(max);
        s.Character.Mana = 0;

        s.RestTick(exact - 1); // §1.8 arithmetic: (exact−1)·q/256 < max, strictly
        Assert.True(s.Character.Mana < max, "one spec-frame short and the pool is ALREADY full — quantum too big");

        s.RestTick(1);
        Assert.Equal(max, s.Character.Mana); // the boundary frame lands it EXACTLY at max
    }

    [Fact]
    public void Rest_PartialCounts_MatchTheSpecArithmetic_AtTwoFrameCounts()
    {
        var s = Fresh();
        long max = s.Character.MaxMana;
        s.Character.Mana = 0;

        s.RestTick(1000);
        Assert.Equal(SpecManaAfterFrames(max, 1000), s.Character.Mana);
        s.RestTick(500);
        Assert.Equal(SpecManaAfterFrames(max, 1500), s.Character.Mana); // back-to-back equals one run of 1500
    }

    // ---- min-1 quantum at a tiny max (§1.8 M1 "at least 1": a 1-point pool still
    // regenerates, at 1/256 per frame — the Basin's 20-mana ≈ 205 s shape, sharpened) ----

    [Fact]
    public void Rest_TinyPool_QuantumFloorsAtOne_Minus1of256_StillDry()
    {
        var s = Fresh();
        s.Character.FlatMana -= s.Character.MaxMana - 1; // MaxMana lands at 1
        Assert.Equal(1, s.Character.MaxMana);
        s.Character.Mana = 0;

        s.RestTick(255); // 255 frames × quantum 1/256 stays below one whole point
        Assert.Equal(0, s.Character.Mana);
        s.RestTick(1);   // the 256th frame completes the point
        Assert.Equal(1, s.Character.Mana);
    }

    // ---- clamp: the pool lands AT max and never above, however long the rest ----

    [Fact]
    public void Rest_PastFull_ClampsExactlyAtMax_NeverAbove()
    {
        var s = Fresh();
        s.Character.Mana = 0;
        s.RestTick(300_000);
        Assert.Equal(s.Character.MaxMana, s.Character.Mana); // M3: clamped to the missing pool
        s.RestTick(300_000);
        Assert.Equal(s.Character.MaxMana, s.Character.Mana); // resting while full changes nothing
    }

    // ---- the accumulator is exact across calls (R2: "none on the accumulation") —
    // a per-call reset is the planted-red this exactness pins ----

    [Fact]
    public void Rest_SplitCalls_TheAccumulatorCarriesTheSubPointAcross_ExactArithmetic()
    {
        var s = Fresh();
        long max = s.Character.MaxMana;
        s.Character.Mana = 0;
        long q = SpecQuantum256(max); // 2 at max 30: 255 frames leave 510 256ths = 1 pt + 254/256

        s.RestTick(255);
        Assert.Equal(1, s.Character.Mana); // the whole point paid; the 254/256 rides on
        s.RestTick(1);                      // +2 → 512 256ths: the second point lands
        Assert.Equal(2, s.Character.Mana);  // per-call-reset would read 1 here (the plant's shape)
        Assert.Equal(SpecManaAfterFrames(max, 256), s.Character.Mana); // equals one call of 256 frames
    }

    // ---- LIFE NEVER MOVES: the D2 rule (L1/L2, amended §1.8) — no passive life
    // regen exists at all, so no window, no trickle, nothing. The plant "passive
    // life regen" fires exactly on this assertion. ----

    [Fact]
    public void Rest_AliveDamaged_LifeNeverMoves_ManaHeals()
    {
        var s = Fresh();
        s.Character.TakeDamage(10); // damaged, alive — the very state a regen window would serve
        s.Character.Mana = 0;
        long life = s.Character.Life;

        s.RestTick(300_000); // a long rest heals the mana pool…

        Assert.Equal(s.Character.MaxMana, s.Character.Mana); // …proving the tick ran…
        Assert.Equal(life, s.Character.Life);                // …and the life pool stands still:
        Assert.True(life < s.Character.MaxLife);             //   "Your life does not regenerate on its own."
    }

    [Fact]
    public void Rest_FreshFullCharacter_Alive_LifeStaysExactlyAtMax()
    {
        var s = Fresh();
        long life = s.Character.Life;
        s.RestTick(1000);
        Assert.Equal(life, s.Character.Life); // life rides max exactly — no overshoot either
        Assert.Equal(s.Character.MaxLife, s.Character.Life);
    }

    // ---- DEAD never regens: RaiseIfDead FIRST (row-64 gate idiom — the corpse
    // hears the death, never a per-verb precondition), and NOTHING moves ----

    [Fact]
    public void Rest_WhileDead_ThrowsYouAreDead_AllPoolsUnmoved()
    {
        var s = DeadInMchy();
        s.Character.Mana = 0; // an empty pool is exactly what regen would want
        long mana = s.Character.Mana, xp = s.Character.Xp, gold = s.Gold;

        var e = Assert.Throws<SessionRejectedException>(() => s.RestTick(300_000));
        Assert.Equal("you are dead", e.Message); // the gate speaks FIRST, like every gated verb

        Assert.Equal(mana, s.Character.Mana); // 300 000 frames of rest paid a corpse nothing…
        Assert.Equal(0, s.Character.Life);    // …life included — still a corpse…
        Assert.Equal(xp, s.Character.Xp);     // …and the grave gives no XP or gold
        Assert.Equal(gold, s.Gold);
    }

    // ---- the rest rolls nothing: all three seeded streams byte-identical across
    // RestTick (and mid-stream, after a real weave burned a combat roll) ----

    [Fact]
    public void Rest_RollsNothing_TheThreeRngStreamsSurviveTheRoad()
    {
        var s = Fresh(3);
        s.Character.GrantSkillPoints(1);
        s.SpendSkill("iskra");
        s.Cast("iskra", "pokrzywnik"); // the streams sit mid-sequence now, not at their starts

        var before = RngWords(s);
        s.RestTick(3000);
        var after = RngWords(s);

        Assert.Equal(before, after); // combat, drop AND enemy words unchanged — rest is seed-free
    }

    // ---- invalid transitions reject, the command contract (nothing half-applied) ----

    [Fact]
    public void Rest_NegativeFrames_RejectsNamed_PoolUnmoved()
    {
        var s = Fresh();
        s.Character.Mana = 0;
        var e = Assert.Throws<SessionRejectedException>(() => s.RestTick(-1));
        Assert.Contains("at least 0", e.Message);
        Assert.Equal(0, s.Character.Mana);
    }

    [Fact]
    public void Rest_ZeroFrames_IsAnOrderedNoOp()
    {
        var s = Fresh();
        s.Character.Mana = 0;
        s.RestTick(0);
        Assert.Equal(0, s.Character.Mana);
    }

    // ---- level-up moves the pool through the formula, and the quantum follows the
    // max (M5: "Energy raises regen only via max mana" — the quantum is a share of
    // the CURRENT pool, recomputed, never cached from the old max) ----

    [Fact]
    public void Rest_AfterLevelUp_TheQuantumTracksTheBiggerPool_M5Shape()
    {
        var s = Fresh();
        s.LevelTo(6);                        // more levels → bigger MaxMana (row 3–5 formulas)
        long max = s.Character.MaxMana;
        Assert.True(max > 30, "level 6 must outgrow the level-1 anchor — the test is not vacuous");
        s.Character.Mana = 0;

        s.RestTick(1500);                    // 1500 frames on the spec quantum of the NEW max
        Assert.Equal(SpecManaAfterFrames(max, 1500), s.Character.Mana);
    }
}
