using System;
using System.Collections.Generic;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// Click-to-move hero — consumes [input] actions ONLY (move_to LMB, attack RMB),
/// no polling. Hero grid/world position lives HERE ALONE (the Sim stores none and
/// the bridge does not widen — the ARCH-declared row plan-c2 §0 re-verifies).
/// Path = A* over AreaRenderer's walkable cells (WORLD-LOCAL), Chebyshev steps;
/// distance reads reuse WorldPos.DistanceTo. In-area walking is DECLARED
/// presentation-owned (plan-c2 S4 row 1: "presentation A* (NO sim call)"); the
/// edge door WALKS then fires the travel door. A click on the LIT standing-stone
/// marker RIDES (P9) through the SAME door and the SAME hint path.
/// Attack / loot route through the §2 bridge surface; rejections become HUD hints.
/// The door intent carries the WAYPOINT ORDER CONTRACT verbatim (v10 §game/):
/// light the DESTINATION stone BEFORE the travel needing it — discover-AFTER-
/// travel is a guaranteed throw and is NOT implemented anywhere. Since
/// ORCH-RULING 2026-11-01 the ONE door also WALKS a linkedTo neighbour with no
/// stone at all (there is nothing to light, and the walk needs none).
/// </summary>
public partial class HeroController : Node2D
{
    private const float StepInterval = 0.2f; // seconds per cell (convention, changeable)

    private AreaRenderer _tiles = null!;
    private MonsterField _monsters = null!;
    private Hud _hud = null!;
    private Sprite2D _sprite = null!;

    private WorldPos _pos = new(0, 0);
    private readonly List<WorldPos> _path = [];
    private Vector2I? _doorTarget; // set when the walk lands on an edge door
    private float _stepTimer;
    private string _facing = "S";

    /// <summary>Wired by Act: after a SUCCESSFUL travel the world (tiles + monster
    /// row + hero placement + camera) is rebuilt under the NEW area — the door
    /// path and the headless journey leg share this ONE rebuild entry.</summary>
    public Action? OnAreaChanged;

    public void Wire(AreaRenderer tiles, MonsterField monsters, Hud hud)
    {
        _tiles = tiles;
        _monsters = monsters;
        _hud = hud;
    }

    /// <summary>Arrival placement (New Game / after a stone-carries-you travel).</summary>
    public void Place(Vector2I cell)
    {
        _pos = new WorldPos(cell.X, cell.Y);
        _path.Clear();
        _doorTarget = null;
        // The sprite rides the PLACEMENT too (S4 legs found: before, an arrival
        // or a stone RIDE left the sprite at its old spot until the first walk
        // tick — the hero stood invisible at the origin on respawn and ride-in).
        if (_sprite is not null) _sprite.Position = IsoCamera.GridToScreen(cell);
    }

    /// <summary>The committed grid cell of the hero — the marker/read surface;
    /// hero position lives HERE ALONE (the Sim stores none).</summary>
    public Vector2I Cell => new(_pos.X, _pos.Y);

    /// <summary>Scripts a walk intent (the headless marker leg): the SAME A* the
    /// windowed click plans; an unwalkable goal plans nothing.</summary>
    public void WalkTo(Vector2I cell)
    {
        if (_tiles.IsWalkable(cell)) PlanPath(new WorldPos(cell.X, cell.Y));
    }

    /// <summary>Commits ONE discrete Chebyshev step — timer-driven in the windowed
    /// game, scripted directly under headless. False when the path is spent.</summary>
    public bool StepNext()
    {
        if (_path.Count == 0) return false;
        var next = _path[0];
        _path.RemoveAt(0);
        SetFacing(next);
        _pos = next; // discrete Chebyshev step (easing joins with the real frames)
        if (_sprite is not null)
            _sprite.Position = IsoCamera.GridToScreen(new Vector2I(_pos.X, _pos.Y));
        return true;
    }

    public override void _Ready()
    {
        // 128×128 hero frames at scale 1.0, feet at cell centre (§3 SPRITE DRAW SCALE).
        _sprite = new Sprite2D
        {
            Texture = HeroFrame(_facing),
            Centered = true,
            Offset = new Vector2(0, -64f),
            YSortEnabled = true,
        };
        AddChild(_sprite);
    }

    /// <summary>Committed cell while a walk path is still playing (S4 driver
    /// read: a stone RIDE lands with NO path steps — plan P9's [C] check).</summary>
    public bool IsWalking => _path.Count > 0;

