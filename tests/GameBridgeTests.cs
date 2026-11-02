using System.Text.Json;
using BabaYaga.Sim;
using BabaYaga.Sim.Cli;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// M5-S3 SIM BRIDGE (MC 11934, plan-c2 S3): SessionClock frame math + flush
/// decisions and the CommandLog CLI-grammar log, both exercised WITHOUT Godot —
/// the bridge files are pure (no engine types), the .Process glue stays thin.
/// The replay gate (the S3 heart) is sealed here as the CLI leg: a REAL
/// CommandLog generated in-process (head "new koldun", real SessionClock pumps
/// as "rest &lt;k&gt;" lines, a dead-click REJECT comment, respawn) replays
/// through CommandRunner at the same seed to a dump BYTE-IDENTICAL to the hand
/// script of the same action list; the planted RED (swapped lines) proves that
/// gate can go red. DA F4 law honored: the log head synthesizes "new &lt;class&gt;"
/// and the clock feeds land IN ORDER as rest lines; rejected calls ride as
/// '#' comments, which the CLI runner already skips. The game-side half of the
/// replay proof (a BABA_LOG-on headless boot replayed here) lives in the run's
/// out/evidence-m5-bridge.md — the grammar the log writes is the SAME strings
/// the exact-string rows below pin.
/// </summary>
public class GameBridgeTests
{
    private const ulong Seed = 42;

    /// <summary>The hand-authored twin of the generated log (verified exit 0
    /// against the CLI at seed 42 before these rows were written).</summary>
    private static readonly string[] NativeScript =
    [
        "new koldun", "levelto 3", "spend iskra", "buy napitek-zycia",
        "belt_add 0 1", "rest 50", "attack pokrzywnik 12", "loot 20",
        "use 1", "cast iskra pokrzywnik", "rest 50", "attack pokrzywnik 1024",
        "respawn", "rest 10", "dump",
    ];

    // ---- frame math: accumulation, whole-frame flush, catch-up clamp ----

    [Fact]
    public void ClockConstants_ReuseTheSimFrameDoor()
    {
        Assert.Equal(GameSession.FramesPerSecond, SessionClock.FramesPerSecond); // 25 — no fourth constant
        Assert.Equal(50, SessionClock.MaxCatchupFrames); // plan-c2 clamp: 2 s @ 25 Hz per flush
    }

    [Fact]
    public void Advance_PartialFrames_AccumulateAndFlushOnce()
    {
        var clock = new SessionClock();
        Assert.Equal(0, clock.Advance(0.01, alive: true)); // a quarter frame: nothing yet
        Assert.Equal(0, clock.FlushCount);
        Assert.Equal(1, clock.Advance(0.03, alive: true)); // 0.04 s = one whole frame
        Assert.Equal(1, clock.FlushedFrames);
        Assert.Equal(1, clock.FlushCount); // N partial ticks, ONE flush
    }

    [Fact]
    public void Advance_NWholeFramesInOneTick_FlushAsOneBatch()
    {
        var clock = new SessionClock();
        Assert.Equal(25, clock.Advance(1.0, alive: true)); // 1 s = 25 frames in one batch
        Assert.Equal(25, clock.FlushedFrames);
        Assert.Equal(1, clock.FlushCount); // ONE RestTick(25), not 25 calls
        Assert.Equal(0, clock.Advance(0.0, alive: true)); // the remainder carries exactly
    }

    [Fact]
    public void Advance_CatchupClampsAtFiftyFrames_DroppingExcess()
    {
        var clock = new SessionClock();
        Assert.Equal(50, clock.Advance(10.0, alive: true)); // a tab-out of minutes pays 2 s, no more
        Assert.Equal(50, clock.FlushedFrames); // the 200-frame excess is DROPPED, never banked
        Assert.Equal(0, clock.Advance(0.0, alive: true)); // and it never comes back
    }

    // ---- dead guard: death freezes, respawn resumes (plan-c2 FIX-DA5 8) ----

