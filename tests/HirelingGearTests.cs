using System.Text.Json;
using System.Text.Json.Nodes;
using BabaYaga.Sim.Cli;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// M4-S15 HIRELING GEAR — the 4-slot model per the VERIFIED amendment (MC 11898;
/// spec-3 §6.7b; out/T0b-sec6-7b-amendment.md §1+§3). A mercenary wears WEAPON,
/// BODY, HELM and SHIELD ONLY — no jewelry: the inventory.txt `Hireling` panel
/// rows read −1 for every jewelry box, the 1.10 give-door whitelists no jewel
/// type, and the official-era matrix names no jewelry in any act (VERIFIED three
/// ways, amendment S1–S3). Give/take are FREE both directions, gated: DEAD-hero
/// gate first (row 64), merc EXISTS, merc ALIVE (amendment D2: "You cannot take
/// gear from a dead mercenary" — the door opens only for the living), SAME AREA
/// (D2's neighboring-room rule read at our area grain — ORIGINAL DESIGN pin),
/// the slot in the 4-slot set, the weapon kind in the merc's data whitelist
/// (hirelings.json gear.weaponTypes — D2 S2 per-kind switch), and the item's
/// own requirements. An occupied slot EXCHANGES (the displaced item rides back
/// into the hero's pack, T2); a pack with no free cell for it refuses BY NAME,
/// nothing half-applied. Gear STAYS through death and returns on the captain's
/// revive of the SAME mercenary (D1 — retention is by object identity, NO flag);
/// a dead mercenary can neither receive nor be stripped (D2); DISMISS FORFEITS
/// the worn gear forever (D3) — the sim has NO ground-drop concept, so the items
/// die with the unit and are gone from every container. STATE READINGS pinned:
/// Item has NO Identified flag (a give-door half D2 has cannot exist here — the
/// gate is vacuous by model shape) and the Hireling model carries NO Str/Dex
/// stat, so a base's ReqStr/ReqDex have nothing to read against; the level req
/// IS enforced. Save bumps to v7 (gear block last); v6 refused by name.
/// RED-FIRST: on the unmodified base this file does not COMPILE — the doors,
/// the Gear surface and the v7 block do not exist there.
/// </summary>
public class HirelingGearTests
{
    private const string Bow = "soluga";      // bows, 1×4, reqDex 20 — the strzelczyni weapon row
    private const string Helm = "szyszak";    // helms, 2×2, reqStr 22 — universal helm slot
    private const string Body = "watan";      // body, 2×3, reqStr 12 — universal body slot
    private const string Club = "palica";     // blunt, 1×3 — NOT a bow: the whitelist probe

    private static GameSession Fresh(ulong seed = 11) => GameSession.New(seed, "koldun", TestData.DataDir());

    private static GameSession TownMerc(ulong seed = 11)
    {
        var s = Fresh(seed);
        s.Hire("strzelczyni");
        return s;
    }

    private static Item BuyOne(GameSession s, string baseId)
    {
        s.Buy(baseId);
        return s.Carried[^1];
    }

    private static (ulong Combat, ulong Drop, ulong Enemy) RngWords(GameSession s)
    {
        using var doc = JsonDocument.Parse(s.Dump());
        var r = doc.RootElement;
        return (r.GetProperty("combatRngState").GetUInt64(),
                r.GetProperty("dropRngState").GetUInt64(),
                r.GetProperty("enemyRngState").GetUInt64());
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"gear-{Guid.NewGuid():N}.save");

    // ---- GIVE — the weapon rides the merc; the hero cell lets go ----------------

