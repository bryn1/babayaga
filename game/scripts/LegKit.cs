using System;
using System.Linq;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// THE shared leg-kit (audit MC 11907 D-2/D-3/D-5): the beat helpers the
/// scripted headless legs had forked. Ride/KillOne/LitSet lived twice (Act1OpenLeg
/// ↔ StashQuestLeg, the bodies byte-identical but for the leg's log-prefix —
/// StashQuestLeg's own comment admitted "the Act1OpenLeg KillOne shape verbatim");
/// TryDerive lived twice (StashQuestLeg ↔ HireLegGear, byte-identical but for
/// comments); the Vector2I→WorldPos InRange adapter lived SIX times (Journey,
/// Belt, Cast, Death, Quest, Regen legs — the audit's D-5 counted five; the
/// JourneyLeg original carries the same one-line body). Consolidated HERE means
/// each leg keeps its exact printed lines: `tag` IS the leg's log-prefix passed
/// in, so every PushError reads in the leg's own voice byte-for-byte. The
/// Walk beats are deliberately NOT here — the two Walk forks are NOT identical
/// (Act1OpenLeg's carries a requireDarkDest gate AND prints the spawn rows,
/// StashQuestLeg's does neither); merging near-forks whose semantics differ is
/// a behavior change, and the byte-anchor law outranks the LOC count.
/// </summary>
public static class LegKit
{
    /// Strike cap for the legs' one-kill beats (Act1OpenLeg's cap, which
    /// StashQuestLeg copied: JourneyLeg's strike cap is 64; two weak rows in
    /// reach die inside this one, the ponurak band included).
    public const int KillCap = 96;

    /// One RIDE hop: legal ONLY through the lit-stone rules. When the two grounds
    /// share no edge (kopcowisko&lt;-&gt;grodzisko), the move is a ride-ONLY proof —
    /// adjacent={False} in the line says exactly that.
    public static bool Ride(GameSession s, Act act, string dest, string tag)
    {
        string from = s.CurrentArea;
        bool adjacent = s.Areas.Get(from).LinkedTo.Contains(dest);
        bool litBoth = s.Waypoints.IsDiscovered(from) && s.Waypoints.IsDiscovered(dest);
        try { s.TravelTo(dest); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"{tag}: the stones refused the ride {from}->{dest} — {e.Message}");
            return false;
        }
        GD.Print($"RIDE from={from} to={dest} adjacent={adjacent} lit={litBoth} area={s.CurrentArea}");
        act.Rebuild(dest);
        return true;
    }

    /// One real kill: the weakest kind the spawn row carries (the JourneyLeg
    /// precedent — pokrzywnik/ponurak die inside the cap for this hero; anything
    /// stronger is DEEP flavour the leg never engages). Adjacency-gated exactly
    /// like every other leg: not adjacent = no Attack call = no marker. `where`
    /// names the ground the kill was owed on — each leg worded its own failure
    /// line ("on the goal ground" / "on the road"), byte-identical through it.
    public static bool KillOne(GameSession s, Act act, string tag, string where)
    {
        var spawns = act.Monsters.RowSnapshot();
        int idx = spawns.FindIndex(e => e.Key is "pokrzywnik" or "ponurak");
        if (idx < 0)
        {
            GD.PushError($"{tag}: {s.CurrentArea} spawned no killable weak kind — rows missing?");
            return false;
        }
        var (key, cell) = spawns[idx];
        var hero = act.Hero;
        hero.WalkTo(cell);
        while (hero.StepNext()) { }
        if (!InRange(hero.Cell, cell, 1))
        {
            GD.PushError($"{tag}: hero not adjacent to the {key} at {cell.X},{cell.Y} — no Attack call");
            return false;
        }
        int kills = 0;
        for (int strike = 1; kills == 0 && strike <= KillCap && !s.IsDead; strike++)
            kills = s.Attack(key, 1).Kills;
        if (s.IsDead || kills < 1)
        {
            GD.PushError($"{tag}: no kill {where} — dead={s.IsDead} strikes hit the cap={KillCap}?");
            return false;
        }
        GD.Print($"MONSTER_DEFEATED area={s.CurrentArea} key={key} n={kills}");
        return true;
    }

    /// The session's lit set, read through the session surface only.
    public static string LitSet(GameSession s) => string.Join(",", s.Areas.Areas
        .Select(a => a.Key).Where(k => s.Waypoints.IsDiscovered(k)).OrderBy(k => k, StringComparer.Ordinal));

    /// The InventoryPanel first-fit, mirrored (cells are a presentation-side
    /// re-derivation — the Sim answers no "which cell?" outside its command
    /// reports, ARCH v8). Returns the anchor the panel would draw `item` (last
    /// in Carried order) on. InventoryPanel's own TryDerive keeps its different
    /// (occ-array, string-spot) signature — presentation owns that shape.
    public static bool TryDerive(GameSession s, Item item, out int row, out int col)
    {
        row = col = -1;
        var occ = new bool[BabaYaga.Sim.InventoryGrid.Rows, BabaYaga.Sim.InventoryGrid.Columns];
        foreach (var it in s.Carried)
        {
            bool placed = false; // one placement per item — the panel RETURNS; the
                                 // loop must stop the same way (cloning filler items
                                 // across the grid is what this flag prevents)
            for (int r = 0; r <= occ.GetLength(0) - it.Base.Height && !placed; r++)
                for (int c = 0; c <= occ.GetLength(1) - it.Base.Width && !placed; c++)
                {
                    bool fits = true;
                    for (int i = 0; i < it.Base.Height && fits; i++)
                        for (int j = 0; j < it.Base.Width && fits; j++)
                            fits = !occ[r + i, c + j];
                    if (!fits) continue;
                    for (int i = 0; i < it.Base.Height; i++)
                        for (int j = 0; j < it.Base.Width; j++) occ[r + i, c + j] = true;
                    if (ReferenceEquals(it, item)) { row = r; col = c; }
                    placed = true;
                }
        }
        return row >= 0;
    }

    /// Cell adjacency on the ONE metric (reuses IsoCamera.InRange — the one-line
    /// Vector2I→WorldPos adapter the six legs each carried verbatim).
    public static bool InRange(Vector2I a, Vector2I b, int cells) =>
        IsoCamera.InRange(new WorldPos(a.X, a.Y), new WorldPos(b.X, b.Y), cells);
}
