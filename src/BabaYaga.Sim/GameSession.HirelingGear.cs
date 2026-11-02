namespace BabaYaga.Sim;

/// <summary>
/// THE MERCENARY PANEL — give/strip/exchange/forfeit (LEDGER row 66 gear half,
/// M4-S15, card/11903; spec-3 §6.7b AMENDMENT, MC 11898 —
/// out/T0b-sec6-7b-amendment.md §1+§3; owner D2-oracle grant 2026-10-30). THE
/// 4-SLOT LAW (amendment S1–S3, VERIFIED three ways): a mercenary wears weapon,
/// body, helm and shield ONLY — no rings, no amulets, no jewelry: the
/// inventory.txt Hireling panel boxes read −1 for every jewelry slot, the 1.10
/// give-door whitelists no jewel type, and the official-era matrix names none.
/// GIVE/TAKE ARE FREE both directions, no gold (T1), gated in the pinned order:
/// RaiseIfDead (row 64 — the gate speaks first in every verb) → the merc EXISTS
/// → the merc is ALIVE → SAME AREA → the slot is one of the four → the item's
/// kind passes the slot's whitelist → the item's own requirements → and only
/// then the exchange check. THE ALIVE GATE quotes the amendment (D2): "You
/// cannot take gear from a dead mercenary … the function only opens if the merc
/// is ALIVE" — so a corpse can be NEITHER given TO nor stripped FROM; gear
/// STAYS ON through death and returns with the SAME mercenary on the captain's
/// revive (D1 — retention by object identity, NO gear-keep flag). An occupied
/// slot EXCHANGES (T2 — "equipping over a worn item EXCHANGES: the displaced
/// item goes to the hero's inventory"), and a pack without a free cell for the
/// displaced item refuses BY NAME with nothing half-applied (the Take-idiom
/// room check). DISMISS FORFEITS (D3): the worn items die with the entry — the
/// sim has NO ground-drop concept, so a forfeit is annihilation, never a drop
/// (state reading; pinned by test, comment at Dismiss). The weapon whitelist is
/// DATA (hirelings.json gear.weaponTypes — the D2 S2 per-kind switch), and the
/// hero-side EquipSlots table answers which base fits which panel slot (REUSE,
/// the InventoryGrid row-68 mechanism, never restated).
/// ORIGINAL DESIGN PINS: (1) the same-area grain — D2's gate is "neighbored
/// rooms" (AreUnitsInNeighboredRooms, amendment §3), our clone reads it at the
/// whole-area level ("she stands with you on the area you stand on"); (2) the
/// rzutca axe whitelist is fenced in the DATA (D2 ships no throwing hireling —
/// nearest shape is the A5 axe marshal, owner may re-rule). THE STAT EFFECT IS
/// LIVE (M4-S16, MC 11914, spec-3 §6.7c AMENDED — this replaces the §6.7b
/// DEFER row): a worn weapon's base damage range REPLACES the merc's bracket
/// dice, worn dmg_pct sums into the hero primitive's ΣED (−90 clamp), flat
/// dmg_min/dmg_max widen the rolled range before the %, and worn +life raises
/// MaxLife — all DERIVED from the persisted gear through Hireling.StrikeStats/
/// MaxLife (v7 UNCHANGED — no stat is a save field). The COMPLEX rows stay
/// DEFERRED, named: attack-rating/to-hit curve, shield block %, +skills from
/// gear, IAS, proc items, Str/Dex-bearing gear (LEDGER 66).
/// STATE READINGS pinned: Item carries NO Identified flag (grep-zero over
/// src/ + data/ — D2's IFLAG_IDENTIFIED half of the door has no counterpart in
/// our model: every item we hold is KNOWN by construction), and the Hireling
/// model carries NO Str/Dex stat (StatsAt answers life/damage/AR only), so an
/// item's RequiredStrength/RequiredDexterity have NOTHING to read against —
/// unenforceable by model shape. The LevelRequirement half DOES exist on both
/// sides (item.LevelRequirement, merc.Level) and IS enforced.
/// The doors touch NO RNG stream (the HARD RNG law, rows 65–66) and move gold
/// through no door at all (T1: the trade is free).
/// </summary>
public enum MercGearSlot
{
    Weapon,
    Body,
    Helm,
    Shield,
}

/// <summary>The four slot names AS THE VERBS AND THE DATA SPEAK THEM (the 4-slot
/// law's canonical vocabulary; the save rides the enum names). Merc slots map
/// 1:1 onto the hero-panel EquipSlots the base table already answers for — the
/// helm is the hero's Head, the shield his Offhand (amendment §3's mapping).</summary>
public static class MercGearSlots
{
    public static readonly (string Name, MercGearSlot Slot)[] All =
    [
        ("weapon", MercGearSlot.Weapon),
        ("body", MercGearSlot.Body),
        ("helm", MercGearSlot.Helm),
        ("shield", MercGearSlot.Shield),
    ];

    public static readonly IReadOnlyList<string> Names = All.Select(a => a.Name).ToArray();

    public static string NameOf(MercGearSlot slot) =>
        All.First(a => a.Slot == slot).Name;

