using System;
using System.IO;
using BabaYaga.Sim;

/// <summary>
/// M5-S3 SIM BRIDGE, the COMMAND LOG (MC 11934, plan-c2 S3; DA F4 log-shape
/// law): every game→sim call is recorded as ONE line in the CLI grammar itself
/// (head "new &lt;class&gt;", the S3a twins' verbs, the clock's feeds as
/// "rest &lt;k&gt;" lines IN ORDER) — the 1:1 translation table IS the format,
/// which REPLACES v1's unspecified "log the call" wording. A rejected call
/// rides as a '#' comment ("<verb args> → REJECT:reason"): the CLI runner
/// already skips comments, so the filter IS the grammar — deterministic fields
/// only, never a second parallel translation mechanism.
///
/// ENV-GATED AND DEFAULT-OFF: the path comes from BABA_LOG; unset, Ambient
/// stays null and <see cref="Call"/> is a plain pass-through — zero file
/// writes, story bytes untouched (the headless anchors prove it). Open()
/// truncates: one New session, one log. This file is PURE — no Godot types,
/// so the xunit gate (tests/GameBridgeTests.cs) covers the whole format.
/// </summary>
public sealed class CommandLog
{
    /// <summary>The one switch: set it to a file path to turn the bridge on.</summary>
    public const string EnvVar = "BABA_LOG";

    /// <summary>The ambient log the game call sites write through — null while OFF.</summary>
    public static CommandLog? Ambient;

    public string? Path { get; }
    public bool Enabled => Path is not null;

    public CommandLog(string? path) => Path = string.IsNullOrEmpty(path) ? null : path;

    /// <summary>The env-gated factory: OFF (disabled, inert) unless BABA_LOG says a path.</summary>
    public static CommandLog FromEnvironment() =>
        new(Environment.GetEnvironmentVariable(EnvVar));

    /// <summary>Starts the log fresh with its head line (the CLI "new &lt;class&gt;"
    /// the session creation synthesizes — DA F4: creation precedes the log).</summary>
    public void Open(string headLine)
    {
        if (Enabled) File.WriteAllText(Path!, headLine + "\n");
    }

    /// <summary>Appends one grammar line, append-only.</summary>
    public void Record(string verbLine)
    {
        if (Enabled) File.AppendAllText(Path!, verbLine + "\n");
    }

    /// <summary>A rejection is history, not replay: the line lands commented,
    /// with the engine's own reason attached (a deterministic string).</summary>
    public void RecordRejected(string verbLine, string reason) =>
        Record($"# {verbLine} \u2192 REJECT:{reason}");

    /// <summary>Runs one door call and records it: the plain line on success,
    /// the comment line on the engine's named rejection — the rejection then
    /// rethrows UNCHANGED, so the HUD hint idiom at every call site survives
    /// untouched. With no Ambient the call is exactly the call: zero writes.</summary>
    public static void Call(string verbLine, Action doorCall)
    {
        var log = Ambient;
        if (log is null) { doorCall(); return; }
        try
        {
            doorCall();
            log.Record(verbLine);
        }
        catch (SessionRejectedException e)
        {
            log.RecordRejected(verbLine, e.Message);
            throw;
        }
    }
}
