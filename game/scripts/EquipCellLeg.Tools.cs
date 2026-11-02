using System.IO;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// EquipCellLeg PARTIAL TWIN (the S4/S6 file-hygiene split, reused shape): the
/// synthetic-INPUT and EVIDENCE tools the state machine in EquipCellLeg.cs
/// drives — PushInput clicks/keys, the settled viewport Shot, the panel/cell
/// hunts, and the one shared label builder (TownPanelLogic's — the panel paints
/// and the leg hunts with the SAME string). Pure presentation plumbing: no sim
/// verb is reachable from any method here — the HeroEquipCellTests negative
/// row scans this file (the TownPanelTests LegFiles twin).
/// </summary>
public partial class EquipCellLeg
{
    /// <summary>One click ON A PANEL BUTTON (the S6 shape): the label comes from
    /// the panel's own builder, the gold-pair base is captured BEFORE the click,
    /// and the click is the PushInput triple (hover, press, release) at the
    /// button centre.</summary>
    private void ClickButton(Control root, string label)
    {
        var button = FindButton(root, label);
        if (button is null)
        {
            Fail($"no button \"{label}\"");
            return;
        }
        _goldBefore = S.Gold;
        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
    }

    /// <summary>One click on a PAINTED node (the thatch anchor cell — the
    /// presentation truth the panel just rebuilt; the node NAME is the shared
    /// InventoryPanelLogic.AnchorNodeName string, never a re-derived literal).</summary>
    private void ClickNode(Control node) =>
        Click(node.GetGlobalRect().GetCenter(), MouseButton.Left);

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
        GD.Print($"M5_EQ_SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
    }

    // Type-first hunts (the S6 shape): the panels ride CanvasLayers, which are
    // Nodes, NOT Controls — a Control-typed layer hunt finds nothing.
    private TownPanel? FindPanel() => FindType<TownPanel>(Act);

    private InventoryPanel? FindInvPanel() => FindType<InventoryPanel>(Act);

    /// <summary>The painted grid cell by its shared node name (the D2 lesson
    /// generalized: cell keys ride node NAMES and plain collections, NEVER
    /// metadata) — only the VISIBLE panel's current build answers.</summary>
    private Control? FindCell(string nodeName) =>
        FindType<InventoryPanel>(Act) is { Visible: true } panel
            ? FindNamed<Control>(panel, nodeName) : null;

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

    private static Button? FindButton(Node root, string text)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button b && b.Text == text) return b;
            if (FindButton(child, text) is { } deep) return deep;
        }
        return null;
    }

    private string BuyLabelOf(string baseId)
    {
        var row = S.Bases.For(baseId);
        return TownPanelLogic.BuyLabel(row.Name, Vendor.BuyPrice(row, Vendor.Standard));
    }
}