    [Fact]
    public void Give_Weapon_FillsSlot_EmptiesHeroCell_PositionsSessionRead_NoRng()
    {
        var s = TownMerc();
        var bow = BuyOne(s, Bow);                       // fresh pack: first-fit anchors 0,0
        var before = RngWords(s);

        var r = s.GiveHirelingGear("weapon", 0, 0);
        Assert.Equal("weapon", r.Slot);
        Assert.Equal(Bow, r.Base);
        Assert.Equal("Soluga", r.Name);
        Assert.Equal((0, 0), (r.InvRow, r.InvCol));     // the cell it lifted from, session-read
        Assert.Same(bow, s.Merc!.Gear[MercGearSlot.Weapon]); // the SAME instance rides the merc
        Assert.Empty(s.Carried);                        // the hero cell is given up…
        Assert.True(s.Grid.CanPlace(bow, 0, 0));         // …and the pack cell stands free again
        Assert.Equal(before, RngWords(s));              // the door rolls NOTHING
        Assert.Equal(0, s.Drops.Pending);               // and moves no drop
    }

    // ---- THE 4-SLOT LAW — ring and amulet slots refused BY NAME -----------------

    [Fact]
    public void Give_RingSlot_Refused_The4SlotLawNamesIt()
    {
        var s = TownMerc();
        var bow = BuyOne(s, Bow);
        var e = Assert.Throws<SessionRejectedException>(() => s.GiveHirelingGear("ring", 0, 0));
        Assert.Contains("'ring'", e.Message);
        Assert.Contains("weapon, body, helm and shield", e.Message); // the four, named in the refusal
        Assert.Empty(s.Merc!.Gear);                     // nothing half-worn
        Assert.Same(bow, s.Carried[^1]);                // nothing moved
    }

    [Fact]
    public void Give_AmuletSlot_Refused_The4SlotLawNamesIt()
    {
        var s = TownMerc();
        BuyOne(s, Bow);
        var e = Assert.Throws<SessionRejectedException>(() => s.GiveHirelingGear("amulet", 0, 0));
        Assert.Contains("'amulet'", e.Message);
        Assert.Contains("weapon, body, helm and shield", e.Message);
        Assert.Empty(s.Merc!.Gear);
    }

    // ---- DEAD MERC — the amendment's alive gate, both directions ---------------

    [Fact]
    public void Give_ToDeadMerc_Refused_CorpseWearsNothing()
    {
        var s = TownMerc();
        var m = s.Merc!;
        m.TakeDamage(m.MaxLife);                        // fell in fight BEFORE any give
        Assert.True(m.IsDead);
        var bow = BuyOne(s, Bow);

        var e = Assert.Throws<SessionRejectedException>(() => s.GiveHirelingGear("weapon", 0, 0));
        Assert.Contains("dead", e.Message);             // amendment D2: the door requires it alive
        Assert.Empty(m.Gear);                           // the corpse received nothing
        Assert.Same(bow, s.Carried[^1]);
    }

    [Fact]
    public void Strip_DeadMerc_Refused_ACorpseIsNotStrippable()
    {
        var s = TownMerc();
        var m = s.Merc!;
        var bow = BuyOne(s, Bow);
        s.GiveHirelingGear("weapon", 0, 0);
        m.TakeDamage(m.MaxLife);                        // she fell WITH the bow on

        var e = Assert.Throws<SessionRejectedException>(() => s.StripHirelingGear("weapon"));
        Assert.Contains("dead", e.Message);             // amendment D2 verbatim: "You cannot take
                                                        // gear from a dead mercenary" (the take-back
                                                        // door opens only if the merc is ALIVE)
        Assert.Same(bow, m.Gear[MercGearSlot.Weapon]);  // the bow stayed ON the corpse
        Assert.Empty(s.Carried);                        // nothing was pulled off her
    }

    // ---- SAME-AREA DOOR (ORIGINAL DESIGN reading of the neighboring-room rule) --

