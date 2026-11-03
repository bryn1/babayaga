using Godot;

/// <summary>
/// MC 11957 ESC GAME MENU (LEDGER row 75's game-menu half). D2 truth (oracle):
/// in Diablo II Escape opens the game menu — this is that panel, with the two
/// REAL actions and nothing else: RESUME hides it, SAVE &amp; QUIT rides the
/// EXISTING F5 save door first and THEN changes to the main-menu scene (the
/// door + scene flow live in Act's wiring; the ORDER is GameMenuLogic's,
/// xunit-pinned — no third decorative row, a button that does nothing is
/// theater and is BANNED). While IsDead this panel never opens (the death
/// menu owns the corpse's Esc — Act's exclusive branch + GameMenuLogic).
/// LAYOUT: the top-level-Control law (DeathMenu D3 / MainMenu D1, MC 11943) —
/// a top-level Control lands its viewport rect only AFTER _Ready, so children
/// are laid out MANUALLY from Size on every NOTIFICATION_RESIZED plus one
/// settle frame, behind the IsNodeReady guard (a pre-_Ready RESIZED pass used
/// to be the NRE).
/// </summary>
public partial class GameMenu : Control
{
    /// <summary>Wired by Act: the EXISTING save door (GameContext.Save, the
    /// F5 path reused) and the main-menu scene flow.</summary>
    public System.Action? OnSaveDoor;
    public System.Action? OnMainMenuScene;

    private ColorRect _dim = null!;
    private VBoxContainer _box = null!;

    public override void _Ready()
    {
        _dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.72f) }; // dusk dim (DeathMenu token)
        AddChild(_dim);

        _box = new VBoxContainer();
        _box.AddChild(new Label
        {
            Text = "GAME MENU",
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 44, FontColor = new Color("C9A227") }, // title: gold-trim token
        });
        _box.AddChild(Button(GameMenuLogic.ResumeButton, () => GameMenuLogic.PressResume(Hide)));
        _box.AddChild(Button(GameMenuLogic.SaveQuitButton, () => GameMenuLogic.PressSaveAndQuit(
            () => OnSaveDoor?.Invoke(), () => OnMainMenuScene?.Invoke())));
        AddChild(_box);
    }

    public override void _Process(double delta)
    {
        if (Size != Vector2.Zero) SetProcess(false); // settle frames are handled by RESIZED; keep ticking till the rect lands
        LayoutToSize();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutToSize();
    }

    private void LayoutToSize()
    {
        if (!IsNodeReady() || Size == Vector2.Zero) return; // RESIZED may fire pre-_Ready (the D3 lesson)
        _dim.Position = Vector2.Zero;
        _dim.Size = Size;
        _box.ResetSize(); // the VBox measures its children (the buttons size the column)
        _box.Position = new Vector2((Size.X - _box.Size.X) / 2f, (Size.Y - _box.Size.Y) / 2f);
    }

    private static Button Button(string text, System.Action onPressed)
    {
        var b = new Button { Text = text };
        b.Pressed += onPressed;
        return b;
    }
}
