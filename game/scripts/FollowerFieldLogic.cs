using System.Collections.Generic;
using BabaYaga.Sim;

/// <summary>
/// HIRED FOLLOWER, the position + presence law (MC 11977 D5 runtime half) —
/// the mapping core beside FollowerField.tscn's shell (the TownPanelLogic
/// precedent: PURE, no Godot types, xunit-covered tests/FollowerFieldTests.cs).
/// THE POSITION LAW IS ORIGINAL DESIGN, FENCED HERE: THE SIM TRACKS NO
/// FOLLOWER POSITION — Hireling.Area is the travel mirror (Hireling.cs, its
/// own ORIGINAL-DESIGN field), not a cell, and no door takes a mercenary
/// coordinate. So the visual sits on a deterministic adjacent cell DERIVED
/// from the hero cell at each rebuild: <see cref="PickCell"/> takes the FIRST
/// FREE cell in a PINNED ring order (clockwise from east, the order below —
/// stated choice, pinned by test), NEVER the hero's own cell, and answers
/// null HONESTLY when no neighbour is free (the shell then paints nothing —
/// no fake placement, no overlap of the hero). NO pathing, NO combat, NO
/// follow logic: VISUAL ONLY (the shell cannot call a sim door — the test
/// scan row pins it). ShouldDraw pulls the session's own hire read
/// (GameSession.Merc + Hireling.IsDead, READ-ONLY — no second truth is kept).
/// The art/portrait maps mirror the SHIPPED art truth (MC 11975 import):
/// strzelczyni has fol-strzelczyni-idle + poi-strzelczyni-portrait; witez and
/// rzutca have NEITHER and ride the palette placeholder / text-only row
/// HONESTLY until their art lands (a shipped-sheet test row keeps map and
/// assets/raw in lockstep, so this map cannot silently lead or lag the art).
/// </summary>
public static class FollowerFieldLogic
{
    /// <summary>Should the follower sprite stand at all — exactly a LIVE
    /// hired hireling (the DEAD law, row 64: a corpse stands no sprite;
    /// dismissed/never-hired: s.Merc is null). The pulled merc answers; this
    /// law cannot write it (it never sees the session).</summary>
    public static bool ShouldDraw(Hireling? merc) => merc is { IsDead: false };

    /// The PINNED ring around the hero cell — Chebyshev neighbours, clockwise
    /// from east in grid space (x = east, y = south). The order is the law:
    /// every rebuild answers the SAME cell for the same walkable set (the
    /// deterministic adjacency the brief fences; test-pinned whole).
    public static readonly (int X, int Y)[] RingOrder =
    [
        (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1),
    ];

    /// <summary>The first ring cell whose step from the hero lands in the
    /// walkable set, in the PINNED order; null when NO neighbour is free.
    /// The hero's own cell is structurally unreachable (the ring excludes it)
    /// and cells beyond the 8-ring are never candidates — the visual never
    /// paths, never lags, never covers the hero.</summary>
    public static (int X, int Y)? PickCell((int X, int Y) heroCell, IReadOnlyCollection<(int X, int Y)> walkable)
    {
        foreach (var (dx, dy) in RingOrder)
        {
            var cell = (heroCell.X + dx, heroCell.Y + dy);
            if (Contains(walkable, cell)) return cell;
        }
        return null; // the honest answer: nowhere to stand — the shell paints nothing
    }

    private static bool Contains(IReadOnlyCollection<(int X, int Y)> cells, (int X, int Y) cell)
    {
        // IReadOnlyCollection (not IReadOnlySet) so the caller may hand ANY
        // shape — the Godot shell hands the area's walkable LIST; a ring is
        // ≤ 8 candidates, the scan is the whole cost.
        foreach (var c in cells)
            if (c == cell) return true;
        return false;
    }

    /// <summary>The follower sheet for one hireling class key, or null — and
    /// null is the HONEST absence: no shipped fol-* sheet means the shell's
    /// palette placeholder stands (the MonsterField ArtOrNull idiom). The map
    /// lists exactly the SHIPPED keys (assets/raw/fol-*-idle.png as imported
    /// by MC 11975); witez/rzutca join when their art does.</summary>
    public static string? ArtPathOrNull(string classKey) => classKey switch
    {
        "strzelczyni" => "res://assets/hero/fol-strzelczyni-idle.png",
        _ => null, // honest absence — placeholder pass, never a speculative path
    };

    /// <summary>The captain-row portrait for one class key, or null — same
    /// mirror law: only strzelczyni shipped a portrait (poi-strzelczyni-
    /// portrait, the 128×128 imported twin); a class with no portrait stays
    /// a TEXT-ONLY row honestly.</summary>
    public static string? PortraitPathOrNull(string classKey) => classKey switch
    {
        "strzelczyni" => "res://assets/ui/poi-strzelczyni-portrait.png",
        _ => null, // honest absence — the row stays text-only
    };
}
