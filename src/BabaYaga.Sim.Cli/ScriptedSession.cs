using System.Text.Json.Nodes;
using BabaYaga.Sim;

namespace BabaYaga.Sim.Cli;

/// <summary>
/// Maps script lines onto one GameSession and turns each result into the JSON
/// payload of its output line. Grammar (one command per line; '#' starts a
/// comment, blank lines are ignored):
///   new &lt;class&gt; | levelto &lt;n&gt; | spend &lt;skill&gt; [n] | attack &lt;monster&gt; [times] |
///   special &lt;monster&gt; | loot [n] | equip lastdrop | equip &lt;x&gt; &lt;y&gt; (hero cell-equip
///   door twin, MC 11952 — the windowed anchor-cell click rides the same line) |
///   buy &lt;item&gt; | sell last | gamble |
///   waypoint &lt;node&gt; | discover &lt;node&gt; | quest &lt;id&gt; accept|step|complete | save &lt;path&gt; | load &lt;path&gt; | dump
///   | rest &lt;frames&gt; | hire &lt;id&gt; | dismiss | revive &lt;id&gt; | stash_store &lt;tab&gt; &lt;x&gt; &lt;y&gt;
///   | stash_take &lt;tab&gt; &lt;x&gt; &lt;y&gt; (engine doors, MC 11896)
///   | give &lt;slot&gt; &lt;x&gt; &lt;y&gt; | strip &lt;slot&gt; (merc gear doors, MC 11903)
///   | belt_add &lt;x&gt; &lt;y&gt; | use &lt;slot&gt; | cast &lt;skill&gt; &lt;monster&gt; | respawn (the M5-S3a
///   replay-gate twins — belt/cast/death doors, MC 11933) — see EngineDoorVerbs.cs
///   | craft &lt;recipeId&gt; (the RITUAL CRAFT door twin, MC 11969 — grammar and echo
///   rows per EngineDoorVerbs.cs; the dispatch layer rolls nothing)
///   | read-only debug verbs (no session) — see DebugVerbs.cs
///   | live-session beats (payload builders) — see RunningVerbs.cs
/// A rejected change or unknown command throws — the runner owns the ERROR line.
/// </summary>
public sealed partial class ScriptedSession
{
    private readonly ulong _seed;
    private readonly string _dataDir;
    private GameSession? _session;

    public ScriptedSession(ulong seed, string dataDir)
    {
        _seed = seed;
        _dataDir = dataDir;
    }

    public Dictionary<string, object?> Execute(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var payload = new Dictionary<string, object?> { ["CMD"] = parts[0] };
        switch (parts[0])
        {
            case "new": return New(payload, parts);
            case "load": return Load(payload, parts);
            default: return DebugVerb(payload, parts) ?? Running(payload, parts);
        }
    }

    private Dictionary<string, object?> New(Dictionary<string, object?> payload, string[] parts)
    {
        if (_session is not null) throw new SessionRejectedException("session already started — use load to replace it");
        Arg(parts, 2, "new <class>");
        _session = GameSession.New(_seed, parts[1], _dataDir); // unknown class → ArgumentException
        payload["CLASS"] = parts[1];
        payload["LEVEL"] = _session.Level;
        payload["AREA"] = _session.CurrentArea;
        payload["GOLD"] = _session.Gold;
        return payload;
    }

    private Dictionary<string, object?> Load(Dictionary<string, object?> payload, string[] parts)
    {
        Arg(parts, 2, "load <path>");
        _session = GameSession.Load(parts[1], _dataDir);
        payload["PATH"] = parts[1];
        payload["LEVEL"] = _session.Level;
        payload["AREA"] = _session.CurrentArea;
        return payload;
    }

    private Dictionary<string, object?> Running(Dictionary<string, object?> payload, string[] parts) =>
        parts[0] switch
        {
            "levelto" => LevelTo(payload, parts),
            "spend" => Spend(payload, parts),
            "attack" => Attack(payload, parts),
            "special" => Special(payload, parts),
            "loot" => Loot(payload, parts),
            "equip" => Equip(payload, parts),
            "buy" => Buy(payload, parts),
            "sell" => Sell(payload, parts),
            "gamble" => Gamble(payload),
            "waypoint" => Waypoint(payload, parts),
            "discover" => Discover(payload, parts),
            "quest" => Quest(payload, parts),
            "save" => Save(payload, parts),
            "dump" => Dump(payload),
            "rest" => Rest(payload, parts),
            "hire" => Hire(payload, parts),
            "dismiss" => DismissVerb(payload, parts),
            "revive" => Revive(payload, parts),
            "stash_store" => StashStore(payload, parts),
            "stash_take" => StashTake(payload, parts),
            "give" => Give(payload, parts),
            "strip" => Strip(payload, parts),
            "belt_add" => BeltAdd(payload, parts),
            "use" => Use(payload, parts),
            "cast" => Cast(payload, parts),
            "respawn" => Respawn(payload, parts),
            "craft" => Craft(payload, parts),
            _ => throw new SessionRejectedException($"unknown command '{parts[0]}'"),
        };

    private GameSession S => _session ?? throw new SessionRejectedException("no session yet — the first command must be 'new <class>'");

    /// Arity check for commands with an optional trailing count argument.
    private static void ArgCount(string[] parts, string grammar, params int[] allowed)
    {
        if (!allowed.Contains(parts.Length))
            throw new SessionRejectedException($"bad arguments '{string.Join(' ', parts)}' (want: {grammar})");
    }

    private static void Arg(string[] parts, int count, string grammar)
    {
        if (parts.Length != count)
            throw new SessionRejectedException($"bad arguments '{string.Join(' ', parts)}' (want: {grammar})");
    }

    /// Parses an optional trailing number, or its fallback when the arg is absent.
    private static int Num(string[] parts, int index, int fallback, string cmd)
    {
        if (index >= parts.Length) return fallback;
        if (!int.TryParse(parts[index], out var n))
            throw new SessionRejectedException($"'{cmd}': '{parts[index]}' is not a number");
        return n;
    }
}
