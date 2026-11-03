using System.Text.Json;
using System.Text.Json.Nodes;
using BabaYaga.Sim.Cli;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// THE RITUAL-CRAFT CONTENT (MC 11978, LEDGER row 48 CONTENT PAID): the five act-1
/// recipes replacing the skeleton-wave PLACEHOLDER rows are the SHIPPED truth — the
/// HEADLINE of this file is the flip: over the SHIPPED data dir the engine admits
/// 5/5 (Recipes.Count == 5, SkippedCount == 0) and the CRAFT PANEL now paints real
/// rows (the empty state lives only in fixtures from today). RED-FIRST: every row
/// below is RED on the placeholder base (the shipped file admits 0, skips 5 there).
///
/// The rows ride ONLY real surfaces: the shipped data dir through the real loaders
/// (TestData.DataDir() sessions and CommandRunner on the default dir), the real
/// CraftRecipe door + DropRng (no fixture rows, no synthetic dirs — this is the
/// content's own proof, the engine law stays pinned by RitualCraftTests), and the
/// shipped-file JSON reads (per-row _witness contract, the twin byte-law aside rides
/// SkeletonDataTests). Balance witnesses ride existing vendor costs: the surplus /
/// shortfall / determinism math reads 3×120 rings, 3×40 ambers off item-bases.json.
///
/// Refusal spot (real recipe): the D4 shortfall refuses BY NAME on the real id with
/// the streams untouched, and the D2 surplus law still NOT-TAKES the extra copy on
/// a real recipe. The full-pack room refusal stays RitualCraftTests' fixture-side
/// law: the 1000-gold start cannot fill the 4×10 pack with any admitted input
/// (cheapest 1×1 fill costs 2000 gold) — stated, not faked around.
/// </summary>
public sealed class RitualCraftContentTests
{
    private static string Data(string file) => Path.Combine(TestData.DataDir(), file);

    private static GameSession Town(ulong seed) => GameSession.New(seed, "koldun", TestData.DataDir());

