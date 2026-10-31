# Baba Yaga — Architecture (v4, 2026-10-31)

version: 4 (merge of the M2 items & economy as-built section, MC 11746, and the M3 monsters &
world as-built section, MC 11747, onto the M1 foundation, MC 11745; v1 was created by the `design`
profile, run 202610301950-31593a83, MC 11739; board record MC 11743 — git history is this repo file's
version store; superseded texts are archived in commits 1351b6c / d0ab182 / 737b0dd / 895a8a4)
Maintained per layout v2: the Architect keeps this doc true to the tree. Engine decision is pinned in
`.audits/202610301950-31593a83/out/T0c-engine-decision.md` (run dir at the Games workspace root);
mechanics Bible is T0b parts 1–3 in the same `out/` dir.

## What the product is

Single-player isometric ARPG with D2:LoD-class mechanics, delivered as an ORIGINAL-IP product:
"Baba Yaga" — a medieval Slavic witch-fantasy world. Engine: Godot 4.7.2-stable official .NET (mono)
build; language: C# / net8.0 (pinned by T0c; the mono binary lives at
`/home/claudecode/Games/.tools/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64`).

## R2 clean-room rule (architecture rule, not a suggestion)

Mechanics are re-expressed under ORIGINAL Baba Yaga names from the T0b spec (the `[charstats]`-style
brackets there are reference keys only, never product names). The D2 reference material lives ONLY in
the run scratch dir `/home/claudecode/Games/.audits/202610301950-31593a83/.tmp/d2/` (the T0a inventory
doc is not written yet; this scratch path is the material location regardless) and is cited as
reference. No Blizzard-derived asset, name, or table content may enter `data/`, `assets/`, or any
source file. Every `data/*.json` row is original content; a row traceable to a Blizzard table cell
verbatim is a violation to be rejected at review.

## Why sim-first

All game rules live in a pure-C# library (`src/BabaYaga.Sim/`) with zero Godot types, per T0c §6 and
last-animal's proven two-layer seam. Consequences:

- The pytest suite drives `src/BabaYaga.Sim.Cli/` — a plain `dotnet` process. Deterministic, seeded,
  no engine process, no display, no `user://` state. This is the fleet's cheap gate shape
  ("pure dotnet, NO engine process", last-animal LEDGER precedent quoted in T0c §2).
- Engine legs (`godot --headless` runs asserting exit code + named `GD.Print` markers) are added
  LATER per T0c §6 — they verify presentation wiring, never game rules.
- A rule bug must be reproducible from the CLI alone; if it needs the engine to reproduce, the seam
  is leaking and that is an architecture defect.

## Repo tree map

```
babayaga/                     this git tree (root = repo root, layout v2)
  README.md                   what the game is + build/run commands
  LEDGER.md                   per-feature status (PARITY CHECKLIST seeded) + ROADMAP M1..M4
  docs/ARCHITECTURE.md        this file — the ONE canonical architecture statement
  .audits/<run>/              (created by future audit/verify runs, layout v2)
  .tmp/                       scratch, gitignored — marker file only
  src/BabaYaga.Sim/           PURE C# class library — all game rules. Zero Godot references.
  src/BabaYaga.Sim.Cli/       dotnet console driver: scripted headless sim sessions = the pytest target
  game/                       Godot 4.7 mono project (project.godot, babayaga.csproj) — presentation
                              ONLY; consumes the Sim public API, holds no game rules
  tests/                      pytest suite driving BabaYaga.Sim.Cli (stdout markers + exit codes)
  data/                       ORIGINAL JSON tables: skills, items, monsters, acts (+ affixes,
                              runewords, cube recipes, npcs, quests, waypoints) — content, in git
  assets/                     3090-generated ORIGINAL art masters; copied into game/assets/ before
                              the headless --import step (one-way copy, never edited in game/)
```

## Dependency rule (mechanism-monogamy: one place for rules, one for pixels)

```
Dependency rule:  Game -> Sim -> nothing
                  Cli  -> Sim
                  tests -> Cli (stdout + exit code only — the contract surface)
```

