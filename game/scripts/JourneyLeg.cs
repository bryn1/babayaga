using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// The M4 headless marker legs — the walking hero striking ONE of EVERY monster
/// kind the data table spawns in mchy — run ONCE by Boot's headless branch
/// through the §2 bridge surface ONLY (GameSession public members + the public
/// table loaders MonsterTable/XpTable; the Sim's internals are assembly-invisible
/// to this project). Chain: New (already run) -> IsDiscovered -> DiscoverWaypoint
/// -> TravelTo (the pinned waypoint order — discover the DESTINATION stone BEFORE
/// the travel, never after-travel) -> per-kind scripted Chebyshev walks (deepest
/// kind first so the deep walk alone pins >=3 `HERO_STEP n=<int> cell=<x>,<y>
/// area=<string>` lines read from REAL hero+session state) -> adjacency-gated
/// `Attack` -> `MONSTER_DEFEATED area=mchy key=<kind> n=<int>` ONLY on a real
/// session state change (kills>=1 AND XP actually granted), once per kind. Then
/// the XP-SUM HONESTY LEG: the expected XP SUM is derived from data/monsters.json
/// + data/xp-ladder.json through the SAME public loaders and the SAME chain the
/// combat gate pays (MonsterTable.StatsAt + XpTable.KillXp at the hero level in
/// force per kill) — never a hardcoded number — printed as `XP_EXPECT sum=<n>
/// actual=<m>` and honoured by `XP_SUM ok` ONLY when expected == the real session
/// delta. Loot when a drop is pending. The strike gate is IsoCamera.InRange —
/// not adjacent = no Attack call = no marker (the leg's planted-red shape).
/// Monster HP before/after each kill reads the session's own Dump() JSON
/// (engagedLife), the one public mirror of the engaged monster. JOURNEY_OK prints
/// only when the whole chain completed AND the XP-sum honesty leg held.
/// </summary>
public static class JourneyLeg
{
    private const string Dest = "mchy";
    private const int StrikeCap = 64; // safety stop; the kill lands far earlier (seed-fixed)

