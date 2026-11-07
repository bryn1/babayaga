using System.Text.Json;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// CZELUSC DEFENDERS (MC 11954, LEDGER row 77 data beat): the dark lair of the
/// glowa rite (act1-areas: czelusc "Czelusc Leszego", level 8, isBossLair) had NO
/// spawn rows in data/area-monsters.json — the story leg printed MONSTER_SPAWN_NONE
/// there (the honest base fact this card turns). These rows pin the lair's DEFENDERS
/// as DATA: exactly two rows in file order — strzyga ×3 near-spawn + widlast ×2 deep
/// — kinds ONLY from the existing data/monsters.json roster (the two art-debt deep-
/// area kinds, LEDGER row 85 D3/D4, the natural lair kin; the boss leszy stays OUT:
/// boss:true is quest-gated, a field row would farm it — stated reading, row below).
/// ORIGINAL DESIGN FENCE: the kind-choice, counts and bands are ORIGINAL to this
/// world (D1 weapon-curve precedent: design values ship fenced as ORIGINAL, never
/// presented as reference-copied) — restated in the data file _comment and the LEDGER row.
/// The spawn CELLS are presentation-side truth the Sim never stores (§2 invariant);
/// MonsterField builds them from Fnv.Hash(area+":mon") over the AreaRenderer walkable
/// scatter. The generator below PORTS that determinism chain (Fnv.cs rides the test
/// project as a LINK — the one hash, no second implementation) and its FIDELITY TO
/// THE LIVE ENGINE IS PINNED: the mchy/pokrzywlasy/kopcowisko literals below are the
/// MONSTER_SPAWN lines of the anchor story run (stdout md5 52af39886c0110a6d038068eb78a9429,
/// out/.tmp/cz/base-story1.out lines 9–24/178–182/209–213) — a layout or spawner drift
/// without a re-review goes RED here, not silently. RED-FIRST on base: every czelusc
/// row fails on the pristine tree (no czelusc entry); the port-fidelity and
/// determinism rows are green there on purpose — they gate the mechanism, not the data.
/// The game/data twin byte-equality stays the SkeletonDataTests.GameDataCopy_IsVerbatim
/// gate (not duplicated here).
/// </summary>
public sealed class CzeluscDefenderTests
{
    private static string Data(string file) => Path.Combine(TestData.DataDir(), file);

    // ---- the ported presentation chain (one concern: reproduce what MonsterField
    // builds — the cited originals are the shipped game files; fidelity rows pin it) ----

    private const int W = 26;                 // AreaRenderer.FieldW (MC 12225 re-pin: fields 26×26; the town pair AreaRenderer.TownW/TownH=20 never applies HERE — the port walks no town, towns spawn nothing per the isTown row below)
    private const int H = 26;                 // AreaRenderer.FieldH (the lair + the three anchor areas are ALL fields — one extent pair serves the whole port)
    private const double BlockChance = 0.22;  // AreaRenderer.Build scatter probability
    private const int NearMax = 3;            // MonsterField.NearMax (Chebyshev)
    private const int DeepMin = 5;            // MonsterField.DeepMin (Chebyshev)

