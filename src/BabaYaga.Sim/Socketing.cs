namespace BabaYaga.Sim;

/// <summary>
/// Socket math (P2 §4.5):
///   max_sockets = min(base gemsockets, type band at ilvl (≤25 / ≤40 / else), inv cells capped at 6)
/// then the count itself rolls uniform 0..max. Bands are this design's own rows in
/// the §4.5 shape (swords 3/4/6, body 3/4/6, shields 3/3/4, helms 2/2/3, staves
/// 5/6/6, circlets 1/2/3, orbs 2/3/3 …). Gems and rune-stones never take sockets.
/// </summary>
public static class Socketing
{
    /// <summary>(band@≤25, band@≤40, band@41+) per item type; types absent roll 0.</summary>
    private static readonly Dictionary<string, (int, int, int)> Bands = new(StringComparer.Ordinal)
    {
        ["swords"] = (3, 4, 6), ["axes"] = (3, 4, 6), ["blunt"] = (3, 4, 6),
        ["polearms"] = (4, 5, 6), ["bows"] = (3, 4, 6), ["staves"] = (5, 6, 6),
        ["orbs"] = (2, 3, 3), ["circlets"] = (1, 2, 3), ["helms"] = (2, 2, 3),
        ["body"] = (3, 4, 6), ["shields"] = (3, 3, 4), ["gloves"] = (1, 2, 2),
        ["boots"] = (2, 2, 3), ["belts"] = (1, 1, 2), ["rings"] = (1, 1, 1),
        ["amulets"] = (1, 1, 1),
    };

    public static int BandAt(string itype, int ilvl)
    {
        if (!Bands.TryGetValue(itype, out var b)) return 0;
        return ilvl <= 25 ? b.Item1 : ilvl <= 40 ? b.Item2 : b.Item3;
    }

    public static int MaxSockets(ItemBaseRow baseRow, int ilvl) =>
        Math.Min(baseRow.MaxSockets,
            Math.Min(BandAt(baseRow.Itype, ilvl), Math.Min(baseRow.Width * baseRow.Height, 6)));

    /// <summary>The §4.5 "sockets by item level" roll for a fresh white base.</summary>
    public static int Roll(Rng rng, ItemBaseRow baseRow, int ilvl) =>
        baseRow.QualityRolls ? rng.NextInt(MaxSockets(baseRow, ilvl) + 1) : 0;
}
