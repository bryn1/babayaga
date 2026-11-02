using System.Collections.Generic;

/// <summary>
/// M5-S4 RENDER + INPUT, the SPRITE ROSTER (MC 11937, FIX-DA5 5): the
/// presentation-side monster roster as a PURE class MonsterField builds on —
/// because the Sim spawns a FRESH monster per kill (Combat.cs header, VERIFIED)
/// the roster is presentation truth the replay gate can never see, so the
/// despawn RULE deserves its own tested unit: one kill takes exactly ONE sprite
/// of the attacked row, build-order first; an unknown key takes nothing (a
/// planted-red shape). MonsterField keeps the geometry (hit-test radii, sprite
/// lifetime); membership lives here. Pure (no Godot types) — xunit-covered.
/// </summary>
public sealed class SpriteRoster<T> where T : class
{
    /// <summary>One standing sprite: its row key, the cell it holds, the sprite.</summary>
    public sealed record Entry(string Key, (int X, int Y) Cell, T Sprite);

    private readonly List<Entry> _entries = [];

    public int Count => _entries.Count;
    public IReadOnlyList<Entry> Entries => _entries;

    public void Clear() => _entries.Clear();

    public void Add(string key, int x, int y, T sprite) => _entries.Add(new Entry(key, (x, y), sprite));

    /// <summary>Kill → sprite despawn: removes the build-order entry of that
    /// row and hands back its sprite (the caller frees it); null when the row
    /// does not stand here — never steals another row's sprite.</summary>
    public T? DespawnOne(string key)
    {
        int i = _entries.FindIndex(e => e.Key == key);
        if (i < 0) return null;
        var entry = _entries[i];
        _entries.RemoveAt(i);
        return entry.Sprite;
    }

    /// <summary>Every standing key, build order — the S7 sprite-count read.</summary>
    public IEnumerable<string> Keys()
    {
        foreach (var e in _entries) yield return e.Key;
    }
}
