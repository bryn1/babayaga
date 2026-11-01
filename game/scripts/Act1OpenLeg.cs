using System;
using System.Linq;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// The M4-S9b ACT-1-OPENS leg (ORCH-RULING 2026-11-01 + D2 oracle grant, owner
/// verbatim: "all the tests and verifications against diablo 2 lod and continue
/// working towards dod", 2026-10-30) — the two stone-less-side rites go LIVE
/// through the evolved ONE travel door, run ONCE by Boot's headless branch after
/// the cast leg. QUEST 2 Wrzeciono Kikimory: accept in town -> the goal ground
/// pokrzywlasy is REACHED ON FOOT (no standing stone there has ever existed —
/// under the old lit-stone-only door this rite was unreachable, the finding that
/// opened the card) -> `WALK_STEP` with the lit-set read BEFORE and AFTER the
/// hop, equal (walking lights NOTHING) -> the area's data rows spawn (the
/// `MONSTER_SPAWN` lines prove the ADDED rows) -> one kill -> the FSM step taken
/// ON the goal ground -> QuestComplete pays the LifePotion -> `QUEST2_REWARD
/// kind=LifePotion maxlife_before/after` read around the payment, the rise
/// asserted equal to GameSession.PotionOfLifeFlat x the row's amount THROUGH THE
/// CONSTANT PATH. QUEST 3 Dary w Kopcach (its prereq gesi is Done — the quest
/// leg ran earlier this same run): accept in town -> the chain to kopcowisko
/// HOP BY HOP, each WALK_STEP gated on the real linkedTo edge: grodzisko->mchy,
/// mchy->krecie (the walk INTO the dark stone's ground — krecie stays dark, the
/// pure-walk proof the brief's "krecie stone-less" note intended: krecie DOES
/// carry a stone, id 3 Krecia Furta — the walk neither needs nor lights it) ->
/// the order-contract Discover lights krecie (a WALK off dark ground towards
/// dark ground is the ruled lit-endpoint concession, so the leg lights its OWN
/// standing stone before departing — a printed DISCOVER_ORDER line) ->
/// krecie->kopcowisko WALK with kopcowisko DARK (a ride cannot enter a dark
/// stone — only feet can) -> kill -> step ON the goal ground -> QuestComplete
/// pays the WaypointUnlock -> `NEW_STONE area=kopcowisko before=False` pasted
/// BEFORE the call, `lit=True` after (the reward IS the light) -> `RIDE` home
/// over the now-lit stones AND back: kopcowisko<->grodzisko are NOT linkedTo
/// neighbours, so each of those moves is ride-ONLY — the RIDE half of the door
/// proven unchanged (town hub). ACT1_OPEN_OK prints only when every reading
/// held; any failure is a PushError with no marker (the leg idiom, never a crash).
/// </summary>
public static class Act1OpenLeg
{
    private const string Tag = "act-1-open leg"; // the leg's log-voice — LegKit helpers print through it (audit D-2)

    public static void Run(GameSession s, Act act)
    {
        // the cast leg leaves the hero standing in mchy with a refilled pool —
        // the lit stone carries him home (hub ride) before the rites begin.
        if (s.CurrentArea != s.Areas.TownKey)
        {
            GD.Print($"OPEN_START area={s.CurrentArea} -> {s.Areas.TownKey} (lit stones carry the hero home)");
            s.TravelTo(s.Areas.TownKey);
            act.Rebuild(s.CurrentArea);
        }

        bool ok = Wrzeciono(s, act) && Dary(s, act);

        // the leg idiom: the tail always brings the hero home.
        if (s.CurrentArea != s.Areas.TownKey)
        {
            s.TravelTo(s.Areas.TownKey);
            act.Rebuild(s.CurrentArea);
        }

        if (ok) GD.Print("ACT1_OPEN_OK");
        else GD.PushError("act-1-open leg: the rite chain has no ACT1_OPEN_OK — the lines above are the evidence");
    }

    // ---- QUEST 2 — Wrzeciono Kikimory: the stone-less rite, reached on foot ----

