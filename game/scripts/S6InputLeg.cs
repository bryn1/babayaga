using System;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// M5-S6 TOWN PANEL LEG (MC 11940, DoD 2): the scripted windowed playthrough
/// the evidence run drives on Xvfb — the S4 rig reused, not re-invented (same
/// PushInput clicks, same settled-present Shots, same env-gated windowed-only
/// build in Boot). Every state move is a SYNTHESIZED CLICK landing on a real
/// call site — the panel's own buttons (buy soluga → the gold pair; buy
/// napitek-zycia → THE windowed AddToBelt caller; HIRE/DISMISS at the
/// captain) — so the printed markers prove the widget, not a scripted verb.
/// The driver reads session/scene state and NEVER calls a sim verb (the
/// TownPanelTests negative row scans these two files). Then: close, walk out,
/// let one ponurak counter chip Life, and press 1 — the HUD's OWN belt hotkey
/// drinks the potion the panel just belted (M5_HEAL_OK, the Life-up pair).
/// Env-gated (BABA_S6_LEG names the output dir), windowed-only — unset, Boot
/// never even builds this node, and the story anchors stay byte-identical.
/// </summary>
public partial class S6InputLeg : Node
{
    public const string EnvVar = "BABA_S6_LEG";
    public const ulong LegSeed = 1; // printed as M5_S6_SEED — the replay's --seed

    public string OutDir = "";
    public Act Act = null!;

    private enum Step { TownSettle, TownShot, PanelOpen, PanelShot, BuySoluga, BuySolugaSettle,
        BuySolugaShot, BuyPotion, BeltCheck, BeltShot, HireCheck, HireShot, DismissCheck,
        DismissShot, ClosePanel, WalkOut, FindPrey, ChipAway, ChipSettle, ChipShot,
        HealCheck, HealShot, Finish }

    private Step _step = Step.TownSettle;
    private int _frames;
    private double _deadline;
    private int _shots;
    private long _goldBefore;
    private long _lifeBeforeHeal;
    private string _monPos = "";

    private GameSession S => GameContext.Session!;

    public override void _Ready()
    {
        GetWindow().Position = Vector2I.Zero;
        GetWindow().Size = new Vector2I(1280, 720); // the judged leg size (plan §1)
        GD.Print($"M5_S6_SEED {S.Seed}");
        Enter(Step.TownSettle, 1.5); // resize + first frames settle before pixels are read
    }

