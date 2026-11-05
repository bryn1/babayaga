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
/// MC 12136 (owner-feel): the hit-test circles the DRAWN body (torso), not the
/// feet anchor, and a KILLED CELL STAYS DEAD for the session — Despawn records
/// the freed cell (the same kill event that moves the Kills counter the
/// M5_S4_DESPAWN_OK marker reads; no second ledger, the Sim's fresh-per-kill MATH
/// untouched) and Build skips recorded cells, so an area rebuild/re-entry never
/// re-materializes a killed monster.
/// </summary>
public partial class MonsterField : Node2D
{
    // MC 12136 §6(b): the sprite draws 160×160 CENTERED 80 px ABOVE its Position
    // (Offset (0,-80)) — the old r=60 circle at the FEET anchor made every
    // torso/head click (distance ≥ 96) a silent miss. The circle now sits at the
    // DRAWN centre: r=90 covers the visible 160 px body, lands the feet anchor the
    // S4 leg clicks with margin (80 < 90, never a boundary float), and a ground
    // click BELOW the feet misses (the nearest south-cell centre is 32 px past the
    // anchor → 112 away): no double-hit of the click that means the ground.
    private const float BodyRise = 80f;  // the drawn body's centre, px above the feet anchor
    private const float HitRadius = 90f; // the visible body + 10 px click slack, centred at BodyRise
    private const int NearMax = 3;   // near-spawn band, Chebyshev from arrival cell
    private const int DeepMin = 5;   // deep band; the 4-ring between may stay empty

    /// <summary>One data-table spawn row, validated at load.</summary>
    private sealed record SpawnRow(string Kind, int Count, string Placement);

    // Loaded ONCE per process from the dataDir surface — the file never changes
    // while the game runs (one-way copy of repo data/, never hand-edited).
    private static Dictionary<string, List<SpawnRow>>? _table;

    private readonly SpriteRoster<Sprite2D> _roster = new();
    private Node2D? _row;
    private string _areaKey = "";
    // MC 12136 KILLS-STAY-DEAD: every cell freed by Despawn, keyed by area. The
    // ONE kill event this field has (Despawn — gated by the Sim's AttackReport
    // .Kills in HeroController, the same event the Kills counter / the
    // M5_S4_DESPAWN_OK marker ride) records it here; Build skips it for the rest
    // of the session, so an area rebuild/re-entry never re-materializes a killed
    // monster. Presentation-side by necessity: the Sim stores NO cells (§2) —
    // this is not a second ledger, it is that kill truth written where cells live.
    private readonly HashSet<(string Area, int X, int Y)> _killed = [];

    // M5-S7 FIX-DA5 10 count-marker state (the S7 row of plan-c2). Since MC 12136
    // the row stands at (data row − kills taken this session): Build counts every
    // sprite it stands, Despawn subtracts, the drift marker compares live nodes
    // against THAT expectation (a ghost free still moves the node count).
    private int _spawnRow;
    private bool _pendingCountMarker;
    private string _pendingPhase = "";
    private bool _sessionStarted;

