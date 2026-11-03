using System.IO;
using Godot;

/// <summary>
/// GameMenuLeg PARTIAL TWIN (the StashChestLeg/S4 file-hygiene split, reused
/// shape): the synthetic-INPUT and EVIDENCE tools the state machine in
/// GameMenuLeg.cs drives — PushInput clicks/keys, the settled viewport Shot,
/// the menu/panel hunts and the CanvasLayer NUMBER read (the overlap row
/// reads the real layer values off the tree, never a literal). Pure
/// presentation plumbing: no sim verb is reachable from any method here —
/// the GameMenuTests driver scan pins that law over these two files.
/// </summary>
public partial class GameMenuLeg
{
    /// <summary>One click ON A PANEL BUTTON (the S6/Stash shape): the label
    /// comes from the twin builders (the panel paints and the leg hunts with
    /// the SAME string — no duplicated literal).</summary>
    private void ClickButton(Control root, string label)
    {
        var button = FindButtonByText(root, label);
        if (button is null)
        {
            Fail($"no button \"{label}\"");
            return;
        }
        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
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
        GD.Print($"M5_MENU_SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
    }

    // Type-first hunts (the S6 law): the panels ride CanvasLayers, which are
    // Nodes, NOT Controls — a Control-typed layer hunt finds nothing.

    private GameMenu? FindGameMenu() => FindType<GameMenu>(Act);

    private DeathMenu? FindDeathMenu() => FindType<DeathMenu>(Act);

    private TownPanel? FindTownPanel() => FindType<TownPanel>(Act);

    /// <summary>The CanvasLayer NUMBER read OFF THE TREE (the overlap row's
    /// honest read — the wiring itself, not the constant re-typed).</summary>
    private int LayerOf(string layerName)
    {
        var layer = FindNamed<CanvasLayer>(Act, layerName)
            ?? (CanvasLayer?)GetTree().Root.FindChild(layerName, recursive: true, owned: false);
        return layer?.Layer ?? -1; // -1 names the absence loudly in the marker
    }

    private static T? FindType<T>(Node root) where T : Node
    {
        if (!Godot.GodotObject.IsInstanceValid(root)) return null;
        foreach (var child in root.GetChildren())
        {
            if (child is T typed) return typed;
            if (FindType<T>(child) is { } deep) return deep;
        }
        return null;
    }

    private static T? FindNamed<T>(Node root, string name) where T : Node
    {
        if (!Godot.GodotObject.IsInstanceValid(root)) return null;
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
}
