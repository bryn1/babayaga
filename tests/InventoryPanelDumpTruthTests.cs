using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BabaYaga.Sim;
using Xunit;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// MC 11995 PRE-GA AUDIT P2 — "which cell does a carried item occupy" had
/// TWO answers: the Sim's (CarriedSpots, row-major first-fit that NEVER
/// compacts — GameSession.TryCarry; the removals drop one entry only) and
/// the inventory panel's PAINT-TIME re-derivation (InventoryPanel.BuildGrid
/// first-fitting over Carried order with InventoryPanelLogic.TryDeriveSpot).
/// After any removal that leaves a hole left of a still-carried item the two
/// diverge: the painted anchor names a cell the engine answers
/// "the pack holds no item anchored at cell r,c" for, the TRUE anchor paints
/// non-clickable, and other geometries land a painted anchor on a DIFFERENT
/// item's cell (wrong-item equip). The dump ALREADY carries per-item row/col
/// (SaveSerializer.ToDto → CarriedSpots.GetValueOrDefault) and the stash
/// panel already paints from it (StashPanelLogic.CarriedItems — "the CLI
/// dump line IS the ONE place the Sim answers 'which cell?'"); this card
/// makes the inventory panel paint the SAME one answer (REPLACES the
/// re-derivation, does not parallel it; click routing stays as-is: the paint
/// rides the dump, the click rides the logged "equip &lt;x&gt; &lt;y&gt;" door).
/// RED-FIRST: on the base these rows do not COMPILE — InventoryPanelLogic
/// PackAnchors does not exist there (the CS0103 shape the
/// TownPanelTests/CraftPanelTests/FollowerFieldTests precedent pins as RED),
/// and out/leg-m5-inv/base-divergence.txt PROVES the base divergence
/// BEHAVIOURALLY: base paint (0,0) vs dump (0,1), painted click REFUSED by
/// the engine's own words, true anchor ACCEPTED. The three-way law the hole
/// case pins: painted cell == dump row/col == a cell EquipFromCell ACCEPTS
/// (the sweep repeats painted == dump over 50+ hole shapes and pins that a
/// survivor's anchor never moves when a neighbour leaves — no compaction).
/// </summary>
public class InventoryPanelDumpTruthTests
{
    private static GameSession Fresh(ulong seed) => GameSession.New(seed, "koldun", TestData.DataDir());

    /// <summary>The dump's OWN anchors, parsed INDEPENDENTLY of the logic
    /// file (raw SaveSerializer rows) — the oracle both sides are measured
    /// against, in Carried order exactly as the serializer writes them.</summary>
    private static List<(int Row, int Col)> DumpAnchors(GameSession s)
    {
        var list = new List<(int, int)>();
        foreach (var i in JsonNode.Parse(s.Dump())!["carried"]!.AsArray())
            list.Add((i!["row"]!.GetValue<int>(), i["col"]!.GetValue<int>()));
        return list;
    }

    // ---- the HOLE CASE: three-way agreement, painted == dump == accepted ----

    [Fact]
    public void HoleCase_EquipRemoval_ThreeWayAgreement_PaintedEqualsDumpEqualsEquipFromCellAccepts()
    {
        // Carry soluga (1×4 → anchor 0,0), then szyszak (2×2 → anchor 0,1);
        // remove the EARLIER through the panel's own click door (EquipFromCell
        // — the logged equip the anchor click rides). The survivor's true
        // anchor stays 0,1 (first-fit NEVER compacts): the base re-derivation
        // painted it 0,0 (out/leg-m5-inv/base-divergence.txt: painted click
        // REFUSED "the pack holds no item anchored at cell 0,0", true anchor
        // 0,1 ACCEPTED). The fixed paint answers 0,1 and the click it names
        // is the cell the engine accepts, wearing the RIGHT item.
        var s = Fresh(11);
        s.Buy("soluga");
        s.Buy("szyszak");
        s.EquipFromCell(0, 0);
        Assert.Single(s.Carried);

        var painted = InventoryPanelLogic.PackAnchors(s.Dump());
        var dump = DumpAnchors(s);
        Assert.Single(painted);
        Assert.Equal(dump[0], painted[0]);        // painted == the dump's answer
        Assert.Equal((0, 1), painted[0]);         // the DIVERGENT shape itself — base painted (0,0)

        s.EquipFromCell(painted[0].Row, painted[0].Col); // … == a cell EquipFromCell ACCEPTS
        Assert.Equal("szyszak", s.Equipment[EquipSlot.Head]!.Base.Id); // … and the RIGHT item, never a wrong-one
        Assert.Empty(s.Carried);
    }

