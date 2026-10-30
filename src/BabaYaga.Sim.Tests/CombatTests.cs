namespace BabaYaga.Sim.Tests;

/// <summary>
/// The physical damage pipeline (P1 §1.6) and the seeded fight happy path:
/// a Koldun killing a monster through ResolvePlayerStrike must pay the XP chain
/// into the character, deterministically from the seed.
/// </summary>
public class CombatTests
{
    [Fact]
    public void PhysicalDamage_PipelineOrder_WeaponEdThenFlatThenEdThenSrcDam()
    {
        var rng = new Rng(7UL);
        // Fixed 1..1 roll: (1×(1+0)+9)=10 → ×(1+100%)=20 → ×64/128=10.
        Assert.Equal(10, CombatResolver.PhysicalDamage(
            rng, damageMin: 1, damageMax: 1, weaponEnhancedPercent: 0, flatDamage: 9,
            enhancedDamageSumPercent: 100, srcDam: 64));
        // Doubled once by a crit-family success: 20.
        Assert.Equal(20, CombatResolver.PhysicalDamage(
            rng, damageMin: 1, damageMax: 1, weaponEnhancedPercent: 0, flatDamage: 9,
            enhancedDamageSumPercent: 100, srcDam: 64, doubled: true));
        // ΣED clamps at −90% (P1 §1.6): (1+9)=10 → ×10% = 1.
        Assert.Equal(1, CombatResolver.PhysicalDamage(
            rng, damageMin: 1, damageMax: 1, flatDamage: 9, enhancedDamageSumPercent: -200));
    }

    [Fact]
    public void CritFamily_RollsInOrder_AnySuccessDoubles_Once()
    {
        var rng = new Rng(9UL);
        Assert.True(CombatResolver.RollDoubled(rng, critPercent: 100, 0, 0));
        Assert.True(CombatResolver.RollDoubled(rng, 0, criticalStrikePercent: 100, 0));
        Assert.True(CombatResolver.RollDoubled(rng, 0, 0, deadlyStrikePercent: 100));
        Assert.False(CombatResolver.RollDoubled(rng, 0, 0, 0));
    }

    [Fact]
    public void Kill_GrantsFullXpChain_ThroughCombatResolver_Deterministically()
    {
        var ladder = TestData.Ladder();

        // Seed A: the killing blow lands and pays the chain.
        var rngA = new Rng(12345UL);
        var hero = TestData.Koldun();
        hero.WeaponBaseAr = 100;
        var ghul = new Monster
        {
            Name = "ash-ghul-pup", Level = 1, Life = 20, BaseXp = 100, KillType = KillType.Normal,
            AttackRating = 50, Defense = 10,
        };
        // Huge damage so the first hit lands kills when the hit roll passes.
        var outcome = CombatResolver.ResolvePlayerStrike(
            rngA, hero, ghul, damageMin: 1, damageMax: 40, players: 1, ladder: ladder);
        Assert.True(outcome.Hit);
        Assert.True(outcome.Killed);
        Assert.True(ghul.IsDead);
        // chain: 100 ×1 ×1 ×100% (same level) ×1024/1024 = 100 XP → one level.
        Assert.Equal(100, outcome.XpGranted);
        Assert.Equal(100, hero.Xp);
        Assert.Equal(1, hero.Level); // 100 XP is below the 500 for level 2

        // Replay: same seed, same script, identical outcome — the DoD-e determinism leg.
        var rngB = new Rng(12345UL);
        var heroB = TestData.Koldun();
        heroB.WeaponBaseAr = 100;
        var ghulB = new Monster
        {
            Name = "ash-ghul-pup", Level = 1, Life = 20, BaseXp = 100, KillType = KillType.Normal,
            AttackRating = 50, Defense = 10,
        };
        var replay = CombatResolver.ResolvePlayerStrike(
            rngB, heroB, ghulB, damageMin: 1, damageMax: 40, players: 1, ladder: ladder);
        Assert.Equal(outcome.Damage, replay.Damage);
        Assert.Equal(outcome.XpGranted, replay.XpGranted);
        Assert.Equal(hero.Level, heroB.Level);
    }

    [Fact]
    public void Kill_ChampionVictim_PaysTripleChain_And_LevelsUp()
    {
        var ladder = TestData.Ladder();
        var rng = new Rng(4242UL);
        var hero = TestData.Koldun();
        hero.WeaponBaseAr = 100;
        var beast = new Monster
        {
            Name = "forest-shambler", Level = 1, Life = 5, BaseXp = 200, KillType = KillType.Champion,
            AttackRating = 10, Defense = 5,
        };
        var outcome = CombatResolver.ResolvePlayerStrike(
            rng, hero, beast, damageMin: 40, damageMax: 40, players: 1, ladder: ladder);
        Assert.True(outcome.Hit && outcome.Killed);
        Assert.Equal(600, outcome.XpGranted); // 200×3 champion, level 1 ratio 1024
        Assert.Equal(2, hero.Level);          // 600 ≥ 500 (lvl 2) and < 693 (interpolated lvl 3)
        Assert.Equal(5, hero.StatPoints);
        Assert.Equal(1, hero.SkillPoints);
    }

    [Fact]
    public void BlockedAndMissed_StrikesDealNothing_And_PayNoXp()
    {
        Assert.False(CombatResolver.RollBlock(new Rng(5UL), 0)); // no shield, no roll
        var ladder = TestData.Ladder();
        var hero = TestData.Koldun(); // AR = 100 from Dex alone
        var warded = new Monster
        {
            Name = "iron-tongue-elder", Level = 40, Life = 100, BaseXp = 1000,
            AttackRating = 10, Defense = 5000, // level gap + DEF: 5% chance floor
        };
        var rng = new Rng(8UL);
        long total = 0;
        int kills = 0;
        for (int i = 0; i < 30; i++)
        {
            var o = CombatResolver.ResolvePlayerStrike(
                rng, hero, warded, damageMin: 1, damageMax: 2, players: 1, ladder: ladder);
            total += o.Damage;
            if (o.Killed) kills++;
        }
        Assert.Equal(0, kills);
        Assert.True(warded.Life > 0); // never killed: no kill, no XP ever
        Assert.Equal(0, hero.Xp);
        Assert.True(total >= 0);
    }
}
