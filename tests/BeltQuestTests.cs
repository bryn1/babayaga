namespace BabaYaga.Sim.Tests;

/// <summary>
/// M4-S8 QUEST LOOP LIVE (LEDGER row 62's live pass, card/11856): Zaginione Gęsi
/// (key=gesi) runs the EXISTING quest FSM end-to-end and the reward LANDS. The
/// sim is UNTOUCHED by this beat — the code truth READ before writing: QuestAccept
/// gates on the row's prereqs-Done + Inactive ONLY (no presence precondition exists);
/// QuestStep is the FSM's Active→completable goal-flag move and verifies nothing
/// about position (the headless leg satisfies the data row's goalAreaKey by really
/// visiting it — the FSM never enforced it, so this file tests the FSM's OWN gates,
/// not a location rule it does not have); QuestComplete moves Completable→reward-
/// pending→Done and pays ApplyReward INSIDE the same call — a SECOND complete hits
/// TryRequestReward from Done, throws SessionRejectedException and pays NOTHING
/// again (refused, not a silent no-op — asserted below). SkillPoint pays through
/// Character.GrantSkillPoints (the single grant door, DA round-1 P1-2); LifePotion
/// pays per the payout design as Character.FlatLife += PotionOfLifeFlat × rewardAmount
/// (+20 base life per the data note — NOT a potion item into the pack; that reading
/// is named in the RESULT); WaypointUnlock lights the goal area's stone when the
/// area has a node; StashTab counts into session.StashTabs; the remaining kinds
/// (CubeUnlock/VendorDiscount/QuiverUpgrade/BeltUpgrade/RespecRite) land on the
/// OtherRewards counter. XP math and the FSM's step semantics are READ-ONLY rows.
/// </summary>
public class BeltQuestTests
{
    private static GameSession Fresh(ulong seed = 11) => GameSession.New(seed, "koldun", TestData.DataDir());

    private static void RunRite(GameSession s, string key)
    {
        s.QuestAccept(key);
        s.QuestStep(key);
        s.QuestComplete(key);
    }

    // ---- unknown key rejected ----

    [Fact]
    public void UnknownRite_EveryVerb_Rejects()
    {
        var s = Fresh();
        Assert.Throws<SessionRejectedException>(() => s.QuestAccept("żuraw"));
        Assert.Throws<SessionRejectedException>(() => s.QuestStep("żuraw"));
        Assert.Throws<SessionRejectedException>(() => s.QuestComplete("żuraw"));
    }

    // ---- prereq unmet rejected (dary before gesi) ----

    [Fact]
    public void Dary_BeforeGesiDone_Rejects_AndStaysInactive()
    {
        var s = Fresh();
        var e = Assert.Throws<SessionRejectedException>(() => s.QuestAccept("dary"));
        Assert.Contains("cannot be accepted", e.Message);
        Assert.Equal(QuestPhase.Inactive, s.Quests.PhaseOf("dary"));

        RunRite(s, "gesi"); // unlock by finishing the prereq the FSM's own way
        s.QuestAccept("dary"); // no throw: prereqs Done
        Assert.Equal(QuestPhase.Active, s.Quests.PhaseOf("dary"));
    }

    // ---- double accept rejected ----

    [Fact]
    public void Gesi_SecondAccept_Rejects_PhaseStaysActive()
    {
        var s = Fresh();
        s.QuestAccept("gesi");
        var e = Assert.Throws<SessionRejectedException>(() => s.QuestAccept("gesi"));
        Assert.Contains("cannot be accepted", e.Message);
        Assert.Equal(QuestPhase.Active, s.Quests.PhaseOf("gesi"));
    }

    // ---- objective NOT met -> complete refused ----

    [Fact]
    public void Gesi_CompleteWithoutStep_Refused_PhaseStaysActive()
    {
        // The FSM's goal move is QuestStep (Active→completable). Skipping it — the
        // force-complete shape the run planted RED in .tmp — is refused here too:
        // the reward-pending gate is unreachable from Active.
        var s = Fresh();
        s.QuestAccept("gesi");
        var e = Assert.Throws<SessionRejectedException>(() => s.QuestComplete("gesi"));
        Assert.Contains("not completable", e.Message);
        Assert.Equal(QuestPhase.Active, s.Quests.PhaseOf("gesi"));
        Assert.Equal(0, s.Character.SkillPoints); // nothing paid on the refused path
    }

    // ---- completion flips phase per the FSM ----

    [Fact]
    public void Gesi_Completion_FlipsPhasesPerFsm()
    {
        var s = Fresh();
        Assert.Equal(QuestPhase.Inactive, s.Quests.PhaseOf("gesi"));
        s.QuestAccept("gesi");
        Assert.Equal(QuestPhase.Active, s.Quests.PhaseOf("gesi"));
        s.QuestStep("gesi");
        Assert.Equal(QuestPhase.Completable, s.Quests.PhaseOf("gesi"));
        s.QuestComplete("gesi");
        Assert.Equal(QuestPhase.Done, s.Quests.PhaseOf("gesi"));
    }

    // ---- reward paid EXACTLY ONCE: the second complete is REFUSED (throws), not a no-op ----

    [Fact]
    public void Gesi_SkillPoint_PaidExactlyOnce_SecondCompleteRefused()
    {
        var s = Fresh();
        int before = s.Character.SkillPoints;
        RunRite(s, "gesi");
        Assert.Equal(before + 1, s.Character.SkillPoints); // the row's SkillPoint × 1

        // second complete: REFUSED (SessionRejectedException — state which: it THROWS,
        // the phase never re-enters the paying edge), and the pool never moves again
        Assert.Throws<SessionRejectedException>(() => s.QuestComplete("gesi"));
        Assert.Equal(before + 1, s.Character.SkillPoints);
        Assert.Equal(QuestPhase.Done, s.Quests.PhaseOf("gesi"));

        // the point is SPENDABLE through the real door — the economy closes into S7
        s.SpendSkill("iskra");
        Assert.Equal(1, s.Skills.HardPoints("iskra"));
        Assert.Equal(before, s.Character.SkillPoints);
    }

    // ---- LifePotion pays per the payout design: FlatLife += 20 (NOT a pack item) ----

    [Fact]
    public void Wrzeciono_LifePotion_RaisesFlatLifeByTheDesign_NotIntoThePack()
    {
        var s = Fresh();
        long flatBefore = s.Character.FlatLife, maxBefore = s.Character.MaxLife;
        int carriedBefore = s.Carried.Count;

        RunRite(s, "wrzeciono"); // the data row: rewardKind LifePotion, amount 1

        Assert.Equal(flatBefore + GameSession.PotionOfLifeFlat, s.Character.FlatLife);
        Assert.Equal(maxBefore + GameSession.PotionOfLifeFlat, s.Character.MaxLife);
        Assert.Equal(carriedBefore, s.Carried.Count); // the design pays the BONUS, never an item
    }

    // ---- the unshipped reward kinds land where ApplyReward actually writes ----

    [Fact]
    public void WaypointUnlock_And_StashTab_PayTheirOwnDoors()
    {
        var s = Fresh();
        RunRite(s, "gesi");
        RunRite(s, "wrzeciono");
        RunRite(s, "dary"); // WaypointUnlock: kopcowisko's stone lights (the row's goal area)
        Assert.True(s.Waypoints.IsDiscovered("kopcowisko"));

        int tabs = s.StashTabs;
        RunRite(s, "glowa"); // StashTab: counts into StashTabs (the stash widget is row 74)
        Assert.Equal(tabs + 1, s.StashTabs);
    }
}
