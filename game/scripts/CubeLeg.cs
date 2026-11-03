using System;
using System.Linq;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// M5 RITUAL-CRAFT PANEL LEG (MC 11970, DoD 2): the scripted windowed
/// playthrough the evidence run drives on Xvfb — the S6 rig reused, not
/// re-invented (same PushInput clicks/keys, same settled-present Shots, same
/// env-gated windowed-only build in Boot). The brief's WINDOWED PROOF ships
/// BOTH states on ONE driver: BABA_CUBE_ROWS names the admitted-count this
/// process must show (the honesty gate — with the shipped zero-admit data the
/// EMPTY leg demands 0, and a FIXTURE leg whose row the engine skipped instead
/// of admitting FAILS its own marker on the spot, never silently painting a
/// lie). Every state move is a SYNTHESIZED CLICK/KEY landing on a real call
/// site — the B key through Act's own town-gated door, the BUY buttons of the
/// town panel, the painted CRAFT button — so the markers prove the widget, not
/// a scripted verb (the CubeLeg scan row pins it: no session verb, no state
/// write; the recipe, every label and the buy line are PULLED from the
/// session's own tables, no fixture literal lives here). Empty leg: open,
/// read the ONE honest line with ZERO buttons, close (the close works row).
/// Fixture leg: buy the recipe's inputs at the pedlar (the gold pair), open
/// the cube, read the exact row text back, click CRAFT, read hint + pack
/// (output landed). Both legs end on the dump line the CLI replays
/// byte-for-byte (the fixture leg's cmp is the DoD's replay law).
/// Env-gated (BABA_CUBE_LEG names the out dir), windowed-only — unset, Boot
/// never even builds this node, and the story anchors stay byte-identical.
/// </summary>
public partial class CubeLeg : Node
{
    public const string EnvVar = "BABA_CUBE_LEG";
    public const string DataEnvVar = "BABA_CUBE_DATA"; // fixture dataDir override (Boot reads it into GameContext)
    public const string RowsEnvVar = "BABA_CUBE_ROWS"; // the honesty gate: admitted count this leg must see
    public const ulong LegSeed = 5; // printed as M5_CUBE_SEED — the replay's --seed

    public string OutDir = "";
    public Act Act = null!;

    private enum Step { TownSettle, TownPanelOpen, Buy1, Buy2, BoughtShot, TownClosed,
        CubeOpen, CubeShot, AfterCraft, CubeClosed, Finish }

    private Step _step = Step.TownSettle;
    private int _frames;
    private double _deadline;
    private int _shots;
    private long _goldBefore;
    private bool _fixture;   // admitted 1 -> the PROVEN-CRAFT leg; 0 -> the honest-empty leg
    private CubeRecipeRow _r = null!; // pulled from the session — never a fixture literal

    private GameSession S => GameContext.Session!;

    public override void _Ready()
    {
        GetWindow().Position = Vector2I.Zero;
        GetWindow().Size = new Vector2I(1280, 720); // the judged leg size (the S6 shape)
        GD.Print($"M5_CUBE_SEED {S.Seed}");
        Enter(Step.TownSettle, 1.5); // resize + first frames settle before pixels are read
    }

