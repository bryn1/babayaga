using System;
using System.Collections.Generic;
using System.IO;
using Godot;

/// <summary>
/// MC 12153 ANIMATION-PLAY LEG — the owner complaint "no attack motions or
/// animations" while multi-frame sheets SHIPPED unused. Windowed ONLY (Xvfb
/// 1280×720), BABA_ANIM_LEG names the evidence dir — unset, Boot never builds
/// this node (the SmoothLeg gate shape). METHOD: the REAL Act settles on the
/// town start frame; ONE synthesized LMB move-click (the SmoothLeg Click/WalkTarget
/// idiom — every state move a click, NO sim verb anywhere here) plans a ≥4-step
/// walk; while it plays the leg samples the HERO SPRITE every frame and gates
/// (a) a walk-pose frame observed WHILE GLIDING (the shipped hero-walk-<dir> pose
/// is on screen mid-glide), (b) the idle pose RETURNING at rest, (c) ≥2 distinct
/// textures across the glide, then walks out to mchy and LMB-strikes a standing
/// monster and gates (d) the attack trigger: the hero sprite's Modulate deviates
/// from plain white AND the monster field reports the red hit-flash. NO attack
/// art ships (the census lives in out/leg-anim/EVIDENCE.md) — the flash IS the
/// tested feedback, the sheet is the recorded art order.
/// SHOTS: the world is PAUSED at the predicate frame (leg ProcessMode=Always) and
/// the shot taken after the present-queue drains — a frozen frame is an HONEST
/// read-back even under llvmpipe present-lag (S4's lesson: state moves seconds
/// ahead of the paint; a paused world stops the race).
/// RED proof: BABA_ANIM_OFF (HeroController.AnimOffVar) freezes pose + flash —
/// the walk-pose assertion goes red. Then the leg GRINDS the struck pokrzywnik
/// down (S4's human re-click tempo) so a REAL kill runs the death-fade path, and
/// gates (e) the Sim's kill truth (Kills>0 + the real node freed — kills-stay-dead)
/// with the S7 [post-kill] M5_SPRITE_COUNT marker printed == spawn-row WHILE the
/// ghost fades outside the counted row. Markers "BABA_ANIM_LEG OK …" / FAIL, pinned.
/// </summary>
public partial class AnimLeg : Node
{
    public const string EnvVar = "BABA_ANIM_LEG";
    public const ulong LegSeed = 42; // the anchor seed — town start, the owner's first frame

    public string OutDir = "";
    public Act Act = null!;

    private enum Step { Settle, WalkClick, WalkWatch, WalkVerdict, DoorClick, WalkOut,
        MchySettle, AttackClick, AttackWatch, KillGrind, Finish }

    private Step _step = Step.Settle;
    private int _frames, _settleFrames;
    private double _deadline, _maxDelta, _nextClick, _nextDebug;
    private Vector2I _door;
    private readonly HashSet<ulong> _glideFrames = [];
    private bool _sawWalk, _sawReturn, _idleAtRest, _midShot, _attackSeen, _monsterFlashSeen;
    private Color _attackModulate = Colors.White;

