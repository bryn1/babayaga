using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BabaYaga.Sim;
using Xunit;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// MC 11977 D5 RUNTIME HALF — the HIRED FOLLOWER rides the screen (MC 11975's
/// imported fol-strzelczyni sheet + portrait get their first runtime consumer).
/// The pure law behind FollowerField/TownPanel — FollowerFieldLogic — exercised
/// WITHOUT Godot (the TownPanelLogic/DeathMenuLogic pure-file/link idiom),
/// RED-FIRST: on the base these rows do not COMPILE (FollowerFieldLogic does
/// not exist yet — the CS0103 shape the TownPanelTests/CraftPanelTests
/// precedent pins as RED). The four law groups: (1) ShouldDraw — the visual
/// exists exactly when the session holds a LIVE hired hireling (pull s.Merc
/// READ-ONLY, no second state, no second truth: none hired / alive hired /
/// dead hired); (2) PickCell — the ORIGINAL DESIGN position fence: THE SIM
/// TRACKS NO FOLLOWER POSITION (Hireling.Area is the travel mirror, not a
/// cell), so the visual sits on a deterministic adjacent cell derived from the
/// hero cell at each rebuild — first free cell in a PINNED ring order, NEVER
/// the hero's own cell, honest null when no neighbour is free (no fake
/// placement, no pathing, no combat — VISUAL ONLY, stated at the logic file);
/// (3) the art-path map — strzelczyni → fol-strzelczyni-idle (art LIVE since
/// MC 11975), every key WITHOUT shipped art (witez, rzutca, unknown) → null =
/// honest absence → palette placeholder; (4) the portrait-path map — same law
/// for the captain row: strzelczyni → poi-strzelczyni-portrait, none other
/// shipped → null = the row stays text-only honestly. The scan rows pin the
/// shipped files to the law (the TownPanelTests/CraftPanelTests scan idiom):
/// the logic file is PURE (no Godot), FollowerField calls NO sim door (visual
/// only is enforceable, not narrated), and the TownPanel portrait rides the
/// ArtOrNull guard with MouseFilter Ignore — the row buttons' STOP law
/// (MC 11959) must not regress behind a picture.
/// </summary>
public class FollowerFieldTests
{
    private static GameSession Fresh(ulong seed = 11) =>
        GameSession.New(seed, "koldun", TestData.DataDir()); // starts in town — the hire door is open

    // ============ 1. ShouldDraw — hired && alive, pulled READ-ONLY ============

    [Fact]
    public void ShouldDraw_NoneHired_False()
    {
        var s = Fresh();
        Assert.Null(s.Merc);                     // the pulled read: one slot, empty
        Assert.False(FollowerFieldLogic.ShouldDraw(s.Merc));
    }

    [Fact]
    public void ShouldDraw_LiveHiredHireling_True()
    {
        var s = Fresh();
        s.Hire("strzelczyni");
        Assert.NotNull(s.Merc);
        Assert.True(FollowerFieldLogic.ShouldDraw(s.Merc));
    }

    [Fact]
    public void ShouldDraw_DeadHireling_False_AndBackOnReviveLife()
    {
        var s = Fresh();
        s.Hire("strzelczyni");
        s.Merc!.Life = 0;                        // the public authoring door (the HireLeg idiom — no kill verb needed here)
        Assert.True(s.Merc.IsDead);
        Assert.False(FollowerFieldLogic.ShouldDraw(s.Merc)); // a corpse stands no sprite (the DEAD law, row 64)
        s.Merc.Life = s.Merc.MaxLife;
        Assert.True(FollowerFieldLogic.ShouldDraw(s.Merc));  // the revive is the ENGINE's door — the law re-reads the pull
    }

    [Fact]
    public void ShouldDraw_Dismissed_False()
    {
        var s = Fresh();
        s.Hire("strzelczyni");
        s.Dismiss();
        Assert.Null(s.Merc);
        Assert.False(FollowerFieldLogic.ShouldDraw(s.Merc));
    }

    [Fact]
    public void ShouldDraw_IsPullOnly_NothingItCanWrite()
    {
        // The law takes the pulled merc and answers a bool — it cannot even be
        // handed the session. The scan row below pins the same law on the shell.
        var s = Fresh();
        s.Hire("strzelczyni");
        var merc = s.Merc!;
        long goldBefore = s.Gold; int lifeBefore = (int)merc.Life;
        _ = FollowerFieldLogic.ShouldDraw(merc);
        Assert.Equal(goldBefore, s.Gold);
        Assert.Equal(lifeBefore, (int)merc.Life);
    }

    // ============ 2. PickCell — the pinned ring, NEVER the hero cell ============

