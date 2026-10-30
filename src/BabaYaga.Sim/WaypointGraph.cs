using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>One waypoint node from data/waypoints.json, standing in an area.</summary>
public sealed record WaypointRow(int Id, string AreaKey, string Name);

/// <summary>
/// The waypoint network (P3 §6.3): one node per key area, activation permanent per
/// character, and travel legal ONLY between discovered nodes. Town is the hub —
/// town↔any discovered node is always one hop; area↔area needs a path of
/// discovered nodes through the area graph. No de-activation method exists:
/// discovery is permanent by construction.
/// </summary>
public sealed class WaypointGraph
{
    private readonly AreaGraph _areas;
    private readonly Dictionary<string, WaypointRow> _byArea;
    private readonly HashSet<string> _discovered = new();

    public IReadOnlyList<WaypointRow> Nodes { get; }

    private WaypointGraph(AreaGraph areas, IReadOnlyList<WaypointRow> nodes)
    {
        _areas = areas;
        Nodes = nodes;
        _byArea = nodes.ToDictionary(n => n.AreaKey);
    }

    /// Parses and validates: unique ids, unique area keys, every node standing in
    /// a known area that actually carries a waypoint.
    public static WaypointGraph Load(AreaGraph areas, string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var nodes = new List<WaypointRow>();
        foreach (var n in doc.RootElement.GetProperty("waypoints").EnumerateArray())
            nodes.Add(new WaypointRow(
                Id: n.GetProperty("id").GetInt32(),
                AreaKey: n.GetProperty("areaKey").GetString()
                    ?? throw new InvalidDataException("waypoint without areaKey"),
                Name: n.GetProperty("name").GetString() ?? ""));
        var graph = new WaypointGraph(areas, nodes);
        if (nodes.GroupBy(n => n.Id).Any(g => g.Count() > 1))
            throw new InvalidDataException("duplicate waypoint id");
        if (nodes.GroupBy(n => n.AreaKey).Any(g => g.Count() > 1))
            throw new InvalidDataException("two waypoints in one area");
        foreach (var n in nodes)
        {
            if (!areas.TryGet(n.AreaKey, out var area))
                throw new InvalidDataException($"waypoint {n.Id} stands in unknown area '{n.AreaKey}'");
            if (!area.HasWaypoint)
                throw new InvalidDataException($"waypoint {n.Id} stands in waypoint-less area");
        }
        return graph;
    }

    public bool HasNode(string areaKey) => _byArea.ContainsKey(areaKey);
    public bool IsDiscovered(string areaKey) => _discovered.Contains(areaKey);

    /// Light a waypoint: false only when it was already lit (the second call is a
    /// no-op — discovery never reverses). Unknown areas are rejected.
    public bool Discover(string areaKey)
    {
        if (!HasNode(areaKey))
            throw new ArgumentException($"no waypoint node in area '{areaKey}'");
        return _discovered.Add(areaKey);
    }

    /// Destinations reachable from one discovered node (the waypoint-book list).
    public IReadOnlyList<string> Destinations(string fromAreaKey)
    {
        if (!_discovered.Contains(fromAreaKey)) return Array.Empty<string>();
        return Nodes
            .Select(n => n.AreaKey)
            .Where(k => k != fromAreaKey && CanTravel(fromAreaKey, k))
            .ToArray();
    }

    /// Travel is legal only between two DISCOVERED nodes, town-hub or via a path
    /// of discovered nodes through the area graph (P3 §6.3).
    public bool CanTravel(string fromAreaKey, string toAreaKey)
    {
        if (fromAreaKey == toAreaKey) return false;
        if (!_discovered.Contains(fromAreaKey) || !_discovered.Contains(toAreaKey))
            return false;
        if (fromAreaKey == _areas.TownKey || toAreaKey == _areas.TownKey)
            return true;
        // area<->area: BFS restricted to discovered waypoint nodes.
        var seen = new HashSet<string> { fromAreaKey };
        var queue = new Queue<string>();
        queue.Enqueue(fromAreaKey);
        while (queue.Count > 0)
        {
            foreach (var next in _areas.Get(queue.Dequeue()).LinkedTo)
            {
                if (next == toAreaKey) return true;
                if (seen.Add(next) && _discovered.Contains(next)) queue.Enqueue(next);
            }
        }
        return false;
    }
}