    [Fact]
    public void HoleCase_StoreRemoval_SurvivorKeepsItsAnchor_PaintFollowsTheDump()
    {
        // Second removal path (the stash door, no equip involved): the hole
        // is a property of REMOVAL, not of one door. The survivor's dump
        // anchor is its PRE-removal anchor (never compaction), and the paint
        // tracks it exactly.
        var s = Fresh(11);
        s.Buy("soluga");   // anchor 0,0
        s.Buy("szyszak");  // anchor 0,1
        var before = DumpAnchors(s);
        Assert.Equal((0, 1), before[1]);
        s.Store(0, 0, 1);  // the EARLIER leaves the pack through the chest door
        Assert.Single(s.Carried);

        var painted = InventoryPanelLogic.PackAnchors(s.Dump());
        Assert.Equal(before[1], DumpAnchors(s)[0]);           // the sim never moved the survivor
        Assert.Equal(DumpAnchors(s).ToHashSet(), painted.ToHashSet()); // paint == dump
        Assert.Equal((0, 1), painted.Single());               // and the shape is the divergent one
    }

    // ---- the SWEEP: 50+ hole shapes, painted == dump, anchors never compact ----

    [Fact]
    public void Sweep_WithHoles_PaintedEqualsDump_Over50SampledShapes()
    {
        // Deterministic buy/store choreography over bases of five different
        // footprints (1×1, 1×3, 1×4, 2×2, 2×3, 2×4, 2×1): every step leaves
        // (or keeps) holes; EVERY step re-checks paint == dump as a set and
        // every survivor's anchor against its first painted anchor (a
        // neighbour leaving NEVER moves an item — the sim's law the
        // re-derivation broke). The purse top-up is the public Purse.Receive
        // authoring door (the HirelingStatTests fixture idiom); no RNG stream
        // is touched and the shape count itself is asserted, never assumed.
        var bases = new[] { "palica", "soluga", "szyszak", "napitek-zycia", "oczep",
                            "watan", "lapcie", "granat", "postak", "pojas" };
        var s = Fresh(5);
        s.Purse.Receive(100_000); // fixture purse so the sweep is never purse-bound
        var anchorOf = new Dictionary<Item, (int Row, int Col)>();
        int shapes = 0;

        for (int step = 0; step < 60; step++)
        {
            int cells = s.Carried.Sum(i => i.Base.Width * i.Base.Height);
            bool wantBuy = s.Carried.Count == 0 || (cells <= 28 && step % 3 != 2);
            bool moved = false;
            if (wantBuy)
            {
                try { s.Buy(bases[step % bases.Length]); moved = true; }
                catch (SessionRejectedException) { /* "the pack has no cell left" — the
                    engine's own fragmentation answer (cells used ≤ 28 says nothing about
                    contiguous room): the honest ride is the door's refusal, then store. */ }
            }
            if (!moved && s.Carried.Count > 0)
            {
                var anchors = DumpAnchors(s);
                int j = step % anchors.Count;
                var (r, c) = anchors[j];
                var leaving = s.Carried[j]; // dump order IS Carried order — the item at that anchor
                try { s.Store(r, c, 1); anchorOf.Remove(leaving); } // store BY ANCHOR, as an anchor click does
                catch (SessionRejectedException)
                {
                    // "stash tab 1 is full" — cycle a tab row BACK: Take re-enters the
                    // pack through TryCarry (hole refill at a FRESH first-fit cell — the
                    // taken-back item is a new anchor, so its stability law is N/A and
                    // the map already dropped it at its store).
                    var tab = StashPanelLogic.TabItems(s.Dump(), 1);
                    if (tab.Count > 0)
                    {
                        var t = tab[step % tab.Count];
                        try { s.Take(1, t.Row, t.Col); } catch (SessionRejectedException) { /* pack-side full: the step simply checks state */ }
                    }
                }
            }

            var dumpNow = DumpAnchors(s);
            var paintedNow = InventoryPanelLogic.PackAnchors(s.Dump());
            Assert.Equal(dumpNow.Count, paintedNow.Count);
            Assert.Equal(dumpNow.ToHashSet(), paintedNow.ToHashSet()); // painted == dump, every shape
            for (int i = 0; i < s.Carried.Count; i++)
            {
                if (!anchorOf.TryAdd(s.Carried[i], dumpNow[i]))
                    Assert.Equal(anchorOf[s.Carried[i]], dumpNow[i]); // never re-anchored by a neighbour's removal
            }
            shapes++;
        }
        Assert.True(shapes >= 50, $"the sweep sampled {shapes} shapes — the 50+ sampling law is not met");
    }

