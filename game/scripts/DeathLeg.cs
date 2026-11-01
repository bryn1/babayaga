using BabaYaga.Sim;
using Godot;

/// <summary>
/// The M4-S6 headless DEATH LEG — run ONCE by Boot's headless branch after the
/// belt leg. Chain: start BELT-EMPTY with NO potions (the belt leg's leftover is
/// lifted and sold back — the belt and the pack read empty) -> HUD_STATE n=1
/// dead=False (the HUD's OWN read-back) -> the pinned WAYPOINT ORDER CONTRACT to
/// mchy + act.Rebuild (the ONE rebuild entry) -> walk to the deepest spawn cell,
/// strike the same row until REAL counter-damage grinds the Life pool to 0 —
/// `HERO_DEAD area=&lt;area&gt; life=&lt;life&gt;` prints ONLY from the observed session.IsDead ->
/// HUD_STATE n=2 dead=True -> attempt Attack while dead: the rejection must come
/// back (HUD hint + `DEAD_REJECT verb=Attack msg=&lt;reason&gt;` — a strike ACCEPTED
/// while dead is a named error, never a marker) -> Respawn through the lit
/// stones: `RESPAWN area=… life=…` (values read from the session), then
/// `HERO_ALIVE` only while the session reads alive, full life, in the last
/// discovered town; HUD_STATE n=3 dead=False reads that back -> act.Rebuild
/// carries the presentation home too -> Buy a life potion in town: the
/// `TRADE post-death buy ok …` line proves the economy is live after death.
/// DEATH_OK prints only when EVERY reading held; any failure is PushError + no
/// marker (never a crash — the HeroController hint idiom).
/// </summary>
public static class DeathLeg
{
    private const string Dest = "mchy";
    private const int StrikeCap = 2048; // safety stop; the seed-fixed grind lands far earlier (≈550)

    public static void Run(GameSession s, Act act)
    {
        // ---- belt-empty start: lift every filled slot, sell back every carried potion ----
        for (int slot = 1; slot <= GameSession.BeltSlots; slot++)
            if (s.Belt[slot - 1] is not null) s.RemoveFromBelt(slot);
        while (s.Carried.Count > 0 && IsPotion(s.Carried[^1])) s.SellLast();
        act.HudPanel.Refresh();
        if (act.HudPanel.BeltFilled != 0 || HasAnyPotion(s))
        {
            GD.PushError($"death leg: not belt-empty — belt={act.HudPanel.BeltFilled} potions={CountPotions(s)}");
            return;
        }
        bool hud1 = act.HudPanel.DeadShown;
        GD.Print($"HUD_STATE n=1 dead={hud1}");

        // ---- the WAYPOINT ORDER CONTRACT: light the DESTINATION stone BEFORE travel ----
        // (the belt leg's tail already left the hero in mchy — same idiom as the
        // hero's door path: only stones MOVE, and mchy->mchy is no stone path)
        if (s.CurrentArea != Dest)
        {
            if (!s.Waypoints.IsDiscovered(Dest)) s.DiscoverWaypoint(Dest);
            s.TravelTo(Dest);
        }
        act.Rebuild(Dest);

        // ---- the grind: REAL counter-damage on the third stream, one strike per call ----
        var row = act.Monsters.RowSnapshot();
        if (row.Count == 0)
        {
            GD.PushError("death leg: mchy spawned no monster — no counter damage possible");
            return;
        }
        var hero = act.Hero;
        var (deepKey, target) = FarthestFrom(act.Tiles.CenterCell(), row);
        hero.WalkTo(target);
        while (hero.StepNext()) { } // the same A* the windowed click plans
        if (!LegKit.InRange(hero.Cell, target, 1))
        {
            GD.PushError($"death leg: hero not adjacent to the deep cell {target.X},{target.Y} — no Attack call");
            return;
        }
        string key = deepKey; // the kind stood on the deep cell (ponurak, mchy's deep row)
        int strikes = 0;
        for (; strikes < StrikeCap && !s.IsDead; strikes++) s.Attack(key, 1);
        if (!s.IsDead) // pinned marker ONLY on the observed DEAD state
        {
            GD.PushError($"death leg: hero survived {StrikeCap} strikes — life={s.Character.Life}, no HERO_DEAD line");
            return;
        }
        GD.Print($"HERO_DEAD area={s.CurrentArea} life={s.Character.Life}");
        act.HudPanel.Refresh();
        bool hud2 = act.HudPanel.DeadShown;
        GD.Print($"HUD_STATE n=2 dead={hud2}");

        // ---- the DEAD gate, live: Attack while dead must come back rejected ----
        try
        {
            s.Attack(key, 1);
            GD.PushError("death leg: Attack was ACCEPTED while dead — the gate is open");
            return;
        }
        catch (SessionRejectedException e)
        {
            act.HudPanel.Hint(e.Message); // rejection is a HUD hint, never a crash
            GD.Print($"DEAD_REJECT verb=Attack msg={e.Message}");
        }

        // ---- the stone resurrection: values printed are the session's, never literals ----
        try { s.Respawn(); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"death leg: Respawn rejected — {e.Message}");
            return;
        }
        GD.Print($"RESPAWN area={s.CurrentArea} life={s.Character.Life}");
        act.HudPanel.Refresh();
        bool hud3 = act.HudPanel.DeadShown;
        GD.Print($"HUD_STATE n=3 dead={hud3}");
        if (s.IsDead || s.Character.Life != s.Character.MaxLife || s.CurrentArea != s.Areas.TownKey)
        {
            GD.PushError($"death leg: read-back mismatch — dead={s.IsDead} life={s.Character.Life}" +
                $"/{s.Character.MaxLife} area={s.CurrentArea} town={s.Areas.TownKey}");
            return;
        }
        GD.Print("HERO_ALIVE");
        act.Rebuild(s.CurrentArea); // presentation follows the stones home (the ONE rebuild entry)

