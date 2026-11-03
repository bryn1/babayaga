using Godot;

/// <summary>
/// MC 11959: the ONE translation from the HUD click LAW (HudReadout.
/// MouseFilterFor — pure, xunit-pinned in the HUDCORE half of Hud.cs) to
/// Godot's mouse-filter enum. It lives beside the shell only because the
/// enum is a Godot type; no law decision is made here (Ignore = world
/// clicks pass through the part; Stop = the part swallows them).
/// </summary>
public static class MouseFilterLaw
{
    public static Control.MouseFilterEnum For(string part) =>
        HudReadout.MouseFilterFor(part) == "Ignore"
            ? Control.MouseFilterEnum.Ignore
            : Control.MouseFilterEnum.Stop;
}
