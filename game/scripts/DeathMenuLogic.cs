/// <summary>
/// M5-S4 RENDER + INPUT, the DEATH-MENU MAP (MC 11937, plan-c2 FIX-DA5 8 /
/// P11 — owner soft-lock): the pure rule behind DeathMenu — Esc opens the menu
/// for the DEAD only (an alive Esc keeps the LEDGER-75 hint; M5 ships no
/// pause, plan R1c), the RESPAWN button maps onto the ONE CLI twin verb line
/// the S3a row shipped ("respawn" → GameSession.Respawn through the CommandLog
/// wrapper, so the death-recovery leg itself replays), and QUIT ends the
/// engine through NO sim door. Pure (no Godot types) — xunit-covered.
/// </summary>
public static class DeathMenuLogic
{
    public const string RespawnButton = "RESPAWN";
    public const string QuitButton = "QUIT";

    /// <summary>P11 gate: the menu exists exactly while the hero lies dead.</summary>
    public static bool OpensFor(bool isDead) => isDead;

    /// <summary>Button label → the logged sim-door line, or null when the
    /// button touches no door (QUIT rides the engine's own exit).</summary>
    public static string? DoorLineFor(string button) =>
        button == RespawnButton ? "respawn" : null;
}
