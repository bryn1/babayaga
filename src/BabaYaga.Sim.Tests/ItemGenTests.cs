namespace BabaYaga.Sim.Tests;

/// <summary>
/// The M2 item-generation contract: the §4.1 quality roll (c-formula lines, classic
/// ratio rows, MF quality-only lift, deterministic seeds), §4.2 ilvl/alvl math,
/// base-table gating, and (later in this file) sockets, runewords and the grid.
/// </summary>
public class ItemGenTests
{
    private static ItemBaseTable Bases() =>
        ItemBaseTable.Load(Path.Combine(TestData.DataDir(), "item-bases.json"));

    // --- §4.1 c-formula, exact rows ---

    [Fact]
    public void CValue_MatchesTheFourSpecLines()
    {
        var magic = QualityRoll.Classic.Single(r => r.Tier == "Magic");   // 30 / 1
        var unique = QualityRoll.Classic.Single(r => r.Tier == "Unique"); // 400 / 2
        Assert.Equal(30 * 128, QualityRoll.CValue(magic, ilvl: 10, qlvl: 10, magicFind: 0, tcFactor: 0));
        // MF 100% halves c: floor(3840 × 100/200) = 1920.
        Assert.Equal(1920, QualityRoll.CValue(magic, 10, 10, magicFind: 100, tcFactor: 0));
        // ilvl 40 qlvl 5 unique: (400 − (35/2=17)) × 128 = 383 × 128.
        Assert.Equal(383 * 128, QualityRoll.CValue(unique, 40, 5, 0, 0));
        // TC quality factor 512 removes half of c.
        Assert.Equal(1920, QualityRoll.CValue(magic, 10, 10, 0, tcFactor: 512));
        // ilvl below qlvl divides toward zero: (ilvl 5 − qlvl 20) = −15 → ratio grows to 45.
        Assert.Equal(45 * 128, QualityRoll.CValue(magic, 5, 20, 0, 0));
    }

    [Fact]
    public void QualityRoll_SameSeed_IdenticalRaritySequence()
    {
        static List<ItemQuality> Run(ulong seed)
        {
            var table = ItemBaseTable.Load(Path.Combine(TestData.DataDir(), "item-bases.json"));
            var rng = new Rng(seed);
            var list = new List<ItemQuality>();
            for (int i = 0; i < 60; i++)
                list.Add(QualityRoll.Roll(rng, table.For("szpula"), ilvl: 20 + i % 40, magicFind: 50 + i));
            return list;
        }
        Assert.Equal(Run(7UL), Run(7UL));               // same seed → identical sequence
        Assert.NotEqual(Run(7UL), Run(11UL));           // different seeds → different streams
    }

    [Fact]
    public void QualityRoll_MagicFind_LiftsMagicWithoutTouchingQuantity()
    {
        var szpula = Bases().For("szpula");
        int At(int mf)
        {
            var rng = new Rng(3UL);
            int magicPlus = 0;
            for (int i = 0; i < 2000; i++)
                if (QualityRoll.Roll(rng, szpula, ilvl: 10, magicFind: mf) >= ItemQuality.Magic) magicPlus++;
            return magicPlus;
        }
        int plain = At(0);
        int found = At(500); // c 3840 → 256: ~50% vs ~3%
        Assert.True(found > plain * 3, $"MF 500 should lift magic-plus: {plain} → {found}");
    }

    [Fact]
    public void QualityRoll_AutoSuccess_WhenIlvlOutrunsQlvl()
    {
        var szpula = Bases().For("szpula"); // qlvl 5 → ilvl 40: magic row c = (30−35)×128 < 0
        var rng = new Rng(5UL);
        for (int i = 0; i < 25; i++)
            Assert.Equal(ItemQuality.Magic, QualityRoll.Roll(rng, szpula, ilvl: 40)); // no rows → magic guaranteed
    }

    [Fact]
    public void QualityRoll_GemBases_NeverRollPastNormal()
    {
        var amber = Bases().For("jantar"); // qualityRolls=false tier gate (P2 §4.1)
        var rng = new Rng(9UL);
        for (int i = 0; i < 25; i++)
            Assert.Equal(ItemQuality.Normal, QualityRoll.Roll(rng, amber, ilvl: 60, magicFind: 9999,
                uniqueCandidateExists: true, setCandidateExists: true));
    }

    // --- §4.2 ilvl + alvl ---

