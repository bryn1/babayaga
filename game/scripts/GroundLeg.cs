using System.IO;
using Godot;

/// <summary>
/// MC 12135 GROUND-PAINTS LEG — the gate the grey world proved missing (the
/// TEST-verdict-ownerfeel-m5 finding: every M5 leg screenshot had grey ground
/// and passed, because NO leg ever asserted the floor paints; the SetCell
/// atlas-coords store bug ate it silently). One env var, two surfaces:
/// HEADLESS (Boot's RunMarkerLeg gate): assert the CURRENT area's TileMap
/// stores &gt; 0 cells — the store bug's exact signature is used_cells 0 — and
/// print BABA_GROUND_LEG OK &lt;n&gt;cells / FAIL, the exit code owned by Boot.
/// WINDOWED (Xvfb evidence, the FollowerLeg gate shape): the REAL Act settles,
/// the root viewport is SHOT (the leg-idiom screenshot mechanism), the frame
/// is sampled for non-background pixels exactly like the pinned probe (bg =
/// 77/255 ± 0.01), and BOTH halves gate the exit. NO sim verb is reachable
/// from any line here — it reads the painted TileMap and the frame, nothing else.
/// MC 12140 adds the third gate: AssertAlignment — the painted floor must sit
/// under the entity lattice (hero cell + centre + corner diagonals), and the
/// OK/FAIL markers carry the ALIGNED verdict the immutable parent probe greps.
/// MC 12144 (camera zoom) COEXISTENCE: the sampler is zoom-aware — world→screen
/// px divides by the live camera zoom and the dx offsets ride the ON-SCREEN
/// diamond as fractions of TileW/zoom (at zoom 1 the values are the pinned
/// {−48,−32,0,+32,+48} verbatim) — and a sample point an adjacent BLOCKED
/// cell's tile art can cover (its grey slab / stake-gap band, the 12135 art
/// design) is NOT floor-asserted (see FenceCovers). The gate stays non-vacuous:
/// it FAILs if no point anywhere got a clean read.
/// </summary>
public partial class GroundLeg : Node
{
    public const string EnvVar = "BABA_GROUND_LEG";
    public const ulong LegSeed = 42; // the anchor seed — town start, the owner's first frame

    public string OutDir = "";
    public Act Act = null!;

    private int _frames;

    /// <summary>HEADLESS gate (Boot calls it right after the Act smoke
    /// instantiate — the FIRST paint, the owner's start frame): PINNED format
    /// BABA_GROUND_LEG OK &lt;n&gt;cells on a painted floor, BABA_GROUND_LEG FAIL
    /// ... on the grey world. Returns the gate for Boot's Quit code.</summary>
    public static bool AssertHeadless(Act act)
    {
        int cells = act.Tiles.UsedCellCount;
        if (cells <= 0)
        {
            GD.Print("BABA_GROUND_LEG FAIL 0cells — the tile layer stores nothing (grey world)");
            return false;
        }
        GD.Print($"BABA_GROUND_LEG OK {cells}cells");
        return true;
    }

    public override void _Ready()
    {
        GetWindow().Position = Vector2I.Zero;
        GetWindow().Size = new Vector2I(1280, 720); // the judged leg size (the S6 shape)
    }

    public override void _Process(double delta)
    {
        if (++_frames < 30) return; // settled presents only (the S4 present-lag law)
        int cells = Act.Tiles.UsedCellCount;
        var frame = GetViewport().GetTexture().GetImage();
        int nonbg = 0;
        for (int y = 100; y < frame.GetHeight() - 20; y += 25)
            for (int x = 2; x < frame.GetWidth() - 2; x += 25)
                if (Mathf.Abs(frame.GetPixel(x, y).R - 77f / 255f) > 0.01f) nonbg++;
        bool aligned = AssertAlignment(frame);
        var path = Path.Combine(OutDir, "01-painted.png");
        frame.SavePng(path);
        GD.Print($"BABA_GROUND_LEG SHOT {path} {frame.GetWidth()}x{frame.GetHeight()}");
        GD.Print($"BABA_GROUND_LEG_PAINT nonbg_samples={nonbg}");
        if (cells <= 0 || nonbg <= 0 || !aligned)
        {
            GD.Print($"BABA_GROUND_LEG FAIL cells={cells} nonbg_samples={nonbg} " +
                     $"ALIGNED=no — floor not under the entity lattice");
            GetTree().Quit(1);
            return;
        }
        GD.Print($"BABA_GROUND_LEG OK {cells}cells ALIGNED");
        GetTree().Quit(0);
    }

