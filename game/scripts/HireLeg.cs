using System;
using System.Linq;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// The M4-S11 HIRELINGS leg (AMENDED spec §6.7, MC 11883; owner D2-oracle grant
/// 2026-10-30 verbatim on the card: "…all the tests and verifications against
/// diablo 2 lod…") — the captain's doors run through the REAL session after the
/// act-1-opens leg leaves the hero home in grodzisko. Beat order:
///   HIRE in town — `HIRE id/price/gold_before/gold_after` all read from the
///   session, plus `HIRE_PRICE_FORMULA` stating the carried-forward price rule
///   (Gold × (100 + 15 × levels_above_bracket) ÷ 100) evaluated AT THE HIRED
///   ROOKIE'S LEVEL 1: levels_above = max(0, 1−3) = 0, price = the row's Gold
///   (the hire-level pin — the clone's rookie enters service at level 1).
///   A printed HIRE_RAISE then authors the rookie UP TO its bracket row: the
///   pinned §6.7 stats below the bracket follow the engine-literal NEGATIVE
///   growth (the L1 strzelczyni's AR lands on the ≥0 clamp — a rookie that
///   can barely hit), a consequence of the adopted rows that is EXACTLY the
///   gap the test suite names (HirelingTests.BracketMerc) — the leg raises the
///   same way through the same public state door and says so.
///   `HIRE_FOLLOW` after a ride to mchy — travel = the merc follows (the area
///   mirror). The mercenary attacks: `HIRE_XP own=full=` a capped full chain
///   at ITS OWN level (chain and cap figures printed beside it, and the hero's
///   XP asserted UNMOVED), and while the merc stands nearest (default
///   CombatDistance 0, the hero's distance pinned adjacent) the whole hero
///   volley's counters land on the MERC — `HIRE_LIFE` prints hero and merc
///   pools around it with the hero UNTOUCHED asserted. A hero kill pays the
///   merc `HIRE_XP shared=floor86_256=` — 86/256 of the CAPPED figure at the
///   merc's level (the capped value printed beside it). A strong monster
///   (leszy, the area's boss row — same dice the DeathLeg's counters ride,
///   seed-fixed stream) kills the merc deterministically: `HIRE_DEAD` — dead
///   mercs strike not (`HIRE_DEAD_REJECT`, the refusal quoted) and regen not
///   (`HIRE_REGEN_DEAD` after a 3000-frame RestTick). The body rides home and
///   the captain takes `REVIVE cost=` min(15·L²/2, 50000) gold — the formula
///   evaluated beside the session-read gold pair — and the SAME mercenary
///   stands up at full life: `HIRE_ALIVE`. The S14 LAW (spec §6.7b AMENDED,
///   MC 11898 — D2: "if your character dies, your Hireling dies as well", the
///   KillPlayerPets path) closes the story: with the LIVING mercenary the hero
///   rides to mchy standing NEAREST and dies on the ponurak's counters —
///   `MERC_DIES` prints her life before/after (the fire goes out in the SAME
///   instant); the stone raises HIM home full and does NOT carry the corpse
///   (`HERO_RESPAWN`, merc_life=0 at the ground where she fell); at the
///   captain the EXACT cost raises the SAME mercenary
///   (`REVIVE_AFTER_HERO_DEATH`). The S15 GEAR TAIL (spec §6.7b, MC 11903) rides
///   last: a bow bought through the buy door rides to mchy and lands on her
///   first slot (`GEAR_EQ`, free; the DEFER row was RETIRED by §6.7c — the
///   `GEAR_STATS_OK` line names the four live stat rows, HireLegGear.cs);
///   a kill with the bow on pays the same capped chain (`HIRE_XP own=… note=gear_on`);
///   the law takes them both again and the bow RIDES THE CORPSE (`MERC_DIES`,
///   `GEAR_DIE`); the captain's revive returns the SAME instance on the SAME
///   mercenary (`GEAR_KEEP`); one ride out and the strip door hands the bow back
///   into the hero's pack (`GEAR_STRIP`, `carries_the_bow=True`) — `GEAR_OK` and
///   the tail rides home for the stash beat. HIRE_OK prints only when every
///   reading held; any failure is a PushError with no marker (the leg idiom).
/// </summary>
public static partial class HireLeg
{
    private const int StrikeCap = 500;   // the own-kill hunt; KillOne-style caps ride generous
    private const int DeadCap = 60;      // the leszy counters land hard — death is fast
    private const int HeroDeathCap = 2048; // the DeathLeg's grind ceiling — counters land death far earlier

