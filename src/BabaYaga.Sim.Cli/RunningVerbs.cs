using System.Text.Json.Nodes;
using BabaYaga.Sim;

namespace BabaYaga.Sim.Cli;

/// <summary>
/// The Running(...) case-map beats, split out of ScriptedSession.cs like
/// DebugVerbs.cs and EngineDoorVerbs.cs before it (audit MC 11907 §5 S6):
/// one handler per live-session verb line, each building its JSON payload
/// from the session's own reads. The case-map, the session door (S) and the
/// arity helpers stay with the grammar in ScriptedSession.cs.
/// </summary>
public sealed partial class ScriptedSession
{
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
        if (times < 1) throw new SessionRejectedException("spend times must be at least 1"); // DA P4-1, as attack/loot
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

    /// equip takes TWO forms (MC 11952): 'equip lastdrop' — the original verb,
    /// unchanged; 'equip &lt;x&gt; &lt;y&gt;' — the thin twin of the hero's cell-addressable
    /// EquipFromCell door (the EngineDoorVerbs idiom: every gate — DEAD first,
    /// the grid range, the anchor, the wearable law, the exchange room — is the
    /// ENGINE'S and arrives as the named ERROR line; the dispatch rolls nothing).
    /// The cell echo reads the door's own report (the gear-anchors idiom).
    private Dictionary<string, object?> Equip(Dictionary<string, object?> p, string[] parts)
    {
        if (parts.Length == 2 && parts[1] == "lastdrop")
        {
            var item = S.EquipLastDrop();
            var slot = S.Equipment.Equipped.First(kv => ReferenceEquals(kv.Value, item)).Key;
            p["ITEM"] = item.DisplayName;
            p["SLOT"] = slot.ToString();
            return p;
        }
        if (parts.Length == 3)
        {
            var r = S.EquipFromCell(Num(parts, 1, 0, "equip"), Num(parts, 2, 0, "equip"));
            p["SLOT"] = r.Slot;
            p["BASE"] = r.Base;
            p["ITEM"] = r.Name;
            p["INV_ROW"] = r.InvRow;
            p["INV_COL"] = r.InvCol;
            if (r.ExchangedBase is not null) p["EXCHANGED"] = $"{r.ExchangedName}@{r.ExchangedRow},{r.ExchangedCol}";
            return p;
        }
        throw new SessionRejectedException("equip takes 'equip lastdrop' or 'equip <x> <y>' (the pack cell)");
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
}
