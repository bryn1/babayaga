# Baba Yaga — LEDGER

Project status ledger (the ONE ledger at repo root; moves with every commit).
Feature rows seeded from the PARITY CHECKLIST in
`.audits/202610301950-31593a83/out/T0b-mechanics-spec-3.md` (run dir at the Games workspace root).
Bracketed keys are reference keys into the D2 mechanics spec only — product names are ORIGINAL
(clean-room rule, docs/ARCHITECTURE.md §R2). Statuses: NOT-BUILT / BUILDING / TESTED / VERIFIED.

STATUS: M1-FOUNDATION + M2-ITEMS (item & economy system 2026-10-30, MC 11746: quality roll, affixes, rares, uniques/sets, sockets/runes/runewords, gamble, vendors, gold, inventory-grid model — 70 xunit tests green). BUILT-M2 = mechanic fully implemented+tested in the Sim as of that commit; the M1 line below stays true for everything it claims. Still open after M2-items: ritual-craft recipe engine (row 48), frame breakpoints (rows 23–25 — next M2 leg), equipment weapon-sets (row 68 — row 67 grid model delivered early with the item system; its Godot widget stays M4).

STATUS: M3-MONSTERS-WORLD (2026-10-30, MC 11747: monster tables, champion packs/uniques/minions, 4 AI behaviours, area+waypoint graphs, quest FSM, party XP 87/256, death penalty; rows 18–19, 53–58, 61–64 BUILT-M3). OPEN: row 59 procedural layout (MapGen), row 60 NPC roles, rows 65–66 hirelings.

STATUS: M1-CLOSE-OUT (2026-10-31, MC 11752: kill→drop wiring — KillFlow + TreasureClass walk over data/treasureclasses.json, row 36 BUILT-M1; deterministic save/load — SaveSerializer + byte-identical dump, BUILT-M1; headless scripted session driver src/BabaYaga.Sim.Cli/ + tests/journey.txt, BUILT-M1; 120 xunit tests green, build 0 warnings). STILL NOT-BUILT: the pytest tests/ suite (CLI-marker journeys — journey.txt is its seed), stash tabs (row 74), ritual-craft (row 48).

STATUS: DA-ROUND-1-FIX (2026-10-31, card/11755, verdict .audits/202610301950-31593a83/out/DA-verdict.md: save format v2 PERSISTS BOTH RNG STATE WORDS — a loaded session continues its streams, never replays (continuation xunit + CLI pair green); quest SkillPoint lands in Character.SkillPoints via GrantSkillPoints (probe tests/probe-p12.txt exits 0, spend kolo HARDPOINTS:2); new CLI verb `special <monster>` rolls the seeded 4:1 group so rows 53–57 run live; DeathPenalty clamps the M2 economy names to the 10 % top band; rare-gate-at-27 and row-min-clamp pinned by BoundaryTests (both mutations proven RED on a .tmp tree copy); 127 xunit green, build 0 warnings).

STATUS: DA-ROUND-2-FIX (2026-10-31, card/11757, verdict .audits/202610301950-31593a83/out/DA-verdict-c2.md: save format v3 adds unspentSkillPoints — the pool restores FROM THE SAVE, quest points survive save/load BOTH unspent (load→spend kolo HARDPOINTS:2 exit 0) and already-spent (the probe-p12 save now LOADS, dump byte-equal; SaveTests ×2 close the coverage gap); the unique live path honours passesToMinions:false via the same minion filter (seed 42: chciwy stays on the leader, MINION-AFFIXES:[] — xunit pinned on that seed); spend <skill> -1 and special <unknown> reject with named ERROR lines like attack/loot; hand-built v1 refusal pinned by xunit; 133 xunit green, build 0 warnings). OPEN (documented limitation, DA2 P4-3 — kept, not fixed): a save taken MID-special-fight keeps plain-restore behaviour — the saved engagement carries its bonus-scaled LIFE but restores PLAIN (kind/affixes unstored), so the next kill pays the plain XP row; dump→save→load→dump stays byte-identical and the payout is consistent with the restored state; see README Known limits.

STATUS: ARCH-CLOSING (2026-10-31, card/11765, verdict .audits/202610301950-31593a83/out/ARCH-verdict.md §4). OPEN: row 5 mana formula flipped BUILT-M1 → BUILDING — the post-multiplier item terms are NOT implemented (BaseMana takes a flat term only), Stats.BaseMana has zero callers, and no test references Mana (needs wire + test); row 1 per-class START values are asserted by no test (no test pins the ClassProfile start rows — name or pin).