    public static void Run(GameSession s, Act act)
    {
        // ---- the WAYPOINT ORDER CONTRACT: light the DESTINATION stone BEFORE travel ----
        GD.Print($"WAYPOINT before area={s.CurrentArea} lit={Dest}={s.Waypoints.IsDiscovered(Dest)}");
        if (!s.Waypoints.IsDiscovered(Dest)) s.DiscoverWaypoint(Dest);
        s.TravelTo(Dest);
        GD.Print($"WAYPOINT after area={s.CurrentArea} lit={Dest}={s.Waypoints.IsDiscovered(Dest)}");
        act.Rebuild(s.CurrentArea); // windowed travel reaches this via HeroController.OnAreaChanged

        // ---- spawn row: the Fnv-seed determinism read (two runs equal; a data-table
        // count change differs — the planted-red shape of that gate) ----
        var row = act.Monsters.RowSnapshot();
        for (int i = 0; i < row.Count; i++)
            GD.Print($"MONSTER_SPAWN i={i} key={row[i].Key} cell={row[i].Cell.X},{row[i].Cell.Y}");
        if (row.Count == 0) { GD.PushError("journey leg: mchy spawned no monster — chain incomplete"); return; }

        // ---- one kill per KIND, deepest kind first (the deep walk pins the step count) ----
        var center = act.Tiles.CenterCell();
        var targets = row.GroupBy(e => e.Key)
            .Select(g => (Key: g.Key, Cell: FarthestFrom(center, g.ToList())))
            .OrderByDescending(t => Dist(center, t.Cell))
            .ToList();

        var hero = act.Hero;
        int steps = 0, defeated = 0;
        long expected = 0, actual = 0;
        var cells = new HashSet<Vector2I>();
        var area = s.Areas.Get(Dest);
        var mons = MonsterTable.Load(Path.Combine(GameContext.DataDir(), "monsters.json"));
        var ladder = XpTable.Load(Path.Combine(GameContext.DataDir(), "xp-ladder.json"));

        foreach (var (key, target) in targets)
        {
            hero.WalkTo(target);
            while (hero.StepNext())
            {
                steps++;
                cells.Add(hero.Cell); // REAL hero state on every line — never a literal
                GD.Print($"HERO_STEP n={steps} cell={hero.Cell.X},{hero.Cell.Y} area={s.CurrentArea}");
            }

            // strike: the real Attack verb, gated on standing adjacent to the spawn cell
            if (!InRange(hero.Cell, target, 1))
            {
                GD.PushWarning($"hero not adjacent to {key} at {target.X},{target.Y} — no Attack call, no marker");
                continue;
            }
            long xp0 = s.Character.Xp;
            int heroLevel = s.Character.Level; // the level in force when the kill pays
            int kills = 0;
            for (int strike = 1; kills == 0 && strike <= StrikeCap; strike++)
            {
                var r = s.Attack(key, 1); // §2 bridge verb, one strike per call
                kills = r.Kills;
                GD.Print($"MONSTER_HP strike={strike} engagedLife={EngagedLife(s)}" +
                    $" engaged={s.EngagedKey ?? "-"} killsSoFar={kills}");
            }
            GD.Print($"MONSTER_HP done engagedLife={EngagedLife(s)} engaged={s.EngagedKey ?? "-"}" +
                $" xp={s.Character.Xp} was={xp0}");
            // print ONLY on real session state change: a kill AND XP actually granted
            if (kills >= 1 && s.Character.Xp > xp0)
            {
                GD.Print($"MONSTER_DEFEATED area={s.CurrentArea} key={key} n={kills}");
                defeated++;
                actual += s.Character.Xp - xp0;
                expected += ExpectedKillXp(mons, ladder, area, key, heroLevel);
            }

            // loot if this kill queued a drop
            if (s.Drops.Pending > 0)
            {
                var lr = s.Loot(1);
                GD.Print($"LOOT taken={lr.Taken} gold={lr.GoldCollected} total={s.Gold}");
            }
            else GD.Print("LOOT pending=0");
        }

        // ---- the XP-SUM honesty leg: expected from the data tables, actual from the session ----
        GD.Print($"XP_EXPECT sum={expected} actual={actual}");
        bool xpHonest = expected == actual;
        if (xpHonest) GD.Print($"XP_SUM ok expected={expected} actual={actual}");
        else GD.PushError($"XP-SUM honesty leg: expected {expected} != session delta {actual}");

        if (steps >= 3 && cells.Count >= 3 && defeated == targets.Count && xpHonest &&
            s.CurrentArea == Dest)
            GD.Print("JOURNEY_OK");
        else GD.PushError("journey chain incomplete — the lines above are the evidence");
    }

    /// <summary>The XP the data tables owe for one kill of this kind — the SAME
    /// derivation the combat gate pays (P1 §1.7): spawn-level resolution, the
    /// growth-scaled base XP, then the KillXp chain (KillType Normal, solo) at
    /// the hero level in force. Nothing here is a hardcoded number.</summary>
    private static long ExpectedKillXp(
        MonsterTable mons, XpTable ladder, AreaRow area, string key, int heroLevel)
    {
        var row = mons.Get(key);
        int monLevel = MonsterTable.ResolveSpawnLevel(row, Difficulty.Ordinary, area);
        return ladder.KillXp(mons.StatsAt(row, monLevel).Xp, KillType.Normal, 1, heroLevel, monLevel);
    }

    /// <summary>The spawn cell farthest from arrival (Chebyshev — the Sim's ONE
    /// metric via WorldPos.DistanceTo), the walk goal with room for >=3 steps.</summary>
    private static Vector2I FarthestFrom(Vector2I center, List<(string Key, Vector2I Cell)> row)
    {
        var best = row[0].Cell;
        int bestD = -1;
        foreach (var e in row)
        {
            int d = Dist(center, e.Cell);
            if (d > bestD) { bestD = d; best = e.Cell; }
        }
        return best;
    }

    private static int Dist(Vector2I a, Vector2I b) =>
        new WorldPos(a.X, a.Y).DistanceTo(new WorldPos(b.X, b.Y));

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
