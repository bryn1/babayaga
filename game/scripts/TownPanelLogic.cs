using BabaYaga.Sim;

/// <summary>
/// M5-S6 TOWN PANEL, the mapping core (MC 11940, plan-c2 S6 row; the
/// DeathMenuLogic precedent): the pure rule behind TownPanel.tscn. Every
/// button maps to ONE CLI twin verb line (plan-c2 §2/S3a: the log line IS the
/// grammar, so a clicked panel action replays through the CommandRunner); the
/// town gate is a pulled-key equality (the panel is inert outside grodzisko —
/// the ENGINE's own town gates on Hire/Revive/Dismiss stay the authority
/// behind it, and Buy has no area gate at all, so this widget gate is the
/// brief's TOWN-ONLY row, not a re-implementation); the belt-slot pick is a
/// presentation-side choice the Sim owns no answer to (the InventoryPanel
/// cell-re-derivation doctrine) — on a full belt it returns slot 1 so the
/// panel still calls the door and the ENGINE'S "already holds" refusal becomes
/// the HUD hint (rejections never silently swallowed, no gate logic here).
/// PURE (no Godot types) — xunit-covered (tests/TownPanelTests.cs).
/// </summary>
public static class TownPanelLogic
{
    /// Vesna's windowed stock (ORIGINAL DESIGN widget choice): the NAMES and
    /// prices are pulled through s.Bases + Vendor.BuyPrice, never repeated here
    /// (the StockRows test row proves every id resolves in the session table).
    public static readonly string[] StockBaseIds = ["soluga", "napitek-zycia", "napitek-manny"];

    /// The captain's live act-1 row (the only hireling the data gates open —
    /// pulled and pinned, the act gate itself stays the ENGINE'S).
    public const string CaptainRowId = "strzelczyni";

    /// Town gate: the panel opens exactly ON the pulled town key (grodzisko).
    public static bool OpensFor(string areaKey, string townKey) => areaKey == townKey;

    // ---- button label → one logged sim-door line (the CLI twin grammar) ----

    public static string BuyLine(string baseId) => $"buy {baseId}";
    public static string BeltAddLine(int carriedIndex, int slot) => $"belt_add {carriedIndex} {slot}";
    public static string HireLine(string id) => $"hire {id}";
    public static string ReviveLine(string id) => $"revive {id}";
    public const string DismissLine = "dismiss";

    // ---- one label builder per button — the panel paints and the leg hunts
    // with the SAME string (the DeathMenuLogic label idiom, no literals twice) ----

    public static string BuyLabel(string name, long price) => $"BUY {name} {price}g";
    public static string HireLabel(string name, long price) => $"HIRE {name} {price}g";
    public static string ReviveLabel(string name) => $"REVIVE {name}";
    public const string DismissButton = "DISMISS";

    /// The first free belt cell (1–4) — a presentation-side pick. A full belt
    /// returns 1: the panel calls AddToBelt anyway and the engine's named
    /// refusal surfaces as the hint (never a silent no-op choice here).
    public static int BeltSlotFor(Item?[] belt)
    {
        for (int i = 0; i < belt.Length; i++)
            if (belt[i] is null) return i + 1;
        return 1;
    }
}
