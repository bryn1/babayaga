using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BabaYaga.Sim;
using BabaYaga.Sim.Cli;
using Xunit;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// MC 11987 TOWN-PANEL GAMBLE ROW (LEDGER row-75 line, gamble half): the pure
/// mapping core the town panel's GAMBLE row rides — GamblePanelLogic —
/// exercised WITHOUT Godot (the TownPanelLogic/CraftPanelLogic pure-file
/// idiom). RED-first on the base: GamblePanelLogic does not exist yet, so
/// every row that names it is a CS0103 build error (the FollowerFieldTests/
/// CraftPanelTests pinned RED shape). The DoD row groups: (1) LABEL
/// COMPOSITION — the row text is "GAMBLE — &lt;base name&gt;, &lt;price&gt;g"
/// composed from the engine's OWN GambleOffer (name pulled through the base
/// table, price Vendor.BuyPrice's line — the panel computes no prices);
/// (2) REFUSAL→HINT PASSTHROUGH — the no-gold and pack-full refusals carry
/// the engine's NAMED words (real-session rows) and the shipped files paint
/// e.Message unchanged (scan rows); GamblePanelLogic owns NO refusal copy
/// (word-scan pins it); (3) the NO-SIM-CALL scan — the pure logic never
/// touches GameSession or any session door: the buy happens ONLY through the
/// panel's logged door line "gamble" (the CLI grammar, replayed LIVE through
/// the real CommandRunner here); (4) OFFER-PROJECTION DETERMINISM — the same
/// seed projects the same label sequence (the DISPLAY-ONLY projection fence:
/// the session exposes no pending offer — GambleStore.Next off a panel-owned
/// display Rng seeded through DisplaySeedOf is the projection, and it never
/// feeds the buy: the TownPanel scans pin the zero-arg door and the
/// paint-only position of every ProjectOffer line). MouseFilter law rows
/// (MC 11959): the row's readout label is Ignore, the BUY button and the row
/// itself Stop — named through HudReadout.MouseFilterFor, assignment scan on
/// GambleRow.cs (every new node explicit, all through MouseFilterLaw).
/// </summary>
public sealed class GamblePanelTests
{
    private static GameSession Town(ulong seed = 7) => GameSession.New(seed, "koldun", TestData.DataDir());

    /// The panel's projection re-derived independently: the display stream is
    /// seeded through DisplaySeedOf and consumed one draw per shown offer —
    /// the SAME sequence the panel paints (the determinism rows' oracle).
    private static List<string> ProjectionLabels(ItemBaseTable bases, ulong seed, int charLevel, int count)
    {
        var rng = new Rng(GamblePanelLogic.DisplaySeedOf(seed));
        var labels = new List<string>();
        for (int i = 0; i < count; i++)
            labels.Add(GamblePanelLogic.OfferLabel(GamblePanelLogic.ProjectOffer(bases, rng, charLevel)));
        return labels;
    }

    // ---- 1. label composition (engine offer → the row's exact text) ----

    [Fact]
    public void LabelComposition_OfferTextIsNameAndEnginePrice()
    {
        var s = Town();
        var row = s.Bases.For("pierscien");
        long price = Vendor.BuyPrice(row, Vendor.Standard); // the engine's own price math
        Assert.Equal($"GAMBLE — Pierscien, {price}g",
            GamblePanelLogic.OfferLabel(new GambleOffer(row, ItemLevel: 5, price)));
    }

    [Fact]
    public void LabelComposition_ProjectsEnginePrices_NobodyElseComputesThem()
    {
        // Every projected offer's price IS Vendor.BuyPrice of its own base —
        // the label never shows a price the engine did not compute (GambleStore.
        // Next's line is the ONLY price source; no second math exists here).
        var s = Town();
        var rng = new Rng(GamblePanelLogic.DisplaySeedOf(6));
        for (int i = 0; i < 12; i++)
        {
            var offer = GamblePanelLogic.ProjectOffer(s.Bases, rng, s.Level);
            Assert.Equal(Vendor.BuyPrice(offer.Base, Vendor.Standard), offer.Price);
        }
    }

