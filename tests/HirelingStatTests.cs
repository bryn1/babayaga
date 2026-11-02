using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// M4-S16 GEAR STATS LIVE — the FOUR SIMPLE ROWS of spec-3 §6.7c AMENDED
/// (MC 11914; source out/T0b-sec6-7c-amendment.md; engine pin D2MOO
/// 1a30fbe4875b0baebfc56fca94e1dde4cd27f552). (a) a worn weapon's damage range
/// REPLACES the bracket dice (one range per weapon base — the fenced ORIGINAL
/// DESIGN; an unarmed merc keeps the bracket dice); (b) enhanced-damage % on
/// worn gear multiplies the summed range with the −90 clamp floor (SUnitDmg
/// :227 max(nDamagePct, −90)); (c) the ORDER is the amendment's, paid through
/// the ONE hero primitive CombatResolver.PhysicalDamage (never a second
/// formula): flat on gear widens the rolled range, then the % floors over it,
/// then min+rand(max−min) rolls; (d) +max-life on worn gear raises MaxLife,
/// DERIVED from the persisted gear (no save field, v7 UNCHANGED) — it survives
/// death, the captain's revive and the level-up recompute. The COMPLEX rows
/// (AR/to-hit curve, block%, +skills, IAS, procs) stay DEFERRED — named in the
/// LEDGER row 66, not tested here.
/// MEASURED DATA STATE (READ FIRST, this session): data/item-bases.json
/// carries NO damage columns and the affix pool carries no ED/flat/life ROLLS
/// onto merc-wearable gear paths the shipped doors walk — so the MECHANISM is
/// proven with SYNTHETIC items: a temp copy of the real data dir with damage
/// columns added to the soluga row (the loader reads damageMin/damageMax), and
/// Item.Mods lines added through the public list (the existing affix
/// vocabulary: dmg_pct = ED %, dmg_min/dmg_max = flat damage, life = +max
/// life). Real data rows await the data beat — the LEDGER names the gap.
/// RNG LAW (HARD, rows 65–66): a no-gear merc costs NOTHING — the damage
/// sequence and the save bytes below were captured FROM THE BASE BUILD
/// (4aece9e + pure DamageSum report plumbing, 2026-11-02, this run) for the
/// pinned script: new koldun seed 42 / hire strzelczyni / levelto 10 / merc
/// XP-grant to L10 / MercStrike pokrzywnik ×40 / save.
/// RED-FIRST: run against the base build the bounds, exact-int and life rows
/// FAIL — the worn weapon is ignored, no clamp, no flat, no gear life (the
/// §6.7b DEFER row). They pass ONLY once §6.7c ships.
/// </summary>
public class HirelingStatTests
{
    private const string Bow = "soluga";      // bows — the strzelczyni weapon row
    private const string Body = "watan";      // body — universal merc slot

    // The BASE-BUILD capture (seed 42, the pinned script above): 40 hits, all
    // bracket dice 2..4, and the save file's SHA-256. Byte-identity here IS
    // the RNG law — any no-gear stream or save shift flips one of them.
    private static readonly long[] BaseHits =
    [
        2, 2, 4, 3, 4, 4, 3, 3, 3, 2, 3, 3, 3, 4, 4, 4, 3, 4, 2, 2,
        4, 4, 3, 2, 4, 4, 3, 3, 4, 3, 3, 3, 2, 2, 4, 4, 3, 2, 2, 4,
    ];
    private const string BaseSaveSha = "85BD52358DEA13821F96D8BD954C5E7CA736930D8C0847D1ED8AF97E21DB8AEF";