    private static readonly (int X, int Y) Hero = (5, 4);
    private static IReadOnlyCollection<(int X, int Y)> Set(params (int X, int Y)[] cells) => cells;

    [Fact]
    public void PickCell_AllEightFree_FirstInPinnedRingOrder()
    {
        var ring = new[] { (6, 4), (6, 5), (5, 5), (4, 5), (4, 4), (4, 3), (5, 3), (6, 3) };
        var picked = FollowerFieldLogic.PickCell(Hero, Set(ring));
        Assert.NotNull(picked);
        Assert.Equal((6, 4), picked!.Value); // the PINNED head of the order — E first, clockwise (stated ORIGINAL DESIGN)
        // The WHOLE order is pinned — offsets from the hero cell, clockwise
        // from east; any drift in RingOrder goes RED here, not silently.
        Assert.Equal(new[] { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) },
            FollowerFieldLogic.RingOrder.ToArray());
    }

    [Fact]
    public void PickCell_NeverTheHeroCell_EveryRingBlocked_HonestNull()
    {
        // ONLY the hero's own cell is walkable — the honest answer is null
        // (no sprite stands), never the hero's cell (the visual never covers
        // the hero, never fakes a placement).
        var picked = FollowerFieldLogic.PickCell(Hero, Set(Hero));
        Assert.Null(picked);
    }

    [Theory] // one free neighbour per row: the ring's OWN cell wins, wherever it sits in the order
    [InlineData(6, 4)] [InlineData(6, 5)] [InlineData(5, 5)] [InlineData(4, 5)]
    [InlineData(4, 4)] [InlineData(4, 3)] [InlineData(5, 3)] [InlineData(6, 3)]
    public void PickCell_SingleFreeNeighbour_ThatCell(int x, int y) =>
        Assert.Equal((x, y), FollowerFieldLogic.PickCell(Hero, Set((x, y)))!.Value);

    [Fact]
    public void PickCell_OrderIsPinned_FirstFreeNotNearestOfList()
    {
        // (5,3) is listed FIRST yet (6,5) comes earlier in the PINNED order —
        // a List-order leak would answer (5,3); the law answers (6,5).
        var picked = FollowerFieldLogic.PickCell(Hero, Set((5, 3), (6, 5)));
        Assert.Equal((6, 5), picked!.Value);
    }

    [Fact]
    public void PickCell_NoFreeNeighbour_OutsideRingIgnored_HonestNull()
    {
        // Cells BEYOND the 8-ring are nobody's candidate — the fence stands.
        var picked = FollowerFieldLogic.PickCell(Hero, Set((7, 4), (5, 2), (3, 3), Hero));
        Assert.Null(picked);
    }

    [Fact]
    public void PickCell_Deterministic_SameInputSameCell()
    {
        var walkable = Set((6, 5), (4, 4), (5, 3));
        Assert.Equal(FollowerFieldLogic.PickCell(Hero, walkable), FollowerFieldLogic.PickCell(Hero, walkable));
    }

    [Fact]
    public void PickCell_ResultIsAlwaysAdjacent_NeverHero()
    {
        // Sweep: over every walkable subset of the hero's own ring, the answer
        // is EITHER null or a Chebyshev-1 neighbour — never the hero cell.
        var ring = new[] { (6, 4), (6, 5), (5, 5), (4, 5), (4, 4), (4, 3), (5, 3), (6, 3) };
        for (int mask = 0; mask < 256; mask++)
        {
            var cells = new List<(int X, int Y)> { Hero };
            for (int b = 0; b < 8; b++)
                if ((mask >> b & 1) != 0) cells.Add(ring[b]);
            var picked = FollowerFieldLogic.PickCell(Hero, Set(cells.ToArray()));
            if (picked is null) continue;
            Assert.NotEqual(Hero, picked.Value);
            Assert.Equal(1, Math.Max(Math.Abs(picked.Value.X - Hero.X), Math.Abs(picked.Value.Y - Hero.Y)));
        }
    }

    // ============ 3. the art-path map — shipped art vs honest absence ============

    [Fact]
    public void ArtPath_Strzelczyni_SheetLiveSinceMC11975() =>
        Assert.Equal("res://assets/hero/fol-strzelczyni-idle.png", FollowerFieldLogic.ArtPathOrNull("strzelczyni"));

    [Theory] // witez/rzutca ride placeholders HONESTLY until their art lands;
             // an unknown key is the same honest absence — no speculative path.
    [InlineData("witez")]
    [InlineData("rzutca")]
    [InlineData("koldun")]
    [InlineData("")]
    public void ArtPath_NoShippedArt_HonestAbsenceNull(string classKey) =>
        Assert.Null(FollowerFieldLogic.ArtPathOrNull(classKey));

    [Fact]
    public void ArtPath_MapMatchesShippedRawArt()
    {
        // The map is a MIRROR of the shipped truth, not a guess: exactly the
        // fol-* raw renders (git truth, assets/import.sh classifies) have keys.
        var raw = Path.Combine(RepoRoot(), "assets", "raw");
        var folKeys = Directory.EnumerateFiles(raw, "fol-*-idle.png")
            .Select(f => Path.GetFileName(f)[4..^9]).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Contains("strzelczyni", folKeys);
        foreach (var key in folKeys)
            Assert.NotNull(FollowerFieldLogic.ArtPathOrNull(key)); // every shipped sheet answers a path
    }

    // ============ 4. the portrait-path map — same law, captain rows ============

    [Fact]
    public void PortraitPath_Strzelczyni_PortraitLiveSinceMC11975() =>
        Assert.Equal("res://assets/ui/poi-strzelczyni-portrait.png", FollowerFieldLogic.PortraitPathOrNull("strzelczyni"));

    [Theory]
    [InlineData("witez")]
    [InlineData("rzutca")]
    [InlineData("nobody")]
    public void PortraitPath_NoShippedPortrait_HonestAbsenceNull(string classKey) =>
        Assert.Null(FollowerFieldLogic.PortraitPathOrNull(classKey));

    [Fact]
    public void PortraitPath_MapMatchesShippedRawArt()
    {
        var raw = Path.Combine(RepoRoot(), "assets", "raw");
        var poiKeys = Directory.EnumerateFiles(raw, "poi-*-portrait.png")
            .Select(f => Path.GetFileName(f)[4..^13]).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Contains("strzelczyni", poiKeys);
        foreach (var key in poiKeys)
            Assert.NotNull(FollowerFieldLogic.PortraitPathOrNull(key));
    }

    // ==================== the shipped-file scans ====================

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game", "scripts")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static string ReadFile(params string[] parts) =>
        File.ReadAllText(parts.Aggregate(RepoRoot(), Path.Combine));

    [Fact]
    public void FollowerFieldLogic_IsPure_NoGodotTypes()
    {
        var src = ReadFile("game", "scripts", "FollowerFieldLogic.cs");
        Assert.DoesNotContain("using Godot", src);
        Assert.Contains("ORIGINAL DESIGN", src); // the position fence is NAMED in the file, not just here
    }

    [Fact]
    public void FollowerField_CallsNoSimDoor_VisualOnlyIsEnforceable()
    {
        // VISUAL ONLY is the brief's fence — the shell that owns the sprite
        // must not reach ONE sim verb: no hire/dismiss/revive/strike/rest/
        // travel/attack call, and no write to the pulled merc.
        var src = ReadFile("game", "scripts", "FollowerField.cs");
        foreach (var verb in new[] { ".Hire(", ".Dismiss(", ".Revive(", ".MercStrike(", ".RestTick(",
                                     ".TravelTo(", ".Attack(", ".Life =", ".Level =", ".Xp =" })
            Assert.False(src.Contains(verb), $"FollowerField.cs calls/writes {verb} — the VISUAL-ONLY fence is broken");
        Assert.Contains("FollowerFieldLogic.ShouldDraw", src); // the law is CALLED, not re-implemented
    }

    [Fact]
    public void TownPanel_PortraitRidesGuard_AndKeepsTheRowClickLaw()
    {
        var src = ReadFile("game", "scripts", "TownPanel.cs");
        Assert.Contains("FollowerFieldLogic.PortraitPathOrNull", src); // the row rides the map, no literal path
        Assert.Contains("AreaRenderer.ArtOrNull", src);                // the ONE art guard (ArtOrNull law)
        Assert.Contains("TextureRect", src);                           // the picture stands as a TextureRect…
        Assert.DoesNotMatch(@"MouseFilter\s*=", src);                   // …with NO MouseFilter assignment anywhere —
                                                                       // TextureRect's engine default is PASS, the
                                                                       // SAME law the panel's own bodies ride (the
                                                                       // sealed HudMouseFilterTests overlay scan
                                                                       // keeps this file assignment-free), the row
                                                                       // buttons sit TOPMOST at their own centres
                                                                       // and take their clicks, and the picture
                                                                       // therefore cannot regress the STOP law
        Assert.Contains("TownPanelLogic.DismissButton", src);          // the STOP-safe row buttons ride on
        Assert.Contains("TownPanelLogic.ReviveLabel", src);
    }
}