- `BabaYaga.Sim` references nothing but .NET stdlib (zero Godot assemblies, zero project references).
- `game/` (Godot) references `BabaYaga.Sim` and consumes its public API; a game-side file that
  computes a rule is misplaced code, moved out at review.
- `data/` JSON is read by the Sim (path injected by the caller); the CLI takes `--data <dir>`,
  the game build ships the same JSON under `res://data/` by copy at export/pack time.
- No circular reference, ever: Sim never names Cli or game.

## Ports: none

Single-player desktop only. No server, no listening port, no IPC, no localhost dependency.

## Data stores

- Read: the ORIGINAL JSON tables in `data/` (git-versioned, the content source of truth).
- Write: `user://` save files at runtime (Godot), JSON, produced/consumed ONLY through
  `src/BabaYaga.Sim/SaveSerializer.cs` so headless and engine paths share one serializer.

## Entrypoints

1. `src/BabaYaga.Sim.Cli` — `dotnet run --project src/BabaYaga.Sim.Cli -- <session-script>`:
   runs a scripted sim session, prints named markers, exits 0/1. The pytest target.
2. Godot main scene `game/Main.tscn` (root script `Main.cs`) — presentation host; instantiates a
   Sim session and renders its state. Same binary runs `--headless` and windowed (T0c §4).

## Build + run commands (verbatim from T0c §6; `<project>` = `game/` in this repo)

```bash
# once per added/changed asset or script:
"$GODOT" --headless --path <project> --import
# build C#:
dotnet build <project>/babayaga.csproj        # Sdk="Godot.NET.Sdk/4.7.2", TargetFramework=net8.0
# engine sim leg (the pytest acceptance gate): exit code + named GD.Print markers
"$GODOT" --headless --path <project> -- <mode-arg>
# hygiene when script-class lookup mysteriously fails:
rm -rf <project>/.godot/mono/temp && dotnet build <project>/babayaga.csproj
```

`$GODOT` = `/home/claudecode/Games/.tools/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64`
(T0c-pinned, sha256 `129f82db…bff247`). Sim-only leg, no engine:
`dotnet run --project src/BabaYaga.Sim.Cli -- --seed <n> --script <file.json> --data data/`.
Carried habits (T0c §4 pitfalls): one worktree per concurrent engine run; never trust a stale
`.godot/mono/temp` (the hygiene line above); headless game runs never need the editor import.

## Module inventory — one file per concern, ~250-line target each (400 hard ceiling w/ header reason)

### As-built (M1 foundation, first code commit, MC 11745) — these files exist and are tested

```
BabaYaga.sln                              solution: Sim + Tests (game/ + Cli join at their milestones)
src/BabaYaga.Sim/                         net8.0 class library — references nothing (dependency rule held)
  Rng.cs                        (66 ln)   seeded PCG-XSH-RR 32/64; BCL Random forbidden, not used
  Stats.cs                      (102 ln)  ClassProfile rows + life/mana/AR derivations + per-group
                                          Str/Dex damage bonuses; Koldun design row
  Character.cs                  (113 ln)  attribute allocation, XP intake → level-up grants (5+1)
  XpTable.cs                    (128 ln)  ladder loader+validator, ExpRatio, level-gap tables,
                                          full kill-XP chain (floor every step)
  SkillData.cs                  (133 ln)  JSON skill-table loader; validation IS the content gate
                                          (tier caps 3/2/2/1, ≤3 prereqs, prereq edges up-row)
  SkillTree.cs                  (81 ln)   spend engine: row level reqs, prereqs, tier caps,
                                          hard-point synergy math, effective level on +skills
  CombatResolver.cs             (162 ln)  to-hit 2AR/(AR+DEF)×alvl/(alvl+dlvl) clamped 5..95,
                                          running-always-hit, block (Dex−15, class factor, 75 cap,
                                          running ÷3-after-cap), physical pipeline weaponED→flat→
                                          %ED→SrcDam with −90 clamp, crit-family order roll,
                                          elemental 75/95 cap + −100 floor, Monster + kill→XP grant
src/BabaYaga.Sim.Tests/                   net8.0 xunit project — 64 tests green (RngTests,
                                          MechanicsTests, SkillTreeTests, CombatTests, + the M3
                                          Monster/PackAffix/AI/World suites)
data/xp-ladder.json                       anchors exact per P1 §1.7; intermediate rows interpolated
data/koldun-skills.json                   Koldun = first playable class (ORIGINAL IP): 3 trees
                                          (Popioly/Uroki/Krag × 4 skills), prereqs, synergies
```