    private static bool Wrzeciono(GameSession s, Act act)
    {
        var row = s.Quests.Get("wrzeciono").Row; // goal + reward ride the DATA row
        if (!Accept(s, "wrzeciono")) return false;

        if (!Walk(s, act, row.GoalAreaKey, requireDarkDest: true)) return false;
        if (!LegKit.KillOne(s, act, Tag, "on the goal ground")) return false;
        if (!Step(s, "wrzeciono", row)) return false;

        // the LifePotion pays FlatLife through the constant path — MaxLife is the
        // session's own computed read, before/after around the one call.
        long maxBefore = s.Character.MaxLife;
        if (!Complete(s, "wrzeciono")) return false;
        long maxAfter = s.Character.MaxLife;
        long owed = GameSession.PotionOfLifeFlat * (long)row.RewardAmount;
        GD.Print($"QUEST2_REWARD kind={row.RewardKind} maxlife_before={maxBefore} maxlife_after={maxAfter}");
        if (row.RewardKind != QuestRewardKind.LifePotion || maxAfter - maxBefore != owed)
        {
            GD.PushError($"act-1-open leg: the rite paid {maxAfter - maxBefore}, the constant path owes" +
                $" {owed} (PotionOfLifeFlat={GameSession.PotionOfLifeFlat} x {row.RewardAmount})");
            return false;
        }

        // home on foot again — pokrzywlasy is dark forever, the road's KNOWN end
        // (the town stone) is what opens the door back.
        return Walk(s, act, s.Areas.TownKey, requireDarkDest: false);
    }

    // ---- QUEST 3 — Dary w Kopcach: walked chain, walked-in dark stone, ridden hub ----

    private static bool Dary(GameSession s, Act act)
    {
        var row = s.Quests.Get("dary").Row;
        if (!Accept(s, "dary")) return false; // the prereq gesi is Done — the quest leg ran this same run

        // the chain, hop by hop — every hop an adjacency the AREA GRAPH asserts.
        if (!Walk(s, act, "mchy", requireDarkDest: false)) return false;
        if (!Walk(s, act, "krecie", requireDarkDest: true)) return false; // walk INTO the dark stone

        // The order contract for the NEXT hop: the lit-endpoint concession means
        // a WALK off DARK ground towards DARK ground is refused — so the leg
        // lights the stone it is STANDING ON before departing (discover-BEFORE-
        // travel, never after), a printed honest ordering move.
        if (s.Waypoints.IsDiscovered("krecie"))
        {
            GD.PushError("act-1-open leg: krecie was lit before the order-contract discover — the walk lit it");
            return false;
        }
        s.DiscoverWaypoint("krecie");
        GD.Print($"DISCOVER_ORDER area=krecie lit_before=False lit_after={s.Waypoints.IsDiscovered("krecie")}");

        if (!Walk(s, act, row.GoalAreaKey, requireDarkDest: true)) return false; // walked-in dark kopcowisko
        if (!LegKit.KillOne(s, act, Tag, "on the goal ground")) return false;
        if (!Step(s, "dary", row)) return false;

        // the reward IS the light: the before-state is pasted False BEFORE the
        // completion, the after-state must read True (ApplyReward's WaypointUnlock).
        bool litBefore = s.Waypoints.IsDiscovered(row.GoalAreaKey);
        GD.Print($"NEW_STONE area={row.GoalAreaKey} before={litBefore}");
        if (!Complete(s, "dary")) return false;
        bool litAfter = s.Waypoints.IsDiscovered(row.GoalAreaKey);
        GD.Print($"QUEST3_REWARD kind={row.RewardKind} area={row.GoalAreaKey}");
        GD.Print($"NEW_STONE area={row.GoalAreaKey} lit={litAfter}");
        if (row.RewardKind != QuestRewardKind.WaypointUnlock || litBefore || !litAfter)
        {
            GD.PushError($"act-1-open leg: the stone unlock reading wrong — kind={row.RewardKind}" +
                $" before={litBefore} after={litAfter}");
            return false;
        }

        // RIDE home and back — kopcowisko<->grodzisko share NO edge, so BOTH of
        // these moves can only be the UNCHANGED lit-stone door (town hub).
        string town = s.Areas.TownKey;
        if (!LegKit.Ride(s, act, town, Tag)) return false;
        if (!LegKit.Ride(s, act, row.GoalAreaKey, Tag)) return false;
        return LegKit.Ride(s, act, town, Tag);
    }

