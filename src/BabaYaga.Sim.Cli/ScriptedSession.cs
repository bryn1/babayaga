using System.Text.Json.Nodes;
using BabaYaga.Sim;

namespace BabaYaga.Sim.Cli;

/// <summary>
/// Maps script lines onto one GameSession and turns each result into the JSON
/// payload of its output line. Grammar (one command per line; '#' starts a
/// comment, blank lines are ignored):
///   new &lt;class&gt; | levelto &lt;n&gt; | spend &lt;skill&gt; [n] | attack &lt;monster&gt; [times] |
///   special &lt;monster&gt; | loot [n] | equip lastdrop | buy &lt;item&gt; | sell last | gamble |
///   waypoint &lt;node&gt; | discover &lt;node&gt; | quest &lt;id&gt; accept|step|complete | save &lt;path&gt; | load &lt;path&gt; | dump
/// A rejected change or unknown command throws — the runner owns the ERROR line.
/// </summary>
public sealed class ScriptedSession
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
            default: return Running(payload, parts);
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
            _ => throw new SessionRejectedException($"unknown command '{parts[0]}'"),
        };

    private Dictionary<string, object?> LevelTo(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 2, "levelto <n>");
        S.LevelTo(Num(parts, 1, 0, "levelto")); // Arg already guarantees the arg exists
        p["LEVEL"] = S.Level;
        p["STATPOINTS"] = S.Character.StatPoints;
        p["SKILLPOINTS"] = S.Character.SkillPoints;
        return p;
    }

    private Dictionary<string, object?> Spend(Dictionary<string, object?> p, string[] parts)
    {
        ArgCount(parts, "spend <skill> [n]", 2, 3);
        int times = Num(parts, 2, 1, "spend");
        for (int i = 0; i < times; i++) S.SpendSkill(parts[1]); // SkillSpendRejectedException propagates
        p["SKILL"] = parts[1];
        p["HARDPOINTS"] = S.Skills.HardPoints(parts[1]);
        return p;
    }

    private Dictionary<string, object?> Special(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 2, "special <monster>");
        var r = S.Special(parts[1]); // seeded 4:1 group; boss rows are refused
        p["KIND"] = r.Kind;
        p["MONSTER"] = parts[1];
        p["UNITS"] = r.Units;
        p["AFFIXES"] = r.Affixes;
        p["MINION-AFFIXES"] = r.MinionAffixes;
        p["LIFE"] = r.Life;
        p["XP"] = r.BaseXp;
        return p;
    }

    private Dictionary<string, object?> Attack(Dictionary<string, object?> p, string[] parts)
    {
        ArgCount(parts, "attack <monster> [times]", 2, 3);
        var r = S.Attack(parts[1], Num(parts, 2, 1, "attack"));
        p["MONSTER"] = parts[1];
        p["STRIKES"] = r.Strikes;
        p["KILLS"] = r.Kills;
        p["XP"] = r.Xp;
        p["LEVEL"] = S.Level;
        p["DROPS_PENDING"] = r.DropsPending;
        p["GOLD_PENDING"] = r.GoldPending;
        return p;
    }

    private Dictionary<string, object?> Loot(Dictionary<string, object?> p, string[] parts)
    {
        ArgCount(parts, "loot [n]", 1, 2);
        int count = Num(parts, 1, 99, "loot");
        var r = S.Loot(count);
        p["TAKEN"] = r.Taken;
        p["GOLD"] = r.GoldCollected;
        p["GOLD_TOTAL"] = S.Gold;
        p["ITEMS"] = r.Items.Select(i => (object?)new string[] { i.DisplayName, i.Quality.ToString() }).ToArray();
        // The DoD-c marker: items at magic tier or above among this loot call.
        p["MagicPlus"] = r.Items.Count(i => i.Quality >= ItemQuality.Magic);
        return p;
    }

    private Dictionary<string, object?> Equip(Dictionary<string, object?> p, string[] parts)
    {
        if (parts.Length != 2 || parts[1] != "lastdrop")
            throw new SessionRejectedException("equip takes exactly one form: 'equip lastdrop'");
        var item = S.EquipLastDrop();
        var slot = S.Equipment.Equipped.First(kv => ReferenceEquals(kv.Value, item)).Key;
        p["ITEM"] = item.DisplayName;
        p["SLOT"] = slot.ToString();
        return p;
    }

    private Dictionary<string, object?> Buy(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 2, "buy <item>");
        p["PRICE"] = S.Buy(parts[1]);
        p["GOLD"] = S.Gold;
        return p;
    }

    private Dictionary<string, object?> Sell(Dictionary<string, object?> p, string[] parts)
    {
        if (parts.Length != 2 || parts[1] != "last")
            throw new SessionRejectedException("sell takes exactly one form: 'sell last'");
        p["PRICE"] = S.SellLast();
        p["GOLD"] = S.Gold;
        return p;
    }

    private Dictionary<string, object?> Gamble(Dictionary<string, object?> p)
    {
        var (price, quality) = S.Gamble();
        p["PRICE"] = price;
        p["QUALITY"] = quality.ToString();
        p["GOLD"] = S.Gold;
        return p;
    }

    private Dictionary<string, object?> Waypoint(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 2, "waypoint <node>");
        S.TravelTo(parts[1]);
        p["AREA"] = S.CurrentArea;
        return p;
    }

    private Dictionary<string, object?> Discover(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 2, "discover <node>");
        S.DiscoverWaypoint(parts[1]);
        p["STONE"] = parts[1];
        return p;
    }

    private Dictionary<string, object?> Quest(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 3, "quest <id> accept|step|complete");
        switch (parts[2])
        {
            case "accept": S.QuestAccept(parts[1]); break;
            case "step": S.QuestStep(parts[1]); break;
            case "complete": S.QuestComplete(parts[1]); break;
            default: throw new SessionRejectedException($"unknown quest mode '{parts[2]}'");
        }
        p["QUEST"] = parts[1];
        p["MODE"] = parts[2];
        p["PHASE"] = S.Quests.PhaseOf(parts[1]).ToString();
        return p;
    }

    private Dictionary<string, object?> Save(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 2, "save <path>");
        S.Save(parts[1]);
        p["PATH"] = parts[1];
        return p;
    }

    private Dictionary<string, object?> Dump(Dictionary<string, object?> p)
    {
        p["STATE"] = JsonNode.Parse(S.Dump());
        return p;
    }

    private GameSession S =>
        _session ?? throw new SessionRejectedException("no session yet — the first command must be 'new <class>'");

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