    private record struct Cell(int X, int Y);
    private static int Chebyshev(Cell a, Cell b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)); // the Sim's ONE metric, presentation-side twin

    /// AreaRenderer.Build: border always walkable, inner blocked at rng &lt; 0.22,
    /// seeded Fnv.Hash(areaKey); the list order is x-major, y-minor — Band() indexes it, so it is law.
    private static List<Cell> Walkable(string areaKey)
    {
        var walkable = new List<Cell>();
        var rng = new Random((int)Fnv.Hash(areaKey));
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                bool border = x == 0 || y == 0 || x == W - 1 || y == H - 1;
                if (!border && rng.NextDouble() < BlockChance) continue; // blocked
                walkable.Add(new Cell(x, y));
            }
        return walkable;
    }

    /// AreaRenderer.CenterCell — the arrival cell Act.Rebuild hands to MonsterField.
    private static Cell CenterCell(List<Cell> walkable)
    {
        var best = walkable[0];
        int bestD = int.MaxValue;
        foreach (var c in walkable)
        {
            int d = Chebyshev(c, new Cell(W / 2, H / 2));
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    /// MonsterField.Band — cells within the placement band; an empty band falls back
    /// to ALL walkable cells (the fallback must never fire on czelusc: pinned below).
    private static List<Cell> Band(List<Cell> walkable, Cell arrival, string placement)
    {
        var picked = new List<Cell>();
        foreach (var c in walkable)
            if (placement == "deep" ? Chebyshev(c, arrival) >= DeepMin : Chebyshev(c, arrival) <= NearMax)
                picked.Add(c);
        return picked.Count > 0 ? picked : new List<Cell>(walkable);
    }

    /// MonsterField.Build: rows in FILE ORDER, one spawner rng over the whole area,
    /// seeded Fnv.Hash(area + ":mon"), one band rng.Next pick per commanded sprite.
    /// Returns (kind, cell) per standing sprite — the spawn row as RowSnapshot would answer.
    private static List<(string Kind, Cell Cell)> BuildRows(string areaKey)
    {
        var built = new List<(string, Cell)>();
        using var doc = JsonDocument.Parse(File.ReadAllText(Data("area-monsters.json")));
        if (!doc.RootElement.GetProperty("areas").TryGetProperty(areaKey, out var rowsEl))
            return built; // the base tree's honest answer: no row, no sprites
        var walkable = Walkable(areaKey);
        var arrival = CenterCell(walkable);
        var rng = new Random((int)Fnv.Hash(areaKey + ":mon"));
        foreach (var r in rowsEl.EnumerateArray())
        {
            var kind = r.GetProperty("kind").GetString()!;
            int count = r.GetProperty("count").GetInt32();
            var band = Band(walkable, arrival, r.GetProperty("placement").GetString()!);
            for (int i = 0; i < count; i++)
                built.Add((kind, band[rng.Next(band.Count)]));
        }
        return built;
    }

    /// The exact story stdout shape (JourneyLeg/Act1OpenLeg print): the fidelity rows
    /// cmp these strings against the anchor capture, byte-for-byte.
    private static string[] StoryLines(string areaKey)
    {
        var row = BuildRows(areaKey);
        var lines = new string[row.Count];
        for (int i = 0; i < row.Count; i++)
            lines[i] = $"MONSTER_SPAWN i={i} key={row[i].Kind} cell={row[i].Cell.X},{row[i].Cell.Y}";
        return lines;
    }

    // ---- data reads: the roster and the area row, both READ-ONLY files ----

    private static (int OwnLevel, bool Boss, bool KeepsOwnLevel) MonsterRow(string key)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Data("monsters.json")));
        foreach (var m in doc.RootElement.GetProperty("monsters").EnumerateArray())
            if (m.GetProperty("key").GetString() == key)
                return (m.GetProperty("ownLevel").GetInt32(),
                        m.GetProperty("boss").GetBoolean(),
                        m.GetProperty("keepsOwnLevel").GetBoolean());
        throw new InvalidOperationException($"kind '{key}' is NOT in the data/monsters.json roster");
    }

    private static (int Level, bool IsTown, bool IsBossLair) AreaRow(string key)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Data("act1-areas.json")));
        foreach (var a in doc.RootElement.GetProperty("areas").EnumerateArray())
            if (a.GetProperty("key").GetString() == key)
                return (a.GetProperty("level").GetInt32(),
                        a.GetProperty("isTown").GetBoolean(),
                        a.GetProperty("isBossLair").GetBoolean());
        throw new InvalidOperationException($"area '{key}' is NOT in data/act1-areas.json");
    }

    private static (string Kind, int Count, string Placement)[] AreaRows(string key)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Data("area-monsters.json")));
        if (!doc.RootElement.GetProperty("areas").TryGetProperty(key, out var rowsEl))
            return [];
        return rowsEl.EnumerateArray()
            .Select(r => (r.GetProperty("kind").GetString()!,
                          r.GetProperty("count").GetInt32(),
                          r.GetProperty("placement").GetString()!))
            .ToArray();
    }

    // ================= czelusc data pins (RED on base: the lair had no rows) =================

    [Fact]
    public void Czelusc_Has_Defender_Rows_In_File_Order()
    {
        var rows = AreaRows("czelusc");
        Assert.Equal(
            new[] { ("strzyga", 3, "near-spawn"), ("widlast", 2, "deep") }, rows);
    }

    [Fact]
    public void Czelusc_Kinds_Are_Existing_Roster_Kinds_And_No_Boss()
    {
        // defenders ride EXISTING stat rows (monsters.json READ-ONLY): no new kind,
        // no new numbers. The boss leszy (boss:true, keepsOwnLevel) is NOT spawnable
        // as a field defender — a fresh-monster-per-kill field would farm the rite
        // final; its absence is law, not omission.
        foreach (var (kind, _, _) in AreaRows("czelusc"))
        {
            var (_, boss, _) = MonsterRow(kind); // throws if the kind has no stat row at all
            Assert.False(boss, $"{kind} is boss:true — bosses do not ride spawn rows");
        }
        Assert.Equal(2, AreaRows("czelusc").Length); // never a vacuous pass over an empty lair
    }

    [Fact]
    public void Czelusc_Counts_Match_Kopcowisko_Density()
    {
        // "count/density comparable to kopcowisko" — derived, not literal: same row
        // count, same per-placement shape (near 3 + deep 2), same total standing sprites.
        var czelusc = AreaRows("czelusc");
        var kopce = AreaRows("kopcowisko");
        Assert.Equal(kopce.Length, czelusc.Length);
        Assert.Equal(kopce.Sum(r => r.Count), czelusc.Sum(r => r.Count));
        Assert.Equal(
            kopce.GroupBy(r => r.Placement).Select(g => g.Sum(r => r.Count)).OrderBy(x => x),
            czelusc.GroupBy(r => r.Placement).Select(g => g.Sum(r => r.Count)).OrderBy(x => x));
    }

    [Fact]
    public void Czelusc_Kind_Levels_Ride_The_Depth_Curve()
    {
        // ORIGINAL DESIGN fence (D1 weapon-curve precedent): the band below is this
        // card's design reading of "the lair sits above the mounds" — the kinds are
        // EXISTING roster rows, the choice of WHICH is original and stays fenced.
        var (lairLevel, isTown, isBossLair) = AreaRow("czelusc");
        Assert.False(isTown);   // a town spawns nothing whatever the table says — the lair must not be one
        Assert.True(isBossLair); // it IS the lair the glowa rite walks into
        var ownLevels = AreaRows("czelusc").Select(r => MonsterRow(r.Kind).OwnLevel).ToArray();
        Assert.All(ownLevels, lvl =>
            Assert.True(lvl <= lairLevel, $"defender ownLevel {lvl} overshoots the lair level {lairLevel}"));
        Assert.Contains(lairLevel, ownLevels); // lair kin stand AT the lair level (widlast 8 @ czelusc 8)
        int moundsMax = AreaRows("kopcowisko").Select(r => MonsterRow(r.Kind).OwnLevel).Max();
        Assert.True(ownLevels.Min() > moundsMax,
            "the lair band must sit ABOVE the mound band (depth curve: czelusc 8 > kopcowisko 6)");
    }

    [Fact]
    public void Czelusc_Spawn_Cells_Are_Legal_And_In_Bands()
    {
        // cells-in-bounds through the ported chain (fidelity pinned below): the exact
        // five sprites the field will stand — cells inside the 16×16 grid, walkable in
        // the deterministic czelusc layout, each inside its commanded band, bands non-
        // empty (no fallback), count == sum of commanded counts.
        var walkable = Walkable("czelusc");
        var arrival = CenterCell(walkable);
        var row = BuildRows("czelusc");
        var spec = AreaRows("czelusc");
        Assert.Equal(5, row.Count);        // the lair stands five defenders (never a vacuous walk over zero)
        Assert.Equal(spec.Sum(r => r.Count), row.Count); // every commanded sprite stands
        Assert.NotEmpty(Band(walkable, arrival, "near-spawn"));
        Assert.NotEmpty(Band(walkable, arrival, "deep"));
        int k = 0;
        foreach (var (kind, count, placement) in spec)
        {
            for (int i = 0; i < count; i++, k++)
            {
                var (rKind, cell) = row[k];
                Assert.Equal(kind, rKind); // file order preserved
                Assert.InRange(cell.X, 0, W - 1);
                Assert.InRange(cell.Y, 0, H - 1);
                Assert.Contains(cell, walkable); // never a sprite on a blocked cell
                int d = Chebyshev(cell, arrival);
                if (placement == "deep") Assert.True(d >= DeepMin, $"deep sprite at Chebyshev {d}");
                else Assert.True(d <= NearMax, $"near sprite at Chebyshev {d}");
            }
        }
    }

    [Fact]
    public void Czelusc_Spawn_Row_Is_The_Full_Commanded_Row()
    {
        // the lair stands EXACTLY this spawn row, pinned to the cell (the same
        // stdout shape the legs print). These five literals are the LAW, verified
        // two ways: the same cell-exact chain the fidelity rows pin to the live
        // story reproduces them deterministically, AND a throwaway print-probe of
        // the existing walk door printed THESE FIVE, byte-equal, from the real
        // engine twice (out/.tmp/cz/probe-story{1,2}.out — the copy was removed).
        Assert.Equal(
        [
            "MONSTER_SPAWN i=0 key=strzyga cell=14,13",
            "MONSTER_SPAWN i=1 key=strzyga cell=11,13",
            "MONSTER_SPAWN i=2 key=strzyga cell=14,11",
            "MONSTER_SPAWN i=3 key=widlast cell=24,25",
            "MONSTER_SPAWN i=4 key=widlast cell=18,7",
        ], StoryLines("czelusc"));
    }

    // ================= the port's live-engine fidelity (green on base on purpose) =================

    // The anchor story's OWN lines — out/.tmp/cz/base-story1.out (stdout md5
    // 52af39886c0110a6d038068eb78a9429): mchy at 9–24 (JourneyLeg), pokrzywlasy at
    // 178–182 and kopcowisko at 209–213 (Act1OpenLeg.PrintSpawns). Captured FIRST on
    // the pristine card/11954 base, per the anchor law.
    private static readonly Dictionary<string, string[]> AnchorStoryLines = new()
    {
        ["mchy"] =
        [
            "MONSTER_SPAWN i=0 key=pokrzywnik cell=12,15",
            "MONSTER_SPAWN i=1 key=pokrzywnik cell=15,12",
            "MONSTER_SPAWN i=2 key=pokrzywnik cell=16,16",
            "MONSTER_SPAWN i=3 key=pokrzywnik cell=15,11",
            "MONSTER_SPAWN i=4 key=pokrzywnik cell=12,15",
            "MONSTER_SPAWN i=5 key=pokrzywnik cell=10,11",
            "MONSTER_SPAWN i=6 key=pokrzywnik cell=15,11",
            "MONSTER_SPAWN i=7 key=pokrzywnik cell=11,11",
            "MONSTER_SPAWN i=8 key=pokrzywnik cell=10,11",
            "MONSTER_SPAWN i=9 key=pokrzywnik cell=14,11",
            "MONSTER_SPAWN i=10 key=pokrzywnik cell=12,12",
            "MONSTER_SPAWN i=11 key=pokrzywnik cell=11,11",
            "MONSTER_SPAWN i=12 key=pokrzywnik cell=10,14",
            "MONSTER_SPAWN i=13 key=pokrzywnik cell=12,12",
            "MONSTER_SPAWN i=14 key=ponurak cell=8,17",
            "MONSTER_SPAWN i=15 key=ponurak cell=13,24",
        ],
        ["pokrzywlasy"] =
        [
            "MONSTER_SPAWN i=0 key=pokrzywnik cell=15,13",
            "MONSTER_SPAWN i=1 key=pokrzywnik cell=12,11",
            "MONSTER_SPAWN i=2 key=pokrzywnik cell=10,15",
            "MONSTER_SPAWN i=3 key=kikimora cell=25,9",
            "MONSTER_SPAWN i=4 key=kikimora cell=7,24",
        ],
        ["kopcowisko"] =
        [
            "MONSTER_SPAWN i=0 key=ponurak cell=16,10",
            "MONSTER_SPAWN i=1 key=ponurak cell=15,16",
            "MONSTER_SPAWN i=2 key=ponurak cell=13,15",
            "MONSTER_SPAWN i=3 key=kretownik cell=16,22",
            "MONSTER_SPAWN i=4 key=kretownik cell=22,25",
        ],
    };

    public static TheoryData<string> AnchorAreas()
    {
        var t = new TheoryData<string>();
        foreach (var key in AnchorStoryLines.Keys.OrderBy(k => k)) t.Add(key);
        return t;
    }

    [Theory]
    [MemberData(nameof(AnchorAreas))]
    public void Port_Fidelity_Reproduces_Live_Story_Spawn_Lines(string areaKey)
    {
        // the port is only a PORT while the anchor lines hold: reproduce the live
        // engine's spawn stdout byte-for-byte from the data files alone.
        Assert.Equal(AnchorStoryLines[areaKey], StoryLines(areaKey));
    }
}
