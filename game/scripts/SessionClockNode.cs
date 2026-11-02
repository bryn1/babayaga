using System;
using Godot;

/// <summary>
/// M5-S3 SIM BRIDGE, the thin GLUE (MC 11934): one Node whose _Process feeds
/// elapsed real time through the ONE SessionClock.Pump the headless bridge leg
/// also calls — all decisions live in the pure class (xunit-covered). The
/// dead-guard hint rides the plan's ONCE-per-transition rule off the Frozen
/// latch: freeze and resume each speak once through the HUD (wired by Act),
/// never 25 times a second. Created by Act ONLY when windowed: the headless
/// story legs run inside one _Ready, so no _Process frame ever pumps them — the
/// story anchors stay byte-identical by construction.
/// </summary>
public partial class SessionClockNode : Node
{
    private readonly SessionClock _clock = new();

    /// <summary>The HUD hint sink (Act wires Hud.Hint into it).</summary>
    public Action<string>? OnHint;

    public override void _Process(double delta)
    {
        var session = GameContext.Session;
        if (session is null) return;
        bool wasFrozen = _clock.Frozen;
        _ = SessionClock.Pump(_clock, session, delta);
        if (_clock.Frozen && !wasFrozen) OnHint?.Invoke("you are dead — the clock waits for the stone");
        else if (wasFrozen && !_clock.Frozen) OnHint?.Invoke("the stone raises you — time flows again");
    }
}
