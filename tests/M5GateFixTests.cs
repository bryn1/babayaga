using System;
using System.IO;
using System.Linq;
using BabaYaga.Sim;
using Xunit;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// M5-GATE FIX (MC 11943, verdict D1/D2/D3): the three presentation defects
/// the S7 windowed gate named, pinned RED-first. D1 and D3 live behind Godot
/// types the engine-process-free suite cannot reach — those ride the shipped-
/// file SCAN idiom (TownPanelTests/GameInputTests twins): the fix SHAPE is the
/// contract the gate read pixel-perfect, so the scan pins it. D2's cell
/// derivation is pure (InventoryPanelLogic) and rides direct rows; the meta
/// key that killed the panel rides a validity row + a scan row.
/// </summary>
public class M5GateFixTests
{
    // ---- D2: the cell map is a plain C# key, never a metadata identifier ----

    /// <summary>Godot's metadata-identifier rule (the rule that threw
    /// "Invalid metadata identifier: '0,0'"): letters/digits/underscore, not
    /// starting with a digit, non-empty.</summary>
    private static bool IsGodotMetaIdentifier(string s) =>
        s.Length > 0 && (char.IsLetter(s[0]) || s[0] == '_') &&
        s.All(ch => char.IsLetterOrDigit(ch) || ch == '_');

    [Fact]
    public void D2_CellAnchorKey_IsExactlyTheString_TheOldMetaCallRejected()
    {
        // The map key keeps its shape (the panel's own string, the painted
        // anchor); the D2 root is that this string is NOT a valid metadata
        // identifier — which is why it lives in a C# collection, never on a
        // Godot object. If AnchorKey ever becomes identifier-shaped the map
        // could migrate to meta again and this row says so; today it must
        // stay FALSE (pinning the exact rejected key).
        Assert.Equal("0,0", InventoryPanelLogic.AnchorKey(0, 0));
        Assert.False(IsGodotMetaIdentifier(InventoryPanelLogic.AnchorKey(0, 0)));
        Assert.True(IsGodotMetaIdentifier("cell_0_0")); // the would-be valid form, for contrast
    }

    [Fact]
    public void D2_ShippedPanel_HandsNothingToMetadata()
    {
        // RED first: the gate's tree still called grid.SetMeta(spot, …) with
        // the "0,0" key and the panel NEVER opened. The fixed panel must
        // carry no SetMeta/HasMeta/GetMeta call at all (code lines only —
        // the doc header may name the rejected mechanism).
        foreach (var line in ShippedLines("InventoryPanel.cs"))
            Assert.DoesNotMatch(@"\b(SetMeta|HasMeta|GetMeta)\s*\(", line);
    }

    [Fact]
    public void D2_ShippedPanel_StillRefusesCellClicks()
    {
        // Existing behavior preserved: the panel never eats the game's
        // clicks (MouseFilter Ignore) — the click-rejection the P6 row rode.
        var code = string.Join('\n', ShippedLines("InventoryPanel.cs"));
        Assert.Contains("MouseFilter = MouseFilterEnum.Ignore", code);
    }

    [Fact]
    public void D2_Derive_FirstFitOrder_MarksFullFootprint_AnchorsTopLeft()
    {
        var occ = new bool[InventoryGrid.Rows, InventoryGrid.Columns];
        Assert.True(InventoryPanelLogic.TryDeriveSpot(occ, 1, 1, out var a));
        Assert.Equal("0,0", a);
        Assert.True(InventoryPanelLogic.TryDeriveSpot(occ, 2, 1, out var b)); // next free 2-wide
        Assert.Equal("0,1", b);
        Assert.True(InventoryPanelLogic.TryDeriveSpot(occ, 2, 2, out var c)); // 2×2: row 0 has no 2-wide-free block before col 3
        Assert.Equal("0,3", c); // first-fit scans r-major: the free (0,3) block wins over (1,0)
        Assert.True(occ[1, 4]); // the FULL footprint is occupied, not just the anchor
    }

    [Fact]
    public void D2_Derive_NoRoom_ReturnsFalse_EmptySpot()
    {
        var occ = new bool[InventoryGrid.Rows, InventoryGrid.Columns];
        for (int r = 0; r < InventoryGrid.Rows; r++)
            for (int c = 0; c < InventoryGrid.Columns; c++)
                Assert.True(InventoryPanelLogic.TryDeriveSpot(occ, 1, 1, out _));
        Assert.False(InventoryPanelLogic.TryDeriveSpot(occ, 1, 1, out var spot));
        Assert.Equal("", spot); // no phantom cell for an item that does not fit
    }

