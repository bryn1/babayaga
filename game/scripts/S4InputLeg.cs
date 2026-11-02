using System;
using System.IO;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// M5-S4 INPUT LEG (MC 11937, DoD 2/3): the scripted windowed playthrough the
/// evidence run drives on Xvfb. METHOD (stated per the brief): the game runs a
/// REAL windowed GL build on Xvfb 1280×720; every "click" is a SYNTHESIZED
/// InputEventMouseButton/Key pushed through Window.PushInput — the SAME input
/// pipeline a human X11 click rides into _UnhandledInput (ParseInputEvent
/// alone updates Input state and never reaches the scene tree; PushInput is
/// the routing half of the brief's "parse_input_event from a test-driver
/// scene" option). Screenshots are the root viewport's GPU texture read back
/// (get_texture().get_image()) at the 1280×720 window the leg pins on start.
/// The driver reads session/scene state for its markers and NEVER calls a sim
/// verb (the GameInputTests negative row scans this file); every state move
/// happens as a click landing on the real call sites. Env-gated (BABA_S4_LEG
/// names the output dir) and windowed-only — unset, Boot never even builds
/// this node, and the story anchors byte-count. BABA_S4_CONTINUE=1 loads
/// user://save instead of New (the S3-named Continue exception: comment head,
/// no replay contract — the heal leg rides it).
/// </summary>
public partial class S4InputLeg : Node
{
    public const string EnvVar = "BABA_S4_LEG";
    public const string ContinueVar = "BABA_S4_CONTINUE";
    public const ulong LegSeed = 1; // printed as M5_S4_SEED — the replay's --seed

    public string OutDir = "";
    public Act Act = null!;

    private enum Step { TownSettle, TownMove, TownShot, WalkOut, MchyShot, AttackShot,
        KillWait, DespawnShot, Potion, PotionShot, AttackToDeath, DeadSettle,
        EscMenu, EscClick, RespawnClick, PostRespawn, RideStone, RideSettle, Finish }

    private Step _step = Step.TownSettle;
    private int _frames;
    private double _deadline;
    private double _nextClick;
    private int _shots;
    private int _strikes;
    private int _spritesBefore;
    private int _spritesAfterKill;
    private long _xpAtKill;
    private Vector2I _moveTarget;
    private string _monPos = "";
    private readonly bool _continueMode;

    public S4InputLeg() => _continueMode = System.Environment.GetEnvironmentVariable(ContinueVar) == "1";

    private GameSession S => GameContext.Session!;

    public override void _Ready()
    {
        GetWindow().Position = Vector2I.Zero;
        GetWindow().Size = new Vector2I(1280, 720); // the judged leg size (plan §1 P-rows)
        // The leg's shots are X11 root grabs (see Shot): under Xvfb the texture
        // readback lags GPU present by seconds — a probe measured ~280fps DRAWN
        // yet stale READS, so the presented framebuffer is the honest capture.
        GD.Print($"M5_S4_SEED {S.Seed} mode={(_continueMode ? "continue" : "newgame")}");
        Enter(Step.TownSettle, 1.5); // let the resize + first frames settle before reading pixels
    }

