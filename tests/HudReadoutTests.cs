using System;
using BabaYaga.Sim;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// M5-S5 HUD READ-BACKS (MC 11939, plan-c2 S5, DoD 1 — written RED-first, the
/// file could not compile until the core existed): the exact strings the HUD
/// paints come from Hud.cs's pure HudReadout core (the Godot shell compiles
/// OUT under the test's HUDCORE define) — every value it formats is PULLED
/// from the session; the HUD computes ZERO rules (v9's HUD-recompute
/// rejection stands: MaxLife/MaxMana/XP thresholds remain the Sim's own
/// computed values, the orb fill only scales the SAME pulled current/max the
/// text already prints, and the damage flash is the delta between two pulls
/// of the same literal — a presentation diff, never a re-derivation).
/// The frozen-literal plant row (the honesty idiom of HUD_LEVEL/BELT/
/// HUD_STATE/CAST_STATE/QUEST_STATE, here run WITHOUT Godot): the diff row
/// MUST flag a HUD that keeps its literals while the session moves — the
/// planted frozen twin stays caught, so the gate can go RED.
/// </summary>
public class HudReadoutTests
{
    private static GameSession Fresh(ulong seed = 42) => GameSession.New(seed, "koldun", TestData.DataDir());

    // ---- the S5 §6.7 spec figures, WRITTEN OUT (the HirelingTests idiom) ----

    // StatsAt at the rookie level: life = Max(40, bracket 45 + (1−3)×9) = 40
    // (the engine-literal negative growth below the bracket clamps at 40).
    private const int SpecRookieLife = 40;
    private const string SpecMercName = "Strzelczyni (bow-kin)";
    private const long SpecStartGold = 1000; // GameSession.StartGold witness
    private const long SpecHireGold = 100;   // hirelings.json strzelczyni row

    // ============================ format rows ============================

    [Fact]
    public void Capture_FreshHero_FormatsTheExistingRows()
    {
        var s = Fresh();
        var f = HudReadout.Capture(s);
        Assert.Equal($"Life {s.Character.Life}/{s.Character.MaxLife}", f.Life);
        Assert.Equal($"Mana {s.Character.Mana}/{s.Character.MaxMana}", f.Mana);
        Assert.Equal("Belt [1:- 2:- 3:- 4:-]", f.Belt);
        Assert.Equal("Gold 1000", f.Gold); // the StartGold literal, not a pull-back reformat
        Assert.Equal("Area grodzisko", f.Area);
        Assert.Equal("Target —", f.Target);
        Assert.Equal($"Rites 0/{s.Quests.Quests.Count}", f.Rites); // all Inactive: no suffix
        Assert.Equal("", f.Quest);
        Assert.False(f.Dead);
    }

    [Fact]
    public void Capture_LifeRow_PullsCurrentOverMax_NotTheLiteral()
    {
        var s = Fresh();
        s.Character.Life = s.Character.MaxLife - 20; // the authoring door, the pool's own setter
        var f = HudReadout.Capture(s);
        Assert.Equal($"Life {s.Character.MaxLife - 20}/{s.Character.MaxLife}", f.Life);
    }

    [Fact]
    public void Capture_XpRow_RidesTheLadderThresholds()
    {
        var s = Fresh();
        long lo = s.Ladder.TotalXpForLevel(s.Level);
        long hi = s.Ladder.TotalXpForLevel(s.Level + 1);
        Assert.Equal($"XP {s.Character.Xp - lo}/{hi - lo} (lvl {s.Level})", HudReadout.Capture(s).Xp);
    }

    [Fact]
    public void Capture_MercRow_WithoutAMerc_IsAnHonestDash()
    {
        Assert.Equal("Merc —", HudReadout.Capture(Fresh()).Merc);
    }

    [Fact]
    public void Capture_MercRow_FollowsTheHire_PulledNotComputed()
    {
        var s = Fresh();
        s.Hire("strzelczyni");
        var f = HudReadout.Capture(s);
        Assert.Equal($"Merc {SpecMercName} — Life {SpecRookieLife}/{SpecRookieLife} (lvl 1)", f.Merc);
        Assert.Equal($"Gold {SpecStartGold - SpecHireGold}", f.Gold); // the P8 gold pair, same pull
    }

    [Fact]
    public void Capture_MercRow_DeadMercIsHonestState()
    {
        var s = Fresh();
        s.Hire("strzelczyni");
        s.Merc!.Life = 0; // the authoring door; IsDead reads the pool (row 64's pinned idiom)
        Assert.Equal($"Merc {SpecMercName} — Life 0/{SpecRookieLife} (lvl 1) — dead",
            HudReadout.Capture(s).Merc);
    }

    [Fact]
    public void Capture_TargetRow_PullsTheEngagedMonster()
    {
        var s = Fresh();
        s.Attack("ponurak", 1); // the sim spawns its row; the deep row survives a strike (DeathLeg's
                                // finding) so the engagement stands for the read-back
        var f = HudReadout.Capture(s);
        Assert.Equal($"Target {s.Monsters.Get(s.EngagedKey!).Name}", f.Target);
    }

    // ======================= the orb fill (pull-scaled) =======================

