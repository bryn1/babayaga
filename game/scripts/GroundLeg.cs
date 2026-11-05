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
    /// under the viewport's current camera — the same camera the owner sees)
    /// is sampled at dx ∈ {−48,−32,0,+32,+48}; ≥ 4 of 5 non-background pixels
    /// means the diamond of THAT cell is painted there (one miss tolerated: a
    /// sprite foot or a seam pixel may cover one point; a mis-based lattice
    /// leaves the whole row background except the 1-px seam vertices — the
    /// one-tile-shift RED run proves it fails). Only WALKABLE candidates are
    /// floor-asserted: the blocked cell's fence art is transparent off its
    /// centre by the MC 12135 art design (measured: mid-row alpha 0 at dx
    /// ±32/±48), so it carries no floor to align. The bg law is the probe's
    /// 77/255 grey at the leg's own 0.01 threshold, checked on EVERY channel
    /// (see the sample loop), and the mapping reuses the SEALED GridToScreen.</summary>
    private bool AssertAlignment(Image frame)
    {
        var cam = GetViewport().GetCamera2D();
        if (cam is null)
        {
            GD.Print("BABA_GROUND_ALIGN FAIL — no current camera to map world to screen");
            return false;
        }
        bool okAll = true;
        foreach (var c in new[] { Act.Tiles.CenterCell(), new Vector2I(8, 8),
                                  new Vector2I(4, 4), new Vector2I(4, 12), new Vector2I(12, 4) })
        {
            if (!Act.Tiles.IsWalkable(c))
            {
                GD.Print($"BABA_GROUND_ALIGN cell={c} skip=fence-art-cell");
                continue;
            }
            var g = IsoCamera.GridToScreen(c);
            var sp = new Vector2I(frame.GetWidth() / 2 + (int)(g.X - cam.Position.X),
                                  frame.GetHeight() / 2 + (int)(g.Y - cam.Position.Y));
            int hit = 0;
            foreach (int dx in new[] { -48, -32, 0, 32, 48 })
            {
                if (sp.X + dx < 0 || sp.X + dx >= frame.GetWidth() ||
                    sp.Y < 0 || sp.Y >= frame.GetHeight()) continue; // off-frame point: no hit
                var p = frame.GetPixel(sp.X + dx, sp.Y);
                // the pinned bg law at the leg's own 0.01 threshold, per CHANNEL
                // (the stone floor's centre pixel sits ~0.02 off bg in R but is
                // clean in G/B — an R-only test reads real floor as bg, measured
                // 2026-11-05; a flat bg pixel is EXACTLY 77/255 on all three):
                if (Mathf.Abs(p.R - 77f / 255f) > 0.01f ||
                    Mathf.Abs(p.G - 77f / 255f) > 0.01f ||
                    Mathf.Abs(p.B - 77f / 255f) > 0.01f) hit++;
            }
            bool ok = hit >= 4;
            okAll &= ok;
            GD.Print($"BABA_GROUND_ALIGN cell={c} painted={hit}/5 ok={(ok ? "yes" : "no")}");
        }
        return okAll;
    }
}
