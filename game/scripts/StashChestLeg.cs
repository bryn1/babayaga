using System;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// M5 TOWN-CHEST LEG (MC 11953, DoD 2): the scripted windowed playthrough the
/// evidence run drives on Xvfb — the EquipCellLeg/S6 rig reused, not re-
/// invented (same PushInput clicks, same settled-present Shots, same env-gated
/// windowed-only build in Boot). Every state move is a SYNTHESIZED CLICK
/// landing on a real call site: the TownPanel's BUY Soluga button (put an item
/// in the pack), the chest panel opened with the C key (the declared reading —
/// no chest sprite stands in the world yet, so the key IS the town-interaction
/// path, the town_panel idiom), then the PAINTED anchors of the chest panel
/// (a pack anchor click STORES, a tab anchor click TAKES — each rides ONE
/// logged CLI-grammar door line), then the LOCKED tab button (its click calls
/// no door and paints the engine-pinned words). The driver reads session/
/// scene state and NEVER calls a sim verb (the StashPanelTests scan rows pin
/// that law over these two files). Sequence: buy → chest open (M5_STASH_OPEN)
/// → store (pack cell empties, the tab row appears — the SHOT PAIR, states
/// hashed by the runner) → take back (pack refilled) → locked tab (HUD hint,
/// selection unchanged) → dump + quit. SHOT steps wait a real SETTLE SPAN
/// (present lag ~2-3s under Xvfb/llvmpipe, the S4/S6 law); state steps gate
/// on the session's own truth with a timeout.
/// Env-gated (BABA_STASH_LEG names the output dir), windowed-only — unset,
/// Boot never even builds this node, and the story anchors stay byte-identical.
/// </summary>
public partial class StashChestLeg : Node
{
    public const string EnvVar = "BABA_STASH_LEG";
    public const ulong LegSeed = 3; // printed as M5_STASH_SEED — the replay's --seed

    public string OutDir = "";
    public Act Act = null!;

    private enum Step { TownSettle, TownShot, PanelOpen, BuyWait, ClosePanel,
        ChestOpen, ChestShot, ClickStore, StoreWait, SettleAfterStore, StoreAfterShot,
        ClickTake, TakeWait, SettleTake, TakeShot, ClickLocked, LockedWait, LockedShot,
        Finish }

    private Step _step = Step.TownSettle;
    private int _frames;
    private double _deadline; // the state-step timeout (the S4 leg's law)
    private double _settle;   // the shot-step paint wait (EnterSettle only)
    private int _shots;
    private long _goldBefore;

    private GameSession S => GameContext.Session!;

    public override void _Ready()
    {
        GetWindow().Position = Vector2I.Zero;
        GetWindow().Size = new Vector2I(1280, 720); // the judged leg size (plan §1)
        GD.Print($"M5_STASH_SEED {S.Seed}");
        Enter(Step.TownSettle, 1.5); // resize + first frames settle before pixels are read
    }

