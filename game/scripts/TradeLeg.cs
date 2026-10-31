using BabaYaga.Sim;
using Godot;

/// <summary>
/// The M4-S2 vendor trade beat — run ONCE by Boot's headless branch after the
/// journey leg, through the §2 bridge surface ONLY (Buy / SellLast / waypoint
/// verbs; no Sim internal touched, no write besides the two public trade verbs).
/// Chain: ride the finished journey (hero stands in mchy) -> the pinned WAYPOINT
/// ORDER CONTRACT to the town (light the DESTINATION stone BEFORE TravelTo, never
/// after-travel) -> Act.Rebuild (the ONE rebuild entry, shared with the door path)
/// -> Buy(a real data/ item key) -> the item is PRESENT in session.Carried and
/// gold moved by exactly the price Buy returned -> SellLast() -> the count is back
/// and gold moved by exactly the price SellLast returned. Every value printed is
/// READ FROM THE SESSION; the inventory counts read the InventoryPanel's own
/// read-only view. TRADE buy ok / TRADE sell ok print only while their assertion
/// holds; TRADE_OK prints only when ALL sub-assertions held. A rejected verb is a
/// PushError + return — never a crash (the HeroController hint idiom).
/// </summary>
public static class TradeLeg
{
    private const string ItemKey = "szpula"; // a real item-bases.json row (cost 90)

    public static void Run(GameSession s, Act act)
    {
        var town = s.Areas.TownKey;

        // ---- the WAYPOINT ORDER CONTRACT: light the DESTINATION stone BEFORE travel ----
        GD.Print($"WAYPOINT trade area={s.CurrentArea} lit={town}={s.Waypoints.IsDiscovered(town)}");
        if (!s.Waypoints.IsDiscovered(town)) s.DiscoverWaypoint(town);
        s.TravelTo(town);
        GD.Print($"WAYPOINT done area={s.CurrentArea} lit={town}={s.Waypoints.IsDiscovered(town)}");
        act.Rebuild(town); // the same rebuild entry the hero's stone travel uses
        if (s.CurrentArea != town)
        {
            GD.PushError("trade leg: travel did not land in the town — no vendor, no trade");
            return;
        }

        // ---- buy at the vendor: the pack gains exactly the bought item, gold drops by price ----
        int before = InventoryPanel.ViewCount(s);
        GD.Print($"INV_COUNT n={before}");
        long gold0 = s.Gold;
        long price;
        try { price = s.Buy(ItemKey); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"trade leg: Buy rejected — {e.Message}");
            return;
        }
        int afterBuy = InventoryPanel.ViewCount(s);
        GD.Print($"INV_COUNT n={afterBuy}");
        if (afterBuy != before + 1 || s.Carried[^1].Base.Id != ItemKey || s.Gold != gold0 - price)
        {
            GD.PushError($"trade leg: buy state wrong count={afterBuy} was={before}" +
                $" item={s.Carried[^1].Base.Id} gold={s.Gold} was={gold0} price={price}");
            return;
        }
        GD.Print($"TRADE buy ok item={ItemKey} gold={s.Gold}");

        // ---- sell it back: the pack count returns, gold rises by the sell price ----
        long gold1 = s.Gold;
        long sold;
        try { sold = s.SellLast(); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"trade leg: SellLast rejected — {e.Message}");
            return;
        }
        int afterSell = InventoryPanel.ViewCount(s);
        GD.Print($"INV_COUNT n={afterSell}");
        if (afterSell != before || s.Gold != gold1 + sold)
        {
            GD.PushError($"trade leg: sell state wrong count={afterSell} was={before}" +
                $" gold={s.Gold} was={gold1} sold={sold}");
            return;
        }
        GD.Print($"TRADE sell ok gold={s.Gold}");
        GD.Print("TRADE_OK");
    }
}
