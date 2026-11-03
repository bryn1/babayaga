using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BabaYaga.Sim;
using BabaYaga.Sim.Cli;
using Xunit;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// M5 RITUAL-CRAFT PANEL (MC 11970, LEDGER row 75 cube-windowed half): the pure
/// decision core the windowed CraftPanel rides — CraftPanelLogic — exercised
/// WITHOUT Godot (the TownPanelLogic/StashPanelLogic pure-file idiom). The DoD
/// row groups: (1) the EMPTY STATE — zero admitted recipes paint exactly ONE
/// honest line ("no inscriptions warm the cube yet") and ZERO rows; the engine's
/// SkippedCount stays engine-side, the player sees the honest empty state, never
/// a fake row; (2) ROW-BUILD — one admitted fixture recipe yields its name plus
/// inputs as item NAMES × counts, all pulled through the session's own tables;
/// (3) the ACTION MAP — the CRAFT button rides the exact CLI twin grammar
/// "craft &lt;recipeId&gt;", one full run replayed LIVE through the real
/// CommandRunner so the table cannot drift from the grammar (the TownPanelTests
/// live-replay idiom); (4) REFUSAL→HINT — every refusal the button can trigger
/// carries the engine's NAMED text (the panel catch paints it, scan-pinned);
/// (5) the TOWN GATE — inert outside the pulled town key. Fixture dirs ride the
/// RangedDataDir convention (RitualCraftTests): a GUID temp copy of the real
/// data dir with cube-recipes.json patched, never a committed fixture.
/// </summary>
public sealed class CraftPanelTests
{
    // ---- the §2(iii)/RitualCraftTests fixture convention: temp copy, patched ----

