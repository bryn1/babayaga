namespace BabaYaga.Sim;

/// <summary>
/// The hireling half of the session (sibling of the core file's command
/// contract, LEDGER rows 65–66, M4-S11): the ONE mercenary slot + the four
/// captain doors. Same contract everywhere — every command either fully applies
/// or is rejected through SessionRejectedException with nothing half-applied.
/// Gates in the pinned order: Hire = DEAD gate (row 64) → town gate → row/act →
/// one-merc gate → gold through the existing Purse.TrySpend; Dismiss pays
/// NOTHING back (the LoD rule — "no refund" was a decision, never a drop —
/// amendment §5); Revive = town + cost at the merc's own level (the same merc
/// returns at full life, level and XP — NO corpse rule, R3); travel = merc
/// follows (the Area mirror in GameSession.World). MercStrike is the merc's
/// own attack: rolls run on the COMBAT stream as the attacker (the brief's
/// RNG pin — a mercenary is a player-side attacker, not an EnemyRng body), and
/// a survivor counters through the SAME row-69 door, where the §6.7 targeting
/// rule may hand the counter to the merc instead of the hero. With NO merc
/// hired every seam below is inert and every stream costs exactly what it cost
/// before §6.7 (the HARD RNG law — pinned by the HirelingTests capture rows).
/// </summary>
public sealed partial class GameSession
{
    /// The session's ONE hireling (the one-merc gate), or null.
    public Hireling? Merc { get; private set; }

    /// The engaged monster stands ADJACENT to the hero — every attack/weave
    /// verb requires it before the swing lands (the melee band, and the legs'
    /// adjacency gate) — so the hero's in-combat distance to the fight is
    /// pinned 1 for the §6.7 targeting choice. ORIGINAL DESIGN (the sim's
    /// fight abstraction; see Hireling.CombatDistance for the whole model).
    public const int HeroCombatDistance = 1;

    /// ORIGINAL DESIGN: the clone's rookie enters service at level 1 and grows
    /// through the XP chain (the hire price formula reads its bracket at
    /// levels_above = max(0, 1 − bracket) = 0, i.e. price = the row's Gold).
    public const int HireLevel = 1;

    /// The monstats DamageRegen = 4 the amendment §4 Q1 pins as the Acts-1–3
    /// witness — THE CONSTANT GAP, named per amendment §4 Q3: the D2MOO engine
    /// line reads a ÷2000-of-maximum model that CONTRADICTS the table reading,
    /// so which constant the real engine ran is not pinned; the BEHAVIOUR
    /// (empty→full 41 s = 4096/4 frames) is VERIFIED, and this adopts the
    /// DamageRegen-4 ÷4096 reading exactly as the brief pins it.
    public const int MercDamageRegen = 4;

    public sealed record HireReport(string Id, string Name, int Level, long Price, long GoldAfter, long Life);
    public sealed record ReviveReport(string Id, long Cost, long GoldAfter, long Life);
    public sealed record MercStrikeReport(
        string Attacker, int Strikes, int Kills, bool MercDead, long XpGranted, long MercLife, long MercLifeMax);

    /// Hires the named row (the town's captain). Gates in order: DEAD (row 64,
    /// the gate speaks first in every verb) → town → unknown id → the row's act
    /// vs this world's act (witez/rzutca are gated by the NOT-BUILT acts, the
    /// ledger's reading) → the one-merc gate (a DEAD merc is replaceable —
    /// "hire a new one … forfeiting" R3) → gold through TrySpend.
    public HireReport Hire(string id)
    {
        RaiseIfDead();
        if (CurrentArea != Areas.TownKey)
            throw new SessionRejectedException(
                $"hirelings are hired in town — the captain is not in '{CurrentArea}'");
        var row = Hirelings.Find(id)
            ?? throw new SessionRejectedException($"there is no hireling named '{id}'");
        int worldAct = Areas.Get(CurrentArea).Act;
        if (row.Act != worldAct)
            throw new SessionRejectedException($"'{id}' serves act {row.Act} — this is act {worldAct}");
        if (Merc is not null && !Merc.IsDead)
            throw new SessionRejectedException(
                $"'{Merc.Id}' rides with you — dismiss or revive before you hire");

        long price = HirelingTable.HirePrice(row, HireLevel);
        if (!Purse.TrySpend(price))
            throw new SessionRejectedException($"not enough gold: {Purse.Amount} below the price {price}");

        Merc = new Hireling(row) { Area = CurrentArea };
        return new HireReport(row.Id, row.Name, Merc.Level, price, Purse.Amount, Merc.Life);
    }