    public static void Run(GameSession s, Act act)
    {
        if (s.CurrentArea != s.Areas.TownKey)
        {
            GD.Print($"HIRE_START area={s.CurrentArea} -> {s.Areas.TownKey} (lit stones carry the hero home)");
            s.TravelTo(s.Areas.TownKey);
            act.Rebuild(s.CurrentArea);
        }

        bool ok = Hire(s) && Follow(s, act) && OwnKill(s) && HeroUntouched(s)
            && SharedKill(s) && KillTheMerc(s) && Revive(s, act) && HeroDeathLaw(s, act)
            && GearBeat(s, act);

        if (ok) GD.Print("HIRE_OK");
        else GD.PushError("hireling leg: the captain's chain has no HIRE_OK — the lines above are the evidence");
    }

    // ---- HIRE — the town gate, the price, the rookie raise ------------------

    private static bool Hire(GameSession s)
    {
        long goldBefore = s.Gold;
        var r = s.Hire("strzelczyni");
        var row = s.Hirelings.For("strzelczyni");
        GD.Print($"HIRE id={r.Id} price={r.Price} gold_before={goldBefore} gold_after={s.Gold} " +
                 $"merc_level={r.Level} life={r.Life}");
        int above = Math.Max(0, r.Level - row.Bracket.Level);
        long spec = row.HireGold * (100 + 15 * above) / 100;
        GD.Print($"HIRE_PRICE_FORMULA gold={row.HireGold} levels_above=max(0,{r.Level}-{row.Bracket.Level})={above} " +
                 $"price={row.HireGold}x(100+15x{above})/100={spec} matches={spec == r.Price}");

        // The pinned §6.7 rookie BELOW its bracket row carries the engine-literal
        // negative growth — AR 10 + (1-3)x12 lands on the ORIGINAL-DESIGN >=0
        // clamp (a rookie that hits almost never). That IS the adoption reading,
        // stated at HirelingTests.BracketMerc; the fight beats author the rookie
        // to its bracket row through the same public state door.
        s.Merc!.Level = row.Bracket.Level;
        s.Merc.Xp = HirelingXp.XpAt(s.Merc.ExpPerLevel, s.Merc.Level);
        GD.Print($"HIRE_RAISE level={s.Merc.Level} life={s.Merc.MaxLife} ar={s.Merc.AttackRating} " +
                 $"dice={s.Merc.DamageMin}-{s.Merc.DamageMax} note=rookie raised to adopted bracket row (stated gap)");

        // The level-cap rule needs a hero ABOVE the merc's row; LevelTo is the
        // test-ladder verb (it consumes no RNG and no gold — every stream line
        // upstream of this leg is untouched by it).
        if (s.Level < 6) s.LevelTo(6);
        GD.Print($"HIRE_LADDER hero_level={s.Level} note=LevelTo is the ladder verb, RNG- and gold-free");

        return spec == r.Price && s.Gold == goldBefore - spec
            && r.Level == 1 && s.Merc.Id == "strzelczyni";
    }

    // ---- FOLLOW — travel moves the merc's area mirror -----------------------

    private static bool Follow(GameSession s, Act act)
    {
        s.TravelTo("mchy"); // the stone was lit on the journey; no discover-after-travel
        act.Rebuild(s.CurrentArea);
        GD.Print($"HIRE_FOLLOW area={s.CurrentArea} merc_area={s.Merc!.Area}");
        return s.Merc.Area == "mchy" && s.CurrentArea == "mchy";
    }

    // ---- KILL THE MERC — dead mercs neither strike nor regen ---------------

    private static bool KillTheMerc(GameSession s)
    {
        var m = s.Merc!;
        GameSession.MercStrikeReport r = null!;
        for (int i = 0; i < DeadCap; i++)
        {
            r = s.MercStrike("leszy", 1); // the area's boss row: dice the leg never dodges
            if (r.MercDead) break;
        }
        GD.Print($"HIRE_DEAD strikes={r.Strikes} merc_life={m.Life} hero_life={s.Character.Life}");
        if (!m.IsDead) return false;

        string refusal = "";
        try { s.MercStrike("pokrzywnik", 1); }
        catch (SessionRejectedException e) { refusal = e.Message; }
        GD.Print($"HIRE_DEAD_REJECT \"{refusal}\"");

        s.RestTick(3000);
        GD.Print($"HIRE_REGEN_DEAD life={m.Life}");
        return refusal.Contains("dead") && m.Life == 0;
    }

    // ---- REVIVE — the captain's price, the same mercenary, full life --------

