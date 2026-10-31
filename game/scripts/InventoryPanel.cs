using Godot;

/// <summary>
/// The 4×10 inventory grid + equipment panel, bound to session.Carried and
/// session.Equipment.Equipped (pull-only, never a 2nd session). The Sim never
/// reports per-cell placement back (CarriedSpots is INTERNAL by design — ARCH v8),
/// so the panel re-derives cells presentation-side: first-fit over the grid in
/// Carried order. Tab (toggled by Act) shows and hides it; refresh on show.
/// </summary>
public partial class InventoryPanel : Control
{
    private static readonly Color CellColor = new("2E2117");
    private static readonly Color CellEdge = new("4A3624");

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; // the panel never eats the game's clicks
        VisibilityChanged += Refresh;
        Refresh();
    }

    /// <summary>The read-only count of what this panel's view is built from: the
    /// carried items — the panel's ONE read source, zero writes into the Sim.
    /// The headless trade-leg proof prints through it so the INV_COUNT lines
    /// come from the panel's own public read view.</summary>
    public static int ViewCount(BabaYaga.Sim.GameSession s) => s.Carried.Count;

    private void Refresh()
    {
        foreach (var child in GetChildren()) child.QueueFree();
        var s = GameContext.Session;
        if (s is null || !Visible) return;

        var columns = new HBoxContainer
        {
            AnchorsPreset = (int)LayoutPreset.Center,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
        };
        AddChild(columns);
        columns.AddChild(BuildGrid(s));
        columns.AddChild(BuildEquipment(s));
    }

    /// <summary>4×10 cells; items drawn first-fit in Carried order (cells are a
    /// presentation-side re-derivation — the Sim owns no answer to "which cell?").</summary>
    private Control BuildGrid(BabaYaga.Sim.GameSession s)
    {
        var occ = new bool[BabaYaga.Sim.InventoryGrid.Rows, BabaYaga.Sim.InventoryGrid.Columns];
        var grid = new GridContainer { Columns = BabaYaga.Sim.InventoryGrid.Columns };
        foreach (var item in s.Carried)
            if (TryDerive(occ, item.Base.Width, item.Base.Height, out var spot))
                grid.SetMeta(spot, item.DisplayName);

        for (int r = 0; r < BabaYaga.Sim.InventoryGrid.Rows; r++)
            for (int c = 0; c < BabaYaga.Sim.InventoryGrid.Columns; c++)
            {
                var cell = new ColorRect
                {
                    Color = CellColor,
                    CustomMinimumSize = new Vector2(48, 48),
                };
                if (grid.HasMeta($"{r},{c}")) cell.Color = new Color("7A5A2A"); // thatch token: item sits here
                grid.AddChild(cell);
            }
        return grid;
    }

    private static bool TryDerive(bool[,] occ, int w, int h, out string spot)
    {
        for (int r = 0; r <= occ.GetLength(0) - h; r++)
            for (int c = 0; c <= occ.GetLength(1) - w; c++)
            {
                bool fits = true;
                for (int i = 0; i < h && fits; i++)
                    for (int j = 0; j < w && fits; j++)
                        fits = !occ[r + i, c + j];
                if (!fits) continue;
                for (int i = 0; i < h; i++)
                    for (int j = 0; j < w; j++)
                        occ[r + i, c + j] = true;
                spot = $"{r},{c}";
                return true;
            }
        spot = "";
        return false;
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
