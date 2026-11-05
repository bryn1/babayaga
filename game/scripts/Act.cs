using BabaYaga.Sim;
using Godot;

/// <summary>
/// The act scene — composes IsoCamera + AreaRenderer + MonsterField +
/// HeroController + Hud around the ONE GameContext session (never a 2nd session).
/// Tab toggles the Inventory.tscn panel OVER this scene; T opens the
/// TownPanel.tscn vendor+captain overlay IN TOWN (M5-S6, MC 11940); C opens the
/// StashPanel.tscn town chest IN TOWN (M5, MC 11953); B opens the
/// CraftPanel.tscn ritual-craft overlay IN TOWN (M5, MC 11970); F5 saves; Esc opens the
/// game menu for the LIVING (MC 11957 — the D2 truth: Escape opens the game
/// menu; GameMenu.tscn, two real actions) and the DeathMenu.tscn RESPAWN door
/// FOR THE DEAD (P11, plan-c2 FIX-DA5 8 — the two are an EXCLUSIVE branch,
/// never a double-open). Renders
/// session.CurrentArea (slice: grodzisko ↔ mchy); a travel rebuilds the tiles +
/// monster row + standing stone under the new area.
/// </summary>
public partial class Act : Node
{
    private AreaRenderer _renderer = null!;
    private MonsterField _monsters = null!;
    private FollowerField _followers = null!;
    private HeroController _hero = null!;
    private IsoCamera _camera = null!;
    private Hud _hud = null!;
    private Control? _panel;
    private DeathMenu? _deathMenu;
    private TownPanel? _townPanel;
    private StashPanel? _stashPanel;
    private CraftPanel? _craftPanel;
    private GameMenu? _gameMenu;

    /// <summary>Read accessors for the headless journey leg (Boot) — the same
    /// wired objects the windowed game drives, never a second set.</summary>
    public AreaRenderer Tiles => _renderer;
    public MonsterField Monsters => _monsters;
    public FollowerField Followers => _followers; // the MC 11977 follower-sprite read for the windowed leg
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
        _followers = new FollowerField { Name = "FollowerField" };
        world.AddChild(_renderer);
        world.AddChild(_monsters);
        world.AddChild(_followers);

        _camera = new IsoCamera { Name = "IsoCamera" };
        AddChild(_camera);
        _hud = new Hud { Name = "Hud" };
        AddChild(_hud);