### As-built (M2 items & economy, MC 11746) — these files exist and are tested

```
src/BabaYaga.Sim/                         (M2 additions — same net8.0 library, still zero Godot refs)
  Item.cs                       (121 ln)  Item instance shape (quality/sockets/affix-record/mods) +
                                          ItemLevels: ilvl sources drop/champ+2/unique+3/gamble/cube
                                          and the §4.2 alvl band formula (incl. magic-lvl-add bases)
  ItemBaseTable.cs              (125 ln)  data/item-bases.json loader; validation IS the content gate
                                          (unique ids, cells, socket ceiling ≤6, equiv chains resolve)
  QualityRoll.cs                (105 ln)  §4.1 c-formula engine, classic 400/160/125/30 + uber
                                          240/96/96/3 rows, unique→set→rare→magic tries, rare ilvl≥27
                                          gate, MF quality-only, white low/normal/superior split
  AffixPool.cs                  (175 ln)  data/affixes.json loader + §4.2 pools: alvl window, type
                                          filter, one-affix-per-group, frequency-weighted pick,
                                          configurable magic affix-count table (27/42 steps), BuildMagic
  RareGen.cs                     (63 ln)  §4.3 rare gen: 3+3 rare-allowed affixes, group exclusions,
                                          req template highest+10+3/affix capped 98
  SetUniqueItems.cs             (122 ln)  data/uniques.json loader + §4.4 pick: lvl≤ilvl gate, weight
                                          60/(rarity+1), nummods ranges, set count-bonus tiers
  Socketing.cs                   (36 ln)  §4.5: max = min(base, type band ≤25/≤40/else, cells ≤ 6)
  Runewords.cs                   (98 ln)  runes/runewords loaders + the 5 conditions (type+equiv,
                                          white-only, exact count, exact order, availability), re-checkable
  Gamble.cs                      (69 ln)  §4.7: ilvl = clamp(clvl−5+0..9, 5, 99), face-down offer,
                                          tier revealed on buy, MF pinned 0
  Vendor.cs                      (72 ln)  §4.8 buy ×mult/1024 (1024/1088/960/2048 original rows) +cap,
                                          sell ×512/1024 ethereal ÷4 class ÷4, act/difficulty caps
                                          5k–25k/30k/35k, repair ×missing/max×128/1024, purse gate
  Gold.cs                        (40 ln)  gold pile = TC gold × mul/256 × (1+killer GF), floored;
                                          Purse (no-debt spend); signature holds no MF (§4.1 rule)
  InventoryGrid.cs              (110 ln)  4×10 grid w/ w×h placement + 10-slot equipment panel keyed
                                          by base type; set-piece worn-count for §4.4 tiers
src/BabaYaga.Sim.Tests/                   +38 tests → 70 green (ItemGenTests, AffixTests,
                                          RareGenTests, VendorEconomyTests)
data/item-bases.json                      22 ORIGINAL bases (19 quality-rolling + 3 gems), qlvl/cost/
                                          cells/sockets/equiv chains
data/affixes.json                         33 ORIGINAL affixes (17 prefix / 16 suffix) w/ alvl windows,
                                          groups, rare gate, itype filters
data/uniques.json                         6 uniques + 2 sets (7 pieces) + count-bonus tiers, ORIGINAL IP
data/runes.json                           8 rune-stones (scribe-mark names, ORIGINAL)
data/runewords.json                       4 words incl. one availability-gated (condition 5 exercised)
data/gems.json                            6 gem rows (raw → next-rank transform rows; cube consumes later)

### As-built (M3 monsters & world, card/11747) — these files exist and are tested

```
src/BabaYaga.Sim/
  MonsterBase.cs                (60 ln)   base row (monstats shape) + final-stat math:
                                          base × growth(level) × own % ÷ 100, floored (P3 §5.1)
  MonsterTable.cs              (145 ln)  data/monsters.json loader+validator, growth-row lookup,
                                          spawn-level stamping (Ordinary = own level; Nightmare/
                                          Hell stamp the AREA's level on ordinary monsters, bosses
                                          keep own), foundation-Monster factory
  PackAffix.cs                 (234 ln)  data/packs.json shape (the ONLY reader): 4:1 special-group
                                          roll, champion pack sizes + family affixes, band bonus
                                          tables (life/AR/level/XP), Encounter composition —
                                          same seed replays the identical pack
  UniqueMonster.cs              (33 ln)  unique modifier roll 1/2/3 per band, equal chance,
                                          distinct, partial Fisher-Yates on the seeded Rng
  MonsterMinion.cs              (54 ln)  3–6 minion escort, reduced band bonus, behaviour-only
                                          affixes never inherit; XP multiplier lives in the bonus
                                          table so KillType stays Normal (no double-count)
  MonsterAI.cs                  (91 ln)  4 pure behaviour scripts over WorldPos (P3 §5.5):
                                          melee strikes in range, ranged herds the target into
                                          band [2..6], charge commits to closing, avoid flees
                                          within its threshold or when hurt
  AreaGraph.cs                 (147 ln)  Difficulty bands + WorldPos (Chebyshev metric) +
                                          data/act1-areas.json graph: one town, symmetric edges,
                                          per-band area monster levels (P3 §6.1)
  WaypointGraph.cs             (102 ln)  data/waypoints.json: permanent per-char discovery,
                                          travel ONLY between lit nodes, town is the hub,
                                          stone-to-stone needs a lit path (P3 §6.3)
  QuestState.cs                (147 ln)  data/act1-quests.json: inactive→active→completable→
                                          reward-pending→done FSM, every Try-method rejects
                                          out-of-order; prereq-gated starts; cycle check at load
                                          (P3 §6.4)
  XpShare.cs                    (59 ln)  party pot ×(256+87·(n−1))/256, living members within
                                          80 units, level-weighted split, floors only — payout
                                          never exceeds the pot (P3 §6.6)
  DeathPenalty.cs               (39 ln)  0/5/10 % of the current level's span, floor at the
                                          level start, level 1 free, ¾ waits on the corpse (§6.6)
  DeathDropEvent.cs             (28 ln)  plain emit-record {AffixKind, Ilvl, AreaSeed} on a kill;
                                          calls NO item-generation code — the sibling owns that,
                                          a later phase wires them (M2/M3 seam)
