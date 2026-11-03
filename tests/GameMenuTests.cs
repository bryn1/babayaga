using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// MC 11957 ESC GAME MENU (LEDGER row 75's game-menu half): the pure decision
/// core the windowed GameMenu rides — GameMenuLogic — exercised WITHOUT Godot
/// (the DeathMenuLogic/TownPanelTests pure-file/link idiom). D2 truth (oracle,
/// cited in Act.cs): Escape opens the game menu; the honest clone scope is a
/// windowed panel with EXACTLY two real actions. Rows: (1) the action→door
/// MAPPING with SPY ORDER — Resume hides and rides no door; Save &amp; Quit
/// calls the save door THEN the scene door, never the reverse (the reversed
/// order would leave the hero unsaved — the spy list makes the order an
/// assertion, not a comment); (2) the DEAD INTERLOCK — the game menu never
/// opens while IsDead owns the key (paired with DeathMenuLogic.OpensFor, the
/// death menu stays authoritative); (3) the double-toggle idempotence row;
/// (4) the TOWN-PANEL OVERLAW row — the menu layer outranks the panel layers
/// PARSED FROM Act.cs's own wiring, and Act's ESC branch is statically proven
/// exclusive; plus the boot law (the bare windowed scene change must ride
/// CallDeferred — the LEDGER row-86 cure pinned) and the driver-leg scans.
/// </summary>
public class GameMenuTests
{
    // ---- 1. action → door mapping, order SPIED through the twin ----

    [Fact]
    public void ResumePress_HidesThePanel_RidesNoDoor()
    {
        var calls = new System.Collections.Generic.List<string>();
        GameMenuLogic.PressResume(() => calls.Add("hide"));
        Assert.Equal(new[] { "hide" }, calls); // hide only — no save, no scene change
    }

    [Fact]
    public void SaveQuitPress_CallsTheSaveDoor_THENTheSceneDoor()
    {
        var calls = new System.Collections.Generic.List<string>();
        GameMenuLogic.PressSaveAndQuit(() => calls.Add("save"), () => calls.Add("scene"));
        // The order IS the contract: save FIRST, main-menu scene flow second.
        // A plant that swaps the two calls goes RED on this exact row.
        Assert.Equal(new[] { "save", "scene" }, calls);
    }

    [Fact]
    public void Labels_AreTheTwoRealActions_NothingElse()
    {
        Assert.Equal("RESUME", GameMenuLogic.ResumeButton);
        Assert.Equal("SAVE & QUIT", GameMenuLogic.SaveQuitButton);
        Assert.NotEqual(GameMenuLogic.ResumeButton, GameMenuLogic.SaveQuitButton);
    }

    [Fact]
    public void GameMenuFile_PaintsExactlyTwoButtons_NoTheaterRow()
    {
        // A button that does nothing is BANNED: the panel file wires exactly the
        // two twin-labelled buttons and no third row.
        var text = File.ReadAllText(RepoFile("game", "scripts", "GameMenu.cs"));
        Assert.Equal(2, Regex.Matches(text, @"Button\(GameMenuLogic\.\w+").Count);
    }

    // ---- 2. the DEAD interlock: the death menu stays authoritative ----

    [Fact]
    public void OpensFor_AliveOpens_DeadNeverDoes_DeathMenuOwnsTheDead()
    {
        Assert.True(GameMenuLogic.OpensFor(isDead: false));
        Assert.False(GameMenuLogic.OpensFor(isDead: true)); // ESC-during-dead: NO game menu, ever
        Assert.True(DeathMenuLogic.OpensFor(isDead: true)); // the pair: the corpse owns the key
    }

    [Fact]
    public void GameMenuYields_WhenDeadAndVisible()
    {
        // The authoritative-yield law (the ESC-during-dead interlock's second
        // half): a game menu left visible dies out of the way when the death
        // menu takes the screen; alive, it never yields to nothing.
        Assert.True(GameMenuLogic.GameMenuMustYield(isDead: true, gameMenuVisible: true));
        Assert.False(GameMenuLogic.GameMenuMustYield(isDead: true, gameMenuVisible: false));
        Assert.False(GameMenuLogic.GameMenuMustYield(isDead: false, gameMenuVisible: true));
    }

    [Fact]
    public void ActEscBranch_IsExclusive_DeadToDeathMenu_AliveToGameMenu()
    {
        // The shipped wiring: within the game_menu handler the dead branch (the
        // DeathMenuLogic.OpensFor gate → ToggleDeathMenu) and the alive branch
        // (ToggleGameMenu) are the ELSE of each other — no double-open path —
        // and the retired alive-hint wording is gone (retired-claim law).
        var act = File.ReadAllText(RepoFile("game", "scripts", "Act.cs"));
        var i = act.IndexOf("IsActionPressed(\"game_menu\")", StringComparison.Ordinal);
        Assert.True(i > 0, "Act has no game_menu handler at all");
        var scope = act[i..].Split("private ")[0];
        Assert.Contains("DeathMenuLogic.OpensFor", scope);
        Assert.Contains("ToggleDeathMenu", scope);
        Assert.Contains("GameMenuMustYield", scope); // the dead branch yields the visible menu
        Assert.Contains("ToggleGameMenu", scope);
        Assert.DoesNotContain("the game menu lands with the widget row", act); // theater hint RETIRED
    }

