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
///   stands up at full life: `HIRE_ALIVE`. HIRE_OK prints only when every
///   reading held; any failure is a PushError with no marker (the leg idiom).
/// </summary>
public static class HireLeg
{
    private const int StrikeCap = 500;   // the own-kill hunt; KillOne-style caps ride generous
    private const int DeadCap = 60;      // the leszy counters land hard — death is fast

    public static void Run(GameSession s, Act act)
    {
        if (s.CurrentArea != s.Areas.TownKey)
        {
            GD.Print($"HIRE_START area={s.CurrentArea} -> {s.Areas.TownKey} (lit stones carry the hero home)");
            s.TravelTo(s.Areas.TownKey);
            act.Rebuild(s.CurrentArea);
        }

        bool ok = Hire(s) && Follow(s, act) && OwnKill(s) && HeroUntouched(s)
            && SharedKill(s) && KillTheMerc(s) && Revive(s, act);

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

    // ---- OWN KILL — the full chain at the merc's own level, capped ----------

    private static bool OwnKill(GameSession s)
    {
        var m = s.Merc!;
        long xpBefore = m.Xp;
        long heroXpBefore = s.Character.Xp;
        var (chain, cap) = SpecChainCap(s, m);

        GameSession.MercStrikeReport r = null!;
        for (int i = 0; i < StrikeCap; i++)
        {
            r = s.MercStrike("pokrzywnik", 1);
            if (r.Kills > 0) break;
        }
        long gained = m.Xp - xpBefore;
        GD.Print($"HIRE_XP own=full={gained} chain={chain} cap={cap} strikes={r.Strikes} " +
                 $"hero_xp_moved={s.Character.Xp != heroXpBefore}");
        return r.Kills == 1 && gained == Math.Min(chain, cap) && s.Character.Xp == heroXpBefore;
    }

    // ---- TARGETING — the merc stands nearest; the hero is never touched ----

    private static bool HeroUntouched(GameSession s)
    {
        var m = s.Merc!;
        long heroBefore = s.Character.Life, mercBefore = m.Life;
        s.Attack("pokrzywnik", 8); // every survivor's counter reaches the CLOSER body (distance 0)
        GD.Print($"HIRE_LIFE hero_before={heroBefore} hero_after={s.Character.Life} " +
                 $"merc_before={mercBefore} merc_after={m.Life}");
        return s.Character.Life == heroBefore && m.Life < mercBefore && !m.IsDead;
    }

    // ---- SHARED — hero kills pay the merc 86/256 of the capped figure ------

    private static bool SharedKill(GameSession s)
    {
        var m = s.Merc!;
        m.CombatDistance = 4; // stand the merc back: now the HERO is nearest and fights alone
        long xpBefore = m.Xp;
        var (chain, cap) = SpecChainCap(s, m);
        long capped = Math.Min(chain, cap);
        long spec = capped * 86 / 256;

        int kills = 0;
        for (int i = 0; i < 60 && kills == 0; i++) kills = s.Attack("pokrzywnik", 1).Kills;
        GD.Print($"HIRE_XP shared=floor86_256={m.Xp - xpBefore} capped={capped} spec={spec}");
        m.CombatDistance = 0; // the merc engages in front again (the default)
        return kills == 1 && m.Xp - xpBefore == spec;
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

    // ---- helpers -------------------------------------------------------------

    /// The full chain and the per-kill cap for the NEXT pokrzywnik the session
    /// would spawn here — the SAME resolution the session's own Spawn door runs
    /// (area level + the data's spawn rules), so the printed figures are the
    /// row the kill actually paid, read beside the session value.
    private static (long Chain, long Cap) SpecChainCap(GameSession s, Hireling m)
    {
        var area = s.Areas.Get(s.CurrentArea);
        var row = s.Monsters.Rows.First(r => r.Key == "pokrzywnik");
        var probe = s.Monsters.Spawn(row, MonsterTable.ResolveSpawnLevel(row, s.Band, area));
        long chain = HirelingXp.FullChain(s.Ladder, probe.BaseXp, probe.KillType, probe.Level, m.Level);
        return (chain, HirelingXp.PerKillCap(m.ExpPerLevel, m.Level));
    }
}
