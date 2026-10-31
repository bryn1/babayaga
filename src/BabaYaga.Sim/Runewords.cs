using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>One rune-stone row (data/runes.json): the grants it radiates while
/// socketed and the ilvl it carries when it drops.</summary>
public sealed record RuneRow(string Id, string Name, int Level, int Cost, IReadOnlyList<AffixModRange> Grants);

/// <summary>One runeword row (data/runewords.json). Runes is the ORDERED list the
/// sockets must carry exactly (§4.5 condition 4); Available is the ladder gate (5).</summary>
public sealed record RunewordRow(
    string Id,
    string Name,
    IReadOnlyList<string> Itypes,
    IReadOnlyList<string> Runes,
    IReadOnlyList<AffixModRange> Grants,
    bool Available);

/// <summary>
/// Socketed-stone tables + the runeword matcher (P2 §4.5). A word completes on
/// ALL five conditions: 1 base type ∈ the itype list (equiv chains included);
/// 2 the item is white (ethereal ok; magic/rare/set/unique never);
/// 3 socket count == the word's stone count, exactly;
/// 4 the stones sit in the row's exact order;
/// 5 the word is available in this game.
/// </summary>
public sealed class RunewordTable
{
    public required IReadOnlyList<RuneRow> Runes { get; init; }
    public required IReadOnlyList<RunewordRow> Words { get; init; }

    public RuneRow For(string runeId) => Runes.First(r => r.Id == runeId);

    /// <summary>Condition 1 for one row: the base's own type or any equiv token matches.</summary>
    public static bool TypeMatches(Item item, RunewordRow word) =>
        word.Itypes.Any(item.Base.MatchesType);

    /// <summary>All five conditions for one candidate word (P2 §4.5).</summary>
    public static bool ConditionsMet(Item item, RunewordRow word) =>
        TypeMatches(item, word)
        && item.IsWhite
        && item.Sockets == word.Runes.Count
        && item.Sockets == item.SocketedRunes.Count
        && item.SocketedRunes.SequenceEqual(word.Runes)
        && word.Available;

    /// <summary>Check every word against the item; on a match, carve it: the word
    /// name replaces the base name and its grants land on the item. Re-checked after
    /// any socket change by the caller (P2 §4.9 step 6).</summary>
    public RunewordRow? TryBind(Rng rng, Item item)
    {
        var word = Words.FirstOrDefault(w => ConditionsMet(item, w));
        if (word is null) return null;
        item.RunewordId = word.Name;
        foreach (var g in word.Grants)
            item.Mods.Add(new ItemMod(g.Stat, rng.NextInRange(g.Min, g.Max)));
        return word;
    }

    /// <summary>Socket one stone (rune id or gem id) into a free socket cell.</summary>
    public static bool InsertStone(Rng rng, Item item, string stoneId)
    {
        if (item.SocketedRunes.Count >= item.Sockets) return false;
        item.SocketedRunes.Add(stoneId);
        return true;
    }

    public static RunewordTable Load(string runesPath, string wordsPath)
    {
        using var runesDoc = JsonDocument.Parse(File.ReadAllText(runesPath));
        var runes = runesDoc.RootElement.GetProperty("runes").EnumerateArray()
            .Select(r => new RuneRow(
                r.GetProperty("id").GetString()!,
                r.GetProperty("name").GetString()!,
                r.GetProperty("level").GetInt32(),
                r.GetProperty("cost").GetInt32(),
                r.GetProperty("grants").EnumerateArray().Select(AffixPool.ReadModRange).ToList()))
            .ToList();

        using var wordsDoc = JsonDocument.Parse(File.ReadAllText(wordsPath));
        var runeIds = runes.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var words = wordsDoc.RootElement.GetProperty("words").EnumerateArray()
            .Select(w => new RunewordRow(
                w.GetProperty("id").GetString()!,
                w.GetProperty("name").GetString()!,
                w.GetProperty("itypes").EnumerateArray().Select(t => t.GetString()!).ToList(),
                w.GetProperty("runes").EnumerateArray().Select(t => t.GetString()!).ToList(),
                w.GetProperty("grants").EnumerateArray().Select(AffixPool.ReadModRange).ToList(),
                w.GetProperty("available").GetBoolean()))
            .ToList();

        foreach (var w in words)
            foreach (var rid in w.Runes)
                if (!runeIds.Contains(rid))
                    throw new InvalidDataException($"runeword '{w.Id}' names unknown stone '{rid}'");
        return new RunewordTable { Runes = runes, Words = words };
    }
}
