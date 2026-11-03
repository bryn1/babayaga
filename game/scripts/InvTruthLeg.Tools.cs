using BabaYaga.Sim;
using Godot;

/// <summary>
/// InvTruthLeg PARTIAL TWIN (the EquipCellLeg/CubeLeg file-hygiene split,
/// reused shape): the synthetic-INPUT and EVIDENCE tools the state machine in
/// InvTruthLeg.cs drives — the PushInput click/key triple, the settled
/// viewport Shot, the panel/cell hunts through MANUAL tree walks (FindChildren
/// answers EMPTY for freshly code-painted panels under this harness — the
/// walk cannot lie about what it visits; the panels ride CanvasLayers, which
/// are Nodes NOT Controls, so a Control-typed layer hunt finds nothing), and
/// the shared builders: the BUY label from TownPanelLogic's own (the panel
/// paints and the leg hunts with the SAME string) and the DUMP's anchor read
/// through StashPanelLogic.CarriedItems (the ONE pull — the leg reports the
/// sim's answer, it never recomputes one). Pure presentation plumbing: NO sim
/// verb is reachable from any method here — tests/InventoryPanelDumpTruthTests.cs
/// scans both leg files for exactly that law.
/// </summary>
public partial class InvTruthLeg
{
    /// <summary>The dump report's carried anchors, joined for the marker line
    /// — the pulled sim truth (row,col per carried item, dump order).</summary>
    private string DumpAnchorsJoined() =>
        string.Join("|", StashPanelLogic.CarriedItems(S.Dump()).ConvertAll(a => $"{a.Row},{a.Col}"));

    /// <summary>One click ON A PANEL BUTTON (the S6 shape): the label comes
    /// from the panel's own builder; the gold-pair base is captured by the
    /// CALLER before it; the click is the PushInput triple at the centre.</summary>
    private void ClickButton(Control root, string label)
    {
        var button = FindButton(root, label);
        if (button is null)
        {
            Fail($"no button \"{label}\"");
            return;
        }
        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
    }

    /// <summary>One click on a PAINTED node (the thatch anchor cell — the
    /// dump-truth paint the panel just rebuilt; the node NAME is the shared
    /// InventoryPanelLogic.AnchorNodeName string, never a re-derived literal).</summary>
    private void ClickNode(Control node) =>
        Click(node.GetGlobalRect().GetCenter(), MouseButton.Left);

    /// <summary>One synthetic click at a WINDOW px — press then release,
    /// pushed through the window's real input queue (hover rides first for
    /// GUI picks).</summary>
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
        var path = System.IO.Path.Combine(OutDir, $"{++_shots:00}-{name}.png");
        img.SavePng(path);
        GD.Print($"M5_INV_SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
    }

    // Type-first hunts (the S6 shape) — manual walks, never FindChildren.
    private TownPanel? FindPanel() => FindType<TownPanel>(Act);

    private InventoryPanel? FindInvPanel() => FindType<InventoryPanel>(Act);

    private StashPanel? FindChest() => FindType<StashPanel>(Act);

    /// <summary>The chest's PACK-side cell by its shared node name (the same
    /// StashPanelLogic.PackAnchorName string the panel paints with) — only
    /// the VISIBLE chest's current build answers.</summary>
    private Control? FindChestCell(string nodeName) =>
        FindType<StashPanel>(Act) is { Visible: true } chest
            ? FindNamed<Control>(chest, nodeName) : null;

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
