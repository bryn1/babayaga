using System;
using System.Collections.Generic;
using System.IO;
using Godot;

/// <summary>
/// MC 12221 MOB-SCALE LEG — the owner ruling MOBS HALVED: the measured comparison
/// vs D2 (DOC-d2-lod-compare §1–§2) proved the hero:mob px-height hierarchy was
/// INVERTED (bounds pair 128:160 = 0.80, visible 137:165 = 0.83; the D2 anchor is
/// 1.42 — the hero TOWERS over what he kills). Windowed ONLY (Xvfb 1280×720),
/// BABA_MOBSCALE_LEG names the evidence dir — unset, Boot never builds this node
/// (the AnimLeg gate shape). METHOD: the REAL Act settles at the town start
/// (seed 42), ONE synthesized LMB door-click walks out to mchy (AnimLeg's door
/// idiom — every state move a click, NO sim verb here), and the leg gates:
/// (a) RATIO — hero sprite px-height ÷ mob sprite px-height from GLOBAL BOUNDS
///     must land inside [1.25, 1.6] (D2 anchor 1.42; bounds ratio is the
///     deterministic reading, the visible-body 137:165 pair is the DOC's — the
///     scale 0.58 lands that one at ≈1.43, stated in EVIDENCE);
/// (b) ONE SCALE — every standing sprite in the Monsters row wears
///     Vector2.One * MonsterField.MobScale (all spawns, one place, read back);
/// (c) FEET-ON-TILE (the MC 12140 anchor law) — per sprite: the node sits EXACTLY
///     on its cell's GridToScreen point AND the scaled global-BOUNDS bottom edge
///     (rect.End.Y) sits ON that same point — the feet, not the centre;
/// (d) CLICK-WHAT-YOU-SEE — a synthesized LMB on the SCALED TORSO (the body
///     centre MonsterField.BodyCentreWorld names, the hit circle's own formula)
///     rides the MC 12136 pipeline and KILLS (Kills&gt;0 + the real node freed);
/// (e) the hit-flash sprite and the death-ghost BOTH WEAR the mob scale (node
///     reads back, verified — not assumed).
/// SHOT: 01-hierarchy.png — the hero beside a mob, the ratio visible to the eye
/// (AnimLeg's freeze-shot shape: the world PAUSED, the present queue drained).
/// RED proof: BABA_MOBSCALE_1X (MonsterField.MobScale1XVar) renders mobs at 1.0 —
/// the ratio gate alone goes red (128:160 = 0.80). Markers "MOBSCALE LEG OK …" /
/// "MOBSCALE LEG FAIL …", pinned.
/// </summary>
public partial class MobScaleLeg : Node
{
    public const string EnvVar = "BABA_MOBSCALE_LEG";
    public const ulong LegSeed = 42; // the anchor seed — town start, the owner's first frame

    public string OutDir = "";
    public Act Act = null!;

    private enum Step { Settle, DoorClick, WalkOut, MchySettle, Measure, AttackClick, AttackWatch, Finish }

    private Step _step = Step.Settle;
    private int _frames, _postKillFrames, _scaleMismatch, _feetCells;
    private double _deadline, _maxDelta, _nextClick, _nextDbg;
    private float _heroPx, _mobPx, _ratio, _feetErr;
    private bool _sawFlash, _ghostRead;
    private Vector2 _flashScale = nan(), _ghostScale = nan();
    private Vector2I _door;

    // The freeze-shot carry-over (AnimLeg's present-lag law, the pause version).
    private double _shotUntil;
    private string _shotName = "";
    private Action _shotDone = () => { };

    private static Vector2 nan() => new(float.NaN, float.NaN);

    /// <summary>Plain px-scale equality at a 1e-3 tolerance — the reads that gate
    /// (b) and the flash/ghost scale inherit (floats written, never derived, but
    /// the law states a tolerance, the law is honoured).</summary>
    private static bool ScaleEq(Vector2 a, Vector2 want) =>
        MathF.Abs(a.X - want.X) < 1e-3f && MathF.Abs(a.Y - want.Y) < 1e-3f;

    public override void _Ready()
    {
        GetWindow().Position = Vector2I.Zero;
        GetWindow().Size = new Vector2I(1280, 720); // the judged leg size (the S6 shape)
        ProcessMode = ProcessModeEnum.Always;       // the freeze-shot needs the leg live while PAUSED
        Enter(Step.Settle, 10.0);
    }

