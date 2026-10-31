using System;
using System.Linq;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// The M4-S5 headless BELT LEG — run ONCE by Boot's headless branch after the
/// trade leg (the hero stands in town, which is also where the vendor is). Chain:
/// Buy TWO life potions in town -> BELT line with the HUD's own painted count
/// (slots=0/4) -> AddToBelt x2 -> BELT slots=2/4 -> the pinned WAYPOINT ORDER
/// CONTRACT to mchy (light the DESTINATION stone BEFORE the travel — same idiom
/// as JourneyLeg/TradeLeg) -> act.Rebuild (the ONE rebuild entry) -> the hero
/// walks a monster and strikes until REAL counter-damage has lowered the pool
/// (BELT_DMG prints the session life < max evidence) -> UsePotion(1) — the SAME
/// public session verb the belt_1 hotkey presses; the POTION_USE line prints ONLY
/// on an actual rise, and the belt line after it reads slots=1/4. XP must not
/// have moved across the drink. BELT_OK prints only when every one of those
/// readings held; any failure is a PushError + no marker (never a crash — the
/// HeroController hint idiom for rejections).
/// </summary>
public static class BeltLeg
{
    private const string Dest = "mchy";
    private const int StrikeCap = 32; // safety stop; a counter lands far earlier (seed-fixed)

    public static void Run(GameSession s, Act act)
    {
        // ---- buy TWO life potions in town (Vendor.Standard; session is already in town) ----
        long gold0 = s.Gold;
        try
        {
            s.Buy(GameSession.LifePotionId);
            s.Buy(GameSession.LifePotionId);
        }
        catch (SessionRejectedException e)
        {
            GD.PushError($"belt leg: Buy rejected — {e.Message}");
            return;
        }
        var (a, b) = (s.Carried[^2], s.Carried[^1]);
        act.HudPanel.Refresh();
        GD.Print($"BELT n=1 slots={act.HudPanel.BeltFilled}/{GameSession.BeltSlots} gold={s.Gold}");
        if (act.HudPanel.BeltFilled != 0)
        {
            GD.PushError("belt leg: fresh belt is not empty — the widget lied");
            return;
        }

        // ---- insert x2: carried refs into slots 1 and 2 ----
        s.AddToBelt(s.Carried.IndexOf(a), 1);
        s.AddToBelt(s.Carried.IndexOf(b), 2);
        act.HudPanel.Refresh();
        GD.Print($"BELT n=2 slots={act.HudPanel.BeltFilled}/{GameSession.BeltSlots}");
        if (act.HudPanel.BeltFilled != 2)
        {
            GD.PushError("belt leg: two inserts did not paint 2/4");
            return;
        }

        // ---- the WAYPOINT ORDER CONTRACT: light the DESTINATION stone BEFORE travel ----
        if (!s.Waypoints.IsDiscovered(Dest)) s.DiscoverWaypoint(Dest);
        s.TravelTo(Dest);
        act.Rebuild(Dest);

        // ---- REAL monster damage: walk a spawn, strike until the pool has fallen ----
        var row = act.Monsters.RowSnapshot();
        if (row.Count == 0)
        {
            GD.PushError("belt leg: mchy spawned no monster — no counter damage possible");
            return;
        }
        var hero = act.Hero;
        int strikes = 0;
        for (int i = 0; s.Character.Life >= s.Character.MaxLife && strikes < StrikeCap; i++)
        {
            var (key, cell) = row[i % row.Count];
            hero.WalkTo(cell);
            while (hero.StepNext()) { } // the same A* the windowed click plans
            if (!InRange(hero.Cell, cell, 1))
            {
                GD.PushWarning($"belt leg: hero not adjacent to {key} at {cell.X},{cell.Y} — no Attack call");
                continue;
            }
            s.Attack(key, 1); // the survivor counters — real damage, real stream
            strikes++;
        }
        GD.Print($"BELT_DMG strikes={strikes} life={s.Character.Life} max={s.Character.MaxLife}");
        if (s.Character.Life >= s.Character.MaxLife)
        {
            GD.PushError($"belt leg: no counter damage within {StrikeCap} strikes — life still at max");
            return;
        }

        // ---- the drink: the SAME public UsePotion verb belt_1 presses (hotkeys share it) ----
        long xp0 = s.Character.Xp;
        long before = s.Character.Life;
        GameSession.PotionUseReport r;
        try { r = s.UsePotion(1); }
        catch (SessionRejectedException e)
        {
            act.HudPanel.Hint(e.Message); // rejection is a HUD hint, never a crash
            GD.PushError($"belt leg: UsePotion(1) rejected — {e.Message}");
            return;
        }
        // pinned marker — ONLY on an actual rise (the clamp-to-Max contract):
        if (r.After > r.Before)
            GD.Print($"POTION_USE slot=1 life_before={r.Before} life_after={r.After} max={r.Max}");
        else
        {
            GD.PushError("belt leg: the drink raised nothing — no POTION_USE line");
            return;
        }
        act.HudPanel.Refresh();
        GD.Print($"BELT n=3 slots={act.HudPanel.BeltFilled}/{GameSession.BeltSlots}");

        // ---- the honesty tail: the printed numbers are the session's, XP untouched ----
        long expect = Math.Min(before + s.Character.MaxLife * GameSession.PotionRestorePercent / 100,
            s.Character.MaxLife);
        bool held = r.Before == before && r.After == s.Character.Life && r.After == expect
            && s.Character.Xp == xp0 && act.HudPanel.BeltFilled == 1
            && s.Carried.Count(i => i.Base.Id == GameSession.LifePotionId) == 1;
        if (held)
            GD.Print("BELT_OK");
        else
            GD.PushError($"belt leg: readings disagree — after={r.After} expect={expect}" +
                $" xp={s.Character.Xp}/{xp0} belt={act.HudPanel.BeltFilled}");
    }

    /// <summary>Cell adjacency on the ONE metric (reuses IsoCamera.InRange, as JourneyLeg).</summary>
    private static bool InRange(Vector2I a, Vector2I b, int cells) =>
        IsoCamera.InRange(new WorldPos(a.X, a.Y), new WorldPos(b.X, b.Y), cells);
}
