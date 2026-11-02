using Godot;

/// <summary>
/// M5-S4 P11 DEATH MENU (MC 11937, plan-c2 FIX-DA5 8 — the owner-soft-lock row):
/// the dead hero's Esc overlay. Two buttons and nothing else: RESPAWN rides the
/// ONE Respawn door through Act's logged wrapper (the raise lands at grodzisko,
/// the clock resumes at the frozen instant — S3's outer dead-guard), QUIT ends
/// the engine. Panel semantics like the inventory: reads no session state itself,
/// owns no session, computes no rules — the button→door map is DeathMenuLogic's
/// (xunit-pinned); this file only paints.
/// LAYOUT NOTE (the leg's RED — a probe pinned the mechanism): this Control is
/// TOP-LEVEL (parented to a CanvasLayer), so its viewport rect lands only AFTER
/// _Ready — and a plain Control never re-sorts its non-container children when
/// it later grows (an anchor FullRect stayed a 0×0 rect at a 1280×720 Size,
/// proven by probe: anchors set, rect dead). So children are laid out MANUALLY
/// from Size on every NOTIFICATION_RESIZED plus one settle frame — anchors and
/// containers are not trusted through this top-level boundary.
/// </summary>
public partial class DeathMenu : Control
{
    /// <summary>Wired by Act: the door wrapper (Act.RespawnViaDoor) and the engine exit.</summary>
    public System.Action? OnRespawn;
    public System.Action? OnQuit;

    private ColorRect _dim = null!;
    private VBoxContainer _box = null!;

    public override void _Ready()
    {
        _dim = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.72f), // dusk dim — the death reads through the paint
        };
        AddChild(_dim);

        _box = new VBoxContainer();
        _box.AddChild(new Label
        {
            Text = "YOU DIED",
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings
            {
                FontSize = 48,
                FontColor = new Color("7E1620"), // blood-rune token (M4-placement §3)
            },
        });
        _box.AddChild(new Label
        {
            Text = "the stones remember you",
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 18, FontColor = new Color("D8CBA6") },
        });
        _box.AddChild(Button(DeathMenuLogic.RespawnButton, () => { Visible = false; OnRespawn?.Invoke(); }));
        _box.AddChild(Button(DeathMenuLogic.QuitButton, () => OnQuit?.Invoke()));
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
        if (Size == Vector2.Zero) return;
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
