namespace BabaYaga.Sim;

/// <summary>
/// One hired mercenary (the AMENDED §6.7, MC 11883): the session's ONE hireling
/// slot, state only — the XP rules sit beside it in <see cref="HirelingXp"/>,
/// the price and stat math in <see cref="HirelingTable"/>. The public setters
/// are the authoring/restore door (the same doctrine as Character.Life); the
/// game-rule doors are Hire/Dismiss/Revive/MercStrike on the session and
/// <see cref="Gain"/> here. Two fields are ORIGINAL DESIGN and stated as such:
/// (1) <see cref="CombatDistance"/>, the in-combat distance abstraction the
/// targeting rule reads — the sim's fight is ONE engaged monster and every
/// attack/weave verb keeps the hero adjacent to it, so the hero's distance is
/// pinned 1 (GameSession.HeroCombatDistance) and the merc's own stand-off is
/// the only distance the choice can vary; it is the same whole-unit Chebyshev
/// metric WorldPos measures, never coordinates. Default 0: the merc engages in
/// front of the hero (the D2 tank shape — a monster's counter reaches the
/// closer body first). (2) <see cref="Area"/>, the travel mirror the session
/// moves with the hero ("travel = merc follows"; the sim has no mercenary
/// walk-path — one area, one mirror field).
/// </summary>
public sealed class Hireling
{
    public HirelingRow Row { get; }
    public string Id => Row.Id;
    public string Name => Row.Name;
    public int ExpPerLevel => Row.ExpPerLevel;

    /// Level/XP/life/area/stand-off ride the v5 save block AS SAVED (the
    /// RestoreHireling door); the pools are never re-derived on load. (v7,
    /// MC 11898/S15: the worn Gear block rides beside them, same rule.)
    public int Level { get; set; } = 1;
    public long Xp { get; set; }
    public long Life { get; set; }
    public string Area { get; set; } = "";
    public int CombatDistance { get; set; }

    /// <summary>The §6.7b 4-slot panel as WORN (MC 11898): gear rides THIS object —
    /// death, travel and the captain's revive move it by identity, NO gear-keep flag
    /// exists (amendment D1: "gear is retained by object identity … unconditional").
    /// The game-rule doors are GiveHirelingGear/StripHirelingGear on the session
    /// (plus Dismiss's forfeit — the entry itself dies); the public dictionary is
    /// the authoring/restore door only (the Character.Life doctrine), used by the
    /// serializer to land the v7 block AS SAVED. A hidden default weapon is never
    /// modelled (S7 — the rookie's base weapon is a damage-table property, never
    /// an item, so there is nothing here that could be stripped or forfeited).</summary>
    public Dictionary<MercGearSlot, Item> Gear { get; } = new();

    public Hireling(HirelingRow row)
    {
        Row = row ?? throw new ArgumentNullException(nameof(row));
        // A rookie arrives holding the ladder's figure at its own level —
        // "the exp a hireling has at level L is Exp/Lvl × L² × (L+1)" (§2).
        Xp = HirelingXp.XpAt(ExpPerLevel, Level);
        Life = MaxLife;
    }

    /// The bracket-table stats at the merc's CURRENT level — computed, never
    /// cached (the Character.MaxLife idiom; per-level columns pay as L grows).
    /// <summary>The §6.7c bracket life PLUS the +life sum of worn gear (MC 11914,
    /// spec-3 §6.7c (d): D2MOO rides +life through the same aggregate stat read
    /// as the hero's, and the level-up recompute "rewrites the base list only",
    /// so the gear bonus survives it — DERIVED at the read, NOTHING new is a
    /// save field (v7 unchanged: the bonus rides the persisted Gear items' own
    /// mods). The revive and the level-up heal both read through here.</summary>
    public long MaxLife => HirelingTable.StatsAt(Row, Level).Life + GearStat("life");
    public int DamageMin => HirelingTable.StatsAt(Row, Level).DamageMin;
    public int DamageMax => HirelingTable.StatsAt(Row, Level).DamageMax;
    public long AttackRating => HirelingTable.StatsAt(Row, Level).AttackRating;

    /// <summary>The §6.7c strike envelope DERIVED from the worn panel (MC 11914;
    /// the amendment's SIMPLE rows only — the COMPLEX rows stay DEFER, LEDGER
    /// 66): a worn weapon whose base carries a damage range REPLACES the bracket
    /// dice (one range per weapon base — the fenced ORIGINAL DESIGN; an unarmed
    /// merc, or a weapon base still carrying no range — every shipped data row
    /// today — keeps the bracket dice); flat dmg_min/dmg_max on worn gear widen
    /// the rolled range BEFORE the percent (the amendment order); the weapon's
    /// own dmg_pct rides the hero primitive's weapon-ED input and the OTHER
    /// worn slots' dmg_pct sum into its ΣED input (the −90 clamp is inside
    /// CombatResolver.PhysicalDamage — THE SAME primitive the hero strikes
    /// with, never a second formula). Computed at the read like MaxLife.</summary>
    public (int Min, int Max, int WeaponEnhancedPercent, int EnhancedDamageSumPercent) StrikeStats()
    {
        var (_, bracketMin, bracketMax, _) = HirelingTable.StatsAt(Row, Level);
        Gear.TryGetValue(MercGearSlot.Weapon, out var weapon);
        int min = bracketMin, max = bracketMax;
        if (weapon is not null && weapon.Base.DamageMax >= 1)
        {
            min = weapon.Base.DamageMin; // THE SWAP — the weapon range owns the roll
            max = weapon.Base.DamageMax;
        }
        int flatMin = 0, flatMax = 0, edSum = 0;
        foreach (var worn in Gear.Values)
        {
            flatMin += worn.ModSum("dmg_min");
            flatMax += worn.ModSum("dmg_max");
            if (!ReferenceEquals(worn, weapon)) edSum += worn.ModSum("dmg_pct");
        }
        min = Math.Max(0, min + flatMin);  // the HirelingTable minima, mirrored
        max = Math.Max(min, max + flatMax);
        return (min, max, weapon?.ModSum("dmg_pct") ?? 0, edSum);
    }

