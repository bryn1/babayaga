using BabaYaga.Sim;
using Godot;

/// <summary>
/// The M4-S10b headless REGEN LEG — run ONCE by Boot's headless branch after the
/// cast leg (the hero stands in mchy, alive; the cast leg drank its one belted
/// napitek-manny, so the belt is EMPTY — this leg's refill must NOT come from a
/// potion). Chain: drain to near-empty with REAL weaves (session-gated, no
/// side-channel mint) -> assert no belt refill is even available -> RestTick(3000)
/// — the explicit headless rest driver (ORIGINAL DESIGN) advancing the D2 quantum
/// of the AMENDED spec §1.8 (amendment MC 11873; 3000 frames = the 25-fps original-
/// design seconds convention) -> `REGEN mana_before=<int> mana_after=<int>
/// frames=3000`, both values session-read -> one final weave succeeds with NO
/// potion between drain and rest -> real counter-damage lands (Attack volley until
/// the Life pool drops — the row-69 counter, never a setter) -> RestTick(3000)
/// again -> `LIFE_STATIC life_before=<int> life_after=<int>` with before == after:
/// D2 RULE, life does not regenerate on its own (amended §1.8 "Life regen" — the
/// potion economy IS the design). `REGEN_OK` prints ONLY when every reading held:
/// the rest raised the mana, the post-rest weave was accepted, the belt stayed
/// empty throughout, damage really landed (life_before below MaxLife — non-vacuity)
/// AND the second rest moved the life pool by exactly nothing.
/// </summary>
public static class RegenLeg
{
    private const int RestFrames = 3000; // 120 s at the sim's 25-fps ORIGINAL-DESIGN convention
    private const int DrainCap = 24;     // safety stop for the drain loop
    private const int HurtCap = 400;     // safety stop for the counter-damage walk

    public static void Run(GameSession s, Act act)
    {
        if (s.CurrentArea != "mchy" || s.IsDead)
        {
            GD.PushError($"regen leg: expected the living cast-leg tail in mchy, got area={s.CurrentArea} dead={s.IsDead}");
            return;
        }
        long cost = s.SkillData.Find("iskra")!.ManaCost; // the DATA row drives the drain bound
        if (cost <= 0)
        {
            GD.PushError("regen leg: the data file sets iskra a free weave — the drain beat is meaningless");
            return;
        }
        if (BeltPotions(s) > 0)
        {
            GD.PushError($"regen leg: the belt still holds {BeltPotions(s)} potion(s) — the no-potion proof is poisoned");
            return;
        }

        // ---- walk the nearest pokrzywnik (the same adjacency gate as every leg) ----
        var row = act.Monsters.RowSnapshot();
        int target = row.FindIndex(e => e.Key == "pokrzywnik");
        if (target < 0)
        {
            GD.PushError("regen leg: mchy spawned no pokrzywnik — no drain or counter target");
            return;
        }
        var cell = row[target].Cell;
        var hero = act.Hero;
        hero.WalkTo(cell);
        while (hero.StepNext()) { }
        if (!LegKit.InRange(hero.Cell, cell, 1))
        {
            GD.PushError($"regen leg: hero not adjacent to the pokrzywnik at {cell.X},{cell.Y} — no verbs");
            return;
        }

        // ---- drain with REAL weaves: no potion between, the belt stays empty ----
        for (int cast = 0; cast < DrainCap && s.Character.Mana >= cost; cast++)
        {
            try { s.Cast("iskra", "pokrzywnik"); }
            catch (SessionRejectedException e)
            {
                GD.PushError($"regen leg: drain weave refused above the mana gate — {e.Message}");
                return;
            }
        }
        if (s.Character.Mana >= cost)
        {
            GD.PushError($"regen leg: drain hit the cap at mana={s.Character.Mana} >= cost={cost}");
            return;
        }

        // ---- the rest: D2 quantum per AMENDED §1.8, both values session-read ----
        long manaBefore = s.Character.Mana;
        s.RestTick(RestFrames);
        long manaAfter = s.Character.Mana;
        GD.Print($"REGEN mana_before={manaBefore} mana_after={manaAfter} frames={RestFrames}");

        // ---- the no-potion refill proof: one honest weave on the rested pool ----
        try
        {
            var r = s.Cast("iskra", "pokrzywnik");
            GD.Print($"REGEN_CAST skill=iskra mana_before={r.ManaBefore} mana_after={r.ManaAfter}");
        }
        catch (SessionRejectedException e)
        {
            GD.PushError($"regen leg: the post-rest weave was REFUSED — rest refilled nothing — {e.Message}");
            return;
        }

        // ---- the D2 life rule: real counter damage, then a rest that moves NOTHING ----
        int strikes = 0;
        while (s.Character.Life >= s.Character.MaxLife && strikes < HurtCap)
        {
            try { s.Attack("pokrzywnik", 1); }
            catch (SessionRejectedException e)
            {
                GD.PushError($"regen leg: the counter-damage walk refused — {e.Message}");
                return;
            }
            strikes++;
            if (s.IsDead)
            {
                GD.PushError("regen leg: the hero DIED to the counters — the life-static beat has no living subject");
                return;
            }
        }
        if (s.Character.Life >= s.Character.MaxLife)
        {
            GD.PushError($"regen leg: no counter damage inside HurtCap={HurtCap} — the life beat would be vacuous");
            return;
        }
        long lifeBefore = s.Character.Life;
        s.RestTick(RestFrames);
        long lifeAfter = s.Character.Life;
        GD.Print($"LIFE_STATIC life_before={lifeBefore} life_after={lifeAfter}");

        // ---- the honesty tail: every printed number was a session reading ----
        bool held = manaAfter > manaBefore && manaAfter >= cost
            && BeltPotions(s) == 0
            && lifeBefore < s.Character.MaxLife && lifeAfter == lifeBefore;
        if (held)
            GD.Print($"REGEN_OK mana={s.Character.Mana} max={s.Character.MaxMana} life={lifeAfter}");
        else
            GD.PushError($"regen leg: readings disagree — mana {manaBefore}->{manaAfter} (cost {cost})," +
                $" life {lifeBefore}->{lifeAfter} of max {s.Character.MaxLife}, belt={BeltPotions(s)}");
    }

    /// <summary>Potions left on the four belt cells — the refill must come from the
    /// rest, so the count must read 0 before AND after every beat of this leg.</summary>
    private static int BeltPotions(GameSession s)
    {
        int n = 0;
        for (int i = 0; i < GameSession.BeltSlots; i++)
            if (s.Belt[i] is not null) n++;
        return n;
    }

}
