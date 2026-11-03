using System;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// MC 11957 GAME-MENU LEG (DoD 2): the scripted windowed playthrough the
/// evidence runner drives on Xvfb — the StashChestLeg/S4 rig reused, not
/// re-invented (same PushInput clicks/keys, same settled-present Shots, same
/// env-gated windowed-only build in Boot). BABA_MENU_CONTINUE=1 boots through
/// GameContext.Load of the runner-STAGED save (the S4 ContinueVar shape): the
/// staged session died at the real counter-damage door, so the leg OPENS on
/// the DEAD hero — the interlock row first: ESC while dead must open the
/// DEATH menu with the game menu never even instantiated (an ABSENT-node read
/// — stronger than a hidden one). Then RESPAWN (the existing door) lifts the
/// hero and the menu rows ride in order: ESC opens (M5_MENU_OPEN, buttons
/// hunted by the GameMenuLogic labels — the panel paints and the leg hunts
/// with the SAME strings); ESC-ESC double-toggle state reads; T then ESC =
/// the TOWN-PANEL OVERLAP row (menu layer above the town layer, BOTH visible
/// — the stated reading: the menu paints OVER and never closes it; RESUME
/// reveals the town panel still open); then the stale save file is REMOVED,
/// the leg DEFERS a reparent under Root (the scene change frees Boot+Act —
/// the post-change scene-name read needs a node that outlives them), and one
/// SAVE &amp; QUIT click rides GameMenuLogic's order: the save file EXISTS
/// again (path+bytes read from disk) THEN GetTree().CurrentScene is the
/// MainMenu. Every state move is a synthesized input (the leg calls no sim
/// verb — GameMenuTests scan rows pin it); the menu opens no SIM doors, so
/// the BABA_LOG lines are only the continue-head + the death menu's own
/// RESPAWN line — the leg reads the log to prove it.
/// Env-gated (BABA_MENU_LEG names the output dir), windowed-only — unset,
/// Boot never builds this node, and the story anchors stay byte-identical.
/// </summary>
public partial class GameMenuLeg : Node
{
    public const string EnvVar = "BABA_MENU_LEG";
    public const string ContinueVar = "BABA_MENU_CONTINUE";
    public const ulong LegSeed = 5; // printed as M5_MENU_SEED — the replay's --seed

    public string OutDir = "";
    public Act Act = null!;

    private enum Step { DeadSettle, DeadEsc, DeadSettle2, DeadShot, RespawnWait,
        EscOpen, MenuSettle, MenuShot, ToggleAway, ToggleBack, MenuHide2, TownOpen,
        MenuOverTown, OverTownShot, ResumeWait, ResumeShot, TownClose,
        ArmQuit, Reparented, QuitOpen, QuitClick, SceneWait, QuitShot, Finish }

    private Step _step = Step.DeadSettle;
    private int _frames;
    private double _deadline;
    private double _settle;
    private int _shots;
    private long _saveBytesBefore; // the deleted-file proof: 0 = the door must CREATE it

    private GameSession S => GameContext.Session!;

    public override void _Ready()
    {
        GetWindow().Position = Vector2I.Zero;
        GetWindow().Size = new Vector2I(1280, 720); // the judged leg size (plan §1)
        GD.Print($"M5_MENU_SEED {S.Seed} continue mode; dead_at_boot={S.IsDead} area={S.CurrentArea}");
        Enter(Step.DeadSettle, 1.5); // resize + first frames settle before pixels are read
    }

