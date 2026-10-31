using System.Text.Json;

namespace BabaYaga.Sim;

/// The four kinds one treasure-class entry can be (P2 §4.9 walk step).
public enum TcEntryKind
{
    NoDrop,
    Gold,
    Group,
    Item,
}

/// One weighted entry of a treasure class. Ref is the base id (Item) or the
/// subgroup key (Group); null for NoDrop and Gold (gold lives on the row).
public sealed record TreasureClassEntry(TcEntryKind Kind, int Weight, string? Ref);

/// <summary>
/// One treasure class (data/treasureclasses.json, P2 §4.9): Picks walks per roll
/// — NEGATIVE Picks mean a forced (guaranteed-drop) walk that never draws NoDrop;
/// MinIlvl floors the ilvl of items this class gives; Gold is the class's nugget
/// pile (nuggets × multiplier/256, then × the killer's GoldFind, P2 §4.8).
/// </summary>
public sealed record TreasureClassRow(
    string Key,
    string Name,
    int Picks,
    int MinIlvl,
    long GoldNuggets,
    int GoldMultiplier,
    IReadOnlyList<TreasureClassEntry> Entries)
{
    /// Negative Picks = guaranteed-drop walk (P2 §4.9 VERIFIED).
    public bool IsForced => Picks < 0;
}

/// What one kill's walk produced: at most <see cref="TreasureClassTable.MaxOutputs"/>
/// outputs counted WITH gold, plus the gold piles summed.
public sealed record TreasureClassDrop(IReadOnlyList<Item> Items, long Gold);

/// <summary>
/// Loads data/treasureclasses.json and walks it (P2 §4.9): per roll, |Picks| draws
/// (NoDrop excluded on forced classes), each draw a frequency-weighted entry pick;
/// a Group draw recurses with its own Picks; the whole walk never yields more than
/// 6 outputs including gold. Loading IS the content gate: unique keys, positive
/// weights, resolvable refs, no group cycles, and forced classes that would
/// contradict their guarantee (a NoDrop entry) fail the load.
/// </summary>
public sealed class TreasureClassTable
{
    /// ≤6 outputs fall per kill, gold included (P2 §4.9 VERIFIED).
    public const int MaxOutputs = 6;

    private readonly Dictionary<string, TreasureClassRow> _rows;

    private TreasureClassTable(List<TreasureClassRow> rows) => _rows = rows.ToDictionary(r => r.Key);

    public TreasureClassRow For(string key) => _rows.TryGetValue(key, out var row)
        ? row
        : throw new KeyNotFoundException($"treasure class '{key}' not in table");

    public static TreasureClassTable Load(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var rows = new List<TreasureClassRow>();
        foreach (var c in doc.RootElement.GetProperty("classes").EnumerateArray())
        {
            var entries = new List<TreasureClassEntry>();
            foreach (var e in c.GetProperty("entries").EnumerateArray())
            {
                string kind = e.GetProperty("kind").GetString() ?? throw new InvalidDataException("entry without kind");
                TcEntryKind k = kind.ToLowerInvariant() switch
                {
                    "nodrop" => TcEntryKind.NoDrop,
                    "gold" => TcEntryKind.Gold,
                    "group" => TcEntryKind.Group,
                    "item" => TcEntryKind.Item,
                    _ => throw new InvalidDataException($"unknown tc entry kind '{kind}'"),
                };
                string? r = e.TryGetProperty("baseId", out var b) ? b.GetString()
                    : e.TryGetProperty("ref", out var rf) ? rf.GetString() : null;
                entries.Add(new TreasureClassEntry(k, e.GetProperty("weight").GetInt32(), r));
            }
            long nuggets = 0;
            int mult = 0;
            if (c.TryGetProperty("gold", out var g))
            {
                nuggets = g.GetProperty("nuggets").GetInt64();
                mult = g.GetProperty("multiplier").GetInt32();
            }
            rows.Add(new TreasureClassRow(
                Key: c.GetProperty("key").GetString() ?? throw new InvalidDataException("class without key"),
                Name: c.GetProperty("name").GetString() ?? "",
                Picks: c.GetProperty("picks").GetInt32(),
                MinIlvl: c.TryGetProperty("minIlvl", out var mi) ? mi.GetInt32() : 0,
                GoldNuggets: nuggets,
                GoldMultiplier: mult,
                Entries: entries));
        }
        Validate(rows);
        return new TreasureClassTable(rows);
    }

