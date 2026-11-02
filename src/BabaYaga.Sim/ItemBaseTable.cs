using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>
/// One item-base row (data/item-bases.json shape): the white substrate every quality
/// roll acts on (P2 §4.2). Qlvl is the base's own quality level — ilvl always comes
/// from context (drop/gamble/cube), never from the row. Equiv is the type chain a
/// runeword itype-list may match against (P2 §4.5 cond. 1).
/// </summary>
public sealed record ItemBaseRow(
    string Id,
    string Name,
    string Itype,
    int Qlvl,
    int Cost,
    int Durability,
    int Width,
    int Height,
    int MaxSockets,
    int ReqStr,
    int ReqDex,
    IReadOnlyList<string> Equiv,
    /// <summary>false = row is always Normal quality: gems, runes (P2 §4.1 tier gate).</summary>
    bool QualityRolls,
    /// <summary>true = alvl is L directly, skipping the −qlvl/2 drop (P2 §4.2).</summary>
    bool MagicLevelAdd,
    /// <summary>true = vendor sell applies the class quarter a second time (P2 §4.8).</summary>
    bool ClassSpecific,
    /// <summary>true = belt-side consumable (row 69 potions): never enters the
    /// Stock roll pipeline (gamble, drops) and needs no durability.</summary>
    bool Consumable,
    /// <summary>§6.7c (MC 11914): the weapon base's ONE damage range — the
    /// clone's fenced collapse of D2's 1-hand/2-hand/throw families (spec-3
    /// §6.7c ORIGINAL DESIGN). 0/0 = the base carries no range yet (every
    /// shipped row today — the data-future LEDGER gap): a worn weapon with no
    /// range changes nothing on a merc's dice, the bracket stands.</summary>
    int DamageMin = 0,
    int DamageMax = 0)
{
    /// <summary>Type name for an itype-or-equiv token match against a runeword's list (P2 §4.5).
    /// Plural/singular spellings are the same type token.</summary>
    public bool MatchesType(string itype) =>
        Norm(Itype) == Norm(itype) || Equiv.Contains(Norm(itype));

    private static string Norm(string t) => t.EndsWith('s') ? t[..^1] : t;
}

/// <summary>
/// Loads and validates the item-base table (the content gate, same pattern as
/// SkillDataLoader): unique ids, positive qlvl/cost, grid cells at least 1×1,
/// socket ceiling within the 6 rule, and every Equiv token resolving to a known
/// itype or a declared chain word.
/// </summary>
public sealed class ItemBaseTable
{
    public required IReadOnlyList<ItemBaseRow> Bases { get; init; }

    private readonly Dictionary<string, ItemBaseRow> _byId =
        new(StringComparer.Ordinal);

    public ItemBaseRow For(string id) =>
        _byId.TryGetValue(id, out var b)
            ? b
            : throw new KeyNotFoundException($"item base '{id}' not in table");

    public ItemBaseRow? Find(string id) =>
        _byId.TryGetValue(id, out var b) ? b : null;

    /// <summary>Rows a monster/gamble stock may hold (quality-rolling bases only;
    /// consumables — the belt potions — are stock-side never-rolled too).</summary>
    public IReadOnlyList<ItemBaseRow> Stock =>
        Bases.Where(b => b.QualityRolls && !b.Consumable).ToList();

    public static ItemBaseTable Load(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var rows = new List<ItemBaseRow>();
        foreach (var e in doc.RootElement.GetProperty("bases").EnumerateArray())
        {
            rows.Add(new ItemBaseRow(
                e.GetProperty("id").GetString()!,
                e.GetProperty("name").GetString()!,
                e.GetProperty("itype").GetString()!,
                e.GetProperty("qlvl").GetInt32(),
                e.GetProperty("cost").GetInt32(),
                e.GetProperty("durability").GetInt32(),
                e.GetProperty("w").GetInt32(),
                e.GetProperty("h").GetInt32(),
                e.GetProperty("maxSockets").GetInt32(),
                e.GetProperty("reqStr").GetInt32(),
                e.GetProperty("reqDex").GetInt32(),
                e.GetProperty("equiv").EnumerateArray().Select(x => x.GetString()!).ToList(),
                !e.TryGetProperty("qualityRolls", out var qr) || qr.GetBoolean(),
                e.TryGetProperty("magicLevelAdd", out var mla) && mla.GetBoolean(),
                e.TryGetProperty("classSpecific", out var cs) && cs.GetBoolean(),
                e.TryGetProperty("consumable", out var con) && con.GetBoolean(),
                e.TryGetProperty("damageMin", out var dmin) ? dmin.GetInt32() : 0,
                e.TryGetProperty("damageMax", out var dmax) ? dmax.GetInt32() : 0));
        }
        Validate(rows);
        var table = new ItemBaseTable { Bases = rows };
        foreach (var b in rows) table._byId[b.Id] = b;
        return table;
    }

    private static void Validate(List<ItemBaseRow> rows)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var itypes = rows.Select(r => r.Itype).ToHashSet(StringComparer.Ordinal);
        foreach (var b in rows)
        {
            if (!seen.Add(b.Id))
                throw new InvalidDataException($"duplicate item base id '{b.Id}'");
            if (b.Qlvl < 1 || b.Cost < 1)
                throw new InvalidDataException($"item base '{b.Id}': qlvl and cost must be positive");
            if (b.Width < 1 || b.Height < 1)
                throw new InvalidDataException($"item base '{b.Id}': inventory cells must be at least 1x1");
            if (b.MaxSockets is < 0 or > 6)
                throw new InvalidDataException($"item base '{b.Id}': socket ceiling outside 0..6 (P2 §4.5)");
            if (b.DamageMin < 0 || b.DamageMax < b.DamageMin)
                throw new InvalidDataException(
                    $"item base '{b.Id}': damage range must satisfy 0 <= min <= max (§6.7c)");
            if (b.QualityRolls && !b.Consumable && b.Durability < 1 && !IsJewelry(b.Itype))
                throw new InvalidDataException($"item base '{b.Id}': wearable needs durability");
            foreach (var t in b.Equiv)
            {
                if (!itypes.Any(i => Norm(i) == Norm(t)) && !ChainWords.Contains(Norm(t)))
                    throw new InvalidDataException($"item base '{b.Id}': equiv '{t}' resolves to no known type");
            }
        }
    }

    /// <summary>Declared chain words equivs may point at besides concrete itypes (P2 §4.5 Equiv chains).</summary>
    private static readonly HashSet<string> ChainWords =
        new(StringComparer.Ordinal) { "weapon", "armor", "melee", "ranged", "magic", "jewelry", "accessory", "offhand", "misc" };

    private static bool IsJewelry(string itype) =>
        itype is "rings" or "amulets" or "gems" or "runes";

    private static string Norm(string t) => t.EndsWith('s') ? t[..^1] : t;
}
