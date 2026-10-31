# Baba Yaga — Architecture (v10, 2026-10-31)

version: 10 (M4 §game/ SYNC + waypoint-order fix, card/11809 — §game/ synced to run
202610301950-31593a83 `out/M4-placement.md` rev3 (MonsterField.cs + scripts/Fnv.cs join the tree and
the Act.cs composition; boot flow carries the EXACT headless check, the leg-3b scene-execution smoke
and the real 2-arg `GameSession.Load(path, dataDir)`, .World.cs:115); the live-probed WAYPOINT ORDER
CONTRACT (light the DESTINATION stone BEFORE the travel needing it) replaces the discover-after-travel
rule — a post-travel entry-discover is a guaranteed throw (`stone … is already lit`, probes
out/.tmp/m4b @ f3d98d9, DA-verdict-m4-c2 P1-c2-1); TILE SCALE 2× + SPRITE DRAW SCALE + the ONE FNV
helper (`Fnv.Hash`, FNV-1a 32-bit, offset 2166136261 prime 16777619) stated; DoD-precedence line
added; fences even, all blocks balanced; v9 archived in git history. Earlier: v9 (M4 presentation
placement, card/11778 — the §game/ presentation section (PLANNED) + the assets/ pipeline (art pass →
assets/raw/ in git → one-way copy → game/assets/, never hand-modified in game/) + the two stale
planned game/ rows re-pointed; it matched `out/M4-placement.md` rev1 — placement later moved on and
DA-verdict-m4-c2 P2-c2-1 caught the twin drift, which v10 closes on rev3; v8 archived
in git history. Earlier: v8 (ARCH closing fix, card/11765 — code-fence repair (the M2 as-built block now closes at
its true end before the M3 header; 14 fences, all blocks balanced) + truth-status fix (Difficulty
dedup RESOLVED by MC 11751, merge a5d8d26, SharedDifficultyTests pins it); v7 archived in git
history. Earlier: v7 (DA round-2 fix addendum, card/11757 — save v3 persists the unspent skill-point
pool, the unique live path honours passesToMinions:false, two CLI grammar rejects — merged
onto v6, which had merged the M2 items & economy as-built section, MC 11746, the M3 monsters &
world as-built section, MC 11747, and the DA round-1 fix addendum (card/11755) onto the M1
foundation, MC 11745; v1 was created by the `design`
profile, run 202610301950-31593a83, MC 11739; board record MC 11743 — git history is this repo file's
version store; superseded texts are archived in commits 1351b6c / d0ab182 / 737b0dd / 895a8a4 / 5b949f0))
Maintained per layout v2: the Architect keeps this doc true to the tree. Engine decision is pinned in
`.audits/202610301950-31593a83/out/T0c-engine-decision.md` (run dir at the Games workspace root);
mechanics Bible is T0b parts 1–3 in the same `out/` dir. Length rationale (file hygiene): the ONE
canonical architecture statement stays whole — a split would create a second arch source (rejected);
the as-built blocks are the only growth, and splitting them is a ruled card, not a drive-by.

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
  tests/                      CLI journey scripts (tests/journey.txt) + the planned pytest suite
  data/                       ORIGINAL JSON tables: skills, items, monsters, acts (+ affixes,
                              runewords, cube recipes, npcs, quests, waypoints) — content, in git
  assets/                     3090-generated ORIGINAL art masters — pipeline (v9, kept at v10): the art pass lands
                              originals in assets/raw/ (git); a one-way copy feeds game/assets/ before
                              the headless --import step; nothing under game/ is ever hand-modified
                              (fix art at the assets/raw/ source, re-copy, re-import)
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

1. `src/BabaYaga.Sim.Cli` — `dotnet run --project src/BabaYaga.Sim.Cli -- --seed N
   --script <path> [--data <dir>]`: runs a scripted sim session, one JSON line per command,
   final `{"RESULT":"OK"}` exit 0, one `{"ERROR":"…"}` line exit 1. The pytest target.
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
BabaYaga.sln                              solution: Sim + Cli + Tests (game/ joins at its milestone)
src/BabaYaga.Sim/                         net8.0 class library — references nothing (dependency rule held)
  Rng.cs                        (74 ln)   seeded PCG-XSH-RR 32/64; internal State get/set carries
                                          the raw stream word into the save DTO (DA P1-1)
  Stats.cs                      (95 ln)  ClassProfile rows + life/mana/AR derivations + per-group
                                          Str/Dex damage bonuses; Koldun design row
  Character.cs                  (133 ln)  attribute allocation, XP intake → level-up grants (5+1),
                                          GrantSkillPoints = the single reward-point grant door
                                          (DA P1-2; TrySpendSkillPoint stays the only debit;
                                          RestoreSkillPoints = the serializer-only pool restore)
  XpTable.cs                    (128 ln)  ladder loader+validator, ExpRatio, level-gap tables,
                                          full kill-XP chain (floor every step)
  SkillData.cs                  (133 ln)  JSON skill-table loader; validation IS the content gate
                                          (tier caps 3/2/2/1, ≤3 prereqs, prereq edges up-row)
  SkillTree.cs                  (78 ln)   spend engine: row level reqs, prereqs, tier caps,
                                          hard-point synergy math, effective level on +skills
  CombatResolver.cs             (162 ln)  to-hit 2AR/(AR+DEF)×alvl/(alvl+dlvl) clamped 5..95,
                                          running-always-hit, block (Dex−15, class factor, 75 cap,
                                          running ÷3-after-cap), physical pipeline weaponED→flat→
                                          %ED→SrcDam with −90 clamp, crit-family order roll,
                                          elemental 75/95 cap + −100 floor, Monster + kill→XP grant
src/BabaYaga.Sim.Tests/                   net8.0 xunit project — 133 tests green at v7 (RngTests,
                                          MechanicsTests, SkillTreeTests, CombatTests, BoundaryTests,
                                          + the M3 Monster/PackAffix/AI/World suites)
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
  Vendor.cs                      (68 ln)  §4.8 buy ×mult/1024 (1024/1088/960/2048 original rows) +cap,
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
```

### As-built (M3 monsters & world, card/11747) — these files exist and are tested

```
src/BabaYaga.Sim/
  MonsterBase.cs                (60 ln)   base row (monstats shape) + final-stat math:
                                          base × growth(level) × own % ÷ 100, floored (P3 §5.1)
  MonsterTable.cs              (145 ln)  data/monsters.json loader+validator, growth-row lookup,
                                          spawn-level stamping (Ordinary = own level; Nightmare/
                                          Hell stamp the AREA's level on ordinary monsters, bosses
                                          keep own), foundation-Monster factory
  PackAffix.cs                 (238 ln)  data/packs.json shape (the ONLY reader): 4:1 special-group
                                          roll, champion pack sizes + family affixes, band bonus
                                          tables (life/AR/level/XP), Encounter composition,
                                          ComposeFor = the same rolls with a named leader (the CLI
                                          special verb, DA P2-a) — same seed replays the pack
  UniqueMonster.cs              (33 ln)  unique modifier roll 1/2/3 per band, equal chance,
                                          distinct, partial Fisher-Yates on the seeded Rng
  MonsterMinion.cs              (56 ln)  3–6 minion escort, reduced band bonus, behaviour-only
                                          AND passesToMinions:false affixes never inherit — one
                                          filter honours both flags, both pools (DA2 P2-N1);
                                          XP multiplier lives in the bonus
                                          table so KillType stays Normal (no double-count)
  MonsterAI.cs                  (91 ln)  4 pure behaviour scripts over WorldPos (P3 §5.5):
                                          melee strikes in range, ranged herds the target into
                                          band [2..6], charge commits to closing, avoid flees
                                          within its threshold or when hurt
  AreaGraph.cs                 (154 ln)  Difficulty bands + WorldPos (Chebyshev metric) +
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
  DeathPenalty.cs               (40 ln)  0/5/10 % of the current level's span, floor at the
                                          level start, level 1 free, ¾ waits on the corpse (§6.6);
                                          the M2 economy names clamp to the 10 % top band (DA P3-1)
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

### As-built (M1 close-out, card/11752) — these files exist and are tested

```
src/BabaYaga.Sim/
  TreasureClass.cs           (233 ln)  data/treasureclasses.json loader+validator (unique keys,
                                        picks≠0, forced classes carry no NoDrop entry, refs and
                                        group cycles checked at load) + the walk: |picks| draws,
                                        ≤6 outputs incl. gold, forced picks re-roll NoDrop,
                                        gold pile nuggets×mul/256×(1+GF) via Gold.Pile, MF moves
                                        quality only — never quantity (P2 §4.8–4.9). ItemForge
                                        shares the drop item shape with the gamble path.
  KillFlow.cs                 (55 ln)  kill→drop wiring: CombatResolver strike → XpTable grant →
                                        DeathDropEvent.ForKill (Champion +2 / Unique +3 ilvl) →
                                        TreasureClass walk → DropQueue. Survivor emits nothing.
  DropQueue.cs                (51 ln)  pending loot: TakeUpTo(count, accept) never swallows the
                                        rest behind a refused item; gold collected separately.
  GameSession.cs             (248 ln)  the plain-C# session the CLI/pytest drive (Godot later):
                                        tables (incl. packs.json) loaded from an injected data dir,
                                        two seeded RNG streams (combat vs drop), commands new/
                                        levelto/spend/attack/loot/equip/buy/sell/gamble — each
                                        fully applies or is rejected, nothing half-applied.
  GameSession.World.cs        (131 ln)  world half (same class, partial): stone discovery,
                                        lit-stone travel, quest accept/step/complete with the
                                        P3 §6.4 reward payouts (SkillPoint → GrantSkillPoints,
                                        DA P1-2), Special = the seeded 4:1 group roll engaging a
                                        bonus-scaled leader (DA P2-a); unknown monster named
                                        rejection like Attack (DA2 P4-2).
  SaveSerializer.cs          (215 ln)  THE save/dump serializer (Version 3): captures BOTH RNG
                                        state words so a loaded session CONTINUES its streams —
                                        never replays consumed rolls (DA P1-1); restores the
                                        unspent skill-point pool FROM THE SAVE (DA2 P1-N1);
                                        version-checked,
                                        corrupt-file → InvalidDataException never a partial load;
                                        quests restore by phase replay (rewards NOT repaid),
                                        carried items land on their exact grid cells; dumps are
                                        byte-identical across save→load→load.
  SaveDto.cs                  (71 ln)  the SessionSave document shape — declaration order IS
                                        dump order (stable JSON): seed, the two RNG state words,
                                        items carried/equipped/pending, the unspentSkillPoints
                                        pool (v3, DA2 P1-N1).
src/BabaYaga.Sim.Cli/
  Program.cs                  (38 ln)  arg parse: --seed N --script <path> [--data <dir>].
  CommandRunner.cs            (53 ln)  line loop: one JSON line per command, {"RESULT":"OK"}/exit
                                        0 at the end, first rejection = {"ERROR":"…"} + exit 1.
  ScriptedSession.cs         (250 ln)  the command grammar (new/levelto/spend/attack/special/
                                        loot/equip lastdrop/buy/sell last/gamble/waypoint/
                                        discover/quest accept|step|complete/save/load/dump) →
                                        GameSession ops; special <monster> (DA P2-a) reports the
                                        rolled group's kind/units/affixes/bonus-scaled stats;
                                        spend counts < 1 reject like attack/loot (DA2 P4-1).
src/BabaYaga.Sim.Tests/                   DropPipelineTests, SaveTests, CliScriptTests (17 new)
                                          + DA round-1: save/load RNG-continuation, P1-2 probe,
                                          special-verb tests; BoundaryTests pins 2 mutation survivors
                                          + DA round-2: quest-point save/load ×2, v1-refusal pin,
                                          spend -1 / special-unknown CLI rejects, seed-42 minion flag
tests/journey.txt                          the scripted acceptance journey (18 commands, seed 2)
tests/probe-p12.txt                        the DA round-1 P1-2 probe journey (quest point spends)
data/treasureclasses.json                  3 area tiers + 3 item groups (lup_lezny, lup_kopcowy,
                                           skarb_leszego — forced-picks class, negative picks;
                                           ORIGINAL names), gold ranges, weighted entries
```

The CLI ships; the pytest suite over its markers is still the planned test leg (LEDGER keeps it
NOT-BUILT) — journey.txt is its first scripted journey and does not duplicate the unit contracts.

### Planned (design, not built yet)

All paths below are under `src/BabaYaga.Sim/` unless prefixed otherwise. Rule sources cite the
T0b spec part/section (P1 = T0b-mechanics-spec.md, P2 = -2, P3 = -3).

src/BabaYaga.Sim/Attributes.cs            — BUILT as M1 Stats.cs + Character.cs (grants incl. quest points via GrantSkillPoints, DA P1-2) (P1 §1.1–1.2)
src/BabaYaga.Sim/LifeManaStam.cs          — BUILT as M1 Stats.cs life/mana derivations (stamina has no spec row yet) (P1 §1.3)
src/BabaYaga.Sim/ToHitBlock.cs            — BUILT inside M1 CombatResolver.cs (to-hit 5/95, block formula) (P1 §1.4–1.5)
src/BabaYaga.Sim/DamagePipeline.cs        — BUILT inside M1 CombatResolver.cs (physical order + per-group bonuses) (P1 §1.6)
src/BabaYaga.Sim/CombatResolver.cs        — PARTIALLY BUILT as M1 CombatResolver.cs (resolution order, crit→CS→DS family, elemental caps); Crushing Blow / Open Wounds / hit recovery / stun ride the status stack (P2 §3.3, §3.5)
src/BabaYaga.Sim/StatusEffectStack.cs     — curse/buff/aura timed-mod stack engine, refresh, ⅕ immune rule, Conviction curve — ~250 (P2 §3.1–3.2)
src/BabaYaga.Sim/DotSystem.cs             — poison rate×length, chill/freeze durations + difficulty divisors — ~150 (P2 §3.4)
src/BabaYaga.Sim/SkillTrees.cs            — BUILT as M1 SkillData.cs + SkillTree.cs (P1 §2.1–2.2)
src/BabaYaga.Sim/FrameBreakpoints.cs      — FCR compression, FHR, IAS+WSM frame tables — ~200 (P1 §2.3–2.4)
~~RarityRoll.cs~~ BUILT as QualityRoll.cs (as-built M2 block above)
~~ItemGen.cs~~ BUILT split as Item.cs + ItemBaseTable.cs + AffixPool.cs + RareGen.cs (as-built M2)
~~SetUnique.cs~~ BUILT as SetUniqueItems.cs (as-built M2)
~~Runewords.cs~~ BUILT as Runewords.cs + Socketing.cs (as-built M2; the socket roll split into its own file)
src/BabaYaga.Sim/CubeRecipes.cs           — recipe engine: inputs → output, ilvl rules, flags — ~150 (P2 §4.6)
src/BabaYaga.Sim/TreasureClass.cs         — BUILT as M1 close-out above (TC walk + ItemForge) (P2 §4.1, §4.8–4.9)
~~Vendors.cs~~ BUILT split as Vendor.cs + Gamble.cs + Gold.cs (as-built M2; TC gold-drop INPUT still arrives with TreasureClass.cs)
src/BabaYaga.Sim/Vendors.cs               — BUILT (dup of the struck row above): Vendor.cs + Gamble.cs + Gold.cs, as-built M2 (P2 §4.7–4.8)
src/BabaYaga.Sim/MonsterStats.cs          — SUPERSEDED by the M3 as-built MonsterBase.cs + MonsterTable.cs (same scope P3 §5.1–5.2)
src/BabaYaga.Sim/MonsterAffixes.cs        — SUPERSEDED by the M3 as-built PackAffix.cs + UniqueMonster.cs + MonsterMinion.cs (P3 §5.2–5.4)
src/BabaYaga.Sim/MonsterAI.cs             — BUILT as M3 above with the 4 DoD-named behaviours (melee/ranged/charge/avoid); caster-teleport/boss scripts arrive with the skills phase (P3 §5.5)
src/BabaYaga.Sim/XpTable.cs               — BUILT as M1; party-pot 87/256 built as M3 XpShare.cs (P1 §1.7; P3 §6.6)
src/BabaYaga.Sim/DeathPenalty.cs          — BUILT as M3 above (P3 §6.6)
src/BabaYaga.Sim/Hirelings.cs             — merc bracket tables, weight skill roll, hire price, merc XP chain — ~200 (P3 §6.7)
src/BabaYaga.Sim/QuestState.cs            — BUILT as M3 above (P3 §6.4)
src/BabaYaga.Sim/WaypointGraph.cs         — BUILT as M3 above (P3 §6.1, §6.3)
src/BabaYaga.Sim/MapGen.cs                — preset + procedural area layout rules, density roll, quest-object slots — ~250 (P3 §6.5)
src/BabaYaga.Sim/DataTables.cs            — BUILT distributed: each table ships its own loader+validator (MonsterTable/TreasureClass/QuestLog/… — one file per table shape)
src/BabaYaga.Sim/SaveSerializer.cs        — BUILT as M1 close-out above (SaveSerializer + SaveDto)
src/BabaYaga.Sim/SimSession.cs            — superseded by GameSession.cs + GameSession.World.cs (M1 close-out; scripted commands, no tick loop yet)
src/BabaYaga.Sim.Cli/Program.cs           — BUILT as M1 close-out above (Program + CommandRunner + ScriptedSession)
~~game/Main.cs + Main.tscn~~ — SUPERSEDED by the v10 §game/ placement below: the entry is
    game/Main.tscn scripted by scripts/Boot.cs (the Main.cs name retired before it was ever built)
~~game/scenes/…~~ — SUPERSEDED for the slice by the v10 §game/ placement below; the M4-WIDE
    per-screen list (inventory grid, skill panel, automap overlay, shop, cube, stash, TP) stays
    planned for later slices; T0b P3 §7 inventory

`tests/` (pytest, one file per mechanic domain, ≤600-line test ceiling): `test_stats.py`,
`test_combat.py`, `test_itemgen.py`, `test_monsters.py`, `test_progression.py`, `test_save.py` —
each asserts on CLI markers/exit codes only.

## game/ — presentation layer (PLANNED at v10 — placement card/11778, this sync card/11809; nothing built yet)

The executable file-level plan and the 26-render ART SPEC the art pass consumes verbatim are
`out/M4-placement.md` in run `202610301950-31593a83` (run dir at the Games workspace root); slice
acceptance is `out/M4-slice-DoD.md` beside it. PRECEDENCE (v10, DA-verdict-m4-c2 P2-c2-1): where any
doc conflicts, `out/M4-slice-DoD.md` rev3 + this section govern; `out/M4-placement.md` is the
executable detail. The M4 SLICE: Bobrowisko (town) and Mchary (one wilderness area) renderable, the
hero walks and strikes one monster row, the HUD binds live session state, the headless marker leg is
green. The tree block below is `out/M4-placement.md` rev3 §1 VERBATIM — byte-diff of the two fenced
blocks is EMPTY (proof row in the card RESULT); its inline §-refs resolve inside M4-placement.md,
where the bridge table and ART SPEC live. It is the contract the slice builder follows.

```
game/
  project.godot           config/name "BabaYaga"; features PackedStringArray("4.7","GL Compatibility");
                          run/main_scene="res://Main.tscn"; [dotnet] project/assembly_name="Game";
                          rendering/method="gl_compatibility"; [input] ONLY here:
                          move_to (LMB) · attack (RMB) · inventory_toggle (Tab) · game_menu (Esc) · save_game (F5)
  Game.csproj             Sdk="Godot.NET.Sdk/4.7.2", TargetFramework=net8.0, Nullable=enable,
                          TreatWarningsAsErrors=true; ProjectReference ../src/BabaYaga.Sim/BabaYaga.Sim.csproj.
                          NOT added to BabaYaga.sln (deliberate, §6)
  Main.tscn               root Node scripted by scripts/Boot.cs; shape per T0c §4.3:
                          [ext_resource type="Script" path="res://scripts/Boot.cs"] (no class=/language=)
  scenes/MainMenu.tscn    scripts/MainMenu.cs  — title card + New Game / Continue / Quit         ~80 ln
  scenes/Act.tscn         scripts/Act.cs       — composes IsoCamera+AreaRenderer+MonsterField+
                                                  HeroController+Hud (rev2: MonsterField joins the
                                                  composition — the monster sprite owner, below)  ~120 ln
  scenes/Inventory.tscn   scripts/InventoryPanel.cs — panel OVER Act.tscn; never a 2nd session   ~150 ln
  scripts/Boot.cs         entry. headless — the EXACT check (rev2, DA P4-1):
                          `DisplayServer.GetName() == "headless"` (`DisplayServer.Headless` is NOT
                          in the 4.7.2 bindings — CS0117, DA probe). Then: GameSession.New(42,
                          "koldun",dataDir) → ≥3 SIM_TICK lines → ONE scenes/Act.tscn instantiate
                          through the placeholder pass → DA_SCENE_ACT_OK → ORCH_M4_OK → Quit(0)
                          (T0c §6 marker shape; rev2 leg 3b — compile is not execution; rev3: the
                          smoke performs NO waypoint verb after any travel — DoD 3b).
                          windowed: → MainMenu.tscn. Zero rules.                                ~90 ln
  scripts/GameContext.cs  the ONE session holder passed between scenes — plain static:
                          New(seed)/Save()/Load()/Session + user://save path. NO autoload singleton. ~60 ln
  scripts/IsoCamera.cs    Camera2D: grid W×H <-> screen 2:1 iso (sx=(x−y)·TILE_W/2, sy=(x+y)·TILE_H/2,
                          TILE 128×64 CELLS — art is 256×128 drawn at scale 0.5, §3 TILE SCALE
                          CONTRACT); drag-clamped to W×H; range/range-check math calls
                          WorldPos.DistanceTo — Chebyshev, reused, never re-implemented            ~100 ln
  scripts/AreaRenderer.cs TileMapLayer per area from res://assets/tiles/*.png; display-only layout
                          seed=Fnv.Hash(area key) (the ONE helper, Fnv.cs below) until MapGen (row
                          59) ships; ResourceLoader.Exists guard → palette-color diamond
                          placeholders when art absent (PushWarning once);
                          EXPOSES its walkable-cell list (rev2: MonsterField lays its row on it)     ~180 ln
  scripts/MonsterField.cs MONSTER SPRITE OWNER (rev2, DA P2-2 — presentation-side; the Sim stores NO
                          monster positions, §2 invariant, so the slice's monsters live ONLY here):
                          draws the M1–M3 idle sprites, hit-tests clicks, hands the clicked monster
                          KEY to HeroController. Slice area→monster-keys map is a code constant
                          { "mchy": pokrzywnik }; grodzisko is town (isTown: true — none spawn).
                          No per-area spawn table exists in data/ (monsters.json carries no area
                          field — checked rev2): the data-driven table is the MONSTER-RENDERER DEBT,
                          exact LEDGER row in M4-slice-DoD.md's DEBT-ROW block. Spawner seed =
                          Fnv.Hash(area + ":mon") (Fnv.Hash(s) ONLY — no second implementation,
                          Fnv.cs below) over AreaRenderer's walkable cells, one idle row
                          of N=4 sprites until the data table ships.                                ~110 ln
  scripts/Fnv.cs          the ONE FNV helper (rev3, DA P3-c2-1): static Fnv.Hash(string) = FNV-1a
                          32-bit over the string's UTF-8 bytes — h=2166136261 (offset), per byte
                          h=(h^b)*16777619 (prime), uint32 wrap; AreaRenderer + MonsterField call
                          Fnv.Hash(s) ONLY — NO second implementation anywhere; seeds are
                          display-only (placeholder layout, spawner), never persisted state           ~20 ln
  scripts/HeroController.cs consumes [input] actions ONLY (InputEventMouseButton, no polling);
                          click-to-move = grid path (A* over walkable cells, WORLD-LOCAL — the Sim
                          stores no position, §2 invariant); click monster (cell key from
                          MonsterField) → GameSession.Attack(key,1); loot key → Loot(1); area-edge
                          door → the WAYPOINT ORDER CONTRACT (§2, the ONE wording, rev3 live-probed): light
                          the DESTINATION stone BEFORE the travel needing it —
                          `if (!session.Waypoints.IsDiscovered(dest)) session.DiscoverWaypoint(dest);`
                          THEN `session.TravelTo(dest)` (catch SessionRejectedException → HUD hint,
                          never a crash); the rev2 discover-AFTER-travel entry rule is DELETED — a
                          guaranteed throw (§2 WHY; DA-verdict-m4-c2 P1-c2-1); hero frame =
                          screen-dominant step, §3 FACING CONVENTION                               ~185 ln
  scripts/Hud.cs          life/mana orbs + XP bar + gold + engaged-target line + rite counter —
                          reads state each _Process, zero rules                                   ~150 ln
  scripts/InventoryPanel.cs 4×10 grid + equipment panel bound to session.Carried / Equipment     ~150 ln
  data/                   ONE-WAY copy of repo data/*.json (git-tracked copy); dataDir for
                          GameSession.New = ProjectSettings.GlobalizePath("res://data");
                          never hand-edited — drift gate `diff -r data game/data` (DoD §6; JSON
                          takes no .import sidecars — DA probe COUNT=0)
  assets/                 ONE-WAY copy of repo assets/ (tiles·hero·monsters·ui·title). rev2 rule
                          (DA P1-3): Godot `--import` writes ONE `.import` sidecar beside EACH
                          source INSIDE game/assets — sidecars are engine-generated, git-tracked,
                          never hand-edited — so the drift gate compares SOURCES ONLY:
                          `diff -r assets game/assets -x '*.import'` EMPTY (DoD §6). A plain
                          `diff -r` here is permanently RED once art lands and must not be used.
```

State bridge (invariant — violating it is a bug): `game/` calls the GameSession public surface the
CLI ALREADY proves — New/Save/Load/Dump; Level/Gold/CurrentArea/EngagedKey; Character.Level/Xp/
MaxLife/MaxMana; Ladder.TotalXpForLevel/MaxLevel; Purse.Amount; Drops.Pending/Gold/LastDrop;
Carried (DisplayName/Quality/Base.Width/Height — the per-cell map is internal, the panel
re-derives); Equipment.Equipped; Areas/Waypoints/Quests; commands Attack/Loot/EquipLastDrop/Buy/
SellLast/Gamble/Special/TravelTo/DiscoverWaypoint/QuestAccept/QuestStep/QuestComplete. ZERO Sim
edits for M4. Hero screen/grid position lives in HeroController.cs alone; the HUD is pull-only —
no Sim event bus is invented to mirror. The orb rows show MAX values until the current-life pool
(ledger rows 4–5, BUILDING) ships — a documented gap, not a hidden bug.

WAYPOINT ORDER CONTRACT (v10 — the ONE wording, identical in M4-placement.md §1/§2; live-probed
this run at `out/.tmp/m4b/probe1..4` on the real CLI @ f3d98d9): the HeroController door intent is
`if (!session.Waypoints.IsDiscovered(dest)) session.DiscoverWaypoint(dest); session.TravelTo(dest);`
— the DESTINATION stone lights BEFORE the travel needing it; catch `SessionRejectedException` → HUD
hint, never a crash. WHY (source + live readings): `WaypointGraph.CanTravel` requires BOTH endpoints'
stones lit (WaypointGraph.cs:84) and `GameSession.New` lights ONLY the town stone (GameSession.cs:87);
`DiscoverWaypoint` THROWS on an already-lit stone (.World.cs:11-15). Probe outcomes: discover-then-
travel = RESULT OK (the journey.txt ln 13→14 order); travel-first = REJECT `no lit-stone path from
'grodzisko' to 'mchy'`; re-discover = REJECT `stone in 'mchy' is already lit`; and because every
successful arrival is AT an already-lit stone, a discover-AFTER-travel entry rule throws on 100% of
travels (live) — it is NOT the mechanism and the placement rev2 rule that carried it is deleted
(DA-verdict-m4-c2 P1-c2-1). The headless smoke performs no waypoint verb after any travel (DoD 3b).

Boot flow (v10, synced to placement rev3): project.godot → Main.tscn(Boot.cs) → [headless — the
EXACT check `DisplayServer.GetName() == "headless"` (`DisplayServer.Headless` does not exist in the
4.7.2 bindings): `GameSession.New(42,"koldun",dataDir)` → ≥3 `SIM_TICK` lines → ONE `scenes/Act.tscn`
instantiate through the placeholder pass printing `DA_SCENE_ACT_OK` BEFORE `ORCH_M4_OK` (the leg-3b
scene-execution smoke — compile is not execution) → Quit(0), exit 0] / [windowed: MainMenu → New
Game (GameContext.New(seed)) or Continue (`GameSession.Load(savePath, dataDir)` — the real 2-arg
signature `static Load(string path, string dataDir)`, .World.cs:115; savePath is the `user://` save,
dataDir = `GlobalizePath("res://data")`; a 1-arg call does not exist)] → Act.tscn renders
`session.CurrentArea` (slice: grodzisko ↔ mchy) → Tab toggles Inventory.tscn.

TILE SCALE CONTRACT (v10, placement rev3 §3): tile ART is 256×128 px; the iso CELL is 128×64 px; the
TileMapLayer draws each art tile at scale 0.5 — exactly 2× on both axes (256/128 = 128/64 = 2.0);
cells stay 128×64, art stays 256×128, neither side re-interpreted. SPRITE DRAW SCALE: the 0.5 scale
belongs to TILE ART ONLY — hero frames (128×128) and monster frames (160×160/frame) draw at scale
1.0, feet anchored at cell centre, Y-sorted after the tiles (DA-verdict-m4-c2 P4-c2-3 folded).

FNV (v10, DA-verdict-m4-c2 P3-c2-1 — one implementation, mechanism-monogamy): the single helper is
`scripts/Fnv.cs` — `Fnv.Hash(string)` = FNV-1a 32-bit over the string's UTF-8 bytes (offset
2166136261, prime 16777619, uint32 wrap). AreaRenderer's layout seed = `Fnv.Hash(area)`;
MonsterField's spawner seed = `Fnv.Hash(area + ":mon")`; `Fnv.Hash(s)` ONLY — no second FNV loop
anywhere; both seeds are display-only, never persisted state.

PLANNED assets/ pipeline: 3090 imagegen originals (spec verbatim in out/M4-placement.md §3) →
repo assets/raw/ (git) → one-way copy → game/assets/ → `"$GODOT" --headless --path game --import`
→ Sprite2D/AnimatedSprite2D/TileMapLayer consume. The R2 wall binds art too: every render ORIGINAL.
Sidecar rule (placement rev2/rev3 §1, DoD leg 6): `--import` writes git-tracked `.import` sidecars
beside each source inside game/assets, so the drift gate is SOURCE-ONLY
(`diff -r assets game/assets -x '*.import'` EMPTY).


## Trade-offs rejected (named, with reason)

- GDScript-only on the standard 4.7.2 binary — rejected: no C# (owner preference; T0c §5), fleet C# gates can't run on it.
- Raw C++ SDL2 / GDExtension C++ hot path — rejected for now: cmake/scons/clang/libsdl2 absent on VM350 (T0c §1); buys nothing the C# Sim cannot do at this scale. Revisit only if profiling names a hot loop.
- Sim inside the Godot assembly (one project) — rejected: every test leg would pay an engine process and lose determinism; the two-layer seam is last-animal-verified (T0c §2).
- pytest driving the Godot binary as the primary gate — rejected as PRIMARY (kept as later legs): slower, non-deterministic display path, and rules bugs would hide behind presentation. Engine legs add markers later per T0c.
- A separate repo for the Sim — rejected: two-repo version dance for one product; in-repo project reference keeps the seam without a second mechanism.
- Sibling dir naming (`babayaga-audit-*/`, seat-prefixed) — rejected: layout v2 defect (one project = one dir `Games/babayaga/`, group-dir rule).
- Autoload singleton for the session (v9) — rejected: `GameContext.cs` plain static holder is the one visible C# mechanism; an engine-lifecycle node buys nothing a static doesn't answer for a non-visual concern.
- Hero position into the Sim as new commands (v9) — rejected: the bridge is the CLI-proven surface; widening it voids the ZERO-Sim-edits contract; movement is presentation-owned state.
- A second distance metric in game code (euclidean pixel math, v9) — rejected: IsoCamera range checks reuse `WorldPos.DistanceTo` (Chebyshev) — one metric, mechanism-monogamy.
- HUD recomputing derived numbers (v9) — rejected: game-side rule compute is misplaced code (§dependency rule); the HUD reads only bridge surface.
- Adding game/Game.csproj to BabaYaga.sln for the slice (v9) — rejected: the xunit/pytest gates stay engine-process- and nuget-free (the sim-first cheap gate, T0c §2); re-admitting it is a full-M4 decision with named engine-leg rows.

## Truth status of this document

The As-built sections above match the tree at this commit: M1 foundation (seeded RNG, stats,
XP/level, 8x8 skill-grid engine, combat resolver, xp-ladder + koldun-skills data), M2 items &
economy (12 Sim files, 6 data tables, 38 tests) and M3 monsters & world (12 Sim files, 5 data
tables, 32 tests), the M1 close-out (drop pipeline + session + save/load + CLI) and the DA
round-1 fixes (save v2 stream persistence, quest skill-point grant, CLI special verb, boundary
pins) and the DA round-2 fixes (save v3 unspent-skill-point pool, passesToMinions honoured on the
unique live path, spend/special CLI rejects, v1-refusal pin) — 133 tests total. One semantic-merge defect (duplicate Difficulty enum across Vendor.cs and
AreaGraph.cs, each branch green alone) is resolved by MC 11751 (merge a5d8d26,
SharedDifficultyTests pins it); this section reads TRUE
only when the merged master builds and tests green again (VERIFIED this commit: 0 warnings, 133/133 green). The M2 build replaced planned
RarityRoll/ItemGen/Vendors names with per-concern files; the M3 build superseded MonsterStats/
MonsterAffixes planned rows (noted inline); the M1 close-out built what the "Planned" list used to
mislabel as DESIGN — `src/BabaYaga.Sim.Cli/` and the TC-walk drop pipeline (36) are AS-BUILT, not
design. What remains DESIGN: `game/`, `tests/` (the pytest-marker suite over CLI stdout; the
xunit CliScriptTests + journey.txt/probe-p12.txt scripted journeys exist — the pytest harness does
not), `assets/`, MapGen (row 59), NPC roster (60), hirelings (65-66), ritual-craft engine (48),
stash tabs (74), frame breakpoints (23-25), weapon sets (68). The next build phase must update
this file to match what it actually created, in the same run. v9 adds DESIGN TEXT ONLY (the §game/
section, the assets/raw/ pipeline note, the two re-pointed planned rows, five v9 trade-off rows);
v10 is DESIGN TEXT ONLY TOO (the §game/ re-sync to placement rev3 verbatim, the live-probed WAYPOINT
ORDER CONTRACT, TILE-SCALE/FNV/sidecar notes and the precedence line — the State-bridge paragraph
stands verbatim, its command set re-proven identical to placement §2, DA-verdict-m4-c2 RC1):
no game file exists, the As-built sections stand verbatim, and §game/ flips to as-built only in
the M4 slice's own commit.