    public override void _Process(double delta)
    {
        _frames++;
        if (OutDir.Length == 0) { Fail("OutDir unset"); return; }
        if (_continueMode) { ContinueLeg(); return; }
        // The step timeout rides FIRST — every predicate below returns early while
        // unmet, and an early return must never hide the deadline (the first XVFB
        // leg caught exactly this: the check once sat after the switch, a wedged
        // leg ran on. The gate proves itself RED before it is trusted.).
        if (Time.GetTicksMsec() / 1000.0 > _deadline) { Fail($"{_step} timed out"); return; }
        switch (_step)
        {
            case Step.TownSettle:
                // The texture readback answers EMPTY until the first real frames
                // present (the XVFB leg's own RED: shot 01 came back blank).
                if (_frames < 12) return;
                Shot("town");
                GD.Print($"M5_S4_TOWN area={S.CurrentArea} stone_lit={Act.Tiles.IsLitStone(Act.Tiles.StoneCell!.Value)}");
                // P3: a far walkable cell — LMB click there plans the A* path (presentation, no sim call).
                _moveTarget = FarTownCell();
                Click(Act.Hero.CellToScreen(_moveTarget), MouseButton.Left);
                Enter(Step.TownMove, 40.0);
                break;
            case Step.TownMove:
                if (Act.Hero.Cell != _moveTarget)
                {
                    DebugMove("town-move"); // the leg's own diagnosis when a walk stalls
                    return;
                }
                GD.Print($"M5_S4_MOVE_OK cell={_moveTarget}");
                Enter(Step.TownShot, 6.0); // the post-walk PRESENT queue drains longest here (the leg's RED: a 2.5s span still read a stale frame)
                break;
            case Step.TownShot:
                Shot("town-moved");
                // P4: click the area-edge DOOR cell — the walk lands on it, then the ONE travel
                // door fires discover+waypoint (walked-out leg; lights mchy's stone).
                Click(Act.Hero.CellToScreen(DoorCell()), MouseButton.Left);
                Enter(Step.WalkOut, 60.0);
                break;
            case Step.WalkOut:
                if (S.CurrentArea != "mchy") return;
                GD.Print($"M5_S4_WALKOUT_OK area=mchy hint=\"{HintText()}\""); // P4's corrected hint line
                _spritesBefore = Act.Monsters.SpriteCount;
                GD.Print($"M5_S4_SPRITE_COUNT spawned={_spritesBefore}"); // S7's marker form, S4's spawn row
                var cell = FirstMonsterCell() ?? throw new InvalidOperationException("no pokrzywnik stood in mchy");
                _monPos = $"{cell.Item2.X},{cell.Item2.Y}";
                Click(Act.Hero.CellToScreen(cell.Item2), MouseButton.Right); // P5: strike what stands there
                Enter(Step.MchyShot, 2.5);
                break;
            case Step.MchyShot:
                Shot("mchy"); // the arrived field: hero + spawned row + lit stone (settled present)
                Enter(Step.AttackShot, 2.5);
                break;
            case Step.AttackShot:
                Shot("mid-attack"); // P5 mid-volley: the strike hint rides, counters already paid
                Enter(Step.KillWait, 40.0);
                break;
            case Step.KillWait:
                if (S.IsDead)
                {
                    GD.PrintErr("M5_S4_LEG_FAIL hero died before the first kill row — attack leg cannot prove the despawn");
                    GetTree().Quit(1); return;
                }
                if (Act.Monsters.Kills == 0 || !Act.Monsters.LastKilledFreed) { ReclickMonster(); return; }
                _spritesAfterKill = Act.Monsters.SpriteCount;
                _xpAtKill = S.Character.Xp; // READ — the XP the kill paid
                // The despawn, per INSTANCE: the killed sprite object is gone, the row
                // count stands at full (the Sim's fresh-per-kill rule, mirrored 1:1).
                GD.Print($"M5_S4_DESPAWN_OK kills={Act.Monsters.Kills} killed_sprite_freed={Act.Monsters.LastKilledFreed} row_full={_spritesAfterKill} xp={_xpAtKill}");
                Enter(Step.DespawnShot, 2.5); // the fresh-mirror row repaints before the shot
                break;
            case Step.DespawnShot:
                Shot("after-kill");
                // P7's use-click at the ONE door: New Game carries an EMPTY belt (the belt
                // fill is S6's TownPanel row) — the honest S4 read is the door's REJECT
                // surfacing as a HUD hint, riding the log as its comment (replay skips it).
                KeyPress(Key.Key1);
                Enter(Step.Potion, 0.6);
                break;
            case Step.Potion:
                GD.Print($"M5_S4_POTION_CLICK hint=\"{HintText()}\"");
                Enter(Step.PotionShot, 2.5);
                break;
            case Step.PotionShot:
                Shot("potion-reject"); // the empty-belt REJECT frame (settled present)
                ReclickMonster();
                Enter(Step.AttackToDeath, 240.0); // the deep-row grind (~550 strikes at the click tempo, per DeathLeg's measure)
                break;
            case Step.AttackToDeath:
                if (S.IsDead)
                {
                    GD.Print($"M5_S4_DEAD strikes={_strikes} life={S.Character.Life}");
                    Enter(Step.DeadSettle, 1.2); // let the clock's frozen hint paint (P11's first half)
                    return;
                }
                // Re-target EVERY strike (the last kill despawned that sprite).
                // DEEP-first: the ponurak rows survive strikes and pay counter-
                // damage (DeathLeg's finding: the grind dies on the deep row), so
                // the same click kills AND grinds — a near pokrzywnik dies in one
                // strike and never kills a koldun.
                var live = DeepCell() ?? FirstMonsterCell();
                if (live is null) { Fail("no monster sprite left to attack while the hero lived"); return; }
                _monPos = $"{live.Value.Cell.X},{live.Value.Cell.Y}";
                ReclickMonster();
                break;
            case Step.DeadSettle:
                GD.Print($"M5_S4_DEATH_HINT text=\"{HintText()}\"");
                Shot("death-menu-pre");
                KeyPress(Key.Escape); // P11: Esc for the DEAD opens the death menu (no pause for the living)
                Enter(Step.EscMenu, 3.0);
                break;
            case Step.EscMenu:
                if (FindMenu()?.Visible != true)
                {
                    double now = Time.GetTicksMsec() / 1000.0;
                    if (now >= _nextDebug)
                    {
                        _nextDebug = now + 5.0;
                        GD.Print($"M5_S4_DEBUG esc menu={FindMenu() is not null} " +
                            $"focus={GetViewport().GuiGetFocusOwner()?.GetName() ?? "none"} dead={S.IsDead}");
                    }
                    return;
                }
                Enter(Step.EscClick, 4.0); // the menu PAINTS before the shot reads it (present lag ~2-3s — the leg's RED: a 0.4s span showed the pre-menu frame)
                break;
            case Step.EscClick:
                DeathMenu menu = FindMenu() ?? throw new InvalidOperationException("menu vanished");
                var button = FindButton(menu, DeathMenuLogic.RespawnButton)
                    ?? throw new InvalidOperationException("no RESPAWN button");
                Shot("death-menu");
                Click(button.GetGlobalRect().GetCenter(), MouseButton.Left); // the GUI click the owner can find
                Enter(Step.RespawnClick, 5.0);
                break;
            case Step.RespawnClick:
                if (S.IsDead || S.CurrentArea != "grodzisko") return;
                GD.Print($"M5_S4_RESPAWN_OK alive=1 area={S.CurrentArea} life={S.Character.Life}");
                Enter(Step.PostRespawn, 4.0); // the raise PAINTS before the shot reads it (present lag ~2-3s)
                break;
            case Step.PostRespawn:
                Shot("post-respawn"); // alive hero at the grodzisko stone, full HUD (P11's second half)
                // P9: the LIT town stone marker under LMB — the ride gate resolves mchy
                // (lit by the walk-out) and the ONE door flips the Area with NO path step.
                Click(Act.Hero.CellToScreen(Act.Tiles.StoneCell!.Value), MouseButton.Left);
                Enter(Step.RideStone, 3.0);
                break;
            case Step.RideStone:
                if (S.CurrentArea != "mchy") return;
                GD.Print($"M5_S4_RIDE_OK area=mchy walking={Act.Hero.IsWalking}"); // [C]: flipped, zero steps
                Enter(Step.RideSettle, 0.8);
                break;
            case Step.RideSettle:
                Shot("after-ride");
                Enter(Step.Finish, 0.3);
                break;
            case Step.Finish:
                string? logPath = System.Environment.GetEnvironmentVariable(CommandLog.EnvVar);
                if (logPath is not null)
                {
                    bool respawnLogged = File.ReadAllLines(logPath).Length > 0
                        && Array.Exists(File.ReadAllLines(logPath), l => l.Trim() == "respawn");
                    GD.Print($"M5_S4_LOG_RESPAWN_LINE {respawnLogged}"); // DoD 3's log row, machine-read here
                }
                CommandLog.Ambient?.Record("dump"); // the log ends on its own dump command
                GD.Print($"M5_S4_DUMP {BridgeLeg.DumpLine(S)}");
                GD.Print($"M5_S4_LEG_OK shots={_shots} strikes={_strikes} xp={S.Character.Xp} gold={S.Gold}");
                GetTree().Quit(0);
                break;
        }
        if (Time.GetTicksMsec() / 1000.0 > _deadline) Fail($"{_step} timed out");
    }

