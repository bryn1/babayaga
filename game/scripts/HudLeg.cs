// reason (file-hygiene, MC 12222): ONE windowed leg runner — its whole value is
// that every number it accuses is re-derived HERE, independently of the HUD's own
// law, so the gates, the world reads and the click idiom stay in the file that
// makes the claim (the sibling legs are the same single-file shape at ~330 lines;
// this one carries five gates and their printed evidence rows instead of a split
// file that would put the arithmetic on the other side of the accusation).
using System;
using System.IO;
using System.Linq;
using Godot;

/// <summary>
/// MC 12222 BOTTOM-BAND LEG — the owner complaint "our UI is a bare left text
/// column; D2 carries a bottom band with orbs, a belt and an XP strip". Windowed
/// ONLY (Xvfb 1280×720), BABA_HUD_BAND_LEG names the evidence dir — unset, Boot
/// never builds this node (the MobScaleLeg gate shape). The REAL Act settles at
/// the town start (seed 42) and every state move below is ONE SYNTHESIZED CLICK;
/// the leg calls no sim verb and writes no state. It gates:
/// (a) SHAPE — the band root is on screen, flush to the window's bottom edge,
///     exactly Hud.BandHeightPx tall and full width, and its four belt cells are
///     visible;
/// (b) FILL == STATE — the life-orb px, mana-orb px and XP-strip px the band says
///     it PAINTED are re-derived HERE, from the live session, by the leg's OWN
///     arithmetic (an assertion that called the HUD's own fill law would prove
///     nothing); the belt letters and the level chip are re-derived too, and the
///     corner column's orb fill must equal the band's — one truth, two widgets;
/// (c) CLICK LAW BOTH WAYS — a ground click ABOVE the band still walks the hero
///     (MC 12136's idiom) and a click INSIDE the band's 90px zone leaves the
///     painted truth byte-identical while the WORLD decides the pixel: the band
///     swallows nothing (the law MC 11959 pinned, now under a widget that covers
///     the bottom of the screen);
/// (d) THE CONTROLS LINE — the MC 12136 line is still visible at its exact shipped
///     position, and the band's life orb does not overlap it;
/// (e) THE FILLS MOVE — one LMB kill pays XP (the strip's px grows) and the deep
///     row's counter-damage drops Life (the life orb's px shrinks), each still
///     equal to the state it reads, before and after pasted side by side.
/// SHOTS: 01-band.png (the town start, the whole band) and 02-band-combat.png
/// (after a kill — the strip moved), both with the world PAUSED and the present
/// queue drained (AnimLeg's freeze-shot shape).
/// RED proof: BABA_HUD_BAND_OFF (Hud.BandOffVar) HIDES the band → gate (a) goes
/// red on the band's own visibility. Markers "BABA_HUD_BAND OK …" /
/// "BABA_HUD_BAND FAIL …", pinned.
/// </summary>
public partial class HudLeg : Node
{
    public const string EnvVar = "BABA_HUD_BAND_LEG";
    public const ulong LegSeed = 42; // the anchor seed — the town start, the owner's first frame

    public string OutDir = "";
    public Act Act = null!;

    private enum Step
    {
        Settle, Reads, WaitShot, GroundClick, GroundWalk, BandClick, BandTruth, BandWalk, BandWalkWatch,
        DoorClick, WalkOut, MchySettle, KillClick, KillWatch, DamageClick, DamageWatch, Finish,
    }

