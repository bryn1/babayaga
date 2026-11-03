using BabaYaga.Sim;
using Godot;

/// <summary>
/// MC 11977 WINDOWED FOLLOWER LEG — the scripted windowed playthrough the
/// evidence run drives on Xvfb (the CubeLeg shape reused, not re-invented:
/// same SYNTHESIZED PushInput clicks/keys through the real input pipeline,
/// same settled-present Shots, same env-gated windowed-only build in Boot —
/// every state move a synthesized input, never a scripted verb: the painted
/// HIRE button, the painted DISMISS row, one WORLD click for the step).
/// Beat order: TOWN with no hire — the honest baseline, follower count 0
/// (M5_FOL_PRE); open the captain panel and CLICK HIRE on the strzelczyni
/// row (the existing logged hire door, the gold pair printed around it →
/// M5_FOL_HIRE with the follower-sprite state read beside it); read the
/// PAINTED portrait off the hired row — a TextureRect standing with the
/// shipped 128×128 source, not a narration (M5_FOL_PORTRAIT) — and SHOT the
/// captain panel with the portrait in it; close the panel, WALK the hero one
/// cell with a world click (the MC 11959 HUD click law live), and read the
/// follower sprite STANDING ADJACENT to the new hero cell (Chebyshev-1 via
/// the Sim's own WorldPos.DistanceTo, never the hero cell — the PickCell
/// law's runtime face) → M5_FOL_MOVED + the shot beside the hero; reopen the
/// panel, CLICK DISMISS → the sprite COUNT goes to zero (M5_FOL_GONE) + the
/// gone shot. The leg ends on the dump line the CLI replays byte-for-byte
/// (hire + dismiss are the logged session — the replay law). Prints NOTHING
/// headless (the branch never builds it there); any failure is
/// M5_FOL_LEG_FAIL + Quit(1), never a silent stall.
/// </summary>
public partial class FollowerLeg : Node
{
    public const string EnvVar = "BABA_FOL_LEG";
    public const ulong LegSeed = 6; // printed as M5_FOL_SEED — the replay's --seed

    public string OutDir = "";
    public Act Act = null!;

    private enum Step { TownSettle, PanelOpen, PanelShot, HireCheck, PortraitShot,
        PanelClosed, WalkSettle, MovedShot, Panel2, DismissCheck, GoneShot, Finish }

    private Step _step = Step.TownSettle;
    private int _frames;
    private double _deadline;
    private int _shots;
    private long _goldBefore;
    private Vector2I _target;

    private GameSession S => GameContext.Session!;

