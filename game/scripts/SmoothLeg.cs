using System;
using System.IO;
using Godot;

/// <summary>
/// MC 12149 MOVEMENT-SMOOTHING LEG — the owner complaint "the movement is very
/// unsmooth" was the hero node TELEPORTING per committed sim step. Windowed ONLY
/// (Xvfb 1280×720), BABA_SMOOTH_LEG names the evidence dir — unset, Boot never
/// builds this node (the CamLeg gate shape). METHOD: the REAL Act settles on the
/// town start frame; ONE synthesized LMB move-click (the CamLeg Click idiom —
/// the same input pipeline a human click rides) plans a ≥4-step A* walk; the leg
/// then gates its exit on, per the pinned markers: (a) MID-STEP the hero node's
/// pos is STRICTLY between the previous and the committed cell's lattice points
/// (t ∈ (ε,1−ε) on the segment, >0.5 px off each end), (b) at STEP END the pos
/// is EXACTLY the target lattice point, (c) a second LMB synthesized DURING a
/// glide still targets its CLICKED cell (the committed truth is what clicks
/// hit — the visual glide changes no targeting), plus the camera glides too
/// (its pos strictly between the two clamped cell points mid-step — CamLeg's
/// ε stays its own). RED proof: BABA_SNAP_MODE (HeroController.SnapVar, the
/// zero-length glide) makes the mid-glide assertion fail. Since MC 12256 the leg
/// ALSO carries the CADENCE marker: a staged town cross (far-interior corner to
/// far-interior corner, one LMB) prints <c>cross_seconds=steps×StepSeconds</c> and
/// asserts the D2 pacing window [8,14] s — RED lever BABA_FAST_WALK (HeroController.
/// FastWalkVar) keeps the old 0.2 s step, dropping cross_seconds to ~1/3 and the
/// leg with it. NO sim verb is reachable from any line here (the driver reads state;
/// every state move is one click). The ≥2-frame sample rule exists because
/// Act._Process pulls the camera BEFORE HeroController advances the glide — on the
/// commit frame the camera legitimately still sits on the previous point.
/// </summary>
public partial class SmoothLeg : Node
{
    public const string EnvVar = "BABA_SMOOTH_LEG";
    public const ulong LegSeed = 42; // the anchor seed — town start, the owner's first frame

    public string OutDir = "";
    public Act Act = null!;

    private enum Step { Settle, WaitStep1, MidGlide, WaitSettle, Arrive, CrossStage, CrossWalk, Finish }

    private Step _step = Step.Settle;
    private int _frames, _midFrames, _settleFrames;
    private double _deadline, _maxDelta, _crossT0, _clickT0Ms;
    private Vector2I _prev, _cur, _retarget, _c0, _c1, _crossPrev;
    private int _crossSteps, _crossHalf;
    private bool _crossShot;
    private float _tSample = -1f;
    private bool _shot;

    private const float MidEps = 0.001f; // the lerp epsilon of the strict-between window

    public override void _Ready()
    {
        GetWindow().Position = Vector2I.Zero;
        GetWindow().Size = new Vector2I(1280, 720); // the judged leg size (the S6 shape)
        Enter(Step.Settle, 8.0); // first presents land before any read (the S4 present-lag law)
    }

