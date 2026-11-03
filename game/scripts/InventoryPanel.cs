using System.Collections.Generic;
using Godot;

/// <summary>
/// The 4×10 inventory grid + equipment panel, bound to session.Carried and
/// session.Equipment.Equipped (pull-only, never a 2nd session). The cells it
/// paints are the Sim's OWN answer to "which cell?" — the public dump
/// report's per-item row/col, pulled through InventoryPanelLogic.PackAnchors
/// exactly as the stash panel pulls its pack side (MC 11995, PRE-GA audit
/// P2 — this REPLACES the MC 11943 presentation-side re-derivation, which
/// answered the same question a second way and diverged from the engine
/// after any hole-leaving removal; see InventoryPanelLogic's header law).
/// Tab (toggled by Act) shows and hides it; refresh on show.
/// M5-GATE FIX (MC 11943, verdict D2): the cell map is a plain C# collection.
/// Handing the cell key "0,0" to Godot metadata threw ERROR "Invalid metadata
/// identifier" mid-_Ready and aborted Act.ToggleInventory BEFORE the visible
/// flip — the panel NEVER opened. Metadata identifiers must be valid Godot
/// identifiers; this panel keeps the mapping off-metadata entirely.
/// LAYOUT (the verdict-D1 mechanism, reused): the columns box lands MANUALLY
/// from Size on NOTIFICATION_RESIZED plus one settle frame (the DeathMenu/
/// TownPanel RESIZED-layout idiom) — the same dead center-preset shape that
/// painted the main menu at the corner never lands across a top-level boundary.
/// </summary>
public partial class InventoryPanel : Control
{
    private static readonly Color CellColor = new("2E2117");
    private static readonly Color CellEdge = new("4A3624");

    /// <summary>Wired by Act to the HUD hint line (the TownPanel/SessionClockNode
    /// OnHint idiom, MC 11952): an equip click the ENGINE refuses paints the
    /// engine's own words — never a silent swallow.</summary>
    public System.Action<string>? OnHint;

    private HBoxContainer? _columns; // built by Refresh, positioned by LayoutToSize

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; // the panel never eats the game's clicks
        VisibilityChanged += Refresh;
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (Size != Vector2.Zero) SetProcess(false); // settle frames are handled by RESIZED (the DeathMenu law)
        LayoutToSize();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutToSize();
    }

    private void LayoutToSize()
    {
        if (!IsNodeReady() || Size == Vector2.Zero || _columns is null) return; // never before _Ready (the D3 lesson)
        _columns.ResetSize();
        _columns.Position = new Vector2((Size.X - _columns.Size.X) / 2f, (Size.Y - _columns.Size.Y) / 2f);
    }

    /// <summary>The read-only count of what this panel's view is built from: the
    /// carried items — the panel's ONE read source, zero writes into the Sim.
    /// The headless trade-leg proof prints through it so the INV_COUNT lines
    /// come from the panel's own public read view.</summary>
    public static int ViewCount(BabaYaga.Sim.GameSession s) => s.Carried.Count;

    private void Refresh()
    {
        foreach (var child in GetChildren()) child.QueueFree();
        _columns = null;
        var s = GameContext.Session;
        if (s is null || !Visible) return;

        _columns = new HBoxContainer();
        AddChild(_columns);
        _columns.AddChild(BuildGrid(s));
        _columns.AddChild(BuildEquipment(s));
        LayoutToSize(); // a show-refresh at a landed rect centres at once; RESIZED re-centres on any resize
    }

    /// <summary>4×10 cells; the anchors painted are the session dump's OWN
    /// per-item row/col (InventoryPanelLogic.PackAnchors — the Sim's ONE
    /// answer to "which cell?", the StashPanelLogic pulled-read idiom reused,
    /// MC 11995; the geometry re-derivation this REPLACES is named dead in
    /// the logic file).
    /// The anchor map is a C# set of InventoryPanelLogic.AnchorKey strings — the
    /// D2 fix: this key is NOT a valid Godot metadata identifier and never goes
    /// to metadata again.
    /// CLICK-TO-WEAR (MC 11952, the declared control-scheme translation): D2
    /// equips via cursor DRAG; this clone is command-driven with NO cursor state
    /// (ARCH law), so a left-click on a PAINTED anchor cell rides the SAME
    /// logged door the CLI twin speaks — CommandLog.Call("equip &lt;x&gt; &lt;y&gt;",
    /// s.EquipFromCell) — every gate stays the engine's and its refusal arrives
    /// as the OnHint line (the TownPanel button law). Empty cells are named
    /// "free_r_c" so the evidence leg can hunt the PAINTED truth (an empty cell
    /// is never clickable — a phantom click is worse than no click; since
    /// MC 11995 painted truth and engine truth are the SAME answer, so a
    /// painted anchor can no longer name a cell the door refuses).</summary>
    private Control BuildGrid(BabaYaga.Sim.GameSession s)
    {
        var anchors = new HashSet<string>();
        foreach (var (row, col) in InventoryPanelLogic.PackAnchors(s.Dump()))
            anchors.Add(InventoryPanelLogic.AnchorKey(row, col));
        var grid = new GridContainer { Columns = BabaYaga.Sim.InventoryGrid.Columns };

        for (int r = 0; r < BabaYaga.Sim.InventoryGrid.Rows; r++)
            for (int c = 0; c < BabaYaga.Sim.InventoryGrid.Columns; c++)
            {
                var cell = new ColorRect
                {
                    Color = CellColor,
                    CustomMinimumSize = new Vector2(48, 48),
                };
                int row = r, col = c; // the loop vars for the closure (C# loops capture per-iteration anyway — pinned by clarity)
                if (anchors.Contains(InventoryPanelLogic.AnchorKey(r, c)))
                {
                    cell.Color = new Color("7A5A2A"); // thatch token: item sits here
                    cell.Name = InventoryPanelLogic.AnchorNodeName(r, c);
                    cell.GuiInput += e =>
                    {
                        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                            EquipViaDoor(row, col);
                    };
                }
                else cell.Name = $"free_{r}_{c}";
                grid.AddChild(cell);
            }
        return grid;
    }

    /// <summary>One anchor-cell click = one LOGGED door line (the TownPanel
    /// button idiom): the grammar string IS what replays; the engine's refusal
    /// paints itself as a hint; success refreshes the paint (the worn cell
    /// empties, the Worn list grows — the evidence pair).</summary>
    private void EquipViaDoor(int row, int col)
    {
        var s = GameContext.Session; if (s is null) return;
        try
        {
            CommandLog.Call(InventoryPanelLogic.EquipCellLine(row, col), () => s.EquipFromCell(row, col));
        }
        catch (BabaYaga.Sim.SessionRejectedException e) { OnHint?.Invoke(e.Message); return; } // the hint IS the answer
        Refresh();
    }

    private Control BuildEquipment(BabaYaga.Sim.GameSession s)
    {
        var box = new VBoxContainer();
        box.AddChild(new Label { Text = "Worn" });
        foreach (var (slot, item) in s.Equipment.Equipped)
            box.AddChild(new Label { Text = $"{slot}: {item.DisplayName} ({item.Quality})" });
        foreach (var item in s.Carried)
            box.AddChild(new Label { Text = $"{item.DisplayName} ({item.Quality}) {item.Base.Width}×{item.Base.Height}" });
        return box;
    }
}
