namespace BabaYaga.Sim.Tests;

/// <summary>
/// The M1 numeric contract: to-hit 5/95 clamps, block 75 cap (and running ÷3),
/// elemental 75 resist cap, physical pipeline flooring, monster resist/immunity,
/// the exact XP ladder, level-up grants, and the XP chain factors.
/// </summary>
public class MechanicsTests
{
    // --- to-hit (P1 §1.4) ---

    [Fact]
    public void ToHit_ClampsAt95_And_5()
    {
        Assert.Equal(95, CombatResolver.ChanceToHit(1_000_000, 1, 99, 1));
        Assert.Equal(5, CombatResolver.ChanceToHit(1, 1_000_000, 1, 99));
    }

    [Fact]
    public void ToHit_DualRatio_MidValueIsExact()
    {
        // AR == DEF → 100%; attacker level == defender level → 50% → 50.
        Assert.Equal(50, CombatResolver.ChanceToHit(2000, 2000, 20, 20));
    }

    [Fact]
    public void ToHit_RunningDefender_MeleeAlwaysHits()
    {
        var rng = new Rng(1UL);
        // AR deliberately hopeless; running overrides the roll (P1 §1.4).
        Assert.True(CombatResolver.RollHit(rng, 1, 1_000_000, 1, 99, defenderIsRunning: true));
    }

    // --- block (P1 §1.5) ---

    [Fact]
    public void Block_CapsAt75_AfterFormula()
    {
        // (shield 80 + bonus 40 + factor 20) × (Dex 60−15) / (2×1) is huge → cap 75.
        Assert.Equal(75, CombatResolver.ChanceToBlock(80, 40, 20, 60, 1, running: false));
    }

    [Fact]
    public void Block_BelowCap_IsExact_And_RunningDividesByThreeAfterCap()
    {
        // (50 + 0 + 20) × (35−15) / (2×10) = 70×20/20 = 70.
        Assert.Equal(70, CombatResolver.ChanceToBlock(50, 0, 20, 35, 10, running: false));
        Assert.Equal(23, CombatResolver.ChanceToBlock(50, 0, 20, 35, 10, running: true)); // floor(70/3)
        Assert.Equal(0, CombatResolver.ChanceToBlock(0, 50, 20, 60, 1, running: false));  // no shield = no block
        Assert.Equal(0, CombatResolver.ChanceToBlock(80, 0, 20, 15, 1, running: false));  // Dex−15 term → 0
    }

    [Fact]
    public void Block_ClassFactor_LiftsBlockAboveAFactorlessShield()
    {
        Assert.Equal(20, Stats.Koldun.BlockClassFactor);
        // Level 30 keeps both results under the 75 cap so the factor is visible.
        int withFactor = CombatResolver.ChanceToBlock(40, 0, Stats.Koldun.BlockClassFactor, 40, 30, false);
        int withoutFactor = CombatResolver.ChanceToBlock(40, 0, 0, 40, 30, false);
        Assert.Equal(25, withFactor);    // (40+0+20)×(40−15)/(2×30) = 1500/60
        Assert.Equal(16, withoutFactor); // (40+0+0)×25/60 = 1000/60
    }

    // --- elemental + resist cap (P2 §3.1) ---

    [Fact]
    public void Elemental_Cap75_NegativeAmplifies_FloorMinus100()
    {
        Assert.Equal(25, CombatResolver.ElementalDamage(100, 80));      // capped at 75
        Assert.Equal(25, CombatResolver.ElementalDamage(100, 75));
        Assert.Equal(70, CombatResolver.ElementalDamage(100, 30));
        Assert.Equal(150, CombatResolver.ElementalDamage(100, -50));    // −50% ⇒ ×1.5
        Assert.Equal(200, CombatResolver.ElementalDamage(100, -200));   // floored at −100
    }

    [Fact]
    public void Elemental_MaxResistBonus_RaisesCapToAtMost95()
    {
        Assert.Equal(20, CombatResolver.ElementalDamage(100, 80, maxResistBonusPercent: 20)); // cap 95: raw 80 passes
        Assert.Equal(5, CombatResolver.ElementalDamage(100, 100, maxResistBonusPercent: 40)); // ceiling 95
    }

    [Fact]
    public void Monster_ResistIsUncapped_And_ImmuneAt100()
    {
        var scorched = new Monster { Name = "zmora-spark", Life = 100, ElementalResistPercent = 90 };
        Assert.Equal(10, scorched.TakeElemental(100)); // monsters take the raw 90, not the 75 cap
        var immune = new Monster { Name = "ash-ghul", Life = 100, ElementalResistPercent = 100 };
        Assert.Equal(0, immune.TakeElemental(500));
        Assert.False(immune.IsDead);
    }

    // --- life formula (P1 §1.2–1.3) ---

