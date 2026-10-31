using Godot;

/// <summary>
/// Title card + New Game / Continue / Quit. The title-card art (256-rule aside,
/// 1920×1080 opaque) is optional — missing art = PushWarning + flat dusk panel,
/// never a crash. The SUBTITLE STRING lives here (A1 spec): the card image carries
/// no baked subtitle. Continue loads user://save, or New's a session when none.
/// </summary>
public partial class MainMenu : Control
{
    private const string Subtitle = "A DARK SLAVIC SAGA";

    public override void _Ready()
    {
        var card = new TextureRect
        {
            Texture = AreaRenderer.ArtOrNull("res://assets/title-card.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            AnchorsPreset = (int)LayoutPreset.FullRect,
        };
        AddChild(card);

        var box = new VBoxContainer
        {
            AnchorsPreset = (int)LayoutPreset.Center,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
        };
        box.AddChild(MakeLabel("BABA YAGA", 64));
        box.AddChild(MakeLabel(Subtitle, 22));
        AddChild(box);

        AddMenuButton(box, "New Game", () =>
        {
            GameContext.New(1);
            GetTree().ChangeSceneToFile("res://scenes/Act.tscn");
        });
        AddMenuButton(box, "Continue", () =>
        {
            GameContext.Load();
            GetTree().ChangeSceneToFile("res://scenes/Act.tscn");
        });
        AddMenuButton(box, "Quit", () => GetTree().Quit(0));
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
