namespace BabaYaga.Sim;

/// <summary>
/// Deterministic seeded PRNG: PCG-XSH-RR 32/64 (output hash of a 64-bit LCG).
/// Pure integer arithmetic, so one seed yields the identical sequence on every
/// platform and run. The BCL Random is deliberately NOT used: its sequence is
/// runtime-version dependent and no cross-platform contract.
/// </summary>
public sealed class Rng
{
    // Standard PCG constants (multiplier = Knuth MMIX, increment = stream 0).
    private const ulong Multiplier = 6364136223846793005UL;
    private const ulong Increment = 1442695040888963407UL;

    private ulong _state;

    public Rng(ulong seed)
    {
        // PCG seeding step: advance once, absorb the seed, advance again.
        _state = 0UL;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    /// <summary>Next 32-bit output of the stream (PCG-XSH-RR).</summary>
    public uint NextUInt()
    {
        ulong old = _state;
        _state = old * Multiplier + Increment;
        uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
    }

    /// <summary>
    /// Uniform integer in [0, maxExclusive), unbiased (rejection on the tail of the
    /// range; the loop runs more than once only with probability ≤ range/2^32).
    /// </summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "range must be positive");
        uint range = (uint)maxExclusive;
        uint bits, val;
        do
        {
            bits = NextUInt();
            val = bits % range;
        } while (bits - val + (range - 1) < bits);
        return (int)val;
    }

    /// <summary>Uniform integer roll 0..99 for percent chances (to-hit, block, crit).</summary>
    public int NextPercent() => NextInt(100);

    /// <summary>Uniform integer in [minInclusive, maxInclusive], e.g. a weapon damage roll.</summary>
    public int NextInRange(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxInclusive), "max below min");
        return minInclusive + NextInt(maxInclusive - minInclusive + 1);
    }
}
