using System;
using System.IO;
using Godot;

/// <summary>
/// S4InputLeg PARTIAL TWIN (MC 11937, file-hygiene split of the 449-line leg):
/// the synthetic-INPUT and EVIDENCE tools the state machine in S4InputLeg.cs
/// drives — PushInput clicks/keys, the settled viewport Shot, the HUD hint
/// read-back, the map/cell picks, and the death-menu hunts. Pure presentation
/// plumbing: no sim verb is reachable from any method here — the
/// GameInputTests ClickPaths scan lists this file so that claim is pinned,
/// not decorative.
/// </summary>
public partial class S4InputLeg
{
    private double _nextDebug;

    /// <summary>One honest stall readout every 5 s: cell, walk state, distance
    /// to target — a wedged step names itself in the log (found both leg bugs
    /// of this card; kept in for the next one).</summary>
    private void DebugMove(string tag)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (now < _nextDebug) return;
        _nextDebug = now + 5.0;
        GD.Print($"M5_S4_DEBUG {tag} area={S.CurrentArea} cell={Act.Hero.Cell} walking={Act.Hero.IsWalking} target={_moveTarget}");
    }

    /// <summary>One synthetic click at a WINDOW px — press then release, pushed
    /// through the window's real input queue (hover rides first for GUI targets).</summary>
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
        if (button == MouseButton.Right) _strikes++;
    }

    private void ReclickMonster()
    {
        if (_monPos.Length == 0) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (now < _nextClick) return; // a fast human click — not a frame storm
        _nextClick = now + 0.10; // DeathLeg's ≈550-strike deep-row grind lands in ~1 min at this tempo
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

    /// <summary>Root viewport → PNG. Both capture paths (texture readback and the
    /// X11 root grab) read the last PRESENTED frame, and llvmpipe present lags
    /// game state by seconds under Xvfb (the leg's own RED: transition shots
    /// byte-identical while the world moved) — so EVERY shot rides a settle
    /// span Enter()-ed after the state it depicts, never the predicate frame.</summary>
    private void Shot(string name)
    {
        var img = GetViewport().GetTexture().GetImage();
        var path = Path.Combine(OutDir, $"{++_shots:00}-{name}.png");
        img.SavePng(path);
        GD.Print($"M5_S4_SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
    }

    /// <summary>The HUD hint label is Hud's ONLY direct Label child (the readout
    /// labels live inside the panel's VBox) — read back, never parsed from pixels.</summary>
    private string HintText()
    {
        foreach (var child in Act.HudPanel.GetChildren())
            if (child is Label label) return label.Text;
        return "";
    }

    /// <summary>A far INTERIOR walkable cell — edges excluded. The XVFB leg's own
    /// RED caught the first scan cell being the (0,0) CORNER (a door): the plain
    /// move click walked there, the edge-door intent fired, and the area flipped.
    /// A P3 move click clicks ground, never a door.</summary>
    private Vector2I FarTownCell()
    {
        var from = Act.Hero.Cell;
        foreach (var c in Act.Tiles.Walkable)
        {
            if (c.X == 0 || c.Y == 0 || c.X == AreaRenderer.W - 1 || c.Y == AreaRenderer.H - 1) continue;
            if (Math.Max(Math.Abs(c.X - from.X), Math.Abs(c.Y - from.Y)) < 6) continue;
            // INSIDE the static camera's iso view — and OUT of the HUD panel:
            // the leg's REDS: an NW target drew an empty field (above the top
            // edge); a Δ(x+y)=−10 target cropped the hero; a west target at
            // screen x≈128 landed UNDER the HUD PanelContainer and the click
            // was eaten (walking=False forever). The hero is 128px tall
            // standing ON the cell: |Δ(x+y)|≤6 keeps it whole; Δ(x−y)∈[−5,8]
            // keeps the sprite between screen x≈320 and ≈1150 — clear of it.
            if ((c.X - c.Y) - (from.X - from.Y) < -5) continue;
            if ((c.X - c.Y) - (from.X - from.Y) > 8) continue;
            if (Math.Abs((c.X + c.Y) - (from.X + from.Y)) > 6) continue;
            return c;
        }
        return Act.Tiles.CenterCell();
    }

    /// <summary>The EAST-edge (link 0 = mchy from grodzisko) walkable cell nearest
    /// the hero — a door the camera actually shows.</summary>
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

    /// <summary>The standing row-cell FARTHEST from the arrival centre — the deep
    /// ponurak (DeathLeg's grind target: survives strikes, counters back).</summary>
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

    /// <summary>Attack target: a pokrzywnik while any stands there (the near-spawn
    /// band, P5's row); once they are all despawned, ANY standing monster (the deep
    /// ponuraks) — the Sim respawns its rows, the presentation roster only shrinks.</summary>
    private (string Key, Vector2I Cell)? FirstMonsterCell()
    {
        (string Key, Vector2I Cell)? any = null;
        foreach (var entry in Act.Monsters.RowSnapshot())
        {
            any ??= entry;
            if (entry.Key == "pokrzywnik") return entry;
        }
        return any;
    }

    private DeathMenu? FindMenu() => FindMenuIn(Act);

    private static DeathMenu? FindMenuIn(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is DeathMenu menu) return menu;
            if (FindMenuIn(child) is { } deep) return deep;
        }
        return null;
    }

    /// <summary>Depth-first hunt for the menu's button by label — the button→door
    /// map itself is DeathMenuLogic's, pinned in GameInputTests.</summary>
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