    public void Build(string areaKey, bool isTown, IReadOnlyList<Vector2I> walkable, Vector2I arrivalCell)
    {
        _row?.QueueFree();
        _roster.Clear();
        _row = new Node2D { Name = "Monsters" };
        AddChild(_row);
        _areaKey = areaKey;
        _spawnRow = 0;
        _pendingCountMarker = false;
        var phase = _sessionStarted ? $"area-build:{areaKey}" : $"session-start:{areaKey}";
        _sessionStarted = true;
        if (isTown || walkable.Count == 0 || !Table().TryGetValue(areaKey, out var rows))
        {
            PrintSpriteMarker(phase); // town / no-row ground: the field's own law is 0 == 0
            return;
        }

        var rng = new Random((int)Fnv.Hash(areaKey + ":mon"));
        foreach (var row in rows)
        {
            var band = Band(walkable, arrivalCell, row.Placement);
            for (int i = 0; i < row.Count; i++)
            {
                var cell = band[rng.Next(band.Count)];
                // MC 12136: the rng draw is consumed either way — the SAME seed keeps
                // every SURVIVING monster on the SAME cell across rebuilds; only the
                // killed cells drop out of the standing row.
                if (_killed.Contains((areaKey, cell.X, cell.Y))) continue; // KILLS STAY DEAD
                _spawnRow++; // the data row commands this many STANDING sprites (kills subtract)
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
        PrintSpriteMarker(phase); // session start / every area build: the roster stands at its row
    }

    /// <summary>M5-S7 FIX-DA5 10 — the presentation-drift detector plan-c2 §2 S7
    /// names (quote: "M5_SPRITE_COUNT &lt;n&gt; == spawn-row &lt;m&gt; … at session start
    /// and after each kill-despawn"). The replay gate is structurally BLIND to the
    /// sprite roster (the Sim spawns a FRESH monster per kill — dumps cannot see
    /// sprites, the verdict's P4-A), so the field testifies for itself: live
    /// sprites STANDING in the field (the nodes, not roster bookkeeping — a
    /// suppressed free stands as a ghost and moves the node count) vs the spawn
    /// rows data/area-monsters.json commands for this area. GREEN prints the plan's
    /// literal '==' line; drift prints '!=' + PRESENTATION-DRIFT. The spawn-row
    /// side is the STANDING row — the data/area-monsters.json rows for this area
    /// as they stand (since MC 12136: minus the kills this session took there;
    /// kills staying dead is truth, not drift). WINDOWED ONLY —
    /// the headless story anchors' bytes are untouched (the exact headless check
    /// Boot.cs uses).</summary>
    private void PrintSpriteMarker(string phase)
    {
        if (DisplayServer.GetName() == "headless") return;
        int n = 0;
        if (_row is not null)
            foreach (var c in _row.GetChildren())
                if (GodotObject.IsInstanceValid(c)) n++;
        GD.Print(n == _spawnRow
            ? $"M5_SPRITE_COUNT {n} == spawn-row {_spawnRow} [{phase}]"
            : $"M5_SPRITE_COUNT {n} != spawn-row {_spawnRow} [{phase}] PRESENTATION-DRIFT");
    }

    /// <summary>The post-kill marker rides ONE FRAME: QueueFree lands at frame
    /// end, so counting now would still see the freed sprite (a false drift);
    /// one frame on, a legitimately freed sprite is gone and a SUPPRESSED free
    /// stands — the ghost class this marker exists to catch.</summary>
    public override void _Process(double delta)
    {
        if (!_pendingCountMarker) return;
        _pendingCountMarker = false;
        PrintSpriteMarker(_pendingPhase);
    }

    /// <summary>M5-S4 kill-despawn (plan FIX-DA5 5) as CURED by MC 12136 (owner
    /// complaint "i can't kill enemies…" / verdict §4: the kill was invisible — the
    /// freed sprite's cell re-materialized a FRESH instance the SAME frame, so the
    /// player who killed saw no change). The killed row's sprite is FREED (QueueFree)
    /// and the cell RECORDS its kill: it stays empty for the session — through
    /// rebuilds and re-entry (Build skips it). The Sim's own rule (a fresh monster
    /// per STRIKE volley — Combat.cs, the row never thins) stays the Sim's math,
    /// byte-identical; the presentation mirror simply stops lying about what the
    /// player killed. Kills counts kills, LastKilledFreed says the killed sprite
    /// object is DEAD, and the despawn is proven per INSTANCE.
    /// One kill takes exactly one sprite (the SpriteRoster rule, xunit-pinned).</summary>
    public void Despawn(string key)
    {
        Sprite2D? killed = null;
        (int X, int Y) cell = default;
        foreach (var e in _roster.Entries)
            if (e.Key == key) { killed = e.Sprite; cell = e.Cell; break; }
        if (killed is null || _roster.DespawnOne(key) is null) return;
        killed.QueueFree();
        _lastKilled = killed;
        Kills++;
        _killed.Add((_areaKey, cell.X, cell.Y)); // THE kill truth, recorded where cells live
        _spawnRow--; // the standing row is one thinner — the drift marker's expectation moves with it
        _pendingCountMarker = true; // M5-S7: the marker rides the frame after the free
        _pendingPhase = $"post-kill:{key}";
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

    /// <summary>The monster key under a world point, or null (click hit-test).
    /// MC 12136: the circle rides the DRAWN body (centre at BodyRise above the
    /// feet anchor, r=HitRadius) — torso and head clicks land; ground below the
    /// feet does not. Roster order answers, so one click takes one key.</summary>
    public string? MonsterAt(Vector2 world)
    {
        foreach (var e in _roster.Entries)
        {
            var p = e.Sprite.Position;
            if (new Vector2(p.X, p.Y - BodyRise).DistanceTo(world) <= HitRadius)
                return e.Key;
        }
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
