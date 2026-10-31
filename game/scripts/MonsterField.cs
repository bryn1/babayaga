using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// MONSTER SPRITE OWNER — presentation-side. The Sim stores NO monster positions
/// (§2 invariant), so the slice's monsters live ONLY here: idle sprites over
/// AreaRenderer's walkable cells, click hit-tests, and the clicked monster KEY
/// handed to HeroController. The slice area→monster-keys map is a code constant
/// { "mchy": pokrzywnik }; grodzisko is town (isTown → none spawn). The data-driven
/// per-area spawn table is the MONSTER-RENDERER DEBT (LEDGER row 77 in the run's
/// DEBT-ROW block). Spawner seed = Fnv.Hash(area + ":mon") — Fnv.Hash(s) ONLY.
/// </summary>
public partial class MonsterField : Node2D
{
    private static readonly Dictionary<string, string[]> SpawnMap = new()
    {
        ["mchy"] = ["pokrzywnik"], // the slice's only wilderness row
    };

    private const int Count = 4; // one idle row of N=4 sprites until the data table ships
    private const float HitRadius = 60f;

    private readonly List<Entry> _spawned = [];
    private Node2D? _row;

    private sealed record Entry(string Key, Vector2I Cell, Sprite2D Sprite);

    public void Build(string areaKey, bool isTown, IReadOnlyList<Vector2I> walkable)
    {
        _row?.QueueFree();
        _spawned.Clear();
        _row = new Node2D { Name = "Monsters" };
        AddChild(_row);
        if (isTown || !SpawnMap.TryGetValue(areaKey, out var keys) || walkable.Count == 0) return;

        var rng = new Random((int)Fnv.Hash(areaKey + ":mon"));
        for (int i = 0; i < Count; i++)
        {
            var cell = walkable[rng.Next(walkable.Count)];
            var key = keys[rng.Next(keys.Length)];
            var sprite = new Sprite2D
            {
                Texture = MonsterTexture(key),
                Centered = true,
                Offset = new Vector2(0, -80f), // feet at cell centre, scale 1.0 (§3 SPRITE DRAW SCALE)
                Position = IsoCamera.GridToScreen(cell),
            };
            _row.AddChild(sprite);
            _spawned.Add(new Entry(key, cell, sprite));
        }
    }

    /// <summary>The monster key under a world point, or null (click hit-test).</summary>
    public string? MonsterAt(Vector2 world)
    {
        foreach (var e in _spawned)
            if (e.Sprite.Position.DistanceTo(world) <= HitRadius
                && Math.Abs(e.Sprite.Position.Y - world.Y) <= 120f)
                return e.Key;
        return null;
    }

    /// <summary>The cell a spawned key stands on (loot-drop marker anchor), or null.</summary>
    public Vector2I? CellOf(string key)
    {
        foreach (var e in _spawned)
            if (e.Key == key) return e.Cell;
        return null;
    }

    /// <summary>
    /// The idle frame of the M1–M3 renders (320×160 = two 160×160 frames side by
    /// side): first frame via AtlasTexture, drawn at scale 1.0. Placeholder = a
    /// 160×160 diamond (scum-glow token) — ArtOrNull warns once, never crashes.
    /// </summary>
    private static Texture2D MonsterTexture(string key)
    {
        var art = AreaRenderer.ArtOrNull($"res://assets/monsters/mon-{key}-idle.png");
        if (art is null) return AreaRenderer.Diamond(new Color("46685A"), 160, 160);
        return new AtlasTexture { Atlas = art, Region = new Rect2(0, 0, 160, 160) };
    }
}