    [Fact]
    public void SameArea_Door_StripFromFarSide_Refused_WalkedStripLands()
    {
        var s = TownMerc();
        var m = s.Merc!;
        var bow = BuyOne(s, Bow);
        s.GiveHirelingGear("weapon", 0, 0);
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");                             // the ALIVE merc follows (the mirror)
        KillHeroByCountersFar(s);                       // the law: she dies with him AT mchy
        s.Respawn();                                    // the stone raises HIM home, corpse behind
        s.Revive("strzelczyni");                        // the captain takes her alive again — AT mchy
        Assert.Equal("mchy", m.Area);                   // state reading: Revive unmoved her ground
        Assert.Equal(s.Areas.TownKey, s.CurrentArea);

        var e = Assert.Throws<SessionRejectedException>(() => s.StripHirelingGear("weapon"));
        Assert.Contains("mchy", e.Message);             // names both areas — the same-area door
        Assert.Contains("grodzisko", e.Message);
        Assert.Same(bow, m.Gear[MercGearSlot.Weapon]);  // the bow still rides her — nothing moved

        s.TravelTo("mchy");                             // WALK one area to reach her (mirror follows)
        var r = s.StripHirelingGear("weapon");          // now the door opens
        Assert.Equal("weapon", r.Slot);
        Assert.Equal("Soluga", r.Name);
        Assert.Same(bow, s.Carried[^1]);                // off her, into the hero's pack
        Assert.Empty(m.Gear);
    }

    /// The law trigger with the gear already worn: the merc must STAND until the
    /// hero's own death takes her (CombatDistance 4 — every counter reaches him).
    private static void KillHeroByCountersFar(GameSession s)
    {
        s.Merc!.CombatDistance = 4;
        if (s.Level < 5) s.LevelTo(5);
        int strikes = 0;
        for (; strikes < 2048 && !s.IsDead; strikes++) s.Attack("ponurak", 1);
        Assert.True(s.IsDead, "hero survived — no death transition");
    }

    // ---- EXCHANGE — the occupied slot returns the old weapon, or refuses -------

    [Fact]
    public void Give_OccupiedSlot_Exchanges_ReturnsOldWeaponToHeroPack()
    {
        var s = TownMerc();
        var first = BuyOne(s, Bow);
        s.GiveHirelingGear("weapon", 0, 0);
        var second = BuyOne(s, Bow);                    // pack was emptied: anchors 0,0 again

        var r = s.GiveHirelingGear("weapon", 0, 0);     // occupied slot -> EXCHANGE (T2)
        Assert.Equal("weapon", r.Slot);
        Assert.Equal((0, 0), (r.InvRow, r.InvCol));     // the new bow's anchor
        Assert.Equal(Bow, r.ExchangedBase);             // the displaced one, echoed
        Assert.Equal("Soluga", r.ExchangedName);
        Assert.Equal((0, 0), (r.ExchangedRow, r.ExchangedCol)); // its landing cell, session-read
        Assert.Same(second, s.Merc!.Gear[MercGearSlot.Weapon]);
        Assert.False(ReferenceEquals(first, s.Merc.Gear[MercGearSlot.Weapon]));
        Assert.Single(s.Carried);                       // the old weapon rides the HERO now
        Assert.Same(first, s.Carried[0]);
    }

    [Fact]
    public void Exchange_NoFreePackCell_Refused_SlotKeepsTheWornWeapon()
    {
        var s = TownMerc();
        var worn = BuyOne(s, Bow);
        s.GiveHirelingGear("weapon", 0, 0);             // the merc wears bow #1
        var fresh = BuyOne(s, Bow);                     // pack col 0, rows 0-3
        for (int i = 0; i < 9; i++) BuyOne(s, Club);    // 1x3 clubs fill cols 1..8 rows 0-2 + col 9
        // Free cells left: row 3 only — NO 1x4 anchor exists for the displaced bow.

        var e = Assert.Throws<SessionRejectedException>(() => s.GiveHirelingGear("weapon", 0, 0));
        Assert.Contains("free cell", e.Message);        // refused BY NAME — nothing half-applied
        Assert.Same(worn, s.Merc!.Gear[MercGearSlot.Weapon]); // the worn bow stayed on her
        Assert.True(s.Carried.Contains(fresh));         // the fresh bow stayed in the pack
        Assert.Equal(10, s.Carried.Count);              // 9 clubs + 1 bow, nothing moved
    }

