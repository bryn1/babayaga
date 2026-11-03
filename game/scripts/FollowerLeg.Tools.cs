using System.Collections.Generic;
using System.IO;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// FollowerLeg PARTIAL TWIN (the CubeLeg/S4 file-hygiene split, reused shape):
/// the synthetic-INPUT and EVIDENCE tools the state machine drives — the
/// PushInput click/key triple, the settled viewport Shot, the button hunt (the
/// HIRE label is built through the panel's OWN builder — TownPanelLogic.
/// HireLabel over the session's own row, no duplicated literal), and the
/// MANUAL tree walks (the MC 11970 finding: FindChildren's class filter
/// answers EMPTY for freshly code-painted panels under this harness — the walk
/// cannot lie about what it visits). Pure presentation plumbing: NO sim verb
/// is reachable from any method here (the FollowerLeg scan mirrors CubeLeg's)
/// — the hire and the dismiss happen ONLY as clicks on painted buttons.
/// </summary>
public partial class FollowerLeg
{
    private double _nextDebug;

    /// <summary>The HIRE row's label — pulled from the session's own table
    /// through the panel's own builder (the CubeLeg BuyLabelOf idiom).</summary>
    private string HireLabel()
    {
        var row = S.Hirelings.For("strzelczyni");
        return TownPanelLogic.HireLabel(row.Name, row.HireGold);
    }

    /// <summary>One click ON A PANEL BUTTON: label built by the panel's own
    /// law, the gold-pair base captured BEFORE, the S4 PushInput triple
    /// (hover, press, release) at the button centre.</summary>
    private void ClickButton(Control root, string label)
    {
        var button = FindButton(root, label);
        if (button is null)
        {
            DebugPaint(root); // the honest stall readout: what the panel DID paint
            Fail($"no button \"{label}\"");
            return;
        }
        _goldBefore = S.Gold;
        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
    }

    /// <summary>The FIRST painted texture on a TextureRect under the panel —
    /// the portrait read (manual walk, the MC 11970 law). Null when the panel
    /// paints no picture at all.</summary>
    private static Texture2D? PaintedPortrait(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is TextureRect rect && rect.Texture is { } tex) return tex;
            if (PaintedPortrait(child) is { } deep) return deep;
        }
        return null;
    }

    /// <summary>Every painted label + button text, printed once per 5 s — a
    /// mismatch names itself instead of timing out silently (the S4 law).</summary>
    private void DebugPaint(Node root)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (now < _nextDebug) return;
        _nextDebug = now + 5.0;
        var seen = "";
        foreach (var child in Walk(root))
            seen += child switch
            {
                Label l => $"[{l.Text}]",
                Button b => $"{{{b.Text}}}",
                _ => "",
            };
        GD.Print($"M5_FOL_DEBUG painted: {seen}");
    }

    private static IEnumerable<Node> Walk(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var deep in Walk(child)) yield return deep;
        }
    }

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
    /// S4 leg measured it; a same-frame read shows the OLD frame).</summary>
    private void Shot(string name)
    {
        var img = GetViewport().GetTexture().GetImage();
        var path = Path.Combine(OutDir, $"{++_shots:00}-{name}.png");
        img.SavePng(path);
        GD.Print($"M5_FOL_SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
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
