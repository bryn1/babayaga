using System.Text.Json.Nodes;
using BabaYaga.Sim.Cli;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// Read-only debug verbs (MC 11777, oracle-parity harness surface): each verb must
/// be a pure passthrough into its owning sim class — the asserted value is recomputed
/// in the test from the same sim class over the shipped data, never from CLI-side math.
/// Verbs run without a session; one JSON line per command still applies.
/// </summary>
public class DebugVerbTests
{
    private const ulong Seed = 1;

    private static JsonNode RunVerb(string verbLine)
    {
        var script = WriteTempScript(verbLine + "\n");
        try
        {
            var output = new StringWriter();
            int exit = CommandRunner.Run(script, Seed, TestData.DataDir(), output);
            Assert.Equal(0, exit); // a debug verb never needs 'new' and never rejects a valid line
            var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("{\"RESULT\":\"OK\"}", lines[^1]);
            return JsonNode.Parse(lines[0])!;
        }
        finally { File.Delete(script); }
    }

    private static string WriteTempScript(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dbg-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, body);
        return path;
    }

    [Fact]
    public void Xpfor_EchoesLadderTotals()
    {
        var ladder = TestData.Ladder();
        foreach (int level in new[] { 1, 2, 30, 70, 99 })
        {
            var line = RunVerb($"xpfor {level}");
            Assert.Equal("xpfor", line["CMD"]!.GetValue<string>());
            Assert.Equal(ladder.TotalXpForLevel(level), line["XP"]!.GetValue<long>());
        }
    }

    [Fact]
    public void Tohit_PassthroughAndClamps()
    {
        // Mid value recomputed through the resolver itself; the 5/95 clamps are the P1 §1.4 bounds.
        var mid = RunVerb("tohit 100 100 10 10");
        Assert.Equal(CombatResolver.ChanceToHit(100, 100, 10, 10), mid["TOHIT"]!.GetValue<int>());
        Assert.Equal(50, mid["TOHIT"]!.GetValue<int>());
        Assert.Equal(5, RunVerb("tohit 1 100000 1 1")["TOHIT"]!.GetValue<int>());
        Assert.Equal(95, RunVerb("tohit 100000 1 1 1")["TOHIT"]!.GetValue<int>());
    }

    [Fact]
    public void Block_Passthrough75CapAndRunningThird()
    {
        // (70+0+30)×(56−15)/(2×10) = 1925 → capped 75; running halves the cap third AFTER it (P1 §1.5).
        Assert.Equal(75, RunVerb("block 70 0 30 56 10 0")["BLOCK"]!.GetValue<int>());
        Assert.Equal(25, RunVerb("block 70 0 30 56 10 1")["BLOCK"]!.GetValue<int>());
        Assert.Equal(0, RunVerb("block 0 0 30 100 10 0")["BLOCK"]!.GetValue<int>()); // no shield, no block
        Assert.Equal(25, RunVerb("block 25 0 0 35 10 0")["BLOCK"]!.GetValue<int>()); // 25×20/20 exact
    }

    [Fact]
    public void Cost_PassthroughOfVendorBuyMath()
    {
        var bases = ItemBaseTable.Load(Path.Combine(TestData.DataDir(), "item-bases.json"));
        var row = bases.For("szpula");
        var plain = RunVerb("cost szpula");
        Assert.Equal(row.Cost, plain["COST"]!.GetValue<int>());
        Assert.Equal(Vendor.BuyPrice(row, Vendor.Standard), plain["PRICE"]!.GetValue<long>());
        // stack ×2, then −50 %: BuyPrice is the only math on the line.
        Assert.Equal(Vendor.BuyPrice(row, Vendor.Standard, 2, 1024, 50),
            RunVerb("cost szpula 2 50")["PRICE"]!.GetValue<long>());
    }

    [Fact]
    public void Gambleroll_IlvlsStayInsideTheClampAndRepeatOnFixedSeed()
    {
        var first = RunVerb("gambleroll 50 500 7");
        var again = RunVerb("gambleroll 50 500 7");
        Assert.Equal(first.ToJsonString(), again.ToJsonString()); // seeded stream: pure passthrough
        var ilvls = first["ILVLS"]!.AsArray().Select(n => n!.GetValue<int>()).ToList();
        Assert.Equal(500, ilvls.Count);
        Assert.All(ilvls, i => Assert.InRange(i, 45, 54)); // clamp(clvl−5+0..9, 5, 99), P2 §4.7
        Assert.Contains(45, ilvls);
        Assert.Contains(54, ilvls);
        Assert.All(RunVerb("gambleroll 1 20 3")["ILVLS"]!.AsArray(), i => Assert.Equal(5, i!.GetValue<int>())); // low clamp
    }

    [Fact]
    public void Xpshare_PotIs87Over256IntegerMath_AndSharesConserve()
    {
        // pot(1000, 4) = 1000×(256+87×3)/256 = 2019; each share floors below the pot (P3 §6.6).
        var line = RunVerb("xpshare 1000 1 5 5 5 5");
        Assert.Equal(1000L * (256 + 87 * 3) / 256, line["POT"]!.GetValue<long>());
        var shares = line["SHARES"]!.AsArray().Select(n => n!.GetValue<long>()).ToList();
        Assert.Equal(4, shares.Count);
        Assert.Equal(shares.Sum(), line["SHARESUM"]!.GetValue<long>());
        Assert.True(line["SHARESUM"]!.GetValue<long>() <= line["POT"]!.GetValue<long>());
        // one member: no bonus, pot is the kill XP itself
        Assert.Equal(1000L, RunVerb("xpshare 1000 1 5")["POT"]!.GetValue<long>());
    }

    [Fact]
    public void Deathpen_ThreeDifficultiesCostZeroFiveTenOfTheLevelSpan()
    {
        var ladder = TestData.Ladder();
        const long cur = 3750 + 4125 / 2; // mid of level 4's span 7875−3750 (P3 §6.6 reading)
        foreach (int d in new[] { 0, 1, 2 })
        {
            var expected = DeathPenalty.Apply(ladder, cur, 4, (Difficulty)d);
            var line = RunVerb($"deathpen {d} {cur} 4");
            Assert.Equal(expected.XpAfter, line["XPAFTER"]!.GetValue<long>());
            Assert.Equal(expected.Lost, line["LOST"]!.GetValue<long>());
            Assert.Equal(expected.CorpseXp, line["CORPSEXP"]!.GetValue<long>()); // ¾ of the loss
            Assert.Equal(0, RunVerb($"deathpen {d} 0 1")["LOST"]!.GetValue<long>()); // level 1 loses nothing
        }
    }

    [Fact]
    public void Qualityroll_CountsSumToTrials_AndAreSeedDeterministic()
    {
        var first = RunVerb("qualityroll 60 0 2000 11");
        Assert.Equal(RunVerb("qualityroll 60 0 2000 11").ToJsonString(), first.ToJsonString());
        var total = new[] { "UNIQUE", "RARE", "MAGIC", "NORMAL" }
            .Sum(k => first[k]!.GetValue<int>());
        Assert.Equal(2000, total);
        // ilvl 60 on a qlvl-5 base: with the table's divisors NO row auto-succeeds —
        // the white family dominates and magic stays a few percent (P2 §4.1).
        Assert.True(first["NORMAL"]!.GetValue<int>() > first["MAGIC"]!.GetValue<int>(),
            "no row auto-succeeds at ilvl 60: white must outrun magic");
    }

    [Fact]
    public void DebugVerb_BadArity_StillOwnsOneErrorLine()
    {
        var script = WriteTempScript("tohit 100 100 10\n");
        try
        {
            var output = new StringWriter();
            int exit = CommandRunner.Run(script, Seed, TestData.DataDir(), output);
            Assert.NotEqual(0, exit);
            Assert.Contains("\"ERROR\"", output.ToString());
        }
        finally { File.Delete(script); }
    }
}