    public override void _Process(double delta)
    {
        if (OutDir.Length == 0) { Fail("Start", "OutDir unset"); return; }
        _maxDelta = Math.Max(_maxDelta, (float)delta);
        if (_shotUntil > 0) // the frozen frame drains the present queue, then the shot lands
        {
            if (Time.GetTicksMsec() / 1000.0 < _shotUntil) return;
            _shotUntil = 0;
            Shot(_shotName);
            GetTree().Paused = false;
            _shotDone();
            return;
        }
        if (Time.GetTicksMsec() / 1000.0 > _deadline) { Fail(_step.ToString(), "timed out"); return; }
        switch (_step)
        {
            case Step.Settle:
                if (++_frames < 30) return; // settled presents only (the GroundLeg shape)
                GD.Print($"BABA_MOBSCALE_START cell={Act.Hero.Cell} mob_scale={MonsterField.MobScale} " +
                         $"scale_1x={System.Environment.GetEnvironmentVariable(MonsterField.MobScale1XVar) is { Length: > 0 }}");
                var door = DoorCell();
                if (door == Act.Hero.Cell) { Fail("Settle", "no east-edge door cell to walk out through"); return; }
                _door = door;
                Click(Act.Hero.CellToScreen(door));
                Enter(Step.DoorClick, 5.0);
                break;
            case Step.DoorClick:
                Enter(Step.WalkOut, 60.0); // the queued click's walk gets its real span (AnimLeg's 60 s)
                break;
            case Step.WalkOut:
                if (GameContext.Session is not { } s || s.IsDead) { Fail("WalkOut", "the session died on the road"); return; }
                if (s.CurrentArea != "mchy")
                {
                    double now = Time.GetTicksMsec() / 1000.0; // one honest stall readout every 5 s
                    if (now >= _nextDbg) { _nextDbg = now + 5.0;
                        GD.Print($"BABA_MOBSCALE DEBUG area={s.CurrentArea} cell={Act.Hero.Cell} door={_door}"); }
                    return;
                }
                Enter(Step.MchySettle, 10.0);
                break;
            case Step.MchySettle:
                if (++_frames < 20) return; // the fresh area-build's presents drain before the reads
                _frames = 0;
                Enter(Step.Measure, 15.0);
                break;
            case Step.Measure:
                if (!Measure(out string why)) { Fail("Measure", why); return; }
                GD.Print($"MOBSCALE MEASURE hero_px={_heroPx:F1} mob_px={_mobPx:F1} ratio={_ratio:F3} " +
                         $"window=[1.25,1.6] scale={MonsterField.MobScale} scale_mismatch={_scaleMismatch} " +
                         $"feet_cells={_feetCells} feet_max_err={_feetErr:F4}");
                FreezeShot("01-hierarchy.png", () => Enter(Step.AttackClick, 10.0));
                break;
            case Step.AttackClick:
                if (FirstMonsterCell() is not { } mon) { Fail("AttackClick", "no monster sprite stands in mchy to strike"); return; }
                var px = Act.Hero.GetCanvasTransform() * MonsterField.BodyCentreWorld(mon.Cell); // the DRAWN torso, hit circle's own formula
                Click(px); // LMB on the body — the MC 12136 attack door
                GD.Print($"MOBSCALE ATTACK_CLICK key={mon.Key} cell={mon.Cell} torso_px=({px.X:F0},{px.Y:F0})");
                Enter(Step.AttackWatch, 30.0);
                break;
            case Step.AttackWatch:
                if (!_sawFlash && Act.Monsters.AnyHitFlash)
                {
                    _sawFlash = true;
                    _flashScale = FlashScaleOrNaN(); // WHICH node wears the flash, at WHICH scale
                }
                if (Act.Monsters.Kills == 0)
                {
                    if (GameContext.Session is { IsDead: true }) { Fail("AttackWatch", "the hero died trading blows before the first kill"); return; }
                    double now = Time.GetTicksMsec() / 1000.0; // S4's human-tempo grind, always on the TORSO
                    if (now >= _nextClick) { _nextClick = now + 0.15; if (FirstMonsterCell() is { } m)
                        Click(Act.Hero.GetCanvasTransform() * MonsterField.BodyCentreWorld(m.Cell)); }
                    return;
                }
                if (!_ghostRead)
                {
                    _ghostRead = true;
                    _ghostScale = GhostScaleOrNaN(); // the ghost was BORN this tick — read it before its fade
                    GD.Print($"MOBSCALE ATTACK flash_scale={_flashScale} ghost_scale={_ghostScale} " +
                             $"want={Vector2.One * MonsterField.MobScale}");
                }
                if (!Act.Monsters.LastKilledFreed) return; // the real node frees on the sim's tick (kills-stay-dead)
                if (++_postKillFrames < 4) return;         // the [post-kill] count marker rides one frame later
                var want = Vector2.One * MonsterField.MobScale;
                if (float.IsNaN(_flashScale.X)) { Fail("AttackWatch", "the red hit-flash node was never caught to read its scale"); return; }
                if (!ScaleEq(_flashScale, want)) { Fail("AttackWatch", $"the hit-flash node wears {_flashScale}, not the mob scale {want}"); return; }
                if (float.IsNaN(_ghostScale.X)) { Fail("AttackWatch", "the death ghost never stood in Ghosts to read its scale"); return; }
                if (!ScaleEq(_ghostScale, want)) { Fail("AttackWatch", $"the death ghost wears {_ghostScale}, not the mob scale {want}"); return; }
                GD.Print($"MOBSCALE LEG OK ratio={_ratio:F3} click=kill " +
                         $"feet_on_tile=yes feet_max_err={_feetErr:F4} flash_scale_inherited=yes ghost_scale_inherited=yes " +
                         $"shots=01-hierarchy.png max_dt={_maxDelta:F4}s");
                Enter(Step.Finish, 0.3);
                break;
            case Step.Finish:
                GetTree().Quit(0);
                break;
        }
    }

