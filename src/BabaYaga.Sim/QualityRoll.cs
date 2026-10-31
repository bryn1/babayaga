namespace BabaYaga.Sim;

/// <summary>One itemratio.txt-style row (P2 §4.1): the ratio, the divisor applied to
/// (ilvl − qlvl), and the per-row minimum c keeps (floor, in ratio units).</summary>
public sealed record QualityRatioRow(string Tier, int Ratio, int Divisor, int Minimum);

/// <summary>
/// The quality roll (P2 §4.1). Order of tries on every dropped base is
/// unique → set → rare → magic, first success wins; a failed magic falls to the
/// superior/normal/low split. The check per tier:
///   c = (ratio − trunc0((ilvl − qlvl)/divisor)) × 128
///   c = floor(c × 100 / (100 + effective_MF)), at least the row minimum
///   c = c − floor(c × TC_quality_factor / 1024)
///   success chance = 128/c; c ≤ 128 auto-succeeds.
/// MF touches quality here and nowhere else — it never changes item quantity (§4.8).
/// </summary>
public static class QualityRoll
{
    /// <summary>Classic (Version-0) itemratio rows (P2 §4.1 VERIFIED): plain
    /// Unique 400 divisor 2 / Rare 160 / Set 125 / Magic 30; Uber 240/96/96/3.</summary>
    public static IReadOnlyList<QualityRatioRow> Classic { get; } =
    [
        new("Unique", 400, 2, 1),
        new("Set", 125, 1, 1),
        new("Rare", 160, 1, 1),
        new("Magic", 30, 1, 1),
    ];

    public static IReadOnlyList<QualityRatioRow> Uber { get; } =
    [
        new("Unique", 240, 2, 1),
        new("Set", 96, 1, 1),
        new("Rare", 96, 1, 1),
        new("Magic", 3, 1, 1),
    ];

    /// <summary>True when ilvl−qlvl is big enough for the row to auto-succeed (c ≤ 128).</summary>
    public static bool AutoSucceeds(QualityRatioRow row, int ilvl, int qlvl) =>
        CValue(row, ilvl, qlvl, magicFind: 0, tcFactor: 0) <= 128;

    /// <summary>The c value for one tier attempt (P2 §4.1, the four fetched lines).</summary>
    public static int CValue(
        QualityRatioRow row, int ilvl, int qlvl, int magicFind, int tcFactor)
    {
        int shift = (ilvl - qlvl) / row.Divisor; // C# integer division truncates toward 0 (§4.1)
        long c = (long)(row.Ratio - shift) * 128;
        c = c * 100 / (100 + Math.Max(magicFind, 0)); // floor; MF is quality-only
        c = Math.Max(c, (long)row.Minimum * 128);     // at least the row minimum
        c -= c * Math.Max(tcFactor, 0) / 1024;        // TC quality factor
        return (int)c;
    }

    /// <summary>
    /// Roll the quality of one dropped base. Unique/Set attempts are only made when
    /// the caller says a spawnable row exists at this ilvl (the pick tables in
    /// SetUniqueItems.cs); a rare attempt needs ilvl ≥ 27 (the §4.3 gate).
    /// magicFind only shifts these odds; quantity is a treasure-class matter, never MF's (§4.8).
    /// </summary>
    public static ItemQuality Roll(
        Rng rng,
        ItemBaseRow baseRow,
        int ilvl,
        int magicFind = 0,
        int tcQualityFactor = 0,
        IReadOnlyList<QualityRatioRow>? ratios = null,
        bool uniqueCandidateExists = false,
        bool setCandidateExists = false)
    {
        // Gems/runes/potions never roll: their type gates the tiers (P2 §4.1).
        if (!baseRow.QualityRolls) return ItemQuality.Normal;

        ratios ??= Classic;
        foreach (var row in ratios)
        {
            ItemQuality tier = row.Tier switch
            {
                "Unique" => ItemQuality.Unique,
                "Set" => ItemQuality.Set,
                "Rare" => ItemQuality.Rare,
                "Magic" => ItemQuality.Magic,
                _ => throw new InvalidDataException($"unknown quality row '{row.Tier}'"),
            };
            if (tier == ItemQuality.Unique && !uniqueCandidateExists) continue;
            if (tier == ItemQuality.Set && !setCandidateExists) continue;
            if (tier == ItemQuality.Rare && ilvl < RareGen.IlvlGate) continue; // §4.3 gate

            int c = CValue(row, ilvl, baseRow.Qlvl, magicFind, tcQualityFactor);
            if (c <= 128 || rng.NextInt(c) < 128) return tier; // chance = 128/c
        }
        return RollWhite(rng, baseRow, ilvl);
    }

    /// <summary>
    /// The white split after every tier failed. Original design row (the fetched spec
    /// GAPs the exact hi/lo chances): ±(ilvl−qlvl) drives both sides, capped so a
    /// generous ilvl never guarantees either end.
    /// </summary>
    public static ItemQuality RollWhite(Rng rng, ItemBaseRow baseRow, int ilvl)
    {
        int gap = ilvl - baseRow.Qlvl;
        if (gap > 0 && rng.NextPercent() < Math.Min(gap * 2, 50)) return ItemQuality.Superior;
        if (gap < 0 && rng.NextPercent() < Math.Min(-gap / 2, 40)) return ItemQuality.LowQuality;
        return ItemQuality.Normal;
    }
}
