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
        var path = Path.Combine(OutDir, "01-painted.png");
        frame.SavePng(path);
        GD.Print($"BABA_GROUND_LEG SHOT {path} {frame.GetWidth()}x{frame.GetHeight()}");
        GD.Print($"BABA_GROUND_LEG_PAINT nonbg_samples={nonbg}");
        if (cells <= 0 || nonbg <= 0)
        {
            GD.Print($"BABA_GROUND_LEG FAIL cells={cells} nonbg_samples={nonbg} — nothing painted");
            GetTree().Quit(1);
            return;
        }
        GD.Print($"BABA_GROUND_LEG OK {cells}cells");
        GetTree().Quit(0);
    }
}
