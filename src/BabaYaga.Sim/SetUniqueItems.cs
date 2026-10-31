using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>One unique or set-piece row (data/uniques.json shape, P2 §4.4): decided
/// inside the §4.1 quality roll, then picked among rows whose lvl gate passes the
/// ilvl, weighted by rarity class (0 common → rarest: weight 60/(rarity+1)).
/// Mods are fixed rows rolled uniform min..max once per item (nummods pattern).</summary>
public sealed record UniqueRow(
    string Id,
    string Name,
    string BaseId,
    string? SetId,
    int Lvl,
    int Rarity,
    IReadOnlyList<AffixModRange> Mods);

/// <summary>One set count-bonus tier (P2 §4.4: bonuses by count equipped).</summary>
public sealed record SetBonusTier(int Count, IReadOnlyList<AffixModRange> Mods);

/// <summary>
/// Load and pick table for uniques and sets (P2 §4.4). Uniques never re-roll:
/// one BuildUnique call per drop decides the item for good.
/// </summary>
public sealed class UniqueSetTable
{
    public required IReadOnlyList<UniqueRow> Rows { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<SetBonusTier>> SetBonuses { get; init; }

    /// <summary>Pick weight for a rarity class (rarity 0 → 1 at the heaviest).</summary>
    public static int Weight(int rarity) => 60 / (rarity + 1);

    /// <summary>True when any unique/set row for this base is spawnable at ilvl —
    /// the flag the §4.1 quality roll consults before spending a unique/set try.</summary>
    public bool UniqueCandidate(string baseId, int ilvl) =>
        Rows.Any(r => r.SetId is null && r.BaseId == baseId && r.Lvl <= ilvl);

    public bool SetCandidate(string baseId, int ilvl) =>
        Rows.Any(r => r.SetId is not null && r.BaseId == baseId && r.Lvl <= ilvl);

    /// <summary>Build a unique drop on this base at this ilvl, or null when none is spawnable.</summary>
    public Item? BuildUnique(Rng rng, ItemBaseRow baseRow, int ilvl) =>
        Build(rng, baseRow, ilvl, r => r.SetId is null, ItemQuality.Unique);

    /// <summary>Build a set-piece drop on this base at this ilvl, or null when none is spawnable.</summary>
    public Item? BuildSetPiece(Rng rng, ItemBaseRow baseRow, int ilvl) =>
        Build(rng, baseRow, ilvl, r => r.SetId is not null, ItemQuality.Set);

    private Item? Build(
        Rng rng, ItemBaseRow baseRow, int ilvl, Func<UniqueRow, bool> side, ItemQuality quality)
    {
        var candidates = Rows.Where(r => side(r) && r.BaseId == baseRow.Id && r.Lvl <= ilvl).ToList();
        if (candidates.Count == 0) return null;
        var row = AffixPool.PickWeighted(rng, candidates, r => Weight(r.Rarity))!;
        var item = new Item(baseRow, ilvl) { Quality = quality };
        item.UniqueId = row.Name; // the special row's own name, worn or set piece alike
        if (quality == ItemQuality.Set) item.SetId = row.SetId; // membership key for count bonuses
        foreach (var m in row.Mods)
            item.Mods.Add(new ItemMod(m.Stat, rng.NextInRange(m.Min, m.Max)));
        return item;
    }

    /// <summary>The set-item count-bonus grants for wearing `equippedCount` pieces:
    /// every tier whose count threshold is met (P2 §4.4 — 2/3/4/5-piece tiers stack).</summary>
    public IReadOnlyList<ItemMod> SetBonusMods(string setId, int equippedCount) =>
        SetBonuses.TryGetValue(setId, out var tiers)
            ? [.. tiers.Where(t => t.Count <= equippedCount).SelectMany(t => t.Mods.Select(m => new ItemMod(m.Stat, m.Min)))]
            : [];

    public static UniqueSetTable Load(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var rows = new List<UniqueRow>();
        foreach (var e in doc.RootElement.GetProperty("uniques").EnumerateArray())
            rows.Add(ReadRow(e, null));
        foreach (var e in doc.RootElement.GetProperty("sets").EnumerateArray())
            rows.Add(ReadRow(e, e.GetProperty("setId").GetString()!));

        var bonuses = new Dictionary<string, IReadOnlyList<SetBonusTier>>(StringComparer.Ordinal);
        foreach (var bonusSet in doc.RootElement.GetProperty("setBonuses").EnumerateObject())
            bonuses[bonusSet.Name] = [.. bonusSet.Value.EnumerateArray().Select(t => new SetBonusTier(
                t.GetProperty("count").GetInt32(),
                t.GetProperty("mods").EnumerateArray().Select(ReadModRange).ToList()))];

        Validate(rows, bonuses);
        return new UniqueSetTable { Rows = rows, SetBonuses = bonuses };
    }

    private static UniqueRow ReadRow(JsonElement e, string? setId) => new(
        e.GetProperty("id").GetString()!,
        e.GetProperty("name").GetString()!,
        e.GetProperty("baseId").GetString()!,
        setId,
        e.GetProperty("lvl").GetInt32(),
        e.GetProperty("rarity").GetInt32(),
        e.GetProperty("mods").EnumerateArray().Select(ReadModRange).ToList());

    private static AffixModRange ReadModRange(JsonElement m) => new(
        m.GetProperty("stat").GetString()!,
        m.GetProperty("min").GetInt32(),
        m.GetProperty("max").GetInt32());

    private static void Validate(List<UniqueRow> rows, Dictionary<string, IReadOnlyList<SetBonusTier>> bonuses)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            if (!seen.Add(r.Id))
                throw new InvalidDataException($"duplicate unique/set id '{r.Id}'");
            if (r.Lvl < 1 || r.Rarity is < 0 or > 3)
                throw new InvalidDataException($"'{r.Id}': lvl ≥1 and rarity 0..3 required");
            foreach (var m in r.Mods)
                if (m.Max < m.Min)
                    throw new InvalidDataException($"'{r.Id}': mod '{m.Stat}' max below min");
            if (r.SetId is not null && !bonuses.ContainsKey(r.SetId))
                throw new InvalidDataException($"set piece '{r.Id}' names bonus-less set '{r.SetId}'");
        }
        foreach (var (setId, tiers) in bonuses)
            if (rows.All(r => r.SetId != setId))
                throw new InvalidDataException($"bonus-only set '{setId}' has no pieces");
    }
}
