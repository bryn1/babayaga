using System.Collections.Generic;

/// <summary>
/// The inventory panel's PURE mapping core (the DeathMenuLogic/TownPanelLogic
/// pure-file idiom — no Godot types, xunit-covered beside the tests at
/// tests/InventoryPanelDumpTruthTests.cs).
/// DUMP-TRUTH LAW (MC 11995, PRE-GA audit P2, SUPERSEDES the MC 11943 header
/// claim "the Sim reports no per-cell placement back"): the dump report the
/// session already answers with DOES carry each carried item's row/col
/// (SaveSerializer → CarriedSpots), and the panel PAINTS EXACTLY THAT one
/// answer — PackAnchors pulls it. The paint-time first-fit re-derivation the
/// panel used to run answered the same question a second way and diverged
/// from the engine after any hole-leaving removal (painted anchor refused
/// "the pack holds no item anchored at cell r,c", the true anchor painted
/// non-clickable, other geometries a wrong-item equip — the audit's B1,
/// reproduced RED at out/leg-m5-inv/base-divergence.txt).
/// The D2 lesson stands unchanged: cell keys live in plain C# collections,
/// NEVER on Godot metadata — metadata identifiers must be valid Godot
/// identifiers and the key "0,0" is not one (ERROR "Invalid metadata
/// identifier: '0,0'" — the panel never opened; MC 11943 verdict D2).
/// </summary>
public static class InventoryPanelLogic
{
    /// <summary>The cell-map key for one grid anchor — the panel's OWN string,
    /// never handed to SetMeta/HasMeta (see the header's D2 note).</summary>
    public static string AnchorKey(int row, int col) => $"{row},{col}";

    /// <summary>The anchor cell's NODE NAME (the leg's hunt handle, MC 11952):
    /// the underscore form because a node name must be a valid Godot
    /// identifier too — the same D2 rule that banned "0,0" from metadata
    /// bans it here; the pair (row,col) never goes to metadata.</summary>
    public static string AnchorNodeName(int row, int col) => $"cell_{row}_{col}";

    /// <summary>The CLI twin line an anchor-cell click rides through
    /// CommandLog.Call (MC 11952, the TownPanelLogic door-line idiom): the
    /// painted cell IS the grammar line "equip &lt;x&gt; &lt;y&gt;" the engine door
    /// EquipFromCell answers — cells are the Sim's truth, painted FROM its
    /// dump and clicked back INTO the same door (MC 11995).</summary>
    public static string EquipCellLine(int row, int col) => $"equip {row} {col}";

    /// <summary>The cells the panel PAINTS as anchors — the session dump
    /// report's OWN per-item row/col (MC 11995: the ONE place the Sim answers
    /// "which cell?", the same pulled read the stash panel's pack side paints
    /// from, StashPanelLogic "the CLI `dump` line IS the ONE place"). Reuses
    /// that ONE pull rather than a second parse (owner doctrine: one
    /// implementation per concern); pending-row entries (row/col −1) never
    /// paint. A painted anchor can therefore never name a cell the engine
    /// disagrees with, and a hole-leaving removal repaints the survivor at
    /// its TRUE cell (first-fit never compacts).</summary>
    public static List<(int Row, int Col)> PackAnchors(string dumpJson)
    {
        var anchors = new List<(int Row, int Col)>();
        foreach (var (_, row, col) in StashPanelLogic.CarriedItems(dumpJson))
            anchors.Add((row, col));
        return anchors;
    }

    /// <summary>First-fit a w×h item into the occupancy grid (rows = the
    /// grid's Rows, cols = its Columns); marks every covered cell occupied
    /// and returns the ANCHOR key. False when nothing fits.
    /// MC 11995 — THIS IS NO LONGER THE PANEL'S PAINT: the panel paints from
    /// PackAnchors (the dump's row/col, header law). The function stays ONLY
    /// because the sealed M5GateFixTests rows D2_Derive_FirstFitOrder and
    /// D2_Derive_NoRoom (tests/M5GateFixTests.cs:64-85) pin it and that file
    /// is READ-ONLY to this card; its deletion needs the orchestrator's seam
    /// ruling (re-point or retire those two rows first). No shipped panel
    /// code may call it — tests/InventoryPanelDumpTruthTests.cs scans that.</summary>
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
