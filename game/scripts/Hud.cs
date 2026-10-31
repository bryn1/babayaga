using System.Linq;
using Godot;

/// <summary>
/// Life/mana orbs + XP bar + gold + engaged-target line + rite counter — PULL-ONLY:
/// reads the GameSession read-only surface each _Process, zero rules computed here
/// (HUD recompute would be misplaced code per the dependency rule). Orbs show
/// CURRENT/MAX: the current pools joined the Sim with the potion belt (LEDGER row
/// 69); the Max formulas (rows 4–5) stay the Sim's computed values. The belt row
/// (row 69's widget) mirrors session.Belt read-only, and keys 1–4 (belt_1..belt_4)
/// press the SAME public GameSession.UsePotion verb the headless belt leg calls —
/// rejections become HUD hints, never crashes.
/// </summary>
public partial class Hud : CanvasLayer
{
    private Label _life = null!, _mana = null!, _belt = null!, _xp = null!, _gold = null!,
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
        _belt = Line(box); // the belt row — LEDGER row 69's widget, read-only below
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

    /// <summary>Filled belt cells the LAST paint showed (0–4) — captured by the
    /// same session pull as LevelShown. The headless belt leg prints its BELT
    /// lines through this read-back; a frozen literal is that gate's planted-red
    /// shape (the same honesty pattern as HUD_LEVEL).</summary>
    public int BeltFilled { get; private set; }

    /// <summary>The DEAD flag (row 64) the LAST paint showed — captured by the
    /// same session pull as LevelShown/BeltFilled. The headless death leg prints
    /// its HUD_STATE lines through this read-back; a frozen literal is that
    /// gate's planted-red shape (the same honesty pattern as HUD_LEVEL).</summary>
    public bool DeadShown { get; private set; }

    /// <summary>The CURRENT mana the LAST paint showed (row 78's read-back) —
    /// captured by the same session pull as LevelShown/BeltFilled/DeadShown. The
    /// headless cast leg prints its CAST_STATE lines through this read-back; a
    /// frozen literal is that gate's planted-red shape (the honesty pattern,
    /// again — the CURRENT pool; the Max formula on the orb row stays computed).</summary>
    public long ManaShown { get; private set; }

    /// <summary>The current rite the LAST paint showed, as "key:Phase" — the quest
    /// with the LATEST phase the log holds ("" while every rite sits Inactive).
    /// Captured by the same session pull as LevelShown/BeltFilled/DeadShown/
    /// ManaShown. The headless quest leg prints its QUEST_STATE lines through this
    /// read-back; a frozen literal is that gate's planted-red shape (row 62's
    /// live pass, M4-S8).</summary>
    public string QuestShown { get; private set; } = "";

    /// <summary>The one pull-only paint pass — _Process rides it; the headless
    /// level-up and belt legs run it explicitly per round (the legs are
    /// synchronous, never frame-bound; zero rules computed here, same as _Process).</summary>
    public void Refresh()
    {
        var s = GameContext.Session;
        if (s is null) return;

        // CURRENT/MAX — the pools joined the Sim with the belt (row 69); Max stays computed.
        _life.Text = $"Life {s.Character.Life}/{s.Character.MaxLife}";
        _mana.Text = $"Mana {s.Character.Mana}/{s.Character.MaxMana}";
        ManaShown = s.Character.Mana; // the row 78 cast leg's read-back on the same pull
        DeadShown = s.IsDead; // the row 64 flag on the same pull — the death leg reads it back

        // Belt row: four cells in slot order, the read-only mirror of session.Belt.
        _belt.Text = "Belt [" + string.Join(" ", s.Belt.Select(
            (item, i) => $"{i + 1}:{item?.Base.Name ?? "-"}")) + "]";
        BeltFilled = s.Belt.Count(item => item is not null);

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
        string current = ""; // the rite with the LATEST phase — "" while every rite waits Inactive
        foreach (var q in s.Quests.Quests)
        {
            if (q.Phase == BabaYaga.Sim.QuestPhase.Done) done++;
            if (q.Phase != BabaYaga.Sim.QuestPhase.Inactive) current = $"{q.Row.Key}:{q.Phase}";
        }
        _rites.Text = $"Rites {done}/{s.Quests.Quests.Count}" + (current.Length == 0 ? "" : $" — {current}");
        QuestShown = current; // the row 62 quest leg's read-back on the same pull
    }

    /// <summary>Row 69's hotkey row: keys 1–4 (belt_1..belt_4) press the SAME
    /// public GameSession.UsePotion verb the headless BeltLeg calls — the game
    /// side carries NO second belt logic. cast_1 (row 78's first skill slot,
    /// the full 8-slot widget stays row 70) presses the SAME public GameSession.
    /// Cast the headless CastLeg calls, weaving the first learned spark at the
    /// ENGAGED monster (the Target line's own value — nothing engaged is the
    /// session's own rejection). A rejection is a HUD hint, never a crash
    /// (the HeroController hint idiom).</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        var s = GameContext.Session;
        if (s is null) return;
        for (int i = 0; i < BabaYaga.Sim.GameSession.BeltSlots; i++)
        {
            if (!@event.IsActionPressed($"belt_{i + 1}")) continue;
            try { s.UsePotion(i + 1); }
            catch (BabaYaga.Sim.SessionRejectedException e) { Hint(e.Message); }
        }
        if (@event.IsActionPressed("cast_1"))
        {
            try { s.Cast(FirstWeave, s.EngagedKey ?? ""); }
            catch (BabaYaga.Sim.SessionRejectedException e) { Hint(e.Message); }
        }
    }

    /// <summary>The weave cast_1 presses — the koldun's first row-0 spark
    /// (hotkey→skill mapping as a widget is row 70's beat, not this slice).</summary>
    private const string FirstWeave = "iskra";

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
