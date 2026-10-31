namespace BabaYaga.Sim;

/// <summary>
/// Rare (yellow) itemgen (P2 §4.3): 3 prefixes + 3 suffixes drawn from the
/// rare-allowed pool, no duplicate groups, the ilvl ≥ 27 gate, and the level-
/// requirement formula. The fetched VERIFIED crafted template is the working
/// template the spec names: req = highest random-affix req + 10 + 3 per random
/// affix, at most 98. A pool too thin at low alvl yields fewer affixes (the
/// count rule is ≤3 per side, never more).
/// </summary>
public static class RareGen
{
    /// <summary>Canon gate for a rare roll at all (P2 §4.3, "ilvl ≥ 27").</summary>
    public const int IlvlGate = 27;

    public static bool Gates(int ilvl) => ilvl >= IlvlGate;

    /// <summary>
    /// Build a rare item: quality Rare, up to 3+3 rare-allowed affixes under the
    /// one-affix-per-group exclusion, requirement per the §4.3 template. Returns
    /// false (no item) when the base/ilvl cannot support a rare at all — the
    /// caller then falls through the quality pipeline (magic/white).
    /// </summary>
    public static bool TryBuild(
        Rng rng, ItemBaseRow baseRow, int ilvl, AffixPool pool, out Item? item)
    {
        item = null;
        if (!Gates(ilvl)) return false;
        int alvl = ItemLevels.AffixLevel(ilvl, baseRow.Qlvl, baseRow.MagicLevelAdd);
        var rareSides = new List<AffixDef>();
        var used = new HashSet<string>(StringComparer.Ordinal);

        for (int side = 0; side < 2; side++)
        {
            bool prefix = side == 0;
            for (int i = 0; i < 3; i++)
            {
                var affix = AffixPool.Pick(
                    rng, pool.Eligible(prefix, alvl, baseRow, forRare: true, used));
                if (affix is null) break; // pool exhausted at this alvl → fewer affixes
                used.Add(affix.Group);
                rareSides.Add(affix);
            }
        }
        if (rareSides.Count == 0) return false; // §4.3: a "rare" with zero affixes is not a rare

        item = new Item(baseRow, ilvl) { Quality = ItemQuality.Rare };
        int highestAffixReq = 0;
        foreach (var affix in rareSides)
        {
            AffixPool.Apply(rng, item, affix);
            highestAffixReq = Math.Max(highestAffixReq, affix.LevelRequirement);
        }
        // Display keeps one name per side (presentation simplification, §4.3 names are cosmetic).
        item.LevelRequirement = LevelRequirement(highestAffixReq, rareSides.Count);
        return true;
    }

    /// <summary>§4.3 template: highest random-affix req, plus 10, plus 3 for each
    /// random affix, at most 98.</summary>
    public static int LevelRequirement(int highestAffixLevelReq, int affixCount) =>
        Math.Min(98, highestAffixLevelReq + 10 + 3 * affixCount);
}
