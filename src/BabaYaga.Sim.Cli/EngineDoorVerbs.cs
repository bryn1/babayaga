using BabaYaga.Sim;

namespace BabaYaga.Sim.Cli;

/// <summary>
/// The engine-door verbs (MC 11896, gate P4 debts from MC 11880/11890/11895):
/// the scripted terminal grew these no-verbs while the sim grew the doors —
/// rest, hire, dismiss, revive, stash_store, stash_take — and the S15 gear
/// doors joined them (MC 11903): give, strip. Each verb is a THIN
/// call onto an EXISTING public session door (RestTick / Hire / Dismiss /
/// Revive / Store / Take / GiveHirelingGear / StripHirelingGear); the CLI owns
/// NO sim semantics here — every gate
/// (DEAD first, town, tab, coords, purse, the alive/same-area/4-slot/whitelist
/// chain) stays where the engine has it, and
/// a refusal propagates as the engine's named SessionRejectedException, which
/// CommandRunner already turns into the ERROR line + exit 1. Echoes read the
/// door's own report or the pool after the call (the Waypoint-echoes-AREA
/// idiom). Grammar → owning door:
///   rest &lt;frames&gt; → GameSession.RestTick (echoes MANA/MAXMANA after)
///   hire &lt;id&gt; → Hire (PRICE + GOLD after; GEAR = the four slots as worn);
///   dismiss → Dismiss (no refund — and THE FORFEIT: worn gear dies with the
///   entry, §6.7b D3);
///   revive &lt;id&gt; → Revive (the door names its merc — R3's id form; COST + GOLD)
///   stash_store &lt;tab&gt; &lt;x&gt; &lt;y&gt; → Store(x, y, tab); stash_take &lt;tab&gt; &lt;x&gt; &lt;y&gt; → Take —
///   both echo the move report's ANCHORS (tab cells and pack cells).
///   give &lt;slot&gt; &lt;x&gt; &lt;y&gt; → GiveHirelingGear; strip &lt;slot&gt; → StripHirelingGear —
///   both echo the gear report's anchors, the displaced EXCHANGED item when the
///   slot spat one back, and the post-move GEAR line.
/// </summary>
public sealed partial class ScriptedSession
{
    private Dictionary<string, object?> Rest(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 2, "rest <frames>");
        int frames = Num(parts, 1, 0, "rest");
        S.RestTick(frames); // DEAD gate + negative reject are the engine's, by name
        p["FRAMES"] = frames;
        p["MANA"] = S.Character.Mana;
        p["MAXMANA"] = S.Character.MaxMana;
        return p;
    }

    private Dictionary<string, object?> Hire(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 2, "hire <id>");
        var r = S.Hire(parts[1]); // gate order the door owns: DEAD → town → row/act → one-merc → gold
        p["MERC"] = r.Id;
        p["NAME"] = r.Name;
        p["LEVEL"] = r.Level;
        p["PRICE"] = r.Price;
        p["GOLD"] = r.GoldAfter;
        p["LIFE"] = r.Life;
        p["GEAR"] = S.MercGearEcho(); // the four merc slots as worn (a rookie arrives empty-handed)
        return p;
    }

    /// give <slot> <x> <y> → GiveHirelingGear — the §6.7b give door (free, both
    /// directions; every gate — DEAD, exists, ALIVE, same-area, the 4-slot law,
    /// the kind whitelist, the reqs, the exchange room — is the ENGINE'S and
    /// arrives as the named ERROR line. The echo reads the move report (the
    /// stash-anchors idiom) and the post-move gear line.
    private Dictionary<string, object?> Give(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 4, "give <slot> <x> <y>");
        var r = S.GiveHirelingGear(parts[1], Num(parts, 2, 0, "give"), Num(parts, 3, 0, "give"));
        return EchoGearMove(p, r);
    }

    /// strip <slot> → StripHirelingGear — the take-back door, straight into the
    /// pack (same gates, room proven before the slot empties).
    private Dictionary<string, object?> Strip(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 2, "strip <slot>");
        return EchoGearMove(p, S.StripHirelingGear(parts[1]));
    }

    /// The two gear doors share one report — echo its anchors and the displaced
    /// item (EXCHANGED, absent on a plain fill/lift), plus the slot line after.
    private Dictionary<string, object?> EchoGearMove(Dictionary<string, object?> p, GameSession.GearMoveReport r)
    {
        p["SLOT"] = r.Slot;
        p["BASE"] = r.Base;
        p["ITEM"] = r.Name;
        p["INV_ROW"] = r.InvRow;
        p["INV_COL"] = r.InvCol;
        if (r.ExchangedBase is not null) p["EXCHANGED"] = $"{r.ExchangedName}@{r.ExchangedRow},{r.ExchangedCol}";
        p["GEAR"] = S.MercGearEcho();
        return p;
    }

    private Dictionary<string, object?> DismissVerb(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 1, "dismiss");
        p["DISMISSED"] = S.Dismiss(); // free, no refund — the echoed GOLD shows the purse as paid
        p["GOLD"] = S.Gold;
        return p;
    }

    private Dictionary<string, object?> Revive(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 2, "revive <id>");
        var r = S.Revive(parts[1]); // the same merc returns at full life (R3/R5), cost at its own level
        p["MERC"] = r.Id;
        p["COST"] = r.Cost;
        p["GOLD"] = r.GoldAfter;
        p["LIFE"] = r.Life;
        return p;
    }

    private Dictionary<string, object?> StashStore(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 4, "stash_store <tab> <x> <y>");
        int tab = Num(parts, 1, 1, "stash_store"), x = Num(parts, 2, 0, "stash_store"), y = Num(parts, 3, 0, "stash_store");
        return EchoStashMove(p, S.Store(x, y, tab)); // the door takes (invX, invY, tab); the verb reads tab-first
    }

    private Dictionary<string, object?> StashTake(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 4, "stash_take <tab> <x> <y>");
        int tab = Num(parts, 1, 1, "stash_take"), x = Num(parts, 2, 0, "stash_take"), y = Num(parts, 3, 0, "stash_take");
        return EchoStashMove(p, S.Take(tab, x, y));
    }

    /// The two stash doors share one report — echo its anchors verbatim.
    private static Dictionary<string, object?> EchoStashMove(
        Dictionary<string, object?> p, GameSession.StashMoveReport r)
    {
        p["BASE"] = r.Base;
        p["ITEM"] = r.Name;
        p["TAB"] = r.Tab;
        p["TAB_ROW"] = r.TabRow;
        p["TAB_COL"] = r.TabCol;
        p["INV_ROW"] = r.InvRow;
        p["INV_COL"] = r.InvCol;
        return p;
    }
}