    [Fact]
    public void Life_Formula_LevelVitAndMultiplier()
    {
        var k = TestData.Koldun();
        Assert.Equal(50, k.MaxLife); // Vit 20 + flat 30 at level 1
        Assert.Equal(500, TestData.Ladder().TotalXpForLevel(2));
        Assert.Equal(1, k.GrantXp(500, TestData.Ladder())); // exactly one level from 500 XP
        var k2 = TestData.Koldun();
        k2.GrantXp(500, TestData.Ladder());   // level 2, 5 stat points
        Assert.True(k2.TryAllocateStat(AllocatableStat.Vitality, 5));
        // level 2 base = 50 + 2×1 + 2×5 = 62.
        Assert.Equal(62, k2.MaxLife);
        k2.IncreaseMaxLifePercent = 50;
        k2.VitFromItems = 10;
        // (62)×1.5 = 93 + LifePerVit 2 × 10 item-Vit = 113 (item Vit joins after multiplier).
        Assert.Equal(113, k2.MaxLife);
    }

    [Fact]
    public void AttackRating_DexAtFive_PerPoint_ThenPercent()
    {
        var k = TestData.Koldun(); // Dex 20 → AR 100 from Dex.
        Assert.Equal(100, k.AttackRating);
        k.FlatAr = 50;
        k.ArPercent = 50;
        Assert.Equal(225, k.AttackRating); // (100+50)×1.5
    }

    // --- XP ladder + grants + chain (P1 §1.7, §1.1) ---

    [Fact]
    public void XpLadder_AnchorThresholds_AreExact()
    {
        var ladder = TestData.Ladder();
        Assert.Equal(500, ladder.TotalXpForLevel(2));
        Assert.Equal(4_663_553, ladder.TotalXpForLevel(30));
        Assert.Equal(47_116_709, ladder.TotalXpForLevel(50));
        Assert.Equal(285_041_630, ladder.TotalXpForLevel(70));
        Assert.Equal(3_520_485_254, ladder.TotalXpForLevel(99));
        Assert.Equal(1024, ladder.ExpRatioForLevel(30));
        Assert.Equal(976, ladder.ExpRatioForLevel(70));
        Assert.Equal(6, ladder.ExpRatioForLevel(98));
        Assert.Equal(0, ladder.ExpRatioForLevel(99));
    }

    [Fact]
    public void XpLadder_LevelForXp_MatchesThresholds()
    {
        var ladder = TestData.Ladder();
        Assert.Equal(1, ladder.LevelForXp(499));
        Assert.Equal(2, ladder.LevelForXp(500));
        Assert.Equal(29, ladder.LevelForXp(4_663_552));
        Assert.Equal(30, ladder.LevelForXp(4_663_553));
    }

    [Fact]
    public void LevelUp_GrantsFiveStatAndOneSkillPoint_And_StopsAt99()
    {
        var ladder = TestData.Ladder();
        var k = TestData.Koldun();
        Assert.Equal(1, k.GrantXp(500, ladder));
        Assert.Equal(2, k.Level);
        Assert.Equal(5, k.StatPoints);
        Assert.Equal(1, k.SkillPoints);
        k.GrantXp(4_663_553, ladder); // crosses many levels at once
        Assert.Equal(30, k.Level);
        Assert.Equal(5 * 29, k.StatPoints); // 29 levels × 5, none spent
        Assert.Equal(29, k.SkillPoints);

        var capped = TestData.Koldun();
        capped.GrantXp(9_000_000_000L, ladder);
        Assert.Equal(99, capped.Level);
        Assert.Equal(0, capped.GrantXp(1_000_000, ladder)); // lvl 99 gains nothing (P1 §1.7)
    }

    [Fact]
    public void XpChain_KillTypePlayersLevelGapRatio_AllFloor()
    {
        var ladder = TestData.Ladder();
        // base 200, champion (×3), 2 players (+1/2 ⇒ ×1.5 → 300×2 wait: 200×3=600; ×(2+1)/2=900),
        // same level (gap 100%), level 1 ratio 1024 → 900.
        Assert.Equal(900, ladder.KillXp(200, KillType.Champion, players: 2, characterLevel: 1, monsterLevel: 1));
        // unique ×5, solo, monster 26+ levels below → 5%: 200×5=1000 ×1 ×5% = 50.
        Assert.Equal(50, ladder.KillXp(200, KillType.Unique, players: 1, characterLevel: 40, monsterLevel: 1));
        // clvl 25+ vs higher monster: your level ÷ its level: 30/60 = 50%: 100×1×1×50% = 50.
        Assert.Equal(50, ladder.KillXp(100, KillType.Normal, players: 1, characterLevel: 30, monsterLevel: 60));
        // under clvl 25, monster higher: 88/68/36/15/2% table: clvl 1 vs lvl 20 monster → above 15 band = 15%.
        Assert.Equal(15, ladder.KillXp(100, KillType.Normal, players: 1, characterLevel: 1, monsterLevel: 20));
    }

    [Fact]
    public void WeaponAttributeDamage_PerGroupPerPoint()
    {
        Assert.Equal(30, Stats.WeaponAttributeDamagePercent(WeaponGroup.BladeOrBluntOrStaff, 30, 10));
        Assert.Equal(33, Stats.WeaponAttributeDamagePercent(WeaponGroup.HammerOrMaul, 30, 10)); // 1.1%/Str
        Assert.Equal(30, Stats.WeaponAttributeDamagePercent(WeaponGroup.BowOrCrossbow, 30, 30)); // Dex only
        Assert.Equal(30, Stats.WeaponAttributeDamagePercent(WeaponGroup.LightOrThrownOrClaw, 20, 20)); // 0.75×40
    }
}