    public static MercGearSlot? Parse(string name)
    {
        foreach (var (n, slot) in All)
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return slot;
        return null;
    }

    public static EquipSlot HeroPanel(MercGearSlot slot) => slot switch
    {
        MercGearSlot.Weapon => EquipSlot.Weapon,
        MercGearSlot.Body => EquipSlot.Body,
        MercGearSlot.Helm => EquipSlot.Head,
        MercGearSlot.Shield => EquipSlot.Offhand,
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };
}

public sealed partial class GameSession
{
    /// What one gear move did — the StashMoveReport shape: InvRow/InvCol are the
    /// pack cells the item left (give) or landed on (strip), always the ANCHOR;
    /// the Exchanged* fields name the item the slot spat back and its landing
    /// cell (null/-1 when the slot stood empty). The headless leg and the CLI
    /// echo their GEAR lines from here — positions session-read, never guessed.
    public sealed record GearMoveReport(
        string Slot, string Base, string Name, int InvRow, int InvCol,
        string? ExchangedBase = null, string? ExchangedName = null,
        int ExchangedRow = -1, int ExchangedCol = -1);

    /// <summary>Give one item from the pack to the merc's named slot (§6.7b:
    /// free, both directions, any time — the merc must be ALIVE and NEAR). Gates
    /// in the amendment order; the occupied slot EXCHANGES through the TryCarry
    /// door (room proven BEFORE the tab-side moves, the stash-Take atomicity
    /// idiom); a refusal leaves nothing half-applied.</summary>
    public GearMoveReport GiveHirelingGear(string slot, int invX, int invY)
    {
        RaiseIfDead();
        var m = Merc ?? throw new SessionRejectedException("you ride alone — no hireling to receive it");
        // Amendment §1 D2 (VERIFIED official + engine): "You cannot take gear from
        // a dead mercenary … the function only opens if the merc is ALIVE" — the
        // give door reads the SAME alive rule (T1: "the merc must be alive"):
        // a corpse can neither receive nor be stripped.
        if (m.IsDead)
            throw new SessionRejectedException(
                $"'{m.Id}' is dead — a corpse wears nothing (the give-door requires it alive, §6.7b D2)");
        RaiseIfMercOutOfReach(m);
        var name = ParseMercSlot(slot);
        // Slot acceptance is DATA (hirelings.json gear.slots): the shield stands
        // in the 4-slot model but no row of this roster accepts it.
        if (!m.Row.Gear.Slots.Contains(name))
            throw new SessionRejectedException(
                $"'{m.Id}' does not accept a '{name}' — she wears: {string.Join(", ", m.Row.Gear.Slots)}");
        if (invX < 0 || invX >= InventoryGrid.Rows || invY < 0 || invY >= InventoryGrid.Columns)
            throw new SessionRejectedException($"inventory cell {invX},{invY} is outside the pack grid");
        var item = FindCarried(invX, invY)
            ?? throw new SessionRejectedException($"the pack holds no item anchored at cell {invX},{invY}");
        RaiseIfGearRefused(m, name, item);

        var mercSlot = MercGearSlots.Parse(name)!.Value;
        // THE EXCHANGE (T2, VERIFIED community + code shape): an occupied slot
        // spits the old item back to the hero — room for it is proven BEFORE
        // anything moves; a pack without one refuses the exchange BY NAME.
        string? exBase = null, exName = null;
        int exRow = -1, exCol = -1;
        if (m.Gear.TryGetValue(mercSlot, out var worn))
        {
            if (!PackHasRoomFor(worn))
                throw new SessionRejectedException(
                    $"the exchange on '{name}' refused — the pack has no free cell for the displaced '{worn.DisplayName}'");
            exBase = worn.Base.Id;
            exName = worn.DisplayName;
        }

        // Apply — the Stash-Store fixups ride (belt re-point, LastLooted, the
        // Carried/CarriedSpots lockstep): the door fully applies or nothing did.
        Grid.Remove(item);
        Carried.Remove(item);
        CarriedSpots.Remove(item);
        Unbelt(item);
        if (ReferenceEquals(LastLooted, item)) LastLooted = null;
        m.Gear[mercSlot] = item;
        if (worn is not null)
        {
            if (!TryCarry(worn))
                throw new InvalidOperationException(
                    $"the displaced '{worn.DisplayName}' found no cell after the room was proven"); // (the check above makes this unreachable)
            (exRow, exCol) = CarriedSpots[worn];
        }
        return new GearMoveReport(name, item.Base.Id, item.DisplayName, invX, invY, exBase, exName, exRow, exCol);
    }

