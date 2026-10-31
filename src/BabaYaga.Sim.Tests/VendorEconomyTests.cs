namespace BabaYaga.Sim.Tests;

/// <summary>
/// The §4.7–4.8 economy contract: vendor buy multipliers and caps, the sell formula
/// with ethereal and class-type quarters and the difficulty caps that nothing
/// raises, repair math, insufficient-gold rejection, gold-pile ×mul/256 ×(1+GF),
/// and the §4.7 gamble ilvl roll.
/// </summary>
public class VendorEconomyTests
{
    private static ItemBaseTable Bases() => ItemBaseTable.Load(Path.Combine(TestData.DataDir(), "item-bases.json"));

    // --- §4.8 buy ---

    [Fact]
    public void BuyPrice_BaseTimesMultipler_StackReduceAndCap()
    {
        var szpula = Bases().For("szpula"); // cost 90
        Assert.Equal(90, Vendor.BuyPrice(szpula, Vendor.Standard));                    // ×1024/1024
        Assert.Equal(95, Vendor.BuyPrice(szpula, Vendor.Coppersmith));                // 90×1088/1024 = 95
        Assert.Equal(84, Vendor.BuyPrice(szpula, Vendor.Hearthwright));               // 90×960/1024 = 84
        Assert.Equal(180, Vendor.BuyPrice(szpula, Vendor.FenMarket));                 // ×2048/1024
        Assert.Equal(450, Vendor.BuyPrice(szpula, Vendor.Standard, stack: 5));
        Assert.Equal(45, Vendor.BuyPrice(szpula, Vendor.Standard, reduceVendorPercent: 50));
        var capped = Vendor.FenMarket with { MaxBuyPrice = 150 };
        Assert.Equal(150, Vendor.BuyPrice(szpula, capped));                            // hard cap
        Assert.Equal(1, Vendor.BuyPrice(Bases().For("jantar"), Vendor.Hearthwright with { BuyMultiplier = 1 })); // ≥1
    }

    [Fact]
    public void Buy_RejectsInsufficientGold_NeverDebt()
    {
        var purse = new Purse(100);
        Assert.False(Vendor.TryBuy(purse, 120));
        Assert.Equal(100, purse.Amount); // unchanged on rejection
        Assert.True(Vendor.TryBuy(purse, 100));
        Assert.Equal(0, purse.Amount);
        Assert.Throws<ArgumentOutOfRangeException>(() => purse.Receive(-1));
    }

    // --- §4.8 sell ---

    [Fact]
    public void Sell_HalfOfBase_EtherealAndClassQuarters_SellIsOnBaseCostNotBuyPrice()
    {
        var table = Bases();
        Assert.Equal(45, Vendor.SellPrice(new Item(table.For("szpula"), 10), Difficulty.Dusk));         // 90/2
        var etherealSword = new Item(table.For("szpula"), 10) { Ethereal = true };
        Assert.Equal(11, Vendor.SellPrice(etherealSword, Difficulty.Dusk));                              // 45/4
        var kugla = new Item(table.For("kugla"), 10); // class-specific cost 180 → 90 → ÷4
        Assert.Equal(22, Vendor.SellPrice(kugla, Difficulty.Dusk));
        var etherealKugla = new Item(table.For("kugla"), 10) { Ethereal = true };
        Assert.Equal(5, Vendor.SellPrice(etherealKugla, Difficulty.Dusk));                               // 90/4/4
    }

    [Fact]
    public void Sell_CappedByActAtDusk_FlatByDifficulty_NothingRaisesTheCap()
    {
        var rich = Bases().For("zgunia") with { Cost = 200_000 }; // 100k raw sell price
        Assert.Equal(5_000, Vendor.SellPrice(new Item(rich, 10), Difficulty.Dusk, act: 1));
        Assert.Equal(15_000, Vendor.SellPrice(new Item(rich, 10), Difficulty.Dusk, act: 3));
        Assert.Equal(25_000, Vendor.SellPrice(new Item(rich, 10), Difficulty.Dusk, act: 5));
        Assert.Equal(30_000, Vendor.SellPrice(new Item(rich, 10), Difficulty.Midnight, act: 5));
        Assert.Equal(35_000, Vendor.SellPrice(new Item(rich, 10), Difficulty.WitchNight, act: 1)); // flat, act no.
    }

