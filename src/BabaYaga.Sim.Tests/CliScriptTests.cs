using System.Text.Json.Nodes;
using BabaYaga.Sim.Cli;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// The CLI contract (docs/ARCHITECTURE.md: tests drive stdout markers and exit
/// codes ONLY): the scripted journey in tests/journey.txt runs to {"RESULT":"OK"}
/// with exit 0 and byte-identical save/load dumps, and an unknown command or a
/// rejected state change prints an {"ERROR":…} line and exits nonzero.
/// </summary>
public class CliScriptTests
{
    /// Seed the DoD journey run uses — chosen so the scripted loot holds a magic-or-better drop.
    public const ulong JourneySeed = 2;

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BabaYaga.sln")))
            dir = dir.Parent;
        if (dir is null) throw new DirectoryNotFoundException("no repo root above the test assembly");
        return Path.Combine([dir.FullName, .. parts]);
    }

    private static (int Exit, List<string> Lines) Run(string scriptPath)
    {
        var output = new StringWriter();
        int exit = CommandRunner.Run(scriptPath, JourneySeed, TestData.DataDir(), output);
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        return (exit, lines);
    }

    [Fact]
    public void JourneyScript_RunsToResultOk_WithIdenticalDumps()
    {
        var (exit, lines) = Run(RepoFile("tests", "journey.txt"));
        Assert.Equal(0, exit);
        Assert.Equal("""{"RESULT":"OK"}""", lines[^1]);

        var dumps = lines
            .Select(l => JsonNode.Parse(l)!)
            .Where(n => n["CMD"]?.GetValue<string>() == "dump")
            .ToList();
        Assert.Equal(2, dumps.Count);
        // save → dump → load → dump through the CLI: STATE byte-identical.
        Assert.Equal(dumps[0]["STATE"]!.ToJsonString(), dumps[1]["STATE"]!.ToJsonString());
    }

    [Fact]
    public void JourneyScript_LootHoldsAMagicOrBetterDrop()
    {
        var (exit, lines) = Run(RepoFile("tests", "journey.txt"));
        Assert.Equal(0, exit);
        var loot = lines.Select(l => JsonNode.Parse(l)!).First(n => n!["CMD"]!.GetValue<string>() == "loot");
        Assert.True(loot["MagicPlus"]!.GetValue<int>() >= 1,
            "the journey seed must yield a magic-or-better drop (DoD c)");
    }

    /// <summary>DA round-1 P1-2: the quest SkillPoint reward must land in the real
    /// spendable pool — this is the orchestrator's probe verbatim (exit 0, last
    /// spend lands kolo at HARDPOINTS:2; pre-fix it died on 'no skill point').</summary>
    [Fact]
    public void QuestSkillPointProbe_SpendsTheRewardPoint_AfterGesiComplete()
    {
        var (exit, lines) = Run(RepoFile("tests", "probe-p12.txt"));
        Assert.Equal(0, exit);
        var lastSpend = JsonNode.Parse(lines[^2])!;
        Assert.Equal("spend", lastSpend["CMD"]!.GetValue<string>());
        Assert.Equal("kolo", lastSpend["SKILL"]!.GetValue<string>());
        Assert.Equal(2, lastSpend["HARDPOINTS"]!.GetValue<int>());
    }

    /// <summary>DA round-1 P1-1: a mid-session save/load continues the RNG streams
    /// — (b) the post-load kill/loot lines EQUAL the same-position lines of one
    /// uninterrupted run, and (a) they are NOT a replay: neither the pre-save
    /// batch's own lines nor the same commands replayed from the seed head.</summary>
    [Fact]
    public void SaveLoadMidway_ContinuesRngStreams_DoesNotReplay()
    {
        var save = Path.Combine(Path.GetTempPath(), $"cont-{Guid.NewGuid():N}.json");
        var full = WriteTempScript($"""
            new koldun
            levelto 5
            attack pokrzywnik 3
            loot 99
            save {save}
            attack pokrzywnik 6
            loot 99
            """);
        var continuation = WriteTempScript($"""
            load {save}
            attack pokrzywnik 6
            loot 99
            """);
        var replay = WriteTempScript("""
            new koldun
            levelto 5
            attack pokrzywnik 6
            loot 99
            """);
        try
        {
            var (exitF, linesF) = Run(full);
            var (exitC, linesC) = Run(continuation);
            var (exitR, linesR) = Run(replay);
            Assert.Equal(0, exitF); Assert.Equal(0, exitC); Assert.Equal(0, exitR);
            // Uninterrupted: new, levelto, attack, loot, save, attack₂, loot₂, RESULT.
            var uninterruptedTail = linesF.Skip(5).Take(2).ToList();
            // Continuation: load, attack₂, loot₂, RESULT — byte-equal same-position lines.
            Assert.Equal(uninterruptedTail, linesC.Skip(1).Take(2).ToList());
            // (a) NOT a replay: the loot₂ line differs from the pre-save batch's
            // loot line AND from the same commands run from the seed head.
            var lootC = linesC[2];
            Assert.NotEqual(linesF[3], lootC);  // not the pre-save batch replayed
            Assert.NotEqual(linesR[3], lootC);  // not the seed-head stream replayed
        }
        finally
        {
            File.Delete(save); File.Delete(full); File.Delete(continuation); File.Delete(replay);
        }
    }

    /// <summary>DA round-1 P2-a: CLI verb 'special &lt;monster&gt;' rolls the 4:1
    /// unique:champion group and engages its bonus-scaled leader, so the band
    /// bonus tables are live: LIFE/XP exceed the plain pokrzywnik row (16×90 %
    /// life = 14, xpValue 40) by any band multiplier ≥ 3.</summary>
    [Fact]
    public void SpecialVerb_RollsSeededGroup_BonusScaled_AndRepeatsOnSameSeed()
    {
        var script = WriteTempScript("""
            new koldun
            special pokrzywnik
            attack pokrzywnik 4
            """);
        try
        {
            List<string> RunOnce()
            {
                var (exit, lines) = Run(script);
                Assert.Equal(0, exit);
                return lines;
            }
            var lines = RunOnce();
            Assert.Equal(RunOnce(), lines); // same seed, same stream → same pack
            var special = JsonNode.Parse(lines[1])!;
            Assert.Contains(special["KIND"]!.GetValue<string>(), new[] { "Champion", "Unique" });
            var life = special["LIFE"]!.GetValue<long>();
            var xp = special["XP"]!.GetValue<long>();
            Assert.True(life > 14, $"special life {life} must exceed plain 14");
            Assert.True(xp >= 120, $"special xp {xp} must be ≥ 3× the plain 40");
        }
        finally { File.Delete(script); }
    }

    private static string WriteTempScript(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, body);
        return path;
    }

    [Fact]
    public void UnknownCommand_PrintsErrorLine_AndExitsNonzero()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "new koldun\nfrobnicate the widget\n");
        try
        {
            var (exit, lines) = Run(path);
            Assert.NotEqual(0, exit);
            Assert.Contains("\"ERROR\"", lines[^1]);
            Assert.DoesNotContain(lines, l => l.Contains("RESULT"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RejectedStateChange_PrintsErrorLine_AndExitsNonzero()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}.txt");
        // mchy is dark: waypoint travel to an unlit stone must be refused, not silently skipped.
        File.WriteAllText(path, "new koldun\nwaypoint mchy\n");
        try
        {
            var (exit, lines) = Run(path);
            Assert.NotEqual(0, exit);
            Assert.Contains("\"ERROR\"", lines[^1]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CommandBeforeNew_IsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "loot 3\n");
        try
        {
            var (exit, lines) = Run(path);
            Assert.NotEqual(0, exit);
            Assert.Contains("\"ERROR\"", lines[^1]);
        }
        finally { File.Delete(path); }
    }
}