    [Fact]
    public void D2_ToggleInventoryDoor_OpensFreshThroughACanvasLayer()
    {
        // D2's door half (RED after the meta fix, twice over): (1) a fresh
        // Instantiate() is ALREADY visible, so the fresh branch must RETURN
        // (the OPEN is done) instead of falling into the flip that hides it,
        // and (2) the panel rides a CanvasLayer of its own — a bare Control
        // under Act sits on the world canvas and the camera pans it off-screen
        // (the gate's XVFB lesson). Both are the sibling-door mechanisms
        // ToggleDeathMenu/ToggleTownPanel already carry in this same file.
        var lines = ShippedFile("Act.cs");
        int door = Array.FindIndex(lines, l => l.Contains("private void ToggleInventory"));
        Assert.True(door >= 0, "Act.cs: no ToggleInventory door");
        var body = string.Join('\n', lines.Skip(door).Take(20));
        Assert.Matches("new CanvasLayer", body); // screen-fixed, camera cannot pan it
        int add = Array.FindIndex(lines, door, 20, l => l.Contains("AddChild(_panel)"));
        Assert.True(add >= 0 && add < door + 20, "Act.cs: fresh branch never AddChilds");
        // fresh AddChild must be followed by the door's early return — the flip
        // may only run on a LATER press (sibling door idiom).
        Assert.Matches(@"^\s*return;", lines[add + 1]);
    }

    // ---- D1: the menu lays out MANUALLY on RESIZED (anchors never landed) ----

    [Fact]
    public void D1_ShippedMenu_UsesTheResizedLayoutIdiom_NotCenterAnchors()
    {
        // kb'd Godot 4.7 truth (verdict D1): a top-level Control lands its
        // rect AFTER _Ready — AnchorsPreset=Center never lands (the title
        // clipped above the top edge, buttons half off-screen). The fix is
        // the in-tree DeathMenu/TownPanel idiom, and the scan pins its shape.
        var all = string.Join('\n', ShippedFile("MainMenu.cs"));
        Assert.Contains("NotificationResized", all);
        Assert.Contains("LayoutToSize", all);
        Assert.DoesNotContain("AnchorsPreset", all);
        Assert.DoesNotContain("GrowHorizontal", all); // the grow-from-corner half of the dead shape
    }

    // ---- D3: LayoutToSize never runs before the widget's fields exist ----

    [Theory]
    [InlineData("DeathMenu.cs")] // the verdict's D3 (s4-xvfb-newgame.log NRE)
    [InlineData("TownPanel.cs")] // the sibling with the SAME pre-ready NRE in s6-xvfb.log
    public void D3_ResizeLayout_GuardedBeforeAnyFieldUse(string file)
    {
        // The throw's root: NOTIFICATION_RESIZED can fire BEFORE _Ready
        // assigned the fields — the guard must reject that pass (IsNodeReady)
        // before touching a field, and keep the settle/resize layout itself.
        var lines = ShippedFile(file);
        int body = Array.FindIndex(lines, l => l.Contains("void LayoutToSize"));
        Assert.True(body >= 0, $"{file}: no LayoutToSize to guard");
        var guard = Array.FindIndex(lines, body, lines.Length - body, l => l.Contains("IsNodeReady"));
        var fieldUse = Array.FindIndex(lines, body, lines.Length - body, l => l.Contains("_dim."));
        Assert.True(guard >= 0, $"{file}: LayoutToSize has no ready-guard");
        Assert.True(fieldUse < 0 || guard < fieldUse,
            $"{file}: LayoutToSize touches fields before the ready-guard (pre-ready NRE window)");
    }

    // ---- shipped-file scan helpers (the TownPanelTests ScriptsDir idiom) ----

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game", "scripts")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static string[] ShippedFile(string name) =>
        File.ReadAllLines(Path.Combine(RepoRoot(), "game", "scripts", name));

    /// <summary>Code lines only: comments (both // and ///) may NAME a
    /// rejected mechanism — the scan asserts no code USES it.</summary>
    private static string[] ShippedLines(string name) =>
        ShippedFile(name).Where(l => !l.TrimStart().StartsWith("//")).ToArray();
}
