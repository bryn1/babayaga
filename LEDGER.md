# Baba Yaga — LEDGER

Project status ledger (the ONE ledger at repo root; moves with every commit).
Feature rows seeded from the PARITY CHECKLIST in
`.audits/202610301950-31593a83/out/T0b-mechanics-spec-3.md` (run dir at the Games workspace root).
Bracketed keys are reference keys into the D2 mechanics spec only — product names are ORIGINAL
(clean-room rule, docs/ARCHITECTURE.md §R2). Statuses: NOT-BUILT / BUILDING / TESTED / VERIFIED.

STATUS: M1-FOUNDATION (first code commit 2026-10-30, MC 11745: src/BabaYaga.Sim + xunit tests + xp-ladder/koldun-skills data green). BUILT-M1 = mechanic fully implemented+tested in the Sim (class content rows for the other six classes, stamina (row 6), phys-50 cap + difficulty penalties (row 26), ⅕ rule (row 27), crush/open-wounds/DoT/stun (31–35) and everything later remain open).

| # | Feature (Baba Yaga ← reference key) | Milestone | Status |
|---|---|---|---|
| 1 | 5 attributes with per-class start table [charstats] | M1 | BUILT-M1 |
| 2 | 5 stat pts + 1 skill pt per level; 5 quest skill pts | M1 | BUILT-M1 |
| 3 | Class-keyed Life/Vit, Mana/Ener, Stam/Vit rates table | M1 | BUILDING |
| 4 | Life formula incl. Increase-Max-Life multiplier | M1 | BUILT-M1 |
| 5 | Mana formula with post-multiplier item terms | M1 | BUILT-M1 |
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
| 17 | XP ladder anchors + ExpRatio curve 70→99, Lvl99 stops | M1 | BUILT-M1 |
| 18 | Monsters: table(level) × own-percent stat shape | M3 | NOT-BUILT |
| 19 | Area-driven monster levels in higher difficulties | M3 | NOT-BUILT |
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
| 36 | Drop pipeline: TC walk ≤6 items, forced-pick classes | M2 | NOT-BUILT |
| 37 | Quality roll U→S→R→M with c-formula + MF | M2 | NOT-BUILT |
| 38 | Classic item-ratio rows (400/160/125/30; Uber 240/96/96/3) | M2 | NOT-BUILT |
| 39 | Unique/set pick by lvl-gate + rarity weights | M2 | NOT-BUILT |
| 40 | ilvl sources (drop/champ+2/unique+3/gamble/cube) | M2 | NOT-BUILT |
| 41 | alvl affix gate formula (steeper near 99) | M2 | NOT-BUILT |
| 42 | Prefix/suffix pools w/ alvl ranges + one-per-group | M2 | NOT-BUILT |
| 43 | Magic affix-count probability table | M2 | NOT-BUILT |
| 44 | Rare itemgen: 3+3 affixes, group exclusions, req formula | M2 | NOT-BUILT |
| 45 | Set-item count-bonus tiers | M2 | NOT-BUILT |
| 46 | Sockets: min(base, type band by ilvl, size≤6) | M2 | NOT-BUILT |
| 47 | Runewords: 5 conditions incl. exact order + plain-only | M2 | NOT-BUILT |
| 48 | Ritual-craft recipe engine (inputs→output, ilvl rules, flags) | M2 | NOT-BUILT |
| 49 | Gambling vendor: ilvl roll, no MF, tier reveal on buy | M2 | NOT-BUILT |
| 50 | Vendor price/repair/sell mults + caps + ethereal quarter | M2 | NOT-BUILT |
| 51 | Gold piles: TC mul/256 × killer GoldFind | M2 | NOT-BUILT |
| 52 | MF quality-only rule (never quantity) | M2 | NOT-BUILT |
| 53 | Special-group roll 4:1 unique:champion; packs | M3 | NOT-BUILT |
| 54 | Champ/unique/minion life-AR-XP bonus table | M3 | NOT-BUILT |
| 55 | Champion families (5+ families as data rows) | M3 | NOT-BUILT |
| 56 | Unique affix pool: 1/2/3 rolls, minion inherit flag | M3 | NOT-BUILT |
| 57 | Minion count 3–6, reduced bonuses, behaviour-immunity | M3 | NOT-BUILT |
| 58 | AI script set (Melee/Archer/Caster/Teleporter/Fleer/Boss) | M3 | NOT-BUILT |
| 59 | 5 acts, area graph w/ procedural + preset layouts | M3 | NOT-BUILT |
| 60 | NPC role set: shop/repair/gamble/stash/craft/quest/hire/waypoint | M3 | NOT-BUILT |
| 61 | Waypoint network, per-char persistence, portal interop | M3 | NOT-BUILT |
| 62 | Quest state machine + reward kinds | M3 | NOT-BUILT |
| 63 | Party XP pot: proximity, +87/256 bonus, level-share | M3 | NOT-BUILT |
| 64 | Death: 0/5/10% XP penalty, ¾ corpse return | M3 | NOT-BUILT |
| 65 | Hirelings: bracket tables, weight skill roll, hire price | M3 | NOT-BUILT |
| 66 | Merc XP chain w/ 86/256 + 1/64 cap | M3 | NOT-BUILT |
| 67 | Inventory 4×10 grid w/ w×h placement | M4 | NOT-BUILT |
| 68 | Equipment panel 10 slots + 2 weapon sets | M4 | NOT-BUILT |
| 69 | Potion belt 4-cell hotkeys 1–4 | M4 | NOT-BUILT |
| 70 | 8 skill hotkey slots + weapon-set toggle | M4 | NOT-BUILT |
| 71 | Stats/resist character sheet | M4 | NOT-BUILT |
| 72 | Automap overlay + POIs + waypoint book (act tabs) | M4 | NOT-BUILT |
| 73 | Town portal scroll w/ return-to-spot | M4 | NOT-BUILT |
| 74 | Stash multi-tab | M4 | NOT-BUILT |
| 75 | Shop / craft / game-menu widgets | M4 | NOT-BUILT |

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