    public override void _Process(double delta)
    {
        _frames++;
        if (OutDir.Length == 0) { Fail("OutDir unset"); return; }
        if (Time.GetTicksMsec() / 1000.0 > _deadline) { Fail($"{_step} timed out"); return; } // deadline FIRST (the S4 law)
        switch (_step)
        {
            case Step.TownSettle:
                if (_frames < 12) return; // the readback answers EMPTY before the first presents
                if (S.CurrentArea != "grodzisko") { Fail($"leg must start in town, area={S.CurrentArea}"); return; }
                if (!int.TryParse(System.Environment.GetEnvironmentVariable(RowsEnvVar), out var expected) || expected < 0)
                { Fail($"{RowsEnvVar} must name the admitted count this leg proves (0 empty / 1 fixture)"); return; }
                if (S.CubeRecipes.Recipes.Count != expected)
                {
                    // The honesty gate: a fixture the ENGINE skipped shows here as
                    // admitted=0 vs expected=1 and dies on its own marker (the
                    // brief's own rule — SkippedCount stays engine-side, the leg
                    // fails rather than faking a row).
                    Fail($"admission says {S.CubeRecipes.Recipes.Count}, the leg was told to prove {expected} " +
                         $"(skipped={S.CubeRecipes.SkippedCount}) — the engine skipped the fixture or the data dir lies");
                    return;
                }
                _fixture = expected == 1;
                if (_fixture) _r = S.CubeRecipes.Recipes[0]; // THE pulled row — ids/labels derive from it
                Shot("town"); // [01] the pre-panel frame
                if (_fixture) { KeyPress(Key.T); Enter(Step.TownPanelOpen, 3.0); } // buy the inputs at the pedlar first
                else { KeyPress(Key.B); Enter(Step.CubeOpen, 3.0); }
                break;

            case Step.TownPanelOpen: // the fixture leg's input run: TWO BUY clicks, the S6 idiom
                if (FindType<TownPanel>(Act)?.Visible != true) return;
                ClickButton(FindType<TownPanel>(Act)!, BuyLabelOf(_r.Inputs[0].ItemId));
                Enter(Step.Buy1, 4.0);
                break;
            case Step.Buy1:
                if (S.Gold >= _goldBefore) return; // the door lands synchronously — this reads the PAINT
                GD.Print($"M5_CUBE_BUY n=1 item={_r.Inputs[0].ItemId} gold_before={_goldBefore} gold_after={S.Gold}");
                ClickButton(FindType<TownPanel>(Act)!, BuyLabelOf(_r.Inputs[0].ItemId));
                Enter(Step.Buy2, 4.0);
                break;
            case Step.Buy2:
                if (S.Gold >= _goldBefore) return;
                GD.Print($"M5_CUBE_BUY n=2 item={_r.Inputs[0].ItemId} gold_before={_goldBefore} gold_after={S.Gold}");
                Enter(Step.BoughtShot, 3.0); // present-lag settle BEFORE the pair shot (the S4 law)
                break;
            case Step.BoughtShot:
                Shot("inputs-bought"); // [02] the pack holds the recipe's inputs (painted later — the gold proves it)
                KeyPress(Key.T); // close the town panel before the cube opens
                Enter(Step.TownClosed, 2.0);
                break;
            case Step.TownClosed:
                if (FindType<TownPanel>(Act)?.Visible != false) return;
                KeyPress(Key.B);
                Enter(Step.CubeOpen, 3.0);
                break;

            case Step.CubeOpen:
                if (FindType<CraftPanel>(Act)?.Visible != true) return; // the RESIZED-settle must land before the shot
                Enter(Step.CubeShot, 2.5);
                break;
            case Step.CubeShot:
                var panel = FindType<CraftPanel>(Act)!;
                var rows = PaintedRowTexts(panel);
                int crafts = CountButtons(panel, CraftPanelLogic.CraftButton);
                if (!_fixture)
                {
                    // THE EMPTY PROOF (the brief's leg A): the honest line is
                    // painted, exactly, and ZERO buttons ride it — never a fake
                    // row. The text is read from the painted Label, not assumed.
                    if (rows.Count != 1 || rows[0] != CraftPanelLogic.EmptyLine || crafts != 0)
                    {
                        DebugTexts(panel);
                        Fail($"empty state must be ONE honest line with 0 buttons, got rows={rows.Count} buttons={crafts}");
                        return;
                    }
                    GD.Print($"M5_CUBE_EMPTY line=\"{rows[0]}\" rows=1 craft_buttons=0 " +
                             $"admitted={S.CubeRecipes.Recipes.Count}");
                    Shot("cube-empty"); // [02] the empty cube, painted
                    KeyPress(Key.B); // close works — the same door closes it
                    Enter(Step.CubeClosed, 2.0);
                    return;
                }
                var expectedRow = CraftPanelLogic.RowLabel(_r, S.Bases); // pulled, not a literal
                if (!rows.Contains(expectedRow) || crafts != 1)
                {
                    DebugTexts(panel);
                    Fail($"fixture row \"{expectedRow}\" with exactly 1 CRAFT button not painted (buttons={crafts})");
                    return;
                }
                GD.Print($"M5_CUBE_OPEN row=\"{expectedRow}\" craft_buttons=1 " +
                         $"admitted={S.CubeRecipes.Recipes.Count}");
                Shot("cube-open"); // [03] the fixture row, painted
                ClickButton(panel, CraftPanelLogic.CraftButton); // THE door click — the pack holds the inputs
                Enter(Step.AfterCraft, 4.0);
                break;

            case Step.AfterCraft:
                var landed = S.Carried.Count(i => i.Base.Id == _r.OutputItemId);
                var inputsLeft = S.Carried.Count(i => i.Base.Id == _r.Inputs[0].ItemId);
                if (landed == 0 || inputsLeft != 0) return; // the door lands synchronously; this reads the state
                GD.Print($"M5_CUBE_CRAFT out={_r.OutputItemId} pack_lines={S.Carried.Count} inputs_left={inputsLeft} " +
                         $"hint=\"{HintText()}\""); // hint painted + output landed, ONE state line
                Shot("after-craft"); // [04] the pack's truth under the open cube
                Enter(Step.Finish, 0.3);
                break;

            case Step.CubeClosed:
                if (FindType<CraftPanel>(Act)?.Visible != false) return;
                GD.Print("M5_CUBE_CLOSE visible=False note=the-B-key-door-closes-what-it-opened");
                Shot("cube-closed"); // [03] the world un-dimmed — close works
                Enter(Step.Finish, 0.3);
                break;

            case Step.Finish:
                CommandLog.Ambient?.Record("dump"); // the log ends on its own dump command (the S6 law)
                GD.Print($"M5_CUBE_DUMP {BridgeLeg.DumpLine(S)}");
                GD.Print($"M5_CUBE_LEG_OK fixture={_fixture} shots={_shots} gold={S.Gold}");
                GetTree().Quit(0);
                break;
        }
    }

    // ---- the leg's tiny tools live in the CubeLeg.Tools.cs partial twin ----

    private void Enter(Step step, double seconds)
    {
        _step = step;
        _deadline = Time.GetTicksMsec() / 1000.0 + seconds;
    }

    private void Fail(string why)
    {
        GD.PrintErr($"M5_CUBE_LEG_FAIL {_step}: {why}");
        GetTree().Quit(1);
    }
}