STATUS: D2-ORACLE-ALIGNMENT (2026-10-31, card/11793, owner grant 2026-10-31 — CYCLES R2 record + board cards 11777/11793: the three D2-oracle parity REDS are GREEN — data/xp-ladder.json regenerated as the 99 cumulative rows + ExpRatio curve of the granted reference table verbatim (all class columns verified identical in the table; anchors unchanged), QualityRoll rows adopt the granted item-ratio V0 divisor/min verbatim (Unique 400/2/6400 · Set 125/6/5600 · Rare 160/3/3200 · Magic 30/16/192, min kept in c units; the table minimums sit above 128 so no row auto-succeeds any more), deathpen green from the ladder with NO code change; out/parity 10 passed 0 failed with oracle.py byte-identical, rows 17 and 37 reworded below; 142 xunit green, build 0 errors; parity never fitted to the sim). FINDING: docs/ARCHITECTURE.md:148 still says the ladder rows are "interpolated" — stale after this commit, ARCHITECT pass owed.

STATUS: SKELETON WAVE LANDED (card 11794, merged to master) — game/ marker protocol VERIFIED (3 live SIM_TICKs, planted-red green, 164 tests); acts2-5/6-classes/cube/hirelings/npc skeletons parse-tested; OWED: 7th sibling class file (roster name owed to content card), screenshot leg (owner device pass), life/mana wiring = rows 4-5 unchanged.

STATUS: row 77 PAYMENT: per-area monster table landed as data/area-monsters.json (card 11825); remainder: MapGen live generation (row 59) — M4-S4, card/11828, 2026-10-31.