    public override void _Process(double delta)
    {
        _frames++;
        if (OutDir.Length == 0) { Fail("OutDir unset"); return; }
        if (Time.GetTicksMsec() / 1000.0 > _deadline) { Fail($"{_step} timed out"); return; } // deadline FIRST (the S4 leg's law)
        switch (_step)
        {
            case Step.TownSettle:
                if (_frames < 12) return; // the readback answers EMPTY before the first presents
                if (S.CurrentArea != "grodzisko") { Fail($"leg must start in town, area={S.CurrentArea}"); return; }
                Shot("town"); // [01] the Gold-1000 frame of the buy pair
                KeyPress(Key.T);
                Enter(Step.PanelOpen, 3.0);
                break;
            case Step.PanelOpen:
                if (FindPanel()?.Visible != true) return; // the RESIZED-settle must land before the shot
                Enter(Step.PanelShot, 2.5);
                break;
            case Step.PanelShot:
                Shot("town-panel"); // [02] Vesna + the captain, painted from the session tables
                ClickButton(FindPanel()!, BuyLabelOf("soluga"));
                Enter(Step.BuySoluga, 4.0);
                break;
            case Step.BuySoluga:
                if (S.Gold >= _goldBefore) return; // the door lands synchronously — this reads the PAINT
                GD.Print($"M5_BUY_OK buy=soluga gold_before={_goldBefore} gold_after={S.Gold}");
                Enter(Step.BuySolugaSettle, 3.0); // present-lag settle BEFORE the pair shot (the S4 law)
                break;
            case Step.BuySolugaSettle:
                Shot("buy-soluga"); // [03] the Gold-down frame of the pair
                ClickButton(FindPanel()!, BuyLabelOf("napitek-zycia"));
                Enter(Step.BeltCheck, 4.0);
                break;
            case Step.BeltCheck:
                var potion = S.Belt[0];
                if (potion is null || S.Gold >= _goldBefore) return;
                GD.Print($"M5_BELT_OK slot=1 base={potion.Base.Id} pack_index={S.Carried.Count - 1} " +
                         $"gold_before={_goldBefore} gold_after={S.Gold}");
                Enter(Step.BeltShot, 3.0);
                break;
            case Step.BeltShot:
                Shot("belt-filled"); // [04] the HUD Belt line reads 1:Napitek Zycia
                ClickButton(FindPanel()!, HireLabelOf("strzelczyni"));
                Enter(Step.HireCheck, 4.0);
                break;
            case Step.HireCheck:
                if (S.Merc?.Id != "strzelczyni" || S.Gold >= _goldBefore) return;
                GD.Print($"M5_HIRE_OK hire=strzelczyni gold_before={_goldBefore} gold_after={S.Gold} " +
                         $"merc_area={S.Merc.Area}");
                Enter(Step.HireShot, 3.0);
                break;
            case Step.HireShot:
                Shot("captain-hired"); // [05] the merc line replaced the offer rows (pull, painted)
                ClickButton(FindPanel()!, TownPanelLogic.DismissButton);
                Enter(Step.DismissCheck, 4.0);
                break;
            case Step.DismissCheck:
                if (S.Merc is not null) return;
                GD.Print($"M5_DISMISS_OK gold_kept={S.Gold} note=the LoD no-refund rule, the engine's");
                Enter(Step.DismissShot, 3.0);
                break;
            case Step.DismissShot:
                Shot("captain-dismissed"); // [06] the offer rows are back (the one-merc truth, pulled)
                KeyPress(Key.T); // close the panel before the world clicks resume
                Enter(Step.ClosePanel, 1.0);
                break;
            case Step.ClosePanel:
                if (FindPanel()?.Visible != false) return;
                Click(Act.Hero.CellToScreen(DoorCell()), MouseButton.Left); // walk out east
                Enter(Step.WalkOut, 60.0);
                break;
            case Step.WalkOut:
                if (S.CurrentArea != "mchy") return;
                Enter(Step.FindPrey, 3.0); // the rebuild settles before the hunt pick
                break;
            case Step.FindPrey:
                var deep = DeepCell() ?? throw new InvalidOperationException("no monster row stood in mchy");
                _monPos = $"{deep.Cell.X},{deep.Cell.Y}";
                Enter(Step.ChipAway, 60.0); // the walk-to-melee band plus a few counters
                break;
            case Step.ChipAway:
                if (S.IsDead) { Fail("the hero died before the heal row — the chip beat overshot"); return; }
                if (S.Character.Life < S.Character.MaxLife)
                {
                    _lifeBeforeHeal = S.Character.Life; // the chip the potion must undo
                    Enter(Step.ChipSettle, 2.5);
                    return;
                }
                ReclickMonster(); // every click rides the logged attack door (HeroController)
                break;
            case Step.ChipSettle:
                Shot("heal-before"); // [07] the chipped Life of the heal pair
                KeyPress(Key.Key1); // the HUD's OWN belt hotkey at the UsePotion door
                Enter(Step.HealCheck, 6.0);
                break;
            case Step.HealCheck:
                if (S.Character.Life <= _lifeBeforeHeal) return;
                GD.Print($"M5_HEAL_OK life {_lifeBeforeHeal}->{S.Character.Life} max={S.Character.MaxLife}");
                Enter(Step.HealShot, 4.0); // the healed HUD must PAINT (present lag ~2-3s)
                break;
            case Step.HealShot:
                Shot("heal-after"); // [08] the Life-up frame of the pair
                Enter(Step.Finish, 0.3);
                break;
            case Step.Finish:
                CommandLog.Ambient?.Record("dump"); // the log ends on its own dump command
                GD.Print($"M5_S6_DUMP {BridgeLeg.DumpLine(S)}");
                GD.Print($"M5_S6_LEG_OK shots={_shots} gold={S.Gold} life={S.Character.Life}");
                GetTree().Quit(0);
                break;
        }
    }

    // ---- the leg's tiny tools live in the S6InputLeg.Tools.cs partial twin ----

    private void Enter(Step step, double seconds)
    {
        _step = step;
        _deadline = Time.GetTicksMsec() / 1000.0 + seconds;
    }

    private void Fail(string why)
    {
        GD.PrintErr($"M5_S6_LEG_FAIL {_step}: {why}");
        GetTree().Quit(1);
    }
}
