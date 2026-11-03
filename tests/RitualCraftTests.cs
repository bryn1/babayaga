using System.Text.Json;
using System.Text.Json.Nodes;
using BabaYaga.Sim.Cli;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// THE RITUAL CRAFT ENGINE (MC 11969, spec T0b §6.8 — LEDGER row 48): the cube-recipe
/// table loader (CubeRecipeTable.Load, silent-admission law), the CraftRecipe door and
/// the CLI twin verb "craft &lt;recipeId&gt;". RED-FIRST: every row below is RED on the
/// base tree (CS0117 — GameSession.CraftRecipe / CubeRecipeTable do not exist there).
///
/// ADMISSION LAW witnesses (§2 cycle-2; MC 11979 CURRENT TRUTH — the five PLACEHOLDER
/// rows were replaced by five REAL recipes): the shipped data/cube-recipes.json carries
/// FIVE REAL RECIPES — the loader ADMITS 5 with SKIPPED COUNT 0 read back via Recipes/
/// SkippedCount, prints NOTHING (the anchor story md5 holds — the loader runs at every
/// boot, so a print would move it; the loader-live anchor leg re-measures it), and the
/// door answers a retired PLACEHOLDER id with the unknown-id refusal VERBATIM. Schema-
/// invalid rows THROW InvalidDataException (the HirelingTable/WaypointGraph idiom);
/// cross-table unknown ids SKIP silently.
///
/// DOOR witnesses (§3): gate order DEAD → town (the stash/hire Areas.TownKey idiom,
/// ORIGINAL DESIGN fenced) → recipe-known → deterministic pack-index-order multiset
/// match → surplus copies simply NOT TAKEN (D2's reading — the pack is not the cube;
/// the shortfall is the NAMED refusal, "missing N × id") → room-for-output proven
/// BEFORE consumption and before any draw (a refusal leaves the pack byte-equal and
/// the three RNG stream words untouched — the dump carries them). Generation mirrors
/// the drop pipeline's terminal lines with quality FORCED (TreasureClass.cs:228–231):
/// witnesses 15 (zero-affix forced-MAGIC stays Magic — BuildMagic stamps before picks)
/// and 16 (failed forced-RARE at lvl 5 &lt; IlvlGate 27 stamps Quality=Rare, NOT plain).
/// fixedMods/lvl/plvl/ilvl ride ItemLevels.FromCube (row 11 arithmetic + clamps).
/// NO save bump: v7 — the output rides existing Carried ItemSave rows.
///
/// SYNTHETIC recipes (§2(iii)) ride the RangedDataDir convention — a GUID temp copy of
/// the REAL data dir with cube-recipes.json patched through JsonNode (and affixes.json
/// emptied ONLY for the row-15 zero-pool witness), never a committed fixture dir, never
/// a data/** edit. The shipped cube file brings its five REAL recipes into every
/// fixture, so synthetic admits load alongside them (6 = five shipped + one patched).
/// CLI rows use CommandRunner.Run(path, seed,
/// dir, output) — the CliTwinVerbsTests idiom; the dispatch layer rolls nothing.
/// </summary>
public sealed class RitualCraftTests
{
    // ---- the §2(iii) fixture convention: temp copy of the real data dir, patched ----

    private sealed record SynthRecipe(
        string Id,
        (string ItemId, int Count)[] Inputs,
        string OutBase,
        string? Quality = "magic", // null = the key is absent (the plain fixed-mint family)
        int? Lvl = null, int? Plvl = null, int? Ilvl = null,
        (string Stat, int Min, int Max, int Chance)? FixedMod = null);

    private static string CraftDataDir(params SynthRecipe[] recipes) => CraftDataDirCore(false, recipes);
    private static string CraftDataDirNoAffixes(params SynthRecipe[] recipes) => CraftDataDirCore(true, recipes);