    /// <summary>Gates (a) RATIO + (b) ONE SCALE + (c) FEET-ON-TILE from the live
    /// nodes. Readings chosen (stated): GLOBAL BOUNDS heights — node-space,
    /// zoom-invariant, deterministic; the visible-body pair is the DOC's own
    /// measurement, quoted in EVIDENCE beside these numbers. The feet law reads
    /// BOTH the node anchor (exactly on the cell's GridToScreen point) and the
    /// scaled bounds BOTTOM edge (exactly on it too) — the FEET, not the centre,
    /// stand on the tile diamond (MC 12140).</summary>
    private bool Measure(out string why)
    {
        Sprite2D? heroSpr = null;
        foreach (var c in Act.Hero.GetChildren())
            if (c is Sprite2D hs) { heroSpr = hs; break; }
        if (heroSpr is null) { why = "hero sprite node not found under HeroController"; return false; }
        var kids = Act.Monsters.StandingSprites(); // roster-backed, never a name hunt
        if (kids.Count == 0) { why = "no monster sprite stands in mchy to measure"; return false; }

        // (a) RATIO — hero:mob global-BOUNDS px heights (the 4.7.2 C# surface
        // has no GetGlobalRect, so the bound is the documented composition:
        // GetGlobalTransform() ∘ GetRect() — the native get_global_rect itself;
        // node space, zoom-invariant, offset + scale ride it).
        _heroPx = WorldRect(heroSpr).Size.Y;
        _mobPx = WorldRect(kids[0]).Size.Y;
        _ratio = _mobPx > 0f ? _heroPx / _mobPx : 0f;
        if (!(_ratio >= 1.25f && _ratio <= 1.6f))
        { why = $"hero:mob bounds ratio {_ratio:F3} OUTSIDE [1.25,1.6] — mobs render at scale {MonsterField.MobScale} (BABA_MOBSCALE_1X set?)"; return false; }

        // (b) ONE SCALE — every standing sprite wears the constant (all spawns).
        var want = Vector2.One * MonsterField.MobScale;
        foreach (var sp in kids)
            if (!ScaleEq(sp.Scale, want)) _scaleMismatch++;
        if (_scaleMismatch > 0) { why = $"{_scaleMismatch}/{kids.Count} standing mob sprites do NOT wear the ONE scale"; return false; }

        // (c) FEET-ON-TILE, per sprite, roster order == children order (the Build
        // add order — no kills have happened yet in this run).
        var snap = Act.Monsters.RowSnapshot();
        if (snap.Count != kids.Count) { why = $"roster row {snap.Count} vs live sprites {kids.Count} before any kill"; return false; }
        for (int i = 0; i < kids.Count; i++)
        {
            var world = IsoCamera.GridToScreen(snap[i].Cell);
            var rect = WorldRect(kids[i]);
            float err = Math.Max(Math.Abs(kids[i].GetGlobalPosition().X - world.X),   // node sits EXACTLY
                                 Math.Abs(kids[i].GetGlobalPosition().Y - world.Y));  // on the lattice point
            err = Math.Max(err, Math.Abs(rect.End.Y - world.Y));                      // the scaled body's BOTTOM
            err = Math.Max(err, Math.Abs((rect.Position.X + rect.End.X) * 0.5f - world.X)); // edge ON the centre
            _feetCells++;
            _feetErr = Math.Max(_feetErr, err);
        }
        if (_feetErr > 0.01f) { why = $"scaled feet drift {_feetErr:F4} px off the tile centre — the MC 12140 anchor law is broken"; return false; }
        why = "";
        return true;
    }

