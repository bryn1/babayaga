using System;
using System.Linq;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// The M4-S12 TOWN STASH leg (LEDGER row 74, card/11893; owner D2-oracle grant
/// 2026-10-30: the stash lives ONLY in town, has MULTIPLE TABS, extra tabs
/// unlocked by rewards) — glowa's StashTab reward goes LIVE after the hirelings
/// leg left the hero home in grodzisko. Beat order:
///   `QUEST_ACCEPT key=glowa` in town (its prereqs wrzeciono+dary are Done —
///   the act-1-open leg ran them THIS run) → RIDE to kopcowisko over the
///   lit stones (no linkedTo edge — the ride-only hub proof stands), the data
///   row's kill taken on the way (`MONSTER_DEFEATED` in kopcowisko, the
///   ponurak band area-monsters.json gives the rite's road), one WALK_STEP
///   into czelusc (the linkedTo road off known ground — the walk lights
///   NOTHING, the destination arrives DARK, the lit set read equal before/
///   after) — and czelusc prints `MONSTER_SPAWN_NONE`: area-monsters.json
///   carries NO czelusc row (data READ-ONLY), so the kill the FSM never
///   demanded is honestly absent ON the goal ground, stated, never faked.
///   `QUEST_STEP` taken ON czelusc (the FSM's goal-flag move, the Act1OpenLeg
///   precedent) → `NEW_TAB tab=2 unlocked_before=False` pasted BEFORE the
///   completion → QuestComplete pays the StashTab → `QUEST4_REWARD kind=StashTab`
///   + `NEW_TAB tab=2 unlocked_after=True` (the reward IS the unlock, read
///   around the payment) → the pay-once gate: a SECOND QuestComplete is
///   REFUSED (`QUEST4_REFUSED`, the refusal quoted). Home over the same door
///   (WALK back off the road, RIDE the hub).
///   In town the chest answers: Buy jantar (STASH_BUY, session-read price and
///   purse), the carried anchor cell re-derived PRESENTATION-side exactly like
///   InventoryPanel (the Sim answers no "which cell?" outside its reports —
///   ARCH v8), `STASH_STORE tab=2` + the session's OWN StashMoveReport line,
///   the item gone from the pack (`Carried` read), and `STASH_TAKE` lifting it
///   back through TryCarry with the landing cell read from the report beside a
///   pack-count read. STASH_OK prints only when every reading held; any
///   failure is a PushError with no marker (the leg idiom, never a crash).
/// </summary>
public static class StashQuestLeg
{
    private const string Tag = "stash leg"; // the leg's log-voice — LegKit helpers print through it (audit D-2)

    public static void Run(GameSession s, Act act)
    {
        // the hire leg's tail leaves the hero in town; the lit stone carries him
        // home from anywhere else before the rite begins (hub ride).
        if (s.CurrentArea != s.Areas.TownKey)
        {
            GD.Print($"STASH_START area={s.CurrentArea} -> {s.Areas.TownKey} (lit stones carry the hero home)");
            s.TravelTo(s.Areas.TownKey);
            act.Rebuild(s.CurrentArea);
        }

        bool ok = Glowa(s, act) && Chest(s, act);

        if (ok) GD.Print("STASH_OK");
        else GD.PushError("stash leg: the town chest has no STASH_OK — the lines above are the evidence");
    }

    // ---- glowa: the rite, the walked lair road, the tab-2 unlock ---------------

    private static bool Glowa(GameSession s, Act act)
    {
        var row = s.Quests.Get("glowa").Row; // goal + reward ride the DATA row
        try { s.QuestAccept("glowa"); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"stash leg: the rite 'glowa' refused acceptance — {e.Message}");
            return false;
        }
        GD.Print($"QUEST_ACCEPT key=glowa phase={s.Quests.PhaseOf("glowa")} area={s.CurrentArea}");