    [Fact]
    public void Ilvl_Sources_DropChampionUniqueCube()
    {
        Assert.Equal(30, ItemLevels.FromKill(30, ItemLevels.KillKind.Drop));
        Assert.Equal(32, ItemLevels.FromKill(30, ItemLevels.KillKind.Champion));
        Assert.Equal(33, ItemLevels.FromKill(30, ItemLevels.KillKind.Unique));
        Assert.Equal(99, ItemLevels.FromKill(98, ItemLevels.KillKind.Unique));      // clamped
        Assert.Equal(42, ItemLevels.FromCube(fixedLvl: 42, 50, 40, 50, 30));
        Assert.Equal(35, ItemLevels.FromCube(-1, 50, 40, 50, 30));                  // 20 + 15
        Assert.Equal(99, ItemLevels.FromCube(-1, 90, 99, 90, 99));                  // clamped
    }

    [Fact]
    public void Alvl_ClassicBand_SteepBand_MagicLevelAdd()
    {
        Assert.Equal(21, ItemLevels.AffixLevel(ilvl: 26, qlvl: 10, magicLevelAdd: false)); // 26 − 5
        Assert.Equal(85, ItemLevels.AffixLevel(90, 10, false));   // L 90 < 94 → 90 − 5
        Assert.Equal(99, ItemLevels.AffixLevel(99, 20, false));   // L 99 ≥ 89 → 2×99 − 99
        Assert.Equal(97, ItemLevels.AffixLevel(98, 2, false));    // L 98 ≥ 98 → 2×98 − 99
        Assert.Equal(40, ItemLevels.AffixLevel(40, 10, magicLevelAdd: true)); // direct L
        Assert.Equal(2, ItemLevels.AffixLevel(1, 3, false));       // L 3 − 1, above the 1-floor
    }

    [Fact]
    public void RareTier_GatedBelowIlvl27_MagicStillRolls()
    {
        var szpula = Bases().For("szpula");
        var rng = new Rng(13UL);
        for (int i = 0; i < 200; i++)
        {
            var q = QualityRoll.Roll(rng, szpula, ilvl: 20);
            Assert.True(q is ItemQuality.LowQuality or ItemQuality.Normal
                        or ItemQuality.Superior or ItemQuality.Magic,
                $"ilvl 20 must not reach the rare tier, got {q}");
        }
    }

    [Fact]
    public void WhiteSplit_SuperiorOnlyAboveQlvl_EqualQlvlIsAlwaysNormal()
    {
        var szpula = Bases().For("szpula"); // qlvl 5
        var rng = new Rng(17UL);
        Assert.Contains(ItemQuality.Superior,
            Enumerable.Range(0, 500).Select(_ => QualityRoll.RollWhite(rng, szpula, ilvl: 30)));
        Assert.All(Enumerable.Range(0, 200), _ =>
            Assert.Equal(ItemQuality.Normal, QualityRoll.RollWhite(rng, szpula, ilvl: 5))); // gap 0
    }

    // --- base table ---

    [Fact]
    public void BaseTable_LoadsAndValidates_OriginalContentOnly()
    {
        var table = Bases();
        Assert.Equal(19, table.Stock.Count);                    // quality-rolling bases
        Assert.Equal(3, table.Bases.Count(b => !b.QualityRolls)); // the three gems
        Assert.True(table.For("postak").MagicLevelAdd);          // staves roll at L
        Assert.Equal("Kugla", table.For("kugla").Name);          // ORIGINAL IP row
        Assert.Equal(1, table.For("pierscien").MaxSockets);       // ring: one cell, one socket
        Assert.Throws<InvalidDataException>(() => ItemBaseTable.Load(
            WriteTempBase("duplicate", "dup", "dup")));          // content gate goes red
    }