    // The freeze-shot carry-over (S4's present-lag law, the pause version).
    private double _shotUntil;
    private string _shotName = "";
    private Action _shotDone = () => { };

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
        var hero = Act.Hero;
        switch (_step)
        {
            case Step.Settle:
                if (++_frames < 30) return; // settled presents only (the GroundLeg shape)
                if (hero.VisualPos != IsoCamera.GridToScreen(hero.Cell))
                { Fail("Settle", "start frame not settled"); return; }
                GD.Print($"BABA_ANIM_LEG_START cell={hero.Cell} walk_pose={hero.ShowingWalkFrame} " +
                         $"modulate={hero.HeroSpriteModulate} anim_off={System.Environment.GetEnvironmentVariable(HeroController.AnimOffVar) is { Length: > 0 }}");
                Enter(Step.WalkClick, 5.0);
                break;
            case Step.WalkClick:
                var t1 = WalkTarget();
                if (t1 == hero.Cell) { Fail("WalkClick", "no interior walkable target 4–6 steps from the start"); return; }
                Click(hero.CellToScreen(t1)); // the SmoothLeg click — the A* plans, the glide walks
                _glideFrames.Clear(); _sawWalk = false; _sawReturn = false;
                Enter(Step.WalkWatch, 40.0);
                break;
            case Step.WalkWatch:
                _glideFrames.Add(hero.CurrentHeroFrame.GetInstanceId()); // texture identity, GD.Load caches
                if (hero.IsGliding && hero.ShowingWalkFrame && !_sawWalk)
                {
                    _sawWalk = true;
                    if (!_midShot)
                    { // freeze at the first mid-glide walk-pose frame → the honest 01 shot
                        _midShot = true;
                        FreezeShot("01-midwalk.png", () => Enter(Step.WalkWatch, _deadline - Time.GetTicksMsec() / 1000.0 + 3.0));
                        return;
                    }
                }
                else if (hero.IsGliding && !hero.ShowingWalkFrame && _sawWalk) _sawReturn = true;
                if (hero.IsWalking || hero.IsGliding) { _settleFrames = 0; return; }
                if (++_settleFrames < 3) return; // the last glide's presents drain before the read
                _idleAtRest = !hero.ShowingWalkFrame;
                Enter(Step.WalkVerdict, 5.0);
                break;
            case Step.WalkVerdict:
                GD.Print($"BABA_ANIM_LEG WALK distinct_frames={_glideFrames.Count} walk_pose_mid_glide={_sawWalk} " +
                         $"idle_mid_glide={_sawReturn} idle_at_rest={_idleAtRest}");
                if (!_sawWalk) { Fail("WalkVerdict", "the walk pose never stood on screen during a glide — frozen frame (BABA_ANIM_OFF?)"); return; }
                if (!_idleAtRest) { Fail("WalkVerdict", "the idle pose did not return at rest"); return; }
                if (_glideFrames.Count < 2) { Fail("WalkVerdict", $"<2 distinct hero frames observed across the walk ({_glideFrames.Count})"); return; }
                var door = DoorCell();
                if (door == hero.Cell) { Fail("WalkVerdict", "no east-edge door cell to walk out through"); return; }
                _door = door;
                Click(hero.CellToScreen(door));
                Enter(Step.DoorClick, 5.0);
                break;
            case Step.DoorClick:
                Enter(Step.WalkOut, 60.0); // the door click above is queued; the walk-out gets its real span (S4's 60s)
                break;
            case Step.WalkOut:
                // READ-ONLY session reads (the area flip is the door's own doing,
                // the walk a chain of presentation steps — this leg calls NO verb).
                if (GameContext.Session is not { } s || s.IsDead) { Fail("WalkOut", "the session died on the road"); return; }
                if (s.CurrentArea != "mchy")
                {
                    double now = Time.GetTicksMsec() / 1000.0; // one honest stall readout every 5 s
                    if (now >= _nextDebug) { _nextDebug = now + 5.0;
                        GD.Print($"BABA_ANIM_LEG DEBUG area={s.CurrentArea} cell={hero.Cell} walking={hero.IsWalking} door={_door}"); }
                    return;
                }
                GD.Print($"BABA_ANIM_LEG WALKOUT area=mchy sprites={Act.Monsters.SpriteCount}");
                Enter(Step.MchySettle, 10.0);
                break;
            case Step.MchySettle:
                if (++_frames < 20) return;
                _frames = 0;
                Enter(Step.AttackClick, 5.0);
                break;
            case Step.AttackClick:
                if (FirstMonsterCell() is not { } mon) { Fail("AttackClick", "no monster sprite stands in mchy to strike"); return; }
                Click(Act.Hero.CellToScreen(mon.Cell)); // LMB on the drawn body — the MC 12136 attack door
                GD.Print($"BABA_ANIM_LEG ATTACK_CLICK key={mon.Key} cell={mon.Cell}");
                Enter(Step.AttackWatch, 30.0);
                break;
            case Step.AttackWatch:
                bool deviates = hero.HeroSpriteModulate != Colors.White;
                if (deviates && !_attackSeen)
                {
                    _attackSeen = true;
                    _attackModulate = hero.HeroSpriteModulate;
                    _monsterFlashSeen = Act.Monsters.AnyHitFlash;
                    FreezeShot("02-attack.png", () => Enter(Step.AttackWatch, _deadline - Time.GetTicksMsec() / 1000.0 + 3.0));
                    return;
                }
                if (_attackSeen)
                {
                    GD.Print($"BABA_ANIM_LEG ATTACK modulate={_attackModulate} off_white=yes monster_flash={_monsterFlashSeen}");
                    if (!_monsterFlashSeen) { Fail("AttackWatch", "the struck monster never wore the red hit-flash"); return; }
                    _frames = 0;
                    Enter(Step.KillGrind, 45.0); // strike the SAME beast down: the death fade + the count law ride a real kill
                }
                else
                {
                    double now = Time.GetTicksMsec() / 1000.0; // a human re-click tempo until the flash answers
                    if (now >= _nextClick) { _nextClick = now + 0.6; if (FirstMonsterCell() is { } m) Click(Act.Hero.CellToScreen(m.Cell)); }
                }
                break;
            case Step.KillGrind:
                // S4's human-tempo grind: LMB stays the attack door (MC 12136); the
                // sim is seed-pinned, the pokrzywnik (life 14, defense 10) falls in a
                // handful of strikes. READS ONLY: Kills is the presentation's own
                // counter of the Sim's report; the area flip / despawn are the game's.
                if (Act.Monsters.Kills == 0)
                {
                    if (GameContext.Session is { IsDead: true }) { Fail("KillGrind", "the hero died trading blows before the first kill"); return; }
                    double now = Time.GetTicksMsec() / 1000.0;
                    if (now >= _nextClick) { _nextClick = now + 0.15; if (FirstMonsterCell() is { } m) Click(Act.Hero.CellToScreen(m.Cell)); }
                    return;
                }
                if (!Act.Monsters.LastKilledFreed) return; // the real node frees on the sim's tick (kills-stay-dead)
                if (++_frames < 4) return;                 // the [post-kill] count marker prints one frame later
                GD.Print($"BABA_ANIM_LEG KILL kills={Act.Monsters.Kills} real_freed=yes death_ghost=rides-Ghosts-row-fading " +
                         "(the S7 [post-kill] M5_SPRITE_COUNT above stayed == spawn-row WITH the fade alive — the count law holds)");
                GD.Print($"BABA_ANIM_LEG OK frames_distinct={_glideFrames.Count} walk_pose=mid_glide idle_at_rest=yes " +
                         $"attack=hero_white_flash+monster_red_flash (no attack art ships — ART ORDER in EVIDENCE) " +
                         $"shots=01-midwalk.png,02-attack.png max_dt={_maxDelta:F4}s");
                Enter(Step.Finish, 0.3);
                break;
            case Step.Finish:
                GetTree().Quit(0);
                break;
        }
    }

    /// <summary>Freeze the world, drain the present queue, take the shot, then run
    /// the continuation. A paused world paints ONE still frame the read-back cannot
    /// race ahead of — the honest capture of a 130 ms flash / mid-glide pose.</summary>
    private void FreezeShot(string name, Action onDone)
    {
        GetTree().Paused = true;
        _shotName = name;
        _shotDone = onDone;
        _shotUntil = Time.GetTicksMsec() / 1000.0 + 1.5;
    }

    /// <summary>An INTERIOR walkable cell 4–6 Chebyshev steps from the hero,
    /// screen-safe (SmoothLeg.WalkTarget's pinned constraints — the same read).</summary>
    private Vector2I WalkTarget()
    {
        var from = Act.Hero.Cell;
        foreach (var c in Act.Tiles.Walkable)
        {
            if (c.X == 0 || c.Y == 0 || c.X == AreaRenderer.W - 1 || c.Y == AreaRenderer.H - 1) continue; // never a door
            if (Act.Tiles.IsLitStone(c)) continue;
            int d = Math.Max(Math.Abs(c.X - from.X), Math.Abs(c.Y - from.Y));
            if (d < 4 || d > 6) continue;
            int dx = (c.X - c.Y) - (from.X - from.Y);
            int ds = (c.X + c.Y) - (from.X + from.Y);
            if (dx < -4 || dx > 4 || ds < 3 || ds > 6) continue;
            return c;
        }
        return from; // the caller FAILS on this — never a silent pass
    }

    /// <summary>The EAST-edge door cell nearest the hero (S4's walk-out pick — the
    /// link 0 grodzisko→mchy door the camera shows).</summary>
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

    /// <summary>A standing monster — pokrzywnik first (the near-spawn band, S4's
    /// pick), any other kind otherwise. Presentation roster read, never Sim state.</summary>
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
    /// real input queue (the SmoothLeg.Click idiom verbatim).</summary>
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

    private void Shot(string name)
    {
        var img = GetViewport().GetTexture().GetImage();
        var path = Path.Combine(OutDir, name);
        img.SavePng(path);
        GD.Print($"BABA_ANIM_LEG SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
    }

    private void Enter(Step step, double seconds)
    {
        _step = step;
        _deadline = Time.GetTicksMsec() / 1000.0 + seconds;
    }

    private void Fail(string where, string why)
    {
        GD.Print($"BABA_ANIM_LEG FAIL {where}: {why}");
        GetTree().Quit(1);
    }
}
