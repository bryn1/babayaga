using BabaYaga.Sim;
using Godot;

/// <summary>
/// The act scene — composes IsoCamera + AreaRenderer + MonsterField +
/// HeroController + Hud around the ONE GameContext session (never a 2nd session).
/// Tab toggles the Inventory.tscn panel OVER this scene; F5 saves; Esc opens the
/// DeathMenu.tscn RESPAWN door FOR THE DEAD (P11, plan-c2 FIX-DA5 8) and hints the
/// menu row (LEDGER 75) while alive — M5 ships no pause (R1c). Renders
/// session.CurrentArea (slice: grodzisko ↔ mchy); a travel rebuilds the tiles +
/// monster row + standing stone under the new area.
/// </summary>
public partial class Act : Node
{
    private AreaRenderer _renderer = null!;
    private MonsterField _monsters = null!;
    private HeroController _hero = null!;
    private IsoCamera _camera = null!;
    private Hud _hud = null!;
    private Control? _panel;
    private DeathMenu? _deathMenu;

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
        // The standing stone rides the ONE waypoint truth (S4 marker row): shown
        // where a node STANDS, lit where it is DISCOVERED — pulled, never stored.
        _renderer.ShowStone(session.Waypoints.HasNode(areaKey), session.Waypoints.IsDiscovered(areaKey));
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
        {
            // M5-S4 P11 (plan-c2 FIX-DA5 8, the owner-soft-lock row): the DEAD
            // get the death menu with the RESPAWN door; an alive Esc keeps the
            // LEDGER-75 hint — M5 ships no pause (plan R1c). The gate is
            // DeathMenuLogic.OpensFor, xunit-pinned. The leg marker below is
            // windowed-only noise in the evidence log (headless never sees it).
            var s = GameContext.Session;
            if (DisplayServer.GetName() != "headless")
                GD.Print($"M5_ACT_MENU dead={s?.IsDead == true}");
            if (s is not null && DeathMenuLogic.OpensFor(s.IsDead)) ToggleDeathMenu();
            else _hud.Hint("the game menu lands with the widget row (LEDGER 75)");
        }
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

    /// <summary>DeathMenu.tscn follows the SAME instantiate-once idiom as the
    /// inventory panel — it reads GameContext.Session, never builds its own.
    /// It rides a CanvasLayer of its own (a Control under the Node2D world would
    /// pan with the camera and dodge the GUI clicks — the XVFB leg caught it).
    /// RESPAWN rides the ONE Respawn door through the CommandLog wrapper (the
    /// S3a "respawn" twin — death recovery itself replays); QUIT is the
    /// engine's own exit, no sim door (DeathMenuLogic.DoorLineFor pins both).</summary>
    private void ToggleDeathMenu()
    {
        if (_deathMenu is null)
        {
            var packed = GD.Load<PackedScene>("res://scenes/DeathMenu.tscn");
            if (packed is null) { GD.PushError("missing res://scenes/DeathMenu.tscn"); return; }
            if (packed.Instantiate() is not DeathMenu menu)
            { GD.PushError("DeathMenu.tscn root is not a DeathMenu"); return; }
            menu.OnRespawn = RespawnViaDoor;
            menu.OnQuit = () => GetTree().Quit(0);
            AddChild(new CanvasLayer { Name = "DeathMenuLayer", Layer = 20 });
            _deathMenu = menu;
            GetNode<CanvasLayer>("DeathMenuLayer").AddChild(menu);
            return; // fresh-instantiated Control is already visible — the OPEN is done
        }
        _deathMenu.Visible = !_deathMenu.Visible; // a second Esc hides it, a third opens it
    }

    private void RespawnViaDoor()
    {
        var s = GameContext.Session;
        if (s is null || !s.IsDead) return; // the door itself rejects too — never twice here
        try
        {
            CommandLog.Call(DeathMenuLogic.DoorLineFor(DeathMenuLogic.RespawnButton)!, () => s.Respawn());
            _hud.Hint("the stone raises you");
            Rebuild(s.CurrentArea); // grodzisko under the NEW area — the same ONE rebuild entry
        }
        catch (SessionRejectedException e) { _hud.Hint(e.Message); } // never a crash, never a soft-lock
    }
}
