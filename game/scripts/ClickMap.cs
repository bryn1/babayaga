using System;
using System.Collections.Generic;

/// <summary>
/// M5-S4 RENDER + INPUT, the CLICK DECISION MAP (MC 11937, plan-c2 S4 row,
/// FIX-DA5 4): the pure table every mouse click rides before any engine call —
/// screen→cell (the ONE iso math, which IsoCamera.ScreenToGrid DELEGATES to,
/// never a fork), the LMB verdict (lit stone marker &gt; walkable ground &gt;
/// refusal — out-of-range clicks refuse, no path is ever planned to them), the
/// RMB verdict (strike only what stands under the cursor), and the RIDE GATE:
/// the door's CanRide half (WaypointGraph) read BEFORE the click so the drawn
/// marker and the allowed destination agree. In-area walking stays what the
/// plan declares: "— presentation A* (NO sim call)"; only the resolved ride
/// target ever reaches a sim verb (the TravelIntent door in HeroController).
/// PURE by design (the S3 idiom): no Godot types, xunit-covered (GameInputTests).
/// </summary>
public static class ClickMap
{
    /// <summary>The LMB click verdicts — in-table priority top to bottom.</summary>
    public enum Left
    {
        Move,   // walkable interior ground: A* plans, no sim call
        Ride,   // the lit standing-stone marker under the cursor: TravelTo the gate's target
        Refuse, // out of grid, or blocked ground: nothing happens, no path, no verb
    }

    /// <summary>World px → the cell containing it — the inverse of the iso
    /// transform (sx=(x−y)·tileW/2, sy=(x+y)·tileH/2), half-to-even rounding
    /// exactly as Mathf.RoundToInt rounded it before this delegation (pinned
    /// by GameInputTests — the existing screen mapping is NOT shifted here).</summary>
    public static (int X, int Y) ScreenToCell(double px, double py, double tileW, double tileH)
    {
        double a = px / (tileW / 2.0), b = py / (tileH / 2.0);
        return (
            (int)Math.Round((a + b) * 0.5, MidpointRounding.ToEven),
            (int)Math.Round((b - a) * 0.5, MidpointRounding.ToEven));
    }

    /// <summary>The LMB decision table: the lit stone marker outranks a walk
    /// plan (P9 ride), walkable ground moves, everything else refuses — an
    /// out-of-range cell refuses even with the stone flag set.</summary>
    public static Left DecideLeft(int cellX, int cellY, int gridW, int gridH,
        bool walkable, bool stoneCellLit)
    {
        bool inside = cellX >= 0 && cellY >= 0 && cellX < gridW && cellY < gridH;
        if (!inside) return Left.Refuse;
        if (stoneCellLit) return Left.Ride;
        return walkable ? Left.Move : Left.Refuse;
    }

    /// <summary>The RMB verdict: the monster key under the cursor, or null — a
    /// click on nothing strikes nothing.</summary>
    public static string? DecideRight(string? monsterKey) => monsterKey;

    /// <summary>The ride gate read ahead of the door (WaypointGraph.CanRide's
    /// lit-endpoint half): the destination is the FIRST linked area that
    /// carries a standing stone AND is lit — dark stones never ride, current
    /// ground is never its own destination, stone-less links stay walked
    /// (ORCH-RULING 2026-11-01). The sim door stays the legality owner: a
    /// null here is the marker-side gate, never a second rule engine.</summary>
    public static string? RideTarget(string current, IReadOnlyList<string> linkedTo,
        Func<string, bool> hasNode, Func<string, bool> isLit)
    {
        foreach (var area in linkedTo)
            if (area != current && hasNode(area) && isLit(area)) return area;
        return null;
    }
}
