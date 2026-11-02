using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BabaYaga.Sim;
using BabaYaga.Sim.Cli;
using Xunit;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// MC 11952 HERO CELL-EQUIP — the cell-addressable equip door EquipFromCell +
/// its CLI twin "equip &lt;x&gt; &lt;y&gt;" + the windowed anchor-cell click that rides
/// them. FENCE (the declared control-scheme translation): D2 equips via cursor
/// DRAG; this clone is command-driven with NO cursor state (ARCH law), so a
/// grid-cell click naming the item to wear is the translation — the GATES per
/// the oracle stay exactly where the engine has them. Slot legality rides the
/// existing EquipSlots.For mapping (row 68, zero new rules); the occupied-slot
/// EXCHANGE law mirrors the merc precedent (GameSession.HirelingGear.cs: the
/// displaced item rides a free pack cell, a pack without one refuses BY NAME;
/// out/T0b-mechanics-spec-3.md §6.7b T2) — the same-area gate is NOT
/// applicable, the hero wears on himself (state reading, door header).
/// THE MAX-LIFE ROW authors Item.Mods through the public list (the
/// HirelingStatTests idiom): worn "+Life" raises MaxLife by the flat term,
/// "+Vit" by LifePerVit × the term (P1 §1.3), while the CURRENT Life pool —
/// damage state — stays (D2; the LifeItemTermTests law). The door touches NO
/// RNG stream (zero-rolls rows) and the save needs no bump (v7 — worn block +
/// grid already persist, round-trip row).
/// CLI twin rows: the EngineDoorVerbs law — thin call, every gate the ENGINE'S,
/// refusals arriving as the named ERROR line + exit 1; 'equip lastdrop' keeps
/// working untouched (anchor journey byte-law). Windowed rows: the pure
/// InventoryPanelLogic door-line + node-name table replayed LIVE through the
/// real CommandRunner (the TownPanelTests mapping idiom), and the shipped-file
/// SCAN twins — the panel's door call rides CommandLog.Call, its refusals
/// paint the engine's own words, and the evidence leg calls NO session verb.
/// RED-FIRST: on the base this file does not COMPILE — the door does not exist
/// there (CS0117 EquipFromCell; the HirelingGearTests precedent).
/// </summary>
public class HeroEquipCellTests
{
    private const string Bow = "soluga";   // bows, 1×4 — the Weapon row
    private const string Helm = "szyszak"; // helms, 2×2 — the Head row
    private const string Club = "palica";  // blunt, 1×3 — the pack filler
    private const string Potion = "napitek-zycia"; // potions — never wearable

    private static GameSession Fresh(ulong seed = 3) => GameSession.New(seed, "koldun", TestData.DataDir());

    private static Item BuyOne(GameSession s, string baseId)
    {
        s.Buy(baseId);
        return s.Carried[^1];
    }

    // ---- GREEN happy path: the slot fills via the EXISTING legality table ----

    [Fact]
    public void EquipFromCell_Bow_FillsWeaponSlot_ByAnchor_LeavesThePack()
    {
        var s = Fresh();
        var bow = BuyOne(s, Bow); // first-fit anchor: (0,0)
        var r = s.EquipFromCell(0, 0);
        Assert.Equal("Weapon", r.Slot);
        Assert.Equal(Bow, r.Base);
        Assert.Equal("Soluga", r.Name);
        Assert.Equal((0, 0), (r.InvRow, r.InvCol));
        Assert.Null(r.ExchangedBase); // nothing worn before — a plain wear, not an exchange
        Assert.Same(bow, s.Equipment[EquipSlot.Weapon]);
        Assert.Empty(s.Carried);
        Assert.Equal(0, s.Grid.PlacedCount);
        // EquipLastDrop's own law mirrored: the equipped item stays the last looted.
        Assert.Same(bow, s.LastLooted);
    }

