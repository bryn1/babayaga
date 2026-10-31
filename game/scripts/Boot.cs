using Godot;

/// <summary>
/// Entry scene script (res://Main.tscn root). Headless → the pinned M4 marker leg:
/// a scripted GameSession (seed 42, "koldun", res://data) + three REAL gold-moving
/// verbs (Buy / SellLast / Gamble), one SIM_TICK line per verb read FROM THE SESSION
/// OBJECT EVERY TICK; then ONE scenes/Act.tscn instantiate through the placeholder
/// pass (DA_SCENE_ACT_OK prints only after a NON-NULL instantiate — a null scene
/// goes to PushError and never prints the marker) → ORCH_M4_OK → Quit(0).
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

    /// <summary>The DoD leg 3/3b gate. Runs NO waypoint verb and never travels;
    /// no discover-after-travel anywhere (the WAYPOINT ORDER CONTRACT lives in
    /// HeroController and is exercised only by live door input).</summary>
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

        InstantiateActSmoke();

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
    /// (missing art = PushWarning, never a crash).</summary>
    private void InstantiateActSmoke()
    {
        var packed = GD.Load<PackedScene>("res://scenes/Act.tscn");
        if (packed is null)
        {
            GD.PushError("scene-exec smoke: res://scenes/Act.tscn loaded NULL");
            return; // no marker on a null scene — the gate's conditional, pinned
        }
        var act = packed.Instantiate();
        if (act is null)
        {
            GD.PushError("scene-exec smoke: Act.tscn instantiated NULL");
            return;
        }
        AddChild(act); // Act._Ready runs here — the placeholder pass executes headless
        GD.Print("DA_SCENE_ACT_OK");
        act.Free();
    }
}
