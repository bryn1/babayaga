namespace BabaYaga.Sim.Tests;

/// <summary>Post M2/M3 merge: Vendor and AreaGraph must read the ONE canonical
/// Difficulty enum (the duplicate definition caused CS0101).</summary>
public class SharedDifficultyTests
{
    [Fact]
    public void VendorAndAreaGraph_ShareTheSameDifficultyType()
    {
        var vendorParam = typeof(Vendor).GetMethod(nameof(Vendor.SellCap))!
            .GetParameters().Single(p => p.Name == "difficulty").ParameterType;
        var areaParam = typeof(AreaGraph).GetMethod(nameof(AreaGraph.AreaMonsterLevel))!
            .GetParameters().Single(p => p.Name == "difficulty").ParameterType;
        Assert.Same(typeof(Difficulty), vendorParam);
        Assert.Same(typeof(Difficulty), areaParam);
    }
}
