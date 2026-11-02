using System.Collections.Generic;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// M5 TOWN STASH CHEST PANEL (MC 11953, LEDGER row 75 chest half): the
/// grodzisko chest — a WINDOWED stash overlay instantiated OVER Act on its own
/// CanvasLayer (the TownPanel/DeathMenu lesson: a Control under the Node2D
/// world pans with the camera and dodges the GUI clicks) and laid out MANUALLY
/// from Size on every NOTIFICATION_RESIZED (the RESIZED-layout law; the
/// IsNodeReady NRE-guard of the D3 lesson). Two grids side by side: the
/// carried pack and the selected stash tab — painted from the PUBLIC dump
/// report's own anchors (StashPanelLogic reads; the Sim answers "which cell?"
/// nowhere else, ARCH v8, so nothing here re-derives or invents). A click on a
/// painted pack anchor STORES through the logged door line "stash_store
/// &lt;tab&gt; &lt;x&gt; &lt;y&gt;"; a click on a tab anchor TAKES through
/// "stash_take" (InventoryPanel's click-&gt;door idiom, the CLI twin grammar —
/// the panel clicks ARE the replayable session). Every engine refusal becomes
/// a HUD hint (the TownPanel OnHint law), never a swallow. Tabs: paid tabs
/// plus ONE locked tab, painted "(locked)" (StashPanelLogic) — clicking a
/// locked tab calls NO door, keeps the current tab, and paints the engine's
/// own refusal wording. Pull-only display; the tab unlock truth stays the
/// engine's (IsStashTabUnlocked; tab 2 via glowa's StashTab reward).
/// </summary>
public partial class StashPanel : Control
{
    /// <summary>Wired by Act to the HUD hint line — the engine's own words,
    /// painted (the TownPanel/SessionClockNode OnHint idiom).</summary>
    public System.Action<string>? OnHint;

    /// <summary>The selected tab — a presentation-side choice the Sim owns no
    /// answer to (the belt-slot-pick doctrine); the engine's tab gates stay
    /// the engine's behind it. Read back by the evidence leg (never written
    /// from outside).</summary>
    public int SelectedTab => _selected;

    private ColorRect _dim = null!;
    private VBoxContainer _box = null!;
    private int _selected = 1;

    private static GameSession? S => GameContext.Session;

