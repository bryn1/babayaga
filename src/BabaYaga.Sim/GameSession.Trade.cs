namespace BabaYaga.Sim;

/// <summary>
/// The trade half of the session, split out of the core file by concern
/// (audit MC 11907 §5 — the P3-scheduled split): the vendor buy door, the
/// sell-last door and the gambler's offer — the doors that move coin and
/// hand goods across the counter. Same contract as the core file: each
/// either fully applies or is rejected with nothing half-applied, and the
/// refund-on-full-pack reading (the coin returns) is the shared shape.
/// </summary>
public sealed partial class GameSession
{
    public long Buy(string baseId)
    {
        RaiseIfDead();
        var baseRow = Bases.Find(baseId) ?? throw new SessionRejectedException($"item base '{baseId}' unknown");
        long price = Vendor.BuyPrice(baseRow, Vendor.Standard);
        if (!Purse.TrySpend(price))
            throw new SessionRejectedException($"not enough gold: {Purse.Amount} below the price {price}");
        var item = new Item(baseRow, itemLevel: Character.Level);
        if (!TryCarry(item))
        {
            Purse.Receive(price); // nothing sold into a full pack — the coin returns
            throw new SessionRejectedException("the pack has no cell left");
        }
        return price;
    }

    public long SellLast()
    {
        RaiseIfDead();
        if (Carried.Count == 0) throw new SessionRejectedException("nothing carried to sell");
        var item = Carried[^1];
        long price = Vendor.SellPrice(item, EconomyBand);
        RemoveFromCarried(item); // the shared leaves-the-pack order (audit D-6)
        Purse.Receive(price);
        return price;
    }

    public (long Price, ItemQuality Quality) Gamble()
    {
        RaiseIfDead();
        var offer = _gambler.Next(DropRng, Character.Level);
        var item = _gambler.Buy(DropRng, offer, Purse)
            ?? throw new SessionRejectedException($"cannot afford the gamble: {offer.Price} gold");
        if (!TryCarry(item))
        {
            Purse.Receive(offer.Price); // refunded like the vendor path: no room, no sale
            throw new SessionRejectedException("the pack has no cell left");
        }
        return (offer.Price, item.Quality);
    }

}
