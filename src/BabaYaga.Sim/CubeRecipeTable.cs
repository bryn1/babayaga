using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>
/// THE RITUAL CRAFT RECIPE TABLE (spec §6.8, LEDGER row 48, MC 11969): one row of
/// data/cube-recipes.json. The input multiset is a LIST — schema order IS the
/// consumption order (§2, D2's determinism carrier); every ingredient is ONE exact
/// itemId (the fenced R2b collapse of the oracle's coded-selector rows: the clone
/// owns one base table, not an item-code table). ilvlRule/flags are DEAD fields the
/// skeleton wave left: a row carrying either LOADS AND RUNS with the field read as
/// the number or ignored (the R7b reading pinned — the 5 shipped PLACEHOLDER rows
/// carry both and skip on admission, never reaching this).
/// </summary>
public sealed record CubeRecipeInputRow(string ItemId, int Count);

/// <summary>One fixed grant, SAME {stat,min,max} shape as AffixPool.ReadModRange,
/// plus chance 1..100 (absent = 100, the always-hits default R5b retires
/// flags:["always-hits"] to). Rolls uniform min..max on the craft stream when it
/// lands (D5(3)); chance semantics pin nothing shipped or synthetic below 100.</summary>
public sealed record CubeRecipeFixedMod(string Stat, int Min, int Max, int Chance);

/// <summary>Lvl is the fixed ilvl or −1 when the row is the percent pair (defaults
/// stored VERBATIM: −1/0/0 → FromCube always answers a clamped ≥1); OutputQuality
/// Normal means the key was absent — the plain fixed-mint family, R2b.</summary>
public sealed record CubeRecipeRow(
    string Id,
    string Name,
    IReadOnlyList<CubeRecipeInputRow> Inputs,
    string OutputItemId,
    ItemQuality OutputQuality,
    IReadOnlyList<CubeRecipeFixedMod> FixedMods,
    int Lvl,
    int Plvl,
    int Ilvl);

/// <summary>
/// Loads data/cube-recipes.json under THE ADMISSION LAW (§2 cycle-2, verbatim):
/// • A SCHEMA-INVALID row THROWS InvalidDataException — the same throw type every
///   existing data-table loader throws (the idiom: HirelingTable.cs:93–108,
///   WaypointGraph.cs:48–57). Schema-invalid = missing required key (id, name,
///   non-empty inputs, itemId on each input + integer count ≥ 1, output.itemId),
///   optional output.quality outside magic/rare, optional fixedMods with min above
///   max or a present chance outside 1..100, or a duplicate id among ADMITTED rows.
/// • A CROSS-TABLE FAILURE SKIPS SILENTLY: a recipe row whose INPUT or OUTPUT
///   itemId is not in bases is SKIPPED at load — it enters neither Recipes nor the
///   id index; the loader COUNTS the skip. The door answers a skipped id with the
///   RECIPE-UNKNOWN refusal (D3), so content that is not in this world's bases can
///   never be crafted nor named to the player.
/// • The loader PRINTS NOTHING — zero-stdout at load, the law the anchor md5 guards:
///   the shipped cube-recipes.json carries 5 PLACEHOLDER rows that skip at EVERY
///   boot (all 15 of its ids are PLACEHOLDER-prefixed, 0 exist in item-bases.json);
///   a print would move the story anchor. Counts travel ONLY through the read-back
///   properties (a pytest/console row cannot read stdout the Sim never wrote).
/// The Load-time checks are the SCHEMA checks only; base membership is the cross-
/// table admission check above, never a throw.
/// </summary>
public sealed class CubeRecipeTable
{
    /// <summary>Admitted rows, file order — the read-back half of the admission law.</summary>
    public IReadOnlyList<CubeRecipeRow> Recipes { get; init; } = [];

    /// <summary>Rows skipped by cross-table admission — the law's ONLY reporting
    /// channel (§2 naming law: SkippedCount).</summary>
    public int SkippedCount { get; init; }

    private readonly Dictionary<string, CubeRecipeRow> _byId = new(StringComparer.Ordinal);

    public CubeRecipeRow? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>The programmer-facing door per the For idiom — calling it with a
    /// skipped id is the door's bug, not the player's (the D3 refusal is the
    /// player-facing path; the door never uses For).</summary>
    public CubeRecipeRow For(string id) => Find(id)
        ?? throw new KeyNotFoundException($"no cube recipe '{id}' (admitted: {Recipes.Count}, skipped: {SkippedCount})");