        // kopcowisko carries the dary stone (lit by its own reward THIS run);
        // kopcowisko<->grodzisko share no edge, so the hop OUT is ride-only.
        if (!LegKit.Ride(s, act, "kopcowisko", Tag)) return false;
        if (!LegKit.KillOne(s, act, Tag, "on the road")) return false;

        // czelusc is the linkedTo neighbour of known ground — feet, not stones:
        // the walk lights NOTHING and the lair stone arrives DARK.
        if (!Walk(s, act, row.GoalAreaKey)) return false;

        // Honest data reading: czelusc carries NO area-monsters.json row (the
        // file is READ-ONLY this card), so NOTHING spawns on the goal ground and
        // no kill is claimed there — the FSM's step demands position, never a
        // kill (the Act1OpenLeg precedent the leg states, not hides).
        if (act.Monsters.RowSnapshot().Count == 0)
            GD.Print($"MONSTER_SPAWN_NONE area={s.CurrentArea} note=no area-monsters row — kill rode kopcowisko above");

        if (s.CurrentArea != row.GoalAreaKey)
        {
            GD.PushError($"stash leg: stepped 'glowa' from '{s.CurrentArea}', the goal is '{row.GoalAreaKey}'");
            return false;
        }
        try { s.QuestStep("glowa"); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"stash leg: the step refused — {e.Message}");
            return false;
        }
        GD.Print($"QUEST_STEP key=glowa area={s.CurrentArea} phase={s.Quests.PhaseOf("glowa")}");

        // the before-state is pasted BEFORE the completion (the NEW_STONE idiom).
        bool before = s.IsStashTabUnlocked(2);
        GD.Print($"NEW_TAB tab=2 unlocked_before={before}");
        try { s.QuestComplete("glowa"); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"stash leg: the completion refused — {e.Message}");
            return false;
        }
        bool after = s.IsStashTabUnlocked(2);
        GD.Print($"QUEST4_REWARD kind={row.RewardKind} tabs={s.StashTabs}");
        GD.Print($"NEW_TAB tab=2 unlocked_after={after}");
        if (row.RewardKind != QuestRewardKind.StashTab || before || !after)
        {
            GD.PushError($"stash leg: the tab-2 unlock reading wrong — kind={row.RewardKind}" +
                $" before={before} after={after}");
            return false;
        }

        // pays once: the SECOND completion is REFUSED (the FSM's REFUSED-throw
        // gate, the same idiom BeltQuestTests pins on gesi — asserted on tab 2).
        string refusal = "";
        try { s.QuestComplete("glowa"); }
        catch (SessionRejectedException e) { refusal = e.Message; }
        GD.Print($"QUEST4_REFUSED \"{refusal}\" tabs_after={s.StashTabs} tab3={s.IsStashTabUnlocked(3)}");
        if (!refusal.Contains("not completable") || s.StashTabs != 1 || s.IsStashTabUnlocked(3))
        {
            GD.PushError("stash leg: the second completion did NOT pay-once — see QUEST4_REFUSED");
            return false;
        }

        // the road back: feet to kopcowisko (the lair stays dark), stones home.
        if (!Walk(s, act, "kopcowisko")) return false;
        return LegKit.Ride(s, act, s.Areas.TownKey, Tag);
    }

    // ---- the chest: store into tab 2, take it back, every line session-read ----

    private static bool Chest(GameSession s, Act act)
    {
        if (s.CurrentArea != s.Areas.TownKey)
        {
            GD.PushError($"stash leg: the chest beat ran out of town — area={s.CurrentArea}");
            return false;
        }
        act.Rebuild(s.CurrentArea);
        if (!s.IsStashTabUnlocked(2))
        {
            GD.PushError("stash leg: tab 2 closed after the reward — the unlock did not stick");
            return false;
        }

        long goldBefore = s.Gold;
        s.Buy("jantar"); // the town vendor's 1×1 gem row (cost 40, Vendor.Standard)
        var item = s.Carried[^1];
        GD.Print($"STASH_BUY base={item.Base.Id} price={goldBefore - s.Gold} gold_after={s.Gold}");

        // The pack anchor, re-derived PRESENTATION-side exactly like the panel
        // (first-fit over Carried — the Sim answers no "which cell?" outside its
        // command reports, ARCH v8). The last bought copy is derived LAST.
        bool ok = LegKit.TryDerive(s, item, out int invR, out int invC);
        GD.Print($"STASH_PACK anchor={invR},{invC} derived={ok} carried={s.Carried.Count}");
        if (!ok) return false;

        GameSession.StashMoveReport st;
        try { st = s.Store(invR, invC, 2); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"stash leg: the store refused — {e.Message}");
            return false;
        }
        GD.Print($"STASH_STORE tab={st.Tab} item={st.Base} inv_pos={st.InvRow},{st.InvCol} " +
                 $"pos={st.TabRow},{st.TabCol} carried_after={s.Carried.Count}");
        if (st.Tab != 2 || st.Base != "jantar" || (st.InvRow, st.InvCol) != (invR, invC)
            || s.Carried.Count == 0) return false;

        GameSession.StashMoveReport tk;
        try { tk = s.Take(2, st.TabRow, st.TabCol); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"stash leg: the take refused — {e.Message}");
            return false;
        }
        // positions session-read: the report's landing cell, cross-checked
        // against the panel-side first-fit of the RESTORED pack.
        bool landed = LegKit.TryDerive(s, s.Carried[^1], out int backR, out int backC)
            && (backR, backC) == (tk.InvRow, tk.InvCol);
        GD.Print($"STASH_TAKE tab={tk.Tab} item={tk.Base} pos={tk.TabRow},{tk.TabCol} " +
                 $"inv_pos={tk.InvRow},{tk.InvCol} carried={s.Carried.Count} anchor_matches={landed}");
        return landed && tk.Tab == 2 && tk.Base == "jantar"
            && (tk.TabRow, tk.TabCol) == (st.TabRow, st.TabCol)
            && ReferenceEquals(s.Carried[^1], item); // the SAME instance rode back
    }

    // ---- the leg's door primitives — Ride/KillOne/LitSet/TryDerive ride the
    // shared LegKit now (audit D-2/D-3, byte-identical bodies, the leg's
    // log-voice passed in as Tag); WALK stays here on purpose: the Act1OpenLeg
    // Walk carries a dark-destination gate AND prints the spawn rows, this one
    // neither — a near-fork, not a duplicate, skipped with reason (audit D-2).

    /// One WALK hop through the ONE door: the linkedTo edge asserted from AREA
    /// DATA before the call, the lit set read equal before/after (walking lights
    /// NOTHING), the destination's stone state printed on arrival.
    private static bool Walk(GameSession s, Act act, string dest)
    {
        string from = s.CurrentArea;
        if (!s.Areas.Get(from).LinkedTo.Contains(dest))
        {
            GD.PushError($"stash leg: no linkedTo edge {from}->{dest} — refusing to call this a WALK_STEP");
            return false;
        }
        string litBefore = LegKit.LitSet(s);
        try { s.TravelTo(dest); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"stash leg: the travel door refused the walk {from}->{dest} — {e.Message}");
            return false;
        }
        string litAfter = LegKit.LitSet(s);
        bool same = litBefore == litAfter;
        GD.Print($"WALK_STEP from={from} to={dest} edge=True lit_before={litBefore} lit_after={litAfter}" +
            $" same={same} dest_lit={s.Waypoints.IsDiscovered(dest)}");
        if (!same)
        {
            GD.PushError($"stash leg: the walk moved lit stones — {litBefore} -> {litAfter} (walking lights NOTHING)");
            return false;
        }
        act.Rebuild(dest);
        return true;
    }
}
