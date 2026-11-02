using BabaYaga.Sim;
using Godot;

/// <summary>
/// M5-S6 TOWN PANEL (MC 11940, plan-c2 S6 row; P7 vendor + P8 captain): the
/// grodzisko overlay — Vesna's stock and the captain's chain, instantiated
/// OVER Act on its own CanvasLayer (the DeathMenu lesson: a Control under the
/// Node2D world pans with the camera and dodges the GUI clicks) and laid out
/// MANUALLY from Size on every NOTIFICATION_RESIZED (the RESIZED-layout law —
/// anchors are not trusted across the top-level CanvasLayer boundary).
/// Pull-only display (s.Bases / s.Hirelings / Vendor.BuyPrice / the Merc line
/// — never a second session, never a rule computed here); every button rides
/// ONE logged door through CommandLog.Call (TownPanelLogic's table — the panel
/// clicks ARE the replayable session); every engine refusal becomes a HUD hint
/// (the HeroController idiom), never a swallow. The engine's gates stay the
/// engine's; the belt-slot pick is the presentation-side choice pinned in
/// TownPanelLogic.
/// </summary>
public partial class TownPanel : Control
{
    /// <summary>Wired by Act to the HUD hint line — the engine's own words,
    /// painted (the SessionClockNode OnHint idiom).</summary>
    public System.Action<string>? OnHint;

    private ColorRect _dim = null!;
    private VBoxContainer _box = null!;

    private static GameSession? S => GameContext.Session;

    public override void _Ready()
    {
        _dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f) }; // dusk dim; the HUD lines stay readable through it (the [D] read pairs ride it)
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

    /// <summary>Below the HUD panel's bottom (~300px at any judged size clears
    /// its rect — the S4 leg's law: the HUD PanelContainer eats every click
    /// under it, and a row at the HUD's height is a row nobody can buy from),
    /// centred across. The box sizes itself; nothing anchors here.</summary>
    private void LayoutToSize()
    {
        // D3 sibling-guard (MC 11943): the same pre-ready RESIZED window threw
        // the identical NRE here (s6-xvfb.log) — one guard at the shared
        // idiom, both files. Reject the pre-_Ready pass; layout rides the
        // settle frame / later RESIZED.
        if (!IsNodeReady() || Size == Vector2.Zero) return;
        _dim.Position = Vector2.Zero;
        _dim.Size = Size;
        _box.ResetSize();
        _box.Position = new Vector2((Size.X - _box.Size.X) / 2f,
            Mathf.Max(Size.Y * 0.42f, 300f));
    }

    /// <summary>Rebuilds the painted rows from the session's own tables (the
    /// InventoryPanel rebuild-on-show idiom) — pull-only, zero writes.</summary>
    private void Refresh()
    {
        foreach (var child in _box.GetChildren()) child.QueueFree();
        var s = S;
        if (s is null || !Visible) return;

        _box.AddChild(Header($"Vesna the Pedlar — Gold {s.Gold}")); // the purse read, pulled
        foreach (var id in TownPanelLogic.StockBaseIds)
        {
            var row = s.Bases.For(id);
            long price = Vendor.BuyPrice(row, Vendor.Standard); // the engine's own price math
            _box.AddChild(Row(new Label { Text = $"{row.Name} ({row.Width}x{row.Height})" },
                Button(TownPanelLogic.BuyLabel(row.Name, price), () => Buy(id, row.Consumable))));
        }

        _box.AddChild(Header("The captain"));
        var merc = s.Merc;
        if (merc is null)
        {
            foreach (var hrow in s.Hirelings.Rows)
                _box.AddChild(Row(new Label { Text = $"{hrow.Name} — act {hrow.Act}" },
                    Button(TownPanelLogic.HireLabel(hrow.Name, hrow.HireGold), () => Hire(hrow.Id))));
        }
        else
        {
            _box.AddChild(Row(
                new Label { Text = $"{merc.Name} — Life {merc.Life}/{merc.MaxLife} (lvl {merc.Level})" },
                Button(TownPanelLogic.ReviveLabel(merc.Name), () => Revive(merc.Id)),
                Button(TownPanelLogic.DismissButton, Dismiss)));
        }
    }

    // ---- the five doors: one logged line per click (TownPanelLogic's table) ----

    private void Buy(string baseId, bool consumable)
    {
        var s = S; if (s is null) return;
        try { CommandLog.Call(TownPanelLogic.BuyLine(baseId), () => s.Buy(baseId)); }
        catch (SessionRejectedException e) { Hint(e.Message); return; } // the hint IS the answer
        // The bought copy lands at the pack tail (TryCarry appends — the Sim's
        // order, read here, never assumed elsewhere): a consumable the buyer
        // just paid for rides to the belt — THE windowed AddToBelt caller the
        // plan's P7 names. On a full belt the engine's refusal is the hint.
        if (consumable)
        {
            int idx = s.Carried.Count - 1, slot = TownPanelLogic.BeltSlotFor(s.Belt);
            try { CommandLog.Call(TownPanelLogic.BeltAddLine(idx, slot), () => s.AddToBelt(idx, slot)); }
            catch (SessionRejectedException e) { Hint(e.Message); return; }
        }
        Refresh();
    }

    private void Hire(string id)
    {
        var s = S; if (s is null) return;
        try { CommandLog.Call(TownPanelLogic.HireLine(id), () => s.Hire(id)); }
        catch (SessionRejectedException e) { Hint(e.Message); return; }
        Refresh(); // the merc line replaces the offer rows (the one-merc truth, pulled)
    }

    private void Revive(string id)
    {
        var s = S; if (s is null) return;
        try { CommandLog.Call(TownPanelLogic.ReviveLine(id), () => s.Revive(id)); }
        catch (SessionRejectedException e) { Hint(e.Message); return; }
        Refresh();
    }

    private void Dismiss()
    {
        var s = S; if (s is null) return;
        try { CommandLog.Call(TownPanelLogic.DismissLine, () => s.Dismiss()); }
        catch (SessionRejectedException e) { Hint(e.Message); return; }
        Refresh();
    }

    private void Hint(string message) => OnHint?.Invoke(message);

    // ---- tiny paint helpers ----

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