    // ---- DISMISS FORFEITS — items die with the unit (no ground-drop concept) ----

    [Fact]
    public void Dismiss_ForfeitsWornGear_GoneFromEveryContainer_NoGroundDrop()
    {
        var s = TownMerc();
        var bow = BuyOne(s, Bow);
        s.GiveHirelingGear("weapon", 0, 0);
        long gold = s.Gold;
        int pending = s.Drops.Pending;

        s.Dismiss();

        Assert.Null(s.Merc);                            // the entry is gone
        Assert.DoesNotContain(bow, s.Carried);          // NOT handed back on dismiss
        Assert.Equal(pending, s.Drops.Pending);         // and NOT queued as a ground drop — the sim
                                                        // knows no drop-on-forfeit; the item dies with
                                                        // the unit (amendment D3, forfeit-forever)
        Assert.Equal(gold, s.Gold);                     // no gold moves either way
        using var doc = JsonDocument.Parse(s.Dump());
        Assert.True(doc.RootElement.GetProperty("merc").ValueKind
            is JsonValueKind.Null);                     // the dump proves it: no block, no gear
    }

    // ---- SAVE v7 — the gear rides; v6 refused BY NAME ---------------------------

    [Fact]
    public void SaveV7_RoundTrip_GearPresent_DumpByteIdentical()
    {
        var s = TownMerc();
        BuyOne(s, Bow);
        s.GiveHirelingGear("weapon", 0, 0);
        BuyOne(s, Helm);                                // 2x2 on the emptied pack: anchor 0,0
        s.GiveHirelingGear("helm", 0, 0);
        BuyOne(s, Body);
        s.GiveHirelingGear("body", 0, 0);
        Assert.Equal(3, s.Merc!.Gear.Count);
        string before = s.Dump();
        string path = TempPath();
        try
        {
            s.Save(path);
            var t = GameSession.Load(path, TestData.DataDir());
            Assert.Equal(7, SaveSerializer.SaveVersion);            // v7 = the gear block (row 66/S15)
            Assert.Equal(before, t.Dump());                         // byte-identical round trip
            Assert.Equal(Bow, t.Merc!.Gear[MercGearSlot.Weapon].Base.Id);
            Assert.Equal(Helm, t.Merc.Gear[MercGearSlot.Helm].Base.Id);
            Assert.Equal(Body, t.Merc.Gear[MercGearSlot.Body].Base.Id);
            Assert.False(t.Merc.Gear.ContainsKey(MercGearSlot.Shield)); // shield: unfilled (accepted by none)
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_V6HandCrafted_Refused_ByName()
    {
        var s = TownMerc();
        BuyOne(s, Bow);
        s.GiveHirelingGear("weapon", 0, 0);
        string untouched = s.Dump();
        string path = TempPath();
        try
        {
            s.Save(path);
            var text = File.ReadAllText(path)
                .Replace($"\"version\": {SaveSerializer.SaveVersion}", "\"version\": 6");
            Assert.NotEqual(text, File.ReadAllText(path));          // the downgrade landed
            File.WriteAllText(path, text);
            var e = Assert.Throws<InvalidDataException>(() => GameSession.Load(path, TestData.DataDir()));
            Assert.Contains("save version 6 unsupported", e.Message); // v6 refused BY NAME
            Assert.Equal(untouched, s.Dump());                       // the refusal touched nothing
        }
        finally { File.Delete(path); }
    }

    // ---- THROUGH DEATH AND REVIVE — the amendment's gear-keep rule --------------

    [Fact]
    public void HeroDeathLaw_GearStaysOnCorpse_ReviveOfSameMercReturnsIt()
    {
        var s = TownMerc();
        var m = s.Merc!;
        var bow = BuyOne(s, Bow);
        s.GiveHirelingGear("weapon", 0, 0);
        s.DiscoverWaypoint("mchy");
        s.TravelTo("mchy");                             // the bow rides the travel untouched
        KillHeroByCountersFar(s);                       // THE LAW (row 83): she died with him

        Assert.True(m.IsDead);
        Assert.Same(bow, m.Gear[MercGearSlot.Weapon]);  // gear STAYS ON the merc through death (D1)

        s.Respawn();
        s.Revive("strzelczyni");                        // the captain raises the SAME mercenary

        Assert.False(m.IsDead);
        Assert.Same(bow, m.Gear[MercGearSlot.Weapon]);  // WITH its gear — unconditional, same
                                                        // instance, NO gear-keep flag (D1: the
                                                        // revive never touches the inventory)
        Assert.Equal(Bow, JsonDocument.Parse(s.Dump()).RootElement
            .GetProperty("merc").GetProperty("gear")[0].GetProperty("item").GetProperty("base").GetString());
    }

    // ---- STATE READINGS — identified / requirements -----------------------------

    [Fact]
    public void ReqsAndIdentified_StateReadingsPinned()
    {
        // (a) NO Identified flag exists on the model — D2's IFLAG_IDENTIFIED half of
        //     the give-door has nothing to read; the gate is VACUOUS BY MODEL SHAPE
        //     (state reading pinned so the next reader does not "fix" a missing flag).
        Assert.Null(typeof(Item).GetProperty("Identified"));

        // (b) The level requirement EXISTS on Item and the merc HAS a level — enforced.
        var s = TownMerc();
        var bow = BuyOne(s, Bow);
        bow.LevelRequirement = 20;                      // the authoring door (public setter)
        var e = Assert.Throws<SessionRejectedException>(() => s.GiveHirelingGear("weapon", 0, 0));
        Assert.Contains("level 20", e.Message);
        Assert.Empty(s.Merc!.Gear);                     // the refusal applied nothing

        // (c) ReqStr/ReqDex EXIST on the item but the Hireling model carries NO Str/Dex
        //     stat (HirelingTable.StatsAt answers life/damage/AR only — state reading),
        //     so they have NOTHING to read against: soluga's reqDex 20 gates nothing and
        //     the plain bow is simply accepted (pinned here, commented at the door).
        var plain = BuyOne(s, Bow);                     // LevelRequirement 0 (white item), reqDex 20 — the REFUSED bow still holds cell 0,0
        Assert.Equal(20, plain.Base.ReqDex);            // the base's own witness
        s.GiveHirelingGear("weapon", 0, 1);             // accepted — no merc-side stat exists
        Assert.Same(plain, s.Merc.Gear[MercGearSlot.Weapon]);
    }

    // ---- THE DATA WHITELIST — weapons by kind (hirelings.json gear) -------------

    [Fact]
    public void WeaponWhitelist_Strzelczyni_RefusesNonBow_NamingHerTypes()
    {
        var s = TownMerc();
        var club = BuyOne(s, Club);                     // blunt — wearable by nobody on this roster
        var e = Assert.Throws<SessionRejectedException>(() => s.GiveHirelingGear("weapon", 0, 0));
        Assert.Contains("bows", e.Message);             // the data row's whitelist, echoed
        Assert.Empty(s.Merc!.Gear);
        Assert.Contains(club, s.Carried);
    }

    [Fact]
    public void ShieldSlot_InTheModel_AcceptedByNoKindOnThisRoster()
    {
        // D2's shield-bearer was the Act-3 hire; this roster (bow/spear/axe-thrower)
        // accepts NONE — the slot stands in the engine (4-slot law) and the data rows
        // name no shield acceptance, so the door refuses the slot by name.
        var s = TownMerc();
        var e = Assert.Throws<SessionRejectedException>(() => s.GiveHirelingGear("shield", 0, 0));
        Assert.Contains("shield", e.Message);
        Assert.DoesNotContain("shield", s.Hirelings.For("strzelczyni").Gear.Slots);
    }

    [Fact]
    public void Rzutca_AxeWhitelist_DataFenced_OriginalDesign()
    {
        // ORIGINAL DESIGN, fenced: D2 ships NO throwing hireling — the nearest D2
        // acceptance shape is the Act-5 one-handed AXE marshal (amendment §3/§4,
        // "owner may re-rule"). The fence rides the DATA row (this file owns
        // data/hirelings.json); the engine stays dumb to the kind.
        var s = Fresh();
        var rzutca = s.Hirelings.For("rzutca").Gear;
        Assert.Equal(["weapon", "body", "helm"], rzutca.Slots);
        Assert.Equal(["axes"], rzutca.WeaponTypes);
        Assert.Equal(["bows"], s.Hirelings.For("strzelczyni").Gear.WeaponTypes);
        Assert.Equal(["spears", "polearms"], s.Hirelings.For("witez").Gear.WeaponTypes); // javelin RETIRED (S4a)
    }

    // ---- THE CLI DOORS — give/strip verbs, the hire GEAR echo --------------------

    public const ulong CliSeed = 7;

    private static (int Exit, List<string> Lines) RunScript(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"gear-cli-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, body);
        try
        {
            var output = new StringWriter();
            int exit = CommandRunner.Run(path, CliSeed, TestData.DataDir(), output);
            return (exit, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList());
        }
        finally { File.Delete(path); }
    }

    private static IEnumerable<JsonNode> Parsed(List<string> lines) => lines.Select(l => JsonNode.Parse(l)!);
    private static JsonNode Node(List<string> lines, string cmd) =>
        Parsed(lines).First(n => n["CMD"]?.GetValue<string>() == cmd);

    [Fact]
    public void Cli_GiveStripRoundTrip_HireEchoesFourSlots()
    {
        var (exit, lines) = RunScript(
            "new koldun\nhire strzelczyni\nbuy soluga\ngive weapon 0 0\ndump\nstrip weapon\n");
        Assert.Equal(0, exit);

        var hire = Node(lines, "hire");
        Assert.Equal("weapon=-,body=-,helm=-,shield=-", hire["GEAR"]!.GetValue<string>()); // the four slots, empty

        var give = Node(lines, "give");
        Assert.Equal("weapon", give["SLOT"]!.GetValue<string>());
        Assert.Equal("soluga", give["BASE"]!.GetValue<string>());
        Assert.Equal("Soluga", give["ITEM"]!.GetValue<string>());
        Assert.Equal(0, give["INV_ROW"]!.GetValue<int>());
        Assert.Equal(0, give["INV_COL"]!.GetValue<int>());

        var gear = Node(lines, "dump")["STATE"]!["merc"]!["gear"]!;
        Assert.Equal("Weapon", gear[0]!["slot"]!.GetValue<string>()); // the v7 block, slot-named
        Assert.Equal("soluga", gear[0]!["item"]!["base"]!.GetValue<string>());

        var strip = Node(lines, "strip");
        Assert.Equal("Soluga", strip["ITEM"]!.GetValue<string>());
        Assert.Equal(0, strip["INV_ROW"]!.GetValue<int>());   // first-fit back into the freed cell
    }

    [Fact]
    public void Cli_GiveRing_PrintsTheEnginesNamedError_AndExits1()
    {
        var (exit, lines) = RunScript("new koldun\nhire strzelczyni\nbuy soluga\ngive ring 0 0\n");
        Assert.Equal(1, exit);
        Assert.Contains("'ring'", JsonNode.Parse(lines[^1])!["ERROR"]!.GetValue<string>());
        Assert.DoesNotContain(lines, l => l.Contains("RESULT"));
    }
}
