using Xunit;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// Boundary pins for the two mutations that SURVIVED the round-1 battery (DA P2-3):
/// widening the rare gate 27 → 28 and deleting the row-minimum clamp of the
/// c-formula both kept the old 120-test suite green. Each test here pins the
/// exact edge; deleting or widening the guarded rule must turn it RED.
/// </summary>
public class BoundaryTests
{
    private static ItemBaseTable Bases() =>
        ItemBaseTable.Load(Path.Combine(TestData.DataDir(), "item-bases.json"));
    private static AffixPool Pool() => AffixPool.Load(Path.Combine(TestData.DataDir(), "affixes.json"));

    /// <summary>Pin #1: the §4.3 rare gate opens at EXACTLY ilvl 27 — not 26, not
    /// 28. A 27 → 28 mutation of RareGen.IlvlGate fails all three of these.</summary>
    [Fact]
    public void RareGate_OpensAtExactly27()
    {
        Assert.False(RareGen.Gates(26));
        Assert.True(RareGen.Gates(27));
        Assert.True(RareGen.TryBuild(new Rng(27UL), Bases().For("szpula"), ilvl: 27, Pool(), out var built));
        Assert.Equal(ItemQuality.Rare, built!.Quality);
        Assert.False(RareGen.TryBuild(new Rng(27UL), Bases().For("szpula"), ilvl: 26, Pool(), out _));
    }

    /// <summary>Pin #2: huge MF shrinks c below the row floor BEFORE the clamp —
    /// deleting the Math.Max clamp leaks the raw floors (3 and 48 here), and each
    /// tier would auto-succeed at any ilvl. The clamp keeps both at 1 × 128.</summary>
    [Fact]
    public void HugeMagicFind_ClampsAtRowMinimum()
    {
        var magic = QualityRoll.Classic.Single(r => r.Tier == "Magic");   // 30/1, min 1 → raw floor 3
        var unique = QualityRoll.Classic.Single(r => r.Tier == "Unique"); // 400/2, min 1 → raw floor 48
        Assert.Equal(128, QualityRoll.CValue(magic, ilvl: 10, qlvl: 10, magicFind: 100_000, tcFactor: 0));
        Assert.Equal(128, QualityRoll.CValue(unique, ilvl: 40, qlvl: 5, magicFind: 100_000, tcFactor: 0));
    }
}
