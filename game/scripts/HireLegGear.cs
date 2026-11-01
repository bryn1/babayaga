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

    // ---- GEAR — the §6.7b 4-slot panel, LIVE (M4-S15, MC 11903) -------------

    /// The gear tail (spec-3 §6.7b AMENDED, MC 11898; card/11903): the bow is
    /// BOUGHT through the same Buy door the CLI's buy verb rides, its pack
    /// anchor derived PRESENTATION-side exactly like the panel (StashQuestLeg's
    /// TryDerive — the Sim answers no "which cell?"), and the give lands her
    /// FIRST weapon: GEAR_EQ echoes the door's own report (slot/item/source
    /// cell) and prints the gold after it — the trade is FREE (§6.7b T1). The
    /// DEFER row is printed too: a worn weapon changes NOTHING on her dice (the
    /// merc damage path reads the bracket table, never an Item — the base+weapon
    /// formula is deliberately NOT invented; LEDGER row 66 names the deferral).
    /// A mercenary kill AFTER the give still pays the same HIRE_XP own= chain
    /// (the XP chain is the gear's only effect and it does not move). Then the
    /// law AGAIN, this time WITH the bow on: MERC_DIES, and the gear rides the
    /// corpse (the amendment's through-death half, printed by GEAR_DIE); the
    /// stone raises the hero, the captain raises HER, and the SAME instance is
    /// still on her — GEAR_KEEP echoes the slot. One last ride to her ground,
    /// and the strip door takes the bow back straight into the hero's pack
    /// (GEAR_STRIP + carried read back). The tail rides home for the stash
    /// beat behind her again.
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
        if (!TryDerive(s, bow, out int ax, out int ay))
        {
            GD.PushError("gear beat: the pack anchor of the bought bow could not be derived");
            return false;
        }

        var g = s.GiveHirelingGear("weapon", ax, ay); // free, both directions (T1)
        long goldAfterGive = s.Gold;                 // the BUY cost above is not the give's doing
        GD.Print($"GEAR_EQ slot={g.Slot} item={g.Name} base={g.Base} from={g.InvRow},{g.InvCol} " +
                 $"gold_after={goldAfterGive} (the give moved no gold)");
        GD.Print($"GEAR_DEFER stat_effect=none dice={m.DamageMin}-{m.DamageMax} " +
                 "note=merc dice stay the bracket-table read — base+weapon is NOT invented (deferred, row 66)");
        if (g.Slot != "weapon" || g.Base != "soluga" || (g.InvRow, g.InvCol) != (ax, ay)
            || !ReferenceEquals(bow, m.Gear[MercGearSlot.Weapon]) || s.Carried.Contains(bow)
            || s.Gold != goldBefore - price)
        {
            GD.PushError($"gear give check failed: slot={g.Slot} base={g.Base} cell={g.InvRow},{g.InvCol}/{ax},{ay} " +
                         $"same={ReferenceEquals(bow, m.Gear[MercGearSlot.Weapon])} carried={s.Carried.Contains(bow)} " +
                         $"gold={s.Gold}/{goldBefore - price}");
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

    /// The InventoryPanel first-fit, mirrored (the StashQuestLeg idiom — cells
    /// are a presentation-side re-derivation; the Sim owns no "which cell?").
    private static bool TryDerive(GameSession s, Item item, out int row, out int col)
    {
        row = col = -1;
        var occ = new bool[BabaYaga.Sim.InventoryGrid.Rows, BabaYaga.Sim.InventoryGrid.Columns];
        foreach (var it in s.Carried)
        {
            bool placed = false;
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
}