    public override void _Process(double delta)
    {
        if (OutDir.Length == 0) { Fail("Start", "OutDir unset"); return; }
        _maxDelta = Math.Max(_maxDelta, (float)delta); // honesty row: the leg's own worst frame
        if (Time.GetTicksMsec() / 1000.0 > _deadline) { Fail(_step.ToString(), "timed out"); return; }
        var hero = Act.Hero;
        switch (_step)
        {
            case Step.Settle:
                if (++_frames < 30) return; // settled presents only (the GroundLeg shape)
                if (hero.VisualPos != IsoCamera.GridToScreen(hero.Cell))
                { Fail("Settle", $"start frame not settled: visual={hero.VisualPos} cell={hero.Cell}"); return; }
                var t1 = WalkTarget();
                if (t1 == hero.Cell) { Fail("Settle", "no interior walkable target 4–6 steps from the start"); return; }
                GD.Print($"BABA_SMOOTH_LEG_START cell={hero.Cell} visual={hero.VisualPos} max_dt={_maxDelta:F4}s snap={System.Environment.GetEnvironmentVariable(HeroController.SnapVar) is { Length: > 0 }}");
                Click(hero.CellToScreen(t1)); // ONE LMB — the A* plans, the step timer walks
                _prev = _cur = hero.Cell;
                Enter(Step.WaitStep1, 20.0);
                break;
            case Step.WaitStep1:
                if (hero.Cell == _cur) return; // no committed step yet
                _prev = _cur; _cur = hero.Cell; _midFrames = 0;
                Enter(Step.MidGlide, 20.0);
                break;
            case Step.MidGlide:
                if (!hero.IsGliding) // snap mode's exact signature: the step committed and the sprite TELEPORTED
                { Fail("MidGlide", "no glide in flight after a committed step — snap mode?"); return; }
                if (hero.Cell != _cur) { _prev = _cur; _cur = hero.Cell; _midFrames = 0; return; } // next step committed
                if (++_midFrames < 2) return; // camera reads lag the glide one pull (header)
                var from = IsoCamera.GridToScreen(_prev);
                var to = IsoCamera.GridToScreen(_cur);
                float t = Proj(hero.VisualPos, from, to);
                if (t >= 1f) { _midFrames = 0; return; } // glide finished between samples — wait for the next step
                if (t <= MidEps || t >= 1f - MidEps) return; // not STRICTLY between yet — sample next frame
                if (hero.VisualPos.DistanceTo(from) <= 0.5f || hero.VisualPos.DistanceTo(to) <= 0.5f) return; // not off the lattice points yet
                var cam = GetViewport().GetCamera2D();
                float dPrev = cam is null ? float.MaxValue : cam.Position.DistanceTo(IsoCamera.ClampToGrid(from, AreaRenderer.W, AreaRenderer.H));
                float dCur = cam is null ? float.MaxValue : cam.Position.DistanceTo(IsoCamera.ClampToGrid(to, AreaRenderer.W, AreaRenderer.H));
                if (dPrev <= 0.5f || dCur <= 0.5f)
                { Fail("MidGlide", $"camera did not glide (d_prev={dPrev:F2} d_cur={dCur:F2})"); return; }
                _tSample = t;
                GD.Print($"BABA_SMOOTH_LEG_MID prev={_prev} cur={_cur} t={t:F3} vis=({hero.VisualPos.X:F1},{hero.VisualPos.Y:F1}) " +
                         $"cam=({cam!.Position.X:F1},{cam.Position.Y:F1}) cam_between=yes d=({dPrev:F1},{dCur:F1})");
                // (c) THE MID-GLIDE CLICK: one LMB on a real cell WHILE the sprite
                // is mid-air between two diamonds — targeting reads the committed
                // cell, so the walk must RE-AIM to exactly this cell.
                _retarget = WalkTarget();
                if (_retarget == hero.Cell) { Fail("MidGlide", "no re-aim target 4–6 steps from the mid-step cell"); return; }
                Click(hero.CellToScreen(_retarget));
                GD.Print($"BABA_SMOOTH_LEG_CLICK cell={_retarget} at_t={t:F3} gliding=yes");
                Enter(Step.WaitSettle, 30.0);
                break;
            case Step.WaitSettle:
                if (hero.Cell != _cur) { _prev = _cur; _cur = hero.Cell; } // keep the step pair for the shot
                if (!_shot && hero.IsGliding) // ONE honest mid-glide frame: the hero painted BETWEEN two diamonds
                {
                    float tShot = Proj(hero.VisualPos, IsoCamera.GridToScreen(_prev), IsoCamera.GridToScreen(_cur));
                    if (tShot >= 0.35f && tShot <= 0.65f)
                    {
                        Shot("01-midglide.png");
                        GD.Print($"BABA_SMOOTH_LEG_SHOT between={_prev}->{_cur} t={tShot:F3}");
                        _shot = true;
                    }
                }
                if (hero.IsWalking || hero.IsGliding) { _settleFrames = 0; return; }
                if (++_settleFrames < 3) return; // the last glide's presents drain before the read
                Enter(Step.Arrive, 4.0);
                break;
            case Step.Arrive:
                // (b) STEP END: EXACTLY the target lattice point — the glide lands
                // on the committed truth to the bit (no float residue, no drift).
                var want = IsoCamera.GridToScreen(hero.Cell);
                float endDist = hero.VisualPos.DistanceTo(want);
                GD.Print($"BABA_SMOOTH_LEG_ARRIVE cell={hero.Cell} clicked={_retarget} visual=({hero.VisualPos.X:F1},{hero.VisualPos.Y:F1}) " +
                         $"want=({want.X:F1},{want.Y:F1}) end_dist={endDist:F6} mid_t={_tSample:F3} max_dt={_maxDelta:F4}s");
                if (hero.VisualPos != want) { Fail("Arrive", $"hero not EXACTLY on the target lattice point (end_dist={endDist:F6})"); return; }
                if (hero.Cell != _retarget) { Fail("Arrive", $"mid-glide click lost targeting: landed {hero.Cell}, clicked {_retarget}"); return; }
                if (!_shot) { Fail("Arrive", "no mid-glide screenshot was taken"); return; }
                // MC 12256 CADENCE — the marker measures a real cross-town walk. The
                // pair is the town's two FARTHEST INTERIOR cells (every edge cell is
                // a DOOR cell — committing one fires TravelIntent, so edge-to-edge is
                // honestly the interior max of the 20×20 town). Staging first makes
                // the measured leg start from a FIXED cell: the A* path, the step
                // count and cross_seconds are then run-invariant (Xvfb frame jitter
                // never moves a pinned number).
                _c0 = CrossStageCell();
                _c1 = CrossFarCell(_c0);
                if (_c1 == _c0) { Fail("Arrive", "no interior walkable pair to cross the town between"); return; }
                _crossHalf = Math.Max(Math.Abs(_c1.X - _c0.X), Math.Abs(_c1.Y - _c0.Y)) / 2;
                GD.Print($"BABA_SMOOTH_LEG_CROSS plan from={_c0} to={_c1} step_seconds={hero.StepSeconds:F2} fast_lever={System.Environment.GetEnvironmentVariable(HeroController.FastWalkVar) is { Length: > 0 }}");
                Click(hero.CellToScreen(_c0)); // stage: walk to the far-corner end (its steps are NOT counted)
                _clickT0Ms = Time.GetTicksMsec(); // MC 12256 RESPONSIVENESS stamp: the cross click (an
                Enter(Step.CrossStage, 60.0);     // LMB through the real queue) to its handler dispatch
                break;
            case Step.CrossStage:
                if (_clickT0Ms > 0 && hero.IsWalking) // ONE read: the synthesized-click → _UnhandledInput →
                {                                     // PlanPath dispatch span — the SAME handler chain
                    double lat = Time.GetTicksMsec() - _clickT0Ms; // an in-range attack rides (its
                    _clickT0Ms = 0;                                // s.Attack COMMITs in-call, never step-end)
                    GD.Print($"BABA_SMOOTH_LEG_CLICKLAT plan_dispatch_ms={lat:F0} (click→handler, attack rides this door — not the step timer)");
                }
                if (hero.Cell != _c0 || hero.IsWalking || hero.IsGliding) { _settleFrames = 0; return; }
                if (++_settleFrames < 3) return; // the staging walk's presents drain before the click
                _crossSteps = 0; _crossPrev = hero.Cell; _crossT0 = Time.GetTicksMsec();
                Click(hero.CellToScreen(_c1)); // THE measured edge->edge walk, one LMB like a human's
                Enter(Step.CrossWalk, 60.0);
                break;
            case Step.CrossWalk:
                if (hero.Cell != _crossPrev)
                {
                    _crossSteps++; _crossPrev = hero.Cell;
                    if (!_crossShot && _crossSteps >= _crossHalf && hero.IsGliding)
                    {
                        Shot("01-town.png"); // mid-cross: the hero painted on the walk across town
                        GD.Print($"BABA_SMOOTH_LEG_CROSSSHOT steps={_crossSteps} half={_crossHalf} — 01-town.png");
                        _crossShot = true;
                    }
                }
                if (hero.IsWalking || hero.IsGliding) { _settleFrames = 0; return; }
                if (++_settleFrames < 3) return; // the last glide's presents drain before the read
                if (hero.Cell != _c1) { Fail("CrossWalk", $"cross-town walk landed {hero.Cell}, clicked {_c1}"); return; }
                if (!_crossShot) { Shot("01-town.png"); _crossShot = true; } // honest fallback: the arrival frame
                // steps × interval -> seconds (the brief's instrument), read against the
                // LIVE cadence (StepSeconds) — under the RED lever this is steps × 0.2.
                float secs = _crossSteps * hero.StepSeconds;
                double wall = (Time.GetTicksMsec() - _crossT0) / 1000.0; // honesty row: the real-clock span
                GD.Print($"BABA_SMOOTH_LEG_CROSS steps={_crossSteps} cross_seconds={secs:F1} " +
                         $"({_crossSteps}x{hero.StepSeconds:F2}s) measured={wall:F1}s max_dt={_maxDelta:F4}s window=[8,14]");
                if (secs < 8f || secs > 14f)
                { Fail("CrossWalk", $"cross_seconds={secs:F1} s outside the D2 cadence window [8,14] (steps={_crossSteps} x {hero.StepSeconds:F2} s)"); return; }
                GD.Print($"BABA_SMOOTH_LEG OK mid_t={_tSample:F3} strict_between=yes exact=target click-mid-glide landed={_retarget} " +
                         $"camera=glides shot=01-midglide.png cross_seconds={secs:F1} edge_cross=yes cross_steps={_crossSteps}");
                Enter(Step.Finish, 0.3);
                break;
            case Step.Finish:
                GetTree().Quit(0);
                break;
        }
    }

