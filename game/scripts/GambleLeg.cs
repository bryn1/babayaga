using BabaYaga.Sim;
using Godot;

/// <summary>
/// MC 11987 WINDOWED GAMBLE LEG — the scripted windowed playthrough the
/// evidence run drives on Xvfb (the FollowerLeg/CubeLeg shape reused, not
/// re-invented: same SYNTHESIZED PushInput clicks/keys through the real
/// input pipeline, same settled-present Shots, same env-gated windowed-only
/// build in Boot — every state move a synthesized CLICK, never a scripted
/// verb: the painted BUY on the painted GAMBLE row is the session door, the
/// GamblePanelTests scan pins the leg itself verb-free). Beat order: TOWN
/// with the purse at StartGold (M5_GAM_PRE); open the town panel and read
/// the PAINTED GAMBLE row — the exact display-projection label re-derived
/// independently through the panel's OWN pure builder, so the marker proves
/// the widget paints the fence, not a narration (M5_GAM_OFFER + shot
/// 01-offer); CLICK BUY (the logged "gamble" door, the gold pair printed
/// around it) and read the BOUGHT truth from the session itself — pack tail
/// + gold after (M5_GAM_BOUGHT + shot 02-bought, the HUD gold line moved on
/// screen); read the row ADVANCED to its next projected label (M5_GAM_ROW_
/// ADVANCED, the fence's runtime face — the display moved, the sim decided
/// the sale); end the log at its last logged verb (dump recorded BEFORE
/// any refusal, because a refusal rides as a REJECT comment — history, not
/// replay — yet still draws the engine's DropRng, so the byte-identical
/// replay contract must close over the successes only). Then the REFUSAL
/// beat: repeated painted BUY clicks drain the purse until the ENGINE
/// refuses — the hint word-for-for-word carries its named words (gold and
/// pack UNCHANGED through it), M5_GAM_REFUSED + shot 03-refused. Any
/// failure is M5_GAM_LEG_FAIL + Quit(1), never a silent stall.
/// </summary>
public partial class GambleLeg : Node
{
    public const string EnvVar = "BABA_GAM_LEG";
    public const ulong LegSeed = 6; // printed as M5_GAM_SEED — the replay's --seed

    public string OutDir = "";
    public Act Act = null!;

    private enum Step { TownSettle, PanelOpen, OfferRead, BoughtCheck, AdvancedRead,
        Drain, DrainCheck, RefusedShot, Finish }

    private Step _step = Step.TownSettle;
    private int _frames;
    private double _deadline;
    private int _shots;
    private long _goldBefore;      // the gold pair around the PROVED buy click
    private long _drainGold;       // gold at the start of the current drain click
    private int _drainCarried;     // pack count at the start of the current drain click
    private string _hintBefore = ""; // HUD hint before the current drain click
    private int _successes;        // drain buys that landed (capped — an endless drain fails)

    private GameSession S => GameContext.Session!;