    private static string CraftDataDirCore(bool emptyAffixes, SynthRecipe[] recipes)
    {
        var src = TestData.DataDir();
        var dir = Path.Combine(Path.GetTempPath(), $"craft-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(src, "*.json"))
            File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
        var path = Path.Combine(dir, "cube-recipes.json");
        var doc = JsonNode.Parse(File.ReadAllText(path))!;
        foreach (var r in recipes) ((JsonArray)doc["recipes"]!).Add(Row(r));
        File.WriteAllText(path, doc.ToJsonString());
        if (emptyAffixes) // the row-15 witness ONLY: the eligible pool yields zero picks
        {
            var ap = Path.Combine(dir, "affixes.json");
            var a = JsonNode.Parse(File.ReadAllText(ap))!;
            a["affixes"] = new JsonArray();
            File.WriteAllText(ap, a.ToJsonString());
        }
        return dir;
    }

    private static JsonObject Row(SynthRecipe r)
    {
        var o = new JsonObject
        {
            ["id"] = r.Id,
            ["name"] = r.Id + " (synthetic witness)",
            ["inputs"] = new JsonArray(
                r.Inputs.Select(i => (JsonNode)new JsonObject { ["itemId"] = i.ItemId, ["count"] = i.Count }).ToArray()),
            ["output"] = new JsonObject { ["itemId"] = r.OutBase },
        };
        if (r.Quality is not null) o["output"]!["quality"] = r.Quality;
        if (r.FixedMod is { } m)
            o["output"]!["fixedMods"] = new JsonArray
            {
                new JsonObject { ["stat"] = m.Stat, ["min"] = m.Min, ["max"] = m.Max, ["chance"] = m.Chance },
            };
        if (r.Lvl is { } lvl) o["output"]!["lvl"] = lvl;
        if (r.Plvl is { } plvl) o["output"]!["plvl"] = plvl;
        if (r.Ilvl is { } ilvl) o["output"]!["ilvl"] = ilvl;
        return o;
    }

    private static GameSession Town(ulong seed, string? dir = null) =>
        GameSession.New(seed, "koldun", dir ?? TestData.DataDir());

    private static void BuyJantars(GameSession s, int n)
    {
        for (int i = 0; i < n; i++) s.Buy("jantar"); // 40 gold each, 1×1 cells — the pack first-fits (0,0),(0,1),(0,2)…
    }

    private static (int Exit, List<string> Lines) RunCraftScript(string body, string dataDir, ulong seed)
    {
        var path = Path.Combine(Path.GetTempPath(), $"craft-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, body);
        try
        {
            var output = new StringWriter();
            int exit = CommandRunner.Run(path, seed, dataDir, output);
            return (exit, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList());
        }
        finally { File.Delete(path); }
    }

    private static JsonNode Node(List<string> lines, string cmd) =>
        lines.Select(l => JsonNode.Parse(l)!).First(n => n["CMD"]?.GetValue<string>() == cmd)!;

    // ---- §5 row 17 + §2(ii): the SHIPPED file admits 5, skips 0, refuses by name ----

    [Fact]
    public void Loader_ShippedFile_AdmitsFiveRealRecipes_ZeroSkips_AndCraftRefusesAGonePlaceholderById()
    {
        var s = Town(9); // the shipped data dir — every session boots over these five REAL recipes
        Assert.Equal(5, s.CubeRecipes.Recipes.Count); // read-back, never a print
        Assert.Equal(0, s.CubeRecipes.SkippedCount);
        var ex = Assert.Throws<SessionRejectedException>(() => s.CraftRecipe("popiol_iskra"));
        Assert.Equal("ritual recipe 'popiol_iskra' is unknown", ex.Message); // the D4 line, verbatim
        Assert.Null(s.CubeRecipes.Find("stara_kosc")); // a retired PLACEHOLDER row simply does not exist
    }

    [Fact]
    public void Loader_SyntheticRow_Admitted_AndCraftable_AlongsideTheRealFive()
    {
        var dir = CraftDataDir(new SynthRecipe("r_ember", [("jantar", 3)], "obereg"));
        try
        {
            var s = Town(9, dir);
            Assert.Equal(6, s.CubeRecipes.Recipes.Count); // the synthetic admit rides WITH the five REAL shipped recipes
            Assert.Equal(0, s.CubeRecipes.SkippedCount);
            BuyJantars(s, 3);
            var r = s.CraftRecipe("r_ember");
            Assert.Equal("r_ember", r.RecipeId);
            Assert.Equal(
                new[] { "jantar", "jantar", "jantar" },
                r.Consumed.Select(c => c.Base).ToArray()); // schema order, ascending pack index
            Assert.Equal(new[] { (0, 0), (0, 1), (0, 2) },
                r.Consumed.Select(c => (c.Row, c.Col)).ToArray()); // the ANCHOR cells
            Assert.Equal("obereg", r.OutBase);
            Assert.Single(s.Carried);
            Assert.Equal("obereg", s.Carried[^1].Base.Id); // the output landed in the pack
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- §2(ii) + DA-c2 note 5: the schema-invalid THROW path (temp fixture only) ----

    [Theory]
    [InlineData(1)] // input count below 1
    [InlineData(2)] // quality outside magic/rare
    [InlineData(3)] // fixedMod chance above 100
    [InlineData(4)] // fixedMod min above max
    public void Loader_SchemaInvalidRow_ThrowsInvalidDataException_AtBoot(int which)
    {
        var dir = which switch
        {
            1 => CraftDataDir(new SynthRecipe("r_bad", [("jantar", 0)], "pierscien")),
            2 => CraftDataDir(new SynthRecipe("r_bad", [("jantar", 1)], "pierscien", Quality: "superior")),
            3 => CraftDataDir(new SynthRecipe("r_bad", [("jantar", 1)], "pierscien", FixedMod: ("life", 5, 10, 101))),
            _ => CraftDataDir(new SynthRecipe("r_bad", [("jantar", 1)], "pierscien", FixedMod: ("life", 10, 5, 100))),
        };
        try { Assert.Throws<InvalidDataException>(() => GameSession.New(9, "koldun", dir)); }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Loader_DuplicateAdmittedIds_ThrowsInvalidDataException()
    {
        var r = new SynthRecipe("r_dup", [("jantar", 1)], "pierscien");
        var dir = CraftDataDir(r, r); // both rows are cross-table valid — the dup is among ADMITTED
        try { Assert.Throws<InvalidDataException>(() => GameSession.New(9, "koldun", dir)); }
        finally { Directory.Delete(dir, true); }
    }

    // ---- §3 D3 gate trio: DEAD → town → known, each refusal BY NAME ----

    [Fact]
    public void Craft_Dead_RefusesWithTheDeathWords_BeforeEveryOtherGate()
    {
        var s = Town(9);
        BuyJantars(s, 1);
        s.Attack("pokrzywnik", 1024); // the scripted death (the CliTwinVerbsTests shape)
        Assert.True(s.IsDead);
        var ex = Assert.Throws<SessionRejectedException>(() => s.CraftRecipe("r_ember"));
        Assert.Equal("you are dead", ex.Message); // DEAD speaks FIRST — even over the unknown id
    }

    [Fact]
    public void Craft_OutsideTown_RefusesNamingTheArea_AndTheSameDoorSucceedsBackHome()
    {
        var dir = CraftDataDir(new SynthRecipe("r_ember", [("jantar", 3)], "obereg"));
        try
        {
            var s = Town(9, dir);
            BuyJantars(s, 3);
            s.TravelTo("pokrzywlasy"); // a REAL walk out of grodzisko (the WALK half, lit town stone)
            var before = s.Dump();
            var ex = Assert.Throws<SessionRejectedException>(() => s.CraftRecipe("r_ember"));
            Assert.Equal("the ritual craft stands in town — the bone cube is not in 'pokrzywlasy'",
                ex.Message); // the stash wording, mirrored
            Assert.Equal(before, s.Dump()); // nothing consumed, nothing moved, no draw
            s.TravelTo("grodzisko"); // the same road home
            var r = s.CraftRecipe("r_ember"); // the same door in town succeeds
            Assert.Equal("obereg", r.OutBase);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- §5 rows 2–3: surplus NOT taken (D2), shortfall refused BY NAME ----

    [Fact]
    public void Craft_SurplusCarried_IsNotTaken_TheExtraStaysAnchoredWhereItStood()
    {
        var dir = CraftDataDir(new SynthRecipe("r_ember", [("jantar", 3)], "obereg"));
        try
        {
            var s = Town(9, dir);
            BuyJantars(s, 4); // one copy surplus — the pack is NOT the cube (D2, fenced)
            var r = s.CraftRecipe("r_ember");
            Assert.Equal(3, r.Consumed.Count); // exactly the multiset was taken
            Assert.Equal(2, s.Carried.Count);  // the extra + the output
            var carried = JsonNode.Parse(s.Dump())!["carried"]!.AsArray();
            var extra = Assert.Single(carried.Where(i => i!["base"]!.GetValue<string>() == "jantar"));
            Assert.Equal(0, extra!["row"]!.GetValue<int>());  // the surplus copy never moved
            Assert.Equal(3, extra["col"]!.GetValue<int>());
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Craft_Shortfall_RefusedByName_NothingMoved_TheStreamDrewNothing()
    {
        var dir = CraftDataDir(new SynthRecipe("r_ember", [("jantar", 3)], "obereg"));
        try
        {
            var s = Town(42, dir);
            BuyJantars(s, 1); // two short of the three
            var before = s.Dump(); // the dump carries all three stream words (DA P1-1)
            var ex = Assert.Throws<SessionRejectedException>(() => s.CraftRecipe("r_ember"));
            Assert.Equal("recipe 'r_ember' found no match — missing 2 × jantar", ex.Message);
            Assert.Equal(before, s.Dump()); // ZERO items moved AND zero RNG consumed
            BuyJantars(s, 2);              // the later craft lands EXACTLY where a
            var r = s.CraftRecipe("r_ember"); // refusal-free twin's does — the stream is untouched
            var t = Town(42, dir);
            BuyJantars(t, 3);
            var rt = t.CraftRecipe("r_ember");
            Assert.Equal(rt.AffixIds, r.AffixIds);
            Assert.Equal((rt.OutRow, rt.OutCol), (r.OutRow, r.OutCol));
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- §5 row 6: the room gate BEFORE consumption — never a half-apply ----

    [Fact]
    public void Craft_FullPack_RefusesBeforeConsume_TheInputsStandIntact()
    {
        var dir = CraftDataDir(new SynthRecipe("r_staff", [("postak", 2)], "postak"));
        try
        {
            var s = Town(9, dir);
            for (int i = 0; i < 5; i++) s.Buy("postak"); // five 2×4 bases fill the 4×10 pack
            var before = s.Dump();
            var ex = Assert.Throws<SessionRejectedException>(() => s.CraftRecipe("r_staff"));
            Assert.Equal("the pack has no cell left for 'postak'", ex.Message); // names the OUTPUT base
            Assert.Equal(before, s.Dump()); // read-back equality: nothing half-applied
            Assert.Equal(5, s.Carried.Count); // conservative reading pinned: the inputs WOULD have
        }                                             // freed a cell — the refusal stands (D4, row 9)
        finally { Directory.Delete(dir, true); }
    }

    // ---- §5 rows 1–5: match green, anchors, the shared consumption order ----

    [Fact]
    public void Craft_ConsumesThroughTheSharedOrder_BeltAndLastLootedFixedUp()
    {
        var dir = CraftDataDir(new SynthRecipe("r_swift", [("napitek-zycia", 1)], "jantar"));
        try
        {
            var s = Town(9, dir);
            s.Buy("napitek-zycia");
            s.AddToBelt(0, 1); // the pack index lands in belt cell 1 (EngineDoorVerbs law)
            Assert.NotNull(s.Belt[0]);
            s.CraftRecipe("r_swift");
            Assert.Null(s.Belt[0]);                  // no door keeps a reference the pack no longer holds
            Assert.Single(s.Carried);                // the potion consumed, the amber carried
            Assert.Equal("jantar", s.Carried[0].Base.Id);
            Assert.Same(s.Carried[^1], s.LastLooted); // LastLooted retreated, the output re-entered (D-6)
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- §5 row 11: the ilvl law rides ItemLevels.FromCube (fixed beats the pair, clamps) ----

    [Fact]
    public void Craft_IlvlLaw_FixedBeatsThePair_ThePairComputes_BothClampsLand()
    {
        var dA = CraftDataDir(new SynthRecipe("r_pin", [("jantar", 3)], "kugla", Lvl: 30));
        var dB = CraftDataDir(new SynthRecipe("r_half", [("jantar", 3)], "kugla", Plvl: 50, Ilvl: 50));
        var dC = CraftDataDir(new SynthRecipe("r_hot", [("jantar", 3)], "kugla", Plvl: 100, Ilvl: 100));
        var dD = CraftDataDir(new SynthRecipe("r_over", [("jantar", 3)], "kugla", Lvl: 150));
        try
        {
            var a = Town(9, dA); BuyJantars(a, 3);
            Assert.Equal(30, a.CraftRecipe("r_pin").Ilvl); // fixed lvl wins
            var b = Town(9, dB); b.LevelTo(20); BuyJantars(b, 3); // L20 buys → input ilvl 20
            Assert.Equal(20, b.CraftRecipe("r_half").Ilvl); // ⌊50%×20⌋ + ⌊50%×20⌋ = 20 (R7a)
            var c = Town(9, dC); c.LevelTo(50); BuyJantars(c, 3);
            Assert.Equal(99, c.CraftRecipe("r_hot").Ilvl); // 50 + 50 = 100 → clamp 99
            var d = Town(9, dD); BuyJantars(d, 3);
            Assert.Equal(99, d.CraftRecipe("r_over").Ilvl); // the fixed path clamps too
        }
        finally { foreach (var dir in new[] { dA, dB, dC, dD }) Directory.Delete(dir, true); }
    }

    // ---- §5 row 12 + witness 15: forced magic, pool window, the pre-pick stamp ----

    [Fact]
    public void Craft_ForcedMagic_RollsInsideTheAlvlWindow_AndTwinSeedsMatch()
    {
        var dir = CraftDataDir(new SynthRecipe("r_mag", [("jantar", 3)], "kugla", Lvl: 30));
        try
        {
            var s = Town(9, dir); BuyJantars(s, 3);
            var r = s.CraftRecipe("r_mag");
            Assert.Equal(ItemQuality.Magic, s.Carried[^1].Quality); // FORCED — no tier roll consulted
            Assert.NotEmpty(r.AffixIds);
            int alvl = ItemLevels.AffixLevel(r.Ilvl, s.Bases.For("kugla").Qlvl, s.Bases.For("kugla").MagicLevelAdd);
            foreach (var (id, level, maxLevel) in AffixWindows())
                if (r.AffixIds.Contains(id)) Assert.InRange(alvl, level, maxLevel); // the existing alvl gate
            var t = Town(9, dir); BuyJantars(t, 3); // same-seed twin: identical affixes
            Assert.Equal(t.CraftRecipe("r_mag").AffixIds, r.AffixIds);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>The affix windows read from the shipped data file (never hardcoded).</summary>
    private static IEnumerable<(string Id, int Level, int MaxLevel)> AffixWindows()
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(TestData.DataDir(), "affixes.json")));
        foreach (var a in doc.RootElement.GetProperty("affixes").EnumerateArray())
            yield return (a.GetProperty("id").GetString()!,
                a.GetProperty("level").GetInt32(), a.GetProperty("maxLevel").GetInt32());
    }

    [Fact]
    public void Craft_ZeroAffixForcedMagic_StaysMagic_TheStampPrecedesThePicks()
    {
        // Witness 15 (§5, Cycle-2 P2): the eligible pool yields NO picks — the fixture's
        // affixes copy stands empty (the RangedDataDir temp-copy convention, no shipped
        // file touched). BuildMagic stamps Magic BEFORE any pick (AffixPool.cs:156) and
        // the pipeline returns the stamped item as-is (TreasureClass.cs:228).
        var dir = CraftDataDirNoAffixes(new SynthRecipe("r_plain", [("jantar", 3)], "kugla", Lvl: 30));
        try
        {
            var s = Town(9, dir); BuyJantars(s, 3);
            var r = s.CraftRecipe("r_plain");
            Assert.Empty(r.AffixIds);                        // zero picks happened
            Assert.Equal(ItemQuality.Magic, s.Carried[^1].Quality); // ...and the item is STILL magic
            var save = Path.Combine(Path.GetTempPath(), $"craft-{Guid.NewGuid():N}.save");
            try
            {
                s.Save(save);
                var loaded = GameSession.Load(save, dir);
                Assert.Equal(ItemQuality.Magic, loaded.Carried[^1].Quality); // the stamp survives ItemSave
                Assert.Empty(loaded.Carried[^1].AffixIds);
            }
            finally { File.Delete(save); }
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- witness 16 (§5, Cycle-2 P2): the failed-forced-rare fallback stamps RARE ----

    [Fact]
    public void Craft_FailedForcedRare_StampsQualityRare_WithEmptyAffixes()
    {
        var dir = CraftDataDir(new SynthRecipe("r_low", [("jantar", 3)], "kugla",
            Quality: "rare", Lvl: 5)); // lvl 5 < RareGen.IlvlGate 27 → TryBuild false
        try
        {
            var s = Town(9, dir); BuyJantars(s, 3);
            var r = s.CraftRecipe("r_low");
            var item = s.Carried[^1];
            Assert.Equal(ItemQuality.Rare, item.Quality); // the verbatim terminal line (TreasureClass.cs:231) — NOT plain
            Assert.Empty(r.AffixIds);
            Assert.Empty(item.Mods);
            Assert.Equal(5, item.ItemLevel);
            var outRow = Assert.Single(JsonNode.Parse(s.Dump())!["carried"]!.AsArray()
                .Where(i => i!["base"]!.GetValue<string>() == "kugla"));
            Assert.Equal(4, outRow!["quality"]!.GetValue<int>()); // dump-visible: (int)Rare
            var save = Path.Combine(Path.GetTempPath(), $"craft-{Guid.NewGuid():N}.save");
            try
            {
                s.Save(save);
                var loaded = GameSession.Load(save, dir);
                Assert.Equal(ItemQuality.Rare, loaded.Carried[^1].Quality); // save-visible (ItemSave.Quality)
            }
            finally { File.Delete(save); }
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- §5 rows 9–10: determinism + the save round-trip law (v7, NO bump) ----

    [Fact]
    public void Craft_SaveLoad_DumpMatchesSameSeedRecraft_ForcedRareSurvives_VersionStays7()
    {
        var dir = CraftDataDir(new SynthRecipe("r_rich", [("jantar", 3)], "kugla",
            Quality: "rare", Lvl: 30)); // ≥ 27: TryBuild TRUE — a real rare with affixes
        try
        {
            var s = Town(7, dir); BuyJantars(s, 3);
            var r = s.CraftRecipe("r_rich");
            Assert.NotEmpty(r.AffixIds); // RareGen built it (the gate passed at 30)
            var dumpA = s.Dump();
            var save = Path.Combine(Path.GetTempPath(), $"craft-{Guid.NewGuid():N}.save");
            try
            {
                s.Save(save);
                Assert.Equal(7, JsonNode.Parse(File.ReadAllText(save))!["version"]!.GetValue<int>());
                var loaded = GameSession.Load(save, dir);
                Assert.Equal(dumpA, loaded.Dump()); // craft → save → load → dump BYTE-IDENTICAL
                var t = Town(7, dir); BuyJantars(t, 3);
                t.CraftRecipe("r_rich");
                Assert.Equal(dumpA, t.Dump()); // ...== the same-seed RE-CRAFT (the round-trip law)
                var src = s.Carried[^1]; var kept = loaded.Carried[^1];
                Assert.Equal(ItemQuality.Rare, kept.Quality);
                Assert.Equal(src.AffixIds, kept.AffixIds); // the ItemSave row carried them
                Assert.Equal(src.Mods, kept.Mods);
            }
            finally { File.Delete(save); }
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void SaveVersion_Stays7_TheCraftDoorAddsNoSaveField()
    {
        Assert.Equal(7, SaveSerializer.SaveVersion); // §3 D7: the craft rides existing rows
    }

    // ---- §5 row 13 + §4: the CLI twin verb, echoed and refused, on the fixture dir ----

    [Fact]
    public void CraftCli_Green_EchoesTheReportRows_Positionally()
    {
        var dir = CraftDataDir(new SynthRecipe("r_ember", [("jantar", 3)], "obereg"));
        try
        {
            var (exit, lines) = RunCraftScript(
                "new koldun\nbuy jantar\nbuy jantar\nbuy jantar\ncraft r_ember\n", dir, 42);
            Assert.Equal(0, exit);
            var echo = Node(lines, "craft");
            Assert.Equal("craft", echo["CMD"]!.GetValue<string>());
            Assert.Equal("r_ember", echo["RECIPE"]!.GetValue<string>());
            Assert.Equal(new[] { "jantar@0,0", "jantar@0,1", "jantar@0,2" },
                echo["CONSUMED"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray());
            Assert.Equal("obereg", echo["OUT_BASE"]!.GetValue<string>());
            Assert.Contains("Obereg", echo["OUT_ITEM"]!.GetValue<string>()); // composed magic name over the base
            var affixes = echo["AFFIXES"]!.AsArray();
            Assert.Single(affixes); // ilvl 1 < 27: the count table answers exactly ONE pick (AffixPool law)
            Assert.Equal(0, echo["OUT_ROW"]!.GetValue<int>());
            Assert.Equal(0, echo["OUT_COL"]!.GetValue<int>());
            Assert.Equal(1, echo["ILVL"]!.GetValue<int>()); // absent lvl/plvl/ilvl → −1/0/0 → clamp 1
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void CraftCli_UnknownRecipe_RidesTheErrorLine_AndExit1_OnTheShippedDir()
    {
        var (exit, lines) = RunCraftScript("new koldun\ncraft lopata\n", TestData.DataDir(), 42);
        Assert.Equal(1, exit);
        Assert.Equal("ritual recipe 'lopata' is unknown",
            JsonNode.Parse(lines[^1])!["ERROR"]!.GetValue<string>());
    }

    [Fact]
    public void CraftCli_SameSeedTwoRuns_DumpsAndSaves_ByteIdentical()
    {
        var dir = CraftDataDir(new SynthRecipe("r_ember", [("jantar", 3)], "obereg",
            Quality: "rare", Lvl: 30));
        var body = "new koldun\nbuy jantar\nbuy jantar\nbuy jantar\ncraft r_ember\ndump\nsave {0}\n";
        var saveA = Path.Combine(Path.GetTempPath(), $"craft-{Guid.NewGuid():N}.a");
        var saveB = Path.Combine(Path.GetTempPath(), $"craft-{Guid.NewGuid():N}.b");
        try
        {
            var (eA, a) = RunCraftScript(string.Format(body, saveA), dir, 42);
            var (eB, b) = RunCraftScript(string.Format(body, saveB), dir, 42);
            Assert.Equal(0, eA); Assert.Equal(0, eB);
            // The MC 11958 stated shape: stdout equals EXCEPT the PATH echo line;
            // the SAVE BYTES themselves are the artifact and cmp EMPTY cross-run.
            Assert.Equal(
                string.Join('\n', a.Where(l => !l.Contains("\"CMD\":\"save\""))),
                string.Join('\n', b.Where(l => !l.Contains("\"CMD\":\"save\""))));
            Assert.Equal(File.ReadAllBytes(saveA), File.ReadAllBytes(saveB)); // save bytes equal
        }
        finally
        {
            File.Delete(saveA); File.Delete(saveB); Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void CraftCli_ReplayLaw_EchoedLinesReRun_LandTheSameDump()
    {
        // The BABA_LOG law: the grammar lines the run's echoes attest to, fed back as a
        // script on the same seed, produce a byte-equal dump — and a trailing REFUSAL
        // (which draws nothing) never moves the state the dump reads.
        var dir = CraftDataDir(new SynthRecipe("r_ember", [("jantar", 3)], "obereg"));
        try
        {
            var (e1, a) = RunCraftScript(
                "new koldun\nbuy jantar\nbuy jantar\nbuy jantar\ncraft r_ember\ndump\n", dir, 42);
            Assert.Equal(0, e1);
            var dumpA = a.Single(l => JsonNode.Parse(l)!["CMD"]?.GetValue<string>() == "dump");
            var (e2, c) = RunCraftScript(
                "new koldun\nbuy jantar\nbuy jantar\nbuy jantar\ncraft r_ember\ndump\ncraft r_wrong\n", dir, 42);
            Assert.Equal(1, e2); // the refusal still stops the script with exit 1 —
            var dumpC = c.Single(l => JsonNode.Parse(l)!["CMD"]?.GetValue<string>() == "dump");
            Assert.Equal(dumpA, dumpC); // ...but the state BEFORE it was byte-identical
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- §5 row 14: the standing scans (three streams; silence; the story script) ----

    [Fact]
    public void Scans_ThreeRngStreamsNoFourth_LoaderSilence_TheStoryScriptCarriesNoCraftLine()
    {
        var root = RepoRoot();
        var core = File.ReadAllText(Path.Combine(root, "src", "BabaYaga.Sim", "GameSession.cs"));
        Assert.Equal(3, core.Split("internal Rng ").Length - 1); // Combat/Drop/Enemy — and NO fourth (D6)
        foreach (var file in new[] { "CubeRecipeTable.cs", "GameSession.RitualCraft.cs" })
        {
            var text = File.ReadAllText(Path.Combine(root, "src", "BabaYaga.Sim", file));
            Assert.DoesNotContain("Console.Write", text); // the ZERO-STDOUT law (§2 ii)
            Assert.DoesNotContain("GD.Print", text);
        }
        var journey = File.ReadAllLines(Path.Combine(root, "tests", "journey.txt"));
        Assert.DoesNotContain(journey, l => l.Trim().Split(' ')[0] == "craft"); // anchors byte-stable
    }

    private static string RepoRoot() // the shipped-file scan idiom (HeroEquipCellTests walk-up)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "BabaYaga.Sim")))
            dir = dir.Parent;
        return dir is null
            ? throw new DirectoryNotFoundException("no repo root above the test assembly")
            : dir.FullName;
    }
}