    // ---- 2. the action map: the exact CLI grammar, replayed LIVE ----

    [Fact]
    public void ActionMap_GambleLine_IsTheExactCliGrammar()
    {
        Assert.Equal("gamble", GamblePanelLogic.GambleLine); // ScriptedSession's door, verbatim
        Assert.Equal("BUY", GamblePanelLogic.BuyButton);     // the leg hunts this exact text
    }

    [Fact]
    public void ActionMap_GambleLines_ReplayLiveThroughTheRealCli()
    {
        // The table's string IS the grammar: the exact lines the gamble row
        // emits (new head, two gambles, dump) run through the real
        // CommandRunner at a fixed seed with NO ERROR line and exit 0 — a
        // mis-shaped line exits nonzero (the TownPanelTests live-replay idiom).
        var path = Path.Combine(Path.GetTempPath(), $"gamblepanel-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, string.Join('\n',
            "new koldun",
            GamblePanelLogic.GambleLine,
            GamblePanelLogic.GambleLine,
            "dump") + '\n');
        try
        {
            var output = new StringWriter();
            int exit = CommandRunner.Run(path, 7, TestData.DataDir(), output);
            Assert.Equal(0, exit);
            Assert.DoesNotContain(output.ToString().Split('\n'), l => l.Contains("\"ERROR\""));
        }
        finally { File.Delete(path); }
    }

    // ---- 3. refusal passthrough: the ENGINE's words, nothing invented ----

    [Fact]
    public void RefusalWords_TheEngineSpeaksThem_AndMovesNothing()
    {
        // Drain the purse with REAL gamble doors until one REFUSES: the words
        // are the engine's named pair, the refused call leaves gold and pack
        // UNCHANGED (GambleStore.Buy rejects a short purse with nothing rolled
        // or revealed; the full-pack path refunds), and the refusal REPEATS in
        // the SAME FORM — nothing half-applied. (The named price is each call's
        // OWN fresh draw: the door rolls the offer before the purse gate, so
        // the price digit moves while the form stands — the truth the fence
        // states, pinned here rather than smoothed over.)
        var s = Town(11);
        string last = "";
        for (int i = 0; i < 60; i++)
        {
            long goldBefore = s.Gold;
            int packBefore = s.Carried.Count;
            try { s.Gamble(); }
            catch (SessionRejectedException e)
            {
                last = e.Message;
                Assert.Equal(goldBefore, s.Gold);
                Assert.Equal(packBefore, s.Carried.Count);
                var again = Assert.Throws<SessionRejectedException>(() => s.Gamble());
                Assert.Matches(@"^cannot afford the gamble: \d+ gold$|^the pack has no cell left$", again.Message);
                Assert.Equal(goldBefore, s.Gold); // the repeat refusal moves nothing either
                Assert.Equal(packBefore, s.Carried.Count);
                break;
            }
        }
        Assert.Matches(@"^cannot afford the gamble: \d+ gold$|^the pack has no cell left$", last);
        Assert.False(string.IsNullOrWhiteSpace(last)); // an empty hint would be a lie
    }

    [Fact]
    public void RefusalWords_NeverInventedByThePanelFiles()
    {
        // The panel owns NO refusal copy: neither the pure logic nor the row
        // builder contains any refusal wording — the catch in TownPanel.cs
        // paints e.Message (the scan below, the CraftPanelTests twin).
        var words = new Regex(@"cannot afford|no cell left|not enough gold|refus", RegexOptions.IgnoreCase);
        foreach (var name in new[] { "GamblePanelLogic.cs", "GambleRow.cs" })
        {
            var f = Path.Combine(ScriptsDir(), name);
            Assert.DoesNotContain(File.ReadAllLines(f), l => !l.TrimStart().StartsWith("//") && words.IsMatch(l));
        }
    }

    [Fact]
    public void TownPanelRefusalCatches_AlwaysPaintTheEnginesText()
    {
        // Every catch of the engine's refusal hands e.Message to the hint —
        // including the new gamble door (CraftPanelTests / TownPanelTests twin).
        var f = Path.Combine(ScriptsDir(), "TownPanel.cs");
        var lines = File.ReadAllLines(f);
        int catches = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!Regex.IsMatch(lines[i], @"catch\s*\(\s*SessionRejectedException\s+\w+\s*\)")) continue;
            catches++;
            var scope = string.Join(' ', lines.Skip(i).Take(2));
            Assert.Matches(@"Hint\(\w+\.Message\)", scope);
        }
        Assert.True(catches > 0);
        Assert.DoesNotContain(lines, l => Regex.IsMatch(l, @"catch\s*(\(\s*\))?\s*\{\s*\}"));
    }

    // ---- 4. offer-projection determinism (same seed → same labels) ----

    [Fact]
    public void Projection_Deterministic_SameSeedSameLabelSequence()
    {
        var s = Town(6);
        var a = ProjectionLabels(s.Bases, seed: 6, s.Level, count: 6);
        var b = ProjectionLabels(s.Bases, seed: 6, s.Level, count: 6);
        Assert.Equal(a, b); // same seed → same offers, in order, across independent walks
        Assert.All(a, label => Assert.StartsWith("GAMBLE — ", label));
    }

    [Fact]
    public void Projection_StreamIsPanelOwned_NeverTheSimSeedAlone()
    {
        // The fence constant is PINNED (it is the display stream's identity)
        // and the display seed is never the bare session seed — a panel-owned
        // stream (ORIGINAL DESIGN, fenced in GamblePanelLogic's header).
        Assert.Equal(0x934B37C1D2A4F8E6UL, GamblePanelLogic.DisplaySalt);
        Assert.NotEqual(7UL, GamblePanelLogic.DisplaySeedOf(7UL));
    }

    // ---- scan rows: the shipped files honour the LAWS ----

    private static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game", "scripts")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "game", "scripts");
    }

    [Fact]
    public void GamblePanelLogic_CallsNoSessionDoor()
    {
        // The pure logic may use the ENGINE'S offer machinery (GambleStore.
        // Next — the price line) but must never touch GameSession, call a
        // session door, call the store's Buy (a display never buys), or write
        // state. The buy lives ONLY behind the panel's logged door line.
        var banned = new Regex(@"\bGameSession\b|\.(Gamble|Buy|SellLast|Hire|Revive|Dismiss|CraftRecipe|Purse|Carried)\s*[.(]|\.(Gold|Level|Seed)\s*=");
        var f = Path.Combine(ScriptsDir(), "GamblePanelLogic.cs");
        foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("///")) continue;
            Assert.False(banned.IsMatch(line), $"GamblePanelLogic.cs:{lineNo} touches a session door/state: {trimmed}");
        }
    }

    [Fact]
    public void TownPanel_GambleDoor_RidesCommandLog_ZeroArgs()
    {
        // The key law (TownPanelTests twin, extended to the new door): the
        // session's Gamble() may be called in the panel ONLY inside a
        // CommandLog.Call line — and with ZERO arguments, so no display
        // projection can ever feed the buy.
        var f = Path.Combine(ScriptsDir(), "TownPanel.cs");
        int doorCalls = 0;
        foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("///")) continue;
            var m = Regex.Match(line, @"\.Gamble\s*\(\s*\)");
            if (!m.Success) continue;
            doorCalls++;
            Assert.Matches(@"CommandLog\.Call\s*\(", line);
            Assert.False(Regex.IsMatch(line, @"ProjectOffer|OfferLabel|_offer"),
                $"TownPanel.cs:{lineNo} display state feeds the door line: {trimmed}");
        }
        Assert.True(doorCalls >= 1, "TownPanel.cs lost its gamble door call");
    }

    [Fact]
    public void TownPanel_ProjectionIsPaintOnly()
    {
        // Every ProjectOffer/offer-projection line in the panel is a PAINT
        // line: none may sit inside a CommandLog.Call line (the fence's
        // runtime half — the display never reaches the door).
        var f = Path.Combine(ScriptsDir(), "TownPanel.cs");
        foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("///")) continue;
            if (!Regex.IsMatch(line, @"ProjectOffer")) continue;
            Assert.DoesNotMatch(@"CommandLog\.Call", line);
        }
    }

    [Fact]
    public void MouseFilterLaw_NamesTheGambleRowParts()
    {
        // The MC 11959 pure law (the sealed scan's own table): the row's
        // READOUT label lets clicks through (Ignore — its row Stop keeps the
        // click inside the open panel); the BUY button and the row chrome
        // BLOCK (Stop — an open panel never leaks world clicks).
        Assert.Equal("Ignore", HudReadout.MouseFilterFor("town.gamble-offer"));
        Assert.Equal("Stop", HudReadout.MouseFilterFor("town.gamble-buy"));
        Assert.Equal("Stop", HudReadout.MouseFilterFor("town.gamble-row"));
    }

    [Fact]
    public void GambleRow_EveryNewNodeCarriesAnExplicitLawAssignment()
    {
        // UI law (MC 11959 law shape, MC 11987 row): EVERY new node names
        // itself through MouseFilterLaw.For — no engine-default nodes, no
        // direct MouseFilterEnum writes, and the three named parts are all
        // present (row, offer label, buy button).
        var f = Path.Combine(ScriptsDir(), "GambleRow.cs");
        var lines = File.ReadAllLines(f);
        var wired = lines.Where(l => Regex.IsMatch(l, @"MouseFilter\s*=\s*MouseFilterLaw\.For\(""([^""]+)""\)"))
                         .Select(l => Regex.Match(l, @"MouseFilterLaw\.For\(""([^""]+)""\)").Groups[1].Value)
                         .ToList();
        Assert.Contains("town.gamble-row", wired);
        Assert.Contains("town.gamble-offer", wired);
        Assert.Contains("town.gamble-buy", wired);
        Assert.DoesNotContain(lines, l => !l.TrimStart().StartsWith("//")
            && Regex.IsMatch(l, @"MouseFilter\s*=\s*(Control\.)?MouseFilterEnum\."));
        // no node is constructed filter-free: count new nodes vs assignments
        int nodes = lines.Count(l => !l.TrimStart().StartsWith("//")
            && Regex.IsMatch(l, @"new\s+(HBoxContainer|Label|Button)\s*[{(]"));
        Assert.True(wired.Count >= nodes,
            $"GambleRow.cs constructs {nodes} nodes but wires only {wired.Count} through the law");
    }

    [Fact]
    public void GambleLegDriver_CallsNoSessionVerb_WritesNoState()
    {
        // The driver law (S4/S6, extended to the gamble files): every state
        // move is a synthesized click landing on the real call sites — the
        // Gamble verb here is what the CLICKED button calls, never the leg.
        var verb = new Regex(
            @"\.(Gamble|Buy|AddToBelt|Hire|Revive|Dismiss|UsePotion|Attack|TravelTo|DiscoverWaypoint|Respawn|Loot|Cast|LevelTo|RestTick)\s*\(");
        var stateWrite = new Regex(
            @"\b(s|S|session|Session)\.(CurrentArea|Gold|Purse|Carried|Belt|Drops|Engaged\w*|Merc|Waypoints|Quests|Equipment|Character)\b\s*(=|\.Add|\.Remove|\.RemoveAt|\.Clear)|\.(Life|Mana|Xp)\s*=\s*[^=]");
        foreach (var name in new[] { "GambleLeg.cs", "GambleLeg.Tools.cs" })
            foreach (var (lineNo, line) in File.ReadAllLines(Path.Combine(ScriptsDir(), name)).Select((l, i) => (i + 1, l)))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("///")) continue;
                Assert.False(verb.IsMatch(line), $"{name}:{lineNo} driver calls a session verb: {trimmed}");
                Assert.False(stateWrite.IsMatch(line), $"{name}:{lineNo} driver writes session state: {trimmed}");
            }
    }
}
