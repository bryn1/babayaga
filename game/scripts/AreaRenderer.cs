using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// One area's tile layer, rebuilt per visit. Display-only layout until MapGen
/// (LEDGER row 59) ships: the layout seed is Fnv.Hash(area key) — the ONE helper
/// (Fnv.cs), never a second FNV loop anywhere. Art loads from res://assets/tiles/
/// *.png through a ResourceLoader.Exists guard; missing art draws palette-colour
/// diamond placeholders and PushWarning ONCE per path, never a crash. Exposes its
/// walkable-cell list — MonsterField lays its row on it.
/// </summary>
public partial class AreaRenderer : Node2D
{
    public const int W = 16;
    public const int H = 16;

    /// <summary>Bobrowisko Dusk tokens (M4-placement §3) for the placeholder pass.</summary>
    private static readonly Color Grass = new("3E5230");
    private static readonly Color Water = new("16324A");
    private static readonly Color Stone = new("4D4A44");
    private static readonly Color Wall = new("2E2117");

    private static readonly HashSet<string> Warned = [];

    private readonly List<Vector2I> _walkable = [];
    private readonly HashSet<Vector2I> _blocked = [];
    private TileMapLayer? _layer;

    /// <summary>The cells a hero may stand on (door edges included).</summary>
    public IReadOnlyList<Vector2I> Walkable => _walkable;

    public void Build(string areaKey, bool isTown)
    {
        _walkable.Clear();
        _blocked.Clear();

        // Display-only layout: a seeded scatter of blocked cells inside an always-
        // walkable border (the door edges live on the border).
        var rng = new Random((int)Fnv.Hash(areaKey));
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                bool border = x == 0 || y == 0 || x == W - 1 || y == H - 1;
                var cell = new Vector2I(x, y);
                if (!border && rng.NextDouble() < 0.22) _blocked.Add(cell);
                else _walkable.Add(cell);
            }

        _layer?.QueueFree();
        _layer = new TileMapLayer { Name = "Tiles", TileSet = MakeTileSet(isTown) };
        AddChild(_layer);
        foreach (var cell in _walkable) _layer.SetCell(cell, 0);
        foreach (var cell in _blocked) _layer.SetCell(cell, 1);
    }

    public bool IsWalkable(Vector2I c) =>
        c.X >= 0 && c.Y >= 0 && c.X < W && c.Y < H && !_blocked.Contains(c);

    /// <summary>The walkable cell nearest the grid centre — the hero's arrival spot.</summary>
    public Vector2I CenterCell()
    {
        var best = _walkable.Count > 0 ? _walkable[0] : new Vector2I(W / 2, H / 2);
        int bestD = int.MaxValue;
        foreach (var c in _walkable)
        {
            int d = Math.Max(Math.Abs(c.X - W / 2), Math.Abs(c.Y - H / 2));
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    /// <summary>The first walkable cell on edge 0=east, 1=south, 2=west, 3=north.</summary>
    public Vector2I? DoorCell(int edge)
    {
        for (int i = 0; i < (edge == 0 || edge == 2 ? H : W); i++)
        {
            var c = edge switch
            {
                0 => new Vector2I(W - 1, i),
                1 => new Vector2I(i, H - 1),
                2 => new Vector2I(0, i),
                _ => new Vector2I(i, 0),
            };
            if (IsWalkable(c)) return c;
        }
        return null;
    }

    private TileSet MakeTileSet(bool isTown)
    {
        var walkTex = ScaledToCell(ArtOrNull(isTown ? TownArt : FieldArt),
            isTown ? Stone : Grass);
        var blockTex = ScaledToCell(ArtOrNull(isTown ? TownEdgeArt : FieldEdgeArt),
            isTown ? Wall : Water);
        var ts = new TileSet { TileSize = new Vector2I(IsoCamera.TileW, IsoCamera.TileH) };
        ts.AddSource(MakeSource(walkTex), 0);
        ts.AddSource(MakeSource(blockTex), 1);
        return ts;
    }

    private const string FieldArt = "res://assets/tiles/tile-grass.png";
    private const string FieldEdgeArt = "res://assets/tiles/tile-water.png";
    private const string TownArt = "res://assets/tiles/tile-stone.png";
    private const string TownEdgeArt = "res://assets/tiles/tile-wall.png";

    private static TileSetAtlasSource MakeSource(Texture2D tex)
    {
        var src = new TileSetAtlasSource
        {
            Texture = tex,
            TextureRegionSize = new Vector2I(IsoCamera.TileW, IsoCamera.TileH),
        };
        src.CreateTile(Vector2I.Zero);
        return src;
    }

    /// <summary>
    /// The ONE art guard: ResourceLoader.Exists, else PushWarning ONCE per path and
    /// null (placeholder pass). Also the TILE SCALE CONTRACT's home: tile art is
    /// 256×128 and this bakes the draw-scale 0.5 by resizing to the 128×64 cell —
    /// exactly 2× on both axes, neither side re-interpreted.
    /// </summary>
    public static Texture2D? ArtOrNull(string path)
    {
        if (ResourceLoader.Exists(path)) return GD.Load<Texture2D>(path);
        if (Warned.Add(path)) GD.PushWarning($"art absent: {path} — placeholder pass");
        return null;
    }

    /// <summary>Tile art (256×128) → cell size (128×64) = the pinned scale 0.5.</summary>
    public static Texture2D ScaledToCell(Texture2D? art, Color fallback)
    {
        if (art is null) return Diamond(fallback, IsoCamera.TileW, IsoCamera.TileH);
        var img = art.GetImage();
        img.Resize(IsoCamera.TileW, IsoCamera.TileH, Image.Interpolation.Lanczos);
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>A generated iso diamond in a flat colour — the placeholder shape.</summary>
    public static Texture2D Diamond(Color color, int w, int h)
    {
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        var clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Math.Abs(x - w / 2f) / (w / 2f) + Math.Abs(y - h / 2f) / (h / 2f);
                img.SetPixel(x, y, d <= 1f ? color : clear);
            }
        return ImageTexture.CreateFromImage(img);
    }
}
