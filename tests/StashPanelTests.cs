using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BabaYaga.Sim;
using BabaYaga.Sim.Cli;
using Xunit;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// M5 TOWN-STASH CHEST PANEL (MC 11953, row-75 chest half): the pure decision
/// core the windowed StashPanel rides — StashPanelLogic — exercised WITHOUT
/// Godot (the TownPanelLogic/DeathMenuLogic pure-file idiom). The four DoD
/// row groups: (1) the action→door MAPPING TABLE — the exact CLI twin verb
/// lines (the BABA_LOG grammar of EngineDoorVerbs stash_store/stash_take, tab-
/// first) the panel emits through CommandLog.Call, one run replayed LIVE
/// through the real CommandRunner so the table cannot drift from the grammar;
/// (2) the TOWN GATE — the panel is inert outside the pulled town key (the
/// ENGINE's own town gates on Store/Take stay the authority behind the widget
/// gate, never re-implemented); (3) the LOCKED-TAB law — a locked tab refuses
/// BY NAME (the engine's own words), the panel shows the locked state honestly
/// and NEVER bypasses: SelectTab refuses a locked pick, and the widget-side
/// hint string is pinned byte-equal to the real engine refusal so it can
/// never drift into a lie; (4) the STORE/TAKE mapping + ECHO — the CLI twin
/// lines echo the session's own StashMoveReport anchors (BASE/ITEM/TAB/
/// TAB_ROW/TAB_COL/INV_ROW/INV_COL) and the paint source (the public dump read)
/// shows the tab row appear on store and the pack refill on take.
/// Scan rows (the TownPanelTests twins): every StashPanel door call rides
/// CommandLog.Call, every refusal catch paints e.Message (never a swallow),
/// and the windowed driver leg calls NO session verb and writes NO state.
/// </summary>
public class StashPanelTests
{
    private static GameSession Fresh(ulong seed = 7) => GameSession.New(seed, "koldun", TestData.DataDir());

    /// BeltStashTests idiom — the FSM's own gates carry the rites to glowa's tab 2.
    private static void UnlockGlowa(GameSession s)
    {
        foreach (var key in new[] { "gesi", "wrzeciono", "dary", "glowa" })
        {
            s.QuestAccept(key);
            s.QuestStep(key);
            s.QuestComplete(key);
        }
    }

    // ---- 1. the action→door mapping table (the exact CLI twin verb lines) ----

    [Fact]
    public void MappingTable_EmitsTheCliTwinVerbLines()
    {
        // Grammar law (ScriptedSession/EngineDoorVerbs): the stash verbs read
        // TAB-FIRST — "stash_store <tab> <x> <y>" — even though the engine door
        // takes (invX, invY, tab). The table's strings ARE the grammar.
        Assert.Equal("stash_store 1 0 0", StashPanelLogic.StoreLine(1, 0, 0));
        Assert.Equal("stash_store 2 0 1", StashPanelLogic.StoreLine(2, 0, 1));
        Assert.Equal("stash_store 1 3 9", StashPanelLogic.StoreLine(1, 3, 9));
        Assert.Equal("stash_take 1 0 0", StashPanelLogic.TakeLine(1, 0, 0));
        Assert.Equal("stash_take 2 1 2", StashPanelLogic.TakeLine(2, 1, 2));
    }