    private static bool Revive(GameSession s, Act act)
    {
        s.TravelTo(s.Areas.TownKey);
        act.Rebuild(s.CurrentArea);
        long goldBefore = s.Gold;
        var m = s.Merc!;
        int level = m.Level;
        long xp = m.Xp;

        var r = s.Revive("strzelczyni");
        long spec = Math.Min(15L * level * level / 2, 50000);
        GD.Print($"REVIVE id={r.Id} cost={r.Cost} gold_before={goldBefore} gold_after={s.Gold} " +
                 $"formula=min(15x{level}^2/2,50000)={spec} matches={spec == r.Cost}");
        GD.Print($"HIRE_ALIVE life={r.Life} maxlife={m.MaxLife} level={m.Level} xp_kept={m.Xp == xp}");
        return spec == r.Cost && s.Gold == goldBefore - spec && r.Life == m.MaxLife
            && !m.IsDead && m.Level == level && m.Xp == xp;
    }

    // ---- HERO DEATH KILLS THE HIRELING — the S14 law, LIVE ------------------

    /// The §6.7b law through the REAL session (MC 11898 — D2 KillPlayerPets):
    /// the freshly-revived mercenary rides to mchy while the hero stands
    /// NEAREST (CombatDistance 4 — the §6.7 targeting door sends every counter
    /// to him, and the loop below asserts she stays alive strike after strike);
    /// the ponurak's counters grind his pool to 0 like the DeathLeg's beat.
    /// The SAME transition that flips IsDead takes her life — MERC_DIES prints
    /// her life before (read beside the killing strike) and after. Respawn
    /// raises the hero home at full life and leaves the corpse at mchy
    /// (HERO_RESPAWN reads both halves back from the session). The captain
    /// then charges the EXACT min(15·L²/2, 50000) to raise the SAME mercenary
    /// (REVIVE_AFTER_HERO_DEATH), and she re-engages in front again — the
    /// tail rides (StashQuestLeg) behind a living shield, not a corpse.
    private static bool HeroDeathLaw(GameSession s, Act act)
    {
        var m = s.Merc!;
        m.CombatDistance = 4;                      // the hero is the nearest valid body
        s.TravelTo("mchy");                        // the ALIVE merc follows (the mirror)
        act.Rebuild(s.CurrentArea);

        int strikes = 0;
        long before = m.Life;                      // her life the moment the killing counter lands
        for (; strikes < HeroDeathCap && !s.IsDead; strikes++)
        {
            if (m.IsDead)
            {
                GD.PushError($"hero-death beat: the merc fell at strike {strikes}, BEFORE the hero — targeting leak");
                return false;
            }
            before = m.Life;
            s.Attack("ponurak", 1);
        }
        if (!s.IsDead || !m.IsDead || m.Life != 0 || before <= 0)
        {
            GD.PushError($"hero-death beat: hero_dead={s.IsDead} merc_life={m.Life} (was {before}) after {strikes} strikes");
            return false;
        }
        GD.Print($"MERC_DIES merc_life_before={before} after={m.Life} strikes={strikes} " +
                 "note=she dies with you — the fire goes out (§6.7b, MC 11898)");

        s.Respawn();                               // the stone raises the HERO
        GD.Print($"HERO_RESPAWN hero_area={s.CurrentArea} hero_life={s.Character.Life}/{s.Character.MaxLife} " +
                 $"merc_life={m.Life} merc_area={m.Area}");
        if (s.IsDead || s.Character.Life != s.Character.MaxLife || s.CurrentArea != s.Areas.TownKey
            || m.Life != 0 || m.Area != "mchy")
        {
            GD.PushError("hero-death beat: the stone moved the hero OR carried the corpse — HERO_RESPAWN above is the evidence");
            return false;
        }

        long goldBefore = s.Gold;                  // home ALREADY — no ride needed, the stone brought him
        int level = m.Level;
        var r = s.Revive("strzelczyni");           // the captain raises her AT the ground's door
        long spec = Math.Min(15L * level * level / 2, 50000);
        GD.Print($"REVIVE_AFTER_HERO_DEATH id={r.Id} cost={r.Cost} gold_before={goldBefore} gold_after={s.Gold} " +
                 $"formula=min(15x{level}^2/2,50000)={spec} matches={spec == r.Cost}");
        m.CombatDistance = 0;                      // the merc engages in front again (the tail rides on her)
        return spec == r.Cost && s.Gold == goldBefore - spec && r.Life == m.MaxLife && !m.IsDead;
    }
}
