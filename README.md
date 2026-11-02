# Baba Yaga

**"Baba Yaga"** — an ORIGINAL-IP, single-player isometric action RPG (ARPG) set in a medieval
Slavic witch-fantasy world of the Baba Yaga myth. All product content — class, creature,
monster, hireling, area and quest names (`koldun`, `strzelczyni`, `Zaginione Gęsi`,
`Głowa Leszego`, `grodzisko`, …) — is original. The **mechanics** are D2:LoD-class: Diablo II:
LoD (v1.10–1.13 era) served this project ONLY as the mechanics verification oracle — every law
was verified against it, then re-expressed under original names (clean-room rule,
docs/ARCHITECTURE.md §R2). Zero Blizzard assets, zero D2-derived product names: "Diablo II"
appears in this repo only as a cited reference key in provenance comments, docs and the LEDGER.
No license applies to third-party content because none is present.

## State — M4 released (v0.4.0); M5 presentation gate PASSED (untagged)

The tagged release is v0.4.0 (Milestone 4). MILESTONE 5 ("make it visible") passed its
twin-green windowed gate 2026-11-02 (`TEST-verdict-m5-b.md`, VERDICT PASS, JUDGED
f01bcde2, 445 tests, parity 10/10) — the game is windowed-playable NOW (see PLAY).
v0.5.0 rides a later owner-PASS commit (no tag in this pass). The owner's own device run
(PLAYTEST.md) is the one open row.

What is LIVE (each item cites its LEDGER.md row; laws in docs/MECHANICS.md):

- **Town loop** — vendors (buy/sell/gamble), potion belt (4 cells, hotkeys), town stash,
  mercenary captain (hire/dismiss/revive), death returns the hero to town (rows 49–51, 69, 74, 64).
- **4 act-1 quests** run the full loop: accept → travel to the goal area → step → complete,
  reward paid EXACTLY ONCE (skill point, life potion, a lit waypoint stone, a real stash tab)
  (rows 62, 81).
- **Walkable + lit-stone world** — one travel door: WALK a discovered neighbour or RIDE the
  lit stones; walking lights nothing and rolls no RNG (row 80).
- **Hireling laws** — hire price `Gold×(100+15·levels_above)/100`, own XP ladder with the
  86/256 hero-share of the per-kill-capped figure, revive `min(15·L²/2, 50 000)` at the town
  captain, THE 4-SLOT GEAR LAW (weapon/body/helm/shield only), stats through gear (range swap,
  ED % with −90 clamp, flat-before-%, worn +life raises MaxLife), and the hero-death law: the
  hero dying kills a living hireling at the same instant (rows 65, 66, 83; §6.7/6.7b/6.7c amendments).
- **Life by potions** — NO passive life regeneration, BY LAW (the potion economy IS the design);
  mana regenerates on the verified D2 per-frame quantum (row 79).
- **Stash** — town-only chest, tab 1 + reward-unlocked tab 2, grid store/take; gold bank is a
  future row (StoreGold refuses by name) (row 74).
- **Scripted CLI verbs** — the full session grammar incl. `rest / hire / dismiss / revive /
  stash_store / stash_take / give / strip` (row 82).

Evidence measured for this tag (2026-11-02, HEAD 511e1c0; run dir
`.audits/202610301950-31593a83` at the Games workspace root):

- `dotnet test BabaYaga.sln` → **Failed: 0, Passed: 356, Skipped: 0** (xunit rule contracts).
- D2 table-parity harness → **10/10** (maintainer-side, out-of-repo — see §Test).
- Headless Godot story, two consecutive runs **byte-identical, exit 0**, printing
  `SIM_TICK ×3`, `QUEST_OK`, `MANA_OK`, `REGEN_OK`, `BELT_OK`, `DEATH_OK`, `TRADE_OK`,
  `ACT1_OPEN_OK`, `HIRE_OK`, `GEAR_OK`, `GEAR_STATS_OK`, `STASH_OK`, `ORCH_M4_OK` with 0 ERROR lines.

## Build & run

Sim-only (no engine needed) — from the repo root:

```bash
dotnet build src/BabaYaga.Sim.Cli -c Release
dotnet src/BabaYaga.Sim.Cli/bin/Release/net8.0/BabaYaga.Sim.Cli.dll \
    --seed 2 --script tests/journey.txt --data data
```

