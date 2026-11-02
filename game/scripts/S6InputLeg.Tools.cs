using System;
using System.IO;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// S6InputLeg PARTIAL TWIN (the S4 file-hygiene split, reused shape): the
/// synthetic-INPUT and EVIDENCE tools the state machine in S6InputLeg.cs
/// drives — PushInput clicks/keys, the settled viewport Shot, the panel/button
/// hunts, the single label builders (TownPanelLogic's — the panel paints and
/// the leg hunts with the SAME string), the door/prey picks and the gold-pair
/// base capture. Pure presentation plumbing: no sim verb is reachable from
/// any method here — the TownPanelTests negative row scans this file.
/// </summary>
public partial class S6InputLeg
{
    private double _nextClick;

    /// <summary>One click ON A PANEL BUTTON: the label is built through the
    /// panel's own builder (no duplicated literal — the GameInputTests label
    /// idiom), the gold-pair base is captured BEFORE the click, and the click
    /// is the S4 PushInput triple (hover, press, release) at the button centre.</summary>
    private void ClickButton(Control root, string label)
    {
        var button = FindButton(root, label);
        if (button is null)
        {
            DebugButtons(root); // the honest stall readout: what the panel DID paint
            Fail($"no button \"{label}\"");
            return;
        }
        _goldBefore = S.Gold;
        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
    }

    /// <summary>Every label the panel currently paints, printed once per 5 s —
    /// a mismatch names itself instead of timing out silently (the S4 law).</summary>
    private void DebugButtons(Node root)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (now < _nextClick) return;
        _nextClick = now + 5.0;
        var texts = "";
        foreach (var child in root.FindChildren("*", "Button", recursive: true))
            if (child is Button b) texts += $"[{b.Text}]";
        GD.Print($"M5_S6_DEBUG buttons painted: {texts}");
    }

    private string BuyLabelOf(string baseId)
    {
        var row = S.Bases.For(baseId);
        return TownPanelLogic.BuyLabel(row.Name, Vendor.BuyPrice(row, Vendor.Standard));
    }

    private string HireLabelOf(string id)
    {
        var row = S.Hirelings.For(id);
        return TownPanelLogic.HireLabel(row.Name, row.HireGold);
    }

    /// <summary>One synthetic click at a WINDOW px — press then release, pushed
    /// through the window's real input queue (hover rides first for GUI picks).</summary>
    private void Click(Vector2 windowPx, MouseButton button)
    {
        var win = GetWindow();
        win.PushInput(new InputEventMouseMotion { GlobalPosition = windowPx, Position = windowPx });
        foreach (bool pressed in new[] { true, false })
            win.PushInput(new InputEventMouseButton
            {
                ButtonIndex = button,
                GlobalPosition = windowPx,
                Position = windowPx,
                Pressed = pressed,
            });
    }

    private void ReclickMonster()
    {
        if (_monPos.Length == 0) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (now < _nextClick) return; // a human tempo, not a frame storm
        _nextClick = now + 0.15;
        var xy = _monPos.Split(',');
        Click(Act.Hero.CellToScreen(new Vector2I(int.Parse(xy[0]), int.Parse(xy[1]))), MouseButton.Right);
    }

    private void KeyPress(Key key)
    {
        foreach (bool pressed in new[] { true, false })
            GetWindow().PushInput(new InputEventKey
            {
                Keycode = key,
                PhysicalKeycode = key,
                Pressed = pressed,
            });
    }

    /// <summary>Root viewport → PNG, ALWAYS after the settle span Enter()-ed
    /// for the state it depicts (llvmpipe present lag ~2-3s under Xvfb — the
    /// S4 leg measured it; a same-frame read shows the OLD frame).</summary>
    private void Shot(string name)
    {
        var img = GetViewport().GetTexture().GetImage();
        var path = Path.Combine(OutDir, $"{++_shots:00}-{name}.png");
        img.SavePng(path);
        GD.Print($"M5_S6_SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
    }

    /// <summary>The EAST-edge door cell nearest the hero (S4's pick, mchy link).</summary>
    private Vector2I DoorCell()
    {
        var from = Act.Hero.Cell;
        Vector2I best = from;
        int bestD = int.MaxValue;
        foreach (var c in Act.Tiles.Walkable)
        {
            if (c.X != AreaRenderer.W - 1) continue;
            int d = Math.Max(Math.Abs(c.X - from.X), Math.Abs(c.Y - from.Y));
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    /// <summary>The standing row-cell FARTHEST from the arrival centre — the
    /// deep ponurak that SURVIVES strikes and counters (the chip source; a
    /// pokrzywnik dies in one strike and never chips anything).</summary>
    private (string Key, Vector2I Cell)? DeepCell()
    {
        (string Key, Vector2I Cell)? best = null;
        var centre = Act.Tiles.CenterCell();
        var row = Act.Monsters.RowSnapshot();
        for (int i = 0; i < row.Count; i++)
        {
            var d = Math.Max(Math.Abs(row[i].Cell.X - centre.X), Math.Abs(row[i].Cell.Y - centre.Y));
            if (best is null || d > Math.Max(Math.Abs(best.Value.Cell.X - centre.X), Math.Abs(best.Value.Cell.Y - centre.Y)))
                best = row[i];
        }
        return best;
    }

    private TownPanel? FindPanel() => FindPanelIn(Act);

    private static TownPanel? FindPanelIn(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is TownPanel panel) return panel;
            if (FindPanelIn(child) is { } deep) return deep;
        }
        return null;
    }

    private static Button? FindButton(Node root, string text)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button b && b.Text == text) return b;
            if (FindButton(child, text) is { } deep) return deep;
        }
        return null;
    }
}
