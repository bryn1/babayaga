using System;
using System.Linq;
using BabaYaga.Sim;
using Godot;

/// <summary>
/// The XP TAIL of the hireling leg, split out of HireLeg.cs by concern the
/// way HireLegGear.cs split the gear tail (audit MC 11907 W3 — "HireLeg
/// Xp-tail", the HIRE_XP/HIRE_LIFE beat row): the own-kill full chain, the
/// nearest-body targeting beat the counters land on, and the 86/256 shared
/// chain, plus the chain/cap helper these three beats alone read. Same
/// partial class, same markers — HireLeg's header names them all.
/// </summary>
public static partial class HireLeg
{
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
