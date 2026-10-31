namespace BabaYaga.Sim.Tests;

/// <summary>
/// M4-S5 POTION BELT (LEDGER row 69, card/11832): the 4-cell belt as session
/// state + the UsePotion verb. Contract per the pinned design: UsePotion consumes
/// the potion from carried AND belt and raises Life (or Mana) by 40 % of Max,
/// CLAMPED to Max; every rejection (empty slot, occupied slot, wrong item kind,
/// bad slot/index range, pool already at max) throws SessionRejectedException and
/// leaves NOTHING half-applied. XP is untouched by a potion; the mana formula and
/// SpendSkill are NOT modified here. The life/mana POOLS are the session's current
/// values — real monster counter-damage lowers them (Attack pays one counter per
/// surviving monster on a third seeded stream, so the player-side combat stream
/// stays bit-identical). Koldun level-1 anchors: MaxLife 50, MaxMana 30.
/// </summary>
public class BeltPotionTests
{
    private const string LifePotion = "napitek-zycia";
    private const string ManaPotion = "napitek-manny";

    private static GameSession Fresh(ulong seed = 9) => GameSession.New(seed, "koldun", TestData.DataDir());

    /// Buys one row through the real vendor door and hands back the carried copy.
    private static Item BuyOne(GameSession s, string baseId)
    {
        s.Buy(baseId);
        return s.Carried[^1];
    }

    // ---- the vendor sells both kinds in town (Vendor.Standard terms) ----

    [Fact]
    public void VendorStandard_SellsBothPotions_InTown_AtBaseCost()
    {
        var s = Fresh();
        Assert.Equal(s.Areas.TownKey, s.CurrentArea); // a fresh session stands in town

        long gold0 = s.Gold;
        Assert.Equal(60, s.Buy(LifePotion)); // cost 60 × 1024/1024 on the Standard profile
        Assert.Equal(50, s.Buy(ManaPotion)); // cost 50 — both are Vendor.Standard purchasable
        Assert.Equal(gold0 - 110, s.Gold);
        Assert.Equal(2, s.Carried.Count);
        Assert.Equal(LifePotion, s.Carried[0].Base.Id);
        Assert.Equal(ManaPotion, s.Carried[1].Base.Id);
    }

    // ---- UsePotion: exact 40 %-of-Max rise, then the clamp at Max ----

    [Fact]
    public void UsePotion_Life_RisesExactly40PercentOfMax_ConsumesCarriedAndBelt()
    {
        var s = Fresh();
        Assert.Equal(50, s.Character.MaxLife); // koldun L1 anchor the math below is read against
        var potion = BuyOne(s, LifePotion);
        s.Character.TakeDamage(25); // life 25/50
        s.AddToBelt(s.Carried.IndexOf(potion), 1);

        var r = s.UsePotion(1);

        Assert.Equal(1, r.Slot);
        Assert.Equal("life", r.Kind);
        Assert.Equal(25, r.Before);
        Assert.Equal(50, r.Max);
        Assert.Equal(45, r.After); // 25 + 50×40/100 = 45 — the exact 40 % rise
        Assert.Equal(45, s.Character.Life);
        Assert.Null(s.Belt[0]); // consumed from the belt…
        Assert.Empty(s.Carried); // …and from carried
    }

    [Fact]
    public void UsePotion_Life_ClampsAtMax_RisesToMaxOnly()
    {
        var s = Fresh();
        var potion = BuyOne(s, LifePotion);
        s.Character.TakeDamage(15); // life 35/50 — a 40 % rise would overshoot to 55
        s.AddToBelt(s.Carried.IndexOf(potion), 2);

        var r = s.UsePotion(2);

        Assert.Equal(50, r.After); // clamp: rises to Max ONLY, never past it
        Assert.Equal(s.Character.MaxLife, s.Character.Life);
    }

