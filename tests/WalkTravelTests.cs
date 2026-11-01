namespace BabaYaga.Sim.Tests;

/// <summary>
/// M4-S9b TRAVEL DOOR EVOLVES (ORCH-RULING 2026-11-01 + D2 oracle grant, owner
/// verbatim: "all the tests and verifications against diablo 2 lod and continue
/// working towards dod", 2026-10-30): in D2 the world is WALKED — waypoints are
/// optional fast-travel that light only where stones stand. The ONE travel door,
/// GameSession.TravelTo, is legal if EITHER (a) WALK — the target is a direct
/// linkedTo neighbour of a KNOWN ground (a lit stone stands at either end of the
/// road; the walking hero lights NOTHING, burns nothing, rolls nothing, no rng) —
/// OR (b) RIDE — the existing lit-stone rules UNCHANGED (town hub included).
/// The walls stay: WaypointGraph.Load refuses stones in waypoint-less areas,
/// Discover throws for node-less areas, and a stone-less NON-adjacent pair is
/// still refused. Walking never lights: the lit set is equal before/after, and
/// DiscoverWaypoint on a stone-less area still rejects.
/// </summary>
public class WalkTravelTests
{
    private static GameSession Fresh(ulong seed = 11) => GameSession.New(seed, "koldun", TestData.DataDir());

    /// The session's lit set read THROUGH the session surface: every area key the
    /// waypoint graph reports discovered. Never a literal.
    private static string LitSet(GameSession s) => string.Join(",", s.Areas.Areas
        .Select(a => a.Key).Where(k => s.Waypoints.IsDiscovered(k)).OrderBy(k => k, StringComparer.Ordinal));

    // ---- WALK: adjacency travels, lights nothing ----

    [Fact]
    public void Walk_AdjacentStonelessTarget_TravelsAndLightsNothing()
    {
        var s = Fresh(); // the town stone (grodzisko) lights on arrival — nothing else
        string before = LitSet(s);

        s.TravelTo("pokrzywlasy"); // linkedTo neighbour of grodzisko, NO stone there

        Assert.Equal("pokrzywlasy", s.CurrentArea);
        Assert.Equal(before, LitSet(s)); // the walk lit NOTHING — the set is equal
        Assert.False(s.Waypoints.IsDiscovered("pokrzywlasy")); // and never can be

        // the hero is never stranded: the road home rides the KNOWN end of the edge.
        s.TravelTo("grodzisko");
        Assert.Equal("grodzisko", s.CurrentArea);
        Assert.Equal(before, LitSet(s));
    }

    [Fact]
    public void Walk_NonAdjacentUnlitPairs_StayRefused_NoStateChange()
    {
        var s = Fresh();
        string before = LitSet(s);

        // grodzisko <-> krecie: NOT linkedTo neighbours (path crosses mchy), krecie dark.
        Assert.Throws<SessionRejectedException>(() => s.TravelTo("krecie"));
        // grodzisko <-> widlan: NOT neighbours AND stone-less — refused by both doors.
        Assert.Throws<SessionRejectedException>(() => s.TravelTo("widlan"));

        Assert.Equal("grodzisko", s.CurrentArea); // refusals change nothing
        Assert.Equal(before, LitSet(s));
    }

    [Fact]
    public void Walk_DarkToDark_WanderRefused_TheLitEndpointConcession()
    {
        // THE CONCESSION (ORCH-RULING 2026-11-01 addendum, parent ruling this card):
        // walk requires a LIT ENDPOINT — dark<->dark wander stays refused (D2
        // full-walk fidelity deferred as its own future LEDGER row). This pins the
        // concession as a tested contract, not folklore: pokrzywlasy and widlan are
        // linkedTo NEIGHBOURS and both stay dark — the edge is real, the road closed.
        var s = Fresh();
        s.TravelTo("pokrzywlasy"); // legal: the grodzisko end of the edge is known
        Assert.Contains("widlan", s.Areas.Get("pokrzywlasy").LinkedTo); // the edge exists
        var ex = Assert.Throws<SessionRejectedException>(() => s.TravelTo("widlan"));
        Assert.Contains("no walked road", ex.Message);
        Assert.Equal("pokrzywlasy", s.CurrentArea);
    }

