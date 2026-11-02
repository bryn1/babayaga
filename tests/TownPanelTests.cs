using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BabaYaga.Sim;
using BabaYaga.Sim.Cli;
using Xunit;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// M5-S6 TOWN PANEL (MC 11940, plan-c2 S6 row): the pure decision core the
/// windowed TownPanel rides — TownPanelLogic — exercised WITHOUT Godot (the
/// DeathMenuLogic/GameInputTests pure-file/link idiom). The three DoD row
/// groups: (1) the action→door MAPPING TABLE — the exact CLI twin verb lines
/// the panel emits through CommandLog.Call, one run replayed LIVE through the
/// real CommandRunner so the table cannot drift from the grammar; (2) the
/// TOWN GATE — the panel is inert outside the pulled town key (grodzisko);
/// (3) REJECTION→HINT — the engine's refusal text surfaces (real-session rows
/// for every refusal the panel's buttons can trigger) and the scan rows prove
/// the shipped files paint it: every TownPanel catch hands e.Message to the
/// HUD hint (never a silent swallow), every panel door call rides
/// CommandLog.Call, and the S6 driver leg calls NO session verb and writes
/// NO state (the GameInputTests KEY LAW extended to the S6 files).
/// </summary>
public class TownPanelTests
{
    private static GameSession Fresh(ulong seed = 7) => GameSession.New(seed, "koldun", TestData.DataDir());

    // ---- 1. the action→door mapping table (the exact CLI twin verb lines) ----

    [Fact]
    public void MappingTable_EmitsTheCliTwinVerbLines()
    {
        Assert.Equal("buy soluga", TownPanelLogic.BuyLine("soluga"));
        Assert.Equal("buy napitek-zycia", TownPanelLogic.BuyLine("napitek-zycia"));
        Assert.Equal("belt_add 0 1", TownPanelLogic.BeltAddLine(0, 1));
        Assert.Equal("belt_add 1 4", TownPanelLogic.BeltAddLine(1, 4));
        Assert.Equal("hire strzelczyni", TownPanelLogic.HireLine("strzelczyni"));
        Assert.Equal("revive strzelczyni", TownPanelLogic.ReviveLine("strzelczyni"));
        Assert.Equal("dismiss", TownPanelLogic.DismissLine);
    }

