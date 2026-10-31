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
