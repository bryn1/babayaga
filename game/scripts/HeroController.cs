// reason (file-hygiene, MC 12153 + MC 12162): past 400 — the walk-pose cycle, the attack
// pose and the attack flash animate THE ONE sprite node whose glide this class already
// pumps in _Process; handing Texture/Modulate to a second file would split one node's
// per-frame owner across files.
using System;
using System.Collections.Generic;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// Click-to-move hero — consumes [input] actions ONLY (move_to LMB — since
/// MC 12136 an LMB ON A MONSTER rides the attack door first, the D2 idiom the
/// owner expects — attack RMB), no polling. Hero grid/world position lives
/// HERE ALONE (the Sim stores none and
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

    /// <summary>MC 12149 RED-CONTROL (the gate proving it can go red): set to any
    /// value and steps SNAP again (zero-length glide) — SmoothLeg's mid-glide
    /// assertion goes red against it, green again when unset.</summary>
    public const string SnapVar = "BABA_SNAP_MODE";

    /// <summary>MC 12153 RED-CONTROL for the ANIM LEG: set to any value and the
    /// shipped walk poses + the attack flash never play (single idle frame, static
    /// Modulate) — AnimLeg's frame/attack assertions go red against it.</summary>
    public const string AnimOffVar = "BABA_ANIM_OFF";

    /// <summary>MC 12162 RED-CONTROL for the ANIM LEG's attack-POSE assertions: set to
    /// any value and the hero-attack sheet never reaches the sprite (the pose window
    /// never opens; walk poses and the flash keep playing) — AnimLeg's
    /// pose-on-screen / pose-resumed assertions go red against it.</summary>
    public const string NoAttackPoseVar = "BABA_NO_ATTACK_POSE";

    private AreaRenderer _tiles = null!;
    private MonsterField _monsters = null!;
    private Hud _hud = null!;
    private Sprite2D _sprite = null!;

    private WorldPos _pos = new(0, 0);
    private readonly List<WorldPos> _path = [];
    private Vector2I? _doorTarget; // set when the walk lands on an edge door
    private float _stepTimer;
    private string _facing = "S";

    // MC 12149 PRESENTATION GLIDE (owner complaint "the movement is very unsmooth" —
    // a PRESENTATION bug: the sim step stays discrete, the sprite animates from the
    // cell it was showing to the newly committed one over the step interval).
    private Vector2 _glideFrom, _glideTo;
    private float _glideT = 1f; // 1 = sitting on the committed cell, no glide in flight
    private bool _snap;         // SnapVar set → zero-length glide (the RED lever)

    // MC 12153 PRESENTATION ANIMATION (owner complaint "no attack motions or
    // animations" — the shipped sheets PLAY now, nothing is invented): the hero
    // cycle is the TWO poses that actually exist per direction (hero-idle-<dir>
    // + hero-walk-<dir>, both 128×128 — the census in out/leg-anim/EVIDENCE.md);
    // the frame index rides the glide progress: walk pose from the step's commit
    // to t=0.5, the planted idle pose for its second half; at rest the idle
    // pose always stands. A committed attack plays the shipped hero-attack-<dir>
    // sheet (MC 12154 art, wired MC 12162) for ~200 ms, then the normal
    // idle/walk selection RESUMES; the 130 ms white over-exposure KEPT on top of
    // the pose (the strike's intensity cue) + the red hit-flash on the monster.
    private const float AttackFlashDur = 0.13f; // brief's 100–150 ms window
    private const float AttackPoseDur = 0.2f;   // MC 12162: ~200 ms pose window
    private bool _animOff;            // AnimOffVar set → frozen frame + no flash (RED lever)
    private bool _noAttackPose;       // NoAttackPoseVar set → attack sheet never plays (RED lever)
    private bool _showWalk;           // the WALK pose texture is what stands on screen
    private float _attackFlashT;      // attack-flash seconds remaining (0 = none)
    private float _attackPoseT;       // attack-pose seconds remaining (0 = none)

    /// <summary>MC 12153: true while the sprite shows the walk pose of the shipped
    /// two-frame hero cycle — the AnimLeg presentation read.</summary>
    public bool ShowingWalkFrame => _showWalk;

    /// <summary>MC 12153: the sprite's current texture (the AnimLeg's distinct-frame
    /// count; identity compares, GD.Load hands out cached instances).</summary>
    public Texture2D CurrentHeroFrame => _sprite!.Texture;

    /// <summary>MC 12153: the sprite's Modulate — the AnimLeg's attack-trigger read
    /// (the white flash deviates from plain white; a static hero answers white).</summary>
    public Color HeroSpriteModulate => _sprite!.Modulate;

    /// <summary>MC 12162: the resource path of the sprite's CURRENT texture — the
    /// AnimLeg's attack-pose read: "hero-attack" is IN it during the pose window and
    /// gone (idle/walk back) after it. Placeholder pass answers "".</summary>
    public string CurrentHeroFramePath => _sprite?.Texture?.ResourcePath ?? "";

    /// <summary>Wired by Act: after a SUCCESSFUL travel the world (tiles + monster
    /// row + hero placement + camera) is rebuilt under the NEW area — the door
    /// path and the headless journey leg share this ONE rebuild entry.</summary>
    public Action? OnAreaChanged;

    public void Wire(AreaRenderer tiles, MonsterField monsters, Hud hud)
    {
        _tiles = tiles;
        _monsters = monsters;
        _hud = hud;
        AddControlsLine(hud); // MC 12136: the controls ride the HUD, never only the README
    }

    /// <summary>MC 12136 CONTROLS-ON-HUD (the owner-feel fix: RMB-attack had ZERO
    /// in-game exposure — "not in MainMenu, not in any HUD line, no hint ever",
    /// TEST-verdict-ownerfeel-m5 §6): ONE persistent small line, bottom-left, on the
    /// HUD's own CanvasLayer so it never pans with the camera — present on the FIRST
    /// game screen (town) and every area after. It rides a PanelContainer so
    /// S4InputLeg.HintText (which reads Hud's first direct Label child) keeps reading
    /// the hint alone, and MouseFilterLaw keeps the HUD CLICK LAW: world clicks pass
    /// THROUGH this part. HeroController wires it here because this class owns the
    /// input map the line describes (the S4 row's owner).</summary>
    private static void AddControlsLine(Hud hud)
    {
        var panel = new PanelContainer
        {
            Name = "ControlsLine",
            MouseFilter = MouseFilterLaw.For("hud.panel"), // the click law: Ignore — clicks pass through
            Modulate = new Color(1f, 1f, 1f, 0.88f),
        };
        // The bottom-left corner it rides — the METHOD, not the editor property: a
        // code-built PanelContainer ignores the AnchorsPreset PROPERTY (probe of the
        // proof run: pos (8,-44), the anchors stayed (0,0) — the line painted over
        // the HUD's Life row / vanished). SetAnchorsPreset then explicit offsets.
        panel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        panel.OffsetLeft = 8; panel.OffsetTop = -44; panel.OffsetRight = 8; panel.OffsetBottom = -10;
        panel.AddChild(new Label
        {
            Text = "LMB: attack / move · RMB: attack · keys per help",
            MouseFilter = MouseFilterLaw.For("hud.panel"),
        });
        hud.AddChild(panel);
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
        if (_sprite is not null)
        {
            var p = IsoCamera.GridToScreen(cell);
            // MC 12149: an arrival (or an area jump) is a CUT, never a slide —
            // the glide state is cancelled and parked on the placement point.
            _glideFrom = _glideTo = p;
            _glideT = 1f;
            _sprite.Position = p;
            SetWalkPose(false); // MC 12153: an arrival stands the IDLE pose (at-rest law)
        }
    }

    /// <summary>The committed grid cell of the hero — the marker/read surface;
    /// hero position lives HERE ALONE (the Sim stores none).</summary>
    public Vector2I Cell => new(_pos.X, _pos.Y);

    /// <summary>MC 12149: the sprite's CURRENT world px — mid-step the interpolated
    /// visual position the camera rides; otherwise the committed cell's lattice
    /// point. Presentation truth only, the committed cell above stays the ONE
    /// sim-side read for targeting, hit tests and every leg marker.</summary>
    public Vector2 VisualPos => _sprite is { } s ? s.Position : IsoCamera.GridToScreen(Cell);

    /// <summary>MC 12149: true while a committed step's glide is still animating
    /// (presentation read for the legs; the sim committed the moment it turned false).</summary>
    public bool IsGliding => _glideT < 1f;

    /// <summary>Scripts a walk intent (the headless marker leg): the SAME A* the
    /// windowed click plans; an unwalkable goal plans nothing.</summary>
    public void WalkTo(Vector2I cell)
    {
        if (_tiles.IsWalkable(cell)) PlanPath(new WorldPos(cell.X, cell.Y));
    }

    /// <summary>Commits ONE discrete Chebyshev step — timer-driven in the windowed
    /// game, scripted directly under headless. False when the path is spent.
    /// MC 12149: the commit stays discrete (the story byte-anchor is the proof);
    /// only the SPRITE is handed a glide from its shown position to the new
    /// cell's lattice point, animated by _Process over the step interval.</summary>
    public bool StepNext()
    {
        if (_path.Count == 0) return false;
        var next = _path[0];
        _path.RemoveAt(0);
        SetFacing(next);
        _pos = next; // discrete Chebyshev step — the sim truth NEVER interpolates
        if (_sprite is not null)
        {
            _glideFrom = _sprite.Position; // resume mid-glide from WHERE IT SHOWS
            _glideTo = IsoCamera.GridToScreen(new Vector2I(_pos.X, _pos.Y));
            if (_snap) { _sprite.Position = _glideTo; _glideT = 1f; } // RED lever: snap mode (SnapVar)
            else
            {
                _glideT = 0f;
                // MC 12153 frame index from glide progress + direction: the step
                // COMMIT shows this facing's shipped walk pose; _Process returns
                // the planted idle pose once the glide passes its half-way point.
                SetWalkPose(!_animOff);
            }
        }
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
        _snap = System.Environment.GetEnvironmentVariable(SnapVar) is { Length: > 0 }; // MC 12149 RED lever
        _animOff = System.Environment.GetEnvironmentVariable(AnimOffVar) is { Length: > 0 }; // MC 12153 RED lever
        _noAttackPose = System.Environment.GetEnvironmentVariable(NoAttackPoseVar) is { Length: > 0 }; // MC 12162 RED lever
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
        // The M5-S4 decision table (ClickMap, xunit-pinned): MC 12136 (the owner's
        // DEAD core loop — LMB on a monster only ever walked) puts the monster read
        // FIRST: an LMB on a standing monster rides the SAME TryAttack door RMB uses
        // (the verdict is the table's, the px hit-test is MonsterAt's — the drawn
        // body, not the cell). The LIT standing-stone marker still rides, walkable
        // ground still walks (presentation A*, NO sim call — plan-c2 S4 row 1; the
        // Sim stores no positions, the ARCH-declared row the hero cell here rests
        // on), everything else refuses — out-of-range included: no path is ever
        // planned to an off-grid click.
        switch (ClickMap.DecideLeft(cell.X, cell.Y, AreaRenderer.W, AreaRenderer.H,
                     _tiles.IsWalkable(cell), _tiles.IsLitStone(cell),
                     onMonster: _monsters.MonsterAt(world) is not null))
        {
            case ClickMap.Left.Attack: TryAttack(s, world); break; // LMB strikes (D2 idiom)
            case ClickMap.Left.Ride: RideStone(s); break;
            case ClickMap.Left.Move: PlanPath(new WorldPos(cell.X, cell.Y)); break;
            default: break; // refuse: blocked ground or off-grid
        }
    }

    /// <summary>The attack door BOTH mouse buttons ride (RMB always; LMB since
    /// MC 12136 on a monster's drawn body): strike the monster under the cursor;
    /// pending loot is taken in
    /// the same click (Loot key → Loot(1)). M5-S3 (MC 11934): both doors ride the
    /// CommandLog.Call wrapper — with the bridge OFF it is the plain call, with it
    /// ON each lands as one CLI-grammar line (a rejection as its comment), and the
    /// hint catch below is untouched. M5-S4 (FIX-DA5 5): a kill despawns one
    /// sprite of the attacked row; since MC 12136 the freed cell STAYS dead for the
    /// session (MonsterField.Despawn records it) — the Sim's fresh-monster rule
    /// stays the Sim's own math (the replay contract is untouched), the sprite
    /// roster is presentation truth this class alone maintains.</summary>
    private void TryAttack(GameSession s, Vector2 world)
    {
        var key = _monsters.MonsterAt(world);
        if (key is null) return;
        try
        {
            GameSession.AttackReport? report = null;
            CommandLog.Call($"attack {key} 1", () => report = s.Attack(key, 1));
            // MC 12153: ONE feedback burst per COMMITTED attack (a rejection threw
            // past this line — the hint answers those alone): the hero's white
            // over-exposure + the hit monster's red flash. No attack FRAMES exist
            // (census: EVIDENCE) — the flash is the honest stand-in, the sheet is
            // the art order. Flash BEFORE the despawn: the hit sprite still stands.
            FlashAttack(key);
            if (report?.Kills > 0) _monsters.Despawn(key); // kill → sprite gone (never the reverse)
            if (s.Drops.Pending > 0) CommandLog.Call("loot 1", () => s.Loot(1));
        }
        catch (SessionRejectedException e) { _hud.Hint(e.Message); } // never a crash
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        // MC 12149 GLIDE (presentation only): animate the in-flight step across the
        // interval. Pumps BEFORE the path gate — the LAST step's sprite finishes its
        // slide after the path is spent; the t=1 branch assigns the target EXACTLY,
        // so a settled hero sits on its lattice point to the bit.
        if (_glideT < 1f)
        {
            _glideT = Mathf.Min(_glideT + dt / StepInterval, 1f);
            if (_sprite is { } sp)
                sp.Position = _glideT < 1f ? _glideFrom.Lerp(_glideTo, _glideT) : _glideTo;
            // MC 12153: the walk cycle's second frame — the planted (idle) pose
            // lands with the second half of the glide, so ONE glide shows both
            // shipped poses: the leg's ≥2-distinct-frames read, deterministic at
            // any fps (frame index read from the glide progress, not a clock).
            if (_showWalk && _glideT >= 0.5f) SetWalkPose(false);
        }
        if (_attackFlashT > 0f)
        {
            // decay clamped at 50 ms/step: a slow Xvfb frame (measured max_dt
            // 0.15 s) must not eat the whole flash before the window paints it.
            _attackFlashT = Mathf.Max(_attackFlashT - Mathf.Min(dt, 0.05f), 0f);
            float k = 1f + 1.25f * (_attackFlashT / AttackFlashDur); // over-bright → plain white
            _sprite!.Modulate = _attackFlashT > 0f ? new Color(k, k, k) : Colors.White;
        }
        if (_attackPoseT > 0f)
        {
            // MC 12162: the attack-pose window, same slow-frame clamp as the flash
            // (≥4 pump frames per window on measured Xvfb deltas). On the expire
            // edge the ONE applicator RESUMES the idle/walk selection — a timer
            // read at any point inside names the pose, deterministic at any fps.
            _attackPoseT = Mathf.Max(_attackPoseT - Mathf.Min(dt, 0.05f), 0f);
            if (_attackPoseT == 0f) ApplyHeroPose();
        }
        if (_path.Count == 0) return;
        _stepTimer += dt;
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
        ApplyHeroPose(); // MC 12162: the facing change rides the ONE pose applicator
    }

    private static Texture2D HeroFrame(string dir) =>
        AreaRenderer.ArtOrNull($"res://assets/hero/hero-idle-{dir}.png")
        ?? AreaRenderer.Diamond(new Color("D8CBA6"), 128, 128);

    /// <summary>MC 12153: the shipped WALK pose for one direction (hero-walk-<dir>
    /// .png, 128×128 — real frames for ALL FOUR facings SetFacing produces, the
    /// measured census: the mapping is the IDENTITY facing→sheet). Falls back to
    /// the idle pose through HeroFrame's own placeholder pass — never a fake.</summary>
    private static Texture2D HeroWalkFrame(string dir) =>
        AreaRenderer.ArtOrNull($"res://assets/hero/hero-walk-{dir}.png") ?? HeroFrame(dir);

    /// <summary>MC 12162: the shipped ATTACK pose for one direction (hero-attack-<dir>
    /// .png, 128×128 — the 12154 art that closes the 12153 art order). Same identity
    /// facing→sheet mapping; falls back through the walk→idle chain's placeholder
    /// pass — never a fake.</summary>
    private static Texture2D HeroAttackFrame(string dir) =>
        AreaRenderer.ArtOrNull($"res://assets/hero/hero-attack-{dir}.png") ?? HeroWalkFrame(dir);

    /// <summary>MC 12162: the sprite's ONE texture applicator — every Texture write
    /// rides it. While the attack window plays the attack sheet stands; otherwise
    /// the 12153 walk/idle cycle picks by _showWalk + facing. A frame is PLAYED,
    /// never drawn — the three choices are three shipped sheets.</summary>
    private void ApplyHeroPose()
    {
        if (_sprite is null) return;
        _sprite.Texture = _attackPoseT > 0f ? HeroAttackFrame(_facing)
                      : _showWalk         ? HeroWalkFrame(_facing)
                                          : HeroFrame(_facing);
    }

    /// <summary>MC 12153: swaps the ONE sprite between the shipped walk and idle
    /// poses of the facing — the cycle's walk/idle state flag, and it only ever
    /// chooses between shipped sheets: a frame is PLAYED, never drawn. MC 12162:
    /// the write rides ApplyHeroPose, so during an attack window the underlying
    /// walk/idle state updates while the attack sheet KEEPS the screen.</summary>
    private void SetWalkPose(bool walk)
    {
        if (_sprite is null || _showWalk == walk) return;
        _showWalk = walk;
        ApplyHeroPose();
    }

    /// <summary>MC 12153 attack feedback, one burst per committed attack: the hit
    /// monster's red flash (MonsterField's own lever-checked decay) + this sprite's
    /// 130 ms white over-exposure, pumped by _Process. With AnimOffVar set NOTHING
    /// moves — the ANIM LEG's RED shape for the attack assertion. MC 12162: the
    /// committed attack ALSO opens the ~200 ms pose window (the 12154 sheet on the
    /// sprite, _Process closes it and resumes the idle/walk selection); the flash
    /// is KEPT on top of the pose — with NoAttackPoseVar set the sheet never plays
    /// and the flash alone answers (the ANIM LEG's attack-pose RED shape).</summary>
    private void FlashAttack(string key)
    {
        _monsters.FlashHit(key);
        if (_animOff || _sprite is null) return;
        _attackFlashT = AttackFlashDur;
        if (_noAttackPose) return;
        _attackPoseT = AttackPoseDur;
        ApplyHeroPose();
    }
}