    [Fact]
    public void UsePotion_Mana_Exact40PercentRise_ThenClampSameShape()
    {
        var s = Fresh();
        Assert.Equal(30, s.Character.MaxMana); // koldun L1 anchor
        var potion = BuyOne(s, ManaPotion);
        s.Character.Mana = 10; // drain input — no mana DRAIN exists (SpendSkill formula untouched)
        s.AddToBelt(s.Carried.IndexOf(potion), 1);

        var r = s.UsePotion(1);
        Assert.Equal("mana", r.Kind);
        Assert.Equal(22, r.After); // 10 + 30×40/100 = 22 — exact, no clamp

        var second = BuyOne(s, ManaPotion);
        s.Character.Mana = 25; // a 12-rise would overshoot to 37
        s.AddToBelt(s.Carried.IndexOf(second), 1);
        Assert.Equal(30, s.UsePotion(1).After); // clamp at Max — same shape as life
    }

    // ---- every rejection path throws, nothing half-applies ----

    [Fact]
    public void UsePotion_EmptySlot_Throws()
    {
        var s = Fresh();
        var e = Assert.Throws<SessionRejectedException>(() => s.UsePotion(1));
        Assert.Contains("empty", e.Message);
    }

    [Fact]
    public void AddToBelt_WrongItemKind_Throws_SlotStaysEmpty()
    {
        var s = Fresh();
        s.Buy("szpula"); // a sword is not a potion
        Assert.Throws<SessionRejectedException>(() => s.AddToBelt(0, 1));
        Assert.Null(s.Belt[0]);
        Assert.Single(s.Carried); // the sword stays carried, unconsumed
    }

    [Fact]
    public void UsePotion_LifeAlreadyAtMax_Throws_NothingHalfApplied()
    {
        var s = Fresh();
        var potion = BuyOne(s, LifePotion);
        s.AddToBelt(s.Carried.IndexOf(potion), 1);

        Assert.Throws<SessionRejectedException>(() => s.UsePotion(1)); // Life == Max already

        Assert.Equal(s.Character.MaxLife, s.Character.Life); // pool untouched…
        Assert.Single(s.Carried); // …potion NOT consumed…
        Assert.NotNull(s.Belt[0]); // …slot still filled
    }

    [Fact]
    public void ManaPotion_AtMaxMana_Throws_LifeUntouched()
    {
        var s = Fresh();
        var potion = BuyOne(s, ManaPotion);
        s.AddToBelt(s.Carried.IndexOf(potion), 1);

        Assert.Throws<SessionRejectedException>(() => s.UsePotion(1));
        Assert.Equal(s.Character.MaxMana, s.Character.Mana);
        Assert.Single(s.Carried);
    }

    [Fact]
    public void AddToBelt_RejectsSlotAndIndexRanges_AndOccupiedSlots()
    {
        var s = Fresh();
        BuyOne(s, LifePotion);
        BuyOne(s, LifePotion);

        Assert.Throws<SessionRejectedException>(() => s.AddToBelt(0, 0)); // slots are 1–4
        Assert.Throws<SessionRejectedException>(() => s.AddToBelt(0, 5));
        Assert.Throws<SessionRejectedException>(() => s.AddToBelt(-1, 1)); // carried index range
        Assert.Throws<SessionRejectedException>(() => s.AddToBelt(2, 1));
        Assert.Throws<SessionRejectedException>(() => s.AddToBelt(99, 1));

        s.AddToBelt(0, 3); // the one legal add
        Assert.Throws<SessionRejectedException>(() => s.AddToBelt(1, 3)); // slot occupied
        Assert.Equal(2, s.Carried.Count); // nothing consumed by any rejection
    }

    // ---- belt removal frees the slot; the potion stays carried ----

