/// <summary>
/// M5-GATE FIX (MC 11943, verdict D2): the cell re-derivation the inventory
/// panel rides, extracted beside the tests (the DeathMenuLogic/TownPanelLogic
/// pure-file idiom — no Godot types, xunit-covered). The Sim reports no
/// per-cell placement back (CarriedSpots is INTERNAL by design, ARCH v8), so
/// the panel first-fits items over the grid in Carried order. The mapping
/// lives in a plain C# collection keyed by AnchorKey — NEVER on Godot
/// metadata: metadata identifiers must be valid Godot identifiers, and the
/// cell key "0,0" is not one (ERROR "Invalid metadata identifier: '0,0'"
/// thrown mid-ToggleInventory — the panel never opened; verdict D2).
/// </summary>
public static class InventoryPanelLogic
{
    /// <summary>The cell-map key for one grid anchor — the panel's OWN string,
    /// never handed to SetMeta/HasMeta (see the header's D2 note).</summary>
    public static string AnchorKey(int row, int col) => $"{row},{col}";

    /// <summary>First-fit a w×h item into the occupancy grid (rows = the
    /// grid's Rows, cols = its Columns); marks every covered cell occupied
    /// and returns the ANCHOR key (the top-left cell the panel paints with
    /// the thatch token). False when nothing fits — the item then rides the
    /// equipment list only, never a phantom cell.</summary>
    public static bool TryDeriveSpot(bool[,] occ, int w, int h, out string spot)
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
                spot = AnchorKey(r, c);
                return true;
            }
        spot = "";
        return false;
    }
}