    [Fact]
    public void Advance_WhileDead_FlushesNothing_CountersFreeze()
    {
        var clock = new SessionClock();
        Assert.Equal(50, clock.Advance(2.0, alive: true)); // alive: 2 s pays 50
        long frames = clock.FlushedFrames;
        Assert.Equal(0, clock.Advance(1.0, alive: false)); // dead: a whole second flushes nothing
        Assert.Equal(0, clock.Advance(10.0, alive: false)); // dead time is never owed
        Assert.Equal(frames, clock.FlushedFrames); // counter frozen — death is not regressing
        Assert.Equal(1, clock.FlushCount); // no new flush happened
        Assert.True(clock.Frozen); // the latch the HUD hint edge reads
        Assert.Equal(50, clock.Advance(2.0, alive: true)); // respawn resumes at the frozen instant
        Assert.False(clock.Frozen);
        Assert.Equal(100, clock.FlushedFrames); // only live time counts
    }

    [Fact]
    public void Pump_WhileDead_DoesNotTouchTheSessionOrTheLog()
    {
        var s = GameSession.New(Seed, "koldun", TestData.DataDir());
        s.Attack("pokrzywnik", 1024); // the scripted-death idiom: the volley halts at the DEAD gate
        Assert.True(s.IsDead);
        var logPath = TempPath("deadpump");
        CommandLog.Ambient = new CommandLog(logPath);
        try
        {
            var clock = new SessionClock();
            var mana = s.Character.Mana;
            Assert.Equal(0, SessionClock.Pump(clock, s, 10.0)); // outer guard: NO RaiseIfDead storm
            Assert.Equal(mana, s.Character.Mana); // mana regen frozen (RestTick is the only mover)
            Assert.False(File.Exists(logPath)); // a dead clock writes not even a rest line
        }
        finally
        {
            CommandLog.Ambient = null;
            File.Delete(logPath);
        }
    }

    // ---- log format: the exact CLI grammar lines (DA F4 law) ----

    [Fact]
    public void Log_HeadAndVerbLines_AreExactCliGrammarStrings()
    {
        var path = TempPath("grammar");
        try
        {
            var log = new CommandLog(path);
            log.Open("new koldun"); // the F4-synthesized head, verbatim
            log.Record("rest 10");
            log.Record("belt_add 0 1");
            log.Record("use 1");
            log.Record("cast iskra pokrzywnik");
            log.Record("respawn");
            Assert.Equal(
                "new koldun\nrest 10\nbelt_add 0 1\nuse 1\ncast iskra pokrzywnik\nrespawn\n",
                File.ReadAllText(path)); // append-only, one line per call, trailing newline
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Log_RejectedCall_RidesAsCommentLine_AndCallRethrows()
    {
        var path = TempPath("reject");
        CommandLog.Ambient = new CommandLog(path);
        try
        {
            CommandLog.Ambient.Open("new koldun");
            var thrown = Record.Exception(() => CommandLog.Call(
                "attack pokrzywnik 1", () => throw new SessionRejectedException("you are dead")));
            Assert.IsType<SessionRejectedException>(thrown); // the HUD hint idiom still gets it
            Assert.Equal("new koldun\n# attack pokrzywnik 1 \u2192 REJECT:you are dead\n",
                File.ReadAllText(path)); // replay skips '#' natively — the filter IS the grammar
        }
        finally
        {
            CommandLog.Ambient = null;
            File.Delete(path);
        }
    }

    [Fact]
    public void Call_NoAmbient_RunsTheDoorAndWritesNothing()
    {
        var path = TempPath("off");
        Environment.SetEnvironmentVariable(CommandLog.EnvVar, path);
        try
        {
            CommandLog.Ambient = null; // bridge OFF: the default
            var ran = false;
            CommandLog.Call("use 1", () => ran = true); // the call still lands
            Assert.True(ran);
            Assert.False(File.Exists(path)); // zero file writes — story bytes untouched
        }
        finally { Environment.SetEnvironmentVariable(CommandLog.EnvVar, null); }
    }

    [Fact]
    public void FromEnvironment_GatesOnBabaLog()
    {
        Assert.False(CommandLog.FromEnvironment().Enabled); // unset: OFF by default
        var path = TempPath("env");
        Environment.SetEnvironmentVariable(CommandLog.EnvVar, path);
        try
        {
            var log = CommandLog.FromEnvironment();
            Assert.True(log.Enabled);
            Assert.Equal(path, log.Path);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CommandLog.EnvVar, null);
            File.Delete(path);
        }
    }

    // ---- the replay gate: a generated log replays to the hand script's dump ----

    [Fact]
    public void GeneratedLog_ReplaysToByteIdenticalDump_OfTheHandScript()
    {
        var (logPath, _) = RunBridgeSession(TempPath("genlog"));
        var (nativeExit, nativeOut) = RunScript(NativeScript);
        var (replayExit, replayOut) = RunScript(File.ReadAllLines(logPath));
        try
        {
            Assert.Equal(0, nativeExit);
            Assert.Equal(0, replayExit); // every logged line is executable grammar (rejects rode comments)
            Assert.Equal(DumpLine(nativeOut), DumpLine(replayOut)); // THE gate: byte-identical dump
            Assert.Equal(nativeOut, replayOut); // stronger than the DoD: whole transcripts equal
        }
        finally { File.Delete(logPath); }
    }

    [Fact]
    public void GeneratedLog_PlantedRed_SwappedLinesTurnTheGateRed()
    {
        var (logPath, _) = RunBridgeSession(TempPath("redlog"));
        var lines = File.ReadAllLines(logPath).ToList();
        var belt = lines.IndexOf("belt_add 0 1");
        var use = lines.IndexOf("use 1");
        (lines[belt], lines[use]) = (lines[use], lines[belt]); // the quaff BEFORE its potion is belted
        var nativeDump = DumpLine(RunScript(NativeScript).Output);
        var swapped = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(swapped, lines);
            var (swappedExit, swappedOut) = RunScript(lines);
            // Deterministically red: the empty slot rejects (exit 1) or the dump diverges.
            Assert.NotEqual(nativeDump, swappedExit == 0 ? DumpLine(swappedOut) : "EXIT-" + swappedExit);
        }
        finally { File.Delete(logPath); File.Delete(swapped); }
    }

