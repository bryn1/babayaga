using System.IO;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// StashChestLeg PARTIAL TWIN (the EquipCellLeg/S4/S6 file-hygiene split, reused
/// shape): the synthetic-INPUT and EVIDENCE tools the state machine in
/// StashChestLeg.cs drives — PushInput clicks/keys, the settled viewport Shot,
/// the panel/button/cell hunts, and the one shared label builder
/// (TownPanelLogic's — the panel paints and the leg hunts with the SAME string,
/// and the chest's own names come from StashPanelLogic, never re-derived
/// literals). Pure presentation plumbing: no sim verb is reachable from any
/// method here — the StashPanelTests negative row scans this file (the
/// TownPanelTests LegFiles twin).
/// </summary>
public partial class StashChestLeg
{
    /// <summary>One click ON A PANEL BUTTON (the S6/Equip shape): the label
    /// comes from the panel's own builder, the gold base is captured BEFORE
    /// the click (the BUY rows), and the click is the PushInput triple (hover,
    /// press, release) at the button centre.</summary>
    private void ClickButton(Control root, string label)
    {
        var button = FindButtonByText(root, label);
        if (button is null)
        {
            Fail($"no button \"{label}\"");
            return;
        }
        _goldBefore = S.Gold;
        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
    }

    /// <summary>One click on a PAINTED anchor cell (the chest's own node NAME
    /// from StashPanelLogic — the D2 lesson generalized: names, never metadata).</summary>
    private void ClickCell(StashPanel root, string nodeName)
    {
        var cell = FindCell(root, nodeName);
        if (cell is null) { Fail($"no painted cell \"{nodeName}\""); return; }
        Click(cell.GetGlobalRect().GetCenter(), MouseButton.Left);
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
    /// S4 law; a same-frame read shows the OLD frame).</summary>
    private void Shot(string name)
    {
        var img = GetViewport().GetTexture().GetImage();
        var path = Path.Combine(OutDir, $"{++_shots:00}-{name}.png");
        img.SavePng(path);
        GD.Print($"M5_STASH_SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
    }

    // Type-first hunts (the S6 shape): the panels ride CanvasLayers, which are
    // Nodes, NOT Controls — a Control-typed layer hunt finds nothing.
    private TownPanel? FindTownPanel() => FindType<TownPanel>(Act);

    private StashPanel? FindChest() => FindType<StashPanel>(Act);

    /// <summary>A painted node inside the VISIBLE chest panel, by its shared
    /// node name (never metadata — the D2 law).</summary>
    private Control? FindCell(StashPanel panel, string nodeName) =>
        panel.Visible ? FindNamed<Control>(panel, nodeName) : null;

    private string HintText() // the S4 read-back: the HUD hint label is Hud's ONLY direct Label child
    {
        foreach (var child in Act.HudPanel.GetChildren())
            if (child is Label label) return label.Text;
        return "";
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

    private static T? FindNamed<T>(Node root, string name) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T typed && child.Name == name) return typed;
            if (FindNamed<T>(child, name) is { } deep) return deep;
        }
        return null;
    }

    private static Button? FindButtonByText(Node root, string text)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button b && b.Text == text) return b;
            if (FindButtonByText(child, text) is { } deep) return deep;
        }
        return null;
    }

    private string BuyLabelOf(string baseId)
    {
        var row = S.Bases.For(baseId);
        return TownPanelLogic.BuyLabel(row.Name, Vendor.BuyPrice(row, Vendor.Standard));
    }
}
