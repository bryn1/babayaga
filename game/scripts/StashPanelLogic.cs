using System.Collections.Generic;
using System.Text.Json.Nodes;
using BabaYaga.Sim;

/// <summary>
/// M5 STASH CHEST PANEL, the mapping core (MC 11953, LEDGER row 75 chest half;
/// the TownPanelLogic precedent): the pure rule behind StashPanel. Every door
/// click maps to ONE CLI twin verb line (the BABA_LOG grammar is Scripted-
/// Session's — "stash_store &lt;tab&gt; &lt;x&gt; &lt;y&gt;" / "stash_take &lt;tab&gt;
/// &lt;x&gt; &lt;y&gt;", tab-FIRST even though the engine door takes
/// (invX, invY, tab) — StashPanelTests replays the table LIVE through the real
/// CommandRunner so it can never drift). The town gate is a pulled-key
/// equality (the panel is inert outside grodzisko; the ENGINE's own DEAD→town
/// gates on Store/Take stay the authority behind it — no re-implementation).
/// The LOCKED-TAB law is the brief's, honest and never bypassed: a locked tab
/// is PAINTED as locked (LockedTabLabel), clicking it calls NO door (selection
/// is not a sim verb) and hands back the engine's own refusal wording
/// (LockedTabHint — pinned BYTE-EQUAL to the real Store-refusal by a test row,
/// so the widget copy can never drift into a lie), and SelectTab keeps the
/// current tab across the refusal. Lock truth is ONE pulled door read:
/// IsStashTabUnlocked (tab 2 opens via glowa's StashTab reward, Stash.cs).
/// The grids' paint is a PULLED read of the public dump report — the CLI
/// `dump` line IS the ONE place the Sim answers "which cell?" (ARCH v8); the
/// panel re-derives nothing and invents no second placement answer.
/// PURE (no Godot types) — xunit-covered (tests/StashPanelTests.cs).
/// </summary>
public static class StashPanelLogic
{
    /// <summary>The one CLI twin line a pack-anchor click rides through
    /// CommandLog.Call — tab-first, the EngineDoorVerbs grammar verbatim.</summary>
    public static string StoreLine(int tab, int row, int col) => $"stash_store {tab} {row} {col}";

    /// <summary>The one CLI twin line a tab-anchor click rides (same grammar,
    /// x/y read as TAB cells there — the CLI passes them to Take(tab, x, y)).</summary>
    public static string TakeLine(int tab, int row, int col) => $"stash_take {tab} {row} {col}";

    /// <summary>One label builder per button (the DeathMenuLogic label idiom,
    /// the TownPanelLogic twin): the panel paints and the leg hunts with the
    /// SAME string — no duplicated literal.</summary>
    public static string TabButtonLabel(int tab) => $"TAB {tab}";

    /// <summary>The LOCKED variant — the lock is PAINTED, never hidden: the
    /// brief's "show locked state honestly, NEVER bypass".</summary>
    public static string LockedTabLabel(int tab) => $"TAB {tab} (locked)";

    /// <summary>Anchor-cell NODE NAMES (the leg's hunt handles, the D2 lesson
    /// generalized: cell keys ride node NAMES, valid Godot identifiers, NEVER
    /// metadata) — the panel paints and the leg hunts with the SAME builder.</summary>
    public static string PackAnchorName(int row, int col) => $"pack_{row}_{col}";
    public static string TabAnchorName(int row, int col) => $"tab_{row}_{col}";

    /// <summary>Free-cell names are the painted truth the leg can NEGOTIATE
    /// with (InventoryPanel's "free_r_c" idiom): an empty cell is never
    /// clickable — a phantom click is worse than no click.</summary>
    public static string PackFreeName(int row, int col) => $"pack_free_{row}_{col}";
    public static string TabFreeName(int row, int col) => $"tab_free_{row}_{col}";

    /// <summary>Which tab buttons stand: every paid tab plus the locked next
    /// one (the README's truth row: "tab 1 + reward-unlocked tab 2"). The
    /// LOCKED state itself is pulled per tab from IsStashTabUnlocked — this
    /// list only says how many buttons paint.</summary>
    public static int[] TabsShown(int stashTabs)
    {
        var tabs = new List<int>();
        for (int t = 1; t <= System.Math.Max(2, stashTabs + 1); t++) tabs.Add(t);
        return [.. tabs];
    }

    /// <summary>Town gate: the chest opens exactly ON the pulled town key
    /// (grodzisko) — the same one the death beat resurrects into.</summary>
    public static bool OpensFor(string areaKey, string townKey) => areaKey == townKey;

    /// <summary>Locked or open? The ONE pulled door read, never a copy.</summary>
    public static bool LockedShown(GameSession s, int tab) => !s.IsStashTabUnlocked(tab);

    /// <summary>The tab-pick rule: an unlocked pick switches; a locked pick
    /// KEEPS the current tab and returns the named hint (the NEVER-BYPASS
    /// row). The hint rides back for the caller to paint — selection is not a
    /// sim verb, so no door call and no log line belongs here.</summary>
    public static (int Tab, string? Hint) SelectTab(int current, int tab, bool unlocked) =>
        unlocked ? (tab, null) : (current, LockedTabHint(tab));

    /// <summary>The locked-click hint — BYTE-PINNED to the engine's own refusal
    /// (StashPanelTests compares it against the real Store-refusal message,
    /// GameSession.Stash.cs). Drift here is a RED test, never a silent lie.</summary>
    public static string LockedTabHint(int tab) =>
        $"stash tab {tab} is locked — rewards open further tabs";

    /// <summary>The pack cells the panel paints — the dump report's OWN anchors
    /// (base + engine row/col, pulled; Row/Col −1 entries, the pending-drop
    /// shape, never paint). The click hands these exact cells to the door, so
    /// a painted anchor can never name a cell the engine disagrees with.</summary>
    public static List<(string Base, int Row, int Col)> CarriedItems(string dumpJson) =>
        ItemsFrom(JsonNode.Parse(dumpJson)?["carried"]?.AsArray());

    /// <summary>One tab's stowed items, pulled from the dump report's "stash"
    /// array (the v6 save law: opened-but-EMPTY tabs are not saved, so a
    /// missing entry is simply an empty paint). The headless StashQuestLeg
    /// reads its anchors from the command reports; this is the same truth in
    /// its dump shape.</summary>
    public static List<(string Base, int Row, int Col)> TabItems(string dumpJson, int tab)
    {
        var list = new List<(string, int, int)>();
        var tabs = JsonNode.Parse(dumpJson)?["stash"]?.AsArray();
        if (tabs is null) return list;
        foreach (var t in tabs)
            if (t?["tab"]?.GetValue<int>() == tab)
                return ItemsFrom(t["items"]?.AsArray());
        return list;
    }

    private static List<(string Base, int Row, int Col)> ItemsFrom(JsonArray? items)
    {
        var list = new List<(string, int, int)>();
        if (items is null) return list;
        foreach (var i in items)
        {
            var baseId = i?["base"]?.GetValue<string>();
            int row = i?["row"]?.GetValue<int>() ?? -1, col = i?["col"]?.GetValue<int>() ?? -1;
            if (baseId is not null && row >= 0 && col >= 0) list.Add((baseId, row, col));
        }
        return list;
    }
}