    // ---- helpers ----

    /// <summary>Drives the bridge the way the windowed game does — the real
    /// CommandLog + SessionClock, no Godot — and returns the log path plus the
    /// session's own final dump string. Same action list as <see cref="NativeScript"/>.</summary>
    private static (string LogPath, string Dump) RunBridgeSession(string logPath)
    {
        var s = GameSession.New(Seed, "koldun", TestData.DataDir());
        var log = new CommandLog(logPath);
        CommandLog.Ambient = log;
        var clock = new SessionClock();
        try
        {
            log.Open("new koldun"); // the head the CLI needs and the game synthesizes (F4)
            CommandLog.Call("levelto 3", () => s.LevelTo(3));
            CommandLog.Call("spend iskra", () => s.SpendSkill("iskra"));
            CommandLog.Call("buy napitek-zycia", () => s.Buy("napitek-zycia"));
            CommandLog.Call("belt_add 0 1", () => s.AddToBelt(0, 1));
            Assert.Equal(50, SessionClock.Pump(clock, s, 2.0)); // pumps land as rest lines, in order
            CommandLog.Call("attack pokrzywnik 12", () => s.Attack("pokrzywnik", 12));
            CommandLog.Call("loot 20", () => s.Loot(20));
            CommandLog.Call("use 1", () => s.UsePotion(1));
            CommandLog.Call("cast iskra pokrzywnik", () => s.Cast("iskra", "pokrzywnik"));
            Assert.Equal(50, SessionClock.Pump(clock, s, 10.0)); // clamped catch-up pays 50
            CommandLog.Call("attack pokrzywnik 1024", () => s.Attack("pokrzywnik", 1024)); // hero dies
            Assert.True(s.IsDead);
            Assert.Equal(0, SessionClock.Pump(clock, s, 2.0)); // dead: the clock freezes
            Assert.Throws<SessionRejectedException>(() => CommandLog.Call(
                "attack pokrzywnik 1", () => s.Attack("pokrzywnik", 1))); // a dead click rides a comment
            CommandLog.Call("respawn", () => s.Respawn());
            Assert.Equal(10, SessionClock.Pump(clock, s, 0.4)); // resume pays 10
            log.Record("dump"); // the log ends on its own dump command
            return (logPath, s.Dump());
        }
        finally { CommandLog.Ambient = null; }
    }

    private static (int Exit, string Output) RunScript(IReadOnlyList<string> lines)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, lines);
            var output = new StringWriter();
            return (CommandRunner.Run(path, Seed, TestData.DataDir(), output), output.ToString());
        }
        finally { File.Delete(path); }
    }

    private static string DumpLine(string output) => output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .First(ln => JsonDocument.Parse(ln).RootElement.GetProperty("CMD").GetString() == "dump");

    private static string TempPath(string tag) => Path.Combine(Path.GetTempPath(), $"bridge-{tag}-{Guid.NewGuid():N}.log");
}
