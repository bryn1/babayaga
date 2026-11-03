/// <summary>
/// MC 11957 ESC GAME MENU (LEDGER row 75's game-menu half) — the pure rule
/// behind GameMenu. D2 truth (oracle): in Diablo II Escape opens the game menu
/// — the honest clone scope is a windowed panel with EXACTLY two real
/// actions: RESUME hides the panel (no sim door), SAVE &amp; QUIT rides the
/// EXISTING F5 save door FIRST (GameContext.Save — reused, never duplicated)
/// and only THEN the main-menu scene flow; the order lives in
/// PressSaveAndQuit as CODE, pinned by the spy-order test rows. INTERLOCK
/// (a kb'd law, the DeathMenu precedent): while IsDead the DEATH menu is
/// authoritative — OpensFor lets the game menu exist only for the living
/// (DeathMenuLogic.OpensFor owns the dead), and Act's ESC branch is the
/// exclusive if/else. The toggle mirrors the Act doors' idiom (a second ESC
/// hides, a third opens). Layer 24 = the z-order reading stated for the
/// town-panel overlap row: the menu PAINTS ABOVE the town panel (21), the
/// pack (22) and the chest (23) and never closes them — RESUME reveals the
/// panel underneath still open. PURE (no Godot types) — xunit-covered
/// (tests/GameMenuTests.cs).
/// </summary>
public static class GameMenuLogic
{
    public const string ResumeButton = "RESUME";
    public const string SaveQuitButton = "SAVE & QUIT";

    /// <summary>Interlock gate: the game menu exists exactly while the hero
    /// is ALIVE — the dead ride the death menu (the pair to
    /// DeathMenuLogic.OpensFor; both rows xunit-pinned).</summary>
    public static bool OpensFor(bool isDead) => !isDead;

    /// <summary>The authoritative-yield law: ESC against a corpse opens the
    /// death menu, and a game menu LEFT VISIBLE (opened alive, the hero died
    /// to a counter with it open) closes — the death menu never shares the
    /// screen from under it. Act calls this line in its dead branch.</summary>
    public static bool GameMenuMustYield(bool isDead, bool gameMenuVisible) => isDead && gameMenuVisible;

    /// <summary>The Act doors' toggle law: press → the opposite visibility.</summary>
    public static bool ToggleTo(bool currentlyVisible) => !currentlyVisible;

    /// <summary>The panel's OWN CanvasLayer number (the painted-above law;
    /// Act wires it, GameMenuTests pins it above every other overlay).</summary>
    public const int Layer = 24;

    /// <summary>RESUME's press model: hide the panel, ride NO door at all
    /// (the spy test proves save and scene-change never fire here).</summary>
    public static void PressResume(System.Action onHide) => onHide();

    /// <summary>SAVE &amp; QUIT's press model — THE ORDER IS THE CONTRACT:
    /// the existing save door first, then the scene change to the main menu.
    /// A reversed plant goes RED on the spy-order row; a save that throws
    /// never reaches the scene door (the hero is never left unsaved).</summary>
    public static void PressSaveAndQuit(System.Action onSave, System.Action onChangeSceneToMainMenu)
    {
        onSave();
        onChangeSceneToMainMenu();
    }
}