    [Fact]
    public void MappingTable_LinesReplayLive_ThroughTheRealCli()
    {
        // The exact lines a chest-panel session emits (buy, store, take, dump)
        // run through the real CommandRunner at a fixed seed with NO ERROR line
        // — a rejected or mis-shaped line exits nonzero.
        var lines = new[]
        {
            "new koldun", // the CLI head (GameContext.New synthesizes it in-game)
            "buy soluga",
            StashPanelLogic.StoreLine(1, 0, 0), // the pack anchor the panel paints
            StashPanelLogic.TakeLine(1, 0, 0),  // the tab cell the panel paints
            "dump",
        };
        var path = Path.Combine(Path.GetTempPath(), $"stashpanel-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, string.Join('\n', lines) + '\n');
        try
        {
            var output = new StringWriter();
            int exit = CommandRunner.Run(path, 7, TestData.DataDir(), output);
            Assert.Equal(0, exit);
            Assert.DoesNotContain(output.ToString().Split('\n'), l => l.Contains("\"ERROR\""));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TabLabels_ComeFromOneBuilder_TheDriverHuntsWith()
    {
        // One builder per button (the DeathMenuLogic label idiom): the panel
        // paints and the leg hunts with the SAME string — no duplicated literal.
        // The LOCKED variant is its own builder: the locked state is PAINTED,
        // never hidden (the brief's "show locked state honestly").
        Assert.Equal("TAB 1", StashPanelLogic.TabButtonLabel(1));
        Assert.Equal("TAB 2", StashPanelLogic.TabButtonLabel(2));
        Assert.Equal("TAB 2 (locked)", StashPanelLogic.LockedTabLabel(2));
    }

    [Fact]
    public void TabsShown_AlwaysShowsTheNextLockedTab_Honestly()
    {
        // Fresh (0 paid tabs): the chest shows TAB 1 + the locked TAB 2 (the
        // README row: "tab 1 + reward-unlocked tab 2"). glowa paid (1): still
        // 1..2 — both open. A second payment (2) grows the shown set to 3.
        Assert.Equal(new[] { 1, 2 }, StashPanelLogic.TabsShown(0));
        Assert.Equal(new[] { 1, 2 }, StashPanelLogic.TabsShown(1));
        Assert.Equal(new[] { 1, 2, 3 }, StashPanelLogic.TabsShown(2));
    }

    // ---- 2. the town gate: the panel is inert outside the pulled town key ----

    [Fact]
    public void OpensFor_TownOnly_GrodziskoIsThePulledTownKey()
    {
        var s = Fresh();
        Assert.Equal("grodzisko", s.Areas.TownKey); // Act gates on the PULLED key, never a literal
        Assert.True(StashPanelLogic.OpensFor("grodzisko", s.Areas.TownKey));
        Assert.False(StashPanelLogic.OpensFor("mchy", s.Areas.TownKey));
        Assert.False(StashPanelLogic.OpensFor("pokrzywlasy", s.Areas.TownKey));
        Assert.False(StashPanelLogic.OpensFor("czelusc", s.Areas.TownKey));
    }

    [Fact]
    public void Store_Take_OutsideTown_Refused_Named_ByTheEngine()
    {
        // The engine's town gate itself (the brief's INERT-OUTSIDE row: the
        // widget gate rides OpensFor; these rows pin what the ENGINE answers
        // if anything ever reaches the doors out of town). Named: the area
        // rides the refusal.
        var s = Fresh(9);
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        var e1 = Assert.Throws<SessionRejectedException>(() => s.Store(0, 0, 1));
        Assert.Contains("in town", e1.Message);
        Assert.Contains("mchy", e1.Message);
        var e2 = Assert.Throws<SessionRejectedException>(() => s.Take(1, 0, 0));
        Assert.Contains("in town", e2.Message);
        Assert.Contains("mchy", e2.Message);
    }

    // ---- 3. the locked-tab law: refusal BY NAME, never a bypass ----

    [Fact]
    public void Store_LockedTab2_Refused_Named_EmptyPackIncluded()
    {
        // Gate order (the Stash.cs header: DEAD → town → tab → coords →
        // content): even a real carried item hits the NAMED lock before any
        // content question — and the name ("tab 2") rides the text.
        var s = Fresh();
        s.Buy("soluga");
        Assert.False(s.IsStashTabUnlocked(2));
        var e = Assert.Throws<SessionRejectedException>(() => s.Store(0, 0, 2));
        Assert.Contains("tab 2", e.Message);
        Assert.Contains("locked", e.Message);
        Assert.Single(s.Carried); // the refusal applied nothing
    }

    [Fact]
    public void Take_LockedTab2_Refused_Named()
    {
        var s = Fresh();
        var e = Assert.Throws<SessionRejectedException>(() => s.Take(2, 0, 0));
        Assert.Contains("tab 2", e.Message);
        Assert.Contains("locked", e.Message);
    }

    [Fact]
    public void SelectTab_RefusesLockedTab_KeepsCurrent_HintNamed()
    {
        // The NEVER-BYPASS row: a locked pick keeps the current tab and hands
        // back the named hint (non-empty, engine-pinned below) — the panel
        // paints the lock, it never switches into it.
        var (tab, hint) = StashPanelLogic.SelectTab(1, 2, unlocked: false);
        Assert.Equal(1, tab);
        Assert.False(string.IsNullOrWhiteSpace(hint));
        var (tab2, hint2) = StashPanelLogic.SelectTab(1, 2, unlocked: true);
        Assert.Equal(2, tab2);
        Assert.Null(hint2);
    }

    [Fact]
    public void LockedTabHint_IsTheEnginesOwnWords_ByteForByte()
    {
        // The widget-side hint (a locked tab-click calls NO door — selection
        // is not a sim verb) is pinned BYTE-EQUAL to the real engine refusal,
        // so the copy cannot drift into a lie beside the engine's wording.
        var s = Fresh();
        var engine = Assert.Throws<SessionRejectedException>(() => s.Store(0, 0, 2)).Message;
        Assert.Equal(engine, StashPanelLogic.LockedTabHint(2));
    }

    [Fact]
    public void GlowaOpensTab2_LockStateFollowsTheEngineRead()
    {
        // The panel's lock truth is ONE pulled door read — IsStashTabUnlocked:
        // fresh the lock stands; glowa's StashTab reward opens it and the
        // store lands (the panel would paint TAB 2 unlocked with no code change).
        var s = Fresh();
        Assert.True(StashPanelLogic.LockedShown(s, 2));
        UnlockGlowa(s);
        Assert.False(StashPanelLogic.LockedShown(s, 2));
        s.Buy("soluga");
        var rep = s.Store(0, 0, 2);
        Assert.Equal(2, rep.Tab);
    }

    // ---- 4. store/take mapping + echo (the CLI twin echoes the report) ----

    [Fact]
    public void StoreEcho_CliLine_EchoesTheSessionReport()
    {
        var (lines, _) = RunCli("new koldun", "buy soluga", "stash_store 1 0 0", "dump");
        var store = lines.Single(l => l.Contains("\"CMD\":\"stash_store\""));
        using var doc = System.Text.Json.JsonDocument.Parse(store);
        var p = doc.RootElement;
        Assert.Equal("soluga", p.GetProperty("BASE").GetString());
        Assert.Equal(1, p.GetProperty("TAB").GetInt32());
        Assert.Equal(0, p.GetProperty("INV_ROW").GetInt32()); // echoed pack anchor
        Assert.Equal(0, p.GetProperty("INV_COL").GetInt32());
        Assert.Equal(0, p.GetProperty("TAB_ROW").GetInt32()); // first-fit into the empty tab
        Assert.Equal(0, p.GetProperty("TAB_COL").GetInt32());
    }

    [Fact]
    public void TakeEcho_CliLine_EchoesTheSessionReport_PackRefilled()
    {
        var (lines, _) = RunCli("new koldun", "buy soluga", "stash_store 1 0 0", "stash_take 1 0 0", "dump");
        var take = lines.Single(l => l.Contains("\"CMD\":\"stash_take\""));
        using var doc = System.Text.Json.JsonDocument.Parse(take);
        var p = doc.RootElement;
        Assert.Equal("soluga", p.GetProperty("BASE").GetString());
        Assert.Equal(1, p.GetProperty("TAB").GetInt32());
        Assert.Equal(0, p.GetProperty("TAB_ROW").GetInt32()); // the exact cells it left
        Assert.Equal(0, p.GetProperty("TAB_COL").GetInt32());
        Assert.Equal(0, p.GetProperty("INV_ROW").GetInt32()); // re-anchored pack (TryCarry)
        Assert.Equal(0, p.GetProperty("INV_COL").GetInt32());
        // The twin session's own dump line (the CLI end): the item is BACK in
        // carried and the tab holds nothing — the pack refilled through the
        // door, read from the state the replayed session answers with. (The
        // v6 save law: opened-but-EMPTY tabs are NOT saved — the stash array
        // is empty again, the unlock truth rides StashTabs.)
        var dump = lines.Single(l => l.Contains("\"CMD\":\"dump\""));
        using var dd = System.Text.Json.JsonDocument.Parse(dump);
        var state = dd.RootElement.GetProperty("STATE");
        Assert.Equal("soluga", state.GetProperty("carried")[0].GetProperty("base").GetString());
        Assert.Equal(0, state.GetProperty("stash").GetArrayLength());
    }

    [Fact]
    public void Store_Take_SessionRoundTrip_PackEmpties_ThenRefills()
    {
        // The windowed DoD pair, session-side: store → Carried empty; the
        // public dump read shows the tab row appear; take → the SAME instance
        // rides back (StashQuestLeg's identity read).
        var s = Fresh();
        var gem = s.Carried.Count;
        Assert.Equal(0, gem);
        s.Buy("soluga");
        Assert.Single(s.Carried);
        var st = s.Store(0, 0, 1);
        Assert.Empty(s.Carried);                       // the pack cell emptied (shot pair, state side)
        Assert.Equal(("soluga", 0, 0), (st.Base, st.TabRow, st.TabCol)); // the tab row appeared
        var painted = StashPanelLogic.TabItems(s.Dump(), 1);
        Assert.Equal(("soluga", 0, 0), (painted[0].Base, painted[0].Row, painted[0].Col)); // the panel's read
        var tk = s.Take(1, st.TabRow, st.TabCol);
        Assert.Single(s.Carried);                      // the pack refilled
        Assert.Same(s.Carried[0], s.Carried[^1]);
        Assert.Equal(("soluga", 0, 0), (tk.Base, tk.InvRow, tk.InvCol));
        Assert.Empty(StashPanelLogic.TabItems(s.Dump(), 1)); // the tab row is gone from the paint too
    }

    [Fact]
    public void PaintRead_CarriedAnchors_ComeFromThePublicDumpRead()
    {
        // The panel paints the PACK side from the dump's own anchors (the CLI
        // `dump` report is the ONE place the Sim answers "which cell?" — ARCH
        // v8; the panel re-derives nothing on this side).
        var s = Fresh();
        s.Buy("soluga"); // 1×1 first-fit → the engine anchors it at 0,0
        var painted = StashPanelLogic.CarriedItems(s.Dump());
        Assert.Equal(("soluga", 0, 0), (painted[0].Base, painted[0].Row, painted[0].Col));
    }

    // ---- scan rows: the shipped files honour the LAW (TownPanelTests twins) ----

    private static (List<string> Lines, GameSession? Session) RunCli(params string[] verbLines)
    {
        var path = Path.Combine(Path.GetTempPath(), $"stashpanel-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, string.Join('\n', verbLines) + '\n');
        var outDir = Path.Combine(Path.GetTempPath(), $"stashpanel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        try
        {
            var output = new StringWriter();
            int exit = CommandRunner.Run(path, 7, TestData.DataDir(), output);
            Assert.Equal(0, exit);
            var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            return (lines, null);
        }
        finally { File.Delete(path); }
    }

    private static string[] ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game", "scripts")))
            dir = dir.Parent;
        return Directory.GetFiles(Path.Combine(dir!.FullName, "game", "scripts"), "*.cs");
    }

    private static readonly string[] PanelFiles = ["StashPanel.cs"];
    private static readonly string[] LegFiles = ["StashChestLeg.cs", "StashChestLeg.Tools.cs"];

    [Fact]
    public void StashPanelDoorCalls_AlwaysRideCommandLog()
    {
        // The GameInputTests KEY LAW twin for the chest panel: a session verb
        // in the panel may exist ONLY inside a CommandLog.Call line (a logged
        // door) — the BABA_LOG replay contract rides it.
        var doorCall = new Regex(@"\.(Store|Take)\s*\(");
        var logged = new Regex(@"CommandLog\.Call\s*\(");
        var panelFiles = ScriptsDir().Where(f => PanelFiles.Contains(Path.GetFileName(f))).ToList();
        Assert.NotEmpty(panelFiles); // the scan scans the shipped files — never vacuous-green
        foreach (var f in panelFiles)
            foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || !doorCall.IsMatch(line)) continue;
                Assert.True(logged.IsMatch(line),
                    $"{Path.GetFileName(f)}:{lineNo} unlogged door call: {trimmed}");
            }
    }

    [Fact]
    public void StashPanelRejections_AlwaysPaintTheEnginesText()
    {
        // Every catch of the engine's refusal hands e.Message to the hint —
        // the DoD row: the refusal text surfaces, never a silent swallow.
        var panelFiles = ScriptsDir().Where(f => PanelFiles.Contains(Path.GetFileName(f))).ToList();
        Assert.NotEmpty(panelFiles);
        foreach (var f in panelFiles)
        {
            var lines = File.ReadAllLines(f);
            int catches = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!Regex.IsMatch(lines[i], @"catch\s*\(\s*SessionRejectedException\s+\w+\s*\)")) continue;
                catches++;
                var scope = string.Join(' ', lines.Skip(i).Take(2)); // same line or the next paints
                Assert.Matches(@"Hint\(\w+\.Message\)", scope);
            }
            Assert.True(catches > 0, $"{Path.GetFileName(f)} has no refusal catch at all — nothing surfaces");
            Assert.DoesNotContain(lines, l => Regex.IsMatch(l, @"catch\s*(\(\s*\))?\s*\{\s*\}")); // bare swallow
        }
    }

