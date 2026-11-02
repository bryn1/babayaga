using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

/// <summary>
/// MONSTER SPRITE OWNER — presentation-side. The Sim stores NO monster positions
/// (§2 invariant), so the area's monsters live ONLY here: idle sprites over
/// AreaRenderer's walkable cells, click hit-tests, and the clicked monster KEY
/// handed to HeroController. The per-area spawn rows are DATA (M4-S3, LEDGER
/// debt row 77 first payment): data/area-monsters.json rows {kind, count,
/// placement} loaded through the GameContext.DataDir() surface — the constant
/// { "mchy": pokrzywnik } is REMOVED (grodzisko is town: isTown → none spawn,
/// whatever the file says). placement "near-spawn" = Chebyshev <= NearMax from
/// the arrival cell, "deep" = >= DeepMin; an empty band falls back to all
/// walkable cells; bad rows warn once per build and are skipped, never crash.
/// Rows build in file order; spawner seed = Fnv.Hash(area + ":mon") — Fnv.Hash(s)
/// ONLY. Spawner keys are the monsters.json keys (Sim Attack looks them up there).
/// </summary>
public partial class MonsterField : Node2D
{
    private const float HitRadius = 60f;
    private const int NearMax = 3;   // near-spawn band, Chebyshev from arrival cell
    private const int DeepMin = 5;   // deep band; the 4-ring between may stay empty

    /// <summary>One data-table spawn row, validated at load.</summary>
    private sealed record SpawnRow(string Kind, int Count, string Placement);

    // Loaded ONCE per process from the dataDir surface — the file never changes
    // while the game runs (one-way copy of repo data/, never hand-edited).
    private static Dictionary<string, List<SpawnRow>>? _table;

    private readonly SpriteRoster<Sprite2D> _roster = new();
    private Node2D? _row;

    public void Build(string areaKey, bool isTown, IReadOnlyList<Vector2I> walkable, Vector2I arrivalCell)
    {
        _row?.QueueFree();
        _roster.Clear();
        _row = new Node2D { Name = "Monsters" };
        AddChild(_row);
        if (isTown || walkable.Count == 0) return;
        if (!Table().TryGetValue(areaKey, out var rows)) return;

        var rng = new Random((int)Fnv.Hash(areaKey + ":mon"));
        foreach (var row in rows)
        {
            var band = Band(walkable, arrivalCell, row.Placement);
            for (int i = 0; i < row.Count; i++)
            {
                var cell = band[rng.Next(band.Count)];
                var sprite = new Sprite2D
                {
                    Texture = MonsterTexture(row.Kind),
                    Centered = true,
                    Offset = new Vector2(0, -80f), // feet at cell centre, scale 1.0 (§3 SPRITE DRAW SCALE)
                    Position = IsoCamera.GridToScreen(cell),
                };
                _row.AddChild(sprite);
                _roster.Add(row.Kind, cell.X, cell.Y, sprite);
            }
        }
    }

    /// <summary>M5-S4 kill-despawn (plan FIX-DA5 5, the S4-owned row): the killed
    /// row's sprite is FREED (QueueFree) and, because the Sim's own rule is a
    /// FRESH monster per kill (Combat.cs header — the row never thins; a headless
    /// leg strikes one row ≈550 times), the field answers with a FRESH instance
    /// of the same row on the same cell. A permanently shrinking field would
    /// contradict the Sim's truth (mchy is still full of monsters after a kill)
    /// and would starve every click-attack target. The despawn is proven per
    /// INSTANCE, not per count: Kills counts kills, LastKilledFreed says the
    /// killed sprite object is DEAD, and the roster order swaps at the cell.
    /// One kill takes exactly one sprite (the SpriteRoster rule, xunit-pinned).</summary>
    public void Despawn(string key)
    {
        Sprite2D? killed = null;
        (int X, int Y) cell = default;
        foreach (var e in _roster.Entries)
            if (e.Key == key) { killed = e.Sprite; cell = e.Cell; break; }
        if (killed is null || _roster.DespawnOne(key) is null) return;
        var tex = killed.Texture; // read BEFORE the free — a freed object answers nothing
        killed.QueueFree();
        _lastKilled = killed;
        Kills++;
        var fresh = new Sprite2D
        {
            Texture = tex,
            Centered = true,
            Offset = new Vector2(0, -80f),
            Position = IsoCamera.GridToScreen(new Vector2I(cell.X, cell.Y)),
        };
        _row?.AddChild(fresh);
        _roster.Add(key, cell.X, cell.Y, fresh);
    }

