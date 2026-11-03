using BabaYaga.Sim;
using Godot;

/// <summary>
/// M5 RITUAL-CRAFT PANEL (MC 11970, LEDGER row 75 cube half): the bone cube
/// made playable UI — the grodzisko overlay listing THE ADMITTED recipes
/// (s.CubeRecipes read-back; the engine's admission law already decided what
/// may stand before a player's eyes, and its SkippedCount stays engine-side).
/// Zero admitted paints the ONE honest line (CraftPanelLogic.EmptyLine) —
/// never a fake row. Instantiated OVER Act on its own CanvasLayer (the
/// DeathMenu lesson) and laid out MANUALLY from Size on every
/// NOTIFICATION_RESIZED (the RESIZED-layout law + the D3 sibling guard, the
/// TownPanel twin). Pull-only display (names × counts through
/// CraftPanelLogic.RowLabels, every name pulled from s.Bases); the CRAFT
/// button rides the ONE logged door "craft &lt;recipeId&gt;" through
/// CommandLog.Call (CraftPanelLogic's grammar — the logged line IS the CLI
/// grammar, so a clicked craft replays through the CommandRunner); the engine's
/// gates (DEAD → town → known → match → room) stay the engine's behind the
/// widget. Every engine refusal becomes a HUD hint (the TownPanel hint idiom,
/// never a swallow); the success hint is CraftPanelLogic.OutHint and the panel
/// then REFRESHES — the output row vanishes with the consumed inputs because
/// the paint is pulled, never appended.
/// </summary>
public partial class CraftPanel : Control
{
    /// <summary>Wired by Act to the HUD hint line — the engine's own words,
    /// painted (the TownPanel OnHint idiom).</summary>
    public System.Action<string>? OnHint;

    private ColorRect _dim = null!;
    private VBoxContainer _box = null!;

    private static GameSession? S => GameContext.Session;

    public override void _Ready()
    {
        _dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f) }; // dusk dim (the TownPanel twin)
        AddChild(_dim);
        _box = new VBoxContainer();
        AddChild(_box);
        VisibilityChanged += Refresh; // the InventoryPanel show-refresh idiom (Refresh re-checks Visible)
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (Size != Vector2.Zero) SetProcess(false); // settle frames handled by RESIZED
        LayoutToSize();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutToSize();
    }

    /// <summary>Below the HUD panel's bottom (the TownPanel/S4 law: a row at
    /// the HUD's height is a row nobody can click), centred across. The box
    /// sizes itself; nothing anchors here.</summary>
    private void LayoutToSize()
    {
        // The D3 sibling-guard (MC 11943): reject the pre-_Ready RESIZED pass;
        // layout rides the settle frame / later RESIZED. One guard at the
        // shared idiom, every overlay.
        if (!IsNodeReady() || Size == Vector2.Zero) return;
        _dim.Position = Vector2.Zero;
        _dim.Size = Size;
        _box.ResetSize();
        _box.Position = new Vector2((Size.X - _box.Size.X) / 2f,
            Mathf.Max(Size.Y * 0.42f, 300f));
    }

    /// <summary>Rebuilds the painted rows from the session's OWN admitted
    /// table (the pull-only rebuild-on-show idiom) — zero writes, zero
    /// invented rows.</summary>
    private void Refresh()
    {
        foreach (var child in _box.GetChildren()) child.QueueFree();
        var s = S;
        if (s is null || !Visible) return;

        _box.AddChild(Header(CraftPanelLogic.Header));
        var rows = CraftPanelLogic.RowLabels(s.CubeRecipes, s.Bases);
        if (rows.Count == 0)
        {
            // The honest empty state (the brief's law): zero admitted recipes
            // paint ONE line and NO rows — the skip count stays engine-side,
            // the player sees only the truth that nothing warm stands here.
            _box.AddChild(new Label { Text = CraftPanelLogic.EmptyLine });
            return;
        }
        foreach (var r in s.CubeRecipes.Recipes)
            _box.AddChild(Row(new Label { Text = CraftPanelLogic.RowLabel(r, s.Bases) },
                Button(CraftPanelLogic.CraftButton, () => Craft(r))));
    }

    // ---- the one door: one logged line per click (CraftPanelLogic's grammar) ----

    private void Craft(CubeRecipeRow r)
    {
        var s = S; if (s is null) return;
        try { CommandLog.Call(CraftPanelLogic.CraftLine(r.Id), () => s.CraftRecipe(r.Id)); }
        catch (SessionRejectedException e) { Hint(e.Message); return; } // the hint IS the answer
        Hint(CraftPanelLogic.OutHint(r, s.Bases)); // success rides a hint TOO (the brief's pair)…
        Refresh(); // …and the pull-only repaint proves the OUTPUT landed in the pack
    }

    private void Hint(string message) => OnHint?.Invoke(message);

    // ---- tiny paint helpers (the TownPanel twins) ----

    private static Label Header(string text) => new()
    {
        Text = text,
        LabelSettings = new LabelSettings { FontSize = 20, FontColor = new Color("D8CBA6") },
    };

    private static Control Row(params Control[] cells)
    {
        var row = new HBoxContainer();
        foreach (var c in cells) row.AddChild(c);
        return row;
    }

    private static Button Button(string text, System.Action onPressed)
    {
        var b = new Button { Text = text };
        b.Pressed += onPressed;
        return b;
    }
}
