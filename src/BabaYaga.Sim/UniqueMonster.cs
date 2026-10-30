namespace BabaYaga.Sim;

/// <summary>
/// The unique-modifier roll (P3 §5.3): a unique takes one modifier in Ordinary,
/// two in Nightmare, three in Hell, each drawn without replacement from one pool
/// with equal chance. The stat bonus rows themselves live in the pack bonus tables
/// (life +300/200/100 %, AR +90/75/66 %, level +3, XP ×5 — data/packs.json).
/// </summary>
public static class UniqueRoller
{
    /// One → three rolled modifiers by difficulty band.
    public static int AffixCountFor(Difficulty difficulty) => 1 + (int)difficulty;

    /// Draw count distinct affixes with equal chance (partial Fisher-Yates on a
    /// copied index list — deterministic from the seeded Rng, pool order untouched).
    public static IReadOnlyList<PackAffixRow> RollAffixes(
        Rng rng, Difficulty difficulty, IReadOnlyList<PackAffixRow> pool)
    {
        int count = AffixCountFor(difficulty);
        if (pool.Count < count)
            throw new ArgumentException($"pool holds {pool.Count} rows, unique rolls {count}");
        var order = new int[pool.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        var rolled = new List<PackAffixRow>(count);
        for (int i = 0; i < count; i++)
        {
            int j = i + rng.NextInt(order.Length - i);
            (order[i], order[j]) = (order[j], order[i]);
            rolled.Add(pool[order[i]]);
        }
        return rolled;
    }
}