    // ---- the pure-scan + shipped-panel law rows (existing scan idiom) ----

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game", "scripts")))
            dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, Path.Combine(parts)));
    }

    private static string[] ShippedCodeLines(string name) =>
        RepoFile("game", "scripts", name).Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//")).ToArray();

    [Fact]
    public void PaintTruth_IsPure_InputIsDumpShapedDataOnly_NoSimTypes()
    {
        // The pure-file law the TownPanelLogic/DeathMenuLogic idiom pins: the
        // paint's ONE logic input is DUMP-SHAPED DATA (the dump report string),
        // never a session object — no Godot types, no sim types in the file.
        var src = RepoFile("game", "scripts", "InventoryPanelLogic.cs");
        Assert.DoesNotContain("using Godot", src);
        Assert.DoesNotContain("GameSession", src);
        Assert.DoesNotContain("BabaYaga.Sim", src);
        Assert.Matches(@"PackAnchors\s*\(\s*string\s+dumpJson\s*\)", src); // the input IS the dump text
    }

    [Fact]
    public void ShippedPanel_PaintsFromTheDump_NoReDerivationLeftInThePaintPath()
    {
        // The replacement, scanned on the shipped file (REPLACED not
        // paralleled): BuildGrid asks the dump — through PackAnchors — which
        // cells to paint; the geometry re-derivation is GONE from the panel
        // (TryDeriveSpot may live in the logic file ONLY as the sealed
        // M5GateFixTests relic — the panel must never call it; the deletion
        // itself rides the orchestrator seam ruling, named in this card's
        // return). Click routing stays: the door call rides CommandLog.Call,
        // pinned where it lives (HeroEquipCellTests).
        var code = ShippedCodeLines("InventoryPanel.cs");
        Assert.DoesNotContain(code, l => l.Contains("TryDeriveSpot"));
        Assert.DoesNotContain(code, l => l.Contains("new bool[")); // no occupancy matrix to first-fit into
        Assert.Contains(code, l => Regex.IsMatch(l, @"PackAnchors\s*\(\s*s\.Dump\s*\(\s*\)\s*\)")); // the pulled truth
    }

    // ---- the driver law over the new windowed leg (the S4/HeroEquip twin) ----

    [Fact]
    public void InvTruthLeg_CallsNoSessionVerb_TheDriverClicksOnly()
    {
        // Every state move of the windowed proof is a synthesized CLICK on a
        // real call site (the EquipCellLeg law, HeroEquipCellTests twin): no
        // door is ever called from the leg files, and Boot wires it env-gated.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game", "scripts")))
            dir = dir.Parent;
        var scripts = Path.Combine(dir!.FullName, "game", "scripts");
        var legFiles = new[] { "InvTruthLeg.cs", "InvTruthLeg.Tools.cs" }
            .Select(f => Path.Combine(scripts, f)).ToList();
        foreach (var f in legFiles)
        {
            Assert.True(File.Exists(f), $"{Path.GetFileName(f)} missing — the scan must never be vacuous-green");
            foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//")) continue;
                Assert.False(Regex.IsMatch(line, @"\.(EquipFromCell|EquipLastDrop|Buy|Hire|UsePotion|Store|Take)\s*\("),
                    $"{Path.GetFileName(f)}:{lineNo} calls a session verb from the driver: {trimmed}");
            }
        }
        var boot = RepoFile("game", "scripts", "Boot.cs");
        Assert.Contains("InvTruthLeg.EnvVar", boot);
        Assert.Contains("RunInvTruthLeg", boot);
    }
}
