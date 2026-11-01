namespace BabaYaga.Sim;

/// <summary>
/// Mana regen (LEDGER row 79, M4-S10b): the D2 quantum rule ADOPTED AS-IS per the
/// run's T0b-mechanics-spec §1.8 AMENDED 2026-11-01 (amendment MC 11873; oracle
/// sources: D2MOO PlrModes.cpp EVENTS_ManaRegen L1190 at the pinned commit +
/// d2r.org/mechanics/regeneration — quoted rows M1–M5, R1–R2, L1–L3, D2/D4 in
/// out/T0b-sec1-8-amendment.md). Sibling of the core file's command contract:
/// RestTick either advances the pool or is rejected, nothing half-applied.
///
/// ADOPTED AS-IS — mana restores EVERY frame by a quantum of the pool, the engine's
/// exact form which "works in 256ths and floors twice: each frame adds the whole
/// part of maximum mana in 256ths ÷ (25 × ManaRegen), at least 1, times (100 +
/// Regenerate Mana %) ÷ 100, again floored" (M1/M3); the quantum is then ADDED to
/// the fixed-point 256ths pool without further truncation — R2: "two floors on the
/// per-frame quantum, none on the accumulation" — and clamped to the missing pool
/// exactly as the engine's add is ("STATLIST_AddUnitStat … clamped to the missing
/// pool", M3). charstats ManaRegen = 120 for every class (M2) ⇒ ~120 s empty→full
/// at 0 % bonus whatever the pool; small pools floor-noticeably slower (the 20-mana
/// ≈ 205 s shape). Energy raises regen ONLY via max mana (M5) — the quantum is a
/// share of the CURRENT pool, never a cached constant.
///
/// ADOPTED AS-IS — LIFE NEVER REGENERATES PASSIVELY (L1/L2: "Your life does not
/// regenerate on its own"; the engine's only life tick is the Replenish-Life item
/// stat — a future own row, NOT built here). There is no leave-combat window either
/// (L3: no window exists in D2 at all — no passive life regen to window). Potions
/// stay the only life refill: the potion economy IS the design.
///
/// ADOPTED AS-IS — no difficulty term anywhere in regen (D2/D4: the old §1.8-era
/// phrase "per-frame with difficulty penalties" is RETIRED — CONTRADICTED; the
/// difficulty effect the old text read is the LEECH divisor, and leech is out of
/// scope, own card and own amendment).
///
/// ORIGINAL DESIGN (not D2-derived): (1) RestTick(int frames) itself as the explicit
/// headless rest driver — D2's regen rides the engine's per-frame event with no
/// caller to invoke; (2) the 25-frames-per-second seconds convention (FramesPerSecond
/// below, only the unit of the VERIFIED 25×ManaRegen denominator); (3) the sub-point
/// accumulator lives on the session and persists across RestTick calls (R2's exact
/// accumulation) but is NOT a save field — v4 saves carry the whole pools only, so a
/// round trip may drop < 1 mana of fraction; save-format work is its own row.
/// DEAD never regens: RaiseIfDead FIRST, the row-64 gate idiom — like UsePotion's
/// "no healing from a corpse", the belt (and the rest) waits for the stone.
/// </summary>
public sealed partial class GameSession
{
    /// charstats ManaRegen = 120 — seconds, every class (§1.8 M2, VERIFIED:
    /// "ManaRegen 120 and the 256ths are the same in Lord of Destruction and
    /// Resurrected tables").
    public const int ManaRegenSeconds = 120;

    /// The sim's 25-frames-per-second convention. ORIGINAL DESIGN (not D2-derived)
    /// — it only supplies the unit for the VERIFIED denominator 25 × ManaRegen (M1:
    /// "at 25 ticks/s", R1); the sim has no engine frame clock to bind it to.
    public const int FramesPerSecond = 25;

    /// The mana pool's sub-point remainder in 256ths (R1: pools tracked in 256ths
    /// of a point). Persists across RestTick calls — R2's "none on the
    /// accumulation"; the belt tests' exactness plants prove a per-call reset is a
    /// bug. Stays normalized to 0…255 after every tick; not persisted (see header).
    private long _mana256;

    /// %Regenerate Mana summed across carried + worn (the Warmth/Meditation term of
    /// M3's second floor; the same summing idiom as MagicFind/GoldFind). No affix
    /// row grants it today (data/affixes.json carries no regen mod), so the path
    /// runs ×100/100 — implemented per spec; item wiring is the %RegenMana future
    /// row, the code path is live the day an item carries the mod.
    private int RegenManaPercent =>
        Carried.Concat(Equipment.Equipped.Values).Sum(i => i.ModSum("regenerate_mana"));

    /// <summary>Rest for the given number of frames: each frame restores the §1.8
    /// quantum into the 256ths mana pool, clamped at MaxMana. Gates: the row-64
    /// DEAD gate FIRST (a corpse regens nothing, any pool), negative frames reject.
    /// Life never moves here (D2 rule, header); the three rng streams are never
    /// touched; frames ≤ 0 is an ordered no-op.</summary>
    public void RestTick(int frames)
    {
        RaiseIfDead(); // dead never regens — the gate speaks before everything (row 64)
        if (frames < 0)
            throw new SessionRejectedException("rest frames must be at least 0");

        long max256 = Character.MaxMana * 256;
        // The quantum: floor(max₂₅₆ ÷ (25 × ManaRegen)) at MIN 1, then the
        // (100+%RegenMana)÷100 second floor (§1.8 M1/M3 verbatim shape). Nothing
        // inside the frame loop changes MaxMana or the items, so it is computed
        // once — the per-frame value is identical by construction.
        long quantum256 = Math.Max(1, max256 / (FramesPerSecond * ManaRegenSeconds))
            * (100 + RegenManaPercent) / 100;

        for (int f = 0; f < frames && quantum256 > 0 && max256 > 0; f++)
        {
            long pool256 = Character.Mana * 256 + _mana256;
            if (pool256 >= max256) break; // full is a fixpoint — the clamp holds (M3)
            _mana256 += Math.Min(quantum256, max256 - pool256); // add, clamped to the missing pool
            Character.Mana += _mana256 / 256;                   // whole points leave the accumulator…
            _mana256 %= 256;                                    // …the sub-point rides on (exact, R2)
        }
    }
}