    /// Sends the mercenary away at the captain — FREE, and what the hire price
    /// paid is NOT returned (the LoD no-refund rule the amendment keeps). The
    /// one-merc gate clears with it; a dead merc can be dismissed too (the
    /// replacement hire already paid the fresh price, R3).
    public string Dismiss()
    {
        RaiseIfDead();
        if (CurrentArea != Areas.TownKey)
            throw new SessionRejectedException(
                $"hirelings are dismissed in town — the captain is not in '{CurrentArea}'");
        var m = Merc ?? throw new SessionRejectedException("no hireling rides with you");
        Merc = null;
        return m.Id;
    }

    /// Pays the captain min(15·L²/2, 50000) at TOWN and returns the SAME
    /// mercenary at full life — level, XP and identity intact (R5: "a revive
    /// brings the same mercenary back at full life"; there is NO corpse run).
    public ReviveReport Revive(string id)
    {
        RaiseIfDead();
        if (CurrentArea != Areas.TownKey)
            throw new SessionRejectedException(
                $"hirelings are revived in town — the captain is not in '{CurrentArea}'");
        var m = Merc ?? throw new SessionRejectedException("no hireling rides with you");
        if (m.Id != id)
            throw new SessionRejectedException($"no hireling named '{id}' rides with you");
        if (!m.IsDead)
            throw new SessionRejectedException($"'{id}' is not dead — nothing to revive at {m.Life} life");

        long cost = Hireling.ReviveCost(m.Level);
        if (!Purse.TrySpend(cost))
            throw new SessionRejectedException($"not enough gold: {Purse.Amount} below the revive cost {cost}");

        m.Life = m.MaxLife;
        return new ReviveReport(id, cost, Purse.Amount, m.Life);
    }

    /// <summary>The mercenary attacks the engaged monster (engagement handled
    /// exactly as Attack holds it). Each strike rolls on the COMBAT stream —
    /// the merc is a player-side attacker (hit roll → crit-family rolls →
    /// damage roll, the same primitive order ResolvePlayerStrike pays, no
    /// ladder grant inside): a kill pays the FULL chain at the merc's own level
    /// through HirelingXp.OwnKillGain (capped), the drop chain runs the SAME
    /// KillFlow.OnKill Cast and Attack feed, and the hero is paid NOTHING.
    /// A survivor counters once through the row-69 door — where §6.7 targeting
    /// may reach the merc. The DEAD gate stops the volley (hero or merc); the
    /// report carries what actually happened.</summary>
    public MercStrikeReport MercStrike(string monsterKey, int times)
    {
        RaiseIfDead(); // a dead hero gives no orders (the row-64 gate)
        var m = Merc ?? throw new SessionRejectedException("no hireling rides with you");
        if (m.IsDead) throw new SessionRejectedException($"'{m.Id}' is dead — a corpse never attacks");
        if (times < 1) throw new SessionRejectedException("merc strikes must be at least 1");
        var row = Monsters.Rows.FirstOrDefault(r => r.Key == monsterKey)
            ?? throw new SessionRejectedException($"unknown monster '{monsterKey}'");
        if (Engaged is not null && EngagedKey != monsterKey) { Engaged = null; EngagedKey = null; }
        var area = Areas.Get(CurrentArea);
        Engaged ??= Spawn(row, area);

        int strikes = 0, kills = 0;
        long xp = 0;
        for (int i = 0; i < times && !IsDead && !m.IsDead; i++)
        {
            strikes++;
            bool hit = CombatResolver.RollHit(CombatRng, m.AttackRating, Engaged.Defense, m.Level, Engaged.Level);
            long damage = 0;
            if (hit)
            {
                bool doubled = CombatResolver.RollDoubled(CombatRng, 0, 0, 0);
                damage = CombatResolver.PhysicalDamage(CombatRng, m.DamageMin, m.DamageMax, doubled: doubled);
                Engaged.Life -= damage;
            }
            if (!hit || Engaged.Life > 0)
            {
                CounterStrike(Engaged); // the survivor hits back — §6.7 may hand it to the merc
                continue;
            }
            kills++;
            xp = HirelingXp.OwnKillGain(Ladder, m, Engaged, Character.Level);
            m.Gain(xp, Character.Level);
            _flow.OnKill(DropRng, new CombatResolver.StrikeOutcome(true, false, damage, true, xp),
                EngagedKind, EngagedAffixKeys, area.Level, AreaSeed(area), TcKeyFor(area.Level),
                MagicFind, GoldFind);
            Engaged = null;
            EngagedKey = null;
            break; // the volley was against this one monster (the Attack idiom)
        }
        return new MercStrikeReport(m.Id, strikes, kills, m.IsDead, xp, m.Life, m.MaxLife);
    }

