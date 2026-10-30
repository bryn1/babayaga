namespace BabaYaga.Sim.Tests;

/// <summary>
/// Skill-grid contract (P1 §2.1–2.2) over the shipped Koldun table: content validation,
/// prerequisite rejection, tier point caps, row level requirements, synergy math on hard
/// points, and invalid-spend rejection (a rejected spend must not move state).
/// </summary>
public class SkillTreeTests
{
    /// A Koldun standing at level 30 with the 29 skill points those levels pay.
    private static (Character c, SkillTree tree, XpTable ladder) KoldunAtLevel30()
    {
        var ladder = TestData.Ladder();
        var c = TestData.Koldun();
        c.GrantXp(4_663_553, ladder);
        return (c, new SkillTree(TestData.KoldunSkills()), ladder);
    }

    [Fact]
    public void KoldunTable_Loads_ThreeTrees_AtLeastFourSkillsEach()
    {
        var data = TestData.KoldunSkills();
        Assert.Equal("koldun", data.ClassKey);
        Assert.Equal(3, data.TreeNames.Count);
        foreach (var tree in data.TreeNames)
            Assert.True(data.Skills.Count(s => s.Tree == tree) >= 4, $"tree {tree} too shallow");
        Assert.All(data.Skills, s => Assert.Equal(SkillDataLoader.MaxPointsForTier(s.Tier), s.MaxPoints));
    }

    [Fact]
    public void Spend_PrereqMissing_Rejected_NoStateMoves()
    {
        var (c, tree, _) = KoldunAtLevel30();
        var ex = Assert.Throws<SkillSpendRejectedException>(() => tree.Spend(c, "zarzewie"));
        Assert.Contains("requires", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, tree.HardPoints("zarzewie"));
        Assert.Equal(29, c.SkillPoints); // the point was never taken
    }

    [Fact]
    public void Spend_MaxTierPoints_RejectedOnFourthPointOfTier1()
    {
        var (c, tree, _) = KoldunAtLevel30();
        for (int i = 0; i < 3; i++) tree.Spend(c, "iskra"); // tier 1 cap is 3
        Assert.Equal(3, tree.HardPoints("iskra"));
        var ex = Assert.Throws<SkillSpendRejectedException>(() => tree.Spend(c, "iskra"));
        Assert.Contains("capped", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, tree.HardPoints("iskra"));
        Assert.Equal(26, c.SkillPoints); // only the three legal spends were paid
    }

    [Fact]
    public void Spend_RowLevelRequirement_RejectedUnderLevel6()
    {
        var ladder = TestData.Ladder();
        var c = TestData.Koldun();
        c.GrantXp(500, ladder); // level 2: one skill point, row 1 needs level 6
        var tree = new SkillTree(TestData.KoldunSkills());
        var ex = Assert.Throws<SkillSpendRejectedException>(() => tree.Spend(c, "zarzewie"));
        Assert.Contains("character level 6", ex.Reason);
        Assert.Equal(1, c.SkillPoints);
    }

    [Fact]
    public void Spend_UnknownSkill_And_EmptyBudget_Rejected()
    {
        var (c, tree, _) = KoldunAtLevel30();
        Assert.Throws<ArgumentException>(() => tree.Spend(c, "not-a-skill-at-all"));
        var broke = TestData.Koldun(); // level 1, no skill points at all
        var ex = Assert.Throws<SkillSpendRejectedException>(() => tree.Spend(broke, "iskra"));
        Assert.Contains("skill point", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Synergy_HardPointMath_PlusSkillsNeverInflateIt()
    {
        var (c, tree, _) = KoldunAtLevel30();
        tree.Spend(c, "iskra");
        tree.Spend(c, "iskra"); // Iskra 2 hard points
        tree.Spend(c, "zarzewie");
        tree.Spend(c, "pochodnia");
        tree.Spend(c, "popielna_burza"); // prereqs now met at level 30
        // Burza synergy: Iskra 2×8 + Zarzewie 1×10 = 26 (P1 §2.2 blvl lines).
        Assert.Equal(26, tree.SynergyBonusPercent("popielna_burza"));
        // +skills move own lines only — synergy lines read hard points and stay put.
        tree.PlusSkillLevels = 7;
        Assert.Equal(26, tree.SynergyBonusPercent("popielna_burza"));
        Assert.Equal(8, tree.EffectiveLevel("popielna_burza")); // 1 hard + 7 plus
        Assert.True(tree.Knows("popielna_burza"));
        Assert.False(tree.Knows("kletwa"));
    }

    [Fact]
    public void InvalidStatSpend_Rejected_NoDebt()
    {
        var c = TestData.Koldun();
        Assert.False(c.TryAllocateStat(AllocatableStat.Vitality, 1)); // level-1 pool is empty
        Assert.Equal(0, c.VitAlloc);
        c.GrantXp(500, TestData.Ladder());
        Assert.False(c.TryAllocateStat(AllocatableStat.Vitality, 6)); // more than the pool
        Assert.True(c.TryAllocateStat(AllocatableStat.Vitality, 5));
        Assert.Equal(5, c.VitAlloc);
        Assert.Equal(0, c.StatPoints);
    }
}