    /// <summary>A cell's world px → WINDOW px under the live camera (the read the
    /// S4 driver clicks by; the inverse of the input path's own transform use).</summary>
    public Vector2 CellToScreen(Vector2I cell) => GetCanvasTransform() * IsoCamera.GridToScreen(cell);

    public override void _UnhandledInput(InputEvent @event)
    {
        var s = GameContext.Session;
        if (s is null || @event is not InputEventMouseButton mb || !mb.Pressed) return;
        Vector2 world = GetCanvasTransform().AffineInverse() * mb.GlobalPosition;

        if (mb.IsActionPressed("attack")) { TryAttack(s, world); return; }
        if (!mb.IsActionPressed("move_to")) return;

        var cell = IsoCamera.ScreenToGrid(world);
        // The M5-S4 decision table (ClickMap, xunit-pinned): the LIT standing-stone
        // marker rides, walkable ground walks (presentation A*, NO sim call —
        // plan-c2 S4 row 1; the Sim stores no positions, the ARCH-declared row
        // the hero cell here rests on), everything else refuses — out-of-range
        // included: no path is ever planned to an off-grid click.
        switch (ClickMap.DecideLeft(cell.X, cell.Y, AreaRenderer.W, AreaRenderer.H,
                     _tiles.IsWalkable(cell), _tiles.IsLitStone(cell)))
        {
            case ClickMap.Left.Ride: RideStone(s); break;
            case ClickMap.Left.Move: PlanPath(new WorldPos(cell.X, cell.Y)); break;
            default: break; // refuse: blocked ground or off-grid
        }
    }

    /// <summary>RMB: strike the monster under the cursor; pending loot is taken in
    /// the same click (Loot key → Loot(1)). M5-S3 (MC 11934): both doors ride the
    /// CommandLog.Call wrapper — with the bridge OFF it is the plain call, with it
    /// ON each lands as one CLI-grammar line (a rejection as its comment), and the
    /// hint catch below is untouched. M5-S4 (FIX-DA5 5): a kill despawns one
    /// sprite of the attacked row — the Sim spawns a fresh monster per kill, so
    /// the sprite roster is presentation truth this class alone maintains.</summary>
    private void TryAttack(GameSession s, Vector2 world)
    {
        var key = _monsters.MonsterAt(world);
        if (key is null) return;
        try
        {
            GameSession.AttackReport? report = null;
            CommandLog.Call($"attack {key} 1", () => report = s.Attack(key, 1));
            if (report?.Kills > 0) _monsters.Despawn(key); // kill → sprite gone (never the reverse)
            if (s.Drops.Pending > 0) CommandLog.Call("loot 1", () => s.Loot(1));
        }
        catch (SessionRejectedException e) { _hud.Hint(e.Message); } // never a crash
    }

    public override void _Process(double delta)
    {
        if (_path.Count == 0) return;
        _stepTimer += (float)delta;
        if (_stepTimer < StepInterval) return;
        _stepTimer = 0f;

        StepNext();
        if (_path.Count == 0 && _doorTarget is { } door)
        {
            _doorTarget = null;
            TravelIntent(door);
        }
    }

    // ---- the WAYPOINT ORDER CONTRACT (v10 §game/, the ONE wording) ----

    /// <summary>Standing on an area-edge door: light the DESTINATION stone BEFORE
    /// the travel that needs it (the WAYPOINT ORDER CONTRACT — a destination that
    /// CARRIES a stone), then travel through the ONE door; a destination with no
    /// stone (ORCH-RULING 2026-11-01) is REACHED ON FOOT — there is nothing to
    /// light and the walk door needs none. A rejection is a HUD hint.</summary>
    public void TravelIntent(Vector2I doorCell)
    {
        var s = GameContext.Session;
        if (s is null) return;
        var dest = DoorArea(s, doorCell);
        if (dest is not null) TravelDoor(s, dest);
    }

    /// <summary>P9 ride half (S4 map row 2): the LMB landed on the LIT standing-stone
    /// marker → ClickMap's ride gate resolves the destination from the session's
    /// OWN waypoint truth (dark stones never ride), then the SAME travel door the
    /// edge walks use fires — the hint path is reused verbatim, and no path step
    /// is ever taken (the [C] read: Area flips with IsWalking false).</summary>
    private void RideStone(GameSession s)
    {
        var dest = ClickMap.RideTarget(s.CurrentArea, s.Areas.Get(s.CurrentArea).LinkedTo,
            s.Waypoints.HasNode, s.Waypoints.IsDiscovered);
        if (dest is null) { _hud.Hint("no lit stone answers this one"); return; } // gate, never a door call
        TravelDoor(s, dest);
    }

