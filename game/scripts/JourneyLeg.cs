using System.Collections.Generic;
using System.Text.Json;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// The M4-SLICE headless marker legs — the walking hero striking one pokrzywnik —
/// run ONCE by Boot's headless branch through the §2 bridge surface ONLY (GameSession
/// public members; the Sim's internals are assembly-invisible to this project).
/// Chain: New (already run) -> IsDiscovered -> DiscoverWaypoint -> TravelTo (the
/// pinned waypoint order — discover the DESTINATION stone BEFORE the travel, never
/// after-travel) -> scripted Chebyshev walk (>=3 `HERO_STEP n=<int> cell=<x>,<y>
/// area=<string>` lines read from REAL hero+session state) -> adjacency-gated
/// `Attack` -> `MONSTER_DEFEATED area=mchy key=pokrzywnik n=<int>` ONLY on a real
/// session state change (kills>=1 AND XP actually granted) -> Loot when a drop is
/// pending. The strike gate is IsoCamera.InRange — not adjacent = no Attack call =
/// no marker (the leg's planted-red shape). Monster HP before/after the kill reads
/// the session's own Dump() JSON (engagedLife), the one public mirror of the
/// engaged monster. JOURNEY_OK prints only when the whole chain completed.
/// </summary>
public static class JourneyLeg
{
    private const string Dest = "mchy";
    private const string MonsterKey = "pokrzywnik";
    private const int StrikeCap = 64; // safety stop; the kill lands far earlier (seed-fixed)

    public static void Run(GameSession s, Act act)
    {
        // ---- the WAYPOINT ORDER CONTRACT: light the DESTINATION stone BEFORE travel ----
        GD.Print($"WAYPOINT before area={s.CurrentArea} lit={Dest}={s.Waypoints.IsDiscovered(Dest)}");
        if (!s.Waypoints.IsDiscovered(Dest)) s.DiscoverWaypoint(Dest);
        s.TravelTo(Dest);
        GD.Print($"WAYPOINT after area={s.CurrentArea} lit={Dest}={s.Waypoints.IsDiscovered(Dest)}");
        act.Rebuild(s.CurrentArea); // windowed travel reaches this via HeroController.OnAreaChanged

        // ---- spawn row: the Fnv-seed determinism read (two runs equal; a seed-string
        // swap differs — the planted-red shape of that gate) ----
        var row = act.Monsters.RowSnapshot();
        for (int i = 0; i < row.Count; i++)
            GD.Print($"MONSTER_SPAWN i={i} key={row[i].Key} cell={row[i].Cell.X},{row[i].Cell.Y}");
        if (row.Count == 0) { GD.PushError("journey leg: mchy spawned no monster — chain incomplete"); return; }

        // ---- walk: the spawn cell farthest from arrival, through the real A* ----
        var hero = act.Hero;
        var target = FarthestFrom(act.Tiles.CenterCell(), row);
        hero.WalkTo(target);
        int steps = 0;
        var cells = new HashSet<Vector2I>();
        while (hero.StepNext())
        {
            steps++;
            cells.Add(hero.Cell); // REAL hero state on every line — never a literal
            GD.Print($"HERO_STEP n={steps} cell={hero.Cell.X},{hero.Cell.Y} area={s.CurrentArea}");
        }

        // ---- strike: the real Attack verb, gated on standing adjacent to the cell ----
        int kills = 0;
        if (InRange(hero.Cell, target, 1))
        {
            long xp0 = s.Character.Xp;
            for (int strike = 1; kills == 0 && strike <= StrikeCap; strike++)
            {
                var r = s.Attack(MonsterKey, 1); // §2 bridge verb, one strike per call
                kills = r.Kills;
                GD.Print($"MONSTER_HP strike={strike} engagedLife={EngagedLife(s)}" +
                    $" engaged={s.EngagedKey ?? "-"} killsSoFar={kills}");
            }
            GD.Print($"MONSTER_HP done engagedLife={EngagedLife(s)} engaged={s.EngagedKey ?? "-"}" +
                $" xp={s.Character.Xp} was={xp0}");
            // print ONLY on real session state change: a kill AND XP actually granted
            if (kills >= 1 && s.Character.Xp > xp0)
                GD.Print($"MONSTER_DEFEATED area={s.CurrentArea} key={MonsterKey} n={kills}");
        }
        else GD.PushWarning("hero not adjacent to the spawn cell — no Attack call, no marker");

        // ---- loot if the kill queued a drop ----
        if (s.Drops.Pending > 0)
        {
            var lr = s.Loot(1);
            GD.Print($"LOOT taken={lr.Taken} gold={lr.GoldCollected} total={s.Gold}");
        }
        else GD.Print("LOOT pending=0");

        if (steps >= 3 && cells.Count >= 3 && kills >= 1 && s.CurrentArea == Dest)
            GD.Print("JOURNEY_OK");
        else GD.PushError("journey chain incomplete — the lines above are the evidence");
    }

    /// <summary>The spawn cell farthest from arrival (Chebyshev — the Sim's ONE
    /// metric via WorldPos.DistanceTo), the walk goal with room for >=3 steps.</summary>
    private static Vector2I FarthestFrom(Vector2I center, List<(string Key, Vector2I Cell)> row)
    {
        var c = new WorldPos(center.X, center.Y);
        var best = row[0].Cell;
        int bestD = -1;
        foreach (var e in row)
        {
            int d = c.DistanceTo(new WorldPos(e.Cell.X, e.Cell.Y));
            if (d > bestD) { bestD = d; best = e.Cell; }
        }
        return best;
    }

    /// <summary>Cell adjacency on the ONE metric (reuses IsoCamera.InRange).</summary>
    private static bool InRange(Vector2I a, Vector2I b, int cells) =>
        IsoCamera.InRange(new WorldPos(a.X, a.Y), new WorldPos(b.X, b.Y), cells);

    /// <summary>The engaged monster's live Life read through the §2 surface: the
    /// session's own Dump() JSON (camelCase engagedLife) — no Sim internal touched.</summary>
    private static long EngagedLife(GameSession s)
    {
        using var doc = JsonDocument.Parse(s.Dump());
        return doc.RootElement.GetProperty("engagedLife").GetInt64();
    }
}