    public override void _Process(double delta)
    {
        _frames++;
        if (OutDir.Length == 0) { Fail("OutDir unset"); return; }
        double now = Time.GetTicksMsec() / 1000.0;
        if (now > _deadline) { Fail($"{_step} timed out"); return; } // timeout FIRST (the S4 law)
        switch (_step)
        {
            case Step.TownSettle:
                if (_frames < 12) return; // the readback answers EMPTY before the first presents
                if (S.CurrentArea != "grodzisko") { Fail($"leg must start in town, area={S.CurrentArea}"); return; }
                EnterSettle(Step.TownShot, 1.5);
                break;
            case Step.TownShot:
                if (now < _settle) return;
                Shot("town"); // [01] the plain town frame
                KeyPress(Key.T); // the pedlar pays for the stored copy first
                Enter(Step.PanelOpen, 3.0);
                break;
            case Step.PanelOpen:
                if (FindTownPanel()?.Visible != true) return; // the RESIZED-settle must land before the click
                ClickButton(FindTownPanel()!, BuyLabelOf("soluga"));
                Enter(Step.BuyWait, 4.0);
                break;
            case Step.BuyWait:
                if (S.Carried.Count < 1) return;
                GD.Print($"M5_STASH_BUY gold_before={_goldBefore} gold_after={S.Gold} base=soluga");
                KeyPress(Key.T); // close the pedlar — the chest opens next
                Enter(Step.ClosePanel, 2.0);
                break;
            case Step.ClosePanel:
                if (FindTownPanel()?.Visible != false) return;
                KeyPress(Key.C); // THE chest key (town-gated in Act — the declared reading)
                Enter(Step.ChestOpen, 3.0);
                break;
            case Step.ChestOpen:
                var p = FindChest();
                if (p?.Visible != true) return;
                if (FindButtonByText(p, StashPanelLogic.TabButtonLabel(1)) is null) return;
                if (FindButtonByText(p, StashPanelLogic.LockedTabLabel(2)) is null) return;
                if (FindCell(p, StashPanelLogic.PackAnchorName(0, 0)) is null) return; // the pack anchor PAINTED
                GD.Print($"M5_STASH_OPEN tabs=[{string.Join(",", StashPanelLogic.TabsShown(S.StashTabs))}] "
                    + $"tab1_unlocked={S.IsStashTabUnlocked(1)} tab2_unlocked={S.IsStashTabUnlocked(2)} "
                    + $"selected={p.SelectedTab}");
                EnterSettle(Step.ChestShot, 3.0); // the freshly-laid chest must PAINT
                break;
            case Step.ChestShot:
                if (now < _settle) return;
                Shot("chest-open"); // [02] BEFORE half of the store pair: the pack anchor stands, tab 1 empty
                ClickCell(FindChest()!, StashPanelLogic.PackAnchorName(0, 0)); // the STORE door click
                Enter(Step.ClickStore, 5.0);
                break;
            case Step.ClickStore:
                Enter(Step.StoreWait, 4.0);
                break;
            case Step.StoreWait:
                if (S.Carried.Count != 0) return; // the pack cell emptied (the session's own truth)
                var stowed = StashPanelLogic.TabItems(S.Dump(), 1);
                if (stowed.Count != 1) return;   // the tab row appeared — read, never assumed
                GD.Print($"M5_STASH_STORE item={stowed[0].Base} tab=1 pack_cell=0,0 tab_cell={stowed[0].Row},{stowed[0].Col} "
                    + $"carried_after={S.Carried.Count}");
                EnterSettle(Step.SettleAfterStore, 3.0); // the repainted chest must PAINT (the S4 law)
                break;
            case Step.SettleAfterStore:
                if (now < _settle) return;
                EnterSettle(Step.StoreAfterShot, 0.001); // the paint wait already elapsed — read the frame
                break;
            case Step.StoreAfterShot:
                Shot("store-after"); // [03] AFTER half: the tab row painted, the pack cell free
                ClickCell(FindChest()!, StashPanelLogic.TabAnchorName(0, 0)); // the TAKE door click
                Enter(Step.ClickTake, 5.0);
                break;
            case Step.ClickTake:
                Enter(Step.TakeWait, 4.0);
                break;
            case Step.TakeWait:
                if (S.Carried.Count != 1) return; // the pack refilled (TryCarry, the one door)
                var back = StashPanelLogic.CarriedItems(S.Dump());
                if (back.Count != 1) return;
                GD.Print($"M5_STASH_TAKE item={back[0].Base} tab=1 tab_cell=0,0 inv_cell={back[0].Row},{back[0].Col} "
                    + $"carried={S.Carried.Count}");
                EnterSettle(Step.SettleTake, 3.0);
                break;
            case Step.SettleTake:
                if (now < _settle) return;
                EnterSettle(Step.TakeShot, 0.001);
                break;
            case Step.TakeShot:
                Shot("take-back"); // [04] the pack anchor painted again, the tab row gone
                ClickButton(FindChest()!, StashPanelLogic.LockedTabLabel(2)); // the NEVER-BYPASS row
                Enter(Step.ClickLocked, 5.0);
                break;
            case Step.ClickLocked:
                Enter(Step.LockedWait, 3.0);
                break;
            case Step.LockedWait:
                string hint = HintText();
                if (hint != StashPanelLogic.LockedTabHint(2)) return; // the hint line reads the pinned words
                GD.Print($"M5_STASH_LOCKED tab=2 unlocked={S.IsStashTabUnlocked(2)} "
                    + $"selected_after={FindChest()!.SelectedTab} hint=\"{hint}\"");
                EnterSettle(Step.LockedShot, 2.0);
                break;
            case Step.LockedShot:
                if (now < _settle) return;
                Shot("locked-hint"); // [05] the HUD line carrying the engine's own refusal words
                Enter(Step.Finish, 0.5);
                break;
            case Step.Finish:
                CommandLog.Ambient?.Record("dump"); // the log ends on its own dump command
                GD.Print($"M5_STASH_DUMP {BridgeLeg.DumpLine(S)}");
                GD.Print($"M5_STASH_LEG_OK shots={_shots} gold={S.Gold} carried={S.Carried.Count} "
                    + $"stash_tabs={S.StashTabs}");
                GetTree().Quit(0);
                break;
        }
    }

    // ---- the leg's tiny tools live in the StashChestLeg.Tools.cs partial twin ----

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
        GD.PrintErr($"M5_STASH_LEG_FAIL {_step}: {why}");
        GetTree().Quit(1);
    }
}