    // ---- the leg's door primitives — every printed value is a session read ----

    private static bool Accept(GameSession s, string key)
    {
        try { s.QuestAccept(key); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"act-1-open leg: the rite '{key}' refused acceptance — {e.Message}");
            return false;
        }
        GD.Print($"QUEST_ACCEPT key={key} phase={s.Quests.PhaseOf(key)} area={s.CurrentArea}");
        return true;
    }

    /// <summary>One WALK hop through the ONE door (this leg's own gate — NOT in
    /// the kit: it additionally pins the destination dark on arrival and prints
    /// the spawn rows, which StashQuestLeg's Walk does not; the fork is real,
    /// audit D-2 named it, the merge skipped it with reason). The linkedTo edge
    /// is asserted from the AREA DATA before the call, the lit set is read from
    /// the session before and after — equal means walking lit NOTHING (the
    /// planted-red shape: a TravelTo that lights on arrival turns this RED).
    /// requireDarkDest additionally pins the destination dark on arrival (the
    /// pure-walk proof into a dark stone).</summary>
    private static bool Walk(GameSession s, Act act, string dest, bool requireDarkDest)
    {
        string from = s.CurrentArea;
        if (!s.Areas.Get(from).LinkedTo.Contains(dest))
        {
            GD.PushError($"act-1-open leg: no linkedTo edge {from}->{dest} — refusing to call this a WALK_STEP");
            return false;
        }
        string litBefore = LegKit.LitSet(s), litAfter;
        try { s.TravelTo(dest); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"act-1-open leg: the travel door refused the walk {from}->{dest} — {e.Message}");
            return false;
        }
        litAfter = LegKit.LitSet(s);
        bool dark = !s.Waypoints.IsDiscovered(dest);
        bool same = litBefore == litAfter;
        GD.Print($"WALK_STEP from={from} to={dest} edge=True lit_before={litBefore} lit_after={litAfter}" +
            $" same={same} dest_lit={s.Waypoints.IsDiscovered(dest)}");
        if (!same)
        {
            GD.PushError($"act-1-open leg: the walk moved lit stones — {litBefore} -> {litAfter}" +
                " (walking lights NOTHING)");
            return false;
        }
        if (requireDarkDest && !dark)
        {
            GD.PushError($"act-1-open leg: '{dest}' arrived lit — this hop was no pure-walk proof");
            return false;
        }
        act.Rebuild(dest);
        PrintSpawns(act);
        return true;
    }

    private static bool Step(GameSession s, string key, QuestRow row)
    {
        if (s.CurrentArea != row.GoalAreaKey)
        {
            GD.PushError($"act-1-open leg: stepped '{key}' from '{s.CurrentArea}', the goal is" +
                $" '{row.GoalAreaKey}' — no evidence");
            return false;
        }
        try { s.QuestStep(key); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"act-1-open leg: the step refused — {e.Message}");
            return false;
        }
        GD.Print($"QUEST_STEP key={key} area={s.CurrentArea} phase={s.Quests.PhaseOf(key)}");
        return true;
    }

    private static bool Complete(GameSession s, string key)
    {
        try { s.QuestComplete(key); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"act-1-open leg: the completion refused — {e.Message}");
            return false;
        }
        GD.Print($"QUEST_COMPLETE key={key} phase={s.Quests.PhaseOf(key)}");
        return true;
    }

    /// The built spawn row of the CURRENT area — the proof the ADDED data rows
    /// really spawn (same pinned line JourneyLeg prints for mchy).
    private static void PrintSpawns(Act act)
    {
        var row = act.Monsters.RowSnapshot();
        for (int i = 0; i < row.Count; i++)
            GD.Print($"MONSTER_SPAWN i={i} key={row[i].Key} cell={row[i].Cell.X},{row[i].Cell.Y}");
    }
}