    private static (int Exit, List<string> Lines) RunShipped(string body, ulong seed)
    {
        var path = Path.Combine(Path.GetTempPath(), $"craftc-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, body);
        try
        {
            var output = new StringWriter();
            int exit = CommandRunner.Run(path, seed, TestData.DataDir(), output);
            return (exit, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList());
        }
        finally { File.Delete(path); }
    }

    private static JsonNode Node(List<string> lines, string cmd) =>
        lines.Select(l => JsonNode.Parse(l)!).First(n => n["CMD"]?.GetValue<string>() == cmd)!;

    // ---- THE HEADLINE FLIP: the shipped dir admits 5/5, zero skips, panel paints ----

    [Fact]
    public void ShippedData_HeadlineFlip_FiveAdmittedZeroSkipped()
    {
        var s = Town(9); // the SHIPPED data dir — the file that skipped 5/5 skips 0 today
        Assert.Equal(5, s.CubeRecipes.Recipes.Count); // the five act-1 recipes stand admitted
        Assert.Equal(0, s.CubeRecipes.SkippedCount); // not one row crosses an unknown base
        Assert.NotNull(s.CubeRecipes.Find("smola_w_kamien")); // a real row simply EXISTS now
        Assert.NotNull(s.CubeRecipes.Find("trzy_kregi"));
        Assert.Null(s.CubeRecipes.Find("popiol_iskra")); // the placeholder rows are GONE, not skipped
    }

    [Fact]
    public void ShippedData_CraftPanelPaintsRealRows_TheEmptyStateIsFixtureOnly()
    {
        var s = Town(9);
        var rows = CraftPanelLogic.RowLabels(s.CubeRecipes, s.Bases);
        Assert.Equal(5, rows.Count); // one label per ADMITTED recipe — the panel has content
        Assert.Contains(rows, r => r.Contains("Raw Amber")); // names pull through s.Bases, real rows
    }

    // ---- every input/output id resolves through the REAL loader (admission check) ----

    [Fact]
    public void ShippedData_EveryRecipeId_ResolvesThroughTheRealLoader()
    {
        var s = Town(9);
        foreach (var r in s.CubeRecipes.Recipes)
        {
            Assert.NotNull(s.Bases.Find(r.OutputItemId)); // output.base row exists (the door's own For would throw otherwise)
            foreach (var i in r.Inputs)
                Assert.NotNull(s.Bases.Find(i.ItemId)); // and EVERY input row — $"{r.Id}:{i.ItemId}" readable below
        }
        // readable failure names: rebuild the sweep with ids in the message
        foreach (var r in s.CubeRecipes.Recipes)
            foreach (var id in r.Inputs.Select(i => i.ItemId).Append(r.OutputItemId))
                Assert.True(s.Bases.Find(id) is not null, $"{r.Id}: id '{id}' resolves through no item-bases row");
    }

    // ---- the shipped-file shape: the five ids, the _witness contract, no placeholders ----

    [Fact]
    public void ShippedFile_CarriesTheFiveRows_EachWithAWitness_AndNoPlaceholder()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Data("cube-recipes.json")));
        var rows = doc.RootElement.GetProperty("recipes").EnumerateArray().ToList();
        Assert.Equal(
            new[] { "smola_w_kamien", "kamien_na_kamien", "jantarowe_serce", "trzy_kregi", "smolista_szpula" },
            rows.Select(r => r.GetProperty("id").GetString()).ToArray()); // file order, the five content ids
        foreach (var r in rows)
        {
            var id = r.GetProperty("id").GetString()!;
            Assert.True(r.TryGetProperty("_witness", out var w) && w.GetString()!.Length > 40,
                $"{id}: every content row carries a _witness (the oracle line/field modeled, own words)");
            Assert.True(r.TryGetProperty("ilvlRule", out _) && r.TryGetProperty("flags", out _),
                $"{id}: the dead keys ride — the sealed key-set contract (SkeletonDataTests)");
        }
        Assert.DoesNotContain("PLACEHOLDER", File.ReadAllText(Data("cube-recipes.json"))); // the skeleton is REPLACED
    }

    // ---- determinism: the door crafts a REAL recipe, same seed, twice — byte-equal ----

    [Fact]
    public void RealRecipe_CliCraft_SameSeedTwoRuns_DumpsAndSaveBytes_ByteIdentical()
    {
        // trzy_kregi (L15-family): 3 × 120-gold rings inside the 1000-gold start; the
        // forced-magic mint draws the DropRng — the byte-equality is the RNG law's proof.
        var body = "new koldun\nbuy pierscien\nbuy pierscien\nbuy pierscien\ncraft trzy_kregi\ndump\nsave {0}\n";
        var saveA = Path.Combine(Path.GetTempPath(), $"craftc-{Guid.NewGuid():N}.a");
        var saveB = Path.Combine(Path.GetTempPath(), $"craftc-{Guid.NewGuid():N}.b");
        try
        {
            var (eA, a) = RunShipped(string.Format(body, saveA), 42);
            var (eB, b) = RunShipped(string.Format(body, saveB), 42);
            Assert.Equal(0, eA); Assert.Equal(0, eB); // the door ANSWERED the real id (no ERROR line)
            Assert.Equal(
                string.Join('\n', a.Where(l => !l.Contains("\"CMD\":\"save\""))), // the MC 11958 stated shape:
                string.Join('\n', b.Where(l => !l.Contains("\"CMD\":\"save\"")))); // stdout equal BUT the path echo
            Assert.Equal(File.ReadAllBytes(saveA), File.ReadAllBytes(saveB)); // SAVE BYTES cmp EMPTY
            Assert.Equal("trzy_kregi", Node(a, "craft")["RECIPE"]!.GetValue<string>());
            Assert.Equal("obereg", Node(a, "craft")["OUT_BASE"]!.GetValue<string>());
        }
        finally { File.Delete(saveA); File.Delete(saveB); }
    }

    // ---- refusal spot on a REAL recipe: shortfall BY NAME, nothing moved, no draw ----

    [Fact]
    public void RealRecipe_Shortfall_RefusedByName_NothingMoved_TheStreamDrewNothing()
    {
        var s = Town(42);
        s.Buy("jantar"); // one of the three smola_w_kamien needs
        var before = s.Dump(); // the dump carries all three stream words (the engine's law surface)
        var ex = Assert.Throws<SessionRejectedException>(() => s.CraftRecipe("smola_w_kamien"));
        Assert.Equal("recipe 'smola_w_kamien' found no match — missing 2 × jantar", ex.Message); // the D4 line on a REAL row
        Assert.Equal(before, s.Dump()); // ZERO items moved AND zero RNG consumed
        s.Buy("jantar"); s.Buy("jantar");
        var r = s.CraftRecipe("smola_w_kamien"); // the later craft lands EXACTLY where a
        var t = Town(42); t.Buy("jantar"); t.Buy("jantar"); t.Buy("jantar");
        var rt = t.CraftRecipe("smola_w_kamien"); // refusal-free twin's does — the stream is untouched
        Assert.Equal(rt.AffixIds, r.AffixIds);
        Assert.Equal((rt.OutRow, rt.OutCol), (r.OutRow, r.OutCol));
    }

    // ---- the D2 surplus law STILL holds on a real recipe: extra copy NOT taken ----

    [Fact]
    public void RealRecipe_Surplus_IsNotTaken_TheExtraStaysAnchoredWhereItStood()
    {
        var s = Town(9);
        for (int i = 0; i < 4; i++) s.Buy("jantar"); // three plus one surplus
        var r = s.CraftRecipe("smola_w_kamien");
        Assert.Equal(3, r.Consumed.Count); // exactly the multiset was taken (D2, the pack is not the cube)
        Assert.Equal(2, s.Carried.Count);  // the extra + the minted garnet
        var extra = Assert.Single(JsonNode.Parse(s.Dump())!["carried"]!.AsArray()
            .Where(i => i!["base"]!.GetValue<string>() == "jantar"));
        Assert.Equal(0, extra!["row"]!.GetValue<int>());  // the surplus copy never moved
        Assert.Equal(3, extra["col"]!.GetValue<int>());
    }
}
