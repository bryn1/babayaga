// M5-S5 (MC 11939, plan-c2 S5): this file carries the windowed HUD AND its
// pure read-back core (HudReadout below). BabaYaga.Sim.Tests links this file
// with HUDCORE defined — the Godot shell then compiles OUT and the format
// rows + the frozen-literal plant run as plain xunit (tests/HudReadoutTests.cs,
// the S3/S4 purity idiom kept inside ONE file, as the S5 ownership row pins).
#if !HUDCORE
using Godot;
#endif
using System.Linq;

/// <summary>
/// Life/mana orbs + XP bar + gold + engaged-target line + rite counter — PULL-ONLY:
/// reads the GameSession read-only surface each _Process, zero rules computed here
/// (HUD recompute would be misplaced code per the dependency rule). Orbs show
/// CURRENT/MAX: the current pools joined the Sim with the potion belt (LEDGER row
/// 69); the Max formulas (rows 4–5) stay the Sim's computed values. The belt row
/// (row 69's widget) mirrors session.Belt read-only, and keys 1–4 (belt_1..belt_4)
/// press the SAME public GameSession.UsePotion verb the headless belt leg calls —
/// rejections become HUD hints, never crashes.
/// M5-S5 adds the three plan-c2 S5 rows, all PULL-ONLY like the rest: the
/// MERCENARY line (the §6.7 one-merc slot read straight off session.Merc — "—"
/// while nothing rides along, "— dead" honest while the corpse is where it fell);
/// the ORB FILL on the existing ui-orb art (the SAME current/max the text line
/// prints, scaled to a bright column rising from the orb's base — a display
/// scale of two pulled literals, zero new art per the S2 reuse audit); and the
/// DAMAGE/HINT FLASH row — the last event the session side offered: the drop
/// between the life the HUD LAST painted and this paint (a presentation delta
/// of two pulls, WHO dealt the blow stays the Sim's own fact) or the Hint door's
/// text. A HUD that could mask a moving session dies in the plant test.
/// </summary>
#if !HUDCORE
public partial class Hud : CanvasLayer
{
    private Label _life = null!, _mana = null!, _belt = null!, _xp = null!, _gold = null!,
        _engaged = null!, _rites = null!, _area = null!, _hint = null!, _merc = null!, _flash = null!;
    private OrbGauge _lifeGauge = null!, _manaGauge = null!;

    public override void _Ready()
    {
        // MC 11959 THE HUD CLICK LAW, wired at EVERY construction site: the
        // HUD is READ-OUT — every part here lets world clicks THROUGH (belt
        // 1–4 / cast_1 are KEYBOARD doors, nothing under this panel answers a
        // click today; a future interactive child must name itself into
        // HudReadout.MouseFilterFor's Stop set and keep its own STOP).
        var panel = new PanelContainer
        {
            AnchorsPreset = (int)Control.LayoutPreset.TopWide,
            Name = "HudPanel",
            MouseFilter = MouseFilterLaw.For("hud.panel"),
        };
        AddChild(panel);

        var box = new VBoxContainer { MouseFilter = MouseFilterLaw.For("hud.box") };
        panel.AddChild(box);

        // Orb row: life (blood-rune/life-glow) + mana (mana-deep/mana-glow) icons
        // beside their MAX-number labels; the orb art is optional (placeholder pass).
        box.AddChild(OrbRow("res://assets/ui/ui-orb-life.png", "7E1620", out _life, out _lifeGauge, "Life"));
        box.AddChild(OrbRow("res://assets/ui/ui-orb-mana.png", "1F3A8C", out _mana, out _manaGauge, "Mana"));
        _belt = Line(box); // the belt row — LEDGER row 69's widget, read-only below
        _xp = Line(box);
        _gold = Line(box);
        _area = Line(box);
        _engaged = Line(box);
        _rites = Line(box);
        _merc = Line(box); // M5-S5 mercenary line — the §6.7 pull, read-only below
        _flash = Line(box); // M5-S5 damage/hint flash row — the last event, below

        _hint = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterLaw.For("hud.hint") };
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

    /// <summary>The mercenary line the LAST paint showed (M5-S5) — captured by
    /// the same session pull as the read-backs above ("Merc —" while the slot is
    /// empty). A frozen literal is that row's planted-red shape (the honesty
    /// pattern, again); the S5 shot leg prints its M5_S5_MERC line through it.</summary>
    public string MercShown { get; private set; } = "Merc —";