src/BabaYaga.Sim.Tests/                   MonsterTests, PackAffixTests, AITests, WorldTests
data/monsters.json                        12 ORIGINAL rows (Leszun, Strzyga, Leszy-boss…) + growth
                                          table — SHAPE from the reference, every name/cell invented
data/packs.json                           8 champion families + 10 unique affixes (ORIGINAL Slavic
                                          witch-fantasy names), band bonus tables, size ranges
data/act1-areas.json                      Act "Bór": Bobrowisko town + 5 wilderness areas +
                                          Czelusc Leszego boss lair, edges, density, per-band levels
data/act1-quests.json                     3 side rites + the head-hunt, ordered prereqs, reward kinds
data/waypoints.json                       5 standing stones across the act
```

The xunit project sits under `src/` beside the library; the planned `tests/` pytest-over-CLI suite
arrives with `src/BabaYaga.Sim.Cli/` (later M1 leg) — it drives the CLI stdout markers and will not
duplicate these unit contracts.

### Planned (design, not built yet)

All paths below are under `src/BabaYaga.Sim/` unless prefixed otherwise. Rule sources cite the
T0b spec part/section (P1 = T0b-mechanics-spec.md, P2 = -2, P3 = -3).

src/BabaYaga.Sim/Attributes.cs            — 5-attribute model, per-archetype start table, stat/skill-point grants — ~150 (P1 §1.1–1.2)
src/BabaYaga.Sim/LifeManaStam.cs          — Life/Mana/Stamina formulas incl. multiplier slots, class rates — ~200 (P1 §1.3)
src/BabaYaga.Sim/ToHitBlock.cs            — Attack Rating, dual-ratio to-hit 5/95 clamp, block formula + class factors — ~200 (P1 §1.4–1.5)
src/BabaYaga.Sim/DamagePipeline.cs        — physical damage order: weapon ED → flat → %ED → SrcDam; str/dex per-weapon-group — ~250 (P1 §1.6)
src/BabaYaga.Sim/CombatResolver.cs        — hit resolution order, crit→CS→DS family, Crushing Blow, Open Wounds, hit recovery, stun — ~250 (P2 §3.3, §3.5)
src/BabaYaga.Sim/StatusEffectStack.cs     — curse/buff/aura timed-mod stack engine, refresh, ⅕ immune rule, Conviction curve — ~250 (P2 §3.1–3.2)
src/BabaYaga.Sim/DotSystem.cs             — poison rate×length, chill/freeze durations + difficulty divisors — ~150 (P2 §3.4)
src/BabaYaga.Sim/SkillTrees.cs            — 3 trees/archetype, row reqs, prerequisite graph, synergy-on-hard-points — ~250 (P1 §2.1–2.2)
src/BabaYaga.Sim/FrameBreakpoints.cs      — FCR compression, FHR, IAS+WSM frame tables — ~200 (P1 §2.3–2.4)
~~RarityRoll.cs~~ BUILT as QualityRoll.cs (as-built M2 block above)
~~ItemGen.cs~~ BUILT split as Item.cs + ItemBaseTable.cs + AffixPool.cs + RareGen.cs (as-built M2)
~~SetUnique.cs~~ BUILT as SetUniqueItems.cs (as-built M2)
~~Runewords.cs~~ BUILT as Runewords.cs + Socketing.cs (as-built M2; the socket roll split into its own file)
src/BabaYaga.Sim/CubeRecipes.cs           — recipe engine: inputs → output, ilvl rules, flags — ~150 (P2 §4.6)
src/BabaYaga.Sim/TreasureClass.cs         — drop pipeline: TC walk ≤6 items, forced picks, gold piles, MF-quality-only — ~250 (P2 §4.1, §4.8–4.9)
~~Vendors.cs~~ BUILT split as Vendor.cs + Gamble.cs + Gold.cs (as-built M2; TC gold-drop INPUT still arrives with TreasureClass.cs)
src/BabaYaga.Sim/Vendors.cs               — buy/sell/gamble/repair price multipliers, caps, gamble ilvl roll — ~200 (P2 §4.7–4.8)
src/BabaYaga.Sim/MonsterStats.cs          — SUPERSEDED by the M3 as-built MonsterBase.cs + MonsterTable.cs (same scope P3 §5.1–5.2)
src/BabaYaga.Sim/MonsterAffixes.cs        — SUPERSEDED by the M3 as-built PackAffix.cs + UniqueMonster.cs + MonsterMinion.cs (P3 §5.2–5.4)
src/BabaYaga.Sim/MonsterAI.cs             — BUILT as M3 above with the 4 DoD-named behaviours (melee/ranged/charge/avoid); caster-teleport/boss scripts arrive with the skills phase (P3 §5.5)
src/BabaYaga.Sim/XpTable.cs               — BUILT as M1; party-pot 87/256 built as M3 XpShare.cs (P1 §1.7; P3 §6.6)
src/BabaYaga.Sim/DeathPenalty.cs          — BUILT as M3 above (P3 §6.6)
src/BabaYaga.Sim/Hirelings.cs             — merc bracket tables, weight skill roll, hire price, merc XP chain — ~200 (P3 §6.7)
src/BabaYaga.Sim/QuestState.cs            — BUILT as M3 above (P3 §6.4)
src/BabaYaga.Sim/WaypointGraph.cs         — BUILT as M3 above (P3 §6.1, §6.3)
src/BabaYaga.Sim/MapGen.cs                — preset + procedural area layout rules, density roll, quest-object slots — ~250 (P3 §6.5)
src/BabaYaga.Sim/DataTables.cs            — load/validate data/*.json into typed tables (the ONLY file touching table shape) — ~200
src/BabaYaga.Sim/SaveSerializer.cs        — deterministic JSON save/load for whole session state — ~200
src/BabaYaga.Sim/SimSession.cs            — seeded session driver: init → tick loop → scripted commands → markers — ~250
src/BabaYaga.Sim.Cli/Program.cs           — arg parse (--seed --script --data --mode), run SimSession, print markers, exit code — ~150
game/Main.cs + Main.tscn                  — presentation root; instantiates SimSession, no rules — ~150
game/scenes/…                             — per-screen presentation (inventory grid, skill panel, automap overlay, shop, cube, stash, TP) — each ≤250; T0b P3 §7 inventory

`tests/` (pytest, one file per mechanic domain, ≤600-line test ceiling): `test_stats.py`,
`test_combat.py`, `test_itemgen.py`, `test_monsters.py`, `test_progression.py`, `test_save.py` —
each asserts on CLI markers/exit codes only.

## Trade-offs rejected (named, with reason)

- GDScript-only on the standard 4.7.2 binary — rejected: no C# (owner preference; T0c §5), fleet C# gates can't run on it.
- Raw C++ SDL2 / GDExtension C++ hot path — rejected for now: cmake/scons/clang/libsdl2 absent on VM350 (T0c §1); buys nothing the C# Sim cannot do at this scale. Revisit only if profiling names a hot loop.
- Sim inside the Godot assembly (one project) — rejected: every test leg would pay an engine process and lose determinism; the two-layer seam is last-animal-verified (T0c §2).
- pytest driving the Godot binary as the primary gate — rejected as PRIMARY (kept as later legs): slower, non-deterministic display path, and rules bugs would hide behind presentation. Engine legs add markers later per T0c.
- A separate repo for the Sim — rejected: two-repo version dance for one product; in-repo project reference keeps the seam without a second mechanism.
- Sibling dir naming (`babayaga-audit-*/`, seat-prefixed) — rejected: layout v2 defect (one project = one dir `Games/babayaga/`, group-dir rule).

## Truth status of this document

<<<<<<< HEAD
The two As-built sections above match the tree at this commit (M1 foundation + M3 monsters &
world: Sim library, 64 xunit tests, xp-ladder/koldun-skills + monsters/packs/act1-areas/
act1-quests/waypoints data). Everything still under "Planned" remains DESIGN — `game/`,
`src/BabaYaga.Sim.Cli/`, `tests/` (pytest) and `assets/` do not exist yet, and several planned
module names were merged into as-built files (noted inline where a row was SUPERSEDED).
MapGen (procedural layout), hirelings and the NPC roster remain open M3 rows. The next build
phase must again update this file to match what it actually created, in the same run.
=======
The As-built sections above match the tree at this commit (M1 foundation: Sim library, xunit
tests, xp-ladder + Koldun skill data; M2 items & economy: 12 Sim files, 6 data tables, 70 tests).
The M2 build replaced the planned RarityRoll/ItemGen/SetUnique/Runewords/Vendors names with the
per-concern files named in the as-built block (one concern per file; the planned ~250-line
monoliths would each have mixed three concerns). Everything under "Planned" remains DESIGN — `game/`,
`src/BabaYaga.Sim.Cli/`, `tests/` (pytest) and `assets/` do not exist yet, and the planned
module names there (Attributes.cs, LifeManaStam.cs, ToHitBlock.cs, DamagePipeline.cs,
SkillTrees.cs…) were partly merged into the as-built files for the M1 scope (Stats/Character/
CombatResolver/SkillData+SkillTree/XpTable); later milestones split further files as named.
The first later build phase must again update this file to match what it actually created,
in the same run.
>>>>>>> card/11746