    public static CubeRecipeTable Load(ItemBaseTable bases, string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var admitted = new List<CubeRecipeRow>();
        var byId = new HashSet<string>(StringComparer.Ordinal);
        var skipped = 0;
        foreach (var e in doc.RootElement.GetProperty("recipes").EnumerateArray())
        {
            var row = ReadRow(e); // schema-invalid → InvalidDataException (the THROW path)
            if (CarriesUnknownBase(bases, row)) { skipped++; continue; } // the silent path
            if (!byId.Add(row.Id))
                throw new InvalidDataException($"cube-recipes.json: duplicate recipe id '{row.Id}'");
            admitted.Add(row);
        }
        var table = new CubeRecipeTable { Recipes = admitted, SkippedCount = skipped };
        foreach (var r in admitted) table._byId[r.Id] = r;
        return table;
    }

    /// <summary>Cross-table admission: EVERY input and the output itemId must stand
    /// in bases — membership checked through bases.Find, never a throw.</summary>
    private static bool CarriesUnknownBase(ItemBaseTable bases, CubeRecipeRow row) =>
        bases.Find(row.OutputItemId) is null ||
        row.Inputs.Any(i => bases.Find(i.ItemId) is null);

    private static CubeRecipeRow ReadRow(JsonElement e)
    {
        var id = ReqString(e, "id", "cube recipe row");
        var name = ReqString(e, "name", $"cube recipe '{id}'");
        if (!e.TryGetProperty("inputs", out var inputsEl) ||
            inputsEl.ValueKind != JsonValueKind.Array || inputsEl.GetArrayLength() == 0)
            throw new InvalidDataException($"cube recipe '{id}': inputs must be a non-empty array");
        var inputs = new List<CubeRecipeInputRow>();
        foreach (var i in inputsEl.EnumerateArray())
        {
            var itemId = ReqString(i, "itemId", $"cube recipe '{id}' input");
            if (!i.TryGetProperty("count", out var c) || !c.TryGetInt32(out var count) || count < 1)
                throw new InvalidDataException(
                    $"cube recipe '{id}' input '{itemId}': count must be an integer >= 1");
            inputs.Add(new CubeRecipeInputRow(itemId, count));
        }
        if (!e.TryGetProperty("output", out var outEl) || outEl.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"cube recipe '{id}': output object required");
        var outId = ReqString(outEl, "itemId", $"cube recipe '{id}' output");

        var quality = ItemQuality.Normal; // absent quality = the plain fixed-mint family (R2b)
        if (outEl.TryGetProperty("quality", out var q))
            quality = q.GetString() switch
            {
                "magic" => ItemQuality.Magic,
                "rare" => ItemQuality.Rare,
                _ => throw new InvalidDataException(
                    $"cube recipe '{id}': output.quality must be magic or rare"),
            };

        var mods = new List<CubeRecipeFixedMod>();
        if (outEl.TryGetProperty("fixedMods", out var fmEl) && fmEl.ValueKind == JsonValueKind.Array)
            foreach (var m in fmEl.EnumerateArray())
            {
                var stat = ReqString(m, "stat", $"cube recipe '{id}' fixedMod");
                var min = ReqInt(m, "min", $"cube recipe '{id}' fixedMod '{stat}'");
                var max = ReqInt(m, "max", $"cube recipe '{id}' fixedMod '{stat}'");
                if (max < min)
                    throw new InvalidDataException($"cube recipe '{id}' fixedMod '{stat}': max below min");
                var chance = 100; // the always-hits default (R5b)
                if (m.TryGetProperty("chance", out var ch))
                {
                    chance = ReqInt(m, "chance", $"cube recipe '{id}' fixedMod '{stat}'");
                    if (chance is < 1 or > 100)
                        throw new InvalidDataException($"cube recipe '{id}' fixedMod '{stat}': chance outside 1..100");
                }
                mods.Add(new CubeRecipeFixedMod(stat, min, max, chance));
            }

        // ilvlRule/flags are DEAD on purpose (§2): read NOTHING from them.
        var lvl = outEl.TryGetProperty("lvl", out var l) ? ReqInt(outEl, "lvl", $"cube recipe '{id}'") : -1;
        var plvl = outEl.TryGetProperty("plvl", out var p) ? ReqInt(outEl, "plvl", $"cube recipe '{id}'") : 0;
        var ilvl = outEl.TryGetProperty("ilvl", out var i2) ? ReqInt(outEl, "ilvl", $"cube recipe '{id}'") : 0;
        return new CubeRecipeRow(id, name, inputs, outId, quality, mods, lvl, plvl, ilvl);
    }

    private static string ReqString(JsonElement e, string key, string at) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s
            ? s
            : throw new InvalidDataException($"{at}: required string '{key}' missing or empty");

    private static int ReqInt(JsonElement e, string key, string at) =>
        e.TryGetProperty(key, out var v) && v.TryGetInt32(out var n)
            ? n
            : throw new InvalidDataException($"{at}: '{key}' must be an integer");
}