    /// <summary>The Area line the LAST paint showed (M5-S5, the shot leg's P8/
    /// follow read on the SAME pull as MercShown — the honesty pattern, again).</summary>
    public string AreaShown { get; private set; } = "";

    /// <summary>The damage/hint flash row's text the LAST paint showed (M5-S5) —
    /// the Hint door's text or the pull-delta damage the Refresh below painted.</summary>
    public string FlashShown { get; private set; } = "";

    /// <summary>The life the LAST paint showed (M5-S5) — the damage flash's base:
    /// the delta between THIS pull and it is what the flash row prints (two
    /// pulls of one literal, no rule). Also the dead/low-life legs' honest read.</summary>
    public long LifeShown { get; private set; }

    /// <summary>The life-orb fill column the LAST paint drew, in px (M5-S5) —
    /// the pulled current over the pulled max, scaled (HudReadout.OrbFillPx).
    /// A frozen literal here is the orb row's planted-red shape; the S5 shot
    /// leg prints M5_S5_FILL beside its idle/low-life pair.</summary>
    public int LifeFillPx { get; private set; }

    /// <summary>The mana-orb twin of LifeFillPx (same pull, same scale).</summary>
    public int ManaFillPx { get; private set; }

    /// <summary>The one pull-only paint pass — _Process rides it; the headless
    /// level-up and belt legs run it explicitly per round (the legs are
    /// synchronous, never frame-bound; zero rules computed here, same as _Process).
    /// Every text comes from HudReadout.Capture — the pure core's ONE pull — and
    /// the read-backs beside the labels record what THIS paint put on screen.</summary>
    public void Refresh()
    {
        var s = GameContext.Session;
        if (s is null) return;

        var f = HudReadout.Capture(s);

        // The damage flash BEFORE the read-backs move: the delta between the
        // life the last paint showed and this pull (a heal or a no-move paints
        // nothing over the row; a Hint keeps the text until the next event).
        string dmg = HudReadout.DamageFlash(LifeShown, f.LifeNow);
        if (dmg.Length > 0) FlashText(dmg);

        // CURRENT/MAX — the pools joined the Sim with the belt (row 69); Max stays computed.
        _life.Text = f.Life;
        _mana.Text = f.Mana;
        _belt.Text = f.Belt;
        _xp.Text = f.Xp;
        _gold.Text = f.Gold;
        _area.Text = f.Area;
        _engaged.Text = f.Target;
        _rites.Text = f.Rites;
        _merc.Text = f.Merc;

        // Orb fills: the SAME two literals the text lines print, as a column.
        _lifeGauge.SetFill(f.LifeNow, f.LifeMax);
        _manaGauge.SetFill(f.ManaNow, f.ManaMax);

        // The read-backs: what this paint put on screen, from the SAME pull.
        ManaShown = f.ManaNow; // the row 78 cast leg's read-back on the same pull
        DeadShown = f.Dead; // the row 64 flag on the same pull — the death leg reads it back
        BeltFilled = f.BeltFilled;
        LevelShown = f.Level; // the value this paint put on screen, from the SAME pull
        QuestShown = f.Quest; // the row 62 quest leg's read-back on the same pull
        MercShown = f.Merc;
        AreaShown = f.Area;
        LifeShown = f.LifeNow;
        LifeFillPx = _lifeGauge.FillPx;
        ManaFillPx = _manaGauge.FillPx;
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
            int slot = i + 1; // M5-S3 (MC 11934): the use line rides the CommandLog wrapper; OFF = plain call
            try { CommandLog.Call($"use {slot}", () => s.UsePotion(slot)); }
            catch (BabaYaga.Sim.SessionRejectedException e) { Hint(e.Message); }
        }
        if (@event.IsActionPressed("cast_1"))
        {
            string weave = FirstWeave, target = s.EngagedKey ?? "";
            try { CommandLog.Call($"cast {weave} {target}", () => s.Cast(weave, target)); }
            catch (BabaYaga.Sim.SessionRejectedException e) { Hint(e.Message); }
        }
    }

    /// <summary>The weave cast_1 presses — the koldun's first row-0 spark
    /// (hotkey→skill mapping as a widget is row 70's beat, not this slice).</summary>
    private const string FirstWeave = "iskra";

    /// <summary>SessionRejectedException surfaces as a HUD hint, never a crash
    /// (HeroController forwards the reason text here). The M5-S5 flash row rides
    /// the same event — the bottom label keeps its exact existing contract (the
    /// S4 leg's HintText read-back), the flash row shows the SAME last event.</summary>
    public void Hint(string message)
    {
        _hint.Text = message;
        FlashText(message);
    }

    /// <summary>The flash row's ONE writer: the last event wins (no wall-clock
    /// fade — the row keeps the last truth until the next one, so a shot or a
    /// synchronous leg reads exactly what the last paint showed).</summary>
    private void FlashText(string message)
    {
        _flash.Text = message;
        FlashShown = message;
    }

    private static Control OrbRow(string artPath, string colorHex, out Label label, out OrbGauge gauge, string kind)
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterLaw.For("hud.orb-row") };
        gauge = new OrbGauge(artPath, colorHex);
        row.AddChild(gauge);
        label = new Label { VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterLaw.For("hud.line") };
        row.AddChild(label);
        label.Name = kind;
        return row;
    }

    private static Label Line(VBoxContainer box)
    {
        var l = new Label { MouseFilter = MouseFilterLaw.For("hud.line") };
        box.AddChild(l);
        return l;
    }

    /// <summary>M5-S5: the ui-orb art with the PULLED fill — the empty orb sits
    /// dim, a bright clipped copy of the art's own bottom band rises as tall as
    /// current/max (HudReadout.OrbFillPx — the two literals the text line prints,
    /// a display scale, zero rules, zero new art). Custom _Draw (DrawTextureRect
    /// + DrawTextureRectRegion) — no shader, no TextureProgress second asset.</summary>
    private sealed partial class OrbGauge : Control
    {
        private readonly Texture2D _tex;
        private readonly Color _dim = new(0.32f, 0.32f, 0.35f);

        /// The fill column height the LAST SetFill asked for (px, 0..tex height).
        public int FillPx { get; private set; }

        public OrbGauge(string artPath, string colorHex)
        {
            _tex = AreaRenderer.ArtOrNull(artPath) ?? AreaRenderer.Diamond(new Color(colorHex), 64, 64);
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            MouseFilter = MouseFilterLaw.For("hud.orb-gauge"); // the art column never eats a world click (MC 11959)
            var size = new Vector2(_tex.GetWidth(), _tex.GetHeight());
            CustomMinimumSize = size;
            Size = size;
        }

        public void SetFill(long current, long max)
        {
            int px = HudReadout.OrbFillPx(current, max, (int)_tex.GetHeight());
            if (px == FillPx) return;
            FillPx = px;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var full = new Rect2(0, 0, _tex.GetWidth(), _tex.GetHeight());
            DrawTextureRect(_tex, full, false, _dim); // the shell: an empty orb
            if (FillPx <= 0) return; // DEAD/empty: the dim shell IS the state
            var band = new Rect2(0, full.Size.Y - FillPx, full.Size.X, FillPx);
            DrawTextureRectRegion(_tex, band, band); // the art's own base band, at full brightness
        }
    }
}
#endif // !HUDCORE