    private Step _step = Step.Settle;
    private int _frames, _strikes;
    private double _deadline, _nextClick, _nextDbg, _shotUntil;
    private string _shotName = "", _truthBefore = "";
    private Action _afterShot = () => { };
    private Vector2I _door, _cellBefore, _deep;
    private long _lifeBeforeKill, _xpBeforeKill, _xpSpan;
    private int _lifePxBeforeKill, _lifePxAfterKill, _xpPxBeforeKill;
    private bool _heroWalked, _bandClickMovedHero, _bandWalked, _sawKill, _sawDamage;
    private Vector2 _bandPx = new(float.NaN, float.NaN); // a band pixel the WORLD owns

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
        if (_shotUntil > 0) // the frozen frame drains the present queue, then the shot lands
        {
            if (Time.GetTicksMsec() / 1000.0 < _shotUntil) return;
            _shotUntil = 0;
            Shot(_shotName);
            GetTree().Paused = false;
            _afterShot();
            return;
        }
        if (Time.GetTicksMsec() / 1000.0 > _deadline) { Fail(_step.ToString(), "timed out"); return; }
        var s = GameContext.Session;
        if (s is null) { Fail(_step.ToString(), "no session"); return; }
        if (s.IsDead && _step != Step.Finish) { Fail(_step.ToString(), "the hero died before the reads finished"); return; }