        _hero = new HeroController { Name = "Hero" };
        world.AddChild(_hero);
        _hero.Wire(_renderer, _monsters, _hud);
        _hero.OnAreaChanged = () => Rebuild(GameContext.Session!.CurrentArea);
        // MC 12144 D2 LOCK-ON (owner report: "the camera fixed on the hero
        // character and moves the same way the hero moves"): every committed
        // step rides the camera — the same ClampToGrid extents Rebuild's
        // FocusOn stashed, so the hero stays dead-center except at the edges.
        _hero.OnStepComplete = () => _camera.Follow(_hero.Cell);
        // MC 11977: the follower field READS the hero cell + the area's
        // walkable set (pull-only, the MonsterField pattern's sibling) — it
        // owns a sprite, never a sim state, never a position the sim tracks.
        _followers.Wire(_hero, _renderer);

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
        else if (@event.IsActionPressed("town_panel"))
        {
            // M5-S6 TOWN PANEL (MC 11940, plan-c2 S6): TOWN-ONLY — the widget
            // opens on the PULLED town key (TownPanelLogic.OpensFor, xunit-
            // pinned); outside grodzisko the key stays inert with a hint. The
            // engine's gates (Buy has no area gate; Hire/Revive/Dismiss have
            // their own) stay the engine's behind the widget — no new rule
            // lives here.
            var t = GameContext.Session;
            if (t is not null && TownPanelLogic.OpensFor(t.CurrentArea, t.Areas.TownKey)) ToggleTownPanel();
            else _hud.Hint("the pedlar and the captain wait in grodzisko");
        }
        else if (@event.IsActionPressed("stash_panel"))
        {
            // M5 STASH CHEST (MC 11953, row-75 chest half): the C key — the
            // existing town-interaction path (the town_panel idiom above; no
            // chest SPRITE stands in the world yet, so there is nothing to
            // click and the key IS the interaction). TOWN-ONLY on the PULLED
            // town key (StashPanelLogic.OpensFor, xunit-pinned); outside
            // grodzisko the key stays inert with a hint — and the ENGINE's
            // own DEAD→town gates on Store/Take stay the authority behind the
            // widget (the Stash.cs gate order), never re-implemented here.
            var u = GameContext.Session;
            if (u is not null && StashPanelLogic.OpensFor(u.CurrentArea, u.Areas.TownKey)) ToggleStashPanel();
            else _hud.Hint("the town chest stands in grodzisko");
        }
        else if (@event.IsActionPressed("craft_panel"))
        {
            // M5 RITUAL CRAFT (MC 11970, row-75 cube half): the B key — the
            // SAME town-interaction path as the chest (the declared reading: no
            // cube SPRITE stands in the world yet, so the key IS the
            // interaction, the stash_panel idiom above). TOWN-ONLY on the
            // PULLED town key (CraftPanelLogic.OpensFor, xunit-pinned); outside
            // grodzisko the key stays inert with the pinned hint — and the
            // ENGINE's own DEAD→town→known→match→room gates on CraftRecipe
            // stay the authority behind the widget (GameSession.RitualCraft.cs),
            // never re-implemented here.
            var v = GameContext.Session;
            if (v is not null && CraftPanelLogic.OpensFor(v.CurrentArea, v.Areas.TownKey)) ToggleCraftPanel();
            else _hud.Hint(CraftPanelLogic.OutsideHint);
        }
        else if (@event.IsActionPressed("game_menu"))
        {
            // MC 11957 — the LEDGER-75 hint this row used to print is RETIRED.
            // D2 truth (oracle, cited): in Diablo II Escape opens the game menu
            // — the ALIVE get the real windowed panel now (GameMenu: RESUME /
            // SAVE & QUIT, two real actions, no theater row). The DEAD keep
            // the P11 death menu ALONE: this branch is EXCLUSIVE (if/else) — a
            // dead Esc never opens or replaces it with the game menu, and a
            // visible game menu YIELDS when the death menu opens (interlock:
            // GameMenuLogic.OpensFor + the yield line below + GameMenuTests +
            // the windowed leg's dead row). The leg marker stays windowed-only
            // noise in the evidence log (headless never sees it).
            var s = GameContext.Session;
            if (DisplayServer.GetName() != "headless")
                GD.Print($"M5_ACT_MENU dead={s?.IsDead == true}");
            if (s is not null && DeathMenuLogic.OpensFor(s.IsDead))
            {
                if (_gameMenu is not null && GameMenuLogic.GameMenuMustYield(true, _gameMenu.Visible))
                    _gameMenu.Visible = false; // the death menu is authoritative (the yield law, xunit-pinned)
                ToggleDeathMenu();
            }
            else ToggleGameMenu();
        }
    }

    /// <summary>Inventory.tscn is instantiated ONCE and only toggled after — it
    /// reads GameContext.Session, never builds its own. The fresh branch OPENS
    /// (M5-FX, MC 11943 — D2's full fix, the sibling-door mechanisms together):
    /// a fresh-instantiated Control is already visible, so the door RETURNS
    /// there — falling into the flip would hide the panel the press just built
    /// (the ToggleDeathMenu/ToggleTownPanel idiom, which this door always meant
    /// to carry: "instantiated ONCE and only toggled AFTER") — AND the panel
    /// rides a CanvasLayer of its own (a bare Control under Act sits on the
    /// world canvas and the camera pans it off-screen — the gate's XVFB lesson,
    /// the same reason the death menu and town panel wrap theirs).</summary>
    private void ToggleInventory()
    {
        if (_panel is null)
        {
            var packed = GD.Load<PackedScene>("res://scenes/Inventory.tscn");
            if (packed is null) { GD.PushError("missing res://scenes/Inventory.tscn"); return; }
            if (packed.Instantiate() is not Control panel)
            { GD.PushError("Inventory.tscn root is not a Control"); return; }
            // MC 11952: the click-to-wear refusal paints itself (the TownPanel
            // OnHint idiom) — every equip click the engine rejects arrives on
            // the HUD line, never a silent swallow.
            if (panel is InventoryPanel inv) inv.OnHint = msg => _hud.Hint(msg);
            _panel = panel;
            AddChild(new CanvasLayer { Name = "InventoryLayer", Layer = 22 }); // above the town panel's 21
            GetNode<CanvasLayer>("InventoryLayer").AddChild(_panel);
            return; // fresh-instantiated Control is already visible — the OPEN is done
        }
        _panel.Visible = !_panel.Visible;
    }

    /// <summary>DeathMenu.tscn follows the SAME instantiate-once idiom as the
    /// inventory panel — it reads GameContext.Session, never builds its own.    /// It rides a CanvasLayer of its own (a Control under the Node2D world would
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

    /// <summary>TownPanel.tscn follows the SAME instantiate-once idiom (M5-S6,
    /// MC 11940) and the SAME CanvasLayer placement — a Control under the
    /// Node2D world would pan with the camera and dodge the GUI clicks (the
    /// XVFB lesson). It reads GameContext.Session, never builds its own;
    /// OnHint paints the engine's refusal text (rejection → HUD hint).</summary>
    private void ToggleTownPanel()
    {
        if (_townPanel is null)
        {
            var packed = GD.Load<PackedScene>("res://scenes/TownPanel.tscn");
            if (packed is null) { GD.PushError("missing res://scenes/TownPanel.tscn"); return; }
            if (packed.Instantiate() is not TownPanel panel)
            { GD.PushError("TownPanel.tscn root is not a TownPanel"); return; }
            panel.OnHint = msg => _hud.Hint(msg);
            AddChild(new CanvasLayer { Name = "TownPanelLayer", Layer = 21 });
            _townPanel = panel;
            GetNode<CanvasLayer>("TownPanelLayer").AddChild(_townPanel);
            return; // fresh-instantiated Control is already visible — the OPEN is done
        }
        _townPanel.Visible = !_townPanel.Visible;
    }

    /// <summary>StashPanel.tscn follows the SAME instantiate-once idiom (MC
    /// 11953, the ToggleTownPanel twin) and the SAME CanvasLayer placement —
    /// a Control under the Node2D world would pan with the camera and dodge
    /// the GUI clicks (the XVFB lesson). It reads GameContext.Session, never
    /// builds its own; OnHint paints the engine's refusal text (rejection →
    /// HUD hint) — the locked-tab hint's engine-pinned wording included.</summary>
    private void ToggleStashPanel()
    {
        if (_stashPanel is null)
        {
            var packed = GD.Load<PackedScene>("res://scenes/StashPanel.tscn");
            if (packed is null) { GD.PushError("missing res://scenes/StashPanel.tscn"); return; }
            if (packed.Instantiate() is not StashPanel panel)
            { GD.PushError("StashPanel.tscn root is not a StashPanel"); return; }
            panel.OnHint = msg => _hud.Hint(msg);
            AddChild(new CanvasLayer { Name = "StashPanelLayer", Layer = 23 }); // above the pack's 22
            _stashPanel = panel;
            GetNode<CanvasLayer>("StashPanelLayer").AddChild(_stashPanel);
            return; // fresh-instantiated Control is already visible — the OPEN is done
        }
        _stashPanel.Visible = !_stashPanel.Visible;
    }

    /// <summary>CraftPanel.tscn follows the SAME instantiate-once idiom (MC
    /// 11970, the ToggleStashPanel twin) and rides a CanvasLayer of its own —
    /// a Control under the Node2D world would pan with the camera and dodge
    /// the GUI clicks (the XVFB lesson). Layer 23 = the chest's number, the
    /// stated reading: both are town furniture that never claims to paint over
    /// the other, while the game menu's 24 stays above EVERY overlay
    /// (GameMenuLogic.Layer, the z-order law). It reads GameContext.Session,
    /// never builds its own; OnHint paints the engine's refusal text AND the
    /// success hint (rejection→hint and success→hint, the brief's pair).</summary>
    private void ToggleCraftPanel()
    {
        if (_craftPanel is null)
        {
            var packed = GD.Load<PackedScene>("res://scenes/CraftPanel.tscn");
            if (packed is null) { GD.PushError("missing res://scenes/CraftPanel.tscn"); return; }
            if (packed.Instantiate() is not CraftPanel panel)
            { GD.PushError("CraftPanel.tscn root is not a CraftPanel"); return; }
            panel.OnHint = msg => _hud.Hint(msg);
            AddChild(new CanvasLayer { Name = "CraftPanelLayer", Layer = 23 }); // the chest's number — see the stated reading above
            _craftPanel = panel;
            GetNode<CanvasLayer>("CraftPanelLayer").AddChild(_craftPanel);
            return; // fresh-instantiated Control is already visible — the OPEN is done
        }
        _craftPanel.Visible = !_craftPanel.Visible;
    }

    /// <summary>GameMenu.tscn follows the SAME instantiate-once idiom (the
    /// ToggleDeathMenu/ToggleTownPanel twins, MC 11957) and rides its OWN
    /// CanvasLayer ABOVE every other overlay (GameMenuLogic.Layer — the
    /// town-panel overlap row: the menu PAINTS over an open town panel and
    /// never closes it; RESUME reveals the panel still open underneath). The
    /// two wired doors: OnSaveDoor IS the EXISTING F5 path (GameContext.Save
    /// — reused, never duplicated) and OnMainMenuScene the EXISTING flow
    /// (the same res://scenes/MainMenu.tscn Boot and MainMenu ride); the
    /// SAVE-first-then-scene ORDER lives in GameMenuLogic (xunit spy).</summary>
    private void ToggleGameMenu()
    {
        if (_gameMenu is null)
        {
            var packed = GD.Load<PackedScene>("res://scenes/GameMenu.tscn");
            if (packed is null) { GD.PushError("missing res://scenes/GameMenu.tscn"); return; }
            if (packed.Instantiate() is not GameMenu menu)
            { GD.PushError("GameMenu.tscn root is not a GameMenu"); return; }
            menu.OnSaveDoor = GameContext.Save; // THE F5 DOOR ITSELF (save_game handler above)
            menu.OnMainMenuScene = () => GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
            AddChild(new CanvasLayer { Name = "GameMenuLayer", Layer = GameMenuLogic.Layer }); // above the chest's 23 — the z-order law, GameMenuTests-pinned
            _gameMenu = menu;
            GetNode<CanvasLayer>("GameMenuLayer").AddChild(_gameMenu);
            return; // fresh-instantiated Control is already visible — the OPEN is done
        }
        _gameMenu.Visible = GameMenuLogic.ToggleTo(_gameMenu.Visible); // a second Esc hides it, a third opens it
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
