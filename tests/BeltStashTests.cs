using System.Text.Json;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// M4-S12 TOWN STASH — MULTI-TAB LIVE (LEDGER row 74, card/11893). D2 truth
/// adopted per the owner oracle grant 2026-10-30: the chest stands ONLY in
/// town; it has MULTIPLE TABS and extra tabs unlock through rewards — glowa's
/// StashTab reward opens tab 2 LIVE. The code truth READ before writing:
/// Store/Take gate DEAD first (the row-64 RaiseIfDead idiom, the belt's shape),
/// then on the SAME town concept the death beat resurrects into (Areas.TownKey
/// — grodzisko, the one isTown area; the Hire gate of rows 65–66 is the idiom);
/// a locked tab refuses by name; the tab storage REUSES InventoryGrid (4×10,
/// ORIGINAL DESIGN per the card — D2's 10×4 grid awaits its own data row), so
/// Store first-fits the tab like TryCarry first-fits the pack, and Take
/// re-enters the pack THROUGH TryCarry (positions read from the session's own
/// report — the Sim never answers "which cell?" outside its command reports,
/// ARCH v8). State readings pinned by rows below: NO swap-on-occupied (storing
/// never displaces; a full tab refuses by name), GOLD NOT storable (no stash
/// semantics in the oracle era — StoreGold refuses by name), multi-cell items
/// address their ANCHOR cell, tabs pay ONCE because the quest FSM refuses the
/// second QuestComplete before ApplyReward runs (the BeltQuestTests gesi idiom
/// — asserted here around tab 2 itself). The RNG law: Store/Take consume NO
/// stream word; the v6 save carries stash contents (v5 refused by name).
/// </summary>
public class BeltStashTests
{
    private const string Gem = "jantar";     // cost 40, 1×1 — the stash-side filler
    private const string LifePotion = "napitek-zycia"; // cost 60, 1×1 (belt-suite row)

    private static GameSession Fresh(ulong seed = 11) => GameSession.New(seed, "koldun", TestData.DataDir());

    /// The BeltQuestTests RunRite idiom — the FSM's own gates carry the rites.
    private static void RunRite(GameSession s, string key)
    {
        s.QuestAccept(key);
        s.QuestStep(key);
        s.QuestComplete(key);
    }

    /// glowa's prereqs (wrzeciono, dary) plus gesi, then the StashTab rite itself.
    private static void UnlockGlowa(GameSession s)
    {
        RunRite(s, "gesi");
        RunRite(s, "wrzeciono");
        RunRite(s, "dary");
        RunRite(s, "glowa");
    }

