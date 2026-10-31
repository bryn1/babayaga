namespace BabaYaga.Sim;

/// <summary>A face-down gamble purchase: the customer sees the white base and pays
/// its normal-buy price; the quality tier is only revealed on purchase (P2 §4.7).</summary>
public sealed record GambleOffer(ItemBaseRow Base, int ItemLevel, long Price);

/// <summary>
/// Gambling is a drop with the monster taken out (P2 §4.7): no treasure class, no
/// no-drop roll, no Magic Find — only the character level drives the ilvl roll
///   ilvl = clamp(clvl − 5 + roll(0..9), 5, 99).
/// The tier is decided by the §4.1 quality roll at that ilvl with MF pinned to 0,
/// at the moment of purchase.
/// </summary>
public sealed class GambleStore
{
    private readonly ItemBaseTable _bases;
    private readonly UniqueSetTable? _uniques;
    private readonly AffixPool? _affixes;

    public GambleStore(ItemBaseTable bases, UniqueSetTable? uniques = null, AffixPool? affixes = null)
    {
        _bases = bases;
        _uniques = uniques;
        _affixes = affixes;
        if (_bases.Stock.Count == 0)
            throw new InvalidDataException("gamble stock needs at least one quality-rolling base");
    }

    /// <summary>The §4.7 ilvl roll, uniform inside the clamp, both bounds reachable.</summary>
    public static int IlvlFor(Rng rng, int charLevel) =>
        Math.Clamp(charLevel - 5 + rng.NextInRange(0, 9), 5, 99);

    /// <summary>Face-down offer: base, rolled ilvl, standard-vendor buy price.</summary>
    public GambleOffer Next(Rng rng, int charLevel)
    {
        var stock = _bases.Stock;
        var baseRow = stock[rng.NextInt(stock.Count)];
        int ilvl = IlvlFor(rng, charLevel);
        return new GambleOffer(baseRow, ilvl, Vendor.BuyPrice(baseRow, Vendor.Standard));
    }

    /// <summary>Buy one offer: rejects a short purse (nothing is rolled or revealed),
    /// otherwise rolls and reveals the item at the offer's ilvl with MF pinned to 0.
    /// A unique/set success whose pick table has no spawnable row degrades to magic.</summary>
    public Item? Buy(Rng rng, GambleOffer offer, Purse buyer)
    {
        if (!Vendor.TryBuy(buyer, offer.Price)) return null;
        var quality = QualityRoll.Roll(
            rng, offer.Base, offer.ItemLevel,
            magicFind: 0,
            uniqueCandidateExists: _uniques?.UniqueCandidate(offer.Base.Id, offer.ItemLevel) ?? false,
            setCandidateExists: _uniques?.SetCandidate(offer.Base.Id, offer.ItemLevel) ?? false);
        if (quality is ItemQuality.Unique or ItemQuality.Set)
        {
            var special = quality == ItemQuality.Unique
                ? _uniques!.BuildUnique(rng, offer.Base, offer.ItemLevel)
                : _uniques!.BuildSetPiece(rng, offer.Base, offer.ItemLevel);
            if (special is not null) return special;
            quality = ItemQuality.Magic; // degrades like the drop pipeline does
        }
        if (quality == ItemQuality.Magic && _affixes is not null)
            return AffixPool.BuildMagic(rng, offer.Base, offer.ItemLevel, _affixes);
        if (quality == ItemQuality.Rare && _affixes is not null
            && RareGen.TryBuild(rng, offer.Base, offer.ItemLevel, _affixes, out var rare))
            return rare!;
        var item = new Item(offer.Base, offer.ItemLevel) { Quality = quality };
        return item;
    }
}