A script is one command per line (`#` comments); a session ends `{"RESULT":"OK"}` exit 0, any
rejection prints one `{"ERROR":"…"}` line exit 1. The full verb grammar is documented at the
top of `src/BabaYaga.Sim.Cli/ScriptedSession.cs`. (Plain `dotnet run --project src/BabaYaga.Sim.Cli -- …`
also works but occasionally misses a just-built dll — the dll form above is the reliable one.)

Headless story run (the Godot presentation layer walks, casts, hires and stores for real):

```bash
GODOT_BIN=/home/claudecode/Games/.tools/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
dotnet build game/Game.csproj            # THE BUILD-TWICE LAW: build it a SECOND time and
dotnet build game/Game.csproj            # require "0 Error(s)" on both before trusting markers
HOME=/tmp "$GODOT_BIN" --headless --path game --import
HOME=/tmp "$GODOT_BIN" --headless --path game --quit-after 240   # story log; expect exit 0
```

(The `HOME=/tmp` prefix only sidesteps a first-run user-data prompt on headless hosts. Engine
pin: Godot 4.7.2-stable official .NET/mono build, sha256 `129f82db…bff247`; C# / net8.0,
dotnet SDK 8.0.133 is sufficient.)

## PLAY — the game is windowed-playable NOW

```bash
GODOT_BIN=/home/claudecode/Games/.tools/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT_BIN" --path game          # after the two builds + one --import (Build & run above)
```

Controls (each wired in `game/project.godot` [input] and consumed in `game/scripts/`):
LMB walk / click lit stone to ride (HeroController), RMB attack (HeroController), TAB
inventory (Act), T town panel in grodzisko (Act), C town chest (stash) in grodzisko
(Act), 1–4 belt potions + R first spark (Hud), F5 save (Act), ESC = death menu with
RESPAWN when dead (Act; alive = hint only,
M5 ships no pause). Owner-facing checklist with what to expect per row: **PLAYTEST.md**.
M5 gate evidence: run `.audits/202610301950-31593a83/out/TEST-verdict-m5-b.md`.

## Test

```bash
dotnet test BabaYaga.sln        # 445 xunit rule-contract tests, engine-process-free
                                # (M5 gate 2026-11-02: 445 passed, 0 failed)
```

Beyond the in-repo suite, maintainers run an OUT-OF-REPO D2 table-parity harness (10 checks:
XP ladder, to-hit/block, party XP + death penalty, quality roll, gamble/vendor, monster stat
shape) that drives this exact CLI against the granted reference tables — it lives beside the
audit run dir, deliberately not in the product repo; at this tag it passes 10/10.

## Known limits (honest list)

- **Windowed play is LIVE** (M5, gate-passed 2026-11-02 — see PLAY + PLAYTEST.md); the
  remaining windowed UI is open LEDGER rows: equipment panel + weapon sets 68, hotkeys
  70, character sheet 71, automap 72, town portal scroll 73, shop/craft/game-menu widget
  halves of 75; the owner-device playtest (76) is the open pass.
- **Real item damage/ED/life data rows are future**: `data/item-bases.json` carries no damage
  columns yet; the gear-stat mechanism is proven on synthetic bases (row 66 DATA-FUTURE).
- **Merc complex curves DEFERRED by design**: gear-driven attack rating, shield block %,
  +skills, IAS, procs, Str/Dex requirements (docs/MECHANICS.md §Stats through gear).
- **Acts 2–5 unwritten**: their data rows exist as parse-tested skeletons; only act 1 is live.
- **Save = v7 JSON**, foreign versions refused by name, no upgrade path; a save taken
  mid-special-fight restores the monster plain (documented, intentional, LEDGER DA-ROUND-2 row).
- **Regen fractions are session-only**: the 256ths mana accumulator is not a save field, so a
  load may drop a sub-1-point pending tick; Replenish-Life/leech are future own rows.
- Walking needs a DISCOVERED endpoint (dark↔dark wander refused by tested contract); ¾
  corpse-return recovery NOT built (row 64); ritual-craft (48), procedural layout (59),
  curse/aura stack engine and effect families (28–35) NOT-BUILT.

## Where truth lives

LEDGER.md — feature rows + per-milestone STATUS lines (the ONE ledger, moves with every commit);
docs/MECHANICS.md — the live laws with their AMENDED dates; docs/ARCHITECTURE.md — module map and
dependency rule; docs/RELEASE-v0.4.0.md — this release's shipped/limits/not-shipped record.
