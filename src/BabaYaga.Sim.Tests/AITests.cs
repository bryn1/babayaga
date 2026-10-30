namespace BabaYaga.Sim.Tests;

/// <summary>
/// The four behaviour scripts (P3 §5.5) on a deterministic grid: melee strikes in
/// range, the ranged attacker herds its target into the distance band, the charge
/// commits to closing, and the avoider breaks off when pressed. Pure functions —
/// no rolls — so every assertion is an exact coordinate claim (DoD d).
/// </summary>
public class AITests
{
    private static readonly WorldPos Origin = new(0, 0);

    [Fact]
    public void Melee_AttacksInRange_ApproachesOutOfIt()
    {
        var p = AiProfile.Default(AiBehaviour.Melee);
        Assert.Equal(AiAction.Attack, MonsterAi.Decide(p, Origin, new WorldPos(1, 0)));
        Assert.Equal(AiAction.Attack, MonsterAi.Decide(p, Origin, new WorldPos(1, 1))); // diagonal counts
        Assert.Equal(AiAction.Approach, MonsterAi.Decide(p, Origin, new WorldPos(2, 0)));
        Assert.Equal(AiAction.Approach, MonsterAi.Decide(p, Origin, new WorldPos(9, 9)));
    }

    [Fact]
    public void Ranged_KeepsDistanceBand_HerdsTargetIntoIt()
    {
        var p = AiProfile.Default(AiBehaviour.Ranged); // band [2, 6]
        Assert.Equal(AiAction.Withdraw, MonsterAi.Decide(p, Origin, new WorldPos(1, 0)));  // under band
        Assert.Equal(AiAction.Withdraw, MonsterAi.Decide(p, Origin, new WorldPos(0, 1)));
        Assert.Equal(AiAction.Attack, MonsterAi.Decide(p, Origin, new WorldPos(2, 0)));    // band floor
        Assert.Equal(AiAction.Attack, MonsterAi.Decide(p, Origin, new WorldPos(6, 6)));    // band ceiling
        Assert.Equal(AiAction.Approach, MonsterAi.Decide(p, Origin, new WorldPos(7, 0)));  // over band
        Assert.Equal(AiAction.Approach, MonsterAi.Decide(p, Origin, new WorldPos(20, 20)));
    }

    [Fact]
    public void Charge_ClosesDistance_ThenStrikes()
    {
        var p = AiProfile.Default(AiBehaviour.Charge);
        var target = new WorldPos(5, 5);
        var self = Origin;
        int previous = self.DistanceTo(target);
        for (int step = 0; step < 10 && previous > p.AttackRange; step++)
        {
            Assert.Equal(AiAction.Approach, MonsterAi.Decide(p, self, target));
            self = MonsterAi.StepToward(self, target);
            int now = self.DistanceTo(target);
            Assert.True(now < previous, $"step {step}: distance {previous} did not close to {now}");
            previous = now;
        }
        Assert.Equal(AiAction.Attack, MonsterAi.Decide(p, self, target));
        // And a charger never retreats, even half-dead: it commits.
        Assert.Equal(AiAction.Approach, MonsterAi.Decide(p, Origin, target, hpPercent: 5));
    }

    [Fact]
    public void Avoid_RetreatsUnderThreshold_HoldsBeyondIt()
    {
        var p = AiProfile.Default(AiBehaviour.Avoid); // flees within 4 units
        Assert.Equal(AiAction.Withdraw, MonsterAi.Decide(p, Origin, new WorldPos(4, 0)));  // at threshold
        Assert.Equal(AiAction.Withdraw, MonsterAi.Decide(p, Origin, new WorldPos(2, 2)));
        Assert.Equal(AiAction.Hold, MonsterAi.Decide(p, Origin, new WorldPos(5, 0)));      // beyond it
        Assert.Equal(AiAction.Hold, MonsterAi.Decide(p, Origin, new WorldPos(10, 3)));

        // A hurt avoider also breaks off below its wound line, whatever the range.
        var skittish = p with { FleeAtHpPercent = 30 };
        Assert.Equal(AiAction.Withdraw, MonsterAi.Decide(skittish, Origin, new WorldPos(10, 10), hpPercent: 20));
        Assert.Equal(AiAction.Hold, MonsterAi.Decide(skittish, Origin, new WorldPos(10, 10), hpPercent: 90));
    }

    [Fact]
    public void Steps_ChebyshevOneUnit_EachAxisOrStays()
    {
        Assert.Equal(new WorldPos(1, 1), MonsterAi.StepToward(Origin, new WorldPos(5, 5)));
        Assert.Equal(new WorldPos(1, 0), MonsterAi.StepToward(Origin, new WorldPos(5, 0)));
        Assert.Equal(new WorldPos(-1, -1), MonsterAi.StepAway(Origin, new WorldPos(2, 2)));
        // One step toward strictly closes the Chebyshev gap by one; one step away
        // strictly widens it by one.
        var threat = new WorldPos(3, 3);
        Assert.Equal(1, Origin.DistanceTo(threat) - MonsterAi.StepToward(Origin, threat).DistanceTo(threat));
        Assert.Equal(-1, Origin.DistanceTo(threat) - MonsterAi.StepAway(Origin, threat).DistanceTo(threat));
    }

    [Fact]
    public void Decide_IsPure_SameInputsSameAction()
    {
        var p = AiProfile.Default(AiBehaviour.Ranged);
        var first = MonsterAi.Decide(p, Origin, new WorldPos(6, 2));
        for (int i = 0; i < 50; i++)
            Assert.Equal(first, MonsterAi.Decide(p, Origin, new WorldPos(6, 2)));
    }
}
