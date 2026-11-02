using Godot;

/// <summary>
/// The act scene — composes IsoCamera + AreaRenderer + MonsterField +
/// HeroController + Hud around the ONE GameContext session (never a 2nd session).
/// Tab toggles the Inventory.tscn panel OVER this scene; F5 saves; Esc hints the
/// menu row (LEDGER 75, ships later). Renders session.CurrentArea (slice:
/// grodzisko ↔ mchy); a travel rebuilds the tiles + monster row under the new area.
/// </summary>
public partial class Act : Node
{
    private AreaRenderer _renderer = null!;
    private MonsterField _monsters = null!;
    private HeroController _hero = null!;
    private IsoCamera _camera = null!;
    private Hud _hud = null!;
    private Control? _panel;

    /// <summary>Read accessors for the headless journey leg (Boot) — the same
    /// wired objects the windowed game drives, never a second set.</summary>
    public AreaRenderer Tiles => _renderer;
    public MonsterField Monsters => _monsters;
    public HeroController Hero => _hero;
    public Hud HudPanel => _hud; // the level-up leg's HUD read-back (HUD_LEVEL)

    public override void _Ready()
    {
        var session = GameContext.Session;
        if (session is null)
        {
            GD.PushError("Act: GameContext.Session is null — no second session is ever built here");
            return;
        }

        var world = new Node2D { Name = "World", YSortEnabled = true };
        AddChild(world);
        _renderer = new AreaRenderer { Name = "Area" };
        _monsters = new MonsterField { Name = "MonsterField" };
        world.AddChild(_renderer);
        world.AddChild(_monsters);

        _camera = new IsoCamera { Name = "IsoCamera" };
        AddChild(_camera);
        _hud = new Hud { Name = "Hud" };
        AddChild(_hud);

        _hero = new HeroController { Name = "Hero" };
        world.AddChild(_hero);
        _hero.Wire(_renderer, _monsters, _hud);
        _hero.OnAreaChanged = () => Rebuild(GameContext.Session!.CurrentArea);

        // M5-S3 SIM BRIDGE (MC 11934): the ONE SIM-state clock, windowed only —
        // the headless marker legs run inside one _Ready, so its _Process never
        // pumps them (the story anchors stay byte-identical by construction).
        // HeroController's walk clock and Hud's repaint clock stay untouched:
        // those are presentation time; this feeds RestTick alone.
        if (DisplayServer.GetName() != "headless")
        {
            var simClock = new SessionClockNode { Name = "SimClock" };
            simClock.OnHint = msg => _hud.Hint(msg);
            AddChild(simClock);
        }

        Rebuild(session.CurrentArea);
    }

    /// <summary>Re-renders tiles + monster row + hero + camera for one area — the
    /// ONE rebuild entry, shared by arrival and by a successful stone travel.</summary>
    public void Rebuild(string areaKey)
    {
        var session = GameContext.Session;
        if (session is null) return;
        var area = session.Areas.Get(areaKey);
        _renderer.Build(areaKey, area.IsTown);
        var start = _renderer.CenterCell(); // the arrival cell the spawn bands measure from
        _monsters.Build(areaKey, area.IsTown, _renderer.Walkable, start);
        _hero.Place(start);
        _camera.FocusOn(start, AreaRenderer.W, AreaRenderer.H);
        _hud.Hint(area.Name);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("inventory_toggle")) ToggleInventory();
        else if (@event.IsActionPressed("save_game"))
        {
            GameContext.Save();
            _hud.Hint("the stow-hand copies your kit to the save-stone");
        }
        else if (@event.IsActionPressed("game_menu"))
            _hud.Hint("the game menu lands with the widget row (LEDGER 75)");
    }

    /// <summary>Inventory.tscn is instantiated ONCE and only toggled after — it
    /// reads GameContext.Session, never builds its own.</summary>
    private void ToggleInventory()
    {
        if (_panel is null)
        {
            var packed = GD.Load<PackedScene>("res://scenes/Inventory.tscn");
            if (packed is null) { GD.PushError("missing res://scenes/Inventory.tscn"); return; }
            if (packed.Instantiate() is not Control panel)
            { GD.PushError("Inventory.tscn root is not a Control"); return; }
            _panel = panel;
            AddChild(_panel);
        }
        _panel.Visible = !_panel.Visible;
    }
}
