using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>
/// One adopted hireling.txt row (the AMENDED §6.7 table template, MC 11883): the
/// bracket Level with its at-bracket stats and the per-level columns the engine
/// adds above it — life and AR whole, damage in EIGHTHS ("stat at level L =
/// bracket + ⌊(L−bracketLevel) × column ÷ 8⌋ on damage; the floor is on the
/// PRODUCT", amendment §1 S2/§2). The V100 NORMAL rows of hireling.txt rows
/// 14–16 / 44–46 / 74–76 are the adopted VALUES (amendment §5); Exp/Lvl is the
/// XP-ladder column (§2) and Gold the D2 witness — the clone's own hireGold
/// stays product data pending the gold-economy card (amendment §5 ORIGINAL
/// DESIGN), which is why the loader reads hireGold, not d2GoldWitness.
/// </summary>
public sealed record HirelingBracket(
    int Level,
    long Life,
    int LifePerLevel,
    int DamageMin,
    int DamageMax,
    int DamagePerLevel,
    int AttackRating,
    int ArPerLevel);

/// <summary>One roster row: identity + the act gate + the price/XP columns + the
/// hire-template bracket with the two adopted higher brackets carried beside it
/// (the offer wave that lets a town sell a L36 rogue is a future row).</summary>
public sealed record HirelingRow(
    string Id,
    string Name,
    int Act,
    string Weapon,
    string AttackKind,
    long HireGold,
    int ExpPerLevel,
    HirelingBracket Bracket,
    IReadOnlyList<HirelingBracket> HigherBrackets);

/// <summary>
/// The hireling table loaded from data/hirelings.json (the MonsterTable/
/// ItemBaseTable loader idiom). Owns the two §6.7 number rules the row type
/// answers for: the carried-forward HIRE PRICE and the bracket STAT MATH.
/// </summary>
public sealed class HirelingTable
{
    public IReadOnlyList<HirelingRow> Rows { get; }
    private readonly Dictionary<string, HirelingRow> _byId;

    private HirelingTable(List<HirelingRow> rows)
    {
        Rows = rows;
        _byId = rows.ToDictionary(r => r.Id, StringComparer.Ordinal);
    }

    public HirelingRow? Find(string id) => _byId.GetValueOrDefault(id);
    public HirelingRow For(string id) => Find(id)
        ?? throw new KeyNotFoundException($"no hireling row '{id}'");

    public static HirelingTable Load(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var rows = new List<HirelingRow>();
        foreach (var e in doc.RootElement.GetProperty("hirelings").EnumerateArray())
        {
            var brackets = e.GetProperty("higherBrackets").EnumerateArray()
                .Select(BracketRow).ToArray();
            var row = new HirelingRow(
                e.GetProperty("id").GetString()!,
                e.GetProperty("name").GetString()!,
                e.GetProperty("act").GetInt32(),
                e.GetProperty("weapon").GetString()!,
                e.GetProperty("attackKind").GetString()!,
                e.GetProperty("hireGold").GetInt64(),
                e.GetProperty("expPerLevel").GetInt32(),
                BracketRow(e.GetProperty("bracket")),
                brackets);
            if (row.HireGold < 1) throw new InvalidDataException($"hireling '{row.Id}' hireGold below 1");
            if (row.ExpPerLevel < 1) throw new InvalidDataException($"hireling '{row.Id}' Exp/Lvl below 1");
            if (row.Bracket.DamageMin > row.Bracket.DamageMax)
                throw new InvalidDataException($"hireling '{row.Id}' bracket damageMin above damageMax");
            if (brackets.Any(b => b.Level <= row.Bracket.Level))
                throw new InvalidDataException($"hireling '{row.Id}' carries a bracket at or below its hire level");
            rows.Add(row);
        }
        if (rows.Count == 0) throw new InvalidDataException("the hireling table lists no rows");
        if (rows.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != rows.Count)
            throw new InvalidDataException("the hireling table lists a duplicate id");
        return new HirelingTable(rows);
    }

    private static HirelingBracket BracketRow(JsonElement b) => new(
        b.GetProperty("level").GetInt32(),
        b.GetProperty("life").GetInt64(),
        b.GetProperty("lifePerLevel").GetInt32(),
        b.GetProperty("damageMin").GetInt32(),
        b.GetProperty("damageMax").GetInt32(),
        b.GetProperty("damagePerLevel").GetInt32(),
        b.GetProperty("attackRating").GetInt32(),
        b.GetProperty("arPerLevel").GetInt32());

    /// <summary>The hire price rule carried forward UNCHANGED through the
    /// amendment (§6.7, spec-3): Gold × (100 + 15 × levels_above_bracket) ÷ 100,
    /// integer floor. The clone's rookie enters at level 1 — below its first
    /// bracket — so the "above" term clamps at 0 and the hire price is exactly
    /// the row's own Gold (a below-bracket hire never sells under the row price).</summary>
    public static long HirePrice(HirelingRow row, int level) =>
        row.HireGold * (100 + 15 * Math.Max(0, level - row.Bracket.Level)) / 100;

    /// <summary>The D2MOO stat read (amendment §1 S3): bracket + nLevelUps × the
    /// column, damage in eighths with the engine's integer division — the same
    /// truncation D2MOO's `nLevelUps * nDmgPerLvl / 8` line carries. The pinned
    /// minima apply: life ≥ 40 and max damage ≥ 1 ("min 40<<8" / "min 1<<8" in
    /// the quoted engine lines). AttackRating has NO D2 minimum — the engine
    /// never stands below the bracket — so clamping the clone's below-bracket
    /// rookie at ≥ 0 is ORIGINAL DESIGN, stated where it reads.</summary>
    public static (long Life, int DamageMin, int DamageMax, long AttackRating)
        StatsAt(HirelingRow row, int level)
    {
        var b = row.Bracket;
        int ups = level - b.Level;
        long life = Math.Max(40, b.Life + (long)ups * b.LifePerLevel);
        int dmgUp = ups * b.DamagePerLevel / 8; // the engine division, eighths
        int dmgMin = Math.Max(0, b.DamageMin + dmgUp);
        int dmgMax = Math.Max(1, Math.Max(b.DamageMax + dmgUp, dmgMin));
        long ar = Math.Max(0, b.AttackRating + (long)ups * b.ArPerLevel);
        return (life, dmgMin, dmgMax, ar);
    }
}
