# Baba Yaga — Mechanics laws (condensed)

The LIVE laws of the simulation as shipped at v0.4.0 (M4 GA slice), condensed from the
mechanics spec of run `.audits/202610301950-31593a83` (`out/T0b-mechanics-spec*.md` + its
amendments; run dir at the Games workspace root, maintainer-side). Bracketed keys are the
spec's D2 reference keys — product names are ORIGINAL (clean-room rule,
docs/ARCHITECTURE.md §R2). Every law here is the behaviour the xunit suite (356 tests) and
the headless story markers pin; the LEDGER rows name the evidence. This file is the
in-repo law summary — where it and a spec disagree, the code + tests win and the line is stale.

## Regeneration [1.8] (AMENDED 2026-11-01, MC 11873)

- MANA regenerates EVERY frame as a fixed quantum: per frame
  `q = max(1, floor(max_mana×256 / (25 × ManaRegen=120)))`, then `q = q × (100 + %RegenMana) / 100`
  (floored), added to a 256ths accumulator that persists across rest calls, clamped to the
  missing pool. Base constant ManaRegen = 120 for all classes → empty→full ≈ 120 s at 0 % bonus.
  Two floors on the quantum, none on the accumulation. No difficulty term exists.
- LIFE NEVER REGENERATES on its own — potions are the ONLY life refill (the potion economy
  IS the design). No leave-combat window exists at all. Replenish-Life / leech / life-on-hit
  are future own rows, NOT built.
- DEAD never regens: the death gate fronts every rest tick.
- A hireling regenerates life per frame `max_life × DamageRegen(4) ÷ 4096` on the same rest
  driver (monster-shape regen). The regen CONSTANT is the amendment's named GAP (§4 Q3): the
  behaviour empty→full ≈ 4096/4 frames is verified, the divisor source is cited in code.

## Hirelings [6.7] (AMENDED 2026-11-01, MC 11883)

- HIRE PRICE = `Gold × (100 + 15 × levels_above_bracket) / 100`; a hired rookie (level 1,
  original-design pin) pays exactly the row's Gold. Roster `data/hirelings.json`: the clone's
  own ids/names/acts (strzelczyni, witez, rzutca) over LoD-era Version-100 bracket stats.
- MERC XP is its OWN ladder: `Exp/Lvl × L² × (L + 1)`. The merc's own kill pays full at ITS
  OWN level (the hero is paid nothing extra); a hero kill shares `floor(× 86/256)` OF THE
  PER-KILL-CAPPED figure (cap FIRST, then 86/256). Per-kill cap = 1/64 of the span the
  current level takes; merc level ≤ min(hero, 98).
- REVIVE at the mercenary captain IN TOWN costs `min(15 · L² / 2, 50 000)` gold and returns
  the SAME mercenary at full life, level + XP kept. NO corpse rule — nothing must be carried
  anywhere. DISMISS pays NO refund (LoD rule).
- Targeting: a counter reaches the NEAREST valid body among hero + living mercs; ties go to
  the hero; no merc → always the hero (the hard RNG law: no merc hired → combat, rest and
  gold streams stay byte-identical to the merc-less build).

## Hero death kills the hireling [6.7b] (AMENDED 2026-11-01, MC 11898)

- When the hero's life hits 0, a LIVING mercenary dies TOO, at the same instant — through her
  own damage floor, NO roll, NO gold, NO event. An already-dead merc is untouched.
- The hireling ENTRY survives: the captain's Revive (law above) remains the unchanged door.
- Respawn raises the HERO at full life in the last discovered town (rides the lit stones);
  it does NOT carry a dead merc back — the corpse stays where it fell ("no corpse-carry").
- Every hero death therefore adds a revive bill to the economy (pinned to the gold-economy card).

## Mercenary gear [6.7b] (AMENDED 2026-11-01, MC 11898)

- THE 4-SLOT LAW: a mercenary wears weapon, body, helm, shield ONLY — no rings, no amulets,
  no jewelry; a 5th slot is refused BY NAME. Acceptance is data (`gear.slots` + weapon-kind
  whitelist per row).
