# Baba Yaga — Architecture (v1, 2026-10-30)

version: 1 (created by the `design` profile, run 202610301950-31593a83, MC 11739; board record MC 11743)
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
src/BabaYaga.Sim/RarityRoll.cs            — quality roll Uncommon→Superior→Magic→Rare, c-formula + MF, classic ratio rows — ~150 (P2 §4.1)
src/BabaYaga.Sim/ItemGen.cs               — ilvl/alvl gates, prefix/suffix pools one-per-group, magic/rare affixgen, reqs — ~250 (P2 §4.2–4.3)
src/BabaYaga.Sim/SetUnique.cs             — set/unique pick by lvl-gate + rarity weights, set count-bonus tiers — ~200 (P2 §4.4)
src/BabaYaga.Sim/Runewords.cs             — socket roll (band by ilvl, size cap), runeword 5 conditions, exact order — ~200 (P2 §4.5)
src/BabaYaga.Sim/CubeRecipes.cs           — recipe engine: inputs → output, ilvl rules, flags — ~150 (P2 §4.6)
src/BabaYaga.Sim/TreasureClass.cs         — drop pipeline: TC walk ≤6 items, forced picks, gold piles, MF-quality-only — ~250 (P2 §4.1, §4.8–4.9)
src/BabaYaga.Sim/Vendors.cs               — buy/sell/gamble/repair price multipliers, caps, gamble ilvl roll — ~200 (P2 §4.7–4.8)
src/BabaYaga.Sim/MonsterStats.cs          — table(level) × own-percent stat shape, special-group 4:1 roll, champ/unique bonus tables — ~250 (P3 §5.1–5.2)
src/BabaYaga.Sim/MonsterAffixes.cs        — unique affix pool 1/2/3 rolls, minion inherit flag, champion families as data — ~200 (P3 §5.3)
src/BabaYaga.Sim/MonsterAI.cs             — 6 behaviour scripts (Melee/Archer/Caster/Teleporter/Fleer/Boss) over Sim state — ~250 (P3 §5.5)
src/BabaYaga.Sim/XpTable.cs               — XP ladder anchors + ExpRatio curve, level-gap tables, XP chain, party pot 87/256 — ~250 (P1 §1.7; P3 §6.6)
src/BabaYaga.Sim/DeathPenalty.cs          — 0/5/10% XP cost, ¾ corpse return, level-floor rule — ~100 (P3 §6.6)
src/BabaYaga.Sim/Hirelings.cs             — merc bracket tables, weight skill roll, hire price, merc XP chain — ~200 (P3 §6.7)
src/BabaYaga.Sim/QuestState.cs            — per-quest state machine inactive→active→completable→reward→done, reward kinds — ~200 (P3 §6.4)
src/BabaYaga.Sim/WaypointGraph.cs         — area graph, waypoint activation, per-char persistence, TP interop hook — ~150 (P3 §6.1, §6.3)
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

Modules above are the DESIGN (nothing under `src/`, `game/`, `tests/`, `data/`, `assets/` exists yet —
this commit is the skeleton). The first build phase must update this file to match what it actually
created, in the same run.
