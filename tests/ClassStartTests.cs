using System.Text.Json;
using BabaYaga.Sim;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// ROW 1 PIN (ARCH-verdict §4 P3-4 → MC 11949): the per-class START values.
/// The attribute MECHANICS were tested (InvalidStatSpend, to-hit, life); the
/// START rows themselves were asserted nowhere — "no test pins the ClassProfile
/// start rows" (verdict §4). The oracle rule (T0b-mechanics-spec P1 §1.2,
/// VERIFIED — https://d2r.org/mechanics/life-and-vitality): "Base life at level
/// 1 = Vitality + flat 30; starting mana = Energy … Life at level 1 is Vitality
/// plus the table's flat 30 (hpadd); starting mana is the Energy."
/// Each class of the shipped roster gets one row. Every EXPECTED number is
/// QUOTED VERBATIM from its data/class-&lt;key&gt;.json twin (never invented) — the
/// row comments carry the quote; the test then reads the json fresh and pins
/// BOTH directions: the json still says the quoted values, AND the derived
/// level-1 life/mana equal them (startVit + 30 = life@1; mana@1 = startEner).
/// The five non-koldun rows are the M4 skeleton-wave PLACEHOLDER values (their
/// _comment says so) — the pin locks what SHIPS today; the class-design pass
/// re-pins deliberately when a real row lands. Koldun carries the extra twin
/// row: its json must equal the shipped Stats.Koldun design row verbatim.
/// RED-FIRST: on the base tree this file passes as written (the values are
/// true today — a pin of a truth goes red by MUTATION); the planted-red proof
/// is run on a scratch copy (fleet gate law): startVit 20→19 in class-koldun.json
/// and the koldun row REDS; startEner 25→24 in a sibling and that sibling REDS
/// (evidence pasted at the card close, run .audits/202610301950-31593a83).
/// </summary>
public sealed class ClassStartTests
{
    private static string Data(string file) => Path.Combine(TestData.DataDir(), file);

    private sealed record JsonRow(
        string Key, int Str, int Dex, int Ener, int Vit,
        int LifePerLevel, int ManaPerLevel, int LifePerVit, int ManaPerEner, int Block)
    {
        public static JsonRow Load(string file)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Data(file)));
            var r = doc.RootElement;
            return new JsonRow(
                r.GetProperty("key").GetString()!,
                r.GetProperty("startStr").GetInt32(), r.GetProperty("startDex").GetInt32(),
                r.GetProperty("startEner").GetInt32(), r.GetProperty("startVit").GetInt32(),
                r.GetProperty("lifePerLevel").GetInt32(), r.GetProperty("manaPerLevel").GetInt32(),
                r.GetProperty("lifePerVit").GetInt32(), r.GetProperty("manaPerEner").GetInt32(),
                r.GetProperty("blockClassFactor").GetInt32());
        }

        public ClassProfile ToProfile() => new(
            Key, Str, Dex, Ener, Vit, LifePerLevel, ManaPerLevel, LifePerVit, ManaPerEner, Block);
    }

    // Every row below QUOTES its data json verbatim (file: startStr/startDex/startEner/startVit).
    [Theory]
    // class-koldun.json: "startStr": 15, "startDex": 20, "startEner": 30, "startVit": 20
    //   ⇒ life@1 = 20 + 30 = 50;  mana@1 = Ener = 30
    [InlineData("class-koldun.json", 15, 20, 30, 20, 50, 30)]
    // class-kalozhan.json: "startStr": 20, "startDex": 20, "startEner": 20, "startVit": 25
    //   ⇒ life@1 = 25 + 30 = 55;  mana@1 = Ener = 20
    [InlineData("class-kalozhan.json", 20, 20, 20, 25, 55, 20)]
    // class-zerca.json: "startStr": 20, "startDex": 20, "startEner": 20, "startVit": 25
    //   ⇒ life@1 = 25 + 30 = 55;  mana@1 = Ener = 20
    [InlineData("class-zerca.json", 20, 20, 20, 25, 55, 20)]
    // class-borowa.json: "startStr": 20, "startDex": 20, "startEner": 20, "startVit": 25
    //   ⇒ life@1 = 25 + 30 = 55;  mana@1 = Ener = 20
    [InlineData("class-borowa.json", 20, 20, 20, 25, 55, 20)]
    // class-wilczarz.json: "startStr": 20, "startDex": 20, "startEner": 20, "startVit": 25
    //   ⇒ life@1 = 25 + 30 = 55;  mana@1 = Ener = 20
    [InlineData("class-wilczarz.json", 20, 20, 20, 25, 55, 20)]
    // class-zbrojnik.json: "startStr": 20, "startDex": 20, "startEner": 20, "startVit": 25
    //   ⇒ life@1 = 25 + 30 = 55;  mana@1 = Ener = 20
    [InlineData("class-zbrojnik.json", 20, 20, 20, 25, 55, 20)]
    public void ClassStartRow_IsPinned_LifeAndManaAtLevel1(
        string file, int str, int dex, int ener, int vit, int lifeAt1, int manaAt1)
    {
        var row = JsonRow.Load(file);

        // The json still carries exactly the QUOTED start row (never invent: quote + pin).
        Assert.Equal(str, row.Str);
        Assert.Equal(dex, row.Dex);
        Assert.Equal(ener, row.Ener);
        Assert.Equal(vit, row.Vit);

        // P1 §1.2: "Life at level 1 is Vitality plus the table's flat 30 (hpadd)".
        Assert.Equal(lifeAt1, row.ToProfile().LifeAtLevel1);
        Assert.Equal(vit + Stats.LifeFlatAtLevel1, row.ToProfile().LifeAtLevel1);
        // P1 §1.2: "starting mana is the Energy".
        Assert.Equal(manaAt1, row.ToProfile().ManaAtLevel1);
        Assert.Equal(ener, row.ToProfile().ManaAtLevel1);

        // The shipped derivations agree with the start rows at level 1 with
        // nothing allocated and no gear: base formulas reduce to the starts.
        var p = row.ToProfile();
        Assert.Equal(lifeAt1, Stats.BaseLife(p, 1, 0, 0));
        Assert.Equal(manaAt1, Stats.BaseMana(p, 1, 0, 0));
    }

    [Fact]
    public void Koldun_JsonTwin_EqualsShippedDesignRow()
    {
        // data/class-koldun.json _comment: "the data twin … values verbatim from
        // src/BabaYaga.Sim/Stats.cs ClassProfile Koldun". Pinned both directions.
        var json = JsonRow.Load("class-koldun.json");
        var k = Stats.Koldun;
        Assert.Equal(k.Key, json.Key);
        Assert.Equal(k.StartStr, json.Str);
        Assert.Equal(k.StartDex, json.Dex);
        Assert.Equal(k.StartEner, json.Ener);
        Assert.Equal(k.StartVit, json.Vit);
        Assert.Equal(k.LifePerLevel, json.LifePerLevel);
        Assert.Equal(k.ManaPerLevel, json.ManaPerLevel);
        Assert.Equal(k.LifePerVit, json.LifePerVit);
        Assert.Equal(k.ManaPerEner, json.ManaPerEner);
        Assert.Equal(k.BlockClassFactor, json.Block);
        // And the shipped profile's own level-1 starts: 50 life / 30 mana.
        Assert.Equal(50, Stats.Koldun.LifeAtLevel1); // 20 Vit + flat 30
        Assert.Equal(30, Stats.Koldun.ManaAtLevel1); // = 30 Ener
    }
}
