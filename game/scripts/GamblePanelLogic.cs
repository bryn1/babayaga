using BabaYaga.Sim;

/// <summary>
/// M5 TOWN-PANEL GAMBLE ROW, the mapping core (MC 11987; the
/// TownPanelLogic/CraftPanelLogic precedent): the pure rule behind the ONE
/// GAMBLE row TownPanel gains — the sim's face-down gambling store
/// (GameSession.Gamble, P2 §4.7) made player-facing. THE SIM DECIDES
/// EVERYTHING: the one BUY click rides the session verb through
/// CommandLog.Call (GambleLine — the CLI grammar "gamble" verbatim,
/// ScriptedSession's door, so a clicked gamble REPLAYS through the
/// CommandRunner, the replay law); every refusal paints the ENGINE's own
/// words (this file owns NO refusal copy — the catch in TownPanel.cs hands
/// e.Message to the hint, the TownPanel idiom); the panel computes NO
/// prices: the label composes only the engine's own GambleOffer (base NAME
/// pulled from the row, price = Vendor.BuyPrice via GambleStore.Next).
/// THE OFFER PROJECTION IS ORIGINAL DESIGN, FENCED HERE (the LEDGER
/// row-66/48 balance-fence idiom): THE SESSION EXPOSES NO PENDING OFFER —
/// GameSession.Gamble rolls the offer off the session's internal DropRng AT
/// THE DOOR (GameSession.Trade.cs), and src/** stays READ-ONLY under this
/// card, so the row's NEXT-offer display cannot be a session read.
/// ProjectOffer is therefore a panel-side DISPLAY-ONLY projection of the
/// Next() DRAW SHAPE: the engine's own GambleStore.Next (base pick + ilvl
/// roll + Standard buy price, verbatim) on a PANEL-OWNED display stream
/// seeded through DisplaySeedOf(s.Seed). The fence is the buy: the door
/// line calls s.Gamble() with ZERO arguments — no projected offer, price
/// or stream ever reaches the sim (the panel scans pin it: ProjectOffer on
/// paint lines only, the door zero-arg inside a CommandLog.Call), and the
/// session's truth lands through the sim's own answers — the gold header,
/// the pack tail the leg reads, the refusal's named price. After a
/// successful buy the row advances to the NEXT projected draw; the display
/// stream is seeded ONCE per session seed so same seed → same label
/// sequence (xunit-pinned determinism), and a refusal never advances it
/// (the row still shows an offer — the shop keeps its line out).
/// PURE (no Godot types) — xunit-covered (tests/GamblePanelTests.cs).
/// </summary>
public static class GamblePanelLogic
{
    /// <summary>The ONE CLI twin line a GAMBLE BUY click rides through
    /// CommandLog.Call — ScriptedSession's grammar verbatim (replay law;
    /// the bare verb, no arguments — the sim rolls its own offer).</summary>
    public const string GambleLine = "gamble";

    /// <summary>The ONE button label (the leg hunts this exact string — the
    /// DeathMenuLogic label idiom, no duplicated literal; unique among the
    /// panel's buttons, which carry a name and a price).</summary>
    public const string BuyButton = "BUY";

    /// <summary>The display stream's salt (pinned by test): the panel draws
    /// its DISPLAY-ONLY offers from s.Seed ^ salt — a panel-owned stream,
    /// never the session's DropRng (internal, unreachable by design), and
    /// never a bare second copy of the session seed.</summary>
    public const ulong DisplaySalt = 0x934B37C1D2A4F8E6UL;

    /// <summary>The panel's display-stream seed for a session seed (the
    /// fence's derivation — original design, display-side only).</summary>
    public static ulong DisplaySeedOf(ulong sessionSeed) => sessionSeed ^ DisplaySalt;

    /// <summary>The row's text, pulled from the drawn offer only: the base's
    /// NAME (the engine's table) and its price (Vendor.BuyPrice, rolled by
    /// GambleStore.Next itself) — the panel adds no number of its own.</summary>
    public static string OfferLabel(GambleOffer offer) =>
        $"GAMBLE — {offer.Base.Name}, {offer.Price}g";

    /// <summary>The display-only projection of a next-offer DRAW (fenced in
    /// the header): the engine's own GambleStore.Next on the caller's
    /// display Rng at the hero level. Display, never authority — no caller
    /// may hand the result to a session door (the TownPanel scans pin it).</summary>
    public static GambleOffer ProjectOffer(ItemBaseTable bases, Rng displayRng, int charLevel) =>
        new GambleStore(bases).Next(displayRng, charLevel);
}