    [Fact]
    public void EquipFromCell_Helm_AnchorsAtItsOwnCell_FillsHead_WeaponStaysFree()
    {
        var s = Fresh();
        BuyOne(s, Bow);               // 1×4 fills col 0 rows 0-3
        var helm = BuyOne(s, Helm);   // 2×2 first-fits at (0,1)
        var r = s.EquipFromCell(0, 1);
        Assert.Equal("Head", r.Slot);
        Assert.Same(helm, s.Equipment[EquipSlot.Head]);
        Assert.Empty(s.Equipment.Equipped.Where(kv => kv.Key == EquipSlot.Weapon));
        Assert.Single(s.Carried); // the bow still rides (0,0)
    }

    // ---- DERIVED MAX moves; the CURRENT pool is damage state and stays ----

    [Fact]
    public void EquipFromCell_LifeAndVitAffixes_RaiseMaxLife_CurrentLifeStays()
    {
        var s = Fresh();
        var bow = BuyOne(s, Bow);
        bow.Mods.Add(new ItemMod("life", 10)); // the public authoring list (HirelingStatTests idiom)
        bow.Mods.Add(new ItemMod("vit", 3));   // koldun LifePerVit = 2 → +6 on top
        long maxBefore = s.Character.MaxLife, lifeBefore = s.Character.Life;
        s.EquipFromCell(0, 0);
        Assert.Equal(maxBefore + 10 + 2 * 3, s.Character.MaxLife); // flat INSIDE, Vit AFTER (P1 §1.3)
        Assert.Equal(lifeBefore, s.Character.Life);                // +max does not heal (D2)
    }

    // ---- refusals, each the engine's named line, nothing half-applied ----

    [Fact]
    public void EquipFromCell_WrongKind_RefusedByName_NothingMoves()
    {
        var s = Fresh();
        var potion = BuyOne(s, Potion);
        var e = Assert.Throws<SessionRejectedException>(() => s.EquipFromCell(0, 0));
        Assert.Contains("is not wearable", e.Message);
        Assert.Contains("Napitek Zycia", e.Message); // BY NAME (the EquipLastDrop message mirrored)
        Assert.Same(potion, s.Carried[0]);
        Assert.Empty(s.Equipment.Equipped);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(4, 0)]  // Rows = 4 — the first off-grid row
    [InlineData(0, 10)] // Columns = 10
    public void EquipFromCell_OffGridCell_RefusedWithTheCellNamed(int x, int y)
    {
        var s = Fresh();
        BuyOne(s, Bow);
        var e = Assert.Throws<SessionRejectedException>(() => s.EquipFromCell(x, y));
        Assert.Contains($"inventory cell {x},{y} is outside the pack grid", e.Message); // the give door's line, verbatim
        Assert.Single(s.Carried);
    }

    [Fact]
    public void EquipFromCell_EmptyCell_RefusedWithTheCellNamed()
    {
        var s = Fresh();
        BuyOne(s, Bow); // only col 0 rides; (0,1) is free
        var e = Assert.Throws<SessionRejectedException>(() => s.EquipFromCell(0, 1));
        Assert.Contains("no item anchored at cell 0,1", e.Message);
    }

    [Fact]
    public void EquipFromCell_DeadHero_RefusedFirst_TheGateSpeaksBeforeTheCell()
    {
        var s = Fresh();
        BuyOne(s, Bow);
        s.Character.TakeDamage(long.MaxValue); // the authoring door (row-64 doctrine)
        Assert.True(s.IsDead);
        var e = Assert.Throws<SessionRejectedException>(() => s.EquipFromCell(0, 0));
        Assert.Equal("you are dead", e.Message); // the ONE RaiseIfDead wording
        var off = Assert.Throws<SessionRejectedException>(() => s.EquipFromCell(9, 9));
        Assert.Equal("you are dead", off.Message); // DEAD first even beside an invalid cell
    }

    // ---- EXCHANGE — the §6.7b T2 law, the merc precedent mirrored ----