- Give and take are FREE both directions (no gold door). The dead can neither receive gear
  nor be stripped. Gear STAYS ON through death and returns with the SAME mercenary on revive
  (object identity). An occupied slot EXCHANGES (displaced item into a free hero-pack cell;
  no free cell → refused, nothing half-applies). DISMISS FORFEITS worn items.
- Requirements by state reading: the merc's LevelRequirement IS enforced; Identified/Str/Dex
  gates are vacuous in this model (named, pinned in tests).

## Stats through gear [6.7c] (AMENDED 2026-11-02, MC 11913; live MC 11914)

- SWAP: a worn weapon whose base carries a damage range REPLACES the merc's bracket dice
  (one range per base — fenced original design); unarmed or range-less keeps the bracket.
- ED %: worn `dmg_pct` sums into the ΣED input with the **−90 clamp floor**; the worn weapon's
  own `dmg_pct` rides the weapon-ED input.
- ORDER: flat `dmg_min/dmg_max` widen the rolled range BEFORE the %, then floor, then
  `min + rand(max − min)` — paid through the SAME physical-damage resolver the hero strikes
  with, never a second formula.
- +LIFE: worn `life` raises MaxLife DERIVED from the persisted gear (save v7 unchanged —
  no stat is a save field), surviving death, revive and level-up recompute.
- DEFERRED BY DESIGN, named: attack-rating / to-hit curves from gear, shield block %,
  +skills, IAS, procs, Str/Dex-bearing gear. The WEAPON damage data is REAL since the
  D1 data beat (MC 11928: every weapon-kind base carries ONE damage range, the
  price-ordered ORIGINAL curve in the json note); real ED%/flat/+life DATA rows are
  future — no such stat rows reach merc gear through the shipped data yet (synthetic
  bases+mods prove those mechanisms).

## Life by potions · belt

No passive life regen (above) makes potions the only refill. The belt is 4 cells
(hotkeys belt_1..belt_4), potion rows `data` + town vendor stock; using a drink raises the
pool by the row's flat amount, never above max.

## Death & party XP [6.6]

- Death costs 0 % / 5 % / 10 % of the current level's XP span (difficulty band; the sim
  defaults Ordinary 0 %); never below the level start, nothing at level 1. ¾-corpse-return
  recovery is NOT built yet (named open row).
- Party XP: a kill is shared by living members near the kill; the pot carries a +87/256 bonus
  per member beyond the first; each share = pot × own level ÷ Σ member levels.

## Travel [6.3] (ORCH-RULING 2026-11-01)

ONE travel door, two lawful ways: WALK a `linkedTo` neighbour whose endpoint is DISCOVERED,
or RIDE the lit stones (fixed hub-and-link network, permanent per character). Walking lights
nothing, burns nothing and rolls no RNG (stream equality pinned). The dark↔dark wander is
refused (a walk off dark ground needs the ground it stands on lit — named concession);
full-walk fidelity is its own future row. Quest rewards can LIGHT a new stone.

## Town stash

The chest stands ONLY in town (outside-town store/take refused by name). Tab 1 rides with the
chest; extra tabs unlock by quest rewards, paid EXACTLY ONCE. Store/Take move items between
pack and tab through the same grid machinery (4×10 tabs, original design); storing never
displaces what stands; gold has NO stash semantics yet (StoreGold refuses by name).

## Save format — v7 (JSON)

`SaveVersion = 7` (`src/BabaYaga.Sim/SaveSerializer.cs`); a foreign version is REFUSED BY NAME,
never silently half-restored. v2 persists both RNG state words (loaded sessions CONTINUE
their streams, never replay); v3 the unspent skill-point pool; v4 the current life/mana
pools; v5 the hireling block; v6 the town stash; v7 the gear list inside the merc block —
each appended last, older dump positions kept. The regen accumulator is NOT a save field: a
load may drop a sub-1-point regen fraction (session-only, named). Known limitation kept by
ruling: a save taken mid-special-fight restores the monster PLAIN (DA2 P4-3).

## Not law here

M1–M3 combat laws (to-hit dual ratio 5/95 clamp, block 75 % cap + Dex−15, AR = Dex×5 + flats,
crit-family roll order, XP chain with level-gap + ExpRatio, drop pipeline, resistances) live
in LEDGER rows 1–58 and the spec parts 1–3; the XP ladder is the 99 verbatim reference rows.