    [Fact]
    public void StashChestLegDriver_CallsNoSessionVerb_WritesNoState()
    {
        // The leg is a driver (the S4 law, extended to the chest files): every
        // state move is a synthesized click landing on the real call sites.
        // Dump() is a read, not a verb — it stays outside the ban.
        var verb = new Regex(
            @"\.(Buy|Store|Take|AddToBelt|Hire|Revive|Dismiss|UsePotion|Attack|TravelTo|DiscoverWaypoint|Respawn|Loot|Cast|LevelTo|RestTick)\s*\(");
        var stateWrite = new Regex(
            @"\b(s|S|session|Session)\.(CurrentArea|Gold|Purse|Carried|Belt|Drops|Engaged\w*|Merc|Waypoints|Quests|Equipment|Character)\b\s*(=|\.Add|\.Remove|\.RemoveAt|\.Clear)|\.(Life|Mana|Xp)\s*=\s*[^=]");
        var legFiles = ScriptsDir().Where(f => LegFiles.Contains(Path.GetFileName(f))).ToList();
        Assert.NotEmpty(legFiles);
        foreach (var f in legFiles)
            foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//")) continue;
                Assert.False(verb.IsMatch(line), $"{Path.GetFileName(f)}:{lineNo} driver calls a session verb: {trimmed}");
                Assert.False(stateWrite.IsMatch(line), $"{Path.GetFileName(f)}:{lineNo} driver writes session state: {trimmed}");
            }
    }
}