    private static void Validate(List<TreasureClassRow> rows)
    {
        if (rows.Count == 0) throw new InvalidDataException("no treasure classes");
        if (rows.GroupBy(r => r.Key).Any(g => g.Count() > 1))
            throw new InvalidDataException("duplicate treasure class key");
        var keys = rows.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            if (r.Picks == 0) throw new InvalidDataException($"class '{r.Key}' picks must be nonzero");
            if (r.MinIlvl is < 0 or > 99) throw new InvalidDataException($"class '{r.Key}' minIlvl outside 0..99");
            if (r.GoldNuggets < 0 || r.GoldMultiplier < 0)
                throw new InvalidDataException($"class '{r.Key}' gold values negative");
            if (r.Entries.Count == 0) throw new InvalidDataException($"class '{r.Key}' has no entries");
            foreach (var e in r.Entries)
            {
                if (e.Weight < 1) throw new InvalidDataException($"class '{r.Key}': weight must be ≥1");
                if (e.Kind == TcEntryKind.NoDrop && r.IsForced)
                    throw new InvalidDataException($"forced class '{r.Key}' carries a NoDrop entry (P2 §4.9)");
                if (e.Kind == TcEntryKind.Gold && r.GoldNuggets < 1)
                    throw new InvalidDataException($"class '{r.Key}' draws gold but has no nuggets");
                if (e.Kind is TcEntryKind.Item or TcEntryKind.Group && string.IsNullOrEmpty(e.Ref))
                    throw new InvalidDataException($"class '{r.Key}': {e.Kind} entry without ref");
                if (e.Kind == TcEntryKind.Group && !keys.Contains(e.Ref!))
                    throw new InvalidDataException($"class '{r.Key}' group ref '{e.Ref}' unknown");
            }
        }
        // Group refs must be acyclic: drain every class whose group targets are drained.
        var remaining = rows.ToList();
        var done = new HashSet<string>(StringComparer.Ordinal);
        while (remaining.Count > 0)
        {
            var ready = remaining.Where(r =>
                !r.Entries.Any(e => e.Kind == TcEntryKind.Group && !done.Contains(e.Ref!))).ToList();
            if (ready.Count == 0) throw new InvalidDataException("treasure-class group cycle");
            foreach (var r in ready) { done.Add(r.Key); remaining.Remove(r); }
        }
    }

    /// One kill's walk (P2 §4.9). Magic Find rides only into the item forge —
    /// the walk's draw count and gold never read it (§4.8, MF is quality-only).
    public TreasureClassDrop Walk(
        Rng rng, string key, int ilvl, int magicFind = 0, int goldFind = 0, ItemForge? forge = null)
    {
        var tally = new WalkTally();
        WalkClass(rng, For(key), ilvl, magicFind, goldFind, forge, tally);
        return new TreasureClassDrop(tally.Items, tally.Gold);
    }

    private sealed class WalkTally
    {
        public List<Item> Items { get; } = [];
        public long Gold { get; set; }
        public int Outputs { get; set; }
    }

    private void WalkClass(
        Rng rng, TreasureClassRow row, int ilvl, int magicFind, int goldFind,
        ItemForge? forge, WalkTally tally)
    {
        var pool = row.IsForced
            ? row.Entries.Where(e => e.Kind != TcEntryKind.NoDrop).ToArray()
            : [.. row.Entries];
        int draws = Math.Min(Math.Abs(row.Picks), MaxOutputs - tally.Outputs);
        for (int d = 0; d < draws; d++)
        {
            var entry = AffixPool.PickWeighted(rng, pool, e => e.Weight)!;
            switch (entry.Kind)
            {
                case TcEntryKind.NoDrop:
                    break;
                case TcEntryKind.Gold:
                    tally.Gold += Gold.Pile(row.GoldNuggets, row.GoldMultiplier, goldFind);
                    tally.Outputs++;
                    break;
                case TcEntryKind.Item:
                    if (forge is null)
                        throw new InvalidOperationException("treasure-class walk drew an item without a forge");
                    tally.Items.Add(forge.Generate(rng, entry.Ref!, Math.Max(ilvl, row.MinIlvl), magicFind));
                    tally.Outputs++;
                    break;
                case TcEntryKind.Group:
                    WalkClass(rng, For(entry.Ref!), ilvl, magicFind, goldFind, forge, tally);
                    break;
            }
            if (tally.Outputs >= MaxOutputs) return; // the hard ≤6 cut, gold included
        }
    }
}

/// <summary>
/// Materialises one dropped base into a finished item at the §4.9 pipeline steps 3–5:
/// the unique→set→rare→magic quality roll (candidate-gated), the special row pick,
/// the rare/magic affix build, degrading a special whose pick table has no spawnable
/// row to magic exactly like the gamble path does (GambleStore.Buy). ilvl floors and
/// MF arrive from the walk; MF only shifts these odds (quality-only, §4.8).
/// </summary>
public sealed class ItemForge
{
    private readonly ItemBaseTable _bases;
    private readonly AffixPool _affixes;
    private readonly UniqueSetTable _uniques;

    public ItemForge(ItemBaseTable bases, AffixPool affixes, UniqueSetTable uniques)
    {
        _bases = bases;
        _affixes = affixes;
        _uniques = uniques;
    }

    public Item Generate(Rng rng, string baseId, int ilvl, int magicFind = 0)
    {
        var baseRow = _bases.For(baseId);
        var quality = QualityRoll.Roll(
            rng, baseRow, ilvl, magicFind,
            uniqueCandidateExists: _uniques.UniqueCandidate(baseRow.Id, ilvl),
            setCandidateExists: _uniques.SetCandidate(baseRow.Id, ilvl));
        if (quality is ItemQuality.Unique or ItemQuality.Set)
        {
            var special = quality == ItemQuality.Unique
                ? _uniques.BuildUnique(rng, baseRow, ilvl)
                : _uniques.BuildSetPiece(rng, baseRow, ilvl);
            if (special is not null) return special;
            quality = ItemQuality.Magic;
        }
        if (quality == ItemQuality.Magic) return AffixPool.BuildMagic(rng, baseRow, ilvl, _affixes);
        if (quality == ItemQuality.Rare && RareGen.TryBuild(rng, baseRow, ilvl, _affixes, out var rare))
            return rare!;
        return new Item(baseRow, ilvl) { Quality = quality };
    }
}