    [Fact]
    public void RemoveFromBelt_FreesSlot_PotionStaysCarried_AndUsableAfter()
    {
        var s = Fresh();
        var potion = BuyOne(s, LifePotion);
        s.AddToBelt(s.Carried.IndexOf(potion), 1);

        Assert.Same(potion, s.RemoveFromBelt(1));
        Assert.Null(s.Belt[0]); // slot freed
        Assert.Single(s.Carried); // still carried

        s.Character.TakeDamage(20);
        s.AddToBelt(s.Carried.IndexOf(potion), 4); // re-slotted into another cell
        Assert.Equal(50, s.UsePotion(4).After); // 30 + 20 → 50 (clamps exactly at Max)
    }

    [Fact]
    public void SellLast_SoldPotion_FreesItsBeltSlot()
    {
        var s = Fresh();
        Assert.Equal(60, s.Buy(LifePotion));
        s.AddToBelt(s.Carried.Count - 1, 2);

        s.SellLast(); // the vendor door: the belt must not dangle on sold goods

        Assert.Null(s.Belt[1]);
        Assert.Empty(s.Carried);
        Assert.Throws<SessionRejectedException>(() => s.UsePotion(2)); // slot reads empty now
    }

    // ---- a potion pays ZERO XP (XP untouched — DoD assert) ----

    [Fact]
    public void UsePotion_GrantsZeroXp_LevelAndXpUntouched()
    {
        var s = Fresh();
        s.Character.GrantXp(100, s.Ladder); // some xp on the books, still level 1
        long xp0 = s.Character.Xp;
        int level0 = s.Character.Level;
        var potion = BuyOne(s, LifePotion);
        s.Character.TakeDamage(30);
        s.AddToBelt(s.Carried.IndexOf(potion), 1);

        s.UsePotion(1);

        Assert.Equal(xp0, s.Character.Xp);
        Assert.Equal(level0, s.Character.Level);
    }

    // ---- real monster damage: Attack pays one counter per surviving monster ----

    [Fact]
    public void Attack_SurvivingMonsterCounters_HeroLifeActuallyFalls()
    {
        var s = Fresh(42);
        Assert.Equal(s.Character.MaxLife, s.Character.Life); // full on arrival
        for (int round = 0; round < 6 && s.Character.Life == s.Character.MaxLife; round++)
            s.Attack("pokrzywnik", 4); // a fresh spawn per kill; counters from live ones
        Assert.True(s.Character.Life < s.Character.MaxLife,
            $"no counter damage across the strikes (life {s.Character.Life}/{s.Character.MaxLife})");
        Assert.True(s.Character.Life >= 0); // the pool floors at 0 — no death rules yet
    }

    // ---- the belt + pools ride the save (no dangling slots across a load) ----

    [Fact]
    public void BeltAndPools_SurviveSaveLoad_SlotPointsAtCarriedCopy_AndUsableAfter()
    {
        var s = Fresh();
        var potion = BuyOne(s, LifePotion);
        s.Character.TakeDamage(30); // life 20
        s.Character.Mana = 5;
        s.AddToBelt(s.Carried.IndexOf(potion), 1);

        var before = s.Dump();
        var path = Path.Combine(Path.GetTempPath(), $"belt-{Guid.NewGuid():N}.json");
        try
        {
            s.Save(path);
            var loaded = GameSession.Load(path, TestData.DataDir());
            Assert.Equal(before, loaded.Dump()); // byte-identical round trip (foundation contract)
            Assert.Equal(20, loaded.Character.Life);
            Assert.Equal(5, loaded.Character.Mana);
            Assert.Same(loaded.Carried[0], loaded.Belt[0]); // the slot points at the RESTORED copy

            var r = loaded.UsePotion(1); // usable straight off the save
            Assert.Equal(40, r.After); // 20 + 50×40/100
        }
        finally { File.Delete(path); }
    }

    // ---- belt geometry: four cells, all empty on a fresh session ----

    [Fact]
    public void Belt_HasFourSlots_AllEmptyOnFreshSession()
    {
        var s = Fresh();
        Assert.Equal(4, GameSession.BeltSlots);
        Assert.Equal(4, s.Belt.Length);
        Assert.All(s.Belt, slot => Assert.Null(slot));
    }
}