    public override void _Ready()
    {
        _dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f) }; // the TownPanel dusk dim
        AddChild(_dim);
        _box = new VBoxContainer();
        AddChild(_box);
        VisibilityChanged += Refresh; // the InventoryPanel show-refresh idiom
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

    /// <summary>Below the HUD panel's bottom (the S4 law — the HUD's
    /// PanelContainer eats every click under it), centred across; the box
    /// sizes itself, nothing anchors here (the TownPanel layout twin).</summary>
    private void LayoutToSize()
    {
        if (!IsNodeReady() || Size == Vector2.Zero) return; // the D3 pre-_Ready guard
        _dim.Position = Vector2.Zero;
        _dim.Size = Size;
        _box.ResetSize();
        _box.Position = new Vector2((Size.X - _box.Size.X) / 2f,
            Mathf.Max(Size.Y * 0.42f, 300f));
    }

    /// <summary>Rebuilds the painted chest from the session's own reports —
    /// pull-only, zero writes (the InventoryPanel rebuild-on-show idiom).</summary>
    private void Refresh()
    {
        foreach (var child in _box.GetChildren()) child.QueueFree();
        var s = S;
        if (s is null || !Visible) return;

        _box.AddChild(Header($"The town chest — Gold {s.Gold}"));

        var tabs = Row();
        tabs.AddChild(new Label { Text = "Tabs:" });
        foreach (var tab in StashPanelLogic.TabsShown(s.StashTabs))
        {
            bool locked = StashPanelLogic.LockedShown(s, tab);
            string label = locked ? StashPanelLogic.LockedTabLabel(tab) : StashPanelLogic.TabButtonLabel(tab);
            int t = tab;
            tabs.AddChild(Button(label, () => PickTab(t, locked)));
        }
        _box.AddChild(tabs);

        var grids = new HBoxContainer();
        grids.AddChild(BuildPack(s));
        grids.AddChild(BuildTab(s));
        _box.AddChild(grids);

        // The named lists beside the grids (the InventoryPanel idiom — the
        // leg reads names off the session, this pair is what a human sees).
        var lists = new VBoxContainer();
        var stowed = StashPanelLogic.TabItems(s.Dump(), _selected);
        lists.AddChild(new Label { Text = $"Chest — tab {_selected}: {(stowed.Count == 0 ? "(empty)" : string.Join(", ", Names(s, stowed)))}" });
        var carried = StashPanelLogic.CarriedItems(s.Dump());
        lists.AddChild(new Label { Text = $"Pack: {(carried.Count == 0 ? "(empty)" : string.Join(", ", Names(s, carried)))}" });
        _box.AddChild(lists);
    }

    // ---- the two doors: one logged line per anchor click (StashPanelLogic's table) ----

    private void PickTab(int tab, bool locked)
    {
        var (chosen, hint) = StashPanelLogic.SelectTab(_selected, tab, unlocked: !locked);
        if (hint is not null) { Hint(hint); return; } // the lock paints its words; NO door, NO switch
        _selected = chosen;
        Refresh();
    }

    private void StoreViaDoor(int row, int col)
    {
        var s = S; if (s is null) return;
        try { CommandLog.Call(StashPanelLogic.StoreLine(_selected, row, col), () => s.Store(row, col, _selected)); }
        catch (SessionRejectedException e) { Hint(e.Message); return; } // the hint IS the answer
        Refresh();
    }

    private void TakeViaDoor(int row, int col)
    {
        var s = S; if (s is null) return;
        try { CommandLog.Call(StashPanelLogic.TakeLine(_selected, row, col), () => s.Take(_selected, row, col)); }
        catch (SessionRejectedException e) { Hint(e.Message); return; }
        Refresh();
    }

    private void Hint(string message) => OnHint?.Invoke(message);

    // ---- grid paint: anchors from the dump report, clicks as doors ----

    private Control BuildPack(GameSession s) =>
        BuildGrid("Carried pack", StashPanelLogic.CarriedItems(s.Dump()),
            StashPanelLogic.PackAnchorName, StashPanelLogic.PackFreeName, StoreViaDoor);

    private Control BuildTab(GameSession s) =>
        BuildGrid($"Stash tab {_selected}", StashPanelLogic.TabItems(s.Dump(), _selected),
            StashPanelLogic.TabAnchorName, StashPanelLogic.TabFreeName, TakeViaDoor);

    /// <summary>4×10 cells; the anchor cells (engine-reported) carry the
    /// thatch token, a clickable NODE NAME (the D2 lesson: names, never
    /// metadata), and a GuiInput that rides ONE logged door — every covered
    /// non-anchor cell of a multi-cell item paints thatch but is NOT
    /// clickable (the anchor is the door, InventoryPanel's declared rule);
    /// free cells are named so the leg hunts the PAINTED truth.</summary>
    private static Control BuildGrid(string title, List<(string Base, int Row, int Col)> items,
        System.Func<int, int, string> anchorName, System.Func<int, int, string> freeName,
        System.Action<int, int> onClick)
    {
        var box = new VBoxContainer();
        box.AddChild(new Label { Text = title });
        var occ = new HashSet<(int, int)>();
        var anchors = new HashSet<(int, int)>();
        foreach (var (baseId, row, col) in items)
        {
            anchors.Add((row, col));
            var w = GameContext.Session!.Bases.For(baseId).Width;
            var h = GameContext.Session!.Bases.For(baseId).Height;
            for (int r = row; r < row + h; r++)
                for (int c = col; c < col + w; c++)
                    occ.Add((r, c));
        }
        var grid = new GridContainer { Columns = InventoryGrid.Columns };
        for (int r = 0; r < InventoryGrid.Rows; r++)
            for (int c = 0; c < InventoryGrid.Columns; c++)
            {
                var cell = new ColorRect
                {
                    Color = occ.Contains((r, c)) ? new Color("7A5A2A") : new Color("2E2117"), // thatch / plain
                    CustomMinimumSize = new Vector2(40, 40),
                };
                int row = r, col = c; // the loop vars for the closure
                if (anchors.Contains((r, c)))
                {
                    cell.Name = anchorName(r, c); // the leg's hunt handle
                    cell.GuiInput += e =>
                    {
                        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                            onClick(row, col);
                    };
                }
                else if (!occ.Contains((r, c))) cell.Name = freeName(r, c);
                grid.AddChild(cell);
            }
        box.AddChild(grid);
        return box;
    }

    private static List<string> Names(GameSession s, List<(string Base, int Row, int Col)> items)
    {
        var names = new List<string>();
        foreach (var (baseId, _, _) in items) names.Add(s.Bases.For(baseId).Name);
        return names;
    }

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
