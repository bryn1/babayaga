using System;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// The GEAR TAIL of the hireling leg, split out of HireLeg.cs by concern (the
/// coding-discipline 400-line law): the §6.7b 4-slot panel beat rides its own
/// file as the SAME partial class — its markers are named in HireLeg's header.
/// M4-S15, card/11903, spec-3 §6.7b AMENDED (MC 11898).
/// </summary>
public static partial class HireLeg
{

    // ---- GEAR — the §6.7b 4-slot panel + §6.7c stats, LIVE (M4-S15/S16) ------

    /// The gear tail (spec-3 §6.7b AMENDED, MC 11898; card/11903; the stat rows
    /// §6.7c AMENDED, MC 11914): the bow is BOUGHT through the same Buy door
    /// the CLI's buy verb rides, its pack anchor derived PRESENTATION-side
    /// exactly like the panel (StashQuestLeg's TryDerive — the Sim answers no
    /// "which cell?"), and the give lands her FIRST weapon: GEAR_EQ echoes the
    /// door's own report (slot/item/source cell) and prints the gold after it —
    /// the trade is FREE (§6.7b T1). The DEFER row RETIRED with §6.7c: the
    /// GEAR_STATS_OK line names the four LIVE rows (swap/ED/flat/life) and the
    /// still-DEFERRED COMPLEX rows, and the STATS block echoes the sim's own
    /// derived StrikeStats/MaxLife reads — the REAL bought bow shows the
    /// bracket dice REPLACED (GEAR_DICE base=… now=…: the soluga row carries
    /// its damage range since the D1 data beat — row 66 data-future-WEAPONS
    /// CLOSED, no authoring door, no RNG), the amendment's integer order
    /// prints exact on PINNED synthetic ranges (ED 7→10, flat-before-% 13→19,
    /// the −90 clamp 20→2), and a worn +life raises MaxLife (GEAR_LIFE). The
    /// ED/flat/life witnesses STAY synthetic — no ED%/flat/+life DATA rows
    /// reach merc gear through the shipped data yet (the named data-future
    /// that row 66 keeps open); their mods ride the AUTHORING door (the real
    /// Gear dict + public Mods list, the Character.Life doctrine) and roll
    /// NO RNG. A mercenary kill
    /// AFTER the block still pays the same HIRE_XP own= chain (the XP chain
    /// does not move). Then the law AGAIN, this time WITH the bow on:
    /// MERC_DIES, and the gear rides the corpse (the amendment's through-death
    /// half, printed by GEAR_DIE); the stone raises the hero, the captain
    /// raises HER, and the SAME instance is still on her — GEAR_KEEP echoes
    /// the slot. One last ride to her ground, and the strip door takes the bow
    /// back straight into the hero's pack (GEAR_STRIP + carried read back).
    /// The tail rides home for the stash beat behind her again.
    private static bool GearBeat(GameSession s, Act act)
    {
        var m = s.Merc!;
        long goldBefore = s.Gold;
        s.Buy("soluga"); // the CLI's buy door, inside the leg — a REAL item on a real price
        var bow = s.Carried[^1];
        long price = goldBefore - s.Gold;
        GD.Print($"GEAR_BUY base={bow.Base.Id} price={price} gold_after={s.Gold}");

        s.TravelTo("mchy");                         // the ALIVE merc follows (mirror)
        act.Rebuild(s.CurrentArea);
        if (!LegKit.TryDerive(s, bow, out int ax, out int ay))
        {
            GD.PushError("gear beat: the pack anchor of the bought bow could not be derived");
            return false;
        }

        var g = s.GiveHirelingGear("weapon", ax, ay); // free, both directions (T1)
        long goldAfterGive = s.Gold;                 // the BUY cost above is not the give's doing
        GD.Print($"GEAR_EQ slot={g.Slot} item={g.Name} base={g.Base} from={g.InvRow},{g.InvCol} " +
                 $"gold_after={goldAfterGive} (the give moved no gold)");
        if (g.Slot != "weapon" || g.Base != "soluga" || (g.InvRow, g.InvCol) != (ax, ay)
            || !ReferenceEquals(bow, m.Gear[MercGearSlot.Weapon]) || s.Carried.Contains(bow)
            || s.Gold != goldBefore - price)
        {
            GD.PushError($"gear give check failed: slot={g.Slot} base={g.Base} cell={g.InvRow},{g.InvCol}/{ax},{ay} " +
                         $"same={ReferenceEquals(bow, m.Gear[MercGearSlot.Weapon])} carried={s.Carried.Contains(bow)} " +
                         $"gold={s.Gold}/{goldBefore - price}");
            return false;
        }

        // ---- STATS LIVE — the §6.7c SIMPLE rows, the DEFER retired -----------
        // (M4-S16, MC 11914; spec-3 §6.7c AMENDED, D2MOO 1a30fbe4. The D1 data
        // beat (MC 11928) moved the SWAP witness to REAL data: item-bases.json
        // carries damageMin/damageMax on EVERY weapon-kind base (price-ordered
        // ORIGINAL balance, curve in the json note), so the bow bought through
        // the Buy door IS the swap — no synthetic base, no authoring door.
        // (b)–(d) stay synthetic PINS: the ED%/flat/+life DATA rows are the
        // data-future row 66 keeps open, and their lines say so. Every echo
        // below reads the sim's OWN derived state, never a restated formula.
        // No strike, no RNG: the beats behind this one keep their exact bytes.
        GD.Print("GEAR_STATS_OK swap=live ed=live flat=live life=live " +
                 "defer=AR_to_hit,block,skills,IAS,procs note=6.7c MC 11914 — the §6.7b DEFER retires; COMPLEX rows named, LEDGER 66");

        // (a) THE SWAP — the REAL bought bow already owns the dice (D1 data).
        var (sMin, sMax, sWeaponEd, sEdSum) = m.StrikeStats();
        GD.Print($"GEAR_DICE base={m.DamageMin}-{m.DamageMax} now={sMin}-{sMax} " +
                 $"note=REAL soluga row {bow.Base.DamageMin}-{bow.Base.DamageMax} REPLACES the bracket dice " +
                 "(D1 data beat, row 66 data-future-weapons CLOSED)");
        if (sMin != bow.Base.DamageMin || sMax != bow.Base.DamageMax || sWeaponEd != 0 || sEdSum != 0
            || (sMin == m.DamageMin && sMax == m.DamageMax)) // the swap must be OBSERVABLE vs the bracket (soluga ≠ the entry rows)
        {
            GD.PushError($"stats beat: swap echo {sMin}-{sMax}/{sWeaponEd}/{sEdSum} expected the real bow {bow.Base.DamageMin}-{bow.Base.DamageMax}/0/0 (distinct from the bracket {m.DamageMin}-{m.DamageMax})");
            return false;
        }

        // (b)+(c) ED % with the −90 clamp, and flat BEFORE the % — pinned weapon
        // bases so the amendment's integer order lands on exact floors: 7×150/100
        // = 10; (10+3)×150/100 = 19; 20×(100−90)/100 = 2 (a −95 sum clamps to −90).
        // StrikeStats answers the PRE-ROLL envelope (what the dice roll IN); the
        // exact figure is what the ONE hero primitive then floors it to, replayed
        // here at the envelope's min==max pin — the tests prove the roll lands on
        // these same integers through the real MercStrike.
        static long Roll(long min, long max, int weaponEd, long flat, int edSum) =>
            CombatResolver.PhysicalDamage(new Rng(0), (int)min, (int)max, weaponEd,
                flat, edSum); // the REAL primitive on a throwaway stream — min==max pins the roll
        var body = new Item(s.Bases.For("watan"), 1);
        body.Mods.Add(new ItemMod("dmg_pct", 50));
        m.Gear[MercGearSlot.Body] = body;
        m.Gear[MercGearSlot.Weapon] = new Item(bow.Base with { DamageMin = 7, DamageMax = 7 }, 1);
        var (edMin, edMax, edW, edSum) = m.StrikeStats();
        long edHit = Roll(edMin, edMax, edW, 0, edSum);
        GD.Print($"GEAR_ED pct=50 envelope={edMin}-{edMax} strike={edHit} expect=10 " +
                 "note=floor(7×150/100), the ΣED path — synthetic pin, ED data still data-future (row 66)");
        body.Mods.Add(new ItemMod("dmg_min", 3));
        body.Mods.Add(new ItemMod("dmg_max", 3));
        m.Gear[MercGearSlot.Weapon] = new Item(bow.Base with { DamageMin = 10, DamageMax = 10 }, 1);
        var (flMin, flMax, flW, flSum) = m.StrikeStats();
        long flHit = Roll(flMin, flMax, flW, 0, flSum);
        GD.Print($"GEAR_FLAT flat=3 pct=50 envelope={flMin}-{flMax} strike={flHit} expect=19 " +
                 "note=flat joins the range BEFORE the %");
        body.Mods.Clear();
        body.Mods.Add(new ItemMod("dmg_pct", -95));
        m.Gear[MercGearSlot.Weapon] = new Item(bow.Base with { DamageMin = 20, DamageMax = 20 }, 1);
        var (clMin, clMax, clW, clEdSum) = m.StrikeStats();
        long clHit = Roll(clMin, clMax, clW, 0, clEdSum);
        GD.Print($"GEAR_CLAMP pct=-95 envelope={clMin}-{clMax} strike={clHit} expect=2 " +
                 "note=ΣED floor −90: 20×10/100");
        if (edMin != 7 || edMax != 7 || edHit != 10 || flMin != 13 || flMax != 13 || flHit != 19
            || clMin != 20 || clMax != 20 || clHit != 2 || clEdSum != -95)
        {
            GD.PushError($"stats beat: arithmetic echo {edMin}-{edMax}/{edHit} {flMin}-{flMax}/{flHit} " +
                         $"{clMin}-{clMax}/{clHit}/{clEdSum} expected 7-7/10 13-13/19 20-20/2/-95 " +
                         "(the −90 floor is INSIDE the primitive)");
            return false;
        }

        // (d) +LIFE — worn +life raises MaxLife, derived (no save field, v7 kept).
        body.Mods.Clear();
        body.Mods.Add(new ItemMod("life", 25));
        long bracketLife = HirelingTable.StatsAt(m.Row, m.Level).Life;
        GD.Print($"GEAR_LIFE base={bracketLife} now={m.MaxLife} note=+25 worn life, derived at the read");
        if (m.MaxLife != bracketLife + 25)
        {
            GD.PushError($"stats beat: life echo base={bracketLife} now={m.MaxLife} expected +25 exactly");
            return false;
        }

        // Restore the REAL bow, drop the synthetic body — the beats behind (the
        // XP chain, the death, the keep, the strip, the sell) run exactly the
        // §6.7b shape again: the real soluga row carries its REAL range (D1),
        // so the shipped-data swap stands — the echo reads the bow's own
        // range, never the bracket.
        m.Gear[MercGearSlot.Weapon] = bow;
        m.Gear.Remove(MercGearSlot.Body);
        var (backMin, backMax, backWeaponEd, backEdSum) = m.StrikeStats();
        GD.Print($"GEAR_DICE base={m.DamageMin}-{m.DamageMax} now={backMin}-{backMax} " +
                 "note=restored REAL bow owns the dice — shipped-data swap (row 66 weapons CLOSED; ED/flat/life data-future stays)");
        if (!ReferenceEquals(bow, m.Gear[MercGearSlot.Weapon]) || m.Gear.Count != 1
            || backMin != bow.Base.DamageMin || backMax != bow.Base.DamageMax || backWeaponEd != 0 || backEdSum != 0)
        {
            GD.PushError("stats beat: the synthetic witness was not restored clean");
            return false;
        }

        // A kill with the bow on pays the SAME capped chain (the gear law moves
        // no XP and no RNG): the OwnKill shape re-run beside its spec figures.
        long xpBefore = m.Xp;
        var (chain, cap) = SpecChainCap(s, m);
        GameSession.MercStrikeReport r = null!;
        for (int i = 0; i < StrikeCap; i++)
        {
            r = s.MercStrike("pokrzywnik", 1);
            if (r.Kills > 0) break;
        }
        long gained = m.Xp - xpBefore;
        GD.Print($"HIRE_XP own=full={gained} chain={chain} cap={cap} strikes={r.Strikes} note=gear_on");
        if (r.Kills != 1 || gained != Math.Min(chain, cap)) return false;

        // The law, this time WITH the bow: she falls wearing it and keeps it.
        m.CombatDistance = 4;                       // the hero is the nearest body again
        int strikes = 0;
        for (; strikes < HeroDeathCap && !s.IsDead; strikes++) s.Attack("ponurak", 1);
        if (!s.IsDead || !m.IsDead || !ReferenceEquals(bow, m.Gear[MercGearSlot.Weapon]))
        {
            GD.PushError($"gear beat: hero_dead={s.IsDead} merc_life={m.Life} gear_held={m.Gear.Count} after {strikes} strikes");
            return false;
        }
        GD.Print($"MERC_DIES strikes={strikes} merc_life={m.Life} note=she dies with you — and WITH her gear (§6.7b D1)");
        GD.Print($"GEAR_DIE slot=weapon item={m.Gear[MercGearSlot.Weapon].DisplayName} alive=False " +
                 "note=gear stays ON the corpse — strip of the dead is refused (HirelingGearTests)");

        s.Respawn();                                // the stone raises the HERO home
        var rv = s.Revive("strzelczyni");           // the captain raises the SAME mercenary
        GD.Print($"GEAR_KEEP slot=weapon item={m.Gear[MercGearSlot.Weapon].DisplayName} " +
                 $"alive={m.Gear[MercGearSlot.Weapon] is not null && !m.IsDead} cost={rv.Cost} gold_after={s.Gold}");
        m.CombatDistance = 0;                       // the tail rides behind a living shield again
        if (!ReferenceEquals(bow, m.Gear[MercGearSlot.Weapon]) || m.IsDead) return false;

        s.TravelTo("mchy");                         // she stands at mchy — ride to her (mirror follows)
        act.Rebuild(s.CurrentArea);
        var t = s.StripHirelingGear("weapon");      // take-back door, straight into the pack
        GD.Print($"GEAR_STRIP slot={t.Slot} item={t.Name} base={t.Base} into={t.InvRow},{t.InvCol} " +
                 $"carried={s.Carried.Count} carries_the_bow={s.Carried.Contains(bow)}");
        if (t.Slot != "weapon" || !s.Carried.Contains(bow) || m.Gear.Count != 0) return false;

        s.TravelTo(s.Areas.TownKey);                // home again — the stash beat starts in town
        act.Rebuild(s.CurrentArea);
        // The hero hocks the bow he just took off (the Arreat counsel in action:
        // the gear came OFF before the mercenary ever stood dismissed) — SellLast
        // sells the pack's last item, the bow the strip door just re-entered.
        if (!ReferenceEquals(bow, s.Carried[^1]))
        {
            GD.PushError("gear beat: the strip did not re-enter the bow as the pack's last item");
            return false;
        }
        long sellGoldBefore = s.Gold;
        long back = s.SellLast();
        GD.Print($"GEAR_SELL base=soluga price={back} gold_after={s.Gold} sold_the_bow={s.Gold == sellGoldBefore + back}");
        GD.Print("GEAR_OK");
        return true;
    }
}