    /// <summary>Parametric position of p on segment a→b: t = dot(p−a, b−a)/|b−a|².</summary>
    private static float Proj(Vector2 p, Vector2 a, Vector2 b)
    {
        var d = b - a;
        float len2 = d.LengthSquared();
        return len2 <= 0f ? 1f : (p - a).Dot(d) / len2;
    }

    /// <summary>An INTERIOR walkable cell 4–6 Chebyshev steps from the hero's
    /// committed cell, never the lit stone, screen-safe (CamLeg.WalkTarget's
    /// pinned constraints — the target read, not a second one).</summary>
    private Vector2I WalkTarget()
    {
        var from = Act.Hero.Cell;
        foreach (var v in Act.Tiles.Walkable)
        {
            var c = v;
            if (c.X == 0 || c.Y == 0 || c.X == AreaRenderer.W - 1 || c.Y == AreaRenderer.H - 1) continue; // never a door
            if (Act.Tiles.IsLitStone(c)) continue;
            int d = Math.Max(Math.Abs(c.X - from.X), Math.Abs(c.Y - from.Y));
            if (d < 4 || d > 6) continue;
            int dx = (c.X - c.Y) - (from.X - from.Y);
            int ds = (c.X + c.Y) - (from.X + from.Y);
            if (dx < -4 || dx > 4 || ds < 3 || ds > 6) continue;
            return c;
        }
        return from; // the callers FAIL on this — never a silent pass
    }