    [Fact]
    public void Repair_ScalesWithMissingDurability()
    {
        var watan = new Item(Bases().For("watan"), 10); // cost 80, durability 36
        Assert.Equal(5, Vendor.RepairCost(watan, missingDurability: 18));  // 80×18×128/(36×1024)
        Assert.Equal(10, Vendor.RepairCost(watan, 36));                     // full break
        Assert.Equal(0, Vendor.RepairCost(watan, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Vendor.RepairCost(watan, 37));
    }

    // --- §4.8 gold piles (mul/256, killer's GF, floored; no MF term anywhere) ---

    [Fact]
    public void GoldPile_TcMulOver256_TimesKillerGoldFind_Floored()
    {
        Assert.Equal(500, Gold.Pile(500, tcMultiplier: 256, killerGoldFindPercent: 0));
        Assert.Equal(250, Gold.Pile(500, 128, 0));
        Assert.Equal(585, Gold.Pile(500, 300, 0));       // 150000/256 floored
        Assert.Equal(1462, Gold.Pile(500, 300, 150));    // 585 × 250/100 floored
        Assert.Equal(0, Gold.Pile(10, 25, 0));           // 10×25/256 = 0 — floors, never rounds up
    }

    // --- §4.7 gambling ---

    [Fact]
    public void Gamble_Clvl30_IlvlStaysIn25To34_OverSeededRolls()
    {
        var store = new GambleStore(Bases(), Uniques(), Affixes());
        var rng = new Rng(41UL);
        var seen = new HashSet<int>();
        for (int i = 0; i < 400; i++)
        {
            var offer = store.Next(rng, charLevel: 30);
            Assert.InRange(offer.ItemLevel, 25, 34);
            seen.Add(offer.ItemLevel);
        }
        Assert.Contains(25, seen); // both clamp-band ends reachable: clvl−5+0 … clvl−5+9
        Assert.Contains(34, seen);
    }

    [Fact]
    public void Gamble_ClampsTo5AtDuskAnd99AtTheTop()
    {
        var rng = new Rng(43UL);
        for (int i = 0; i < 200; i++)
        {
            Assert.InRange(GambleStore.IlvlFor(rng, charLevel: 3), 5, 5 + 9); // −2..7 clamped up to 5
            Assert.InRange(GambleStore.IlvlFor(rng, charLevel: 99), 5, 99);   // 94..103 clamped at 99
        }
        Assert.Contains(99, Enumerable.Range(0, 200).Select(_ => GambleStore.IlvlFor(rng, 99)));
    }

    [Fact]
    public void Gamble_BuyRejectsShortPurse_AndRevealsAtIlvl_NoMagicFind()
    {
        var store = new GambleStore(Bases(), Uniques(), Affixes());
        var rng = new Rng(47UL);
        var offer = store.Next(rng, charLevel: 30);
        var broke = new Purse(offer.Price - 1);
        Assert.Null(store.Buy(rng, offer, broke));       // nothing revealed, purse untouched
        Assert.Equal(offer.Price - 1, broke.Amount);

        var buyer = new Purse(100_000);
        var revealed = store.Buy(rng, offer, buyer)!;
        Assert.Equal(100_000 - offer.Price, buyer.Amount);
        Assert.Equal(offer.ItemLevel, revealed.ItemLevel);
        Assert.InRange(revealed.ItemLevel, 25, 34);      // §4.7 holds through the reveal
        Assert.True(revealed.Quality <= ItemQuality.Unique);
        // MF is pinned to 0 in Buy: the same seed sequence with no MF is the definition
        // of the §4.7 "no Magic Find" rule (no MF reaches this call site at all).
    }

    private static UniqueSetTable Uniques() =>
        UniqueSetTable.Load(Path.Combine(TestData.DataDir(), "uniques.json"));

    private static AffixPool Affixes() => AffixPool.Load(Path.Combine(TestData.DataDir(), "affixes.json"));
}