    public override void _Ready()
    {
        GetWindow().Position = Vector2I.Zero;
        GetWindow().Size = new Vector2I(1280, 720); // the judged leg size (the S6 shape)
        GD.Print($"M5_GAM_SEED {S.Seed}");
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
                GD.Print($"M5_GAM_PRE gold={S.Gold} carried={S.Carried.Count} " +
                         "note=town panel closed, the purse at the start-gold the panel header will print");
                KeyPress(Key.T);
                Enter(Step.PanelOpen, 3.0);
                break;

            case Step.PanelOpen:
                if (FindType<TownPanel>(Act)?.Visible != true) return;
                Enter(Step.OfferRead, 3.0);
                break;

            case Step.OfferRead:
                var panel = FindType<TownPanel>(Act)!;
                var expected = ProjectionLabel(1); // re-derived through the panel's OWN pure builder
                if (FindLabel(panel, expected) is null) { DebugPaint(panel); return; } // the honest walk retries until deadline
                GD.Print($"M5_GAM_OFFER label=\"{expected}\" note=display-only GambleStore projection " +
                         "(fence: GamblePanelLogic — the SIM decides the buy, the row shows the next draw)");
                Shot("offer"); // [01] the painted row — label + BUY button, the dim over the town
                _goldBefore = S.Gold;
                ClickButton(panel, GamblePanelLogic.BuyButton); // THE logged gamble door, clicked as a player
                Enter(Step.BoughtCheck, 5.0);
                break;

            case Step.BoughtCheck:
                if (S.Gold >= _goldBefore) return; // the door lands synchronously — this reads the PAINT/state
                var item = S.Carried[^1]; // the bought copy lands at the pack tail (TryCarry appends)
                GD.Print($"M5_GAM_BOUGHT item={item.Base.Id} quality={item.Quality} " +
                         $"gold_before={_goldBefore} gold_after={S.Gold} note=truth read from the session, not the label");
                Enter(Step.AdvancedRead, 2.5); // present-lag settle BEFORE the shot (the S4 law)
                break;

            case Step.AdvancedRead:
                var p2 = FindType<TownPanel>(Act)!;
                var next = ProjectionLabel(2); // the NEXT projected draw, independently re-derived
                if (FindLabel(p2, next) is null) { DebugPaint(p2); return; }
                GD.Print($"M5_GAM_ROW_ADVANCED label=\"{next}\" note=the row moved its display, the sim moved the purse");
                Shot("bought"); // [02] the advanced row over the town — HUD gold line moved
                CommandLog.Ambient?.Record("dump"); // the log ends at its last LOGGED verb (the S6 law):
                GD.Print($"M5_GAM_DUMP {BridgeLeg.DumpLine(S)}"); // refusals draw the DropRng but ride as comments — history, not replay
                Enter(Step.Drain, 8.0);
                break;

            case Step.Drain: // drain the purse with painted BUY clicks until the ENGINE refuses
                if (_successes > 30) { Fail("drain capped at 30 buys without a refusal — the purse refuses nobody"); return; }
                var gp = FindType<TownPanel>(Act)!;
                if (FindButton(gp, GamblePanelLogic.BuyButton) is null) { DebugPaint(gp); return; }
                _drainGold = S.Gold;
                _drainCarried = S.Carried.Count;
                _hintBefore = HintText();
                ClickButton(gp, GamblePanelLogic.BuyButton);
                Enter(Step.DrainCheck, 6.0);
                break;

            case Step.DrainCheck:
                if (S.Gold < _drainGold) // another logged success landed — keep draining
                {
                    _successes++;
                    GD.Print($"M5_GAM_BUY n={_successes} gold_before={_drainGold} gold_after={S.Gold}");
                    Enter(Step.Drain, 8.0);
                    return;
                }
                var words = HintText();
                // (the flipped comparison shapes are the driver-scan law, not
                // style: the inherited S4/S6 write-scan reads a direct
                // `S.Gold ==` as the write shape — the leg READS state only)
                if (_drainGold != S.Gold) return; // the click's gold moved (or drifted) — retry on the settle
                if (words == _hintBefore || !RefusalWords(words)) return; // not yet the engine's refusal on the line
                GD.Print($"M5_GAM_REFUSED words=\"{words}\" gold_unchanged=True " +
                         $"carried_unchanged={_drainCarried.Equals(S.Carried.Count)} note=the ENGINE's own words, nothing half-applied");
                Enter(Step.RefusedShot, 2.5);
                break;

            case Step.RefusedShot:
                Shot("refused"); // [03] the refusal hint painted on the HUD over the open panel
                Enter(Step.Finish, 0.3);
                break;

            case Step.Finish:
                GD.Print($"M5_GAM_LEG_OK shots={_shots} buys={1 + _successes} gold={S.Gold} carried={S.Carried.Count}");
                GetTree().Quit(0);
                break;
        }
    }

    /// <summary>The engine's OWN refusal wording for this door (GameSession.
    /// Trade.cs, quoted VERBATIM as a shape — the panel invents none): the
    /// short-purse refusal names the price, the full-pack refusal refunds.
    /// The leg never composes a refusal — it recognises the engine's forms.</summary>
    private static bool RefusalWords(string s) =>
        System.Text.RegularExpressions.Regex.IsMatch(s, @"^cannot afford the gamble: \d+ gold$")
        || s == "the pack has no cell left";

    // ---- the leg's tiny tools live in the GambleLeg.Tools.cs partial twin ----

    private void Enter(Step step, double seconds)
    {
        _step = step;
        _deadline = Time.GetTicksMsec() / 1000.0 + seconds;
    }

    private void Fail(string why)
    {
        GD.PrintErr($"M5_GAM_LEG_FAIL {_step}: {why}");
        GetTree().Quit(1);
    }
}