    /// <summary>THE ONE travel door call site (edge walk AND stone ride share it):
    /// M5-S3 (MC 11934) discover/waypoint log as the CLI's own lines; a rejection
    /// is a HUD hint.</summary>
    private void TravelDoor(GameSession s, string dest)
    {
        try
        {
            if (s.Waypoints.HasNode(dest) && !s.Waypoints.IsDiscovered(dest))
                CommandLog.Call($"discover {dest}", () => s.DiscoverWaypoint(dest));
            CommandLog.Call($"waypoint {dest}", () => s.TravelTo(dest));
            OnAreaChanged?.Invoke(); // Act rebuilds tiles + monsters + placement under the new area
            _hud.Hint(s.Waypoints.IsDiscovered(dest)
                ? $"the stone carries you to {dest}"
                : $"you walk to {dest}"); // the walked half of the door (stone-less ground)
        }
        catch (SessionRejectedException e) { _hud.Hint(e.Message); }
    }

    /// <summary>The linked area whose edge this door stands on (edge order:
    /// 0=east, 1=south, 2=west, 3=north — the AreaRenderer.DoorCell edges).</summary>
    private static string? DoorArea(GameSession s, Vector2I cell)
    {
        var links = s.Areas.Get(s.CurrentArea).LinkedTo;
        if (links.Count == 0) return null;
        int edge = cell.X == AreaRenderer.W - 1 ? 0 : cell.Y == AreaRenderer.H - 1 ? 1 :
                   cell.X == 0 ? 2 : 3;
        return links[Math.Min(edge, links.Count - 1)];
    }

    // ---- A* over the walkable cells (Chebyshev steps, the one metric reused) ----

    private void PlanPath(WorldPos goal)
    {
        _path.Clear();
        _doorTarget = null;
        var came = new Dictionary<WorldPos, WorldPos>();
        var dist = new Dictionary<WorldPos, int> { [_pos] = 0 };
        var open = new List<WorldPos> { _pos };
        while (open.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < open.Count; i++)
                if (Rank(open[i], dist, goal) < Rank(open[best], dist, goal)) best = i;
            var cur = open[best];
            open.RemoveAt(best);
            if (cur == goal) { Rebuild(came, goal); return; }
            foreach (var nb in Neighbours(cur))
            {
                int step = dist[cur] + 1;
                if (dist.TryGetValue(nb, out var old) && old <= step) continue;
                dist[nb] = step;
                came[nb] = cur;
                open.Add(nb);
            }
        }
    }

    private static int Rank(WorldPos p, Dictionary<WorldPos, int> dist, WorldPos goal) =>
        dist[p] + p.DistanceTo(goal); // f = g + h, Chebyshev h — the Sim's metric, reused

    private IEnumerable<WorldPos> Neighbours(WorldPos p)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if ((dx != 0 || dy != 0) && _tiles.IsWalkable(new Vector2I(p.X + dx, p.Y + dy)))
                    yield return new WorldPos(p.X + dx, p.Y + dy);
    }

    private void Rebuild(Dictionary<WorldPos, WorldPos> came, WorldPos goal)
    {
        for (var c = goal; !c.Equals(_pos); c = came[c])
            _path.Insert(0, c);
        var cell = new Vector2I(goal.X, goal.Y);
        if (IsEdgeDoor(cell)) _doorTarget = cell;
    }

    private static bool IsEdgeDoor(Vector2I c) =>
        c.X == 0 || c.Y == 0 || c.X == AreaRenderer.W - 1 || c.Y == AreaRenderer.H - 1;

    // ---- facing (screen-dominant step on the 2:1 map, ties go S) ----

    private void SetFacing(WorldPos next)
    {
        int sx = (next.X - next.Y) - (_pos.X - _pos.Y); // screen-x component of the step
        int sy = (next.X + next.Y) - (_pos.X + _pos.Y); // screen-y component of the step
        _facing = sy switch
        {
            > 0 => "S", // down-screen, toward camera
            < 0 => "N",
            _ => sx > 0 ? "E" : sx < 0 ? "W" : _facing, // ties go S
        };
        if (_sprite is not null) _sprite.Texture = HeroFrame(_facing);
    }

    private static Texture2D HeroFrame(string dir) =>
        AreaRenderer.ArtOrNull($"res://assets/hero/hero-idle-{dir}.png")
        ?? AreaRenderer.Diamond(new Color("D8CBA6"), 128, 128);
}