    public override void _Process(double delta)
    {
        _frames++;
        if (OutDir.Length == 0) { Fail("OutDir unset"); return; }
        double now = Time.GetTicksMsec() / 1000.0;
        if (now > _deadline) { Fail($"{_step} timed out"); return; } // timeout FIRST (the S4 law)
        switch (_step)
        {
            case Step.DeadSettle:
                if (_frames < 12) return; // the readback answers EMPTY before the first presents
                if (!S.IsDead) { Fail($"the staged save must leave the hero DEAD, alive area={S.CurrentArea}"); return; }
                Enter(Step.DeadEsc, 2.0);
                break;
            case Step.DeadEsc:
                KeyPress(Key.Escape); // ESC against a corpse: the DEATH menu is authoritative (P11)
                Enter(Step.DeadSettle2, 4.0);
                break;
            case Step.DeadSettle2:
                if (FindDeathMenu()?.Visible != true) return;
                // The interlock read, honest and STRONG: the game menu was never even
                // instantiated while dead — an ABSENT node, not merely hidden.
                if (FindGameMenu() is not null)
                { Fail("ESC while DEAD instantiated the game menu — the death menu is not alone"); return; }
                GD.Print("M5_MENU_DEAD_INTERLOCK dead=True death_menu=visible game_menu=absent");
                EnterSettle(Step.DeadShot, 3.0); // the death menu must PAINT before the shot (present lag, the S4 law)
                break;
            case Step.DeadShot:
                if (now < _settle) return;
                Shot("dead-esc-death-menu"); // [01] ESC-during-dead: the death menu, no game menu
                ClickButton(FindDeathMenu()!, DeathMenuLogic.RespawnButton); // the existing door lifts the hero
                Enter(Step.RespawnWait, 8.0);
                break;
            case Step.RespawnWait:
                if (S.IsDead || S.CurrentArea != "grodzisko") return;
                GD.Print($"M5_MENU_RESPAWN alive=True area={S.CurrentArea} life={S.Character.Life}");
                Enter(Step.EscOpen, 3.0);
                break;
            case Step.EscOpen:
                KeyPress(Key.Escape); // ALIVE now: the same key opens the GAME menu (the D2 row)
                Enter(Step.MenuSettle, 4.0);
                break;
            case Step.MenuSettle:
                var m = FindGameMenu();
                if (m?.Visible != true) return;
                if (FindButtonByText(m, GameMenuLogic.ResumeButton) is null) return;
                if (FindButtonByText(m, GameMenuLogic.SaveQuitButton) is null) return;
                GD.Print($"M5_MENU_OPEN dead=False visible=True layer={LayerOf("GameMenuLayer")} "
                    + $"buttons=[{GameMenuLogic.ResumeButton},{GameMenuLogic.SaveQuitButton}]");
                EnterSettle(Step.MenuShot, 3.0);
                break;
            case Step.MenuShot:
                if (now < _settle) return;
                Shot("menu-open"); // [02] the game menu over town
                KeyPress(Key.Escape); // double-toggle row, half 1: a second Esc HIDES it
                Enter(Step.ToggleAway, 4.0);
                break;
            case Step.ToggleAway:
                if (FindGameMenu()?.Visible != false) return;
                GD.Print("M5_MENU_TOGGLE1 second_esc_hidden=True");
                KeyPress(Key.Escape); // half 2: a third opens it (the Act doors' idiom)
                Enter(Step.ToggleBack, 4.0);
                break;
            case Step.ToggleBack:
                if (FindGameMenu()?.Visible != true) return;
                GD.Print("M5_MENU_TOGGLE2 third_esc_shown=True");
                KeyPress(Key.Escape); // close it — ONE press, the wait steps below read it
                Enter(Step.MenuHide2, 4.0);
                break;
            case Step.MenuHide2:
                if (FindGameMenu()?.Visible != false) return;
                KeyPress(Key.T); // the pedlar panel (Act's town-gated door) — ONE press
                Enter(Step.TownOpen, 4.0);
                break;
            case Step.TownOpen:
                if (FindTownPanel()?.Visible != true) return;
                KeyPress(Key.Escape); // the OVERLAP row: the menu above the town panel — the stated
                Enter(Step.MenuOverTown, 4.0); // reading: paints OVER, NEVER closes the panel
                break;
            case Step.MenuOverTown:
                var mm = FindGameMenu();
                if (mm?.Visible != true) return;
                bool above = LayerOf("GameMenuLayer") > LayerOf("TownPanelLayer");
                GD.Print($"M5_MENU_OVER_TOWN menu_layer={LayerOf("GameMenuLayer")} town_layer={LayerOf("TownPanelLayer")} "
                    + $"menu_above={above} both_visible={mm.Visible && FindTownPanel()!.Visible}");
                EnterSettle(Step.OverTownShot, 3.0); // the OVERLAP PAINT must settle before the shot (the S4 law)
                break;
            case Step.OverTownShot:
                Shot("menu-over-town"); // [03] the menu dim OVER the open pedlar rows
                ClickButton(FindGameMenu()!, GameMenuLogic.ResumeButton); // RESUME: the real hide
                Enter(Step.ResumeWait, 5.0);
                break;
            case Step.ResumeWait:
                if (FindGameMenu()?.Visible != false) return;
                GD.Print($"M5_MENU_RESUME menu_hidden=True town_stays_open={FindTownPanel()?.Visible == true}"); // the overlap reading, read back
                EnterSettle(Step.ResumeShot, 3.0); // the town panel must be PAINTED open before the shot
                break;
            case Step.ResumeShot:
                Shot("resumed-town-open"); // [04] town panel still open underneath — the menu never closed it
                KeyPress(Key.T); // close the pedlar — the quit row starts from a plain field
                Enter(Step.TownClose, 4.0);
                break;
            case Step.TownClose:
                if (FindTownPanel()?.Visible != false) return;
                string path = GameContext.SavePath();
                try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } // the evidence-side file op
                catch (System.IO.IOException e)
                { Fail($"stale save file at {path} could not be removed — the door write would be unprovable ({e.Message})"); return; }
                _saveBytesBefore = FileAccess.FileExists(path) ? 1 : 0; // must read 0: the click must CREATE the file
                Callable.From(() =>
                {
                    var tree = GetTree(); // CAPTURED before the remove — a detached node has no tree
                    GetParent().RemoveChild(this);
                    tree.Root.AddChild(this);
                }).CallDeferred();
                // OUTLIVE the scene change (the engine law: a node WITH a parent is
                // refused — remove first, then add; deferred so neither call rides a
                // tree-processing pass). Boot+Act free with the old scene — Root's
                // child survives to read the NEW CurrentScene by name.
                Enter(Step.Reparented, 3.0);
                break;
            case Step.Reparented:
                if (GetParent() != GetTree().Root) return;
                KeyPress(Key.Escape); // the menu again (Act still stands under Main)
                Enter(Step.QuitOpen, 4.0);
                break;
            case Step.QuitOpen:
                if (FindGameMenu()?.Visible != true) return;
                ClickButton(FindGameMenu()!, GameMenuLogic.SaveQuitButton); // GameMenuLogic order: save door, THEN scene
                Enter(Step.QuitClick, 5.0);
                break;
            case Step.QuitClick:
                Enter(Step.SceneWait, 8.0);
                break;
            case Step.SceneWait:
                string scene = GetTree().CurrentScene?.GetName() ?? "none";
                if (scene != "MainMenu") return; // the scene-tree name STATE READ
                string savePath = GameContext.SavePath();
                if (!FileAccess.FileExists(savePath)) return; // the save door landed FIRST — bytes on disk
                long size = new System.IO.FileInfo(savePath).Length;
                if (size <= 0 || _saveBytesBefore != 0) return; // the removed-file proof: the door CREATED these bytes
                GD.Print($"M5_MENU_QUIT scene={scene} save_path={savePath} save_bytes={size} save_first_then_scene=GameMenuLogic-order");
                EnterSettle(Step.QuitShot, 3.0); // the MAIN MENU must PAINT before the shot
                break;
            case Step.QuitShot:
                Shot("main-menu-reached"); // [05] the title card after SAVE & QUIT
                Enter(Step.Finish, 1.0);
                break;
            case Step.Finish:
                // BABA_LOG UNAFFECTED (the DoD's stated reading): the menu presses open
                // NO sim door — RESUME rides none, SAVE & QUIT rides the file save +
                // scene flow (engine-side, exactly the F5 door, which records no line
                // either). Every line this session moved: the continue head, the death
                // menu's own RESPAWN, and the S3 windowed clock's rest feeds — NONE
                // attributable to the menu. foreign>0 would say otherwise.
                string? logPath = System.Environment.GetEnvironmentVariable(CommandLog.EnvVar);
                if (logPath is not null)
                {
                    var lines = System.IO.File.ReadAllLines(logPath);
                    int rest = 0, respawn = 0, head = 0, foreign = 0;
                    foreach (var l in lines)
                    {
                        var t = l.Trim();
                        if (t == $"# continue-from-save {GameContext.SavePath()}") head++;
                        else if (t == "respawn") respawn++;
                        else if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^rest \d+$")) rest++;
                        else foreign++;
                    }
                    GD.Print($"M5_MENU_LOG lines={lines.Length} continue_head={head} respawn_line={respawn} "
                        + $"clock_rest_lines={rest} menu_attributable={foreign}");
                }
                GD.Print($"M5_MENU_LEG_OK shots={_shots}");
                GetTree().Quit(0);
                break;
        }
    }

    // ---- the leg's tiny tools live in the GameMenuLeg.Tools.cs partial twin ----

    private void Enter(Step step, double seconds)
    {
        _step = step;
        _deadline = Time.GetTicksMsec() / 1000.0 + seconds;
    }

    /// <summary>A PAINT settle: the step waits until _settle (present lag) and
    /// still fails loudly at _deadline (settle + 6s slack — the S4 law).</summary>
    private void EnterSettle(Step step, double seconds)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        _step = step;
        _settle = now + seconds;
        _deadline = _settle + 6.0;
    }

    private void Fail(string why)
    {
        GD.PrintErr($"M5_MENU_LEG_FAIL {_step}: {why}");
        GetTree().Quit(1);
    }
}
