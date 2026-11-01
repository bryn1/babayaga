using BabaYaga.Sim;
using Godot;

/// <summary>
/// The M4-S7 headless CAST LEG — run ONCE by Boot's headless branch after the
/// quest leg (the hero stands in town, alive, belt empty, the mana pool holding
/// ONLY the quest leg's single reward-bought weave). Chain: ASSERT the iskra
/// weave the M4-S8 gesi rite paid for (the point is spent in QuestLeg — the
/// real quest loop is the ONLY point source since card/11856; a missing weave
/// is a named RED, no side-channel mint) -> Buy napitek-manny + belt it (the belt
/// quaff beat; row 79's RestTick regen joined beside it in M4-S10b — the quaff leg
/// stands) -> the pinned WAYPOINT ORDER
/// CONTRACT to mchy + act.Rebuild (the ONE rebuild entry) -> walk the nearest
/// pokrzywnik, adjacency-gated (the same InRange gate as every leg — no
/// adjacency = no Cast call = no marker) -> CAST until dead:
/// `CAST skill=iskra mana_before=<int> mana_after=<int> dmg=<int>` per weave,
/// every value a session/report reading, and `CAST_DEFEATED key=<kind>` ONLY
/// on the report's Killed -> drain the pool with real debits -> `MANA_REJECT
/// msg=<reason>` (a rejection is a HUD hint, and NO CAST line follows the
/// empty) -> HUD `CAST_STATE n=1/2` pulled around the rejection (the reject
/// changed nothing) -> UsePotion(1) (the belt quaff, the same public verb the
/// belt_1 hotkey presses) -> `CAST_STATE n=3` shows the rise -> the weave goes
/// GREEN again -> MANA_OK prints ONLY when every reading held: each CAST
/// debit equalled the data cost, the HUD mana read-back equalled the session
/// pool at all three pulls AND actually moved across the quaff (the frozen-
/// literal planted-red shape), and no CAST line sat between reject and refill.
/// </summary>
public static class CastLeg
{
    private const string Dest = "mchy";
    private const int KillCap = 8; // safety stop; the seed-fixed kill lands far earlier
    private const int DrainCap = 16; // safety stop for the drain past the first kill

    public static void Run(GameSession s, Act act)
    {
        // ---- the weave must already be learned: the M4-S8 QUEST rite (QuestLeg)
        // accepts→steps→completes gesi, its paid SkillPoint is spent into iskra
        // THERE, and the leg carries the hero home. This leg ASSERTS the woven
        // point instead of minting one — the quest loop is the only point source
        // (rewired 11856: S7's old inline accept→step→complete prep retired; the
        // economy closes THROUGH the real quest, both legs stay green on it). ----
        if (s.Skills.HardPoints("iskra") != 1)
        {
            GD.PushError($"cast leg: iskra holds {s.Skills.HardPoints("iskra")} hard point(s)," +
                " expected the one the quest rite paid — the quest leg must run first");
            return;
        }
        long cost = s.SkillData.Find("iskra")!.ManaCost; // the DATA row drives every debit assert below
        GD.Print($"CAST_PREP skill=iskra points={s.Skills.HardPoints("iskra")} cost={cost} mana={s.Character.Mana}");
        if (cost <= 0)
        {
            GD.PushError("cast leg: the data file sets iskra a free weave — the drain beat is meaningless");
            return;
        }

        // ---- town buys the mana potion and belters it (the refill beat's stock) ----
        try
        {
            s.Buy(GameSession.ManaPotionId);
            s.AddToBelt(s.Carried.Count - 1, 1);
        }
        catch (SessionRejectedException e)
        {
            GD.PushError($"cast leg: town refill stock refused — {e.Message}");
            return;
        }

        // ---- the WAYPOINT ORDER CONTRACT: light the DESTINATION stone BEFORE travel ----
        if (!s.Waypoints.IsDiscovered(Dest)) s.DiscoverWaypoint(Dest);
        s.TravelTo(Dest);
        act.Rebuild(Dest);

        // ---- walk the nearest pokrzywnik (the weakest row — the kill must fit the pool) ----
        var row = act.Monsters.RowSnapshot();
        int target = row.FindIndex(e => e.Key == "pokrzywnik");
        if (target < 0)
        {
            GD.PushError("cast leg: mchy spawned no pokrzywnik — no cast target");
            return;
        }
        var (key, cell) = row[target];
        var hero = act.Hero;
        hero.WalkTo(cell);
        while (hero.StepNext()) { } // the same A* the windowed click plans
        if (!InRange(hero.Cell, cell, 1))
        {
            GD.PushError($"cast leg: hero not adjacent to the pokrzywnik at {cell.X},{cell.Y} — no Cast call");
            return;
        }

        // ---- cast until dead: the pinned CAST lines, every value session-read ----
        bool defeated = false;
        for (int cast = 1; cast <= KillCap && !defeated; cast++)
        {
            if (!Weave(s, act, key, out bool killed)) return; // the line + debit asserts live inside
            defeated = killed; // Weave prints CAST_DEFEATED on Killed
        }
        if (!defeated)
        {
            GD.PushError("cast leg: the pokrzywnik outlived the mana budget inside KillCap — no CAST_DEFEATED");
            return;
        }

        // ---- drain the pool with real debits (fresh spawns keep the weave busy) ----
        for (int cast = 0; cast < DrainCap && s.Character.Mana >= cost; cast++)
            if (!Weave(s, act, key, out _)) return;
        if (s.Character.Mana >= cost)
        {
            GD.PushError($"cast leg: drain hit the cap at mana={s.Character.Mana} >= cost={cost}");
            return;
        }

        // ---- the empty pool: MANA_REJECT through the hint idiom, no CAST line after ----
        act.HudPanel.Refresh();
        long mana1 = s.Character.Mana, hud1 = act.HudPanel.ManaShown;
        GD.Print($"CAST_STATE n=1 mana={hud1}");
        bool castLineAfterReject = false;
        try
        {
            s.Cast("iskra", key);
            castLineAfterReject = true; // accepted at an empty pool — gate inverted (the plant's shape)
            GD.PushError("cast leg: a weave was ACCEPTED at mana below cost — the gate is open");
            return;
        }
        catch (SessionRejectedException e)
        {
            act.HudPanel.Hint(e.Message); // rejection is a HUD hint, never a crash
            GD.Print($"MANA_REJECT msg={e.Message}");
        }
        act.HudPanel.Refresh();
        long mana2 = s.Character.Mana, hud2 = act.HudPanel.ManaShown;
        GD.Print($"CAST_STATE n=2 mana={hud2}");

        // ---- the belt quaff: the same public UsePotion the belt_1 hotkey presses ----
        GameSession.PotionUseReport pr;
        try { pr = s.UsePotion(1); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"cast leg: the mana quaff refused — {e.Message}");
            return;
        }
        act.HudPanel.Refresh();
        long mana3 = s.Character.Mana, hud3 = act.HudPanel.ManaShown;
        GD.Print($"CAST_STATE n=3 mana={hud3}");