    /// A temp copy of the REAL data dir with a damage range written onto the
    /// soluga row — the loader's future data columns (data-future LEDGER row).
    private static string RangedDataDir(int min, int max)
    {
        var src = TestData.DataDir();
        var dir = Path.Combine(Path.GetTempPath(), $"s16-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(src, "*.json"))
            File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
        var path = Path.Combine(dir, "item-bases.json");
        var doc = JsonNode.Parse(File.ReadAllText(path))!;
        var row = doc["bases"]!.AsArray().First(b => b!["id"]!.GetValue<string>() == Bow);
        row!["damageMin"] = min;
        row!["damageMax"] = max;
        File.WriteAllText(path, doc.ToJsonString());
        return dir;
    }

    /// The pinned no-gear setup carried into gear runs: merc at L10 (the XP-
    /// grant idiom HirelingTests rows use — Gain AT the hero's level), the one
    /// level where the capture stream was taken.
    private static GameSession TownMercL10(ulong seed = 42, string? dataDir = null)
    {
        var s = GameSession.New(seed, "koldun", dataDir ?? TestData.DataDir());
        s.Hire("strzelczyni");
        s.LevelTo(10);
        s.Merc!.Gain(1_000_000_000L, s.Character.Level);
        return s;
    }

    private static Item BuyOne(GameSession s, string baseId)
    {
        s.Buy(baseId);
        return s.Carried[^1];
    }

    /// Wear one bought item with its mods added through the public authoring
    /// list — the same ItemMod shape the affix pool rolls (dmg_pct / dmg_min /
    /// dmg_max / life). The merc stands in town with the hero, so the give
    /// door's same-area gate passes.
    private static Item GiveOne(GameSession s, string slot, string baseId, params (string Stat, int Value)[] mods)
    {
        var item = BuyOne(s, baseId);
        foreach (var (stat, value) in mods) item.Mods.Add(new ItemMod(stat, value));
        s.GiveHirelingGear(slot, 0, 0); // first-fit anchor — the pack is empty when each item is bought
        return item;
    }

    private static List<long> StrikeHits(GameSession s, int times)
    {
        var hits = new List<long>();
        for (int i = 0; i < times && !s.IsDead && s.Merc is { IsDead: false }; i++)
        {
            var r = s.MercStrike("pokrzywnik", 1);
            if (r.DamageSum > 0) hits.Add(r.DamageSum);
        }
        return hits;
    }

    // ---- (a) THE SWAP — the weapon range REPLACES the bracket dice ----------

    [Fact]
    public void WeaponRange_Equipped_StrikesAreBoundedByTheWeaponRange_AndDivergeFromTheBracket()
    {
        var s = TownMercL10(dataDir: RangedDataDir(6, 9)); // one-handed swap, ONE range per base
        var bow = GiveOne(s, "weapon", Bow);
        Assert.Same(bow, s.Merc!.Gear[MercGearSlot.Weapon]); // the ranged weapon IS worn

        var hits = StrikeHits(s, 40);
        Assert.NotEmpty(hits);
        Assert.All(hits, d => Assert.InRange(d, 6, 9)); // the weapon range owns the roll
        // Divergence: the same-seed no-gear sequence is the bracket capture 2..4 —
        // a swap that never happened would replay EXACTLY those values.
        Assert.DoesNotContain(hits, h => h < 6);
        Assert.NotEqual(BaseHits, hits);
    }

    [Fact]
    public void WeaponRange_Stripped_TheBracketDiceReturn()
    {
        var s = TownMercL10(dataDir: RangedDataDir(6, 9));
        GiveOne(s, "weapon", Bow);
        var withBow = StrikeHits(s, 12);
        Assert.All(withBow, d => Assert.InRange(d, 6, 9));

        var stripped = s.StripHirelingGear("weapon");
        Assert.Equal(Bow, stripped.Base);                       // the same instance rides back
        var bare = StrikeHits(s, 12);                           // the bracket owns the roll again
        Assert.NotEmpty(bare);
        Assert.All(bare, d => Assert.InRange(d, 2, 4));         // L10 bracket 2..4, never 6..9
    }

    // ---- (b) ED % — the −90 clamp floor, the amendment's arithmetic ---------

    [Fact]
    public void EnhancedDamage_Plus50_MultipliesTheWeaponRangeWithExactFlooredInts()
    {
        // Pinned 7–7 weapon so every strike lands on one integer: the summed
        // 7 × (100+50)/100 = 10.5 FLOORS TO 10 — exact, amendment order.
        var s = TownMercL10(dataDir: RangedDataDir(7, 7));
        GiveOne(s, "weapon", Bow);
        GiveOne(s, "body", Body, ("dmg_pct", 50));

        var hits = StrikeHits(s, 12);
        Assert.NotEmpty(hits);
        Assert.All(hits, d => Assert.Equal(10, d)); // floor(7 × 150 ÷ 100) — no rounding up
    }

    [Fact]
    public void EnhancedDamage_Minus95_ClampsToMinus90_ExactArithmetic()
    {
        // Pinned 20–20: the clamp floor is the whole row — max(−95, −90) reads
        // −90: 20 × (100−90)/100 = 2 EXACTLY. A missing clamp rolls 20×5/100 = 1;
        // a mis-floored −95 would never print 2.
        var s = TownMercL10(dataDir: RangedDataDir(20, 20));
        GiveOne(s, "weapon", Bow);
        GiveOne(s, "body", Body, ("dmg_pct", -95));

        var hits = StrikeHits(s, 12);
        Assert.NotEmpty(hits);
        Assert.All(hits, d => Assert.Equal(2, d)); // the −90 floor, integer-exact
    }

    // ---- (c) ORDER — flat BEFORE the %: the amendment's integer order -------

    [Fact]
    public void FlatDamage_IsAddedToTheRange_BeforeThePercent_FloorOfThirteenAndAHalfTimesFifteen()
    {
        // Pinned 10–10 weapon + flat +3/+3 + ED +50: the amendment order is
        // (range + flat) THEN ×(100+ED)/100 FLOOR: (10+3) × 150/100 = 19.5 → 19.
        // Percent-first arithmetic would give floor(10×1.5)+3 = 18 — this test
        // is the plant (c) tripwire.
        var s = TownMercL10(dataDir: RangedDataDir(10, 10));
        GiveOne(s, "weapon", Bow);
        GiveOne(s, "body", Body, ("dmg_pct", 50), ("dmg_min", 3), ("dmg_max", 3));

        var hits = StrikeHits(s, 12);
        Assert.NotEmpty(hits);
        Assert.All(hits, d => Assert.Equal(19, d)); // flat before the %, floor last
    }

    // ---- (d) +MAX LIFE — derived from gear: echo, math, death, revive, LUP --

    [Fact]
    public void PlusLifeItem_RaisesMaxLife_EchoAndMath()
    {
        var s = TownMercL10();                                  // real data: the rookie's own bracket
        long baseMax = s.Merc!.MaxLife;                         // the bracket read, gear-free
        GiveOne(s, "body", Body, ("life", 25));

        Assert.Equal(baseMax + 25, s.Merc.MaxLife);             // derived at the read, never stored
        var r = s.MercStrike("pokrzywnik", 1);
        Assert.Equal(baseMax + 25, r.MercLifeMax);              // the report ECHOES the raised max
        Assert.True(r.MercLife <= r.MercLifeMax);               // current pool rides under it
        // Removing the item lets the max fall back — it rides the ITEM, not the merc.
        s.StripHirelingGear("body");
        Assert.Equal(baseMax, s.Merc.MaxLife);
    }

    [Fact]
    public void PlusLifeItem_SurvivesHeroDeath_AndTheCaptainsRevive_StillRaised()
    {
        var s = GameSession.New(42, "koldun", TestData.DataDir());
        s.Hire("strzelczyni");                                // rookie at L1 — the death-idiom shape
        GiveOne(s, "body", Body, ("life", 25));
        Assert.Equal(40 + 25, s.Merc!.MaxLife);               // 65 = bracket minimum + worn life
        s.Merc.CombatDistance = 4;                            // every counter reaches the HERO (row-83 law)
        s.LevelTo(5);                                         // the HirelingDeathTests cap-friendlier level
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");

        int strikes = 0;
        for (; strikes < 2048 && !s.IsDead; strikes++) s.Attack("ponurak", 1);
        Assert.True(s.IsDead, $"hero survived {strikes} counters — no death transition");
        Assert.True(s.Merc.IsDead, "the hero-death-kills-merc law (row 83) must stand here too");

        s.Respawn();                                          // the stone raises the hero home
        var rv = s.Revive("strzelczyni");                     // the captain raises the SAME mercenary
        long bracketLife = HirelingTable.StatsAt(s.Hirelings.For("strzelczyni"), s.Merc.Level).Life;
        Assert.Equal(bracketLife + 25, s.Merc.MaxLife);       // still raised — DERIVED, v7 unchanged
        Assert.Equal(s.Merc.MaxLife, rv.Life);                // she stands at the RAISED full life
    }

    [Fact]
    public void PlusLifeItem_SurvivesTheLevelUp_GearLifeRidesTheRecompute()
    {
        var s = TownMercL10();                                  // merc stands at L10
        var m = s.Merc!;
        GiveOne(s, "body", Body, ("life", 25));
        Assert.Equal(HirelingTable.StatsAt(s.Hirelings.For("strzelczyni"), 10).Life + 25, m.MaxLife);

        s.LevelTo(20);                                        // raise the cap, then the XP-grant idiom
        int gained = m.Gain(2_000_000_000L, s.Character.Level); // the HirelingTests XP-grant door
        Assert.True(gained > 0, "the XP grant must climb the ladder for this row to mean anything");
        Assert.Equal(HirelingTable.StatsAt(s.Hirelings.For("strzelczyni"), m.Level).Life + 25, m.MaxLife);
        Assert.Equal(m.MaxLife, m.Life);                        // the level-up heal rides the RAISED max
    }

    // ---- THE HARD RNG LAW — no gear costs nothing ---------------------------

    [Fact]
    public void NoGearMerc_DamageSequenceAndSave_ByteIdenticalVsBaseCapture()
    {
        var s = GameSession.New(42, "koldun", TestData.DataDir());
        s.Hire("strzelczyni");
        s.LevelTo(10);
        s.Merc!.Gain(1_000_000_000L, s.Character.Level);

        var hits = new List<long>();
        for (int i = 0; i < 40 && !s.IsDead && s.Merc is { IsDead: false }; i++)
        {
            var r = s.MercStrike("pokrzywnik", 1);
            if (r.DamageSum > 0) hits.Add(r.DamageSum);
        }
        Assert.Equal(BaseHits, hits);                           // the base damage sequence, value by value

        string path = Path.Combine(Path.GetTempPath(), $"s16-save-{Guid.NewGuid():N}.json");
        try
        {
            s.Save(path);
            Assert.Equal(BaseSaveSha, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            // …and the save still LOADS, gear-free (v7 UNCHANGED — nothing new is stored).
            var t = GameSession.Load(path, TestData.DataDir());
            Assert.Equal(s.Dump(), t.Dump());
        }
        finally { File.Delete(path); }
    }
}