    // ---- 3. toggle idempotence (the Act doors' toggle pattern, pinned) ----

    [Fact]
    public void ToggleTo_DoublePressIsIdempotent()
    {
        Assert.True(GameMenuLogic.ToggleTo(false));  // one press opens
        Assert.False(GameMenuLogic.ToggleTo(true));   // second press closes
        Assert.True(GameMenuLogic.ToggleTo(GameMenuLogic.ToggleTo(true)));  // double = IDENTITY (open stays open)
        Assert.False(GameMenuLogic.ToggleTo(GameMenuLogic.ToggleTo(false))); // …from both sides (closed stays closed)
        Assert.True(GameMenuLogic.ToggleTo(GameMenuLogic.ToggleTo(GameMenuLogic.ToggleTo(false)))); // triple = single
    }

    // ---- 4. the town-panel overlap law + the z-order from Act's own wiring ----

    [Fact]
    public void GameMenuLayer_OutranksEveryActOverlay_ParsedFromTheWiring()
    {
        // The reading this card states (brief DoD 1): the menu RIDES ABOVE the
        // town panel (and every other overlay) — it never closes them; RESUME
        // reveals them still open (the windowed leg reads exactly this state).
        // Layers are PARSED FROM Act.cs — the wiring itself, not a comment.
        var act = File.ReadAllText(RepoFile("game", "scripts", "Act.cs"));
        var layers = Regex.Matches(act, @"Name = ""(\w+Layer)"", Layer = (\d+)")
            .ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));
        Assert.Equal(20, layers["DeathMenuLayer"]); // pin the known siblings: a silent
        Assert.Equal(21, layers["TownPanelLayer"]); // drift of these goes RED here too
        Assert.Equal(22, layers["InventoryLayer"]);
        Assert.Equal(23, layers["StashPanelLayer"]);
        Assert.Contains("Name = \"GameMenuLayer\", Layer = GameMenuLogic.Layer", act); // the constant IS the wiring
        Assert.True(GameMenuLogic.Layer > layers["TownPanelLayer"]); // the overlap row
        Assert.All(layers.Values, l => Assert.True(GameMenuLogic.Layer > l)); // above EVERY overlay
    }

    // ---- the boot law: the LEDGER row-86 cure, pinned against regression ----

    [Fact]
    public void Boot_BareWindowedSceneChange_RidesCallDeferred()
    {
        // Root is busy setting up children INSIDE _Ready — a synchronous
        // ChangeSceneToFile there prints one engine ERROR per windowed boot
        // (LEDGER row 86, measured: "Parent node is busy… remove_child()").
        // The cure shape the engine names is DEFERRAL; this row bans re-adding
        // the synchronous call to the bare windowed branch.
        var boot = File.ReadAllLines(RepoFile("game", "scripts", "Boot.cs"));
        var line = Assert.Single(boot, l => l.Contains("ChangeSceneToFile(\"res://scenes/MainMenu.tscn\")"));
        Assert.Contains("CallDeferred", line);
    }

    // ---- driver-leg scans (the TownPanelTests/S6 twins) ----

    private static string[] ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game", "scripts")))
            dir = dir.Parent;
        return Directory.GetFiles(Path.Combine(dir!.FullName, "game", "scripts"), "*.cs");
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, Path.Combine(parts));
    }

    [Fact]
    public void GameMenuDriverLeg_CallsNoSessionVerb_WritesNoState()
    {
        // The leg is a driver (the S4 law): every state move is a synthesized
        // click/key landing on the real call sites.
        var verb = new Regex(
            @"\.(Buy|AddToBelt|Hire|Revive|Dismiss|UsePotion|Attack|TravelTo|DiscoverWaypoint|Respawn|Loot|Cast|LevelTo|RestTick|Save)\s*\(");
        var stateWrite = new Regex(
            @"\b(s|S|session|Session)\.(CurrentArea|Gold|Purse|Carried|Belt|Drops|Engaged\w*|Merc|Waypoints|Quests|Equipment|Character)\b\s*(=|\.Add|\.Remove|\.RemoveAt|\.Clear)|\.(Life|Mana|Xp)\s*=\s*[^=]");
        var legFiles = new[] { "GameMenuLeg.cs", "GameMenuLeg.Tools.cs" };
        foreach (var f in ScriptsDir().Where(f => legFiles.Contains(Path.GetFileName(f))))
            foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//")) continue;
                Assert.False(verb.IsMatch(line), $"{Path.GetFileName(f)}:{lineNo} driver calls a session verb: {trimmed}");
                Assert.False(stateWrite.IsMatch(line), $"{Path.GetFileName(f)}:{lineNo} driver writes session state: {trimmed}");
            }
    }
}