        // ---- green again: one honest weave after the refill ----
        if (!Weave(s, act, key, out _)) return;

        // ---- the honesty tail: every printed number was a session/HUD reading ----
        bool held = defeated && !castLineAfterReject
            && pr.Kind == "mana" && pr.After > pr.Before
            && mana2 == mana1 && mana3 > mana2
            && hud1 == mana1 && hud2 == mana2 && hud3 == mana3;
        if (held)
            GD.Print($"MANA_OK mana={s.Character.Mana} max={s.Character.MaxMana}");
        else
            GD.PushError($"cast leg: readings disagree — hud={hud1}/{hud2}/{hud3} vs" +
                $" session={mana1}/{mana2}/{mana3} quaff={pr.Kind}:{pr.Before}->{pr.After}");
    }

    /// <summary>One weave through the SAME public Cast the cast_1 hotkey will
    /// press, printing the pinned CAST line and asserting the debit equals the
    /// data cost. False = a PushError was written and the leg must stop.</summary>
    private static bool Weave(GameSession s, Act act, string key, out bool killed)
    {
        killed = false;
        long before = s.Character.Mana;
        GameSession.CastReport r;
        try { r = s.Cast("iskra", key); }
        catch (SessionRejectedException e)
        {
            act.HudPanel.Hint(e.Message);
            GD.PushError($"cast leg: weave refused above the gate — {e.Message}");
            return false;
        }
        GD.Print($"CAST skill=iskra mana_before={r.ManaBefore} mana_after={r.ManaAfter} dmg={r.Damage}");
        long cost = s.SkillData.Find("iskra")!.ManaCost;
        if (r.ManaBefore != before || r.ManaAfter != before - cost || s.Character.Mana != r.ManaAfter)
        {
            GD.PushError($"cast leg: debit mismatch — {r.ManaBefore}->{r.ManaAfter}" +
                $" expected -{cost} session={s.Character.Mana}");
            return false;
        }
        if (r.Killed) { killed = true; GD.Print($"CAST_DEFEATED key={key}"); }
        act.HudPanel.Refresh(); // the orbs follow every weave, as _Process does windowed
        return true;
    }

    /// <summary>Cell adjacency on the ONE metric (reuses IsoCamera.InRange, as BeltLeg).</summary>
    private static bool InRange(Vector2I a, Vector2I b, int cells) =>
        IsoCamera.InRange(new WorldPos(a.X, a.Y), new WorldPos(b.X, b.Y), cells);
}