    /// Buys one row through the real vendor door and hands back the carried copy.
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

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"stash-{Guid.NewGuid():N}.save");

    // ---- round-trip: exact coords, glowa's tab 2, pack coords preserved back --

    [Fact]
    public void RoundTrip_StoreTake_ExactCoords_OnGlowaTab2()
    {
        var s = Fresh();
        UnlockGlowa(s);
        Assert.True(s.IsStashTabUnlocked(2)); // the reward opened it — the store target

        var gem = BuyOne(s, Gem);
        Assert.Equal(0, s.Carried.IndexOf(gem)); // fresh pack: first-fit anchors at 0,0

        var st = s.Store(0, 0, 2);
        Assert.Equal(Gem, st.Base);
        Assert.Equal(2, st.Tab);
        Assert.Equal((0, 0), (st.TabRow, st.TabCol)); // first-fit INSIDE the tab
        Assert.Equal((0, 0), (st.InvRow, st.InvCol)); // echoed pack anchor
        Assert.Empty(s.Carried);                      // the pack gave the item up

        // a multi-cell item addresses its ANCHOR cell on both sides (state reading)
        var club = BuyOne(s, "palica");               // 1×3, empty pack: anchor 0,0
        var cs = s.Store(0, 0, 2);
        Assert.Equal("palica", cs.Base);
        Assert.Equal((0, 1), (cs.TabRow, cs.TabCol)); // steps aside for the gem's anchor

        var tk = s.Take(2, cs.TabRow, cs.TabCol);     // the exact cells the club left
        Assert.Equal("palica", tk.Base);
        Assert.Same(club, s.Carried[^1]);             // the SAME instance rode back
        Assert.Equal((0, 0), (tk.InvRow, tk.InvCol)); // re-anchored pack (TryCarry)

        var tk2 = s.Take(2, st.TabRow, st.TabCol);    // then the gem, same exact cells
        Assert.Equal(Gem, tk2.Base);
        Assert.Same(gem, s.Carried[^1]);
        Assert.Equal((0, 1), (tk2.InvRow, tk2.InvCol)); // first-fit AROUND the club's 1×3
    }

    // ---- glowa pays tab 2 ONCE: the second complete is REFUSED (pays-once) ----

    [Fact]
    public void Glowa_UnlocksTab2_ExactlyOnce_SecondCompleteRefused()
    {
        var s = Fresh();
        Assert.True(s.IsStashTabUnlocked(1));   // tab 1 stands with the town chest
        Assert.False(s.IsStashTabUnlocked(2));  // dark until the StashTab reward

        UnlockGlowa(s);

        Assert.True(s.IsStashTabUnlocked(2));   // the reward IS the unlock
        Assert.Equal(1, s.StashTabs);           // the existing paid-tab count
        Assert.False(s.IsStashTabUnlocked(3));  // the next tab stays reward-gated

        var e = Assert.Throws<SessionRejectedException>(() => s.QuestComplete("glowa"));
        Assert.Contains("not completable", e.Message); // the FSM gate pays once
        Assert.Equal(1, s.StashTabs);                   // the refusal paid nothing
        Assert.True(s.IsStashTabUnlocked(2));           // tab 2 stays open
        Assert.False(s.IsStashTabUnlocked(3));          // and NO second tab opened
    }

    // ---- the town gate: outside grodzisko every door refuses BY NAME ----------

    [Fact]
    public void Store_Take_OutsideTown_Refused_Named()
    {
        var s = Fresh();
        var gem = BuyOne(s, Gem); // pack + purse states read around the refusals
        long gold = s.Gold;

        s.TravelTo("mchy"); // WALK one area first — the linkedTo road from known ground
        Assert.Equal("mchy", s.CurrentArea);

        var e1 = Assert.Throws<SessionRejectedException>(() => s.Store(0, 0, 1));
        Assert.Contains("town", e1.Message); // names the rule, not a stack trace
        var e2 = Assert.Throws<SessionRejectedException>(() => s.Take(1, 0, 0));
        Assert.Contains("town", e2.Message);

        Assert.Single(s.Carried);          // nothing moved on the refused paths
        Assert.Same(gem, s.Carried[0]);
        Assert.Equal(gold, s.Gold);        // and no gold moved either
    }

    // ---- the DEAD gate speaks first, in the belt's own words ------------------

    [Fact]
    public void Store_Take_WhenDead_Refused_Named()
    {
        var s = Fresh();
        UnlockGlowa(s);
        var gem = BuyOne(s, Gem);

        s.Character.TakeDamage(s.Character.Life); // the pinned trigger: Life 0 = DEAD
        Assert.True(s.IsDead);

        var e1 = Assert.Throws<SessionRejectedException>(() => s.Store(0, 0, 2));
        Assert.Contains("dead", e1.Message);
        var e2 = Assert.Throws<SessionRejectedException>(() => s.Take(2, 0, 0));
        Assert.Contains("dead", e2.Message);

        Assert.Single(s.Carried); // the corpse kept its copy until the stone raises
        Assert.Same(gem, s.Carried[0]);
    }

    // ---- tab 2 dark before the reward: refuse by name --------------------------

    [Fact]
    public void Take_LockedTab2_Refused_Named()
    {
        var s = Fresh(); // no rites run — only tab 1 stands
        Assert.False(s.IsStashTabUnlocked(2));

        var e1 = Assert.Throws<SessionRejectedException>(() => s.Take(2, 0, 0));
        Assert.Contains("locked", e1.Message);
        BuyOne(s, Gem);
        var e2 = Assert.Throws<SessionRejectedException>(() => s.Store(0, 0, 2));
        Assert.Contains("locked", e2.Message);

        Assert.Single(s.Carried); // the dark tab swallowed nothing
    }

    // ---- a full tab refuses BY NAME (no swap-on-occupied, state reading) ------

    [Fact]
    public void Store_FullTab_Refused_Named()
    {
        var s = Fresh();
        UnlockGlowa(s);

        // Fill the tab's rows 0–2 with ten 1×3 clubs: the tab first-fits row-major
        // anchor-wise, so each club takes one column and the block lands flush.
        // The eleventh finds NO anchor whose 1×3 footprint fits — the named FULL
        // refusal answers by the probe's own footprint (state reading: first-fit
        // is footprint, never compaction, never a swap — the leftover 1-high
        // strip holds no 1×3 cell, and no door re-stacks the tab to make room).
        for (int i = 0; i < 10; i++)
        {
            BuyOne(s, "palica");
            var r = s.Store(0, 0, 1);
            Assert.Equal(0, r.TabRow);
            Assert.Equal(i, r.TabCol); // exact first-fit anchors, session-read
        }

        BuyOne(s, "palica");
        var e = Assert.Throws<SessionRejectedException>(() => s.Store(0, 0, 1));
        Assert.Contains("full", e.Message);
        Assert.Single(s.Carried); // the refused club stayed in the pack
    }

    // ---- out-of-band coords refuse on every face -------------------------------

    [Fact]
    public void Store_Take_OobCoords_Refused_Named()
    {
        var s = Fresh();
        BuyOne(s, Gem);

        var e1 = Assert.Throws<SessionRejectedException>(() => s.Store(99, 0, 1));
        Assert.Contains("outside", e1.Message);   // pack-side face
        var e2 = Assert.Throws<SessionRejectedException>(() => s.Store(-1, -1, 1));
        Assert.Contains("outside", e2.Message);   // negatives are no window

        var e3 = Assert.Throws<SessionRejectedException>(() => s.Take(1, 99, 99));
        Assert.Contains("outside", e3.Message);   // tab-side face (tab 1 stands)

        Assert.Single(s.Carried); // every refusal left the state alone
    }

    // ---- the HARD RNG law: neither door consumes a stream word -----------------

    [Fact]
    public void Store_Take_SameSeed_RngWords_Unchanged()
    {
        var s = Fresh(seed: 42);
        UnlockGlowa(s);
        BuyOne(s, Gem);
        var before = RngWords(s);

        var st = s.Store(0, 0, 2);
        var mid = RngWords(s);
        s.Take(2, st.TabRow, st.TabCol);
        var after = RngWords(s);

        Assert.Equal(before, mid);   // Store rolled nothing
        Assert.Equal(before, after); // Take rolled nothing either
    }

    // ---- v6: the save carries the stash; dump round-trips byte-equal -----------

    [Fact]
    public void SaveV6_RoundTrip_KeepsStashContents_DumpEqual()
    {
        var s = Fresh(seed: 42);
        UnlockGlowa(s);
        BuyOne(s, Gem);
        var g1 = s.Store(0, 0, 1);   // tab 1 at its first-fit anchor
        BuyOne(s, LifePotion);
        var g2 = s.Store(0, 0, 2);   // tab 2 likewise
        Assert.Empty(s.Carried);

        BuyOne(s, Gem); // one copy stays carried — the pack grid rides too
        var before = s.Dump();
        var path = TempPath();
        try
        {
            s.Save(path);
            var restored = GameSession.Load(path, TestData.DataDir());
            Assert.Equal(before, restored.Dump()); // byte-identical (SaveTests idiom)

            // and the RESTORED session really answers on the stash doors
            var back = restored.Take(2, g2.TabRow, g2.TabCol);
            Assert.Equal(LifePotion, back.Base);
            Assert.Equal((g2.TabRow, g2.TabCol), (back.TabRow, back.TabCol));
            Assert.Equal(2, restored.Carried.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_V5HandBuilt_Refused_ByName()
    {
        var s = Fresh();
        UnlockGlowa(s);
        BuyOne(s, Gem);
        s.Store(0, 0, 2);
        var untouched = s.Dump();

        var path = TempPath();
        try
        {
            s.Save(path);
            var text = File.ReadAllText(path)
                .Replace($"\"version\": {SaveSerializer.SaveVersion}", "\"version\": 5");
            Assert.NotEqual(text, File.ReadAllText(path)); // the downgrade landed
            File.WriteAllText(path, text);
            var e = Assert.Throws<InvalidDataException>(() => GameSession.Load(path, TestData.DataDir()));
            Assert.Contains("save version 5 unsupported", e.Message);
            Assert.Equal(untouched, s.Dump()); // refusal touched nothing
        }
        finally { File.Delete(path); }
    }

    // ---- gold is NOT storable: the door refuses by name (state reading) --------

    [Fact]
    public void StoreGold_Refused_ByName_NoGoldMoves()
    {
        var s = Fresh();
        long gold = s.Gold;
        var e = Assert.Throws<SessionRejectedException>(() => s.StoreGold(100));
        Assert.Contains("gold", e.Message);
        Assert.Equal(gold, s.Gold); // the refusal spent nothing
    }

    // ---- a stashed potion frees its belt slot (no dangling reference) ----------

    [Fact]
    public void Store_BeltedPotion_Unbelts_TheSlot()
    {
        var s = Fresh();
        var potion = BuyOne(s, LifePotion);
        s.AddToBelt(s.Carried.IndexOf(potion), 1);
        Assert.Same(potion, s.Belt[0]);

        s.Store(0, 0, 1);
        Assert.Null(s.Belt[0]); // UsePotion can never consume a stashed phantom
    }
}
