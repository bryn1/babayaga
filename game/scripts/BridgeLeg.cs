using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

/// <summary>
/// M5-S3 SIM BRIDGE, the headless REPLAY-PROOF leg (MC 11934, DoD 2 game leg):
/// runs ONLY when BABA_LOG names a path (env-gated — with the env off this
/// code never executes and the story bytes are untouched). It opens a FRESH
/// session through GameContext.New (which starts the CommandLog and its
/// "new koldun" head), then performs the same action list the CLI mini-session
/// at seed 42 runs, EVERY call through the same CommandLog.Call wrapper and
/// SessionClock.Pump the windowed game uses — so the lines written ARE what a
/// windowed session writes (the S6 town-panel / S4 input-map call sites carry
/// the identical wrapper at their doors). It then prints the final dump as the
/// CLI's own dump line (byte-shape included) — the evidence script replays the
/// produced log through the CLI and cmp's the two lines byte-for-byte.
/// The scripted death (attack 1024, the volley halting at the DEAD gate), the
/// frozen clock while dead (NO rest lines) and the dead-click comment line are
/// all in-leg: the replay exercises them through the grammar.
/// </summary>
public static class BridgeLeg
{
    public const ulong BridgeSeed = 42; // the brief's seed; printed for the replay's --seed

    public static void Run()
    {
        var session = GameContext.New(BridgeSeed); // fresh session: log truncated, head written
        var clock = new SessionClock();
        GD.Print($"M5_BRIDGE_SEED {BridgeSeed}");

        CommandLog.Call("levelto 3", () => session.LevelTo(3));
        CommandLog.Call("spend iskra", () => session.SpendSkill("iskra"));
        CommandLog.Call("buy napitek-zycia", () => session.Buy("napitek-zycia"));
        CommandLog.Call("belt_add 0 1", () => session.AddToBelt(0, 1));
        _ = SessionClock.Pump(clock, session, 2.0); // rest 50 — the clock feeds, in order
        CommandLog.Call("attack pokrzywnik 12", () => session.Attack("pokrzywnik", 12));
        CommandLog.Call("loot 20", () => session.Loot(20));
        CommandLog.Call("use 1", () => session.UsePotion(1));
        CommandLog.Call("cast iskra pokrzywnik", () => session.Cast("iskra", "pokrzywnik"));
        _ = SessionClock.Pump(clock, session, 10.0); // the clamp pays 50, drops 200
        CommandLog.Call("attack pokrzywnik 1024", () => session.Attack("pokrzywnik", 1024)); // dies here
        _ = SessionClock.Pump(clock, session, 2.0); // dead: the clock freezes, no line lands
        try
        {
            CommandLog.Call("attack pokrzywnik 1", () => session.Attack("pokrzywnik", 1)); // dead click
        }
        catch (BabaYaga.Sim.SessionRejectedException) { /* the windowed hint path; the comment is logged */ }
        CommandLog.Call("respawn", () => session.Respawn());
        _ = SessionClock.Pump(clock, session, 0.4); // resume pays rest 10
        CommandLog.Ambient?.Record("dump"); // the log ends on its own dump command

        GD.Print($"M5_BRIDGE_DUMP {DumpLine(session)}");
        GD.Print("M5_BRIDGE_OK");
    }

    /// <summary>The CLI dump line, byte-shape and all: CommandRunner emits its
    /// lines with the same default JsonSerializerOptions over the same payload
    /// shape ({"CMD":"dump","STATE":&lt;parsed Dump()&gt;}), so the replayed
    /// CLI line and this one compare byte-for-byte. PUBLIC since M5-S4: the
    /// windowed input leg ends on the SAME shape — one dump-line builder.</summary>
    public static string DumpLine(BabaYaga.Sim.GameSession session) =>
        JsonSerializer.Serialize(
            new Dictionary<string, object?> { ["CMD"] = "dump", ["STATE"] = JsonNode.Parse(session.Dump()) },
            new JsonSerializerOptions());
}
