namespace BabaYaga.Sim;

/// <summary>One vendor's terms: the buy multiplier over the 1024 base (P2 §4.8
/// sell_mult rows: 1024 standard · 1088 · 960 · 2048) and an optional hard cap on
/// any single purchase price.</summary>
public sealed record VendorProfile(string Name, int BuyMultiplier, long MaxBuyPrice = long.MaxValue);

/// <summary>
/// Vendor trade math (P2 §4.8), all prices floored integer math:
///   buy    = base_cost × mult/1024 × quest_sell/1024 × stack, then −reduce_vendor%, ≥1, ≤ cap
///   sell   = base_cost × 512/1024 (ethereal ÷4, class-specific row ÷4 again), capped
///            by act/difficulty — 5k…25k at Dusk, 30k at Midnight, 35k at Witch-Night,
///            and nothing raises the cap
///   repair = base_cost × missing/max_durability × 128/1024
/// Insufficient gold is rejected by the purse, never a debt.
/// </summary>
public static class Vendor
{
    /// <summary>Named original-world vendors carrying the §4.8 multiplier rows.</summary>
    public static VendorProfile Standard { get; } = new("Vesna the Pedlar", 1024);
    public static VendorProfile Coppersmith { get; } = new("Zhosta the Coppersmith", 1088);
    public static VendorProfile Hearthwright { get; } = new("Mila the Hearthwright", 960);
    public static VendorProfile FenMarket { get; } = new("Yaroslav of the Fen Market", 2048);

    public static long BuyPrice(
        ItemBaseRow baseRow,
        VendorProfile vendor,
        int stack = 1,
        int questSellMultiplier = 1024,
        int reduceVendorPercent = 0)
    {
        if (stack < 1) throw new ArgumentOutOfRangeException(nameof(stack));
        var price = (long)baseRow.Cost * vendor.BuyMultiplier / 1024 * questSellMultiplier / 1024 * stack;
        price = price * (100 - Math.Clamp(reduceVendorPercent, 0, 99)) / 100;
        return Math.Clamp(Math.Min(price, vendor.MaxBuyPrice), 1, long.MaxValue);
    }

    /// <summary>Sell-price ceiling by difficulty/act (P2 §4.8: 5k–25k by act at
    /// Dusk, flat 30k Midnight, 35k Witch-Night; nothing raises it).</summary>
    public static long SellCap(Difficulty difficulty, int act = 1) => difficulty switch
    {
        Difficulty.Dusk => 5_000L * Math.Clamp(act, 1, 5),
        Difficulty.Midnight => 30_000L,
        Difficulty.WitchNight => 35_000L,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
    };

    public static long SellPrice(Item item, Difficulty difficulty, int act = 1)
    {
        var price = (long)item.Base.Cost * 512 / 1024;
        if (item.Ethereal) price /= 4;
        if (item.Base.ClassSpecific) price /= 4; // class-type rows take the quarter a second time
        return Math.Min(Math.Max(price, 1), SellCap(difficulty, act));
    }

    /// <summary>Repair price for missing durability (P2 §4.8; charge/stack costs are
    /// a later system — nothing charges to repair yet).</summary>
    public static long RepairCost(Item item, int missingDurability)
    {
        if (missingDurability < 0 || missingDurability > item.MaxDurability)
            throw new ArgumentOutOfRangeException(nameof(missingDurability));
        if (item.MaxDurability < 1 || missingDurability == 0) return 0;
        return (long)item.Base.Cost * missingDurability * 128 / ((long)item.MaxDurability * 1024);
    }

    /// <summary>Trade gate: the buyer's purse must cover the price (DoD f).</summary>
    public static bool TryBuy(Purse buyer, long price) => buyer.TrySpend(price);
}