    /// <summary>Kills this field has taken (the per-instance despawn counter).</summary>
    public int Kills { get; private set; }

    private Sprite2D? _lastKilled;

    /// <summary>True once the QueueFree has landed — the killed sprite object is
    /// gone (frame after the kill): the despawn, proven per instance.</summary>
    public bool LastKilledFreed => _lastKilled is { } s && !GodotObject.IsInstanceValid(s);

    /// <summary>Standing sprites for the S7 count marker (spawn-row read).</summary>
    public int SpriteCount => _roster.Count;

    /// <summary>The built spawn row (key + cell per entry) — the headless leg's read
    /// for the Fnv-seed determinism proof. Presentation-side truth, never Sim state.</summary>
    public List<(string Key, Vector2I Cell)> RowSnapshot()
    {
        var row = new List<(string Key, Vector2I Cell)>(_roster.Count);
        foreach (var e in _roster.Entries) row.Add((e.Key, new Vector2I(e.Cell.X, e.Cell.Y)));
        return row;
    }

    /// <summary>The monster key under a world point, or null (click hit-test).</summary>
    public string? MonsterAt(Vector2 world)
    {
        foreach (var e in _roster.Entries)
            if (e.Sprite.Position.DistanceTo(world) <= HitRadius
                && Math.Abs(e.Sprite.Position.Y - world.Y) <= 120f)
                return e.Key;
        return null;
    }

    /// <summary>The walkable cells of one placement band around the arrival cell
    /// (Chebyshev, the one metric — same inline form as AreaRenderer.CenterCell);
    /// an empty band falls back to the full walkable list, never an empty spawn.</summary>
    private static List<Vector2I> Band(IReadOnlyList<Vector2I> walkable, Vector2I arrivalCell, string placement)
    {
        var picked = new List<Vector2I>();
        foreach (var c in walkable)
        {
            int d = Math.Max(Math.Abs(c.X - arrivalCell.X), Math.Abs(c.Y - arrivalCell.Y));
            if (placement == "deep" ? d >= DeepMin : d <= NearMax) picked.Add(c);
        }
        if (picked.Count > 0) return picked;
        GD.PushWarning($"area-monsters: '{placement}' band empty — falling back to all walkable cells");
        return new List<Vector2I>(walkable);
    }

    /// <summary>Loads data/area-monsters.json through the GameContext.DataDir()
    /// surface (the same read path GameSession.New/Load use). A missing or broken
    /// file warns ONCE and yields an empty table — the placeholder-pass idiom.</summary>
    private static Dictionary<string, List<SpawnRow>> Table()
    {
        if (_table is not null) return _table;
        _table = [];
        try
        {
            var path = Path.Combine(GameContext.DataDir(), "area-monsters.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var area in doc.RootElement.GetProperty("areas").EnumerateObject())
            {
                var rows = new List<SpawnRow>();
                foreach (var r in area.Value.EnumerateArray())
                {
                    var kind = r.GetProperty("kind").GetString() ?? "";
                    var placement = r.GetProperty("placement").GetString() ?? "";
                    int count = r.GetProperty("count").GetInt32();
                    if (kind.Length == 0 || count < 1 || (placement != "deep" && placement != "near-spawn"))
                    {
                        GD.PushWarning($"area-monsters: {area.Name} row '{kind}' invalid — skipped");
                        continue;
                    }
                    rows.Add(new SpawnRow(kind, count, placement));
                }
                if (rows.Count > 0) _table[area.Name] = rows;
            }
        }
        catch (Exception e)
        {
            GD.PushWarning($"area-monsters.json absent or broken — no spawn rows: {e.Message}");
        }
        return _table;
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
