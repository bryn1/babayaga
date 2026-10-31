using System.Text.Json;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// SKELETON WAVE (card/11794): the gate over the skeleton data files — every new
/// data/*.json PARSES with its required keys; the act2-5 graphs are FULLY LINKED
/// (no dangling edges, symmetric, exactly one town, consistent waypoint flags);
/// the six class files mirror the class-koldun.json key set; cube/hirelings/npc
/// rows carry their key contracts. These files have NO sim-loader yet by design
/// (the wave added zero Sim edits) — the tests are what makes them real, and what
/// goes RED when a skeleton rots (e.g. an edge pointing at a removed area).
/// </summary>
public sealed class SkeletonDataTests
{
    private static string Data(string file) => Path.Combine(TestData.DataDir(), file);
    private static JsonDoc Parse(string file) => JsonDoc.Load(Data(file));

    private sealed class JsonDoc : IDisposable
    {
        private readonly JsonDocument _doc;
        private JsonDoc(JsonDocument doc) => _doc = doc;
        public static JsonDoc Load(string path) => LoadFile(path);
        private static JsonDoc LoadFile(string path) => new(JsonDocument.Parse(File.ReadAllText(path)));
        public JsonElement Root => _doc.RootElement;
        public void Dispose() => _doc.Dispose();
    }

    private static readonly string[] ActFiles =
    [
        "act2-desert.json", "act3-jungle.json", "act4-ash.json", "act5-ruins.json",
    ];

    private static readonly string[] ClassFiles =
    [
        "class-koldun.json", "class-kalozhan.json", "class-zerca.json",
        "class-borowa.json", "class-wilczarz.json", "class-zbrojnik.json",
    ];

    // ---- every new skeleton file parses + carries its _comment + required keys ----

    [Theory]
    [InlineData("act2-desert.json")]
    [InlineData("act3-jungle.json")]
    [InlineData("act4-ash.json")]
    [InlineData("act5-ruins.json")]
    [InlineData("class-koldun.json")]
    [InlineData("class-kalozhan.json")]
    [InlineData("class-zerca.json")]
    [InlineData("class-borowa.json")]
    [InlineData("class-wilczarz.json")]
    [InlineData("class-zbrojnik.json")]
    [InlineData("cube-recipes.json")]
    [InlineData("hirelings.json")]
    [InlineData("npc-roster.json")]
    public void SkeletonFile_Parses_With_Comment(string file)
    {
        using var d = Parse(file);
        Assert.True(d.Root.TryGetProperty("_comment", out var c) && c.GetString()!.Length > 10,
            $"{file} must carry a non-trivial _comment (skeleton marker)");
    }

    // ---- acts 2-5: EXACT act1 schema keys, >=4 regions, fully linked, one town ----

    public static TheoryData<string> ActFileData()
    {
        var t = new TheoryData<string>();
        foreach (var f in ActFiles) t.Add(f);
        return t;
    }

    [Theory]
    [MemberData(nameof(ActFileData))]
    public void ActGraph_Has_ExactAct1Schema_And_FullyLinked(string file)
    {
        using var d = Parse(file);
        var areas = d.Root.GetProperty("areas");
        var rows = areas.EnumerateArray().ToList();
        Assert.True(rows.Count >= 4, $"{file} needs >=4 regions");

        var keys = new HashSet<string>();
        foreach (var a in rows)
        {
            // EXACT schema keys of data/act1-areas.json (the AreaRow contract)
            foreach (var k in new[]
            {
                "key", "name", "act", "level", "monLvlNightmare", "monLvlHell",
                "densityPercent", "isTown", "isBossLair", "hasWaypoint", "linkedTo",
            })
                Assert.True(a.TryGetProperty(k, out _), $"{file} region missing key '{k}'");

            var unlinked = a.EnumerateObject().Where(p => p.Name != "_comment")
                .Select(p => p.Name).OrderBy(n => n).ToList();
            Assert.Equal(
                new[] { "act", "densityPercent", "hasWaypoint", "isBossLair", "isTown",
                        "key", "level", "linkedTo", "monLvlHell", "monLvlNightmare", "name" },
                unlinked); // no schema drift: EXACTLY the act1 key set
            Assert.True(keys.Add(a.GetProperty("key").GetString()!),
                $"{file} duplicate area key");
        }

        // Fully linked: no dangling edges, symmetry, exactly one town, one act.
        int towns = 0;
        int actNo = -1;
        foreach (var a in rows)
        {
            var key = a.GetProperty("key").GetString()!;
            var linked = a.GetProperty("linkedTo").EnumerateArray()
                .Select(e => e.GetString()!).ToList();
            Assert.NotEmpty(linked); // every region links somewhere
            foreach (var link in linked)
            {
                Assert.True(keys.Contains(link), $"{file}: {key} has DANGLING edge to '{link}'");
                var peer = rows.Single(r => r.GetProperty("key").GetString() == link);
                Assert.Contains(key, peer.GetProperty("linkedTo").EnumerateArray()
                    .Select(e => e.GetString()!).ToList()); // symmetric
            }
            if (a.GetProperty("isTown").GetBoolean()) towns++;
            if (actNo < 0) actNo = a.GetProperty("act").GetInt32();
            Assert.Equal(actNo, a.GetProperty("act").GetInt32());
            Assert.True(a.GetProperty("hasWaypoint").GetBoolean(),
                $"{file}: {key} — every skeleton waypoint flag lights (travel is the slice headline)");
            Assert.True(a.GetProperty("level").GetInt32() >= 1);
            Assert.True(a.GetProperty("densityPercent").GetInt32() >= 0);
        }
        Assert.Equal(1, towns); // exactly one town per act
    }

