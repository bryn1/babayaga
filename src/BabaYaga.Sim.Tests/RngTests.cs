using Xunit.Abstractions;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// PRNG determinism + the seeded combat/stat units the same seed must replay.
/// (T0c: the pytest gate is deterministic — same seed, identical run, every platform.)
/// </summary>
public class RngTests(ITestOutputHelper output)
{
    [Fact]
    public void SameSeed_TwoRuns_IdenticalSequence()
    {
        var a = new Rng(0xC0FFEEUL);
        var b = new Rng(0xC0FFEEUL);
        var seqA = Enumerable.Range(0, 64).Select(_ => a.NextUInt()).ToArray();
        var seqB = Enumerable.Range(0, 64).Select(_ => b.NextUInt()).ToArray();
        Assert.Equal(seqA, seqB);
        // Printed for the run-log determinism check (DoD e): identical across runs.
        output.WriteLine("RNG-SEQ " + string.Join(',', seqA.Take(8)));
    }

    [Fact]
    public void DifferentSeed_DifferentSequence()
    {
        var a = new Rng(1UL);
        var b = new Rng(2UL);
        Assert.NotEqual(
            Enumerable.Range(0, 8).Select(_ => a.NextUInt()).ToArray(),
            Enumerable.Range(0, 8).Select(_ => b.NextUInt()).ToArray());
    }

    [Fact]
    public void NextInt_HonoursBounds_And_NextInRange_IncludesEndpoints()
    {
        var rng = new Rng(42UL);
        var seen = new HashSet<int>();
        for (int i = 0; i < 2000; i++)
        {
            Assert.InRange(rng.NextInt(100), 0, 99);
            Assert.InRange(rng.NextPercent(), 0, 99);
            int r = rng.NextInRange(3, 7);
            Assert.InRange(r, 3, 7);
            seen.Add(r);
        }
        Assert.Contains(3, seen); // range endpoints are reachable
        Assert.Contains(7, seen);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rng(1UL).NextInt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rng(1UL).NextInRange(9, 3));
    }

    [Fact]
    public void SameSeed_DamageRolls_Replay()
    {
        long RollAll(Rng rng) => Enumerable.Range(0, 50)
            .Aggregate(0L, (acc, _) => acc + CombatResolver.PhysicalDamage(rng, 2, 9, 25, 3, 40));
        Assert.Equal(RollAll(new Rng(7777UL)), RollAll(new Rng(7777UL)));
    }
}