/// <summary>The HUD's PURE read-back core (M5-S5, MC 11939): ONE capture per
/// paint turns the session's READ-ONLY surface into the exact strings the shell
/// paints and the fill/flash values it scales — every number PULLED, ZERO rules
/// computed here (v9's HUD-recompute rejection unchanged: the Max formulas and
/// the XP thresholds stay the Sim's own computed values). The test project links
/// this file with HUDCORE defined, so the format rows and the frozen-literal
/// plant run as plain xunit — see tests/HudReadoutTests.cs.</summary>
public static class HudReadout
{
    /// <summary>One paint's worth of HUD text plus the two numeric pulls the
    /// gauges scale. Plain fields: the plant test hands a FROZEN twin (two
    /// frames that never move) to the diff row (HudReadoutTests).</summary>
    public sealed class Frame
    {
        public string Life = "", Mana = "", Belt = "", Xp = "", Gold = "", Area = "",
            Target = "", Rites = "", Merc = "", Quest = "";
        public int Level, BeltFilled;
        public bool Dead;
        public long LifeNow, LifeMax, ManaNow, ManaMax;
    }

    /// <summary>The ONE pull: session read-only surface in, paint-ready frame
    /// out. The strings are the pinned HUD contract (the M4/S4 evidence rows
    /// read them off the window verbatim) — moved here UNCHANGED in shape. The
    /// ONE dotless object initializer (values computed into locals above it)
    /// also keeps every line clear of the GameInputTests StateWrite scan —
    /// the frame is a paint value, never session state; the shape says so too.</summary>
    public static Frame Capture(BabaYaga.Sim.GameSession s)
    {
        long lo = s.Ladder.TotalXpForLevel(s.Level);
        long hi = s.Ladder.TotalXpForLevel(s.Level >= s.Ladder.MaxLevel ? s.Level : s.Level + 1);
        long xp = s.Character.Xp;
        string xpText = hi > lo ? $"XP {xp - lo}/{hi - lo} (lvl {s.Level})" : $"XP {xp} (lvl {s.Level} MAX)";

        int done = 0;
        string current = ""; // the rite with the LATEST phase — "" while every rite waits Inactive
        foreach (var q in s.Quests.Quests)
        {
            if (q.Phase == BabaYaga.Sim.QuestPhase.Done) done++;
            if (q.Phase != BabaYaga.Sim.QuestPhase.Inactive) current = $"{q.Row.Key}:{q.Phase}";
        }
        string ritesText = $"Rites {done}/{s.Quests.Quests.Count}" + (current.Length == 0 ? "" : $" — {current}");

        var m = s.Merc; // the §6.7 one-merc slot: pulled, never mirrored (M5-S5)
        string mercText = m is null ? "Merc —"
            : $"Merc {m.Name} — Life {m.Life}/{m.MaxLife} (lvl {m.Level})" + (m.IsDead ? " — dead" : "");

        return new Frame
        {
            // CURRENT/MAX — the pools joined the Sim with the belt (row 69); Max stays computed.
            Life = $"Life {s.Character.Life}/{s.Character.MaxLife}",
            Mana = $"Mana {s.Character.Mana}/{s.Character.MaxMana}",
            Belt = "Belt [" + string.Join(" ", s.Belt.Select(
                (item, i) => $"{i + 1}:{item?.Base.Name ?? "-"}")) + "]",
            Xp = xpText,
            Gold = $"Gold {s.Gold}",
            Area = $"Area {s.CurrentArea}",
            Target = s.EngagedKey is null
                ? "Target —"
                : $"Target {s.Monsters.Get(s.EngagedKey).Name}",
            Rites = ritesText,
            Merc = mercText,
            Quest = current,
            Level = s.Level,
            BeltFilled = s.Belt.Count(item => item is not null),
            Dead = s.IsDead,
            LifeNow = s.Character.Life, LifeMax = s.Character.MaxLife,
            ManaNow = s.Character.Mana, ManaMax = s.Character.MaxMana,
        };
    }