    [Fact]
    public void MappingTable_LinesReplayLive_ThroughTheRealCli()
    {
        // The table's strings ARE the grammar: the exact lines a town-panel
        // session emits (buy, buy, belt_add, hire, dismiss, dump) run through
        // the real CommandRunner at a fixed seed with NO ERROR line — a
        // rejected or mis-shaped line exits nonzero.
        var lines = new[]
        {
            "new koldun", // the CLI head (GameContext.New synthesizes it in-game)
            TownPanelLogic.BuyLine("soluga"),
            TownPanelLogic.BuyLine("napitek-zycia"),
            TownPanelLogic.BeltAddLine(1, 1), // the potion landed at pack index 1
            TownPanelLogic.HireLine("strzelczyni"),
            TownPanelLogic.DismissLine,
            "dump",
        };
        var path = Path.Combine(Path.GetTempPath(), $"townpanel-{Guid.NewGuid():N}.txt");
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
    public void ButtonLabels_ComeFromOneBuilder_TheDriverHuntsWith()
    {
        // One builder per button (the DeathMenuLogic label idiom): the panel
        // paints and the leg hunts with the SAME string — no duplicated literal.
        Assert.Equal("BUY Soluga 110g", TownPanelLogic.BuyLabel("Soluga", 110));
        Assert.Equal("HIRE Strzelczyni (bow-kin) 100g", TownPanelLogic.HireLabel("Strzelczyni (bow-kin)", 100));
        Assert.Equal("REVIVE Strzelczyni (bow-kin)", TownPanelLogic.ReviveLabel("Strzelczyni (bow-kin)"));
        Assert.Equal("DISMISS", TownPanelLogic.DismissButton);
    }

    [Fact]
    public void StockRows_ResolvethroughtheSessionsOwnTables()
    {
        // "session Vendor tables": every stock id resolves in s.Bases and the
        // captain row in s.Hirelings — the panel can only paint what the
        // session holds (pulled, never a widget-side phantom).
        var s = Fresh();
        foreach (var id in TownPanelLogic.StockBaseIds)
        {
            var row = s.Bases.Find(id);
            Assert.NotNull(row);
            Assert.True(Vendor.BuyPrice(row!, Vendor.Standard) >= 1);
        }
        Assert.NotNull(s.Hirelings.Find(TownPanelLogic.CaptainRowId));
    }

    // ---- the belt-slot pick (presentation-side choice; the engine still gates) ----

    [Fact]
    public void BeltSlotFor_PicksFirstFree_OrFallsBackToOne_TheEngineRefusalRow()
    {
        var s = Fresh();
        Assert.Equal(1, TownPanelLogic.BeltSlotFor(s.Belt));
        s.Buy("napitek-zycia");
        s.AddToBelt(0, 2); // slot 2 occupied by a real door — the pick must skip it
        Assert.Equal(1, TownPanelLogic.BeltSlotFor(s.Belt));
        for (int buy = 0; buy < 3; buy++) { s.Buy("napitek-zycia"); s.AddToBelt(buy + 1, buy == 0 ? 1 : buy + 2); }
        // Belt full: the pick returns 1 — the panel calls AddToBelt anyway and
        // the ENGINE'S "already holds" refusal becomes the hint (no silent
        // swallow, no panel-side gate). That fallback call is asserted here.
        Assert.Equal(1, TownPanelLogic.BeltSlotFor(s.Belt));
        var e = Assert.Throws<SessionRejectedException>(() => s.AddToBelt(s.Carried.Count - 1, 1));
        Assert.Contains("already holds", e.Message);
    }

    // ---- 2. the town gate: the panel is inert outside the pulled town key ----

    [Fact]
    public void OpensFor_TownOnly_GrodziskoIsThePulledTownKey()
    {
        var s = Fresh();
        Assert.Equal("grodzisko", s.Areas.TownKey); // Act gates on the PULLED key, never a literal
        Assert.True(TownPanelLogic.OpensFor("grodzisko", s.Areas.TownKey));
        Assert.False(TownPanelLogic.OpensFor("mchy", s.Areas.TownKey));
        Assert.False(TownPanelLogic.OpensFor("pokrzywlasy", s.Areas.TownKey));
        Assert.False(TownPanelLogic.OpensFor("krecie", s.Areas.TownKey));
    }

    [Fact]
    public void HireTownRefusal_NamedByTheEngine_WhenCarriedOutOfTown()
    {
        // The engine's town gate itself: the lit-stone doors carry the hero out,
        // then the captain's door refuses with its named text — the wording the
        // hint would paint. The panel's OpensFor gate never re-implements it.
        var s = Fresh(9);
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");
        var e = Assert.Throws<SessionRejectedException>(() => s.Hire("strzelczyni"));
        Assert.Contains("in town", e.Message);
        Assert.Contains("mchy", e.Message);
    }

    // ---- 3. rejection→hint: the engine's refusal TEXT surfaces, named ----

    public static TheoryData<string, Func<GameSession, string>> Refusals => new()
    {
        { "hire while one rides", s => { s.Hire("strzelczyni"); return Reject(s, () => s.Hire("strzelczyni")); } },
        { "revive a living merc", s => { s.Hire("strzelczyni"); return Reject(s, () => s.Revive("strzelczyni")); } },
        { "dismiss with none", s => Reject(s, () => s.Dismiss()) },
        { "buy without gold", s => Reject(s, () => { for (int i = 0; i < 10; i++) s.Buy("soluga"); }) },
        { "drink at full life", s => Reject(s, () => { s.Buy("napitek-zycia"); s.AddToBelt(0, 1); s.UsePotion(1); }) },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public void EngineRefusals_CarryTheNamedText_TheHintPaints(string row, Func<GameSession, string> trigger)
    {
        // These strings are what the panel's catch hands to hud.Hint — the
        // engine owns the wording; empty text would make the hint a lie.
        var message = trigger(Fresh());
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    private static string Reject(GameSession s, Action call) =>
        Assert.Throws<SessionRejectedException>(() => call()).Message;

    // ---- scan rows: the shipped S6 files honour the LAW (GameInputTests twins) ----

    private static string[] ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game", "scripts")))
            dir = dir.Parent;
        return Directory.GetFiles(Path.Combine(dir!.FullName, "game", "scripts"), "*.cs");
    }

    private static readonly string[] PanelFiles = ["TownPanel.cs"];
    private static readonly string[] LegFiles = ["S6InputLeg.cs", "S6InputLeg.Tools.cs"];

    [Fact]
    public void TownPanelDoorCalls_AlwaysRideCommandLog()
    {
        // The GameInputTests KEY LAW twin for the S6 panel: a session verb in
        // the panel may exist ONLY inside a CommandLog.Call line (a logged door).
        var doorCall = new Regex(@"\.(Buy|AddToBelt|Hire|Revive|Dismiss)\s*\(");
        var logged = new Regex(@"CommandLog\.Call\s*\(");
        foreach (var f in ScriptsDir().Where(f => PanelFiles.Contains(Path.GetFileName(f))))
            foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || !doorCall.IsMatch(line)) continue;
                Assert.True(logged.IsMatch(line),
                    $"{Path.GetFileName(f)}:{lineNo} unlogged door call: {trimmed}");
            }
    }

    [Fact]
    public void TownPanelRejections_AlwaysPaintTheEnginesText()
    {
        // Every catch of the engine's refusal hands e.Message to the hint —
        // the DoD row: the refusal text surfaces, never a silent swallow.
        foreach (var f in ScriptsDir().Where(f => PanelFiles.Contains(Path.GetFileName(f))))
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
    public void S6DriverLeg_CallsNoSessionVerb_WritesNoState()
    {
        // The leg is a driver (the S4 law, extended to the S6 files): every
        // state move is a synthesized click landing on the real call sites.
        var verb = new Regex(
            @"\.(Buy|AddToBelt|Hire|Revive|Dismiss|UsePotion|Attack|TravelTo|DiscoverWaypoint|Respawn|Loot|Cast|LevelTo|RestTick)\s*\(");
        var stateWrite = new Regex(
            @"\b(s|S|session|Session)\.(CurrentArea|Gold|Purse|Carried|Belt|Drops|Engaged\w*|Merc|Waypoints|Quests|Equipment|Character)\b\s*(=|\.Add|\.Remove|\.RemoveAt|\.Clear)|\.(Life|Mana|Xp)\s*=\s*[^=]");
        foreach (var f in ScriptsDir().Where(f => LegFiles.Contains(Path.GetFileName(f))))
            foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//")) continue;
                Assert.False(verb.IsMatch(line), $"{Path.GetFileName(f)}:{lineNo} driver calls a session verb: {trimmed}");
                Assert.False(stateWrite.IsMatch(line), $"{Path.GetFileName(f)}:{lineNo} driver writes session state: {trimmed}");
            }
    }
}
