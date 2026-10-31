using Godot;

/// <summary>
/// Life/mana orbs + XP bar + gold + engaged-target line + rite counter — PULL-ONLY:
/// reads the GameSession read-only surface each _Process, zero rules computed here
/// (HUD recompute would be misplaced code per the dependency rule). Life/mana show
/// MAX values on purpose: no current-life pool exists in the Sim yet (LEDGER rows
/// 4–5 BUILDING) — a documented gap, not a hidden bug (M4-slice-DoD §2).
/// </summary>
public partial class Hud : CanvasLayer
{
    private Label _life = null!, _mana = null!, _xp = null!, _gold = null!,
        _engaged = null!, _rites = null!, _area = null!, _hint = null!;

    public override void _Ready()
    {
        var panel = new PanelContainer
        {
            AnchorsPreset = (int)Control.LayoutPreset.TopWide,
            Name = "HudPanel",
        };
        AddChild(panel);

        var box = new VBoxContainer();
        panel.AddChild(box);

        // Orb row: life (blood-rune/life-glow) + mana (mana-deep/mana-glow) icons
        // beside their MAX-number labels; the orb art is optional (placeholder pass).
        box.AddChild(OrbRow("res://assets/ui/ui-orb-life.png", "7E1620", out _life, "Life"));
        box.AddChild(OrbRow("res://assets/ui/ui-orb-mana.png", "1F3A8C", out _mana, "Mana"));
        _xp = Line(box);
        _gold = Line(box);
        _area = Line(box);
        _engaged = Line(box);
        _rites = Line(box);

        _hint = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        AddChild(_hint);
    }

    public override void _Process(double delta) => Refresh();

    /// <summary>The level the HUD last PAINTED — captured by the same session pull
    /// that writes the XP line's "(lvl N)". The headless level-up leg calls
    /// Refresh + reads this back every kill round (HUD_LEVEL); a frozen literal is
    /// that gate's planted-red shape.</summary>
    public int LevelShown { get; private set; }

    /// <summary>The one pull-only paint pass — _Process rides it; the headless
    /// level-up leg runs it explicitly per kill round (the leg is synchronous,
    /// never frame-bound; zero rules computed here, same as _Process).</summary>
    public void Refresh()
    {
        var s = GameContext.Session;
        if (s is null) return;

        // MAX values — the current-life pool is LEDGER rows 4–5 (BUILDING), expected.
        _life.Text = $"Life {s.Character.MaxLife}";
        _mana.Text = $"Mana {s.Character.MaxMana}";

        long lo = s.Ladder.TotalXpForLevel(s.Level);
        long hi = s.Ladder.TotalXpForLevel(s.Level >= s.Ladder.MaxLevel ? s.Level : s.Level + 1);
        long xp = s.Character.Xp;
        _xp.Text = hi > lo ? $"XP {xp - lo}/{hi - lo} (lvl {s.Level})" : $"XP {xp} (lvl {s.Level} MAX)";
        LevelShown = s.Level; // the value this paint put on screen, from the SAME pull

        _gold.Text = $"Gold {s.Gold}";
        _area.Text = $"Area {s.CurrentArea}";
        _engaged.Text = s.EngagedKey is null
            ? "Target —"
            : $"Target {s.Monsters.Get(s.EngagedKey).Name}";

        int done = 0;
        foreach (var q in s.Quests.Quests)
            if (q.Phase == BabaYaga.Sim.QuestPhase.Done) done++;
        _rites.Text = $"Rites {done}/{s.Quests.Quests.Count}";
    }

    /// <summary>SessionRejectedException surfaces as a HUD hint, never a crash
    /// (HeroController forwards the reason text here).</summary>
    public void Hint(string message) => _hint.Text = message;

    private static Control OrbRow(string artPath, string colorHex, out Label label, string kind)
    {
        var row = new HBoxContainer();
        var tex = AreaRenderer.ArtOrNull(artPath) ?? AreaRenderer.Diamond(new Color(colorHex), 64, 64);
        row.AddChild(new TextureRect { Texture = tex });
        label = new Label { VerticalAlignment = VerticalAlignment.Center };
        row.AddChild(label);
        label.Name = kind;
        return row;
    }

    private static Label Line(VBoxContainer box)
    {
        var l = new Label();
        box.AddChild(l);
        return l;
    }
}
