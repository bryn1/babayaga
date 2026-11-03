using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// CubeLeg PARTIAL TWIN (the S4/S6 file-hygiene split, reused shape): the
/// synthetic-INPUT and EVIDENCE tools the state machine in CubeLeg.cs drives —
/// PushInput clicks/keys, the settled viewport Shot, the panel/label/button
/// hunts (the panel paints and the leg reads with the SAME builder strings —
/// TownPanelLogic.BuyLabel and CraftPanelLogic's row/label idiom, no
/// duplicated literal), the gold-pair base capture and the HUD hint read-back
/// (the S4 HintText idiom). Pure presentation plumbing: no sim verb is
/// reachable from any method here — the CraftPanelTests scan row pins it.
/// </summary>
public partial class CubeLeg
{
    private double _nextDebug;

    /// <summary>One click ON A PANEL BUTTON: the label is built through the
    /// panel's own builder (no duplicated literal), the gold-pair base is
    /// captured BEFORE the click, and the click is the S4 PushInput triple
    /// (hover, press, release) at the button centre.</summary>
    private void ClickButton(Control root, string label)
    {
        var button = FindButton(root, label);
        if (button is null)
        {
            DebugTexts(root); // the honest stall readout: what the panel DID paint
            Fail($"no button \"{label}\"");
            return;
        }
        _goldBefore = S.Gold;
        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
    }

    private string BuyLabelOf(string baseId)
    {
        var row = S.Bases.For(baseId);
        return TownPanelLogic.BuyLabel(row.Name, Vendor.BuyPrice(row, Vendor.Standard));
    }

    /// <summary>The row/empty-line texts the panel paints (every Label under
    /// the panel root except the header line — the header is fixed chrome, the
    /// brief's row count IS the recipe count). MANUAL walk (MC 11970 finding:
    /// FindChildren's class filter answered EMPTY for a freshly-painted panel
    /// under this leg harness, twice-verified; the tree walk cannot lie about
    /// what it visits — the S4 read-the-PAINT law made concrete).</summary>
    private static List<string> PaintedRowTexts(Control panel) =>
        WalkTexts(panel).Where(t => !t.StartsWith(CraftPanelLogic.Header)).ToList();

    private static List<string> WalkTexts(Node root)
    {
        var texts = new List<string>();
        foreach (var child in root.GetChildren())
        {
            if (child is Label label) texts.Add(label.Text);
            texts.AddRange(WalkTexts(child));
        }
        return texts;
    }

    private int CountButtons(Node root, string text) =>
        WalkButtons(root).Count(t => t == text);

    private static List<string> WalkButtons(Node root)
    {
        var texts = new List<string>();
        foreach (var child in root.GetChildren())
        {
            if (child is Button b) texts.Add(b.Text);
            texts.AddRange(WalkButtons(child));
        }
        return texts;
    }

    /// <summary>Every painted label + button text, printed once per 5 s — a
    /// mismatch names itself instead of timing out silently (the S4 law).</summary>
    private void DebugTexts(Node root)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (now < _nextDebug) return;
        _nextDebug = now + 5.0;
        var seen = "";
        foreach (var t in WalkTexts(root)) seen += $"[{t}]";
        foreach (var t in WalkButtons(root)) seen += $"{{{t}}}";
        GD.Print($"M5_CUBE_DEBUG painted: {seen}");
    }

    /// <summary>The HUD hint line — the S4 read-back: the HUD hint label is
    /// Hud's ONLY direct Label child (the StashChestLeg twin).</summary>
    private string HintText()
    {
        foreach (var child in Act.HudPanel.GetChildren())
            if (child is Label label) return label.Text;
        return "";
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
        GD.Print($"M5_CUBE_SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
    }

    private static T? FindType<T>(Node root) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T typed) return typed;
            if (FindType<T>(child) is { } deep) return deep;
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