    /// <summary>The ui-orb fill column height for one paint: the pulled current
    /// over the pulled max, scaled to the art height (integer, floors, clamped).
    /// DISPLAY SCALE ONLY — both inputs are the literals the text line already
    /// prints; no rule lives here (a max of 0 has no ratio: honest empty).</summary>
    public static int OrbFillPx(long current, long max, int heightPx) =>
        max <= 0 || current <= 0 ? 0
        : current >= max ? heightPx
        : (int)(current * heightPx / max);

    /// <summary>The HUD click LAW as data (MC 11959): every read-out part —
    /// the panel box, the orb rows, the counters, the hint label — lets world
    /// clicks THROUGH ("Ignore": a PanelContainer swallows every click under
    /// its rect — MC 11958 measured it live, the west-door walk had ONE
    /// click-through pixel); every interactive overlay STOPS ("Stop": a click
    /// on an open panel never falls through to the world), and so does any
    /// part nobody named — the fail-safe direction: a new widget can never
    /// leak world clicks by accident. The Godot shell maps the names through
    /// MouseFilterLaw; the overlays keep their shipped blocking defaults
    /// (bodies PASS, buttons STOP — the InventoryPanel root's documented
    /// full-screen margin is the one exception, pinned by the scan rows).</summary>
    public static string MouseFilterFor(string part) =>
        part is "hud.panel" or "hud.box" or "hud.orb-row" or "hud.orb-gauge" or "hud.line" or "hud.hint"
            ? "Ignore" : "Stop";

    /// <summary>The damage-flash text: the drop between the life the HUD LAST
    /// PAINTED and this pull — the same presentation delta as the LevelShown/
    /// BeltFilled/DeadShown read-back family (a heal or a no-move paints
    /// nothing; WHO dealt the blow stays the Sim's fact, never re-derived here).</summary>
    public static string DamageFlash(long lifeShown, long nowLife) =>
        nowLife >= lifeShown ? "" : $"-{lifeShown - nowLife} life";
}
