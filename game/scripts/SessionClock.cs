using BabaYaga.Sim;

/// <summary>
/// M5-S3 SIM BRIDGE, the SESSION CLOCK (MC 11934, plan-c2 S3, FIX-DA5 7/8):
/// the ONE clock for SIM state. It accumulates real seconds and hands WHOLE
/// frames to the sim's only frame door, GameSession.RestTick, at the sim's own
/// 25 fps constant (GameSession.FramesPerSecond REUSED — no fourth constant,
/// no forked clock: HeroController's walk clock and Hud's repaint clock stay
/// presentation-owned and untouched).
///
/// CATCH-UP CLAMP (plan P3-A): one flush pays at most MaxCatchupFrames (2 s
/// @ 25 Hz); the excess of a tab-out is DROPPED, never banked — a minute in
/// the background must not fast-forward the potion-gated economy.
///
/// OUTER DEAD GUARD (FIX-DA5 8): while the hero is dead NOTHING accumulates
/// and nothing flushes — RestTick's RaiseIfDead-first would otherwise storm
/// 25 exceptions a second (Regen.cs:80); death FREEZES the clock and respawn
/// resumes it at the frozen instant, so dead time is never owed as regen. The
/// engine's gate stays as the backstop for every non-clock caller.
///
/// PURE by design (DA F4 / the brief): frame math and flush decisions carry an
/// alive-probe, no Godot types — xunit covers it all; SessionClockNode.cs is
/// the thin _Process glue. Pump is the ONE feed both glue and the headless
/// bridge leg call, so the tested path is the shipped path.
/// </summary>
public sealed class SessionClock
{
    /// <summary>The sim's frame convention — the SAME constant RestTick's
    /// quantum divides by (reuse, not a forked 25).</summary>
    public const int FramesPerSecond = GameSession.FramesPerSecond;

    /// <summary>Plan-c2 clamp: at most 2 s of catch-up per flush.</summary>
    public const int MaxCatchupFrames = 50;

    private double _seconds; // the less-than-one-frame remainder, carried exactly

    /// <summary>Whole frames handed to the sim so far — frozen while dead.</summary>
    public long FlushedFrames { get; private set; }

    /// <summary>Flush batches made — N whole frames arriving at once is ONE batch.</summary>
    public int FlushCount { get; private set; }

    /// <summary>The dead-guard latch: true between the first dead tick and the
    /// first live tick — the hint-ONCE edges the glue reads.</summary>
    public bool Frozen { get; private set; }

    /// <summary>Advance by deltaSeconds with the alive probe; returns the whole
    /// frames to feed the sim (0 while dead, while under one frame, or when the
    /// clamp drops a batch to zero — never negative, never owed later).</summary>
    public int Advance(double deltaSeconds, bool alive)
    {
        if (!alive)
        {
            Frozen = true; // dead time is dropped, never accumulated
            return 0;
        }
        Frozen = false;
        _seconds += deltaSeconds;
        int frames = (int)(_seconds * FramesPerSecond);
        if (frames <= 0) return 0;
        _seconds -= frames / (double)FramesPerSecond; // the sub-frame remainder carries
        if (frames > MaxCatchupFrames) frames = MaxCatchupFrames; // the excess is DROPPED
        FlushedFrames += frames;
        FlushCount++;
        return frames;
    }

    /// <summary>The ONE feed: probe, decide, and hand the batch to the sim as
    /// RestTick(k) through the CommandLog ("rest k", in order — DA F4). Shared
    /// by the windowed glue and the headless bridge leg verbatim.</summary>
    public static int Pump(SessionClock clock, GameSession session, double deltaSeconds)
    {
        int frames = clock.Advance(deltaSeconds, alive: !session.IsDead);
        if (frames > 0)
            CommandLog.Call($"rest {frames}", () => session.RestTick(frames));
        return frames;
    }
}
