using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>
/// The three world difficulty bands (P3 §6.1, §6.6). Every difficulty-aware table
/// row (area monster levels, special bonuses, death cost) carries one column per band.
/// </summary>
public enum Difficulty
{
    Ordinary = 0,
    Nightmare = 1,
    Hell = 2,
}

/// <summary>
/// A point in an area's coordinate space (one unit per world step). Chebyshev
/// distance is the sim's single distance metric — the same rule the party-XP
/// proximity check and the AI range bands read (P3 §6.6).
/// </summary>
public readonly record struct WorldPos(int X, int Y)
{
    public int DistanceTo(WorldPos other) =>
        Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));
}

/// <summary>
/// One area row of an act (P3 §6.1): its own level (Normal-era), the area monster
/// levels the higher bands stamp onto ordinary monsters (MonLvlEx shape), spawn
/// density, the town / boss-lair / waypoint flags, and the linked-area edges.
/// All names are ORIGINAL Baba Yaga content (data/act1-areas.json).
/// </summary>
public sealed record AreaRow(
    string Key,
    string Name,
    int Act,
    int Level,
    int MonLvlNightmare,
    int MonLvlHell,
    int DensityPercent,
    bool IsTown,
    bool IsBossLair,
    bool HasWaypoint,
    IReadOnlyList<string> LinkedTo);

/// <summary>
/// The act's area graph (P3 §6.1): named areas joined by symmetric edges, exactly
/// one town hub. Traversal is graph reachability; the layout generator (MapGen) is
/// a later phase and only ever decides where the EDGE DOORS stand, never the graph.
/// </summary>
public sealed class AreaGraph
{
    private readonly Dictionary<string, AreaRow> _rows;

    public string TownKey { get; }
    public int Act { get; }
    public IReadOnlyList<AreaRow> Areas { get; }

    private AreaGraph(List<AreaRow> rows, Dictionary<string, AreaRow> index)
    {
        Areas = rows;
        _rows = index;
        TownKey = rows.First(r => r.IsTown).Key;
        Act = rows[0].Act;
    }

    /// Parses and validates an areas file: unique keys, exactly one town, every edge
    /// resolving to a known area, symmetric links, all rows in the same act.
    public static AreaGraph Load(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var rows = new List<AreaRow>();
        foreach (var a in doc.RootElement.GetProperty("areas").EnumerateArray())
        {
            var links = new List<string>();
            foreach (var l in a.GetProperty("linkedTo").EnumerateArray())
                links.Add(l.GetString() ?? throw new InvalidDataException("empty link"));
            rows.Add(new AreaRow(
                Key: a.GetProperty("key").GetString() ?? throw new InvalidDataException("area without key"),
                Name: a.GetProperty("name").GetString() ?? "",
                Act: a.GetProperty("act").GetInt32(),
                Level: a.GetProperty("level").GetInt32(),
                MonLvlNightmare: a.GetProperty("monLvlNightmare").GetInt32(),
                MonLvlHell: a.GetProperty("monLvlHell").GetInt32(),
                DensityPercent: a.GetProperty("densityPercent").GetInt32(),
                IsTown: a.GetProperty("isTown").GetBoolean(),
                IsBossLair: a.GetProperty("isBossLair").GetBoolean(),
                HasWaypoint: a.GetProperty("hasWaypoint").GetBoolean(),
                LinkedTo: links));
        }
        var index = new Dictionary<string, AreaRow>();
        foreach (var r in rows)
        {
            if (index.ContainsKey(r.Key))
                throw new InvalidDataException($"duplicate area key '{r.Key}'");
            if (r.Level < 1 || r.DensityPercent < 0)
                throw new InvalidDataException($"area '{r.Key}' level/density out of range");
            index[r.Key] = r;
        }
        foreach (var r in rows)
        {
            if (r.Act != rows[0].Act)
                throw new InvalidDataException($"area '{r.Key}' belongs to another act");
            foreach (var link in r.LinkedTo)
            {
                if (!index.TryGetValue(link, out var peer))
                    throw new InvalidDataException($"area '{r.Key}' links to unknown area '{link}'");
                if (!peer.LinkedTo.Contains(r.Key))
                    throw new InvalidDataException($"edge {r.Key}<->{link} is not symmetric");
            }
        }
        int towns = rows.Count(r => r.IsTown);
        if (towns != 1)
            throw new InvalidDataException($"exactly one town required, found {towns}");
        return new AreaGraph(rows, index);
    }

    public AreaRow Get(string key) => _rows[key];
    public bool TryGet(string key, out AreaRow row) => _rows.TryGetValue(key, out row!);

    /// The monster level this area stamps in each band (P3 §5.1: the higher bands
    /// give ordinary monsters the AREA's level; Ordinary uses the monster's own).
    public int AreaMonsterLevel(AreaRow area, Difficulty difficulty) => difficulty switch
    {
        Difficulty.Nightmare => area.MonLvlNightmare,
        Difficulty.Hell => area.MonLvlHell,
        _ => area.Level,
    };

    /// Graph reachability between two areas (any path), the travel precondition.
    public bool Connected(string fromKey, string toKey)
    {
        if (!_rows.ContainsKey(fromKey) || !_rows.ContainsKey(toKey)) return false;
        var seen = new HashSet<string> { fromKey };
        var queue = new Queue<string>();
        queue.Enqueue(fromKey);
        while (queue.Count > 0)
        {
            foreach (var next in _rows[queue.Dequeue()].LinkedTo)
            {
                if (next == toKey) return true;
                if (seen.Add(next)) queue.Enqueue(next);
            }
        }
        return false;
    }
}