    /// <summary>MC 12140 ALIGNMENT ASSERTION — the FLOOR COMES TO THE ENTITIES
    /// gate. For the hero's own cell, the grid centre and three Chebyshev-4
    /// corner diagonals, the PAINTED FLOOR must sit under the entity lattice:
    /// the row through IsoCamera.GridToScreen(cell) (converted to screen px
    /// under the viewport's current camera AND its live zoom — MC 12144) is
    /// sampled at fractions {−3/8,−1/4,0,+1/4,+3/8} of the on-screen diamond
    /// (zoom 1 ⇒ the pinned −48…+48 verbatim); ≥ 4 of the EVALUATED points
    /// non-background means the diamond of THAT cell is painted there (one
    /// miss tolerated; a mis-based lattice leaves the whole row background
    /// except the 1-px seam vertices — the one-tile-shift RED run proves it
    /// fails). Only WALKABLE candidates are floor-asserted, and a point a
    /// neighbouring blocked tile can cover is skipped, not counted (FenceCovers;
    /// the skip is why the clean-evaluated total below must stay &gt; 0).
    /// The bg law is the probe's 77/255 grey at the leg's own 0.01 threshold,
    /// checked on EVERY channel (see the sample loop), and the mapping reuses
    /// the SEALED GridToScreen.</summary>
    private bool AssertAlignment(Image frame)
    {
        var cam = GetViewport().GetCamera2D();
        if (cam is null)
        {
            GD.Print("BABA_GROUND_ALIGN FAIL — no current camera to map world to screen");
            return false;
        }
        bool okAll = true;
        int evaluated = 0; // MC 12144: never let the fence-cover skips silence the gate
        foreach (var c in new[] { Act.Tiles.CenterCell(), new Vector2I(8, 8),
                                  new Vector2I(4, 4), new Vector2I(4, 12), new Vector2I(12, 4) })
        {
            if (!Act.Tiles.IsWalkable(c))
            {
                GD.Print($"BABA_GROUND_ALIGN cell={c} skip=fence-art-cell");
                continue;
            }
            var g = IsoCamera.GridToScreen(c);
            float z = cam.Zoom.X;
            var sp = new Vector2I(frame.GetWidth() / 2 + (int)((g.X - cam.Position.X) / z),
                                  frame.GetHeight() / 2 + (int)((g.Y - cam.Position.Y) / z));
            int hit = 0, eval = 0;
            foreach (float f in new[] { -3f / 8, -1f / 4, 0f, 1f / 4, 3f / 8 })
            {
                int dx = (int)System.MathF.Round(f * IsoCamera.TileW / z); // fraction of the diamond
                if (sp.X + dx < 0 || sp.X + dx >= frame.GetWidth() ||
                    sp.Y < 0 || sp.Y >= frame.GetHeight()) continue; // off-frame point: no hit
                // MC 12144: a point an adjacent fence tile's art can cover carries
                // no honest floor read — skip it, count neither way (FenceCovers).
                if (FenceCovers(c, cam.Position, z, new Vector2I(sp.X + dx, sp.Y))) continue;
                eval++;
                var p = frame.GetPixel(sp.X + dx, sp.Y);
                // the pinned bg law at the leg's own 0.01 threshold, per CHANNEL
                // (the stone floor's centre pixel sits ~0.02 off bg in R but is
                // clean in G/B — an R-only test reads real floor as bg, measured
                // 2026-11-05; a flat bg pixel is EXACTLY 77/255 on all three):
                if (Mathf.Abs(p.R - 77f / 255f) > 0.01f ||
                    Mathf.Abs(p.G - 77f / 255f) > 0.01f ||
                    Mathf.Abs(p.B - 77f / 255f) > 0.01f) hit++;
            }
            if (eval == 0)
            {
                GD.Print($"BABA_GROUND_ALIGN cell={c} skip=all-points-under-fence-art");
                continue;
            }
            evaluated += eval;
            int need = System.Math.Max(1, eval - 1); // pinned ≥4 of 5 (and eval−1 tolerance on shorter rows); a lone point must hit
            bool ok = hit >= need;
            okAll &= ok;
            GD.Print($"BABA_GROUND_ALIGN cell={c} painted={hit}/{eval} ok={(ok ? "yes" : "no")}" +
                     $" sp=({sp.X},{sp.Y}) cam={cam.Position} zoom={z}"); // MC 12144 diagnostic
        }
        if (evaluated == 0)
        {
            GD.Print("BABA_GROUND_ALIGN FAIL — no clean sample point anywhere, the gate would be vacuous");
            return false;
        }
        return okAll;
    }

    /// <summary>Window px of a cell's lattice point under the live camera
    /// (the same zoom-divided mapping the sampler uses).</summary>
    private Vector2I ScreenOf(Vector2I cell, Vector2 camPos, float z)
    {
        var v = GetViewport().GetVisibleRect().Size;
        var g = IsoCamera.GridToScreen(cell);
        return new Vector2I((int)(v.X / 2 + (g.X - camPos.X) / z), (int)(v.Y / 2 + (g.Y - camPos.Y) / z));
    }

    /// <summary>MC 12144: can any BLOCKED cell's tile art cover this sample
    /// point? The palisade tile paints a grey slab band plus transparent stake
    /// gaps around its lattice point (the 12135 art design — seen own-eyed in
    /// the leg's own 01-painted.png: cell (4,4) at zoom 1.25 sat painted 1/5
    /// while the floor under it stayed STORED and ALIGNED, anchors elsewhere
    /// read 5/5, CamLeg follow_eps=0.000). The conservative box is the tile's
    /// full art reach — its diamond rows plus one TileH of stake height above
    /// centre, one half-diamond below. The 12140 RED survives UNCHANGED: with
    /// the lattice shifted a tile, NO tile covers the candidates' points, so
    /// eval stays 5 and hit < 4 fails exactly as that run proved.</summary>
    private bool FenceCovers(Vector2I c, Vector2 camPos, float z, Vector2I sp)
    {
        for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var n = new Vector2I(c.X + dx, c.Y + dy);
                if (Act.Tiles.IsWalkable(n)) continue; // only blocked tiles paint fence art
                var s = ScreenOf(n, camPos, z);
                int offX = sp.X - s.X, offY = sp.Y - s.Y;
                // art box: ±half-tile in x; stake reach up to one TileH above,
                // half a TileH below centre — 2 px slack for the scale seam.
                if (Mathf.Abs(offX) <= IsoCamera.TileW / (2 * z) + 2 &&
                    offY >= -(int)(IsoCamera.TileH / z) - 2 &&
                    offY <= (int)(IsoCamera.TileH / (2 * z)) + 2)
                    return true;
            }
        return false;
    }
}
