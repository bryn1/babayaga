using System.Text.RegularExpressions;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// M5-S4 RENDER + INPUT (MC 11937, plan-c2 S4 row): the PURE decision cores the
/// windowed input map rides — ClickMap (screen→cell→verb table, the ride gate,
/// the out-of-range refusal), SpriteRoster (the kill→sprite-despawn rule, the
/// presentation-side roster the Sim can never see), DeathMenuLogic (P11: which
/// button maps to which sim door, and WHEN the menu opens at all) — exercised
/// WITHOUT Godot, the same pure-file/link idiom as the S3 bridge. The negative
/// rows close the brief's KEY LAW from the presentation side: the click paths
/// mutate NOTHING on the session directly (grep rows over game/scripts — every
/// door call site rides CommandLog.Call, the S4 driver leg calls no session
/// verb at all) and the Sim stores no positions (the ARCH-declared row the
/// presentation A* rests on — MonsterField.cs: "Presentation-side truth, never
/// Sim state").
/// </summary>
public class GameInputTests
{
    // ---- screen → cell: the ONE iso math, delegated by IsoCamera.ScreenToGrid ----

    [Fact]
    public void ScreenToCell_InvertsGridToScreen_OnKnownCorners()
    {
        // IsoCamera: sx=(x−y)·TILE_W/2, sy=(x+y)·TILE_H/2 at 128×64 cells.
        Assert.Equal((0, 0), ClickMap.ScreenToCell(0, 0, 128, 64));
        Assert.Equal((3, 5), ClickMap.ScreenToCell((3 - 5) * 64.0, (3 + 5) * 32.0, 128, 64));
        Assert.Equal((15, 0), ClickMap.ScreenToCell(15 * 64.0, 15 * 32.0, 128, 64));
        Assert.Equal((0, 15), ClickMap.ScreenToCell(-15 * 64.0, 15 * 32.0, 128, 64));
    }

    [Fact]
    public void ScreenToCell_CellCentre_AndNegativeHalfPlane_MapsIntoTheRightDiamond()
    {
        // A point inside cell (1,1)'s diamond maps to (1,1); the west half-plane is negative X.
        Assert.Equal((1, 1), ClickMap.ScreenToCell(32, 68, 128, 64));
        Assert.Equal((-1, 1), ClickMap.ScreenToCell(-128, 0, 128, 64));
    }

    [Fact]
    public void ScreenToCell_Midpoint_RoundsToEven_LikeMathfRoundToInt()
    {
        // Mathf.RoundToInt is half-to-even (Godot docs: 2.5 → 2) — the delegation must not shift it.
        Assert.Equal((0, 0), ClickMap.ScreenToCell(0, 32, 128, 64)); // a=0, b=1: both halves land on 0.5 → 0
        Assert.Equal((1, 0), ClickMap.ScreenToCell(96, 32, 128, 64)); // a=1.5, b=1: x=round(1.25)=1, y=round(-0.25)=0
    }

    // ---- LMB decision table: ride > walk > refusal, out-of-range refused ----

    [Fact]
    public void DecideLeft_WalkableInterior_Moves()
    {
        Assert.Equal(ClickMap.Left.Move, ClickMap.DecideLeft(8, 8, 16, 16, walkable: true, stoneCellLit: false));
    }

    [Fact]
    public void DecideLeft_UnwalkableInterior_Refuses_NoPathIsPlanned()
    {
        Assert.Equal(ClickMap.Left.Refuse, ClickMap.DecideLeft(8, 8, 16, 16, walkable: false, stoneCellLit: false));
    }

    [Fact]
    public void DecideLeft_LitStoneCell_Rides_WalkYieldsToTheMarker()
    {
        // The stone cell is walkable ground too — the lit marker outranks the walk plan (P9).
        Assert.Equal(ClickMap.Left.Ride, ClickMap.DecideLeft(8, 8, 16, 16, walkable: true, stoneCellLit: true));
    }