    private static string CraftDataDir(
        string id = "proba_kosci", string name = "Bone Trial (synthetic witness)",
        string inBase = "soluga", int inCount = 2, string outBase = "jantar")
    {
        var src = TestData.DataDir();
        var dir = Path.Combine(Path.GetTempPath(), $"cubeui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(src, "*.json"))
            File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
        var path = Path.Combine(dir, "cube-recipes.json");
        var doc = JsonNode.Parse(File.ReadAllText(path))!;
        ((JsonArray)doc["recipes"]!).Add(new JsonObject
        {
            ["id"] = id,
            ["name"] = name,
            ["inputs"] = new JsonArray { new JsonObject { ["itemId"] = inBase, ["count"] = inCount } },
            ["output"] = new JsonObject { ["itemId"] = outBase }, // the plain fixed-mint family (R2b)
        });
        File.WriteAllText(path, doc.ToJsonString());
        return dir;
    }

    private static GameSession Town(ulong seed = 7, string? dir = null) =>
        GameSession.New(seed, "koldun", dir ?? TestData.DataDir());

    // ---- 1. the EMPTY STATE: zero admitted -> exactly the honest line, no rows ----

    [Fact]
    public void ShippedPanel_FiveAdmitted_PaintsTheFiveRealRows()
    {
        // The SHIPPED truth (row 75; MC 11979 — the five PLACEHOLDER rows are
        // gone): the panel must paint EXACTLY the five REAL shipped rows in
        // file order, no more and no fewer. SkippedCount stays engine-side:
        // the pure surface answers rows, never a skip count.
        var s = Town();
        Assert.Equal(5, s.CubeRecipes.Recipes.Count);
        Assert.Equal(0, s.CubeRecipes.SkippedCount); // engine read-back, NOT a paint input
        Assert.Equal(
            ["Resin into Stone — 3 × Raw Amber", "Stone upon Stone — 3 × Swamp Garnet",
             "Amber Heart — 1 × Raw Amber + 1 × Swamp Garnet + 1 × Fen Peridot",
             "Three Rings, One Ward — 3 × Pierscien", "The Resin-Coated Blade — 2 × Palica + 1 × Raw Amber"],
            CraftPanelLogic.RowLabels(s.CubeRecipes, s.Bases)); // the five REAL rows, file order = paint order
        Assert.Equal("no inscriptions warm the cube yet", CraftPanelLogic.EmptyLine);
    }

    // ---- 2. row-build: fixture recipe -> exact name + inputs text ----

    [Fact]
    public void RowBuild_FixtureRecipe_ExactNameAndInputsFromTheSessionsOwnTables()
    {
        var dir = CraftDataDir();
        try
        {
            var s = Town(7, dir);
            Assert.Equal(6, s.CubeRecipes.Recipes.Count); // admitted: the five REAL shipped recipes + this fixture row
            var rows = CraftPanelLogic.RowLabels(s.CubeRecipes, s.Bases);
            // Item NAMES (not ids) × counts, pulled through s.Bases — the exact
            // string the panel paints and the windowed leg reads back (one
            // builder, no duplicated literal).
            Assert.Equal("Bone Trial (synthetic witness) — 2 × Soluga", rows[^1]); // the appended row, file order = paint order
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void RowBuild_MultiInputRecipe_ListsEveryRowInSchemaOrder()
    {
        var dir = CraftDataDir(inBase: "soluga", inCount: 2);
        // a second admitted row through the same patched-dir convention:
        var src = TestData.DataDir();
        var p = Path.Combine(dir, "cube-recipes.json");
        var doc = JsonNode.Parse(File.ReadAllText(p))!;
        var row = (JsonObject)JsonNode.Parse(
            """{"id":"drugi","name":"Second Trial","inputs":[{"itemId":"jantar","count":3}],"output":{"itemId":"obereg"}}""")!;
        ((JsonArray)doc["recipes"]!).Add(row);
        File.WriteAllText(p, doc.ToJsonString());
        try
        {
            var s = Town(7, dir);
            var rows = CraftPanelLogic.RowLabels(s.CubeRecipes, s.Bases);
            Assert.Equal(7, rows.Count); // file order, one row per ADMITTED recipe — never more (five real + two fixture)
            Assert.Equal("Second Trial — 3 × Raw Amber", rows[^1]); // the appended row, file order = paint order
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- 3. the action map: the exact CLI twin grammar, replayed LIVE ----

    [Fact]
    public void ActionMap_CraftLine_IsTheExactCliGrammar()
    {
        Assert.Equal("craft proba_kosci", CraftPanelLogic.CraftLine("proba_kosci"));
        Assert.Equal("craft r_ember", CraftPanelLogic.CraftLine("r_ember"));
        Assert.Equal("CRAFT", CraftPanelLogic.CraftButton);
    }

    [Fact]
    public void ActionMap_LinesReplayLive_ThroughTheRealCli()
    {
        // The table's strings ARE the grammar: the exact lines a cube-panel
        // session emits (new head, two buys, the craft, dump) run through the
        // real CommandRunner on the FIXTURE dir at a fixed seed with NO ERROR
        // line and exit 0 — a mis-shaped line exits nonzero.
        var dir = CraftDataDir();
        var path = Path.Combine(Path.GetTempPath(), $"cubeui-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, string.Join('\n',
            "new koldun",
            TownPanelLogic.BuyLine("soluga"),
            TownPanelLogic.BuyLine("soluga"),
            CraftPanelLogic.CraftLine("proba_kosci"),
            "dump") + '\n');
        try
        {
            var output = new StringWriter();
            int exit = CommandRunner.Run(path, 1, dir, output);
            Assert.Equal(0, exit);
            var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.DoesNotContain(lines, l => l.Contains("\"ERROR\""));
            var echo = lines.Select(l => JsonNode.Parse(l)!).First(n => n["CMD"]?.GetValue<string>() == "craft");
            Assert.Equal("proba_kosci", echo["RECIPE"]!.GetValue<string>()); // the door answered the grammar
        }
        finally { File.Delete(path); Directory.Delete(dir, true); }
    }

    // ---- 4. refusal→hint: the engine's refusal TEXT surfaces, named ----

    [Fact]
    public void Refusal_Shortfall_NamedByTheEngine_TheHintPaints()
    {
        var dir = CraftDataDir();
        try
        {
            var s = Town(7, dir);
            s.Buy("soluga"); // one short of the two
            var e = Assert.Throws<SessionRejectedException>(() => s.CraftRecipe("proba_kosci"));
            Assert.Equal("recipe 'proba_kosci' found no match — missing 1 × soluga", e.Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Refusal_OutsideTown_NamedByTheEngine_TheHintPaints()
    {
        var dir = CraftDataDir();
        try
        {
            var s = Town(7, dir);
            s.Buy("soluga"); s.Buy("soluga");
            s.TravelTo("pokrzywlasy"); // a REAL walk out (the RitualCraftTests witness shape)
            var e = Assert.Throws<SessionRejectedException>(() => s.CraftRecipe("proba_kosci"));
            Assert.Contains("stands in town", e.Message);
            Assert.Contains("pokrzywlasy", e.Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- 5. the town gate: inert outside the pulled town key ----

    [Fact]
    public void OpensFor_TownOnly_GrodziskoIsThePulledTownKey()
    {
        var s = Town();
        Assert.Equal("grodzisko", s.Areas.TownKey); // Act gates on the PULLED key, never a literal
        Assert.True(CraftPanelLogic.OpensFor("grodzisko", s.Areas.TownKey));
        Assert.False(CraftPanelLogic.OpensFor("mchy", s.Areas.TownKey));
        Assert.False(CraftPanelLogic.OpensFor("pokrzywlasy", s.Areas.TownKey));
    }

    [Fact]
    public void OutsideHint_AndSuccessHint_AreOneBuilderEach_ThePanelAndLegShare()
    {
        // Label-idiom twins (DeathMenuLogic/TownPanelLogic): the panel paints
        // and the leg reads with the SAME string — no duplicated literal. The
        // out-name is PULLED from the base table (the plain fixed-mint output
        // carries its base name verbatim — the R2b reading).
        var s = Town();
        Assert.Equal("the bone cube stands in grodzisko", CraftPanelLogic.OutsideHint);
        var dir = CraftDataDir();
        try
        {
            var f = Town(7, dir);
            var r = f.CubeRecipes.For("proba_kosci");
            Assert.Equal("the cube cools — Raw Amber lands in your pack",
                CraftPanelLogic.OutHint(r, f.Bases));
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- scan rows: the shipped cube files honour the LAW (panel-test twins) ----

    private static string[] ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game", "scripts")))
            dir = dir.Parent;
        return Directory.GetFiles(Path.Combine(dir!.FullName, "game", "scripts"), "*.cs");
    }

    private static readonly string[] PanelFiles = ["CraftPanel.cs"];
    private static readonly string[] LegFiles = ["CubeLeg.cs", "CubeLeg.Tools.cs"];

    [Fact]
    public void CraftPanelDoorCalls_AlwaysRideCommandLog()
    {
        // The TownPanelTests KEY LAW twin: the craft door may be called in the
        // panel ONLY inside a CommandLog.Call line (the logged door, replay law).
        var doorCall = new Regex(@"\.CraftRecipe\s*\(");
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
    public void CraftPanelRejections_AlwaysPaintTheEnginesText()
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
    public void CraftPanel_KeepsTheBlockingOverlayDefault()
    {
        // The MC 11959 overlay law: an overlay is interactive — its root and
        // body block by the PASS default (buttons STOP); NO overlay file sets a
        // MouseFilter (the InventoryPanel root exception stays the only one,
        // pinned in HudMouseFilterTests). CraftPanel must not touch it.
        foreach (var f in ScriptsDir().Where(f => PanelFiles.Contains(Path.GetFileName(f))))
            Assert.DoesNotContain(File.ReadAllLines(f), l =>
                Regex.IsMatch(l, @"MouseFilter\s*=") && !l.TrimStart().StartsWith("//"));
    }

    [Fact]
    public void CubeLegDriver_CallsNoSessionVerb_WritesNoState()
    {
        // The driver law (S4/S6, extended): every state move is a synthesized
        // click/key landing on the real call sites — the Buy/Craft verbs here
        // are what the CLICKED buttons call, never the leg.
        var verb = new Regex(
            @"\.(Buy|CraftRecipe|AddToBelt|Hire|Revive|Dismiss|UsePotion|Attack|TravelTo|DiscoverWaypoint|Respawn|Loot|Cast|LevelTo|RestTick)\s*\(");
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
