# Baba Yaga — Release v0.4.0 (Milestone 4 GA slice) · 2026-11-02

Original-IP medieval Slavic witch-fantasy single-player ARPG; the mechanics verification
oracle for this milestone was Diablo II: LoD (v1.10–1.13 reference era) — the ONLY place any
D2 material was used; all product content (names, creatures, quests, areas, classes, roster)
is original. Mechanics laws with their amendments: `docs/MECHANICS.md`; feature evidence:
`LEDGER.md` rows named below.

Evidence measured for this tag (run `.audits/202610301950-31593a83`, HEAD 511e1c0):
356/356 xunit green; D2 table-parity harness 10/10 (out-of-repo maintainer harness, README
§Test); the headless story runs twice byte-identical, exit 0, printing SIM_TICK ×3 +
QUEST_OK, CAST/MANA_OK, REGEN_OK, BELT_OK, DEATH_OK, TRADE_OK, ACT1_OPEN_OK, HIRE_OK,
GEAR_OK + GEAR_STATS_OK, STASH_OK and ORCH_M4_OK, zero ERROR lines.

## What shipped — world & travel

- Act-1 world opened: the walked world (stone-less doors WALK, lit stones RIDE, one travel
  door) — walking lights nothing and rolls no RNG; dark↔dark wander refused as a tested
  contract (LEDGER row 80; spec-3 §6.3, ORCH-RULING 2026-11-01).
- Per-area monster tables live (`data/area-monsters.json`), area/waypoint graphs, discovery
  order contract (light-before-travel) (LEDGER rows 77 note, 61, 81).

## What shipped — Act-1 quests (4 live)

- Zaginione Gęsi (gesi), Wrzeciono Kikimory (wrzeciono), Dary w Kopcach (dary) and Głowa
  Leszego (glowa) run the full loop live: accept gates prereqs → goal-area visit → step on
  the goal → complete pays the reward EXACTLY ONCE (double-pay refused) → rewards spend
  (skill point into iskra; LifePotion raises max life; WaypointUnlock lights a stone;
  StashTab pays a real stash tab) (LEDGER rows 62, 74, 81).
- Unshipped reward kinds land on a named OtherRewards counter, never silently (row 62).

## What shipped — companion (hireling) laws

- Hire price `Gold × (100 + 15 × levels_above_bracket) / 100`; original roster over LoD-era
  V100 bracket stats (LEDGER row 65; spec §6.7, AMENDED 2026-11-01, MC 11883).
- Merc XP: own ladder `Exp/Lvl × L² × (L+1)`; hero kills share floor(×86/256) of the
  per-kill-capped figure (cap 1/64 of the level span), level ≤ min(hero, 98); revive
  `min(15·L²/2, 50 000)` at the town captain, no corpse rule (row 66; §6.7 AMENDED MC 11883).
- Hero death kills a living hireling at the same instant; the captain's revive stays the
  unchanged door; no corpse-carry (row 83; §6.7b AMENDED 2026-11-01, MC 11898).
- Mercenary gear: THE 4-SLOT LAW (weapon/body/helm/shield only), free give/take, the dead
  neither receive nor are stripped, gear rides the corpse and returns with the revived merc,
  occupied-slot exchange, dismiss forfeits (row 66; §6.7b AMENDED MC 11898).
- Stats through gear: weapon-range swap, ED % with the −90 clamp, flat-before-% order, worn
  +life raising MaxLife derived from gear — complex curves (to-hit/block/skills/IAS/procs)
  DEFERRED by design (row 66 PARTIAL LIVE; §6.7c AMENDED 2026-11-02, MC 11913; code MC 11914).

## What shipped — economy & life support

- Life by potions — NO passive life regen by law (the potion economy IS the design), mana on
  the verified D2 quantum (per-frame, 256ths, ManaRegen 120 ≈ 120 s empty→full) driven by the
  explicit RestTick door (row 79; spec §1.8, AMENDED 2026-11-01, MC 11873).
- Potion belt 4 cells with hotkeys; vendor/gamble/sell economy live behind the dead-gate
  (row 69, M2 vendor rows).
- Town stash: chest stands ONLY in town, tab 1 + reward-unlocked extra tab (glowa pays it),
  grid store/take, never displaces, no gold stash yet (row 74).
- Death: 0/5/10 % XP band, default Ordinary 0 %; the dead hero is refused every action verb;
  respawn rides the lit stones home (row 64; spec-3 §6.6).
- CLI speaks the engine doors: rest / hire / dismiss / revive / stash_store / stash_take
  (+ give / strip) — thin calls on the existing public doors, refusals print named errors
  (row 82).

## What shipped — clean-code waves W1–W3 (post-milestone doctrine, MC 11907)

- W1 (MC 11908): 4 orphan files removed, warnings 5→0, markers + parity unchanged.
- W2 (MC 11910): Fixed256Accumulator + LegKit consolidation, byte-anchors empty base-vs-W2.
- W3 (MC 11912): GameSession split (210-line head) + soft-row splits, pure moves, byte
  anchors unchanged. All waves: full suite green, zero behaviour change.

## Known limits (honest list)

- **Graphics play session OPEN at this tag** (the retired claim, exact words archived in git
  history): the game ran as the pure-C# sim (CLI) and the
  headless Godot story; the windowed UI (equipment panel row 68, sheet 71, automap 72, town
  portal 73, shop/craft/menu widgets 75, art-screenshot leg 76) was open work.
  UPDATED 2026-11-02 (M5-S8a): this line is the v0.4.0 record only — windowed play was
  delivered and gate-PASSED after this tag (`.audits/202610301950-31593a83/out/TEST-verdict-m5-b.md`,
  JUDGED f01bcde2; see PLAYTEST.md + README §PLAY); v0.5.0 release notes ride a later
  owner-PASS commit.
- Real item damage/ED/life data rows are future — `item-bases.json` carries no damage columns;
  the gear-stat mechanism is proven on synthetic bases (row 66 DATA-FUTURE).
- Merc complex curves (to-hit / block / +skills / IAS / procs from gear) DEFERRED by design
  (§6.7c, `docs/MECHANICS.md`).
- Acts 2–5 are unwritten: their data rows exist as parse-tested skeletons; only act-1 content
  is live (hirelings witez/rzutca load but stand gated by their unbuilt acts).
- Save format is v7 JSON; foreign versions refused by name; no upgrade path; a save taken
  mid-special-fight keeps plain-restore (documented, intentional — LEDGER DA-ROUND-2 row).
- Regen fractions are session-only: the 256ths accumulator is not a save field (a load may
  drop < 1 point of pending mana); Replenish-Life/leech are future own rows.
- Walk law concedes: walking requires a DISCOVERED endpoint (dark↔dark wander refused);
  D2 full-walk fidelity is its own future row (row 80).
- ¾-corpse-return recovery after death NOT built (row 64); ritual-craft recipe engine NOT
  built (row 48); procedural map layout BUILDING (row 59); curse/aura stack engine and the
  effect-family rows (crushing blow, open wounds, poison, chill, stun) remain NOT-BUILT
  (rows 28–35).

## NOT SHIPPED (what this milestone promised beyond what it delivered)

Windowed/graphics play session (Godot UI screens), acts 2–5 content, hireling attack-rating
and block curves from gear, real damage/ED item data rows, equipment weapon-set swap (row 68),
skill hotkey bar (row 70), character sheet (row 71), automap (row 72), town-portal scroll
(row 73), in-repo pytest marker suite (the CLI-marker + parity harnesses live out-of-repo for
maintainers).