    // ---- continue mode: the P7 heal leg (staged save: potion BELTED, life chipped) ----

    private bool _healClicked;
    private long _lifeBeforeHeal;
    private double _healOkAt;

    private void ContinueLeg()
    {
        if (!_healClicked)
        {
            if (Time.GetTicksMsec() / 1000.0 < _deadline) return;
            _lifeBeforeHeal = S.Character.Life;
            Shot("heal-before");
            KeyPress(Key.Key1); // the belt_1 use click at the UsePotion door — the BELT was filled by the staged save (S6's widget owns in-game filling)
            _healClicked = true;
            _deadline = Time.GetTicksMsec() / 1000.0 + 1.0;
            return;
        }
        if (S.Character.Life <= _lifeBeforeHeal) return;
        if (_healOkAt == 0)
        {
            GD.Print($"M5_S4_HEAL_OK life {_lifeBeforeHeal}->{S.Character.Life} hint=\"{HintText()}\"");
            _healOkAt = Time.GetTicksMsec() / 1000.0 + 4.0; // llvmpipe present lag (~2-3s): the frame must SHOW the healed HUD (the leg's RED: a same-tick read showed 39/50)
            return;
        }
        if (Time.GetTicksMsec() / 1000.0 < _healOkAt) return;
        Shot("heal-after");
        GD.Print("M5_S4_CONTINUE_OK"); // named exception (S3): comment head — no dump, no replay contract
        GetTree().Quit(0);
    }

    // ---- the leg's tiny tools: clicks, keys, shots, reads ----

    private void Enter(Step step, double seconds)
    {
        _step = step;
        _deadline = Time.GetTicksMsec() / 1000.0 + seconds;
    }

    private void Fail(string why)
    {
        GD.PrintErr($"M5_S4_LEG_FAIL {_step}: {why}");
        GetTree().Quit(1);
    }

    // Synthetic clicks, keys, shots, HUD read-back and the map picks live in
    // the S4InputLeg.Tools.cs twin of this partial class (file-hygiene split).
}