    /// The hero-kill share (strike AND weave): the alive merc is paid
    /// floor(×86/256) of the capped chain at its own level. Consumes NO RNG —
    /// the hero's own payment path is untouched byte-for-byte by this door.
    internal void ShareKillWithHireling(Monster slain)
    {
        if (Merc is { IsDead: false })
            Merc.Gain(HirelingXp.SharedKillGain(Ladder, Merc, slain, Character.Level), Character.Level);
    }

    /// The merc's sub-point life fraction in 256ths — the Regen.cs accumulator
    /// idiom, deliberately NOT a save field (v5 carries the whole pools, so a
    /// round trip may drop < 1 life of fraction, the same disclosed gap as the
    /// mana accumulator's header row).
    private long _mercLife256;

    /// <summary>RestTick's mercenary half (the SAME rest driver): each frame
    /// adds max × DamageRegen ÷ 4096 of the merc's life in 256ths, clamped to
    /// the missing pool. NO merc costs NOTHING (the hard-RNG-law guard); a
    /// DEAD merc skips — dead never regen; the fixpoint clamp holds at full.</summary>
    private void TickHirelingRegen(int frames)
    {
        if (Merc is null || Merc.IsDead) return;
        var m = Merc;
        long max256 = m.MaxLife * 256;
        // "monster life a frame = maximum life × DamageRegen ÷ 4096" (§4 Q2,
        // VERIFIED behaviour; the constant is the named GAP of §4 Q3). Nothing
        // inside the loop moves MaxLife, so the quantum is computed once.
        long quantum256 = max256 * MercDamageRegen / 4096;
        for (int f = 0; f < frames && quantum256 > 0 && max256 > 0; f++)
        {
            long pool256 = m.Life * 256 + _mercLife256;
            if (pool256 >= max256) break; // full is a fixpoint — the clamp holds
            _mercLife256 += Math.Min(quantum256, max256 - pool256); // added, clamped to the missing pool
            m.Life += _mercLife256 / 256; // whole points leave the accumulator…
            _mercLife256 %= 256;          // …the sub-point rides on (exact, R2's shape)
        }
    }

    /// Serializer restore hook (SaveSerializer is the only caller — the
    /// RestorePurse pattern): the merc block lands AS SAVED; the row lookup
    /// through For() lets a save naming a vanished id fail the load whole
    /// (KeyNotFoundException → the LoadSession InvalidDataException idiom).
    internal void RestoreHireling(HirelingSave d)
    {
        var row = Hirelings.For(d.Id);
        Merc = new Hireling(row) { Level = d.Level, Area = d.Area, CombatDistance = d.CombatDistance };
        Merc.Xp = d.Xp;
        Merc.Life = d.Life;
    }
}
