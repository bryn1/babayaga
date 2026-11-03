namespace BabaYaga.Sim;

/// <summary>
/// THE RITUAL CRAFT DOOR (spec §6.8, LEDGER row 48, MC 11969): the grodzisko bone
/// cube over the fire — ONE method takes the recipe id, reads the inputs from THE
/// CARRIED PACK and nothing else, mints the output riding the existing generation
/// math, and lands it in the pack. THE CUBE STANDS ONLY IN TOWN: DEAD first (the
/// row-64 RaiseIfDead idiom), then the SAME town concept every town door uses
/// (Areas.TownKey — the stash/hire gate idiom; D3's gate order). ORIGINAL DESIGN
/// fence: the oracle-era cube had a 4-slot input GRID; this clone declares the
/// carried pack as the source of inputs and the pack as the landing place — the
/// cube-container grid awaits the windowed UI card (LEDGER row 75 tail).
///
/// GATE ORDER (§3 D3, every refusal NAMED per D4): dead → town → recipe-known (a
/// row SKIPPED at admission is indistinguishable from never-existing — the same
/// wording) → the deterministic MATCH → room-for-output proven BEFORE anything
/// moves or dies → consume via RemoveFromCarried → land the output via TryCarry.
/// The MATCH LAW (§3 D2): for each recipe input row, IN SCHEMA ORDER, take carried
/// items whose Base.Id equals the row's itemId in ASCENDING PACK-INDEX ORDER (the
/// index into the Carried list), until the row's count is satisfied; a row already
/// taken never answers again. A shortage is the named refusal "found no match —
/// missing N × itemId" naming the FIRST unmet row (schema order); surplus copies
/// OUTSIDE the multiset are simply not taken — the pack is not the cube (the
/// fenced R1d extension to a future cube grid does NOT stand here, D2 pinned).
///
/// THE RNG LAW: the door rides the EXISTING DropRng exactly like the Gamble door
/// (GameSession.Trade.cs:43–44) — NO new stream; the drop/combat streams are
/// untouched and every prior anchor stays. Every gate (dead/town/known/match/
/// room) consumes ZERO draws: the recipe's lvl/plvl/ilvl read-back, the D2 match
/// and the room probe are all deterministic. Generation happens ONLY once the
/// command commits, and consumes EXACTLY the draws the generation path itself
/// consumes — the mirror-the-tail reading of the terminal pipeline lines
/// (ItemForge.Generate, TreasureClass.cs:228–231) with quality FORCED, no tier
/// roll (D5(2) verbatim: forced magic → AffixPool.BuildMagic; forced rare →
/// RareGen.TryBuild; the failed forced rare stamps Quality=Rare at the terminal
/// line, NOT plain — the row-16 witness; fixedMods then roll on the same stream).
///
/// The ilvl is D5(1)'s: ItemLevels.FromCube(row.Lvl, row.Plvl, clvl, row.Ilvl,
/// max-Ilvl of the consumed set) — fixed wins, else floor(plvl%×clvl) +
/// floor(ilvl%×input-ilvl), clamped 1..99, defaults −1/0/0 riding verbatim.
/// The save needs NO bump: the output rides existing Carried ItemSave rows, v7
/// unchanged (D7).
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The generation collaborator handle (DA-c2 note 3): CubeRecipeTable
    /// admission already has Bases; BuildMagic/TryBuild take the pool, so the door
    /// stores it like _flow/_gambler already do — assigned in the boot ctor.</summary>
    private readonly AffixPool _affixes;

    /// <summary>What one craft did — Consumed is the taken items in the TAKE ORDER
    /// (schema order, ascending pack index) with their ANCHOR cells; OutRow/OutCol
    /// the cell the output landed through (first-fit). The CLI twin echoes these.</summary>
    public sealed record CraftConsumed(string Base, string Name, int Row, int Col);

    public sealed record CraftReport(
        string RecipeId,
        IReadOnlyList<CraftConsumed> Consumed,
        string OutBase,
        string OutName,
        int OutRow,
        int OutCol,
        int Ilvl,
        IReadOnlyList<string> AffixIds);

    public CraftReport CraftRecipe(string recipeId)
    {
        RaiseIfDead(); // the row-64 gate speaks FIRST, like every sibling door
        if (CurrentArea != Areas.TownKey)
            throw new SessionRejectedException(
                $"the ritual craft stands in town — the bone cube is not in '{CurrentArea}'");
        var recipe = CubeRecipes.Find(recipeId)
            ?? throw new SessionRejectedException($"ritual recipe '{recipeId}' is unknown");

        // THE MATCH (D2): schema order, ascending pack index, never a re-take.
        // Zero draws — the whole gate is a pure state read (the RNG law).
        var taken = new List<Item>();
        var claimed = new HashSet<Item>(ReferenceEqualityComparer.Instance);
        foreach (var row in recipe.Inputs)
        {
            var have = 0;
            for (var i = 0; i < Carried.Count && have < row.Count; i++)
            {
                if (claimed.Contains(Carried[i]) || Carried[i].Base.Id != row.ItemId) continue;
                claimed.Add(Carried[i]);
                taken.Add(Carried[i]);
                have++;
            }
            if (have < row.Count)
                throw new SessionRejectedException(
                    $"recipe '{recipe.Id}' found no match — missing {row.Count - have} × {row.ItemId}");
        }

        // D5(1): the ilvl FIRST (the room probe needs the footprint and the mint
        // needs the ilvl) — max-Ilvl of the consumed set, ties never matter for the
        // max VALUE itself; the scan is ascending regardless, exactly as §3 states.
        var inputIlvl = taken.Max(i => i.ItemLevel);
        var ilvl = ItemLevels.FromCube(recipe.Lvl, recipe.Plvl, Character.Level, recipe.Ilvl, inputIlvl);
        var outBase = Bases.For(recipe.OutputItemId); // admitted ⇒ present (the For idiom)

        // ROOM FOR OUTPUT, proven BEFORE anything moves or dies (D4) — with the
        // pack AS IS, so the refusal stands even when the inputs would free a
        // cell (the conservative reading, §5 row 9); the footprint probe draws
        // nothing (the stash PackHasRoomFor idiom).
        var probe = new Item(outBase, ilvl);
        if (!PackHasRoomFor(probe))
            throw new SessionRejectedException($"the pack has no cell left for '{outBase.Id}'");

        // COMMIT — the ONLY point a die is rolled. Mirror-the-tail of the drop
        // pipeline's terminal lines (TreasureClass.cs:228–231) with quality FORCED
        // and no tier roll (D5(2)); a zero-pick forced magic keeps its Magic stamp
        // (BuildMagic stamps before picks, AffixPool.cs:156) and a failed forced
        // rare stamps Rare, never plain (row-16 witness). fixedMods next, same
        // stream (D5(3)); the ALWAYS-HITS family (chance 100) is the shipped
        // default; a chance < 100 gates with one percent roll before the value.
        var output = MintForced(DropRng, outBase, ilvl, recipe.OutputQuality);
        foreach (var m in recipe.FixedMods)
        {
            if (m.Chance < 100 && DropRng.NextPercent() >= m.Chance) continue; // the named gate
            output.Mods.Add(new ItemMod(m.Stat, DropRng.NextInRange(m.Min, m.Max)));
        }

        // CONSUME through the SHARED door (RemoveFromCarried — Grid/Carried/
        // CarriedSpots/belt/LastLooted all fixed up, the Stash/Take order), then
        // land the output through TryCarry and read its landing anchor back.
        var consumed = new List<CraftConsumed>();
        foreach (var item in taken)
        {
            var (r, c) = CarriedSpots[item];
            consumed.Add(new CraftConsumed(item.Base.Id, item.DisplayName, r, c));
            RemoveFromCarried(item);
        }
        if (!TryCarry(output))
            throw new InvalidOperationException(
                $"the crafted '{outBase.Id}' found no cell after the room was proven"); // (the probe makes this unreachable)
        var (or, oc) = CarriedSpots[output];
        return new CraftReport(recipe.Id, consumed, outBase.Id, output.DisplayName, or, oc, ilvl, output.AffixIds);
    }

    /// <summary>The D5(2) mirror of ItemForge.Generate's terminal lines with the
    /// quality FORCED (no QualityRoll call anywhere): forced magic → BuildMagic
    /// (pool picks may land ZERO affixes — the Magic stamp PRECEDES them); forced
    /// rare → RareGen.TryBuild, and its failure answers the verbatim terminal line
    /// {Quality=Rare} with empty affixes; an absent quality (Normal) mints the
    /// plain fixed item the R2b modeling names. Same three branches, same order,
    /// same collaborator calls — the drop path itself stays untouched.</summary>
    private Item MintForced(Rng rng, ItemBaseRow baseRow, int ilvl, ItemQuality quality)
    {
        if (quality == ItemQuality.Magic) return AffixPool.BuildMagic(rng, baseRow, ilvl, _affixes);
        if (quality == ItemQuality.Rare && RareGen.TryBuild(rng, baseRow, ilvl, _affixes, out var rare))
            return rare!;
        return new Item(baseRow, ilvl) { Quality = quality };
    }
}
