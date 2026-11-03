using System.Collections.Generic;
using System.IO;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// GambleLeg PARTIAL TWIN (the CubeLeg/FollowerLeg file-hygiene split, reused
/// shape): the synthetic-INPUT and EVIDENCE tools the state machine drives —
/// the PushInput click/key triple, the settled viewport Shot, the button/label
/// hunts through MANUAL tree walks (the MC 11970 finding: FindChildren's
/// class filter answers EMPTY for freshly code-painted panels under this
/// harness — the walk cannot lie about what it visits), and the HUD hint
/// read-back (the S4 idiom — the hint label is Hud's only direct Label
/// child). The projection EXPECTATIONS re-derive through the panel's OWN pure
/// builder (GamblePanelLogic over the session's own tables at the leg seed —
/// the CubeLeg BuyLabelOf idiom, no literal lives here). Pure presentation
/// plumbing: NO sim verb is reachable from any method here (the
/// GamblePanelTests scan pins it) — the gambles happen ONLY as clicks on the
/// painted BUY button.
/// </summary>
public partial class GambleLeg
{
    private double _nextDebug;

    /// <summary>The panel's next-offer label after `draws` display draws —
    /// re-derived independently through GamblePanelLogic (the same pure code
    /// path the panel paints through, from the same seed): the fence's
    /// fidelity read, never a hand-written string.</summary>
    private string ProjectionLabel(int draws)
    {
        var rng = new Rng(GamblePanelLogic.DisplaySeedOf(S.Seed));
        GambleOffer offer = GamblePanelLogic.ProjectOffer(S.Bases, rng, S.Level);
        for (int i = 1; i < draws; i++)
            offer = GamblePanelLogic.ProjectOffer(S.Bases, rng, S.Level);
        return GamblePanelLogic.OfferLabel(offer);
    }

    /// <summary>One click ON A PANEL BUTTON at its painted centre — the S4
    /// PushInput triple (hover, press, release); the gold-pair base is
    /// captured by the CALLER before it, never here.</summary>
    private void ClickButton(Control root, string label)
    {
        var button = FindButton(root, label);
        if (button is null)
        {
            DebugPaint(root); // the honest stall readout: what the panel DID paint
            Fail($"no button \"{label}\"");
            return;
        }
        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
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
        GD.Print($"M5_GAM_DEBUG painted: {seen}");
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
        GD.Print($"M5_GAM_SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
    }

    /// <summary>The HUD hint line — the S4 read-back: the HUD hint label is
    /// Hud's ONLY direct Label child (the StashChestLeg/CubeLeg twin).</summary>
    private string HintText()
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

    private static Button? FindButton(Node root, string text)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button b && b.Text == text) return b;
            if (FindButton(child, text) is { } deep) return deep;
        }
        return null;
    }

    private static Label? FindLabel(Node root, string text)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label l && l.Text == text) return l;
            if (FindLabel(child, text) is { } deep) return deep;
        }
        return null;
    }
}
