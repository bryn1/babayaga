using System.Text.Json.Nodes;
using BabaYaga.Sim.Cli;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// M4-S13 CLI VERBS (MC 11896, gate P4 debts MC 11880/11890/11895): the six
/// engine-door verbs rest / hire / dismiss / revive / stash_store / stash_take
/// are THIN calls onto the existing public session doors — these tests prove
/// the terminal speaks each door (green rows: the quantum rest moves, the hire
/// price line, the free dismiss, the revive cost after a SCRIPTED merc death,
/// the stash round-trip anchors) and that every refusal prints the ENGINE'S
/// named error and exits 1 (hire outside town, stash_store outside town,
/// revive while alive, rest while dead) — the same stdout-markers + exit-codes
/// contract CliScriptTests pins for the older verbs. Expected numbers come
/// from the spec formulas written OUT here (the BeltRegenTests idiom), never
/// read back from the implementation. Seed 5 pins every scripted journey.
/// </summary>
public class CliVerbsTests
{
    public const ulong Seed = 5;

    private static (int Exit, List<string> Lines) RunScript(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, body);
        try
        {
            var output = new StringWriter();
            int exit = CommandRunner.Run(path, Seed, TestData.DataDir(), output);
            return (exit, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList());
        }
        finally { File.Delete(path); }
    }

    private static JsonNode Line(List<string> lines, int i) => JsonNode.Parse(lines[i])!;
    private static IEnumerable<JsonNode> Parsed(List<string> lines) => lines.Select(l => JsonNode.Parse(l)!);
    private static JsonNode Node(List<string> lines, string cmd) =>
        Parsed(lines).First(n => n["CMD"]?.GetValue<string>() == cmd);

    // ---- rest: the §1.8 quantum, moved by the CLI verb and echoed by the dump ----

    [Fact]
    public void RestVerb_MovesManaExactlyTheSpecQuantum_DumpEchoesThePool()
    {
        var (exit, lines) = RunScript("new koldun\nlevelto 20\nrest 0\nrest 1024\ndump\n");
        Assert.Equal(0, exit);
        // The spec formula written OUT (§1.8 M1/M3, BeltRegenTests' shape; no
        // item grants %RegenMana today, so the second floor is an identity).
        var dry = Node(lines, "rest"); // rest 0 is the ordered no-op — the pool before resting
        Assert.Equal(0, dry["FRAMES"]!.GetValue<int>());
        long mana0 = dry["MANA"]!.GetValue<long>(), max = dry["MAXMANA"]!.GetValue<long>();
        // The two rest lines share CMD — RestLine picks the 1024-frame one.
        var ticked = RestLine(lines, 1024);
        int tFrames = ticked["FRAMES"]!.GetValue<int>();
        long tMana = ticked["MANA"]!.GetValue<long>();
        Assert.Equal(1024, tFrames);
        long quantum256 = Math.Max(1, max * 256 / (25 * 120));
        long expected = Math.Min(max, (mana0 * 256 + (long)tFrames * quantum256) / 256);
        Assert.True(tMana > mana0, "rest did not move the pool at all");
        Assert.Equal(expected, tMana); // EXACTLY the quantum — no drift, no rounding on the accumulation
        Assert.Equal(tMana, Node(lines, "dump")["STATE"]!["mana"]!.GetValue<long>()); // the dump agrees
    }

    private static JsonNode RestLine(List<string> lines, int frames) => Parsed(lines)
        .First(n => n["CMD"]?.GetValue<string>() == "rest" && n["FRAMES"]!.GetValue<int>() == frames);

    // ---- hire / dismiss: the town captain's price line, the free send-off ----

    [Fact]
    public void HireVerb_InTown_PaysThePriceLine_AndTheDumpCarriesTheMerc()
    {
        var (exit, lines) = RunScript("new koldun\nhire strzelczyni\ndump\n");
        Assert.Equal(0, exit);
        var hire = Node(lines, "hire");
        Assert.Equal("strzelczyni", hire["MERC"]!.GetValue<string>());
        var goldBefore = Node(lines, "new")["GOLD"]!.GetValue<long>();
        long price = hire["PRICE"]!.GetValue<long>();
        Assert.Equal(100, price);                      // the row's hireGold (data/hirelings.json, act 1)
        Assert.Equal(goldBefore - price, hire["GOLD"]!.GetValue<long>()); // gold AFTER the hire on the price line
        Assert.Equal("strzelczyni", Node(lines, "dump")["STATE"]!["merc"]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void DismissVerb_IsFree_NoRefund_ThePurseStaysAsPaid()
    {
        var (exit, lines) = RunScript("new koldun\nhire strzelczyni\ndismiss\n");
        Assert.Equal(0, exit);
        var dismiss = Node(lines, "dismiss");
        Assert.Equal("strzelczyni", dismiss["DISMISSED"]!.GetValue<string>());
        Assert.Equal(Node(lines, "hire")["GOLD"]!.GetValue<long>(), dismiss["GOLD"]!.GetValue<long>()); // LoD no-refund
    }

    // ---- revive: a SCRIPTED merc death, then the cost line at town ----

    [Fact]
    public void ScriptedMercDeath_ReviveVerb_PaysTheCost_RestoresTheSameMercAtFullLife()
    {
        var (exit, lines) = RunScript("new koldun\nhire strzelczyni\nattack pokrzywnik 40\ndump\nrevive strzelczyni\n");
        Assert.Equal(0, exit);
        var merc = Node(lines, "dump")["STATE"]!["merc"]!;
        Assert.Equal(0, merc["life"]!.GetValue<long>()); // the SCRIPT killed the merc (counters, §6.7 targeting)
        var revive = Node(lines, "revive");
        int level = merc["level"]!.GetValue<int>();
        long specCost = Math.Min(15L * level * level / 2, 50000); // the door's formula, written OUT
        Assert.Equal("strzelczyni", revive["MERC"]!.GetValue<string>()); // the SAME mercenary (R3)
        Assert.Equal(specCost, revive["COST"]!.GetValue<long>());
        Assert.Equal(Node(lines, "hire")["GOLD"]!.GetValue<long>() - specCost, revive["GOLD"]!.GetValue<long>());
        Assert.True(revive["LIFE"]!.GetValue<long>() > 0); // back at full life
    }

    // ---- stash_store / stash_take: the round trip and its echoed anchors ----

    [Fact]
    public void StashRoundTrip_MovesTheItemAndBack_EchoingBothGridAnchors()
    {
        var (exit, lines) = RunScript("new koldun\nbuy jantar\nstash_store 1 0 0\nstash_take 1 0 0\ndump\n");
        Assert.Equal(0, exit);
        var store = Node(lines, "stash_store");
        Assert.Equal("jantar", store["BASE"]!.GetValue<string>());
        Assert.Equal(1, store["TAB"]!.GetValue<int>());
        Assert.Equal(0, store["INV_ROW"]!.GetValue<int>());  // the pack anchor it lifted from
        Assert.Equal(0, store["INV_COL"]!.GetValue<int>());
        var take = Node(lines, "stash_take");
        Assert.Equal(store["ITEM"]!.GetValue<string>(), take["ITEM"]!.GetValue<string>()); // the same item
        Assert.Equal(store["TAB_ROW"]!.GetValue<int>(), take["TAB_ROW"]!.GetValue<int>());  // lifted from the cell it stored to
        Assert.Equal(store["TAB_COL"]!.GetValue<int>(), take["TAB_COL"]!.GetValue<int>());
        Assert.Equal(store["INV_ROW"]!.GetValue<int>(), take["INV_ROW"]!.GetValue<int>());  // back in the same pack cell
        Assert.Equal(store["INV_COL"]!.GetValue<int>(), take["INV_COL"]!.GetValue<int>());
        var carried = Node(lines, "dump")["STATE"]!["carried"]!;
        Assert.Equal("jantar", carried[0]!["base"]!.GetValue<string>()); // and the dump agrees it rides again
    }

    // ---- refusals: the ENGINE'S named error + exit 1, per the CLI idiom ----

    [Fact]
    public void Hire_OutsideTown_PrintsTheEnginesNamedError_AndExits1()
    {
        var (exit, lines) = RunScript("new koldun\nwaypoint mchy\nhire strzelczyni\n");
        Assert.Equal(1, exit);
        Assert.Equal("hirelings are hired in town — the captain is not in 'mchy'",
            Line(lines, lines.Count - 1)["ERROR"]!.GetValue<string>());
        Assert.DoesNotContain(lines, l => l.Contains("RESULT"));
    }

    [Fact]
    public void StashStore_OutsideTown_PrintsTheEnginesNamedError_AndExits1()
    {
        var (exit, lines) = RunScript("new koldun\nwaypoint mchy\nstash_store 1 0 0\n");
        Assert.Equal(1, exit);
        Assert.Equal("the stash stands in town — the chest is not in 'mchy'",
            Line(lines, lines.Count - 1)["ERROR"]!.GetValue<string>());
    }

    [Fact]
    public void Revive_AliveMerc_PrintsTheEnginesNamedError_AndExits1()
    {
        var (exit, lines) = RunScript("new koldun\nhire strzelczyni\nrevive strzelczyni\n");
        Assert.Equal(1, exit);
        Assert.Equal("'strzelczyni' is not dead — nothing to revive at 40 life",
            Line(lines, lines.Count - 1)["ERROR"]!.GetValue<string>());
    }

    [Fact]
    public void Rest_WhileDead_PrintsTheEnginesNamedError_AndExits1()
    {
        var (exit, lines) = RunScript("new koldun\nattack pokrzywnik 1024\ndump\nrest 100\n");
        Assert.Equal(1, exit);
        Assert.Equal(0, Node(lines, "dump")["STATE"]!["life"]!.GetValue<long>()); // the SCRIPT killed the hero
        Assert.Equal("you are dead", Line(lines, lines.Count - 1)["ERROR"]!.GetValue<string>());
    }
}
