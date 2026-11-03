using System;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// HIRED FOLLOWER SPRITE OWNER — presentation-side (MC 11977 D5 runtime half;
/// MonsterField is the mirrored pattern). VISUAL ONLY: this class calls NO
/// sim door and writes NO session state (the FollowerFieldTests scan row
/// pins it enforceable, not narrated). The position law is the ORIGINAL
/// DESIGN fence in FollowerFieldLogic (SIM tracks no follower position): the
/// sprite stands on the deterministic adjacent cell PickCell derives from the
/// hero cell, re-deriving whenever the hero steps or the hire state changes
/// — the pull-only read is FollowerFieldLogic.ShouldDraw over the session's
/// own hire (GameContext.Session.Merc). Art rides the MonsterField idiom
/// VERBATIM: the mapped fol-<classkey>-idle sheet through the ArtOrNull
/// guard, first frame via AtlasTexture (the SAME two-frame 320×160 character
/// contract, MC 11975), missing art = 'art absent' PushWarning + palette
/// placeholder, never a crash; a class the map answers null for (witez,
/// rzutca — their sheets have not landed) stands the placeholder HONESTLY
/// (hero-parchment D8CBA6, the humanoid token colour, stated reading). No
/// animation is invented: the frame-swap idiom is MonsterField's — the first
/// idle frame, static. Prints NOTHING (headless anchors untouched by
/// construction); the leg reads the state through the accessors below.
/// </summary>
public partial class FollowerField : Node2D
{
    private Sprite2D? _sprite;
    private HeroController? _hero;
    private AreaRenderer? _tiles;

    // The pull-cache of the last paint: the re-derivation runs when the hero
    // cell, the hire key, or the draw law CHANGES — not every frame.
    private Vector2I _lastHero = new(int.MinValue, int.MinValue);
    private string _lastKey = "";
    private bool _lastDraw;
    private Vector2I? _cell;

    /// <summary>Wired by Act (the HeroController.Wire idiom): the two pulled
    /// presentation objects the law reads — the hero cell and the area's
    /// walkable set. References, never copies of state.</summary>
    public void Wire(HeroController hero, AreaRenderer tiles)
    {
        _hero = hero;
        _tiles = tiles;
    }

    /// <summary>Standing follower sprites — 1 while the law draws, else 0
    /// (the node itself, the MonsterField count-marker shape: a suppressed
    /// free stands as a ghost and moves this count).</summary>
    public int SpriteCount => _sprite is { } s && GodotObject.IsInstanceValid(s) ? 1 : 0;

    /// <summary>The cell the sprite stands on, or null while nothing draws —
    /// the leg's adjacency read (Chebyshev-1 from the hero, pinned by the
    /// PickCell law, provable per rebuild).</summary>
    public Vector2I? Cell => SpriteCount == 1 ? _cell : null;

    public override void _Process(double delta)
    {
        if (_hero is null || _tiles is null) return; // not wired — nothing to pull
        var merc = GameContext.Session?.Merc;
        bool draw = FollowerFieldLogic.ShouldDraw(merc);
        var key = merc?.Id ?? "";
        var hero = _hero.Cell;
        if (draw == _lastDraw && key == _lastKey && hero == _lastHero) return; // state unchanged — no re-derivation
        _lastDraw = draw;
        _lastKey = key;
        _lastHero = hero;
        Repaint(draw, key, hero);
    }

    private void Repaint(bool draw, string key, Vector2I hero)
    {
        // The deterministic adjacent cell — the ORIGINAL DESIGN position law
        // (FollowerFieldLogic header): first free cell in the PINNED ring
        // order over the area's own walkable set, never the hero's cell, null
        // when no neighbour is free (then NOTHING stands — no fake placement).
        _cell = null;
        if (draw)
        {
            var walkable = new System.Collections.Generic.List<(int X, int Y)>(_tiles!.Walkable.Count);
            foreach (var c in _tiles.Walkable) walkable.Add((c.X, c.Y));
            var picked = FollowerFieldLogic.PickCell((hero.X, hero.Y), walkable);
            if (picked is { } p) _cell = new Vector2I(p.X, p.Y);
        }
        if (_cell is null)
        {
            _sprite?.QueueFree();
            _sprite = null;
            return;
        }
        if (_sprite is not { } s || !GodotObject.IsInstanceValid(s))
        {
            // MonsterField's contract verbatim: feet at cell centre, scale
            // 1.0, first idle frame; Y-sort so the follower stands WITH the
            // hero in the depth order (the hero's own YSortEnabled idiom).
            _sprite = new Sprite2D { Centered = true, Offset = new Vector2(0, -80f), YSortEnabled = true };
            AddChild(_sprite);
            s = _sprite;
        }
        s.Texture = FollowerTexture(key);
        s.Position = IsoCamera.GridToScreen(_cell.Value);
    }

    /// <summary>The idle frame of the 320×160 two-frame sheets (the MC 11975
    /// fol-* contract, MonsterField.MonsterTexture mirrored): first frame via
    /// AtlasTexture at scale 1.0. No mapped path (witez/rzutca today) = the
    /// placeholder stands honestly; a mapped path whose file is missing takes
    /// the ArtOrNull guard's 'art absent' warning + the same placeholder.</summary>
    private static Texture2D FollowerTexture(string classKey)
    {
        var artPath = FollowerFieldLogic.ArtPathOrNull(classKey);
        var art = artPath is null ? null : AreaRenderer.ArtOrNull(artPath);
        if (art is null) return AreaRenderer.Diamond(new Color("D8CBA6"), 160, 160);
        return new AtlasTexture { Atlas = art, Region = new Rect2(0, 0, 160, 160) };
    }
}