    // ---- the six class files mirror class-koldun.json's key set ----

    [Fact]
    public void ClassFiles_Mirror_Koldun_KeySet()
    {
        string[] expected;
        using (var koldun = Parse("class-koldun.json"))
            expected = koldun.Root.EnumerateObject().Select(p => p.Name)
                .Where(n => n != "_comment").OrderBy(n => n).ToArray();
        Assert.Equal(
            new[] { "blockClassFactor", "key", "lifePerLevel", "lifePerVit", "manaPerEner",
                    "manaPerLevel", "name", "startDex", "startEner", "startStr", "startVit" },
            expected);

        foreach (var file in ClassFiles)
        {
            using var d = Parse(file);
            var got = d.Root.EnumerateObject().Select(p => p.Name)
                .Where(n => n != "_comment").OrderBy(n => n).ToList();
            Assert.Equal(expected, got); // the mirror contract
            Assert.Equal(Path.GetFileNameWithoutExtension(file)[ "class-".Length..],
                d.Root.GetProperty("key").GetString()); // file ↔ key agreement
        }
    }

    // ---- cube / hirelings / npc key contracts ----

    [Fact]
    public void CubeRecipes_Have_5_Placeholders_With_Key_Set()
    {
        using var d = Parse("cube-recipes.json");
        var recipes = d.Root.GetProperty("recipes").EnumerateArray().ToList();
        Assert.Equal(5, recipes.Count);
        foreach (var r in recipes)
        {
            foreach (var k in new[] { "id", "name", "inputs", "output", "ilvlRule", "flags" })
                Assert.True(r.TryGetProperty(k, out _), $"cube recipe missing key '{k}'");
            Assert.NotEmpty(r.GetProperty("inputs").EnumerateArray());
            foreach (var input in r.GetProperty("inputs").EnumerateArray())
            {
                Assert.True(input.TryGetProperty("itemId", out _));
                Assert.True(input.TryGetProperty("count", out _));
            }
            Assert.True(r.GetProperty("output").TryGetProperty("itemId", out _));
        }
    }

    [Fact]
    public void Hirelings_Have_3_Kinds_With_Key_Set()
    {
        using var d = Parse("hirelings.json");
        var kinds = d.Root.GetProperty("hirelings").EnumerateArray().ToList();
        Assert.Equal(3, kinds.Count);
        foreach (var h in kinds)
            foreach (var k in new[] { "id", "name", "act", "weapon", "attackKind", "hireGold" })
                Assert.True(h.TryGetProperty(k, out _), $"hireling missing key '{k}'");
    }

    [Fact]
    public void NpcRoster_Covers_Vendor_Keeper_Act1_Npcs_And_Act25_Placeholders()
    {
        using var d = Parse("npc-roster.json");
        var npcs = d.Root.GetProperty("npcs").EnumerateArray().ToList();
        var roles = new[] { "shop", "repair", "gamble", "stash", "craft", "quest", "hire", "waypoint" };

        var act1 = npcs.Where(n => n.GetProperty("act").GetInt32() == 1).ToList();
        Assert.True(act1.Count >= 4, "act1 needs >=4 named NPCs");
        Assert.All(npcs, n => Assert.True(
            n.GetProperty("roles").EnumerateArray().All(r => roles.Contains(r.GetString())),
            "roles must ride the LEDGER row-60 role set"));
        Assert.Contains(npcs, n => n.GetProperty("roles").EnumerateArray()
            .Any(r => r.GetString() == "shop") && n.GetProperty("name").GetString()!.Contains("Vesna"));
        Assert.Contains(npcs, n => n.GetProperty("name").GetString()!.Contains("Wayward Keeper"));

        foreach (var act in new[] { 2, 3, 4, 5 })
            Assert.Contains(npcs, n => n.GetProperty("act").GetInt32() == act
                && n.GetProperty("placeholder").GetBoolean()); // placeholder rows for acts 2-5
    }

    // ---- the game/data one-way copy is verbatim (never hand-edited) ----

    [Fact]
    public void GameDataCopy_IsVerbatim()
    {
        var repoData = TestData.DataDir();
        var gameData = Path.Combine(Path.GetDirectoryName(repoData)!, "game", "data");
        Assert.True(Directory.Exists(gameData), "game/data/ must exist (one-way copy)");
        foreach (var file in Directory.GetFiles(repoData, "*.json"))
        {
            var twin = Path.Combine(gameData, Path.GetFileName(file));
            Assert.True(File.Exists(twin), $"game/data missing {Path.GetFileName(file)}");
            Assert.Equal(File.ReadAllText(file), File.ReadAllText(twin)); // byte-verbatim copy
        }
    }
}
