using System;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// M5 HERO-EQUIP LEG (MC 11952, DoD 3): the scripted windowed playthrough the
/// evidence run drives on Xvfb — the S6 rig reused, not re-invented (same
/// PushInput clicks, same settled-present Shots, same env-gated windowed-only
/// build in Boot). Every state move is a SYNTHESIZED CLICK landing on a real
/// call site: the TownPanel's own BUY Soluga button (put an item in the pack),
/// then the PAINTED anchor cell of the inventory panel (click-to-wear — the
/// declared control-scheme translation: D2 drags with a cursor, this clone has
/// NO cursor state, the cell click rides the SAME logged EquipFromCell door
/// the CLI twin speaks). The driver reads session/scene state and NEVER calls
/// a sim verb (the HeroEquipCellTests scan rows pin that law over these two
/// files). Sequence: buy → open pack (shot BEFORE: the thatch cell, the empty
/// Worn list) → click the thatch (Worn grows, the cell empties — shot AFTER:
/// the plain-base equip pair; no +life affix exists on any vendor row, so the
/// HUD max-life pair is NOT painted — the honest choice stated in the report)
/// → buy again → click again → the EXCHANGE echo (M5_EQX_OK: the displaced
/// first Soluga is back in the pack, session-read by identity, §6.7b T2).
/// SHOT STEPS wait a real SETTLE SPAN (EnterSettle) before reading the
/// viewport — present lag under Xvfb/llvmpipe is ~2-3s (the S4/S6 law); state
/// steps only gate on the session's own truth with a timeout.
/// Env-gated (BABA_EQ_LEG names the output dir), windowed-only — unset, Boot
/// never even builds this node, and the story anchors stay byte-identical.
/// </summary>
public partial class EquipCellLeg : Node
{
    public const string EnvVar = "BABA_EQ_LEG";
    public const ulong LegSeed = 2; // printed as M5_EQ_SEED — the replay's --seed

    public string OutDir = "";
    public Act Act = null!;

    private enum Step { TownSettle, TownShot, PanelOpen, BuyWait, ClosePanel,
        InvOpen, InvSettle, ShotBefore, ClickWear, WearWait, SettleAfter, ShotAfter,
        CloseInv, PanelOpen2, BuyWait2, ClosePanel2, InvOpen2, InvSettle2, ClickXchg,
        XchgWait, SettleXchg, ShotXchg, Finish }

    private Step _step = Step.TownSettle;
    private int _frames;
    private double _deadline; // the state-step timeout (the S4 leg's law)
    private double _settle;   // the shot-step paint wait (EnterSettle only)
    private int _shots;
    private long _goldBefore;
    private Item _first = null!, _second = null!;

    private GameSession S => GameContext.Session!;

    public override void _Ready()
    {
        GetWindow().Position = Vector2I.Zero;
        GetWindow().Size = new Vector2I(1280, 720); // the judged leg size (plan §1)
        GD.Print($"M5_EQ_SEED {S.Seed}");
        Enter(Step.TownSettle, 1.5); // resize + first frames settle before pixels are read
    }

