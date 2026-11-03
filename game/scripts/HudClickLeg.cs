using BabaYaga.Sim;
using Godot;

/// <summary>
/// MC 11959 HUD CLICK-THROUGH LEG (DoD 2): a PASSIVE state reader the windowed
/// evidence runner boots (env-gated BABA_HUD_LEG, windowed-only — the Boot
/// gate shape every S4/S6/MENU leg rides). The clicks themselves are NOT here:
/// the committed xdotool driver (out/leg-m5-hud/) lands REAL X clicks on real
/// screen px while this node only READS and PRINTS — hero cell, walking flag,
/// area, gold and the town-panel visibility — one M5_HUD_STATE line per half
/// second. The before/after pair the driver states: the same click on a world
/// cell under the HUD rect, BEFORE the fix leaves every read unchanged
/// (swallowed); AFTER, the reads move (hero walk, area travel). It also probes
/// ONCE at boot: the HUD PanelContainer's measured rect (the ~283x346 swallow
/// box MC 11958 measured) and every candidate cell's live walkability + px —
/// the driver picks a covered walkable cell FROM THESE READS, never a guess.
/// Zero session verbs, zero state writes (the driver-leg scan law, pinned by
/// tests/HudMouseFilterTests.cs); GameContext.Session is read read-only.
/// </summary>
public partial class HudClickLeg : Node
{
    public const string EnvVar = "BABA_HUD_LEG";
    public const ulong LegSeed = 6; // printed as M5_HUD_SEED

    public Act Act = null!;

    private int _frames;
    private double _lastState = -9;
    private string _buyPx = ""; // one BUY-button read per open (the panel-click row)

    private static GameSession? S => GameContext.Session;

    public override void _Ready()
    {
        GetWindow().Position = Vector2I.Zero;
        GetWindow().Size = new Vector2I(1280, 720); // the judged leg size
    }

    public override void _Process(double delta)
    {
        _frames++;
        if (_frames < 12) return; // reads answer EMPTY before the first presents (the leg law)
        if (_frames == 12) { Probe(); GD.Print("M5_HUD_READY"); }
        double now = Time.GetTicksMsec() / 1000.0;
        if (now - _lastState < 0.5) return;
        _lastState = now;
        PrintState();
    }

    /// <summary>The boot probe: HUD rect + the candidate cells' live px and
    /// walkability (covered = its screen px lands inside the measured rect —
    /// the exact cells MC 11958 measured as swallowed).</summary>
    private void Probe()
    {
        var s = S;
        if (s is null) return;
        GD.Print($"M5_HUD_SEED {s.Seed} area={s.CurrentArea}");
        var panel = Act.HudPanel.GetNodeOrNull<Control>("HudPanel");
        var rect = panel?.Size ?? Vector2.Zero;
        GD.Print($"M5_HUD_RECT w={(int)rect.X} h={(int)rect.Y}");
        for (int y = 0; y < AreaRenderer.H; y++)
            ProbeCell(new Vector2I(0, y), rect); // west edge (the door row)
        for (int x = 1; x <= 3; x++)
            for (int y = 6; y <= 12; y++)
                ProbeCell(new Vector2I(x, y), rect); // interior ground under the rect
    }

    private void ProbeCell(Vector2I cell, Vector2 rect)
    {
        var px = Act.Hero.CellToScreen(cell);
        bool covered = px.X >= 0 && px.Y >= 0 && px.X < rect.X && px.Y < rect.Y;
        GD.Print($"M5_HUD_CELL x={cell.X} y={cell.Y} walk={Act.Tiles.IsWalkable(cell)} "
               + $"px={(int)px.X},{(int)px.Y} covered={covered}");
    }

    private void PrintState()
    {
        var s = S;
        if (s is null) return;
        string town = TownState();
        GD.Print($"M5_HUD_STATE t={Time.GetTicksMsec() / 1000.0:F1} hero={Act.Hero.Cell.X},{Act.Hero.Cell.Y} "
               + $"walking={Act.Hero.IsWalking} area={s.CurrentArea} gold={s.Gold} town={town}");
        if (town == "visible") PrintBuyButton();
    }

    private string TownState()
    {
        if (Act.GetNodeOrNull<CanvasLayer>("TownPanelLayer") is not { } layer) return "absent";
        foreach (var child in layer.GetChildren())
            if (child is Control c) return c.Visible ? "visible" : "hidden";
        return "absent";
    }

    /// <summary>The panel's painted BUY row, read from the live tree (pulled
    /// text, pulled px — the leg hunts nothing it did not read): the driver
    /// clicks THIS px for the regression side (click on an OPEN panel hits the
    /// panel, never the world).</summary>
    private void PrintBuyButton()
    {
        if (_buyPx.Length > 0) return;
        if (Act.GetNodeOrNull<CanvasLayer>("TownPanelLayer") is not { } layer) return;
        foreach (var n in Descendants(layer))
            if (n is Button b && b.Text.StartsWith("BUY "))
            {
                var px = b.GlobalPosition + b.Size / 2f;
                _buyPx = $"{(int)px.X},{(int)px.Y}";
                GD.Print($"M5_HUD_BTN text={b.Text} px={_buyPx}");
                return;
            }
    }

    private static System.Collections.Generic.IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var deep in Descendants(child)) yield return deep;
        }
    }
}
