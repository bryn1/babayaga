using System.Collections.Generic;
using System.Linq;
using BabaYaga.Sim;

/// <summary>
/// M5 RITUAL-CRAFT PANEL, the mapping core (MC 11970, LEDGER row 75 cube half;
/// the TownPanelLogic/StashPanelLogic precedent): the pure rule behind
/// CraftPanel. The rows are the ADMITTED recipes and nothing else — the engine's
/// admission law (CubeRecipeTable: a cross-table-invalid row SKIPS silently)
/// already decided what may stand before a player's eyes, so the panel reads
/// CubeRecipes.Recipes (the read-back the door answers to) and NOTHING else;
/// SkippedCount is engine-side bookkeeping and never paints (the honest empty
/// state stands in its place: zero rows paint ONE line, EmptyLine, never a fake
/// row). The CRAFT button maps to the exact CLI twin grammar line "craft
/// &lt;recipeId&gt;" (EngineDoorVerbs — the logged line IS the grammar, so a
/// clicked panel action replays through the CommandRunner; CraftPanelTests
/// replays it LIVE). The town gate is a pulled-key equality (inert outside
/// grodzisko — the ENGINE's own DEAD→town gates on CraftRecipe stay the
/// authority behind the widget, the stash/hire idiom, never re-implemented
/// here). Every refusal text is the engine's own words (the panel catch paints
/// e.Message, the TownPanel hint idiom); the two builders below pin the two
/// texts THIS file owns — the outside-town hint and the success hint — so the
/// panel paints and the leg reads with the SAME string (the label idiom).
/// PURE (no Godot types) — xunit-covered (tests/CraftPanelTests.cs).
/// </summary>
public static class CraftPanelLogic
{
    /// <summary>The panel's chrome header (the ONE literal — the panel paints
    /// it and the leg's row-read-back excludes it with the SAME string).</summary>
    public const string Header = "The Bone Cube";

    /// <summary>The honest empty state (zero admitted recipes — the shipped
    /// truth until the recipe-data card lands): exactly this ONE line, no
    /// rows. The wording is the brief's, pinned by test.</summary>
    public const string EmptyLine = "no inscriptions warm the cube yet";

    /// <summary>Outside-town key hint (Act's line, the "the town chest stands
    /// in grodzisko" twin — pinned by test, the leg never retypes it).</summary>
    public const string OutsideHint = "the bone cube stands in grodzisko";

    /// <summary>Town gate: the cube opens exactly ON the pulled town key
    /// (grodzisko) — the same key every town door pulls, never a literal.</summary>
    public static bool OpensFor(string areaKey, string townKey) => areaKey == townKey;

    /// <summary>The one CLI twin line a CRAFT click rides through
    /// CommandLog.Call — the EngineDoorVerbs grammar verbatim (replay law).</summary>
    public static string CraftLine(string recipeId) => $"craft {recipeId}";

    /// <summary>The ONE CRAFT button label (the leg hunts with this exact
    /// string — the DeathMenuLogic label idiom, no duplicated literal).</summary>
    public const string CraftButton = "CRAFT";

    /// <summary>The row texts, in table order (file order = paint order) — the
    /// ADMITTED rows only. Zero admitted yields zero texts: the caller paints
    /// EmptyLine (the empty-state law lives in the caller's one branch, tested
    /// against the shipped zero-admit data).</summary>
    public static List<string> RowLabels(CubeRecipeTable table, ItemBaseTable bases) =>
        table.Recipes.Select(r => RowLabel(r, bases)).ToList();

    /// <summary>One row's text: the recipe's own NAME plus its inputs as
    /// item NAMES × counts, every name pulled through the session's base table
    /// (a skipped ingredient id never reaches here — admission already refused
    /// it, so For() cannot throw on an admitted row).</summary>
    public static string RowLabel(CubeRecipeRow r, ItemBaseTable bases) =>
        $"{r.Name} — {string.Join(" + ", r.Inputs.Select(i => $"{i.Count} × {bases.For(i.ItemId).Name}"))}";

    /// <summary>The SUCCESS hint, painted after the door returned its report
    /// (a refusal never reaches it — the catch paints the engine's words
    /// instead). The out-name is PULLED from the base table: the plain
    /// fixed-mint output carries its base name verbatim (R2b), and a composed
    /// magic/rare name is the door's business, not this string's. Pinned by
    /// test; the leg compares the HUD line against THIS builder.</summary>
    public static string OutHint(CubeRecipeRow r, ItemBaseTable bases) =>
        $"the cube cools — {bases.For(r.OutputItemId).Name} lands in your pack";
}