    [Theory]
    [InlineData(100, 100, 64, 64)] // full pool: full column
    [InlineData(0, 100, 64, 0)]    // empty pool: no fill (the DEAD shape)
    [InlineData(50, 100, 64, 32)]  // half pool: half height (integer, floors)
    [InlineData(1, 100, 64, 0)]    // a sliver floors to zero — honest thin
    [InlineData(99, 100, 64, 63)]  // and stays below full while below full
    [InlineData(-5, 100, 64, 0)]   // a negative pull can never carve below the base
    [InlineData(120, 100, 64, 64)] // an over-max pull clamps at the whole orb
    [InlineData(10, 0, 64, 0)]     // max <= 0 has no ratio: honest empty, never NaN
    [InlineData(10, -4, 64, 0)]
    public void OrbFillPx_ScalesThePulledFraction(long cur, long max, int h, int expect) =>
        Assert.Equal(expect, HudReadout.OrbFillPx(cur, max, h));

    [Fact]
    public void OrbFillPx_LowLifeDiffersFromIdle_SameMax()
    {
        // The shot pair's machine twin: full pool paints the full column, a
        // chipped one paints LESS — equal fills across an idle/low-life pair
        // would make the pixel-region proof's RED tell ignorable.
        var s = Fresh();
        long full = HudReadout.OrbFillPx(s.Character.Life, s.Character.MaxLife, 64);
        s.Character.Life = s.Character.MaxLife / 3;
        long low = HudReadout.OrbFillPx(s.Character.Life, s.Character.MaxLife, 64);
        Assert.NotEqual(full, low);
    }

    // ===================== the damage/hint flash row content =====================

    [Theory]
    [InlineData(100, 97, "-3 life")]     // a counter between two paints
    [InlineData(100, 0, "-100 life")]    // the death blow reads its whole delta
    [InlineData(100, 100, "")]           // no move: no flash
    [InlineData(100, 130, "")]           // a heal/raise is NOT damage — no flash
    [InlineData(0, 0, "")]               // dead stays dead: no phantom flash
    public void DamageFlash_ReportsThePullDelta(long lifeShown, long nowLife, string expect) =>
        Assert.Equal(expect, HudReadout.DamageFlash(lifeShown, nowLife));

    // =================== the frozen-literal plant (RED proof) ===================

    /// <summary>The diff row the legs (and the plant test) run: do ANY painted
    /// rows differ between two captures? A HUD whose text could mask a moving
    /// session answers FALSE here — that is the shape the plant proves caught.</summary>
    private static bool AnyRowDiffers(HudReadout.Frame a, HudReadout.Frame b) =>
        a.Life != b.Life || a.Mana != b.Mana || a.Belt != b.Belt || a.Xp != b.Xp
        || a.Gold != b.Gold || a.Area != b.Area || a.Target != b.Target
        || a.Rites != b.Rites || a.Merc != b.Merc || a.Quest != b.Quest;

    [Fact]
    public void HudText_Moves_WhenTheSessionMoves()
    {
        var s = Fresh();
        var before = HudReadout.Capture(s);

        s.Hire("strzelczyni"); // gold row + merc row move
        Assert.True(AnyRowDiffers(before, HudReadout.Capture(s)),
            "a hire moved no HUD row — the read-back is frozen");

        var afterHire = HudReadout.Capture(s);
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy"); // the Area row moves (and the Target row resets with the fight)
        Assert.True(AnyRowDiffers(afterHire, HudReadout.Capture(s)),
            "a travel moved no HUD row — the read-back is frozen");

        var afterTravel = HudReadout.Capture(s);
        s.Character.Life = s.Character.MaxLife - 7; // a counter chip
        Assert.True(AnyRowDiffers(afterTravel, HudReadout.Capture(s)),
            "life damage moved no HUD row — the read-back is frozen");
    }

    [Fact]
    public void FrozenLiteralPlant_TheDiffRowGoesRed()
    {
        // THE PLANT: a HUD frozen at literals (the v9-rejected shape). Two
        // "paints" of the planted HUD keep the rows EQUAL while the session
        // moves — the diff row above MUST answer FALSE for it. If this test
        // ever goes GREEN (the plant differs), the diff row has lost its
        // teeth and every read-back row beside it is unproven.
        var planted = new HudReadout.Frame
        {
            Life = "Life 100/100", Mana = "Mana 80/80", Belt = "Belt [1:- 2:- 3:- 4:-]",
            Xp = "XP 0/500 (lvl 1)", Gold = "Gold 1000", Area = "grodzisko",
            Target = "Target —", Rites = "Rites 0/5", Merc = "Merc —", Quest = "",
        };
        var frozenTwin = new HudReadout.Frame
        {
            Life = "Life 100/100", Mana = "Mana 80/80", Belt = "Belt [1:- 2:- 3:- 4:-]",
            Xp = "XP 0/500 (lvl 1)", Gold = "Gold 1000", Area = "grodzisko",
            Target = "Target —", Rites = "Rites 0/5", Merc = "Merc —", Quest = "",
        };
        Assert.False(AnyRowDiffers(planted, frozenTwin),
            "planted frozen HUD was NOT caught — the diff row cannot go RED");

        // And the same session move the REAL capture catches, the frozen twin
        // sits through unchanged: hire a merc — a live HUD says so, the plant
        // still reads "Merc —".
        var s = Fresh();
        var before = HudReadout.Capture(s);
        s.Hire("strzelczyni");
        var after = HudReadout.Capture(s);
        Assert.True(AnyRowDiffers(before, after));
        Assert.Equal("Merc —", planted.Merc); // the plant's tell: no merc row, ever
    }
}