    /// <summary>The node's global bounds as Godot's native get_global_rect composes
    /// them (GetGlobalTransform ∘ GetRect — 4.7.2's C# surface exposes the parts;
    /// Sprite2D.GetRect already folds Centered + Offset + Scale into the local
    /// rect). Node space — no camera, no zoom.</summary>
    private static Rect2 WorldRect(Sprite2D sp)
    {
        var xf = sp.GetGlobalTransform();
        var r = sp.GetRect();
        var p = xf * r.Position;
        var e = xf * r.End;
        return new Rect2(p, e - p);
    }

    /// <summary>The scale of the STANDING sprite NOT wearing plain white — the
    /// red-flash node the field is decaying (verified, not assumed: same node ⇒
    /// same scale). NaN when the read answers nothing.</summary>
    private Vector2 FlashScaleOrNaN()
    {
        foreach (var sp in Act.Monsters.StandingSprites())
            if (sp.Modulate != Colors.White) return sp.Scale;
        return nan();
    }

    /// <summary>The scale of the first live death ghost (MC 12153's fading tail,
    /// MC 12221's scale-copy law). NaN when none stands.</summary>
    private Vector2 GhostScaleOrNaN()
    {
        var gs = Act.Monsters.GhostSprites();
        return gs.Count > 0 ? gs[0].Scale : nan();
    }

    /// <summary>The EAST-edge door cell nearest the hero (AnimLeg's walk-out pick —
    /// the link-0 grodzisko→mchy door).</summary>
    private Vector2I DoorCell()
    {
        var from = Act.Hero.Cell;
        Vector2I best = from;
        int bestD = int.MaxValue;
        foreach (var c in Act.Tiles.Walkable)
        {
            if (c.X != AreaRenderer.W - 1) continue;
            int d = Math.Max(Math.Abs(c.X - from.X), Math.Abs(c.Y - from.Y));
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    /// <summary>A standing monster — pokrzywnik first (AnimLeg's pick). The
    /// presentation roster read, never Sim state.</summary>
    private (string Key, Vector2I Cell)? FirstMonsterCell()
    {
        (string Key, Vector2I Cell)? any = null;
        foreach (var entry in Act.Monsters.RowSnapshot())
        {
            any ??= entry;
            if (entry.Key == "pokrzywnik") return entry;
        }
        return any;
    }

    /// <summary>One synthesized LMB click — press then release through the window's
    /// real input queue (the SmoothLeg/AnimLeg Click idiom verbatim).</summary>
    private void Click(Vector2 windowPx)
    {
        var win = GetWindow();
        win.PushInput(new InputEventMouseMotion { GlobalPosition = windowPx, Position = windowPx });
        foreach (bool pressed in new[] { true, false })
            win.PushInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                GlobalPosition = windowPx,
                Position = windowPx,
                Pressed = pressed,
            });
    }

    /// <summary>Freeze the world, drain the present queue, take the shot, then run
    /// the continuation (the AnimLeg shape — a frozen frame is an honest read-back).</summary>
    private void FreezeShot(string name, Action onDone)
    {
        GetTree().Paused = true;
        _shotName = name;
        _shotDone = onDone;
        _shotUntil = Time.GetTicksMsec() / 1000.0 + 1.5;
    }

    private void Shot(string name)
    {
        var img = GetViewport().GetTexture().GetImage();
        var path = Path.Combine(OutDir, name);
        img.SavePng(path);
        GD.Print($"MOBSCALE SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
    }

    private void Enter(Step step, double seconds)
    {
        _step = step;
        _deadline = Time.GetTicksMsec() / 1000.0 + seconds;
    }

    private void Fail(string where, string why)
    {
        GD.Print($"MOBSCALE LEG FAIL {where}: {why}");
        GetTree().Quit(1);
    }
}