    /// <summary>Writes a minimal base-table file whose two rows share an id — the
    /// loader's duplicate gate must reject it (planted-bad for the content gate).</summary>
    private static string WriteTempBase(string a, string b, string c)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bases-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            { "bases": [
              { "id": "x", "name": "A", "itype": "swords", "qlvl": 1, "cost": 1, "durability": 2, "w": 1, "h": 3, "maxSockets": 1, "reqStr": 0, "reqDex": 0, "equiv": ["sword"] },
              { "id": "x", "name": "B", "itype": "swords", "qlvl": 1, "cost": 1, "durability": 2, "w": 1, "h": 3, "maxSockets": 1, "reqStr": 0, "reqDex": 0, "equiv": ["sword"] }
            ] }
            """);
        return path;
    }

    // --- §4.5 sockets ---

    [Fact]
    public void Sockets_RespectTypeBand_BaseCeiling_And_SizeCap()
    {
        var table = Bases();
        var szyszak = table.For("szyszak");   // helms band 2/2/3, base 3, 2x2 cells
        var zgunia = table.For("zgunia");     // body band 3/4/6, base 5, 2x3 cells
        var ring = table.For("pierscien");    // ring 1x1 cell → size cap 1
        var postak = table.For("postak");     // staves 5/6/6, base 6, 2x4 cells → cap 6
        Assert.Equal(2, Socketing.MaxSockets(szyszak, ilvl: 10));
        Assert.Equal(4, Socketing.MaxSockets(zgunia, ilvl: 30));
        Assert.Equal(5, Socketing.MaxSockets(zgunia, ilvl: 50)); // base 5 under the band 6
        Assert.Equal(1, Socketing.MaxSockets(ring, ilvl: 90));   // size caps below the band
        Assert.Equal(6, Socketing.MaxSockets(postak, ilvl: 50));

        var rng = new Rng(19UL);
        var rolled = Enumerable.Range(0, 400).Select(_ => Socketing.Roll(rng, zgunia, 50)).ToList();
        Assert.All(rolled, s => Assert.InRange(s, 0, 5));
        Assert.Contains(5, rolled);
        Assert.All(Enumerable.Range(0, 50), _ =>
            Assert.Equal(0, Socketing.Roll(rng, table.For("jantar"), 60))); // gems never socket
    }

    // --- §4.5 runewords: all five conditions ---

    private static RunewordTable Words() => RunewordTable.Load(
        Path.Combine(TestData.DataDir(), "runes.json"),
        Path.Combine(TestData.DataDir(), "runewords.json"));

    private static Item WhiteWithStones(ItemBaseRow b, params string[] stones)
    {
        var item = new Item(b, 45) { Sockets = stones.Length };
        item.SocketedRunes.AddRange(stones);
        return item;
    }

    [Fact]
    public void Runeword_ExactOrderAndCount_WhiteOnly_AvailabilityGate()
    {
        var words = Words();
        var kugla = Bases().For("kugla"); // orbs: Morana's itype list names "staves"+"orbs"
        var morana = words.Words.Single(w => w.Id == "morana");

        // cond. 1+2+3+4+5 hold → binds
        var good = WhiteWithStones(kugla, "lugove", "zelo");
        Assert.NotNull(words.TryBind(new Rng(1UL), good));
        Assert.Equal("Morana", good.RunewordId);

        // cond. 4: wrong order must not bind
        var wrongOrder = WhiteWithStones(kugla, "zelo", "lugove");
        Assert.Null(words.TryBind(new Rng(2UL), wrongOrder));

        // cond. 3: an extra socket cell (count mismatch) must not bind
        var extraSocket = WhiteWithStones(kugla, "lugove", "zelo");
        extraSocket.Sockets = 3;
        Assert.False(RunewordTable.ConditionsMet(extraSocket, morana));

        // cond. 2: magic quality must not bind
        var magic = WhiteWithStones(kugla, "lugove", "zelo");
        magic.Quality = ItemQuality.Magic;
        Assert.False(RunewordTable.ConditionsMet(magic, morana));

        // cond. 5: Zorza exists but is unavailable on this ladder
        var zorza = words.Words.Single(w => w.Id == "zorza");
        var helm = WhiteWithStones(Bases().For("szyszak"), "glagoli", "buky", "buky");
        Assert.False(RunewordTable.ConditionsMet(helm, zorza));
    }

    [Fact]
    public void Runeword_TypeCondition_DrawsOnTheEquivChain()
    {
        var words = Words();
        var kolo = words.Words.Single(w => w.Id == "kolo"); // names "swords" (plural)
        var szpula = Bases().For("szpula");                  // equiv carries "sword" (singular)
        Assert.True(RunewordTable.TypeMatches(WhiteWithStones(szpula), kolo));
        var obereg = Bases().For("obereg");
        Assert.False(RunewordTable.TypeMatches(WhiteWithStones(obereg), kolo));
    }

    // --- §4.4 uniques & sets ---

    private static UniqueSetTable Uniques() =>
        UniqueSetTable.Load(Path.Combine(TestData.DataDir(), "uniques.json"));

    [Fact]
    public void Unique_LvlGateAndWeighting()
    {
        var table = Uniques();
        var postak = Bases().For("postak"); // two unique rows: lvl 18 and lvl 25
        Assert.False(table.UniqueCandidate("postak", ilvl: 10));
        Assert.True(table.UniqueCandidate("postak", ilvl: 18));
        var rng = new Rng(29UL);
        for (int i = 0; i < 60; i++)
            Assert.Equal("The Hut's Chicken Leg", table.BuildUnique(rng, postak, 18)!.UniqueId); // lvl-25 row still gated
        Assert.Contains(
            Enumerable.Range(0, 60).Select(_ => table.BuildUnique(rng, postak, 40)!.UniqueId),
            n => n == "Mokosh's Distaff"); // past the gate the rarer row appears
        Assert.Equal(60, UniqueSetTable.Weight(0));
        Assert.Equal(20, UniqueSetTable.Weight(2)); // 60/(rarity+1)
    }

    [Fact]
    public void Unique_ModsRollWithinTheirDeclaredRanges()
    {
        var table = Uniques();
        var byId = table.Rows.ToDictionary(r => r.Id);
        var rng = new Rng(31UL);
        for (int i = 0; i < 80; i++)
        {
            var item = table.BuildUnique(rng, Bases().For("palica"), 40)!;
            Assert.Equal("Kosc's Millstone", item.UniqueId);
            var def = byId["kosc_millstone"];
            foreach (var mod in item.Mods)
            {
                var r = def.Mods.Single(m => m.Stat == mod.Stat);
                Assert.InRange(mod.Value, r.Min, r.Max);
            }
        }
    }

    [Fact]
    public void Set_CountBonusTiers_StackByEquippedPieces()
    {
        var table = Uniques();
        Assert.Empty(table.SetBonusMods("morana", 1));
        Assert.Single(table.SetBonusMods("morana", 2));                       // 2-piece tier only
        Assert.Equal(2, table.SetBonusMods("morana", 3).Count);               // tiers stack
        var four = table.SetBonusMods("morana", 4);
        Assert.Contains(four, m => m.Stat == "life" && m.Value == 60);         // 4-piece tier in
        Assert.Contains(table.SetBonusMods("kupalo", 3), m => m.Stat == "fire_res");

        var panel = new EquipmentPanel();
        var rng = new Rng(37UL);
        var veil = table.BuildSetPiece(rng, Bases().For("szyszak"), 40)!;
        var shroud = table.BuildSetPiece(rng, Bases().For("opaszka"), 40)!;
        Assert.Equal("Morana's Veil", veil.UniqueId);       // the piece's own display name
        Assert.Equal("morana", veil.SetId);                 // and the set membership key
        Assert.True(panel.Equip(veil, EquipSlot.Head));
        Assert.True(panel.Equip(shroud, EquipSlot.Body));
        Assert.Equal(2, panel.EquippedSetPieces("morana")); // worn-piece count feeds the bonus tiers
    }

    // --- P3 §7 grid + equipment panel ---

    [Fact]
    public void Inventory_FourByTenGrid_WxHPlacement()
    {
        var table = Bases();
        var grid = new InventoryGrid();
        Assert.Equal(4 * 10, grid.FreeCells);
        var watan = new Item(table.For("watan"), 10);   // 2 wide × 3 tall
        var ring = new Item(table.For("pierscien"), 10); // 1×1
        Assert.True(grid.TryPlace(watan, row: 1, col: 8));   // 2 cols to the right edge, rows 1..3
        Assert.False(grid.TryPlace(new Item(table.For("watan"), 10), 1, 8)); // same cells taken
        Assert.False(grid.TryPlace(ring, row: 3, col: 10));  // off the grid
        Assert.True(grid.TryPlace(ring, 0, 9));
        Assert.False(grid.TryPlace(ring, 0, 8));             // already placed
        Assert.Equal(40 - 6 - 1, grid.FreeCells);
        Assert.True(grid.Remove(ring));
        Assert.Equal(34, grid.FreeCells);
        Assert.True(grid.TryPlace(new Item(table.For("lapcie"), 10), 0, 0)); // 2×2 into freed space
        Assert.Equal(30, grid.FreeCells);
    }

    [Fact]
    public void Equipment_PanelRejectsWrongSlot()
    {
        var table = Bases();
        var panel = new EquipmentPanel();
        Assert.True(panel.Equip(new Item(table.For("pierscien"), 10), EquipSlot.RingRight));
        Assert.False(panel.Equip(new Item(table.For("watan"), 10), EquipSlot.Head));   // body ≠ head
        Assert.False(panel.Equip(new Item(table.For("jantar"), 10), EquipSlot.Body));  // gems never worn
        Assert.True(panel.Equip(new Item(table.For("watan"), 10), EquipSlot.Body));
        Assert.True(panel.Equip(new Item(table.For("tarcza"), 10), EquipSlot.Offhand));
        Assert.False(panel.Equip(new Item(table.For("szpula"), 10), EquipSlot.Offhand)); // sword ≠ offhand
        Assert.Equal(3, panel.Equipped.Count);
        Assert.True(panel.Unequip(EquipSlot.Body));
        Assert.Null(panel[EquipSlot.Body]);
    }
}