    [Fact]
    public void DecideLeft_OutOfRange_Refuses_EvenWithStoneFlagSet()
    {
        Assert.Equal(ClickMap.Left.Refuse, ClickMap.DecideLeft(-1, 5, 16, 16, walkable: false, stoneCellLit: false));
        Assert.Equal(ClickMap.Left.Refuse, ClickMap.DecideLeft(16, 5, 16, 16, walkable: false, stoneCellLit: false));
        Assert.Equal(ClickMap.Left.Refuse, ClickMap.DecideLeft(5, -1, 16, 16, walkable: false, stoneCellLit: false));
        Assert.Equal(ClickMap.Left.Refuse, ClickMap.DecideLeft(5, 99, 16, 16, walkable: false, stoneCellLit: true));
    }

    // ---- ride gate truth table (the door's CanRide half, read BEFORE the click) ----

    [Fact]
    public void RideTarget_DarkLinkedStone_NeverRides_AndNoNodeNeverRides()
    {
        Assert.Null(ClickMap.RideTarget("mchy", ["grodzisko"], a => true, a => false)); // dark stone
        Assert.Null(ClickMap.RideTarget("mchy", ["widlan"], a => false, a => true));    // stone-less ground
        Assert.Null(ClickMap.RideTarget("mchy", [], a => true, a => true));             // no links at all
    }

    [Fact]
    public void RideTarget_LitLinkedStone_Rides_FirstLitWins_CurrentExcluded()
    {
        Assert.Equal("grodzisko", ClickMap.RideTarget("mchy", ["grodzisko", "krecie"],
            a => a is "grodzisko" or "krecie", a => a == "grodzisko"));
        Assert.Equal("krecie", ClickMap.RideTarget("mchy", ["krecie", "grodzisko"],
            a => a is "grodzisko" or "krecie", a => true)); // first in linkedTo order
        Assert.Equal("grodzisko", ClickMap.RideTarget("mchy", ["mchy", "grodzisko"],
            a => true, a => true)); // the current area is never its own destination
    }

    [Fact]
    public void RideTarget_FreshTownVsAfterWalkIn_P9TruthRow()
    {
        // Fresh New Game: only the town stone is lit — no ride target out of grodzisko…
        Assert.Null(ClickMap.RideTarget("grodzisko", ["mchy", "pokrzywlasy"],
            a => a is "mchy" or "grodzisko", a => a == "grodzisko"));
        // …but once the walked-out door has lit mchy's stone, the ride exists (P9).
        Assert.Equal("mchy", ClickMap.RideTarget("grodzisko", ["mchy", "pokrzywlasy"],
            a => a is "mchy" or "grodzisko", a => a is "mchy" or "grodzisko"));
    }

    // ---- despawn-on-kill rule: one kill takes exactly one sprite of the row ----

    [Fact]
    public void Roster_KillDespawnsOneSpriteOfTheRow_OthersStand()
    {
        var r = new SpriteRoster<string>();
        r.Add("pokrzywnik", 3, 4, "p1");
        r.Add("pokrzywnik", 6, 2, "p2");
        r.Add("ponurak", 9, 9, "n1");
        Assert.Equal("p1", r.DespawnOne("pokrzywnik")); // the build-order sprite goes first
        Assert.Equal(2, r.Count);
        Assert.Equal(("pokrzywnik", "p2"), (r.Entries[0].Key, r.Entries[0].Sprite)); // the rest stand, in order
        Assert.Equal(("ponurak", "n1"), (r.Entries[1].Key, r.Entries[1].Sprite));
        Assert.Equal("p2", r.DespawnOne("pokrzywnik")); // the row still has one left — then it is empty
        Assert.Null(r.DespawnOne("pokrzywnik"));
    }

    [Fact]
    public void Roster_UnknownKeyAndEmptyField_NoOpNull()
    {
        var r = new SpriteRoster<string>();
        Assert.Null(r.DespawnOne("leszun")); // an empty field despawns nothing (a planted-red shape)
        r.Add("ponurak", 1, 1, "n1");
        Assert.Null(r.DespawnOne("leszun")); // an unknown row never steals another sprite
        Assert.Equal(1, r.Count);
    }

    // ---- P11: the death-menu button → sim-door map ----

    [Fact]
    public void DeathMenu_OpensOnlyForTheDead_EscAliveKeepsTheHint()
    {
        Assert.True(DeathMenuLogic.OpensFor(isDead: true));
        Assert.False(DeathMenuLogic.OpensFor(isDead: false)); // no pause in M5 (plan R1c)
    }