    /// <summary>MC 12256: one interior cell — no edge (edge cells are DOOR cells: a
    /// committed step onto one fires TravelIntent and leaves the town) and never the
    /// lit stone (its click RIDES, it does not walk).</summary>
    private bool IsInterior(Vector2I c) =>
        c.X != 0 && c.Y != 0 && c.X != AreaRenderer.W - 1 && c.Y != AreaRenderer.H - 1
        && !Act.Tiles.IsLitStone(c);

    /// <summary>MC 12256: the interior walkable cell hugging the (0,0) corner —
    /// deterministic off the walkable set (never off the hero's wanderings), so the
    /// measured cross starts from the SAME cell in every run. Ties follow the Build
    /// loop's row-major insertion order (stable).</summary>
    private Vector2I CrossStageCell()
    {
        var best = new Vector2I(int.MaxValue, int.MaxValue);
        int bestSum = int.MaxValue; // the sum rides its OWN sentinel — best.X+best.Y on the
        foreach (var c in Act.Tiles.Walkable) // uninit pair overflows to −2 (caught by this leg's first run)
            if (IsInterior(c) && c.X + c.Y < bestSum) { bestSum = c.X + c.Y; best = c; }
        return best;
    }

    /// <summary>MC 12256: the interior walkable cell at the GREATEST Chebyshev
    /// distance from the staging cell — the far edge of the 20-wide town, the one
    /// A* path this walk then walks. Same metric as the Sim's DistanceTo.</summary>
    private Vector2I CrossFarCell(Vector2I from)
    {
        var best = from;
        int bestD = -1;
        foreach (var c in Act.Tiles.Walkable)
        {
            if (!IsInterior(c) || c == from) continue;
            int d = Math.Max(Math.Abs(c.X - from.X), Math.Abs(c.Y - from.Y));
            if (d > bestD) { bestD = d; best = c; }
        }
        return best; // the Arrive caller FAILS on the from==to signature — never a silent pass
    }

    /// <summary>One synthesized LMB click — press then release, pushed through
    /// the window's real input queue (the CamLeg.Click idiom verbatim).</summary>
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
        GD.Print($"BABA_SMOOTH_LEG SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
    }

    private void Enter(Step step, double seconds)
    {
        _step = step;
        _deadline = Time.GetTicksMsec() / 1000.0 + seconds;
    }

    private void Fail(string where, string why)
    {
        GD.Print($"BABA_SMOOTH_LEG FAIL {where}: {why}");
        GetTree().Quit(1);
    }
}