    [Fact]
    public void EquipFromCell_OccupiedSlot_Exchanges_DisplacedRidesAFreeCell()
    {
        var s = Fresh();
        var first = BuyOne(s, Bow);
        s.EquipFromCell(0, 0);
        var second = BuyOne(s, Bow); // the pack emptied: anchored (0,0) again
        var r = s.EquipFromCell(0, 0);
        Assert.Equal("Weapon", r.Slot);
        Assert.Equal(Bow, r.ExchangedBase);
        Assert.Equal("Soluga", r.ExchangedName);
        Assert.Equal((0, 0), (r.ExchangedRow, r.ExchangedCol)); // its landing cell, session-read
        Assert.Same(second, s.Equipment[EquipSlot.Weapon]);
        Assert.Single(s.Carried);
        Assert.Same(first, s.Carried[0]); // the displaced one rides the HERO's pack (T2)
    }

    [Fact]
    public void EquipFromCell_ExchangeWithoutRoom_RefusedByName_NothingHalfApplied()
    {
        var s = Fresh();
        var worn = BuyOne(s, Bow);
        s.EquipFromCell(0, 0);                       // the hero wears bow #1
        var fresh = BuyOne(s, Bow);                  // pack col 0, rows 0-3
        for (int i = 0; i < 9; i++) BuyOne(s, Club); // 1×3 clubs fill cols 1..9 — row 3 stays free
        // Free cells left: row 3 only — NO 1×4 anchor exists for the displaced bow.
        var e = Assert.Throws<SessionRejectedException>(() => s.EquipFromCell(0, 0));
        Assert.Contains("free cell", e.Message);     // refused BY NAME (the give door's wording)
        Assert.Contains("Soluga", e.Message);         // the displaced item itself, named
        Assert.Same(worn, s.Equipment[EquipSlot.Weapon]); // the worn bow stayed on
        Assert.Contains(fresh, s.Carried);           // the fresh bow stayed in the pack
        Assert.Equal(10, s.Carried.Count);           // 9 clubs + 1 bow — nothing moved
    }

    // ---- RNG law: the door rolls NOTHING; save v7 unchanged ----

    private static (ulong Combat, ulong Drop, ulong Enemy) RngWords(GameSession s)
    {
        using var doc = JsonDocument.Parse(s.Dump());
        var r = doc.RootElement;
        return (r.GetProperty("combatRngState").GetUInt64(),
                r.GetProperty("dropRngState").GetUInt64(),
                r.GetProperty("enemyRngState").GetUInt64());
    }

    [Fact]
    public void EquipFromCell_ZeroRolls_RngWordsIdentical_ThroughEquipAndExchange()
    {
        var s = Fresh(42);
        BuyOne(s, Bow);
        var before = RngWords(s);
        s.EquipFromCell(0, 0);
        BuyOne(s, Bow);
        s.EquipFromCell(0, 0); // the exchange path too
        Assert.Equal(before, RngWords(s)); // all three streams untouched
    }

    [Fact]
    public void EquipFromCell_SaveV7_RoundTrips_DumpByteIdentical()
    {
        var s = Fresh(5);
        BuyOne(s, Bow);
        s.EquipFromCell(0, 0);
        var path = Path.Combine(Path.GetTempPath(), $"heroeq-{Guid.NewGuid():N}.save");
        try
        {
            s.Save(path);
            var loaded = GameSession.Load(path, TestData.DataDir());
            Assert.NotNull(loaded.Equipment[EquipSlot.Weapon]); // the worn block persisted (v7 format, no bump)
            Assert.Empty(loaded.Carried);
            Assert.Equal(JsonDocument.Parse(s.Dump()).RootElement.ToString(),
                         JsonDocument.Parse(loaded.Dump()).RootElement.ToString());
        }
        finally { File.Delete(path); }
    }

    // ---- CLI twin: the EngineDoorVerbs law — thin call, named ERROR, exit 1 ----