    [Fact]
    public void DeathMenu_RespawnMapsToTheRespawnDoor_QuitMapsToNoDoor()
    {
        Assert.Equal("respawn", DeathMenuLogic.DoorLineFor(DeathMenuLogic.RespawnButton)); // the CLI twin line
        Assert.Null(DeathMenuLogic.DoorLineFor(DeathMenuLogic.QuitButton)); // QUIT ends the engine, no sim door
        Assert.Null(DeathMenuLogic.DoorLineFor("SAVE")); // nothing else is a door
    }

    // ---- the ARCH-declared row: the Sim stores NO positions (presentation A* rests on it) ----

    [Fact]
    public void GameSession_StoresNoPositions_TheDeclaredRow()
    {
        // plan-c2 §0 "the Sim stores NO positions": no property or field on the
        // session answers "where" — hero/monster cells are presentation truth.
        var named = typeof(BabaYaga.Sim.GameSession)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .Concat(typeof(BabaYaga.Sim.GameSession)
                .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Select(f => f.Name));
        Assert.DoesNotContain(named, n => Regex.IsMatch(n, "(?i)pos|cell|coord|location"));
    }

    // ---- negative law rows: no presentation write touches session state directly ----

    private static string[] ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "game", "scripts")))
            dir = dir.Parent;
        return Directory.GetFiles(Path.Combine(dir!.FullName, "game", "scripts"), "*.cs");
    }

    /// <summary>The M5-S4 input map's files — the brief's KEY LAW scopes to the
    /// CLICK PATHS: windowed code a player's mouse/keys reach. The headless
    /// marker legs are M4 scaffolding riding outside this set (Boot runs them
    /// inside one _Ready, never a click).</summary>
    private static readonly string[] ClickPaths =
    [
        "HeroController.cs", "Act.cs", "Hud.cs", "MonsterField.cs", "AreaRenderer.cs",
        "IsoCamera.cs", "MainMenu.cs", "S4InputLeg.cs", "S4InputLeg.Tools.cs", "DeathMenu.cs", "SessionClockNode.cs",
    ];

    private static readonly Regex StateWrite = new(
        @"\b(s|S|session|Session)\.(CurrentArea|Gold|Purse|Carried|Belt|Drops|Engaged\w*|Merc|Waypoints|Quests|Equipment|Character)\b\s*(=|\.Add|\.Remove|\.RemoveAt|\.Clear)|\.(Life|Mana|Xp)\s*=\s*[^=]",
        RegexOptions.Compiled);

    [Fact]
    public void NoClickPathFile_WritesSessionFields()
    {
        // Presentation NEVER mutates sim state: only read-backs and door CALLS.
        // GameContext owns the single session reference (Session = New/Load),
        // which is the holder, not a state write — the regex above never names it.
        foreach (var f in ScriptsDir())
        {
            if (!ClickPaths.Contains(Path.GetFileName(f))) continue;
            foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
                Assert.False(StateWrite.IsMatch(line),
                    $"{Path.GetFileName(f)}:{lineNo} writes session state: {line.Trim()}");
        }
    }

    [Fact]
    public void DoorCallsInClickPaths_AlwaysRideCommandLog()
    {
        // KEY LAW twin: every click-path door call is a logged door — with the
        // bridge ON the log line IS what replays; the files the S4 input map
        // lives in may call a sim verb ONLY inside CommandLog.Call.
        var doorCall = new Regex(
            @"\.(Attack|Loot|UsePotion|Cast|TravelTo|DiscoverWaypoint|Respawn)\s*\(");
        var logged = new Regex(@"CommandLog\.Call\s*\(");
        var offenders = new List<string>();
        foreach (var f in ScriptsDir())
        {
            var name = Path.GetFileName(f);
            if (!ClickPaths.Contains(name)) continue;
            foreach (var (lineNo, line) in File.ReadAllLines(f).Select((l, i) => (i + 1, l)))
            {
                if (line.TrimStart().StartsWith("//") || !doorCall.IsMatch(line)) continue;
                if (name.StartsWith("S4InputLeg", StringComparison.Ordinal)) // driver + its Tools twin: NO session verb, ever
                    offenders.Add($"{name}:{lineNo} driver calls a session verb: {line.Trim()}");
                else if (!logged.IsMatch(line))
                    offenders.Add($"{name}:{lineNo} unlogged door call: {line.Trim()}");
            }
        }
        Assert.Empty(offenders);
    }
}
