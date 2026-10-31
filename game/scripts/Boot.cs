using Godot;

/// <summary>
/// Entry scene script (res://Main.tscn root). Headless → the pinned M4 marker leg:
/// a scripted GameSession (seed 42, "koldun", res://data) + three REAL gold-moving
/// verbs (Buy / SellLast / Gamble), one SIM_TICK line per verb read FROM THE SESSION
/// OBJECT EVERY TICK; then ONE scenes/Act.tscn instantiate through the placeholder
/// pass (DA_SCENE_ACT_OK prints only after a NON-NULL instantiate — a null scene
/// goes to PushError and never prints the marker), and the retained Act rides the
/// M4-SLICE journey legs (JourneyLeg: HERO_STEP walk + one MONSTER_DEFEATED per
/// kind the data table spawns in mchy + the XP_EXPECT honesty leg + the M4-S4
/// level-up beat (LEVEL_UP on the runtime ladder threshold, HUD_LEVEL pulled from
/// the live Hud), bridge-only) → the M4-S2 vendor trade beat (TradeLeg: stone home to the
/// town, Buy + SellLast, every value read from the session) → the M4-S5 potion belt beat
/// (BeltLeg) → the M4-S6 DEATH BEAT (DeathLeg: real counter-damage kills the hero in
/// mchy, the DEAD session rejects Attack, the lit stones raise him in town with the
/// DeathPenalty paid through the Sim door — DEATH_OK) → ORCH_M4_OK → Quit(0).
/// Windowed → MainMenu.tscn. Zero rules live here (Game → Sim → nothing).
/// </summary>
public partial class Boot : Node
{
    public override void _Ready()
    {
        // The EXACT headless check (DisplayServer.Headless is NOT in the 4.7.2 bindings).
        if (DisplayServer.GetName() == "headless")
            RunMarkerLeg();
        else
            GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }

    /// <summary>The DoD leg 3/3b gate + the slice journey legs. The legs below
    /// perform NO waypoint verb after any travel (the WAYPOINT ORDER CONTRACT
    /// lights the DESTINATION stone BEFORE the travel — HeroController and
    /// JourneyLeg both honour it; discover-after-travel is nowhere).</summary>
    private void RunMarkerLeg()
    {
        var session = GameContext.New(42);

        // Three real gold-moving verbs from the §2 bridge surface; a dead session
        // cannot fake the three DISTINCT gold values the ticks below report.
        session.Buy("palica");
        PrintTick(1, session);
        session.SellLast();
        PrintTick(2, session);
        session.Gamble();
        PrintTick(3, session);

        // The slice legs ride the SAME instantiated Act the scene smoke produces.
        var act = InstantiateActSmoke();
        if (act is not null)
        {
            JourneyLeg.Run(session, act);
            TradeLeg.Run(session, act); // M4-S2: town vendor trade riding the journey's tail
            BeltLeg.Run(session, act); // M4-S5: potion belt leg riding the trade leg's town tail
            DeathLeg.Run(session, act); // M4-S6: death + stone-resurrection riding the belt leg's empty belt
        }

        GD.Print("ORCH_M4_OK");
        GetTree().Quit(0);
    }

    /// <summary>PINNED format: SIM_TICK n=&lt;int&gt; area=&lt;string&gt; gold=&lt;long&gt; level=&lt;int&gt;.
    /// area/gold/level are read from the session object on every call —
    /// session.CurrentArea / .Gold / .Level.</summary>
    private static void PrintTick(int n, BabaYaga.Sim.GameSession session) =>
        GD.Print($"SIM_TICK n={n} area={session.CurrentArea} gold={session.Gold} level={session.Level}");

    /// <summary>Scene-execution smoke (leg 3b): compile is not execution — this
    /// proves the scene scripts run headless through the placeholder pass
    /// (missing art = PushWarning, never a crash). The instance is RETAINED: the
    /// slice journey legs ride it; Quit(0) frees it with the tree.</summary>
    private Act? InstantiateActSmoke()
    {
        var packed = GD.Load<PackedScene>("res://scenes/Act.tscn");
        if (packed is null)
        {
            GD.PushError("scene-exec smoke: res://scenes/Act.tscn loaded NULL");
            return null; // no marker on a null scene — the gate's conditional, pinned
        }
        if (packed.Instantiate() is not Act act)
        {
            GD.PushError("scene-exec smoke: Act.tscn instantiated NULL");
            return null;
        }
        AddChild(act); // Act._Ready runs here — the placeholder pass executes headless
        GD.Print("DA_SCENE_ACT_OK");
        return act;
    }
}