| # | Feature (Baba Yaga ← reference key) | Milestone | Status |
|---|---|---|---|
| 1 | 5 attributes with per-class start table [charstats] | M1 | BUILT-M1 |
| 2 | 5 stat pts + 1 skill pt per level; 5 quest skill pts | M1 | BUILT-M1 |
| 3 | Class-keyed Life/Vit, Mana/Ener, Stam/Vit rates table | M1 | BUILDING |
| 4 | Life formula incl. Increase-Max-Life multiplier | M1 | BUILT-M1 |
| 5 | Mana formula with post-multiplier item terms | M1 | BUILDING |
| 6 | Stamina: run-only drain, class drain rates, heavy-armor 2× | M1 | NOT-BUILT |
| 7 | Attack Rating = Dex×5 + flats, then %AR | M1 | BUILT-M1 |
| 8 | To-hit dual-ratio formula, 5/95 clamp, integer floors | M1 | BUILT-M1 |
| 9 | Running-always-hit (melee) rule | M1 | BUILT-M1 |
| 10 | Block formula w/ Dex−15 term, class factors 30/25/20, 75% cap | M1 | BUILT-M1 |
| 11 | Running block ÷3 (cap 25) | M1 | BUILT-M1 |
| 12 | Physical damage pipeline: weapon ED → flat → %ED → SrcDam | M1 | BUILT-M1 |
| 13 | Str/Dex per-point damage table per weapon group | M1 | BUILT-M1 |
| 14 | 2H Str-penalty switch (optional difficulty flavour) | M1 | NOT-BUILT |
| 15 | XP chain: base × killtype × players × level-gap × ExpRatio | M1 | BUILT-M1 |
| 16 | Level-gap tables both directions + clvl25 rule | M1 | BUILT-M1 |
| 17 | XP ladder: all 99 cumulative rows verbatim of the granted D2 reference table + ExpRatio curve 71→99 (1024 through L70), Lvl99 caps levels | M1 | BUILT-M1 |
| 18 | Monsters: table(level) × own-percent stat shape | M3 | BUILT-M3 |
| 19 | Area-driven monster levels in higher difficulties | M3 | BUILT-M3 |
| 20 | 3 skill trees per class, row reqs 1/6/12/18/24/30 | M2 | BUILT-M1 |
| 21 | Prerequisite arrows: max-3 prereqs w/ point reqs | M2 | BUILT-M1 |
| 22 | Synergy system on hard points (blvl), own lines on lvl | M2 | BUILT-M1 |
| 23 | Cast-speed compression curve + class frame tables | M2 | NOT-BUILT |
| 24 | Hit-recovery formula + life-trigger + thresholds | M2 | NOT-BUILT |
| 25 | Attack-speed frame pipeline + weapon speed modifier | M2 | NOT-BUILT |
| 26 | Resistances: caps 75/95, phys 50, floor −100, difficulty penalties | M1 | BUILDING |
| 27 | Immunity ≥100 monster resist; ⅕ rule on immune debuffs | M1 | BUILDING |
| 28 | Curse/aura/buff stack engine (timed stat mods, refresh) | M1 | NOT-BUILT |
| 29 | Amplify(−100)/Decrepify(−50)/Conviction-style curves | M1 | NOT-BUILT |
| 30 | Crit family: mastery→crit-strike→deadly-strike roll order | M1 | BUILT-M1 |
| 31 | Crushing Blow fractions + players divisor | M1 | NOT-BUILT |
| 32 | Open Wounds level-rate table (256ths/frame, 200 f) | M1 | NOT-BUILT |
| 33 | Poison rate×length model with mastery + SrcDam | M1 | NOT-BUILT |
| 34 | Chill/freeze durations w/ difficulty divisors | M1 | NOT-BUILT |
| 35 | Stun 1-in-10 on special monsters | M1 | NOT-BUILT |
| 36 | Drop pipeline: TC walk ≤6 items, forced-pick classes | M2 | BUILT-M1 |
| 37 | Quality roll U→S→R→M with c-formula + MF, V0 ratio/divisor/min verbatim from the granted D2 item-ratio table | M2 | BUILT-M2 |
| 38 | Classic item-ratio rows (400/160/125/30; Uber 240/96/96/3) | M2 | BUILT-M2 |
| 39 | Unique/set pick by lvl-gate + rarity weights | M2 | BUILT-M2 |
| 40 | ilvl sources (drop/champ+2/unique+3/gamble/cube) | M2 | BUILT-M2 |
| 41 | alvl affix gate formula (steeper near 99) | M2 | BUILT-M2 |
| 42 | Prefix/suffix pools w/ alvl ranges + one-per-group | M2 | BUILT-M2 |
| 43 | Magic affix-count probability table | M2 | BUILT-M2 |
| 44 | Rare itemgen: 3+3 affixes, group exclusions, req formula | M2 | BUILT-M2 |
| 45 | Set-item count-bonus tiers | M2 | BUILT-M2 |
| 46 | Sockets: min(base, type band by ilvl, size≤6) | M2 | BUILT-M2 (unit-tested; live path with M4) |
| 47 | Runewords: 5 conditions incl. exact order + plain-only | M2 | BUILT-M2 (unit-tested; live path with M4) |
| 48 | Ritual-craft recipe engine (inputs→output, ilvl rules, flags) | M2 | NOT-BUILT |
| 49 | Gambling vendor: ilvl roll, no MF, tier reveal on buy | M2 | BUILT-M2 |
| 50 | Vendor price/repair/sell mults + caps + ethereal quarter | M2 | BUILT-M2 |
| 51 | Gold piles: TC mul/256 × killer GoldFind | M2 | BUILT-M2 |
| 52 | MF quality-only rule (never quantity) | M2 | BUILT-M2 |
| 53 | Special-group roll 4:1 unique:champion; packs | M3 | BUILT-M3 (live: CLI special) |
| 54 | Champ/unique/minion life-AR-XP bonus table | M3 | BUILT-M3 (leader live via CLI special; minion bonus row unit-tested, live path with M4) |
| 55 | Champion families (5+ families as data rows) | M3 | BUILT-M3 (live: CLI special) |
| 56 | Unique affix pool: 1/2/3 rolls, minion inherit flag | M3 | BUILT-M3 (1-roll live via CLI special; 2/3 roll unit-tested, live path with M4) |
| 57 | Minion count 3–6, reduced bonuses, behaviour-immunity | M3 | BUILT-M3 (count+inherit live via CLI special; reduced-bonus spawn unit-tested, live path with M4) |
| 58 | AI script set (Melee/Ranged/Charge/Avoid) — 4 behaviours, card/11747; teleport/boss variants later | M3 | BUILT-M3 (unit-tested; live path with M4) |
| 59 | 5 acts, area graph w/ procedural + preset layouts | M3 | BUILDING |
| 60 | NPC role set: shop/repair/gamble/stash/craft/quest/hire/waypoint | M3 | NOT-BUILT |
| 61 | Waypoint network, per-char persistence | M3 | BUILT-M3 |
| 62 | Quest state machine + reward kinds | M3 | BUILT-M3 |
| 63 | Party XP pot: proximity, +87/256 bonus, level-share | M3 | BUILT-M3 (unit-tested; live path with M4) |
| 64 | Death: 0/5/10% XP penalty, ¾ corpse return | M3 | LIVE-M4 (BUILT-M3 math untouched — Respawn CALLS DeathPenalty.Apply only; TakeDamage emptying Life enters DEAD, one gate rejects Attack/UsePotion/Buy/SellLast/TravelTo/Loot/Gamble with "you are dead"; Respawn refills Life to Max (single-player stone-resurrection), relocates to the last discovered town riding the LIT STONES (adjacency bypassed — resurrection rides stones, not roads), band selectable at New (default Ordinary 0%, Hell asserted equal to the helper's own output); DEAD derives from the persisted Life pool so the save stays v4 — v4 readers unbroken; 12 BeltDeathTests + headless death leg HERO_DEAD→DEAD_REJECT→RESPAWN→HERO_ALIVE + HUD_STATE false,true,false + post-death TRADE — DEATH_OK green (plants: no-clear/no-relocate/frozen-HUD all RED) — evidence run .audits/202610301950-31593a83, leg game/scripts/DeathLeg.cs; ¾ corpse-return recovery run stays NOT-BUILT — later beat) |
| 65 | Hirelings: bracket tables, weight skill roll, hire price | M3 | NOT-BUILT |
| 66 | Merc XP chain w/ 86/256 + 1/64 cap | M3 | NOT-BUILT |
| 67 | Inventory 4×10 grid w/ w×h placement | M4 | BUILT-M2 |
| 68 | Equipment panel 10 slots + 2 weapon sets | M4 | NOT-BUILT |
| 69 | Potion belt 4-cell hotkeys 1–4 | M4 | BUILT-M4 (napitek-zycia/napitek-manny data rows + Vendor.Standard town stock; belt = session state, AddToBelt/RemoveFromBelt/UsePotion doors, counter-damage lowers the pools; 15 BeltPotionTests + headless belt leg BELT 0/4→2/4→1/4 + POTION_USE + BELT_OK green — evidence run .audits/202610301950-31593a83, leg game/scripts/BeltLeg.cs, hotkeys belt_1..belt_4 share the one UsePotion path; windowed hotkey pass pending the row-76 art window) |
| 70 | 8 skill hotkey slots + weapon-set toggle | M4 | NOT-BUILT |
| 71 | Stats/resist character sheet | M4 | NOT-BUILT |
| 72 | Automap overlay + POIs + waypoint book (act tabs) | M4 | NOT-BUILT |
| 73 | Town portal scroll w/ return-to-spot | M4 | NOT-BUILT |
| 74 | Stash multi-tab | M4 | NOT-BUILT |
| 75 | Shop / craft / game-menu widgets | M4 | NOT-BUILT |
| 76 | M4-slice DEFERRED-ART: screenshot/windowed leg open until 26 renders land | M4 | DEFERRED (tracked by run .audits/202610301950-31593a83 — close on the ART SPEC delivery (M4-placement.md §3) + the windowed leg; DA-verdict-m4.md P2-1) |
| 77 | Monster spawn mapping — per-area area→monster-key data table (data/); the slice ships the presentation-side constant map {mchy: pokrzywnik} in scripts/MonsterField.cs | M4 | NOT-BUILT (lands with MapGen row 59; DA-verdict-m4.md P2-2) |

## ROADMAP — D2 full-parity areas as milestones

Full reference parity is the end state: **all 5 acts** (each town + wilderness/caves/dungeon
chain + act boss), **7 playable classes/archetypes** (ORIGINAL Baba Yaga archetypes; names are
content design, mechanics parity per T0b), **all systems** of the checklist above.

- **M1 — Sim core (stats & combat engine):** rows 1–19 subset stats (1–17) + combat physics
  (26–35). Deliverable: BabaYaga.Sim + CLI + pytest green for attribute/derivation/to-hit/damage/
  resistance/effect/crit/DoT/stun/XP-ladder math. No engine, no items, no world.
- **M2 — Items & skills:** rows 20–25 (skill trees, synergies, frame breakpoints) + 36–52
  (full item system: quality roll, affixes, sets/uniques, socket/runes, craft engine, vendors).
  Deliverable: seeded itemgen/skill sessions reproducible from the CLI.
- **M3 — Monsters & world:** rows 18–19, 53–66 (monster stats/affixes/AI, 5-act area graph,
  mapgen, waypoints, quest state machine, party XP, death penalty, hirelings). Deliverable: a
  scripted full-act-crawl sim session end to end.
- **M4 — Presentation & UI to parity:** rows 67–75 (D2-parity screens: inventory, equipment,
  belt, hotkeys, sheet, automap, portals, stash, shop/cube/menu) in Godot, consuming the Sim API;
  7 classes playable, 5 acts completable with original content; engine headless-marker legs added.

Each milestone closes only with its pytest rows green from a fresh clone AND LEDGER rows moved
to TESTED/VERIFIED with evidence, per fleet doctrine.