    /// Σ of one mod stat over every worn item (the ModSum summing idiom, audit
    /// D-4's shape) — the gear-life read and the strike math's one door.
    private long GearStat(string stat)
    {
        long sum = 0;
        foreach (var worn in Gear.Values) sum += worn.ModSum(stat);
        return sum;
    }

    /// DEAD as the pool reads (the Character.IsDead pinned idiom, row 64).
    public bool IsDead => Life <= 0;

    public void TakeDamage(long amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Life = Math.Max(0, Life - amount);
    }

    /// <summary>Adds XP and climbs the ladder, HARD-CAPPED at
    /// min(hero level, <see cref="HirelingXp.MercLevelCap"/>): nothing gains
    /// "at or above your level" (§6.7 AMENDED — the clone's level rule). A
    /// level-up FULLY HEALS the merc ("they fully heal on level up", §4 Q3).</summary>
    public int Gain(long amount, int heroLevel)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        int cap = Math.Min(heroLevel, HirelingXp.MercLevelCap);
        if (Level >= cap) return 0;
        Xp += amount;
        int gained = 0;
        while (Level < cap && Xp >= HirelingXp.XpAt(ExpPerLevel, Level + 1))
        {
            Level++;
            gained++;
        }
        if (gained > 0) Life = MaxLife; // the level-up heal (the §4 Q3 VERIFIED row)
        return gained;
    }

    /// <summary>The resurrection price (§3, VERIFIED code + server witnesses):
    /// min(15 × L² ÷ 2, 50000) gold at the hireling's own level — the cap reads
    /// exactly as adopted (41,070 at L74; 50,000 from L82). NO corpse rule:
    /// the captain anywhere takes the coin and returns the SAME mercenary at
    /// full life, level, XP and equipment (R3/R5).</summary>
    public static long ReviveCost(int level) => Math.Min(15L * level * level / 2, 50000);
}

/// <summary>
/// The §6.7 mercenary XP chain (D2-derived numbers, adoption contract §5): the
/// hireling ladder is its OWN — "the exp a hireling has at level L is Exp/Lvl ×
/// L² × (L+1)" (§2 witness: E=120, L=85 → 74,562,000; L=86 → 77,214,240) — the
/// per-kill cap is floor(span/64) of the span the CURRENT level takes
/// ("span &gt;&gt; 6"; the amendment's worked figure: 41,441 at L85), and the
/// full chain itself is XpTable's own P1 §1.7 row computed AT THE MERC'S LEVEL
/// (d2r.org: "the chain is computed at her own level") — reused, never
/// restated. Full chain on the merc's own kill; floor(×86/256) OF THE CAPPED
/// FIGURE on hero kills ("86/256 of the capped figure", §6.7 AMENDED verbatim).
/// </summary>
public static class HirelingXp
{
    /// "cap level 98" (§6.7 AMENDED) — outlives any hero.
    public const int MercLevelCap = 98;

    /// Cumulative XP a hireling HOLDS at level L (the §2 ladder, VERIFIED).
    public static long XpAt(int expPerLevel, int level) =>
        (long)expPerLevel * level * level * (level + 1);

    /// The per-kill cap: 1/64 of the span the current level takes
    /// (XpAt(L+1) − XpAt(L)) — integer floor, the "span >> 6" shift.
    public static long PerKillCap(int expPerLevel, int level) =>
        (XpAt(expPerLevel, level + 1) - XpAt(expPerLevel, level)) / 64;

    /// The full kill-XP chain computed at the MERC'S level (players = 1, the
    /// sim's single-witch session; the whole table row reused from XpTable).
    public static long FullChain(XpTable ladder, long baseXp, KillType killType,
        int monsterLevel, int mercLevel) =>
        ladder.KillXp(baseXp, killType, players: 1, mercLevel, monsterLevel);

    /// The merc's own kill: the full chain, capped. Zero when the level gate is
    /// shut (at/above the hero, or at the 98 cap) — "nothing at or above".
    public static long OwnKillGain(XpTable ladder, Hireling merc, Monster slain, int heroLevel)
    {
        if (merc.Level >= Math.Min(heroLevel, MercLevelCap)) return 0;
        long full = FullChain(ladder, slain.BaseXp, slain.KillType, slain.Level, merc.Level);
        return Math.Min(full, PerKillCap(merc.ExpPerLevel, merc.Level));
    }

    /// A hero kill (strike OR weave — "your/minion kills"): floor(×86/256) of
    /// the SAME capped figure. The hero's own XP is untouched by this door.
    public static long SharedKillGain(XpTable ladder, Hireling merc, Monster slain, int heroLevel) =>
        OwnKillGain(ladder, merc, slain, heroLevel) * 86 / 256;
}