    [Fact]
    public void Walk_CannotLight_DiscoverStillThrowsForNodelessAreas()
    {
        var s = Fresh();
        s.TravelTo("pokrzywlasy"); // walk in (adjacent, known end)
        Assert.False(s.Waypoints.IsDiscovered("pokrzywlasy")); // the walk lit nothing

        // The ruled walls UNCHANGED: the session door rejects, the graph throws.
        Assert.Throws<SessionRejectedException>(() => s.DiscoverWaypoint("pokrzywlasy"));
        Assert.Throws<ArgumentException>(() => s.Waypoints.Discover("pokrzywlasy"));
        Assert.False(s.Waypoints.HasNode("pokrzywlasy"));
    }

    [Fact]
    public void Walk_IntoDarkStoneGround_TheStoneStaysDark()
    {
        // Walking a real edge INTO an area that HAS a standing stone — never lit
        // by the walk. (The planted-red shape: a TravelTo that lights on arrival
        // turns this test RED.)
        var s = Fresh(); // only grodzisko lit; mchy has a stone, dark
        string before = LitSet(s);

        s.TravelTo("mchy"); // grodzisko<->mchy is a linkedTo edge; the walk door carries it

        Assert.Equal("mchy", s.CurrentArea);
        Assert.False(s.Waypoints.IsDiscovered("mchy")); // walk-into a dark stone: still dark
        Assert.Equal(before, LitSet(s));
        s.TravelTo("grodzisko"); // home rides the known end of the same edge
        Assert.Equal("grodzisko", s.CurrentArea);
    }

    // ---- RIDE: the existing lit-stone rules, unchanged ----

    [Fact]
    public void Ride_LitStoneJourney_Traverses_HubAndChainRulesUnchanged()
    {
        var s = Fresh();
        s.DiscoverWaypoint("mchy");
        s.DiscoverWaypoint("krecie");
        s.DiscoverWaypoint("kopcowisko");

        // hub rule UNCHANGED: town -> any lit stone, one hop, NOT adjacent (grodzisko
        // links only mchy/pokrzywlasy) — this travel is ride-only, walk cannot reach.
        s.TravelTo("kopcowisko");
        Assert.Equal("kopcowisko", s.CurrentArea);

        // …and back over the same lit stones.
        s.TravelTo("grodzisko");
        Assert.Equal("grodzisko", s.CurrentArea);

        // dark target, no edge, no lit path: still refused (target czelusc has a
        // node but stays dark; kopcowisko is the only neighbour, also unlit-side).
        Assert.Throws<SessionRejectedException>(() => s.TravelTo("czelusc"));
        s.TravelTo("mchy"); // lit neighbour, also a hub stone — rides as always
        Assert.Throws<SessionRejectedException>(() => s.TravelTo("czelusc")); // czelusc dark, mchy not its neighbour
    }

    // ---- WALK burns nothing: the combat stream position survives the road ----

    [Fact]
    public void Walk_RollsNothing_TheCombatStreamSurvivesTheRoad()
    {
        var walked = Fresh(seed: 4);
        var stayed = Fresh(seed: 4);

        walked.TravelTo("pokrzywlasy"); // out on foot…
        walked.TravelTo("grodzisko");   // …and home again

        // the SAME five strikes on both sessions: the full value-equal report +
        // byte-equal dumps prove the walk burned no rng (a walk that rolled
        // anything would shift the streams and the strikes would diverge).
        var a = walked.Attack("pokrzywnik", 5);
        var b = stayed.Attack("pokrzywnik", 5);
        Assert.Equal(b, a);
        Assert.Equal(stayed.Dump(), walked.Dump());
    }
}