    private static (int Exit, List<string> Lines) RunScript(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"heroeq-cli-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, body);
        try
        {
            var output = new StringWriter();
            int exit = CommandRunner.Run(path, 7, TestData.DataDir(), output);
            return (exit, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CliEquipCell_EchoesTheDoorReport_AndRollsNoRng()
    {
        var (exit, lines) = RunScript("new koldun\nbuy soluga\ndump\nequip 0 0\ndump\n");
        Assert.Equal(0, exit);
        Assert.DoesNotContain(lines, l => l.Contains("\"ERROR\""));
        var echo = lines.Select(l => JsonNode.Parse(l)!).First(n => n!["CMD"]?.GetValue<string>() == "equip")!;
        Assert.Equal("Weapon", echo["SLOT"]!.GetValue<string>());
        Assert.Equal("soluga", echo["BASE"]!.GetValue<string>());
        Assert.Equal("Soluga", echo["ITEM"]!.GetValue<string>());
        Assert.Equal(0, echo["INV_ROW"]!.GetValue<int>());
        Assert.Equal(0, echo["INV_COL"]!.GetValue<int>());
        Assert.True(!echo.AsObject().ContainsKey("EXCHANGED")); // plain wear
        var dumps = lines.Select(l => JsonNode.Parse(l)!)
            .Where(n => n!["CMD"]?.GetValue<string>() == "dump").ToList();
        string Words(JsonNode d) => d!["STATE"]!["combatRngState"]!.ToString() + d!["STATE"]!["dropRngState"];
        Assert.Equal(Words(dumps[0]), Words(dumps[1])); // the twin consumed zero rolls
    }

    [Fact]
    public void CliEquipCell_ExchangeEchoesTheDisplacedItem()
    {
        var (exit, lines) = RunScript("new koldun\nbuy soluga\nequip 0 0\nbuy soluga\nequip 0 0\n");
        Assert.Equal(0, exit);
        var echoes = lines.Select(l => JsonNode.Parse(l)!)
            .Where(n => n!["CMD"]?.GetValue<string>() == "equip").ToList();
        Assert.Equal("Soluga@0,0", echoes[1]!["EXCHANGED"]!.GetValue<string>());
    }

    [Fact]
    public void CliEquipLastDrop_StillWorks_TheOriginalVerbUntouched()
    {
        var (exit, lines) = RunScript("new koldun\nbuy soluga\nequip lastdrop\n");
        Assert.Equal(0, exit);
        var echo = lines.Select(l => JsonNode.Parse(l)!).First(n => n!["CMD"]?.GetValue<string>() == "equip")!;
        Assert.Equal("Soluga", echo["ITEM"]!.GetValue<string>());
        Assert.Equal("Weapon", echo["SLOT"]!.GetValue<string>());
    }

    [Fact]
    public void CliEquip_Refusals_NamedErrorLineAndExit1()
    {
        var (exit, lines) = RunScript("new koldun\nbuy napitek-zycia\nequip 0 0\n");
        Assert.Equal(1, exit);
        Assert.Contains("is not wearable", lines[^1]);

        (exit, lines) = RunScript("new koldun\nequip 0 0\n");
        Assert.Equal(1, exit); // nothing anchored at an empty cell — by name
        Assert.Contains("no item anchored at cell 0,0", lines[^1]);

        (exit, lines) = RunScript("new koldun\nequip\n");
        Assert.Equal(1, exit); // the grammar itself, named
        var grammar = JsonNode.Parse(lines[^1])!["ERROR"]!.GetValue<string>(); // < > ride \u003C escapes in the raw line
        Assert.Contains("equip lastdrop", grammar);
        Assert.Contains("<x> <y>", grammar);

        (exit, lines) = RunScript("new koldun\nequip foo bar\n");
        Assert.Equal(1, exit); // non-numeric cells speak the number law
        Assert.Contains("is not a number", lines[^1]);
    }

    // ---- WINDOWED: the pure door-line table + the shipped-file law scans ----

    [Fact]
    public void PanelDoorLines_AreTheGrammar_ReplayedLiveThroughTheRealCli()
    {
        // The mapping-table idiom (TownPanelTests): the panel's strings ARE the
        // grammar — the exact lines a pack-click session emits run through the
        // real CommandRunner at a fixed seed with NO ERROR line.
        Assert.Equal("equip 0 0", InventoryPanelLogic.EquipCellLine(0, 0));
        Assert.Equal("equip 3 9", InventoryPanelLogic.EquipCellLine(3, 9));
        Assert.Equal("cell_0_0", InventoryPanelLogic.AnchorNodeName(0, 0)); // identifier-safe (the D2 rule)
        var (exit, lines) = RunScript("new koldun\nbuy soluga\n" + InventoryPanelLogic.EquipCellLine(0, 0) + "\ndump\n");
        Assert.Equal(0, exit);
        Assert.DoesNotContain(lines, l => l.Contains("\"ERROR\""));
    }

    private static string[] ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game", "scripts")))
            dir = dir.Parent;
        return Directory.GetFiles(Path.Combine(dir!.FullName, "game", "scripts"), "*.cs");
    }

    [Fact]
    public void InventoryPanelDoorCall_AlwaysRidesCommandLog()
    {
        // The KEY LAW twin for the pack panel: the equip door may be called
        // ONLY inside a CommandLog.Call line (a logged door — the click IS the
        // replayable session).
        foreach (var f in ScriptsDir().Where(f => Path.GetFileName(f) == "InventoryPanel.cs"))
        {
            var code = File.ReadAllLines(f);
            for (int lineNo = 0; lineNo < code.Length; lineNo++)
            {
                var line = code[lineNo];
                if (line.TrimStart().StartsWith("//") || !Regex.IsMatch(line, @"\.EquipFromCell\s*\(")) continue;
                Assert.Matches(@"CommandLog\.Call\s*\(", line); // the door call rides the log — line {lineNo+1}
            }
        }
    }

    [Fact]
    public void InventoryPanelRejection_AlwaysPaintsTheEnginesText()
    {
        // Every catch of the engine's refusal hands e.Message to the hint —
        // the TownPanelTests twin: never a silent swallow.
        foreach (var f in ScriptsDir().Where(f => Path.GetFileName(f) == "InventoryPanel.cs"))
        {
            var lines = File.ReadAllLines(f);
            int catches = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!Regex.IsMatch(lines[i], @"catch\s*\(\s*BabaYaga\.Sim\.SessionRejectedException\s+\w+\s*\)")) continue;
                catches++;
                var scope = string.Join(' ', lines.Skip(i).Take(2));
                Assert.Matches(@"OnHint\?\.Invoke\(\w+\.Message\)", scope);
            }
            Assert.True(catches > 0, $"{Path.GetFileName(f)} has no refusal catch — nothing surfaces");
        }
    }

    [Fact]
    public void EquipCellLeg_CallsNoSessionVerb_TheDriverClicksOnly()
    {
        // The GameInputTests KEY LAW twin for this slice's driver: every state
        // move is a synthesized click on a real call site — no door is ever
        // called from the leg files.
        foreach (var f in ScriptsDir().Where(f => Path.GetFileName(f) is "EquipCellLeg.cs" or "EquipCellLeg.Tools.cs"))
        {
            var code = File.ReadAllLines(f);
            for (int lineNo = 0; lineNo < code.Length; lineNo++)
            {
                var line = code[lineNo];
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//")) continue;
                Assert.False(Regex.IsMatch(line, @"\.(EquipFromCell|EquipLastDrop|Buy|Hire|UsePotion)\s*\("),
                    $"{Path.GetFileName(f)}:{lineNo + 1} calls a session verb from the driver: {trimmed}");
            }
        }
        // The driver is wired AND env-gated (Boot names it).
        var boot = File.ReadAllText(Path.Combine(ScriptsDir().First(f => Path.GetFileName(f) == "Boot.cs")));
        Assert.Contains("EquipCellLeg.EnvVar", boot);
        Assert.Contains("RunEquipCellLeg", boot);
    }
}