        // ---- the economy speaks again: a real town Buy on the resurrected hero ----
        long gold0 = s.Gold;
        long price;
        try { price = s.Buy(GameSession.LifePotionId); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"death leg: post-death Buy rejected — {e.Message}");
            return;
        }
        if (s.Gold != gold0 - price || s.Carried[^1].Base.Id != GameSession.LifePotionId)
        {
            GD.PushError($"death leg: post-death buy state wrong gold={s.Gold} was={gold0} price={price}");
            return;
        }
        GD.Print($"TRADE post-death buy ok item={GameSession.LifePotionId} gold={s.Gold}");

        // ---- the honesty tail: every printed line was a session/HUD reading ----
        if (!hud1 && hud2 && !hud3)
            GD.Print($"DEATH_OK strikes={strikes} xp={s.Character.Xp}");
        else
            GD.PushError($"death leg: HUD_STATE reads {hud1}/{hud2}/{hud3} — not false,true,false");
    }

    private static bool IsPotion(Item item) =>
        item.Base.Id == GameSession.LifePotionId || item.Base.Id == GameSession.ManaPotionId;

    private static bool HasAnyPotion(GameSession s) =>
        s.Carried.Exists(IsPotion);

    private static int CountPotions(GameSession s) => s.Carried.FindAll(IsPotion).Count;

    /// <summary>The spawn row entry farthest from arrival (Chebyshev — the Sim's
    /// ONE metric, as JourneyLeg picks its deep target).</summary>
    private static (string Key, Vector2I Cell) FarthestFrom(
        Vector2I center, System.Collections.Generic.List<(string Key, Vector2I Cell)> row)
    {
        var best = row[0];
        int bestD = -1;
        foreach (var e in row)
        {
            int d = new WorldPos(center.X, center.Y).DistanceTo(new WorldPos(e.Cell.X, e.Cell.Y));
            if (d > bestD) { bestD = d; best = e; }
        }
        return best;
    }

}