    public override void _Process(double delta)
    {
        _frames++;
        if (OutDir.Length == 0) { Fail("OutDir unset"); return; }
        double now = Time.GetTicksMsec() / 1000.0;
        if (now > _deadline) { Fail($"{_step} timed out"); return; } // timeout FIRST (the S4 leg's law)
        switch (_step)
        {
            case Step.TownSettle:
                if (_frames < 12) return; // the readback answers EMPTY before the first presents
                if (S.CurrentArea != "grodzisko") { Fail($"leg must start in town, area={S.CurrentArea}"); return; }
                EnterSettle(Step.TownShot, 1.5);
                break;
            case Step.TownShot:
                if (now < _settle) return;
                Shot("town"); // [01] the plain town frame (Gold 1000)
                KeyPress(Key.T);
                Enter(Step.PanelOpen, 3.0);
                break;
            case Step.PanelOpen:
                if (FindPanel()?.Visible != true) return; // the RESIZED-settle must land before the click
                ClickButton(FindPanel()!, BuyLabelOf("soluga"));
                Enter(Step.BuyWait, 4.0);
                break;
            case Step.BuyWait:
                if (S.Carried.Count < 1) return;
                _first = S.Carried[^1]; // the session's own tail — read, never assumed
                GD.Print($"M5_EQ_BUY gold_before={_goldBefore} gold_after={S.Gold} base={_first.Base.Id}");
                KeyPress(Key.T); // close the town panel — the pack panel opens next
                Enter(Step.ClosePanel, 2.0);
                break;
            case Step.ClosePanel:
                if (FindPanel()?.Visible != false) return;
                KeyPress(Key.Tab);
                Enter(Step.InvOpen, 3.0);
                break;
            case Step.InvOpen:
                if (FindInvPanel()?.Visible != true) return; // the D2-fixed panel must PAINT first
                if (FindCell("cell_0_0") is null) return;   // the thatch anchor is the painted truth
                EnterSettle(Step.InvSettle, 3.0);
                break;
            case Step.InvSettle:
                if (now < _settle) return;
                EnterSettle(Step.ShotBefore, 0.001); // the paint wait already elapsed — read the frame
                break;
            case Step.ShotBefore:
                Shot("inv-before"); // [02] BEFORE: the thatch cell filled, the Worn list empty
                ClickNode(FindCell("cell_0_0")!); // the click-to-wear: the panel's OWN click site
                Enter(Step.ClickWear, 5.0);
                break;
            case Step.ClickWear:
                Enter(Step.WearWait, 4.0);
                break;
            case Step.WearWait:
                if (!WearsFirst()) return; // the door lands synchronously — this reads the truth
                GD.Print($"M5_EQ_OK equip=soluga slot=Weapon cell=0,0 worn=1 pack={S.Carried.Count}");
                EnterSettle(Step.SettleAfter, 3.0); // the repainted panel must PAINT (the S4 law)
                break;
            case Step.SettleAfter:
                if (now < _settle) return;
                EnterSettle(Step.ShotAfter, 0.001);
                break;
            case Step.ShotAfter:
                Shot("inv-after"); // [03] AFTER: the worn cell empty, the Worn list reads Weapon: Soluga
                KeyPress(Key.Tab); // close the pack FIRST — its cells sit above the town panel (layer 22 > 21)
                Enter(Step.CloseInv, 2.0);
                break;
            case Step.CloseInv:
                if (FindInvPanel()?.Visible != false) return;
                KeyPress(Key.T); // exchange leg: buy the second Soluga at the pedlar
                Enter(Step.PanelOpen2, 3.0);
                break;
            case Step.PanelOpen2:
                if (FindPanel()?.Visible != true) return;
                ClickButton(FindPanel()!, BuyLabelOf("soluga"));
                Enter(Step.BuyWait2, 4.0);
                break;
            case Step.BuyWait2:
                if (S.Carried.Count < 1) return;
                _second = S.Carried[^1];
                GD.Print($"M5_EQX_BUY gold_after={S.Gold} base={_second.Base.Id}");
                KeyPress(Key.T);
                Enter(Step.ClosePanel2, 2.0);
                break;
            case Step.ClosePanel2:
                if (FindPanel()?.Visible != false) return;
                KeyPress(Key.Tab); // re-open the pack — the second Soluga is anchored there
                Enter(Step.InvOpen2, 3.0);
                break;
            case Step.InvOpen2:
                if (FindInvPanel()?.Visible != true) return;
                if (FindCell("cell_0_0") is null) return; // anchored 0,0 again (the pack emptied first)
                EnterSettle(Step.InvSettle2, 1.5);
                break;
            case Step.InvSettle2:
                if (now < _settle) return;
                ClickNode(FindCell("cell_0_0")!); // the EXCHANGE click — §6.7b T2 through the same door
                Enter(Step.ClickXchg, 5.0);
                break;
            case Step.ClickXchg:
                Enter(Step.XchgWait, 4.0);
                break;
            case Step.XchgWait:
                // Identity read (the session's own truth): the WEAPON slot holds the
                // second Soluga now and the FIRST one is back in the pack — the
                // exchange, pinned the way HirelingGearTests pins it.
                if (!ReferenceEquals(S.Equipment[EquipSlot.Weapon], _second) || S.Carried.Count != 1
                    || !ReferenceEquals(S.Carried[0], _first)) return;
                GD.Print($"M5_EQX_OK exchanged={_first.DisplayName}@0,0 worn={_second.DisplayName} pack={S.Carried.Count}");
                EnterSettle(Step.SettleXchg, 3.0);
                break;
            case Step.SettleXchg:
                if (now < _settle) return;
                EnterSettle(Step.ShotXchg, 0.001);
                break;
            case Step.ShotXchg:
                Shot("inv-exchange"); // [04] the displaced first Soluga painted back in the pack
                Enter(Step.Finish, 0.5);
                break;
            case Step.Finish:
                CommandLog.Ambient?.Record("dump"); // the log ends on its own dump command
                GD.Print($"M5_EQ_DUMP {BridgeLeg.DumpLine(S)}");
                GD.Print($"M5_EQ_LEG_OK shots={_shots} gold={S.Gold} worn={S.Equipment[EquipSlot.Weapon]?.DisplayName}");
                GetTree().Quit(0);
                break;
        }
    }

    private bool WearsFirst() =>
        S.Equipment.Equipped.Count == 1 && ReferenceEquals(S.Equipment[EquipSlot.Weapon], _first);

    // ---- the leg's tiny tools live in the EquipCellLeg.Tools.cs partial twin ----

    private void Enter(Step step, double seconds)
    {
        _step = step;
        _deadline = Time.GetTicksMsec() / 1000.0 + seconds;
    }

    /// <summary>A PAINT settle: the step waits until _settle (present lag) and
    /// still fails loudly at _deadline (settle + 6s slack — a stall names
    /// itself, the S4 law).</summary>
    private void EnterSettle(Step step, double seconds)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        _step = step;
        _settle = now + seconds;
        _deadline = _settle + 6.0;
    }

    private void Fail(string why)
    {
        GD.PrintErr($"M5_EQ_LEG_FAIL {_step}: {why}");
        GetTree().Quit(1);
    }
}
