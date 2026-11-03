using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// MC 11959 HUD MOUSE PASS-THROUGH — the HUD click law, RED-first (on the
/// base these rows do not COMPILE: HudReadout.MouseFilterFor does not exist
/// yet — the CS0117 shape the TownPanelTests precedent pins as RED). The law:
/// the read-out HUD (pull-only widgets, kb law: a PanelContainer swallows
/// every click under its rect — MC 11958 measured it live: walking into
/// czelusc's west door needed the one pixel standing clear of the panel)
/// lets world clicks THROUGH (Ignore); the interactive overlays STOP them —
/// a click on an open panel must never fall through to the world. The pure
/// table lives in Hud.cs's HUDCORE half (HudReadout.MouseFilterFor, the half
/// this project compiles; the Godot shell maps it through MouseFilterLaw),
/// and the scan rows pin the shipped files to the law (the TownPanelTests /
/// GameMenuTests scan-row idiom). The runtime side rides the windowed leg
/// out/leg-m5-hud/ (xdotool clicks THROUGH the measured rect, state reads).
/// </summary>
public class HudMouseFilterTests
{
    // ===================== the pure law (HUDCORE half) =====================

    [Theory] // every HUD part is READ-OUT today — belt row, orbs, counters,
             // hint label — so every one of them lets the click through.
    [InlineData("hud.panel")]
    [InlineData("hud.box")]
    [InlineData("hud.orb-row")]
    [InlineData("hud.orb-gauge")]
    [InlineData("hud.line")]
    [InlineData("hud.hint")]
    public void ReadoutParts_LetWorldClicksThrough(string part) =>
        Assert.Equal("Ignore", HudReadout.MouseFilterFor(part));

    [Theory] // the interactive overlays block: a click that lands on an OPEN
             // panel hits the panel, never the world beneath it.
    [InlineData("TownPanel")]
    [InlineData("InventoryPanel")]
    [InlineData("StashPanel")]
    [InlineData("GameMenu")]
    [InlineData("DeathMenu")]
    public void Overlays_StopClicks(string part) =>
        Assert.Equal("Stop", HudReadout.MouseFilterFor(part));

    [Fact] // the fail-safe direction: an unknown part STOPS — a part nobody
           // named can never leak world clicks by accident.
    public void UnknownPart_DefaultsToStop() =>
        Assert.Equal("Stop", HudReadout.MouseFilterFor("something-nobody-named"));

    // ======================= the shipped-file scans =======================

    private static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game", "scripts")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "game", "scripts");
    }

    [Fact]
    public void HudShell_WiresEveryReadoutPartThroughTheLaw()
    {
        // Every MouseFilter the HUD shell sets rides the pure law, and every
        // part name it hands the law is a read-out name (Ignore). A direct
        // MouseFilterEnum.Stop inside the HUD tree IS the swallowed shape
        // MC 11958 measured — the last assert keeps it out for good.
        var lines = File.ReadAllLines(Path.Combine(ScriptsDir(), "Hud.cs"));
        var wired = new Regex(@"MouseFilter\s*=\s*MouseFilterLaw\.For\(""([^""]+)""\)");
        int wiredCount = 0;
        foreach (var (lineNo, line) in lines.Select((l, i) => (i + 1, l)))
        {
            var m = wired.Match(line);
            if (!m.Success) continue;
            wiredCount++;
            Assert.Equal("Ignore", HudReadout.MouseFilterFor(m.Groups[1].Value));
        }
        Assert.True(wiredCount >= 4,
            $"the HUD shell wires only {wiredCount} parts through the law — the swallow box is back");
        Assert.DoesNotContain(lines, l =>
            !l.TrimStart().StartsWith("//") &&
            Regex.IsMatch(l, @"MouseFilter\s*=\s*(Control\.)?MouseFilterEnum\.Stop"));
    }

    [Fact]
    public void HudShell_BuildsNoInteractiveWidgets()
    {
        // Pins TODAY's truth: no HUD child answers a click (belt 1–4 and
        // cast_1 are KEYBOARD doors — the brief's "if a child is interactive
        // TODAY, keep it STOP and say which" branch is therefore vacuous).
        // A Button/LineEdit added to Hud.cs goes RED here: the new widget
        // must keep its own STOP and the law table must name it.
        var lines = File.ReadAllLines(Path.Combine(ScriptsDir(), "Hud.cs"));
        Assert.DoesNotContain(lines, l =>
            !l.TrimStart().StartsWith("//") &&
            Regex.IsMatch(l, @"new\s+(Button|LineEdit|OptionButton|CheckBox|TextureButton)\b"));
    }

    [Fact]
    public void OverlayFiles_NeverOpenTheirClickBodies()
    {
        // The overlays keep blocking: today NO overlay file sets a MouseFilter
        // at all (panel bodies block by the PASS default, buttons by STOP).
        // The ONE shipped exception is InventoryPanel's root — the full-screen
        // shell whose own comment names why it passes clicks around its body
        // ("the panel never eats the game's clicks", MC 11943); the body
        // still blocks. Any OTHER MouseFilter move in these five files goes
        // RED here.
        var files = new[] { "TownPanel.cs", "InventoryPanel.cs", "StashPanel.cs", "GameMenu.cs", "DeathMenu.cs" };
        var assign = new Regex(@"MouseFilter\s*=");
        foreach (var f in Directory.GetFiles(ScriptsDir(), "*.cs").Where(f => files.Contains(Path.GetFileName(f))))
            foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
            {
                if (!assign.IsMatch(line) || line.TrimStart().StartsWith("//")) continue;
                Assert.True(Path.GetFileName(f) == "InventoryPanel.cs"
                        && Regex.IsMatch(line, @"MouseFilter\s*=\s*MouseFilterEnum\.Ignore\s*;"),
                    $"{Path.GetFileName(f)}:{lineNo} changes an overlay's click behavior: {line.Trim()}");
            }
    }

    [Fact]
    public void HudClickLeg_CallsNoSessionVerb_WritesNoState()
    {
        // The leg is a reader (the S4/S6 driver law, extended): the clicks
        // are the xdotool driver's; the leg only reads and prints.
        var verb = new Regex(
            @"\.(Buy|AddToBelt|Hire|Revive|Dismiss|UsePotion|Attack|TravelTo|DiscoverWaypoint|Respawn|Loot|Cast|LevelTo|RestTick)\s*\(");
        var stateWrite = new Regex(
            @"\b(s|S|session|Session)\.(CurrentArea|Gold|Purse|Carried|Belt|Drops|Engaged\w*|Merc|Waypoints|Quests|Equipment|Character)\b\s*(=|\.Add|\.Remove|\.RemoveAt|\.Clear)|\.(Life|Mana|Xp)\s*=\s*[^=]");
        var f = Path.Combine(ScriptsDir(), "HudClickLeg.cs");
        foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//")) continue;
            Assert.False(verb.IsMatch(line), $"HudClickLeg.cs:{lineNo} reader calls a session verb: {trimmed}");
            Assert.False(stateWrite.IsMatch(line), $"HudClickLeg.cs:{lineNo} reader writes session state: {trimmed}");
        }
    }
}