    /// <summary>Take one slot back, straight into the hero's pack (§6.7b T3:
    /// "Taking gear back is per-slot, straight into the hero's inventory";
    /// Arreat: take it off "before you dismiss him or you'll lose it"). The SAME
    /// gates as give — DEAD-hero → exists → ALIVE (the amendment names the
    /// take-back door: a dead mercenary cannot be stripped) → same area → the
    /// slot is one of the four — then the pack room, proven before the slot
    /// empties (the stash-Take atomicity idiom).</summary>
    public GearMoveReport StripHirelingGear(string slot)
    {
        RaiseIfDead();
        var m = Merc ?? throw new SessionRejectedException("you ride alone — no hireling to strip");
        // Amendment §1 D2, verbatim: "You cannot take gear from a dead mercenary
        // … the function only opens if the merc is ALIVE" — the corpse keeps what
        // it died wearing until the captain raises THIS mercenary again.
        if (m.IsDead)
            throw new SessionRejectedException(
                $"'{m.Id}' is dead — nothing is stripped from a corpse (§6.7b D2)");
        RaiseIfMercOutOfReach(m);
        var name = ParseMercSlot(slot);
        var mercSlot = MercGearSlots.Parse(name)!.Value;
        if (!m.Gear.TryGetValue(mercSlot, out var worn))
            throw new SessionRejectedException($"'{m.Id}' wears nothing in '{name}'");
        if (!PackHasRoomFor(worn))
            throw new SessionRejectedException($"the pack has no cell left for '{worn.DisplayName}'");

        m.Gear.Remove(mercSlot);
        TryCarry(worn); // the stash-Take door: re-entry THROUGH TryCarry (room proven above)
        var (row, col) = CarriedSpots[worn];
        return new GearMoveReport(name, worn.Base.Id, worn.DisplayName, row, col);
    }

    /// <summary>The four slots as worn, one line — "weapon=Soluga,body=-,helm=-,
    /// shield=-" (the hire/give/strip CLI echo reads it; a lone rider or no
    /// session merc prints the four empties). Presentation string, built here so
    /// the CLI stays semantics-free.</summary>
    public string MercGearEcho() => string.Join(",", MercGearSlots.All.Select(a =>
        $"{a.Name}={(Merc is not null && Merc.Gear.TryGetValue(a.Slot, out var it) ? it.DisplayName : "-")}"));

    /// ORIGINAL DESIGN (amendment §3): D2 gates the give at "neighbored rooms"
    /// (AreUnitsInNeighboredRooms); the clone has one area field and one mirror,
    /// so the rule is read at the WHOLE-AREA grain — she trades with you only
    /// where she stands. The refusal names BOTH areas.
    private void RaiseIfMercOutOfReach(Hireling m)
    {
        if (m.Area != CurrentArea)
            throw new SessionRejectedException(
                $"'{m.Id}' is not with you — she stands in '{m.Area}', you are in '{CurrentArea}'");
    }

    /// The kind gates, in the amendment order: weapon slots ride the DATA
    /// whitelist (the D2 S2 per-kind switch, hirelings.json); the other slots
    /// ask the hero-panel table (REUSE — EquipSlots.For already answers which
    /// base fits Head/Body/Offhand; a rings base NEVER maps there, so a ring
    /// given to "helm" is refused by the same mechanism the hero's own equip
    /// uses). Then the REQUIREMENTS, per state reading: LevelRequirement exists
    /// on both sides and IS enforced; the identified gate is VACUOUS (Item
    /// carries no Identified flag — grep-zero model-wide, everything we hold is
    /// known by construction); RequiredStrength/RequiredDexterity exist on the
    /// item but the Hireling model carries NO Str/Dex stat, so they have nothing
    /// to read against and gate NOTHING (pinned by test, named in the LEDGER).
    private static void RaiseIfGearRefused(Hireling m, string name, Item item)
    {
        var mercSlot = MercGearSlots.Parse(name)!.Value;
        if (mercSlot == MercGearSlot.Weapon)
        {
            if (!m.Row.Gear.WeaponTypes.Any(t => item.Base.MatchesType(t)))
                throw new SessionRejectedException(
                    $"'{item.DisplayName}' is no '{m.Id}' weapon — she takes: {string.Join("/", m.Row.Gear.WeaponTypes)}");
        }
        else if (!EquipSlots.For(item.Base).Contains(MercGearSlots.HeroPanel(mercSlot)))
        {
            throw new SessionRejectedException(
                $"'{item.DisplayName}' does not fit the mercenary '{name}' slot");
        }
        if (item.LevelRequirement > m.Level)
            throw new SessionRejectedException(
                $"'{item.DisplayName}' demands level {item.LevelRequirement} — '{m.Id}' stands at level {m.Level}");
    }

    /// Parse to the canonical lowercase name; a 5th slot (a ring, an amulet, a
    /// glove — anything beyond the four) is refused BY NAME, quoting the 4-slot
    /// law. The gate order reads it AFTER the alive door on purpose: a dead
    /// mercenary is the DEAD fact first, in every verb (row-64 doctrine).
    private static string ParseMercSlot(string slot)
    {
        foreach (var (name, _) in MercGearSlots.All)
            if (string.Equals(name, slot, StringComparison.OrdinalIgnoreCase)) return name;
        throw new SessionRejectedException(
            $"there is no mercenary slot named '{slot}' — a mercenary wears weapon, body, helm " +
            "and shield ONLY (the 4-slot law: the Hireling panel has no ring, no amulet and no " +
            "jewelry box — inventory.txt rows 16/32 read −1 for every one of them)");
    }
}
