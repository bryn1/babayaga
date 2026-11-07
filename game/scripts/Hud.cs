// reason (file-hygiene, MC 12222): the HUD's Godot shell and its HUDCORE pure
// core are ONE file because the S5 ownership row pins it — BabaYaga.Sim.Tests
// links THIS path with HUDCORE defined, so the bottom band's fills and the text
// column's rows stay ONE paint of ONE Capture; splitting the shell off would put
// a second file between the pull and what it paints. MC 12222's band is ~150 lines.
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

    // ── MC 12222 THE BOTTOM BAND ─────────────────────────────────────────────
    // D2's SHAPE, OUR art: a wood-and-iron band on the window's bottom edge — LIFE
    // orb LEFT, MANA orb RIGHT, the four belt slots CENTRE, the XP strip and its
    // level chip above them. It MIRRORS the corner column (the brief's ruling:
    // that column stays as-is) off the SAME HudReadout.Capture — one pull, one
    // truth, no second belt/XP logic, and nothing in the band uses anything.
    // Click law: every part answers MouseFilterLaw.For("hud.band*") with "Ignore",
    // so the band owns no pixel of the click map — the world under the bottom 90px
    // still answers a click as if the band were not there (HudLeg proves both).
    // Original styling only: StyleBoxFlat frames in tokens this repo already paints
    // with. No D2 asset is shipped, referenced or imitated past the LAYOUT.
    public const string BandOffVar = "BABA_HUD_BAND_OFF"; // HudLeg's RED lever: env hides the band
    public const int BandHeightPx = 90;                   // D2's band ≈18.8% of frame H at 720
    public const int BandCornerInsetPx = 272;             // the MC 12136 controls line owns that corner
    public const int BandOrbBottomPx = 46;                // the wood kept below the orb, clear of it

    private PanelContainer _band = null!;
    private Control _bandRow = null!;
    private OrbGauge _bandLife = null!, _bandMana = null!;
    private ColorRect _xpTrack = null!, _xpFill = null!;
    private Label _chip = null!;
    private readonly Label[] _slots = new Label[BabaYaga.Sim.GameSession.BeltSlots];
    private Vector2 _laid; // the band size the layout last saw (the TownPanel RESIZED-layout law)

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

        // MC 12222: built LAST in _Ready — AFTER the hint label (the S4 leg's
        // first-direct-Label read still lands on the hint) and BEFORE Wire adds
        // the controls line, which therefore keeps painting on top of the wood.
        BuildBand();
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

    // MC 12222 THE BAND's honest read-backs — the LevelShown/BeltFilled family
    // again: what the band's LAST paint put on screen, off the same single pull,
    // which HudLeg asserts against the live session (a frozen literal is the
    // planted-red shape). BandRoot is the live node the leg reads vis/rect off;
    // BandLifePos is its overlap read against the controls line.
    public Control BandRoot => _band;
    public int BandLifeFillPx { get; private set; }  // the band's life-orb fill px …
    public int BandManaFillPx { get; private set; }  // … and its mana twin
    public long BandXpFillPx { get; private set; }   // the strip's fill px …
    public int BandXpTrackPx { get; private set; }   // … painted into this track width
    public string BandBeltShown { get; private set; } = ""; // "letter" or "-" per slot
    public string BandChipShown { get; private set; } = ""; // the "lvl N" word
    public int BandOrbPx => (int)_bandLife.CustomMinimumSize.Y; // the fill denominator
    public int BandSlotCount => _slots.Length;                  // the Sim's own belt ceiling
    public Vector2 BandLifePos => _bandLife.GlobalPosition;
    public bool BandSlotsShown => _slots.Length > 0 && !_slots.Any(c => !c.IsVisibleInTree());


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

        // MC 12222 THE BAND, same paint off the same f: its orbs are the same
        // SetFill calls, its strip is the XP pair the "XP a/b" row prints, its
        // slots are the belt the "Belt […]" row prints. The layout only moves
        // when the band's live size moves (the TownPanel law — a code-built
        // Control under a CanvasLayer is sized from Size, never the preset).
        if (_bandRow.Size.X > 0 && _bandRow.Size != _laid) LayoutBand(_bandRow.Size);
        _bandLife.SetFill(f.LifeNow, f.LifeMax);
        _bandMana.SetFill(f.ManaNow, f.ManaMax);
        _chip.Text = BandChipShown = f.LevelText;
        BandXpTrackPx = (int)_xpTrack.Size.X;
        BandXpFillPx = HudReadout.XpFillPx(f.XpInto, f.XpSpan, BandXpTrackPx);
        _xpFill.Size = new Vector2(BandXpFillPx, _xpTrack.Size.Y);
        for (int i = 0; i < _slots.Length; i++)
            _slots[i].Text = $"{i + 1}\n" + (i < f.BeltLetters.Length ? f.BeltLetters[i] : ""); // cap over letter
        BandLifeFillPx = _bandLife.FillPx;
        BandManaFillPx = _bandMana.FillPx;
        BandBeltShown = string.Join(",", f.BeltLetters.Select(g => g.Length == 0 ? "-" : g));


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

    /// <summary>The bottom band, built ONCE (MC 12222). A PanelContainer carries the
    /// wood frame; its ONE child is a plain Control the panel sizes to the frame's
    /// content rect, and THAT node's children are placed by hand from its live size
    /// (LayoutBand). Every part answers the click law with "Ignore": the band is a
    /// read-out, so belt 1–4 stay KEYBOARD doors and the four cells visualise
    /// session.Belt while pressing nothing — the reason no cell here is a Button.</summary>
    private void BuildBand()
    {
        _band = new PanelContainer
        {
            Name = "HudBand",
            MouseFilter = MouseFilterLaw.For("hud.band"),
            // The leg's RED lever: hiding the band must turn its own assertion red.
            Visible = System.Environment.GetEnvironmentVariable(BandOffVar) is not { Length: > 0 },
        };
        _band.SetAnchorsPreset(Control.LayoutPreset.BottomWide); // THE METHOD — the preset PROPERTY is ignored
        _band.OffsetLeft = _band.OffsetRight = _band.OffsetBottom = 0;
        _band.OffsetTop = -BandHeightPx; // explicit offsets: the kb law for code-built Controls
        _band.AddThemeStyleboxOverride("panel", Iron.Wood());
        AddChild(_band);

        _bandRow = new Control { Name = "BandRow", MouseFilter = MouseFilterLaw.For("hud.band-row") };
        _band.AddChild(_bandRow); // the panel sizes this to the frame's content rect
        // The shipped ui-orb art reused: OrbGauge has ONE fill law (OrbFillPx).
        _bandRow.AddChild(_bandLife = new OrbGauge("res://assets/ui/ui-orb-life.png", "7E1620")); // blood-rune
        _bandRow.AddChild(_bandMana = new OrbGauge("res://assets/ui/ui-orb-mana.png", "1F3A8C")); // mana-deep

        _chip = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterLaw.For("hud.band-chip") };
        _chip.AddThemeColorOverride("font_color", new Color("D8CBA6")); // the parchment token
        _chip.AddThemeStyleboxOverride("normal", Iron.Rim("C9A227"));    // gold trim: the chip is the level
        _bandRow.AddChild(_chip);

        _xpTrack = new ColorRect { Color = new Color("5A564E", 0.55f), MouseFilter = MouseFilterLaw.For("hud.band-xp") };
        _xpFill = new ColorRect { Color = new Color("C9A227"), MouseFilter = MouseFilterLaw.For("hud.band-xp-fill") };
        _xpTrack.AddChild(_xpFill); // no container here: the fill's WIDTH IS the painted XP fraction
        _bandRow.AddChild(_xpTrack);

        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterLaw.For("hud.band-slot") };
            slot.AddThemeColorOverride("font_color", new Color("D8CBA6"));
            slot.AddThemeStyleboxOverride("normal", Iron.Rim("7A5A2A")); // thatch rim: the socket, not the item
            _bandRow.AddChild(_slots[i] = slot);
        }
    }

    /// <summary>The band's ONE layout, from its live size — the law every code-built
    /// Control under a CanvasLayer here obeys. The life orb sits RIGHT of the MC
    /// 12136 controls line (that corner is taken, and the line keeps its exact
    /// shipped position) and bulges above the band's top edge the way D2's orbs do;
    /// the mana orb is flush right, the belt cells centre the window, the strip
    /// spans between the orbs above them.</summary>
    private void LayoutBand(Vector2 s)
    {
        _laid = s;
        int orb = BandOrbPx, cell = orb - 18, gap = 8;
        int rowW = _slots.Length * cell + (_slots.Length - 1) * gap, x0 = (int)((s.X - rowW) / 2);
        _bandLife.Position = new Vector2(BandCornerInsetPx, s.Y - BandOrbBottomPx - orb);
        _bandMana.Position = new Vector2(s.X - 8 - orb, s.Y - BandOrbBottomPx - orb);
        int chipX = BandCornerInsetPx + orb + 8;
        _chip.Position = new Vector2(chipX, 2);
        _chip.Size = new Vector2(64, 24);
        _xpTrack.Position = new Vector2(chipX + 72, 10);
        _xpTrack.Size = new Vector2(System.Math.Max(0, s.X - 88 - chipX - 72), 10);
        _xpFill.Position = Vector2.Zero; // its WIDTH is the painted XP fraction, set in Refresh
        for (int i = 0; i < _slots.Length; i++)
        {
            // The cap line rides above the letter, and the WHOLE cell stays inside
            // the band: the first shot clipped its bottom rim on the window edge.
            _slots[i].Position = new Vector2(x0 + i * (cell + gap), 30);
            _slots[i].Size = new Vector2(cell, cell + 8);
        }
    }

    /// <summary>The band's ORIGINAL frames (MC 12222) — the only style boxes the HUD
    /// owns, from tokens this repo already paints with: 2E2117 dark wood, 5A564E
    /// iron, 7A5A2A thatch, C9A227 gold. The IP wall holds: the D2 LAYOUT is
    /// followed; no D2 asset is shipped, referenced or imitated.</summary>
    private static class Iron
    {
        /// <summary>The band's plank: dark wood under an iron border with ZERO
        /// content margins, so the row inside IS the band's rect — the exact
        /// number the leg pins, not a rect minus a style box's implicit inset.</summary>
        public static StyleBoxFlat Wood()
        {
            var b = Rim("5A564E");
            b.BgColor = new Color("2E2117", 0.94f);
            b.BorderWidthBottom = 0; // the plank runs off the screen's bottom edge
            b.ContentMarginLeft = b.ContentMarginRight = b.ContentMarginTop = b.ContentMarginBottom = 0;
            return b;
        }

        /// <summary>Dark wood inside a named 1 px rim — the belt cells (thatch) and
        /// the level chip (gold) are the SAME frame, one constructor.</summary>
        public static StyleBoxFlat Rim(string borderHex) => new()
        {
            BgColor = new Color("2E2117", 0.88f),
            BorderColor = new Color(borderHex),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 2, ContentMarginBottom = 2,
        };
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
        // MC 12222 THE BAND's pulls: the strip's pair (xp INTO the level over the
        // level's span — the two literals the "XP a/b" row prints), the chip's
        // "lvl N" word, and the four belt letters. Dotless like every field here:
        // a paint value, never a session write (the GameInputTests StateWrite law).
        public long XpInto, XpSpan;
        public string LevelText = "";
        public string[] BeltLetters = System.Array.Empty<string>();
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
            // MC 12222 THE BAND's three reads, same pull. XpSpan 0 IS max — the
            // strip law there is a FULL bar, stated at XpFillPx, not a silent empty one.
            XpInto = hi > lo ? xp - lo : 0, XpSpan = hi > lo ? hi - lo : 0,
            LevelText = hi > lo ? $"lvl {s.Level}" : $"lvl {s.Level} MAX",
            BeltLetters = s.Belt.Select(item => item is null || item.Base.Name.Length == 0
                ? "" : char.ToUpperInvariant(item.Base.Name[0]).ToString()).ToArray(),
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

    /// <summary>The XP strip's fill px (MC 12222): OrbFillPx is "current over max
    /// into an extent" and never cared which way the bar runs, so the strip REUSES
    /// it rather than growing a second fill scale. The one honest difference is
    /// MAX: with no headroom left (span 0) the strip paints FULL, not empty.</summary>
    public static long XpFillPx(long intoLevel, long span, int widthPx) =>
        span <= 0 ? widthPx : OrbFillPx(intoLevel, span, widthPx);

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
            // MC 12222: EVERY part of the bottom band is a read-out too, so the
            // band's frame, orbs, cells, strip and chip all let world clicks
            // THROUGH — the bottom 90px stays the world's, decided by ClickMap.
            or "hud.band" or "hud.band-row" or "hud.band-slot" or "hud.band-chip"
            or "hud.band-xp" or "hud.band-xp-fill"
            // MC 11987: the town panel's GAMBLE-row offer label is a READ-OUT
            // (Ignore) — its row and button name themselves Stop in GambleRow.cs,
            // so the click stays inside the open panel either way.
            or "town.gamble-offer"
            ? "Ignore" : "Stop";

    /// <summary>The damage-flash text: the drop between the life the HUD LAST
    /// PAINTED and this pull — the same presentation delta as the LevelShown/
    /// BeltFilled/DeadShown read-back family (a heal or a no-move paints
    /// nothing; WHO dealt the blow stays the Sim's fact, never re-derived here).</summary>
    public static string DamageFlash(long lifeShown, long nowLife) =>
        nowLife >= lifeShown ? "" : $"-{lifeShown - nowLife} life";
}
