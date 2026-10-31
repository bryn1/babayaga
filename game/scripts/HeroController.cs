using System;
using System.Collections.Generic;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// Click-to-move hero — consumes [input] actions ONLY (move_to LMB, attack RMB),
/// no polling. Hero grid/world position lives HERE ALONE (the Sim stores none and
/// the bridge does not widen). Path = A* over AreaRenderer's walkable cells
/// (WORLD-LOCAL), Chebyshev steps; distance reads reuse WorldPos.DistanceTo.
/// Attack / loot route through the §2 bridge surface; rejections become HUD hints.
/// The door intent carries the WAYPOINT ORDER CONTRACT verbatim (v10 §game/):
/// light the DESTINATION stone BEFORE the travel needing it — discover-AFTER-
/// travel is a guaranteed throw and is NOT implemented anywhere.
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

    public override void _UnhandledInput(InputEvent @event)
    {
        var s = GameContext.Session;
        if (s is null || @event is not InputEventMouseButton mb || !mb.Pressed) return;
        Vector2 world = GetCanvasTransform().AffineInverse() * mb.GlobalPosition;

        if (mb.IsActionPressed("attack")) { TryAttack(s, world); return; }
        if (!mb.IsActionPressed("move_to")) return;

        var cell = IsoCamera.ScreenToGrid(world);
        if (!_tiles.IsWalkable(cell)) return; // no path to an unwalkable cell
        PlanPath(new WorldPos(cell.X, cell.Y));
    }

    /// <summary>RMB: strike the monster under the cursor; pending loot is taken in
    /// the same click (Loot key → Loot(1)).</summary>
    private void TryAttack(GameSession s, Vector2 world)
    {
        var key = _monsters.MonsterAt(world);
        if (key is null) return;
        try
        {
            s.Attack(key, 1);
            if (s.Drops.Pending > 0) s.Loot(1);
        }
        catch (SessionRejectedException e) { _hud.Hint(e.Message); } // never a crash
    }

    public override void _Process(double delta)
    {
        if (_path.Count == 0) return;
        _stepTimer += (float)delta;
        if (_stepTimer < StepInterval) return;
        _stepTimer = 0f;

        var next = _path[0];
        _path.RemoveAt(0);
        SetFacing(next);
        _pos = next; // discrete Chebyshev step (easing joins with the real frames)
        if (_sprite is not null)
            _sprite.Position = IsoCamera.GridToScreen(new Vector2I(_pos.X, _pos.Y));
        if (_path.Count == 0 && _doorTarget is { } door)
        {
            _doorTarget = null;
            TravelIntent(door);
        }
    }

    // ---- the WAYPOINT ORDER CONTRACT (v10 §game/, the ONE wording) ----

    /// <summary>Standing on an area-edge door: light the DESTINATION stone BEFORE
    /// the travel that needs it, then travel; a rejection is a HUD hint.</summary>
    public void TravelIntent(Vector2I doorCell)
    {
        var s = GameContext.Session;
        if (s is null) return;
        var dest = DoorArea(s, doorCell);
        if (dest is null) return;
        try
        {
            if (!s.Waypoints.IsDiscovered(dest)) s.DiscoverWaypoint(dest);
            s.TravelTo(dest);
            _hud.Hint($"the stone carries you to {dest}");
            Place(_tiles.CenterCell());
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
