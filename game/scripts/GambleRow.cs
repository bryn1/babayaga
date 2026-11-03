using BabaYaga.Sim;
using Godot;

/// <summary>
/// MC 11987 THE GAMBLE ROW — the ONE row TownPanel gains for the sim's
/// face-down store (CraftPanel's Row/Button twins are the layout idiom; this
/// file exists as its own concern because the MC 11959 UI LAW binds every
/// NEW node: an explicit MouseFilter assignment, interactive → Stop, readout
/// → Ignore, all named through MouseFilterLaw/MouseFilterFor — the one
/// Godot-side translation of the pure HUDCORE table, the MC 11959 idiom).
/// The sealed HudMouseFilterTests overlay scan pins TownPanel.cs itself
/// assignment-free, so the row carries its OWN assignments HERE: the row
/// box and the BUY button STOP (a click on an open panel never falls
/// through to the world), the offer label IGNORES (read-out — its row's
/// Stop keeps the click inside the panel; "town.gamble-offer" is named into
/// HudReadout.MouseFilterFor's read-out set, the two Stop names ride the
/// fail-safe default, xunit-pinned by tests/GamblePanelTests.cs).
/// The row paints exactly ONE thing the sim owns: GamblePanelLogic.
/// OfferLabel over a display-only projected GambleStore draw (fence:
/// GamblePanelLogic's header) — and ONE door: the click runs the caller's
/// logged action, zero session state read into it.
/// </summary>
public static class GambleRow
{
    /// <summary>One row: the offer's text (engine-composed) + the BUY button
    /// riding the caller's door action (TownPanel.Gamble — the CommandLog
    /// line). Every node names itself into the click law.</summary>
    public static Control Build(GambleOffer offer, System.Action onBuy)
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterLaw.For("town.gamble-row") };
        row.AddChild(new Label
        {
            Text = GamblePanelLogic.OfferLabel(offer),
            MouseFilter = MouseFilterLaw.For("town.gamble-offer"),
        });
        var buy = new Button
        {
            Text = GamblePanelLogic.BuyButton,
            MouseFilter = MouseFilterLaw.For("town.gamble-buy"),
        };
        buy.Pressed += onBuy;
        row.AddChild(buy);
        return row;
    }
}
