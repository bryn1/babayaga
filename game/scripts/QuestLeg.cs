using BabaYaga.Sim;
using Godot;

/// <summary>
/// The M4-S8 headless QUEST LEG — Zaginione Gęsi (The Lost Geese, data row
/// key=gesi) goes LIVE: run ONCE by Boot's headless branch after the death leg,
/// before the cast leg. Chain: QuestAccept through the FSM's OWN rules (the only
/// accept preconditions the code holds are prereqs-Done + Inactive — no presence
/// rule exists to honour) -> `QUEST_ACCEPT key=gesi phase=<phase>` -> the OBJECTIVE
/// exactly as the code defines it: the row's goalAreaKey (read from the data row,
/// never a literal) is VISITED for real on the pinned WAYPOINT ORDER CONTRACT +
/// act.Rebuild — the FSM's QuestStep is the goal-flag move itself and verifies
/// nothing about position, so standing ON the goal ground before the step is the
/// leg's no-side-channel shape (the step is printed with the area it was taken
/// from — the evidence line) -> `QUEST_STEP key=gesi area=<goal> phase=<phase>` ->
/// QuestComplete (Completable → reward-pending → Done, the reward pays inside the
/// same call) -> `QUEST_COMPLETE` + `QUEST_REWARD kind=<kind> points_before=<int>
/// points_after=<int>` — both pools read from the session around the call, the
/// rise asserted equal to the row's rewardAmount (the SkillPoint door is
/// Character.GrantSkillPoints) -> SpendSkill("iskra") through the EXISTING point
/// door the CLI spends (a SkillSpendRejected here is a named RED, the weave is
/// what the rite pays for) -> one CAST on the goal ground, adjacency-gated like
/// every other leg, printing the pinned S7 `CAST skill=iskra …` line with the
/// same debit assert (the CAST-after-reward proof) -> the stones carry the hero
/// home so the cast leg still starts in town -> QUEST_OK. HUD `QUEST_STATE
/// n=1/2/3 key=gesi phase=…` pull the HUD's OWN painted rite read-back
/// (Hud.QuestShown) AT the accept / step / complete moments; QUEST_OK prints ONLY
/// when all three pulls carried this rite's key, equalled the session's phase at
/// that moment (Active, Completable, Done — a moved sequence, so a frozen literal
/// cannot pass) AND the reward rise, the spend read-back and the cast debit held
/// (the honesty pattern of HUD_LEVEL / BELT / HUD_STATE / CAST_STATE, again).
/// </summary>
public static class QuestLeg
{
    private const string Rite = "gesi";

    public static void Run(GameSession s, Act act)
    {
        var row = s.Quests.Get(Rite).Row; // goal area, reward kind/amount: all ride the DATA row

        // ---- accept: the FSM's own gate (prereqs Done + Inactive; no other precondition exists) ----
        try { s.QuestAccept(Rite); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"quest leg: the rite refused acceptance — {e.Message}");
            return;
        }
        string pull1 = PrintHudState(s, act, 1);
        GD.Print($"QUEST_ACCEPT key={Rite} phase={s.Quests.PhaseOf(Rite)}");

        // ---- the objective: the data row's goal area, visited for real (waypoint order contract) ----
        if (s.CurrentArea != row.GoalAreaKey)
        {
            if (!s.Waypoints.IsDiscovered(row.GoalAreaKey)) s.DiscoverWaypoint(row.GoalAreaKey);
            try { s.TravelTo(row.GoalAreaKey); }
            catch (SessionRejectedException e)
            {
                GD.PushError($"quest leg: the stones refuse the goal area — {e.Message}");
                return;
            }
            act.Rebuild(row.GoalAreaKey);
        }

        bool ok = Rites(s, act, row, pull1); // markers print inside; the tail below always brings the hero home
        if (s.CurrentArea != s.Areas.TownKey) s.TravelTo(s.Areas.TownKey);
        act.Rebuild(s.CurrentArea); // the cast leg starts in town, as it always has