    public override void _Ready()
    {
        GetWindow().Position = Vector2I.Zero;
        GetWindow().Size = new Vector2I(1280, 720); // the judged leg size (the S6 shape)
        GD.Print($"M5_FOL_SEED {S.Seed}");
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
                if (Act.Followers.SpriteCount != 0) { Fail("no hire stands — the field must start with 0 sprites"); return; }
                GD.Print($"M5_FOL_PRE sprite_count=0 note=no hireling rides yet — the honest baseline");
                Shot("town"); // [01] the pre-hire town frame
                KeyPress(Key.T);
                Enter(Step.PanelOpen, 3.0);
                break;

            case Step.PanelOpen:
                if (FindType<TownPanel>(Act)?.Visible != true) return;
                Enter(Step.PanelShot, 2.5);
                break;

            case Step.PanelShot:
                Shot("captain-offer"); // [02] the offer rows — no portrait on an UNHIRED row
                ClickButton(FindType<TownPanel>(Act)!, HireLabel()); // THE logged hire door, clicked as a player
                Enter(Step.HireCheck, 4.0);
                break;

            case Step.HireCheck:
                if (S.Merc?.Id != "strzelczyni" || S.Gold >= _goldBefore) return;
                if (Act.Followers.SpriteCount != 1) return; // the field re-derives within one frame — WAIT for the PAINT, never narrate it
                GD.Print($"M5_FOL_HIRE hire=strzelczyni gold_before={_goldBefore} gold_after={S.Gold} " +
                         $"merc_alive=True follower_count=1 cell={CellText(Act.Followers.Cell)}");
                Enter(Step.PortraitShot, 2.5); // present-lag settle BEFORE the portrait shot (the S4 law)
                break;

            case Step.PortraitShot:
                var panel = FindType<TownPanel>(Act)!;
                var art = PaintedPortrait(panel);
                if (art is null) { DebugPaint(panel); Fail("the HIRED row paints no portrait TextureRect"); return; }
                if (art.GetWidth() != 128 || art.GetHeight() != 128)
                { DebugPaint(panel); Fail($"portrait source is {art.GetWidth()}x{art.GetHeight()}, the shipped twin is 128x128"); return; }
                GD.Print($"M5_FOL_PORTRAIT painted=True src=poi-strzelczyni-portrait size=128x128 " +
                         $"note=the hired row scaled to row height; unhired rows stay text-only (map law, xunit-pinned)");
                Shot("captain-portrait"); // [03] the captain panel SHOWING the portrait (the DoD's PORTRAIT shot)
                KeyPress(Key.T); // close the panel — the world click must reach the ground, not the overlay
                Enter(Step.PanelClosed, 2.5);
                break;

            case Step.PanelClosed:
                if (FindType<TownPanel>(Act)?.Visible != false) return;
                var hero = Act.Hero.Cell;
                var follower = Act.Followers.Cell;
                _target = PickWalkTarget(hero, follower);
                GD.Print($"M5_FOL_CLICK hero=({hero.X},{hero.Y}) follower={CellText(follower)} target=({_target.X},{_target.Y}) " +
                         "note=one LMB world click, the ClickMap walk door");
                Click(Act.Hero.CellToScreen(_target), MouseButton.Left);
                Enter(Step.WalkSettle, 60.0);
                break;

            case Step.WalkSettle:
                if (Act.Hero.IsWalking || Act.Hero.Cell != _target) return; // the step lands through the real walk clock
                var hcell = Act.Hero.Cell;
                var fcell = Act.Followers.Cell;
                if (Act.Followers.SpriteCount != 1 || fcell is not { } f)
                { Fail($"after the step the follower must stand beside the hero, count={Act.Followers.SpriteCount}"); return; }
                int cheb = new BabaYaga.Sim.WorldPos(hcell.X, hcell.Y).DistanceTo(new BabaYaga.Sim.WorldPos(f.X, f.Y));
                if (cheb != 1 || f == hcell)
                { Fail($"follower cell ({f.X},{f.Y}) not adjacent to hero ({hcell.X},{hcell.Y}) — Chebyshev {cheb}"); return; }
                GD.Print($"M5_FOL_MOVED hero=({hcell.X},{hcell.Y}) follower=({f.X},{f.Y}) adjacent=True " +
                         "chebyshev=1 follower_count=1 note=sim tracks NO follower position — PickCell re-derived at the new hero cell");
                Enter(Step.MovedShot, 2.5); // settle BEFORE the beside shot (present lag, the S4 law)
                break;

            case Step.MovedShot:
                Shot("follower-beside"); // [04] the sprite stands BESIDE the hero after the walk
                KeyPress(Key.T); // back to the captain for the dismiss row
                Enter(Step.Panel2, 3.0);
                break;

            case Step.Panel2:
                if (FindType<TownPanel>(Act)?.Visible != true) return;
                ClickButton(FindType<TownPanel>(Act)!, TownPanelLogic.DismissButton);
                Enter(Step.DismissCheck, 4.0);
                break;

            case Step.DismissCheck:
                if (S.Merc is not null) return; // the door lands synchronously; this reads the PAINT
                if (Act.Followers.SpriteCount != 0) return; // the QueueFree lands at frame end (the MonsterField lesson)
                GD.Print($"M5_FOL_GONE sprite_count=0 hero=({Act.Hero.Cell.X},{Act.Hero.Cell.Y}) " +
                         "note=dismissed stands nobody — the field freed its one sprite");
                Enter(Step.GoneShot, 2.5);
                break;

            case Step.GoneShot:
                Shot("follower-gone"); // [05] the hero alone again — count 0 read AND seen
                Enter(Step.Finish, 0.3);
                break;

            case Step.Finish:
                CommandLog.Ambient?.Record("dump"); // the log ends on its own dump command (the S6 law)
                GD.Print($"M5_FOL_DUMP {BridgeLeg.DumpLine(S)}");
                GD.Print($"M5_FOL_LEG_OK shots={_shots} gold={S.Gold} merc=None");
                GetTree().Quit(0);
                break;
        }
    }

    /// <summary>The deterministic step target: the first FREE neighbour cell
    /// around the hero in the PINNED ring order, skipping the follower's OWN
    /// cell (the move must visibly MOVE — the hero walks to a cell the sprite
    /// is not standing on, when one exists; an all-blocked ring fails the leg
    /// honestly, never fakes a step).</summary>
    private Vector2I PickWalkTarget(Vector2I hero, Vector2I? follower)
    {
        foreach (var (dx, dy) in FollowerFieldLogic.RingOrder)
        {
            var c = new Vector2I(hero.X + dx, hero.Y + dy);
            if (!Act.Tiles.IsWalkable(c)) continue;
            if (follower is { } f && f == c) continue;
            return c;
        }
        Fail("no walkable neighbour besides the follower cell — the leg cannot prove a step");
        return hero;
    }

    private static string CellText(Vector2I? c) => c is { } v ? $"({v.X},{v.Y})" : "none";

    private void Enter(Step step, double seconds)
    {
        _step = step;
        _deadline = Time.GetTicksMsec() / 1000.0 + seconds;
    }

    private void Fail(string why)
    {
        GD.PrintErr($"M5_FOL_LEG_FAIL {_step}: {why}");
        GetTree().Quit(1);
    }
}
