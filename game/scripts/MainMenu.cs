using Godot;

/// <summary>
/// Title card + New Game / Continue / Quit. The title-card art (256-rule aside,
/// 1920×1080 opaque) is optional — missing art = PushWarning + flat dusk panel,
/// never a crash. The SUBTITLE STRING lives here (A1 spec): the card image carries
/// no baked subtitle. Continue loads user://save, or New's a session when none.
/// LAYOUT (verdict D1 fix, MC 11943): the same top-level lesson DeathMenu learned
/// — a top-level Control lands its viewport rect only AFTER _Ready, and anchored
/// children never re-sort across that boundary (the old center-preset + grow-both
/// box painted at (0,0), title clipped, at BOTH judged sizes). So the card and
/// the menu box are laid out MANUALLY from Size on every NOTIFICATION_RESIZED
/// plus one settle frame — the DeathMenu/TownPanel RESIZED-layout idiom, reused.
/// </summary>
public partial class MainMenu : Control
{
    private const string Subtitle = "A DARK SLAVIC SAGA";

    private TextureRect _card = null!;
    private VBoxContainer _box = null!;

    public override void _Ready()
    {
        _card = new TextureRect
        {
            Texture = AreaRenderer.ArtOrNull("res://assets/title-card.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        };
        AddChild(_card);

        _box = new VBoxContainer();
        _box.AddChild(MakeLabel("BABA YAGA", 64));
        _box.AddChild(MakeLabel(Subtitle, 22));
        AddChild(_box);

        AddMenuButton(_box, "New Game", () =>
        {
            // M5-S4 SEED TRUTH (plan-c2 S4 "kill MainMenu.cs:36 blindness"): the
            // New seed is printed AND rides the log as a comment (comments skip
            // clean — it is replay metadata, not grammar), so the windowed log
            // can be replayed through the CLI at --seed <the printed number>.
            var s = GameContext.New(1);
            GD.Print($"NEW_GAME_SEED {s.Seed}");
            CommandLog.Ambient?.Record($"# seed {s.Seed}");
            GetTree().ChangeSceneToFile("res://scenes/Act.tscn");
        });
        AddMenuButton(_box, "Continue", () =>
        {
            GameContext.Load();
            GetTree().ChangeSceneToFile("res://scenes/Act.tscn");
        });
        AddMenuButton(_box, "Quit", () => GetTree().Quit(0));
    }

    public override void _Process(double delta)
    {
        if (Size != Vector2.Zero) SetProcess(false); // settle frames are handled by RESIZED; keep ticking till the rect lands (DeathMenu/TownPanel law)
        LayoutToSize();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutToSize();
    }

    private void LayoutToSize()
    {
        if (!IsNodeReady() || Size == Vector2.Zero) return; // RESIZED may fire pre-_Ready (the D3 lesson)
        _card.Position = Vector2.Zero;
        _card.Size = Size; // the dusk card fills the window (opaque when the art exists)
        _box.ResetSize(); // the VBox measures its children (the buttons size the column)
        _box.Position = new Vector2((Size.X - _box.Size.X) / 2f, (Size.Y - _box.Size.Y) / 2f);
    }

    private static Label MakeLabel(string text, int size) => new()
    {
        Text = text,
        HorizontalAlignment = HorizontalAlignment.Center,
        // title: gold-trim token; subtitle: parchment token (M4-placement §3 A1)
        LabelSettings = new LabelSettings
        {
            FontSize = size,
            FontColor = size >= 64 ? new Color("C9A227") : new Color("D8CBA6"),
        },
    };

    private static void AddMenuButton(VBoxContainer box, string text, System.Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        box.AddChild(button);
    }
}