        switch (_step)
        {
            case Step.Settle:
                if (++_frames < 30) return; // settled presents only (the GroundLeg shape)
                GD.Print($"BABA_HUD_BAND_START seed={LegSeed} area={s.CurrentArea} cell={Act.Hero.Cell} " +
                         $"band_h={Hud.BandHeightPx} red_lever={Hud.BandOffVar}={System.Environment.GetEnvironmentVariable(Hud.BandOffVar)} " +
                         $"window={GetWindow().Size} band_off_env_set={System.Environment.GetEnvironmentVariable(Hud.BandOffVar) is { Length: > 0 }}");
                Enter(Step.Reads, 20.0);
                break;

            case Step.Reads:
                if (!Gate(out string why1)) { Fail("Reads", why1); return; }
                FreezeShot("01-band.png", () => Enter(Step.GroundClick, 20.0));
                Enter(Step.WaitShot, 25.0);
                break;

            case Step.WaitShot:
                break; // the shot's continuation owns the next transition

            case Step.GroundClick:
            {
                // (c1) a walkable cell whose pixel lands well ABOVE the band's top
                // edge — the click that must still reach the world.
                var target = GroundCellAboveBand();
                if (target is null) { Fail("GroundClick", "no walkable cell lands above the band at this camera"); return; }
                var px = Act.Hero.CellToScreen(target.Value);
                _cellBefore = Act.Hero.Cell;
                GD.Print($"BABA_HUD_BAND GROUND click_px=({(int)px.X},{(int)px.Y}) band_top={(int)(GetWindow().Size.Y - Hud.BandHeightPx)} " +
                         $"cell={target.Value} hero={_cellBefore}");
                Click(px);
                Enter(Step.GroundWalk, 25.0);
                break;
            }

            case Step.GroundWalk:
                if (Act.Hero.Cell == _cellBefore) return;
                _heroWalked = true;
                GD.Print($"BABA_HUD_BAND GROUND_OK hero {_cellBefore} -> {Act.Hero.Cell} (a click above the band still walks)");
                Enter(Step.BandClick, 20.0);
                break;

            case Step.BandClick:
            {
                // (c2) a click INSIDE the band's zone: the belt-cell row, dead
                // centre — the px the wood paints and the world shares.
                _truthBefore = Truth(s);
                _cellBefore = Act.Hero.Cell;
                var win = GetWindow().Size;
                var px = new Vector2(win.X / 2f, win.Y - Hud.BandHeightPx / 2f);
                var cell = IsoCamera.ScreenToGrid(Act.Hero.GetCanvasTransform().AffineInverse() * px);
                GD.Print($"BABA_HUD_BAND BANDCLICK px=({(int)px.X},{(int)px.Y}) inside_band=True cell={cell} " +
                         $"walkable={Act.Tiles.IsWalkable(cell)} hero={_cellBefore}");
                Click(px);
                Enter(Step.BandTruth, 6.0);
                break;
            }

            case Step.BandTruth:
            {
                string after = Truth(s);
                _bandClickMovedHero = Act.Hero.Cell != _cellBefore;
                if (after != _truthBefore)
                {
                    Fail("BandTruth", $"a click inside the band moved the painted truth: before[{_truthBefore}] after[{after}]");
                    return;
                }
                if (!Gate(out string why)) { Fail("BandTruth", $"the band stopped matching state after a click inside it: {why}"); return; }
                GD.Print($"BABA_HUD_BAND BANDCLICK_OK truth_unchanged=True hero_moved={_bandClickMovedHero} " +
                         $"(the band answered nothing; the world kept the pixel)");
                _bandPx = BandPxOverWorld(); // a band pixel whose cell the WORLD would answer
                Enter(Step.BandWalk, 20.0);
                break;
            }

            case Step.BandWalk:
                if (float.IsNaN(_bandPx.X))
                {
                    // Honest branch: at THIS camera the band's whole pixel row maps
                    // to ground the world refuses anyway, so there is no pixel for
                    // the band to swallow — the truth check above is the evidence.
                    GD.Print("BABA_HUD_BAND BANDWALK skipped — no walkable, monster-free cell under the band at this camera");
                }
                else
                {
                    _cellBefore = Act.Hero.Cell;
                    GD.Print($"BABA_HUD_BAND BANDWALK click_px=({(int)_bandPx.X},{(int)_bandPx.Y}) inside_band=True hero={_cellBefore} " +
                             "(a cell the world would answer, painted over by the band)");
                    Click(_bandPx);
                }
                var door = DoorCell();
                if (door == Act.Hero.Cell) { Fail("BandWalk", "no east-edge door cell to walk out through"); return; }
                _door = door;
                Enter(float.IsNaN(_bandPx.X) ? Step.DoorClick : Step.BandWalkWatch, float.IsNaN(_bandPx.X) ? 10.0 : 20.0);
                break;

            case Step.BandWalkWatch:
                if (Act.Hero.Cell == _cellBefore) return;
                _bandWalked = true;
                GD.Print($"BABA_HUD_BAND BANDWALK_OK hero {_cellBefore} -> {Act.Hero.Cell} " +
                         $"(the click REACHED THE WORLD THROUGH the band — the widget swallowed nothing)");
                Enter(Step.DoorClick, 10.0);
                break;

            case Step.DoorClick:
                Click(Act.Hero.CellToScreen(_door));
                Enter(Step.WalkOut, 60.0); // the queued click's walk gets its real span (AnimLeg's 60 s)
                break;

            case Step.WalkOut:
                if (s.CurrentArea != "mchy")
                {
                    double now = Time.GetTicksMsec() / 1000.0; // one honest stall readout every 5 s
                    if (now >= _nextDbg) { _nextDbg = now + 5.0; GD.Print($"BABA_HUD_BAND DEBUG area={s.CurrentArea} cell={Act.Hero.Cell} door={_door}"); }
                    return;
                }
                Enter(Step.MchySettle, 10.0);
                break;

            case Step.MchySettle:
                if (++_frames < 20) return; // the fresh area-build's presents drain before the reads
                _frames = 0;
                if (!Gate(out string why2)) { Fail("MchySettle", why2); return; }
                _lifeBeforeKill = s.Character.Life;
                _lifePxBeforeKill = Act.HudPanel.BandLifeFillPx;
                _xpBeforeKill = s.Character.Xp;
                _xpPxBeforeKill = (int)Act.HudPanel.BandXpFillPx;
                _xpSpan = XpSpan(s);
                GD.Print($"BABA_HUD_BAND BEFORE_KILL life={s.Character.Life}/{s.Character.MaxLife} life_px={_lifePxBeforeKill} " +
                         $"xp={s.Character.Xp} xp_into={s.Character.Xp - XpFloor(s)}/{_xpSpan} xp_px={_xpPxBeforeKill}/{Act.HudPanel.BandXpTrackPx} " +
                         $"kills={Act.Monsters.Kills}");
                var mob = FirstMonsterCell();
                if (mob is null) { Fail("MchySettle", "no standing monster to strike in mchy"); return; }
                _deep = mob.Value.Cell;
                Enter(Step.KillClick, 20.0);
                break;

            case Step.KillClick:
                Click(Torso(_deep));
                _nextClick = Time.GetTicksMsec() / 1000.0 + 0.9;
                GD.Print($"BABA_HUD_BAND KILL_CLICK torso_px={Torso(_deep)} cell={_deep} strike=1");
                Enter(Step.KillWatch, 40.0);
                break;

            case Step.KillWatch:
            {
                if (Act.Monsters.Kills > 0)
                {
                    _sawKill = true;
                    int pxAfter = (int)Act.HudPanel.BandXpFillPx;
                    if (!Gate(out string why)) { Fail("KillWatch", why); return; }
                    if (pxAfter <= _xpPxBeforeKill)
                    {
                        Fail("KillWatch", $"a kill paid no visible strip: xp_px {_xpPxBeforeKill} -> {pxAfter} (into {s.Character.Xp - XpFloor(s)}/{_xpSpan})");
                        return;
                    }
                    GD.Print($"BABA_HUD_BAND KILL_OK kills={Act.Monsters.Kills} xp={_xpBeforeKill} -> {s.Character.Xp} " +
                             $"xp_into={s.Character.Xp - XpFloor(s)}/{_xpSpan} xp_px={_xpPxBeforeKill} -> {pxAfter}");
                    FreezeShot("02-band-combat.png", () => Enter(Step.DamageClick, 30.0));
                    Enter(Step.WaitShot, 25.0);
                    return;
                }
                if (Time.GetTicksMsec() / 1000.0 < _nextClick) return;
                if (++_strikes > 8) { Fail("KillWatch", $"no kill in {_strikes} strikes at {_deep}"); return; }
                _nextClick = Time.GetTicksMsec() / 1000.0 + 0.9;
                Click(Torso(_deep));
                GD.Print($"BABA_HUD_BAND KILL_CLICK strike={_strikes} cell={_deep}");
                break;
            }

            case Step.DamageClick:
                _deep = DeepCell() ?? _deep;
                _strikes = 0;
                Click(Torso(_deep));
                _nextClick = Time.GetTicksMsec() / 1000.0 + 0.9;
                GD.Print($"BABA_HUD_BAND DAMAGE_CLICK cell={_deep} life={s.Character.Life}/{s.Character.MaxLife} " +
                         $"life_px={Act.HudPanel.BandLifeFillPx} strike=1");
                Enter(Step.DamageWatch, 45.0);
                break;

            case Step.DamageWatch:
            {
                if (s.Character.Life < _lifeBeforeKill)
                {
                    _sawDamage = true;
                    _lifePxAfterKill = Act.HudPanel.BandLifeFillPx;
                    if (!Gate(out string why)) { Fail("DamageWatch", why); return; }
                    GD.Print($"BABA_HUD_BAND DAMAGE_OK life={_lifeBeforeKill}/{s.Character.MaxLife} -> {s.Character.Life}/{s.Character.MaxLife} " +
                             $"life_px={_lifePxBeforeKill} -> {_lifePxAfterKill} over {Act.HudPanel.BandOrbPx}px (the orb shrank WITH the pool)");
                    Enter(Step.Finish, 15.0);
                    return;
                }
                if (Time.GetTicksMsec() / 1000.0 < _nextClick) return;
                if (++_strikes > 12) { Fail("DamageWatch", $"12 strikes at the deep row {_deep} and Life never moved"); return; }
                _nextClick = Time.GetTicksMsec() / 1000.0 + 0.9;
                Click(Torso(_deep));
                GD.Print($"BABA_HUD_BAND DAMAGE_CLICK strike={_strikes} cell={_deep} life={s.Character.Life}");
                break;
            }

            case Step.Finish:
                if (!Gate(out string whyF)) { Fail("Finish", whyF); return; }
                GD.Print($"BABA_HUD_BAND OK band={GetWindow().Size.X}x{Hud.BandHeightPx} slots={Act.HudPanel.BandSlotCount} " +
                         $"orb={Act.HudPanel.BandOrbPx}px strip={Act.HudPanel.BandXpFillPx}/{Act.HudPanel.BandXpTrackPx}px " +
                         $"life_px={Act.HudPanel.BandLifeFillPx} mana_px={Act.HudPanel.BandManaFillPx} " +
                         $"belt=[{Act.HudPanel.BandBeltShown}] chip={Act.HudPanel.BandChipShown} " +
                         $"ground_walk={_heroWalked} band_click_harmless=True band_walkthrough={_bandWalked} kill={_sawKill} damage={_sawDamage} " +
                         $"column_and_band_agree=True");
                GetTree().Quit(0);
                break;
        }
    }

    // ── the gates ────────────────────────────────────────────────────────────

    /// <summary>Gates (a) shape, (b) fill==state and (d) the controls line, over
    /// the LIVE session — and the READS line it prints IS the evidence paste, so
    /// what the assertion used is what the reader sees. The fill numbers are the
    /// leg's own arithmetic (Px below), never a call into HudReadout.</summary>
    private bool Gate(out string why)
    {
        why = "";
        var hud = Act.HudPanel;
        var s = GameContext.Session!;
        var win = GetWindow().Size;
        var band = hud.BandRoot;

        // (a) SHAPE — the band is there, flush to the bottom, full width, its own height.
        if (band is null) { why = "the HUD carries no band node"; return false; }
        if (!band.IsVisibleInTree()) { why = $"the band root is not visible (the {Hud.BandOffVar} lever hides it)"; return false; }
        if ((int)band.Size.Y != Hud.BandHeightPx) { why = $"band is {(int)band.Size.Y}px tall, not {Hud.BandHeightPx}"; return false; }
        if ((int)band.Size.X != win.X) { why = $"band is {(int)band.Size.X}px wide, the window is {win.X}"; return false; }
        if ((int)band.Position.X != 0 || (int)band.Position.Y != win.Y - Hud.BandHeightPx)
        { why = $"band not flush to the bottom edge: pos {(int)band.Position.X},{(int)band.Position.Y}"; return false; }
        if (hud.BandSlotCount != BabaYaga.Sim.GameSession.BeltSlots) { why = $"{hud.BandSlotCount} belt cells, the Sim's ceiling is {BabaYaga.Sim.GameSession.BeltSlots}"; return false; }
        if (!hud.BandSlotsShown) { why = "not every belt cell is on screen"; return false; }

        // (b) FILL == STATE, re-derived from the live pools by the leg's own math.
        int orb = hud.BandOrbPx, track = hud.BandXpTrackPx;
        long lo = XpFloor(s), span = XpSpan(s);
        long into = Math.Max(0, s.Character.Xp - lo);
        long wantLife = Px(s.Character.Life, s.Character.MaxLife, orb);
        long wantMana = Px(s.Character.Mana, s.Character.MaxMana, orb);
        long wantXp = span <= 0 ? track : Px(into, span, track);
        if (orb <= 0 || track <= 0) { why = $"the band never sized itself (orb {orb}px strip {track}px)"; return false; }
        if (hud.BandLifeFillPx != wantLife) { why = $"life orb says {hud.BandLifeFillPx}px, {s.Character.Life}/{s.Character.MaxLife} over {orb}px is {wantLife}px"; return false; }
        if (hud.BandManaFillPx != wantMana) { why = $"mana orb says {hud.BandManaFillPx}px, {s.Character.Mana}/{s.Character.MaxMana} over {orb}px is {wantMana}px"; return false; }
        if (hud.BandXpFillPx != wantXp) { why = $"xp strip says {hud.BandXpFillPx}px, {into}/{span} over {track}px is {wantXp}px"; return false; }
        if (hud.LifeFillPx != hud.BandLifeFillPx) { why = $"the column's orb drew {hud.LifeFillPx}px and the band's drew {hud.BandLifeFillPx}px — two truths, one pull"; return false; }

        string belt = string.Join(",", s.Belt.Select(i => i is null ? "-" : char.ToUpperInvariant(i.Base.Name[0]).ToString()));
        if (hud.BandBeltShown != belt) { why = $"belt cells show [{hud.BandBeltShown}], session.Belt is [{belt}]"; return false; }
        string chip = span > 0 ? $"lvl {s.Level}" : $"lvl {s.Level} MAX";
        if (hud.BandChipShown != chip) { why = $"chip shows \"{hud.BandChipShown}\", the level is {s.Level} (span {span})"; return false; }

        // (d) THE MC 12136 CONTROLS LINE — same node, same pixels, and no overlap
        // with the band's life orb (the orb was laid out to the RIGHT of it).
        var line = hud.GetNodeOrNull<Control>("ControlsLine");
        if (line is null) { why = "the MC 12136 ControlsLine node is gone from the HUD"; return false; }
        if (!line.IsVisibleInTree()) { why = "the ControlsLine is no longer visible"; return false; }
        if ((int)line.Position.X != 8 || (int)line.Position.Y != win.Y - 44)
        { why = $"the ControlsLine moved to {(int)line.Position.X},{(int)line.Position.Y} (shipped: 8,{win.Y - 44})"; return false; }
        if (new Rect2(hud.BandLifePos, new Vector2(orb, orb)).Intersects(new Rect2(line.Position, line.Size)))
        { why = "the band's life orb overlaps the controls line — the corner is taken"; return false; }

        GD.Print($"BABA_HUD_BAND READS band={win.X}x{(int)band.Size.Y}@y{(int)band.Position.Y} orb={orb}px strip={track}px " +
                 $"life={s.Character.Life}/{s.Character.MaxLife} life_px={hud.BandLifeFillPx} " +
                 $"mana={s.Character.Mana}/{s.Character.MaxMana} mana_px={hud.BandManaFillPx} " +
                 $"xp_into={into}/{span} xp_px={hud.BandXpFillPx} belt=[{hud.BandBeltShown}] chip={hud.BandChipShown} " +
                 $"slots={hud.BandSlotCount} shown={hud.BandSlotsShown} controls={line.Position} line_vis={line.IsVisibleInTree()}");
        return true;
    }

    /// <summary>The leg's OWN display-scale arithmetic: current over max, floored
    /// into an extent, clamped at both ends. Deliberately NOT HudReadout.OrbFillPx
    /// — a check that calls the code under test proves nothing; this is the same
    /// formula written twice on purpose, once to paint and once to accuse.</summary>
    private static long Px(long current, long max, long extent) =>
        current <= 0 || max <= 0 ? 0 : current >= max ? extent : current * extent / max;

    /// <summary>The truth the band paints, as ONE string: a click inside the band
    /// must leave it byte-identical (the band answers no click), so the leg
    /// compares this instead of trusting a screenshot.</summary>
    private static string Truth(BabaYaga.Sim.GameSession s) =>
        $"gold={s.Gold} life={s.Character.Life}/{s.Character.MaxLife} mana={s.Character.Mana}/{s.Character.MaxMana} " +
        $"xp={s.Character.Xp} lvl={s.Level} belt=[{string.Join(",", s.Belt.Select(i => i is null ? "-" : i.Base.Name))}] " +
        $"area={s.CurrentArea} target={s.EngagedKey ?? "-"}";

    // The XP ladder reads, off the Sim's own computed thresholds (never re-derived).
    private static long XpFloor(BabaYaga.Sim.GameSession s) => s.Ladder.TotalXpForLevel(s.Level);
    private static long XpSpan(BabaYaga.Sim.GameSession s) =>
        s.Ladder.TotalXpForLevel(s.Level >= s.Ladder.MaxLevel ? s.Level : s.Level + 1) - s.Ladder.TotalXpForLevel(s.Level);

    // ── the world reads (presentation only — the leg never writes state) ─────

    /// <summary>A walkable cell whose screen pixel lands ABOVE the band's top edge
    /// and inside the window, at least two cells off the hero — the pick is the one
    /// closest to the brief's probe row (a ground click at y≈400), because an
    /// off-screen pixel would prove nothing about the band's own zone. Null when
    /// the camera offers none.</summary>
    private Vector2I? GroundCellAboveBand()
    {
        int bandTop = GetWindow().Size.Y - Hud.BandHeightPx - 60;
        var from = Act.Hero.Cell;
        Vector2I? best = null;
        int bestErr = int.MaxValue;
        foreach (var c in Act.Tiles.Walkable)
        {
            if (Math.Max(Math.Abs(c.X - from.X), Math.Abs(c.Y - from.Y)) < 2) continue;
            var px = Act.Hero.CellToScreen(c);
            if (px.Y > bandTop || px.Y < 40 || px.X < 40 || px.X > GetWindow().Size.X - 40) continue;
            int err = Math.Abs((int)px.Y - 400); // the mid-screen pixel: 12136's own click band
            if (err < bestErr) { bestErr = err; best = c; }
        }
        return best;
    }

    /// <summary>A pixel inside the band's own zone whose cell the WORLD would walk
    /// the hero to — walkable, monster-free, at least two cells off him. The
    /// click-through proof: if the band owned this pixel the hero would never
    /// move. NaN when the camera puts only refused ground under the band.</summary>
    private Vector2 BandPxOverWorld()
    {
        var win = GetWindow().Size;
        var from = Act.Hero.Cell;
        var xf = Act.Hero.GetCanvasTransform().AffineInverse();
        for (int y = Hud.BandHeightPx - 12; y >= 12; y -= 12)
            for (int x = 60; x < win.X - 60; x += 24)
            {
                var px = new Vector2(x, win.Y - y);
                var world = xf * px;
                var cell = IsoCamera.ScreenToGrid(world);
                if (!Act.Tiles.IsWalkable(cell) || Act.Monsters.MonsterAt(world) is not null) continue;
                if (Math.Max(Math.Abs(cell.X - from.X), Math.Abs(cell.Y - from.Y)) < 2) continue;
                return px;
            }
        return new Vector2(float.NaN, float.NaN);
    }

    /// <summary>The EAST-edge door cell nearest the hero (AnimLeg's / MobScaleLeg's
    /// walk-out pick — the link-0 grodzisko→mchy door).</summary>
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

    /// <summary>A standing monster — pokrzywnik first (the near-spawn band, the
    /// one-strike row): the presentation roster read, never Sim state.</summary>
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

    /// <summary>The monster furthest from the area centre — S4's row that SURVIVES
    /// a strike and therefore pays counter-damage (the life orb's before/after).</summary>
    private Vector2I? DeepCell()
    {
        var centre = Act.Tiles.CenterCell();
        (string Key, Vector2I Cell)? best = null;
        var row = Act.Monsters.RowSnapshot();
        for (int i = 0; i < row.Count; i++)
        {
            int d = Math.Max(Math.Abs(row[i].Cell.X - centre.X), Math.Abs(row[i].Cell.Y - centre.Y));
            if (best is null || d > Math.Max(Math.Abs(best.Value.Cell.X - centre.X), Math.Abs(best.Value.Cell.Y - centre.Y))) best = row[i];
        }
        return best?.Cell;
    }

    /// <summary>The DRAWN torso of a cell, in window px — the hit circle's own
    /// formula, transformed by the live canvas (MobScaleLeg's click shape).</summary>
    private Vector2 Torso(Vector2I cell) => Act.Hero.GetCanvasTransform() * MonsterField.BodyCentreWorld(cell);

    /// <summary>One synthesized LMB click — press then release through the window's
    /// real input queue (the SmoothLeg/AnimLeg/MobScaleLeg idiom verbatim).</summary>
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
        _afterShot = onDone;
        _shotUntil = Time.GetTicksMsec() / 1000.0 + 1.5;
    }

    private void Shot(string name)
    {
        var img = GetViewport().GetTexture().GetImage();
        var path = Path.Combine(OutDir, name);
        img.SavePng(path);
        GD.Print($"BABA_HUD_BAND SHOT {path} {img.GetWidth()}x{img.GetHeight()}");
    }

    private void Enter(Step step, double seconds)
    {
        _step = step;
        _deadline = Time.GetTicksMsec() / 1000.0 + seconds;
    }

    private void Fail(string where, string why)
    {
        GD.Print($"BABA_HUD_BAND FAIL {where}: {why}");
        GetTree().Quit(1);
    }
}