        if (ok) GD.Print($"QUEST_OK key={Rite} reward={row.RewardKind}");
        else GD.PushError($"quest leg: the rite {Rite} has no QUEST_OK — the lines above are the evidence");
    }

    /// <summary>Step-on-the-goal, complete, reward read-back, the point spent into
    /// iskra and ONE cast on the goal ground. True = every reading held.</summary>
    private static bool Rites(GameSession s, Act act, QuestRow row, string pull1)
    {
        // the step ON the goal ground: no side channel — the step's evidence line names the area
        if (s.CurrentArea != row.GoalAreaKey)
        {
            GD.PushError($"quest leg: stepped from '{s.CurrentArea}', the goal is '{row.GoalAreaKey}' — no evidence");
            return false;
        }
        try { s.QuestStep(Rite); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"quest leg: the step refused — {e.Message}");
            return false;
        }
        GD.Print($"QUEST_STEP key={Rite} area={s.CurrentArea} phase={s.Quests.PhaseOf(Rite)}");
        string pull2 = PrintHudState(s, act, 2);

        // complete: the FSM pays the reward inside the same call — the pools read around it
        int pointsBefore = s.Character.SkillPoints;
        try { s.QuestComplete(Rite); }
        catch (SessionRejectedException e)
        {
            GD.PushError($"quest leg: the completion refused — {e.Message}");
            return false;
        }
        int pointsAfter = s.Character.SkillPoints;
        GD.Print($"QUEST_COMPLETE key={Rite} phase={s.Quests.PhaseOf(Rite)}");
        GD.Print($"QUEST_REWARD kind={row.RewardKind} points_before={pointsBefore} points_after={pointsAfter}");
        string pull3 = PrintHudState(s, act, 3);
        if (pointsAfter != pointsBefore + row.RewardAmount)
        {
            GD.PushError($"quest leg: the rite paid {pointsAfter - pointsBefore}, the row rewards {row.RewardAmount}");
            return false;
        }

        // the rite's point pays the weave — the SAME public SpendSkill the CLI spends
        try { s.SpendSkill("iskra"); }
        catch (SkillSpendRejectedException e)
        {
            GD.PushError($"quest leg: the rite's point refused the weave — {e.Message}");
            return false;
        }
        if (s.Skills.HardPoints("iskra") != 1 || s.Character.SkillPoints != pointsAfter - 1)
        {
            GD.PushError($"quest leg: spend read-back wrong — hard={s.Skills.HardPoints("iskra")}" +
                $" pool={s.Character.SkillPoints} was={pointsAfter}");
            return false;
        }

        // the CAST-after-reward proof: one weave on the goal ground, adjacency-gated (as CastLeg)
        var spawns = act.Monsters.RowSnapshot();
        int target = spawns.FindIndex(e => e.Key == "pokrzywnik");
        if (target < 0)
        {
            GD.PushError("quest leg: the goal area spawned no pokrzywnik — no cast target");
            return false;
        }
        var (key, cell) = spawns[target];
        var hero = act.Hero;
        hero.WalkTo(cell);
        while (hero.StepNext()) { }
        if (!InRange(hero.Cell, cell, 1))
        {
            GD.PushError($"quest leg: hero not adjacent to the pokrzywnik at {cell.X},{cell.Y} — no Cast call");
            return false;
        }
        long cost = s.SkillData.Find("iskra")!.ManaCost;
        long mana0 = s.Character.Mana;
        GameSession.CastReport r;
        try { r = s.Cast("iskra", key); }
        catch (SessionRejectedException e)
        {
            act.HudPanel.Hint(e.Message);
            GD.PushError($"quest leg: the reward-paid weave refused — {e.Message}");
            return false;
        }
        GD.Print($"CAST skill=iskra mana_before={r.ManaBefore} mana_after={r.ManaAfter} dmg={r.Damage}");
        if (r.ManaBefore != mana0 || r.ManaAfter != mana0 - cost || s.Character.Mana != r.ManaAfter)
        {
            GD.PushError($"quest leg: cast debit mismatch — {r.ManaBefore}->{r.ManaAfter} expected -{cost}" +
                $" session={s.Character.Mana}");
            return false;
        }

        // the honesty tail: the three moments' HUD pulls carried THIS rite and moved Active→Completable→Done
        return PullPhase(pull1) == QuestPhase.Active.ToString()
            && PullPhase(pull2) == QuestPhase.Completable.ToString()
            && PullPhase(pull3) == QuestPhase.Done.ToString();
    }

    /// <summary>The pinned HUD read-back line — key + phase PULLED from the HUD's
    /// own paint (Hud.QuestShown), never from the session; the session twin is the
    /// QUEST_ACCEPT/STEP/COMPLETE line beside each one. Returns the painted string
    /// so the honesty tail can compare all three moments.</summary>
    private static string PrintHudState(GameSession s, Act act, int n)
    {
        act.HudPanel.Refresh();
        string pulled = act.HudPanel.QuestShown; // "key:Phase" as painted
        int sep = pulled.IndexOf(':');
        string key = sep < 0 ? pulled : pulled[..sep];
        string phase = sep < 0 ? "" : pulled[(sep + 1)..];
        GD.Print($"QUEST_STATE n={n} key={key} phase={phase}");
        return pulled;
    }

    /// <summary>The phase half of a pull, but ONLY while it carries this rite's key
    /// ("" on a wrong key or a frozen/unpainted pull — then the tail can never pass).</summary>
    private static string PullPhase(string pull)
    {
        int sep = pull.IndexOf(':');
        if (sep < 0 || pull[..sep] != Rite) return "";
        return pull[(sep + 1)..];
    }

    /// <summary>Cell adjacency on the ONE metric (reuses IsoCamera.InRange, as CastLeg).</summary>
    private static bool InRange(Vector2I a, Vector2I b, int cells) =>
        IsoCamera.InRange(new WorldPos(a.X, a.Y), new WorldPos(b.X, b.Y), cells);
}
