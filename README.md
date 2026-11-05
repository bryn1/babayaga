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

## State — pre-v0.5.0: M4 tagged (v0.4.0); the v0.5.0 notes are committed, the tag awaits the owner green run

The tagged release is v0.4.0 (Milestone 4). MILESTONE 5 ("make it visible") passed its
twin-green windowed gate 2026-11-02 (`TEST-verdict-m5-b.md`, VERDICT PASS, JUDGED
f01bcde2, 445 tests at that gate, parity 10/10) — the game is windowed-playable NOW (see
PLAY). v0.5.0 is PRE-STAGED: its release-notes body exists as a committed artifact
(`docs/RELEASE-v0.5.0.md` — shipped + known limits + promised-not-shipped) and the doc-sync
sweep ran clean (MC 11991); **the tag flips in the SAME commit as the owner's green
PLAYTEST.md run** (row 76 flipped LIVE 2026-11-04 via owner release order; kit rev4 remains available; v0.5.0 ships a self-contained Windows BabaYaga.exe).

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
- **Ritual craft — the bone cube (B, in town)**: five oracle-witnessed, balance-audited
  recipes (`data/cube-recipes.json`, audit MC 11985/11986); the cube reads the CARRIED pack
  — no cube-grid container yet (rows 48, 75; MC 11969/11975/11978).
- **A hired strzelczyni walks beside you — VISUAL-ONLY** (sprite only, no follower combat;
  FollowerField, MC 11977) and true-alpha painted art across the slice (row 85 D1–D6 CURED).
- **Life by potions** — NO passive life regeneration, BY LAW (the potion economy IS the design);
  mana regenerates on the verified D2 per-frame quantum (row 79).
- **Stash** — town-only chest, tab 1 + reward-unlocked tab 2, grid store/take; gold bank is a
  future row (StoreGold refuses by name) (row 74).
- **Scripted CLI verbs** — the full session grammar incl. `rest / hire / dismiss / revive /
  stash_store / stash_take / give / strip` (row 82).

Evidence measured at the v0.4.0 tag (2026-11-02, HEAD 511e1c0; run dir
`.audits/202610301950-31593a83` at the Games workspace root):

- `dotnet test BabaYaga.sln` → **Failed: 0, Passed: 356, Skipped: 0** (xunit rule contracts).
- D2 table-parity harness → **10/10** (maintainer-side, out-of-repo — see §Test).
- Headless Godot story, two consecutive runs **byte-identical, exit 0**, printing
  `SIM_TICK ×3`, `QUEST_OK`, `MANA_OK`, `REGEN_OK`, `BELT_OK`, `DEATH_OK`, `TRADE_OK`,
  `ACT1_OPEN_OK`, `HIRE_OK`, `GEAR_OK`, `GEAR_STATS_OK`, `STASH_OK`, `ORCH_M4_OK` with 0 ERROR lines.

Evidence measured for the v0.5.0 pre-staging (2026-11-03, base 76754b9, MC 11991):

- `dotnet test BabaYaga.sln` → **Passed! Failed: 0, Passed: 635, Skipped: 0**.
- Headless story, two consecutive runs **byte-identical, exit 0**, stdout md5
  `40c1e0a2e23501efa32906b9d894942d` (LEDGER row 87's re-pin; the v0.4.0 marker set above
  plus `DA_SCENE_ACT_OK`/`JOURNEY_OK`), 0 ERROR lines; game build ×2 `0 Error(s)`,
  `--import` exit 0.

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
bash assets/import.sh                    # ONE-TIME per clone: the painted art lives in
                                         # assets/raw/ (git truth); game/assets/ is its
                                         # one-way copy and is NOT in git (see .gitignore)
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
LMB attack the monster under the cursor / walk on empty ground / click lit stone to ride (HeroController —
MC 12136: LMB attacks too, the D2 idiom; kills STAY DEAD for the session; the hit-box is the DRAWN sprite;
the controls line rides the HUD), RMB attack also (HeroController), TAB
inventory (Act), T town panel in grodzisko (Act), C town chest (stash) in grodzisko
(Act), B bone-cube ritual-craft panel in grodzisko (Act — lists admitted recipes only,
zero admitted paints the honest empty line), 1–4 belt potions + R first spark (Hud),
F5 save (Act), ESC (`game_menu` action) =
the game menu RESUME / SAVE & QUIT when alive — SAVE & QUIT rides the F5 save door then
the title screen — and the death menu with RESPAWN when dead, where the game menu never
opens (Act; the D2 truth: in Diablo II, Escape opens the game menu). Owner-facing
checklist with what to expect per row: **PLAYTEST.md**.
M5 gate evidence: run `.audits/202610301950-31593a83/out/TEST-verdict-m5-b.md`.

## Test

```bash
dotnet test BabaYaga.sln        # 635 xunit rule-contract tests, engine-process-free
                                # (measured 2026-11-03; M5 gate 2026-11-02: 445 passed, 0 failed)
```

Beyond the in-repo suite, maintainers run an OUT-OF-REPO D2 table-parity harness (10 checks:
XP ladder, to-hit/block, party XP + death penalty, quality roll, gamble/vendor, monster stat
shape) that drives this exact CLI against the granted reference tables — it lives beside the
audit run dir, deliberately not in the product repo; last recorded on a merged tree at 10/10
(LEDGER row 84 and later row appends; re-runs are maintainer-side).

## Known limits (honest list)

- **Windowed play is LIVE** (M5, gate-passed 2026-11-02 — see PLAY + PLAYTEST.md); the town
  panel, stash chest, cube and game-menu widgets are LIVE too (row 75 half: MC 11940/11953/
  11957/11975). The remaining windowed UI is open LEDGER rows: equipment paper-doll panel +
  weapon sets 68, skill hotkeys 70, character sheet 71, automap 72, town portal scroll 73;
  the owner-device playtest (76) is the open pass.
- **Real item damage rows landed for weapons** (`data/item-bases.json` carries
  `damageMin/damageMax` per weapon base — the §6.7c range swap is observable, MC 11914 +
  the D1 data beat MC 11928); armor/ED-side real-data rows stay future and the gear-stat
  test witnesses are still SYNTHETIC (row 66 DATA-FUTURE); the `+energy` affix is code-live
  but DATA-SILENT (zero energy rows in `data/affixes.json`).
- **Merc complex curves DEFERRED by design**: gear-driven attack rating, shield block %,
  +skills, IAS, procs, Str/Dex requirements (docs/MECHANICS.md §Stats through gear).
- **Acts 2–5 unwritten**: their data rows exist as parse-tested skeletons; only act 1 is live.
- **Save = v7 JSON**, foreign versions refused by name, no upgrade path; a save taken
  mid-special-fight restores the monster plain (documented, intentional, LEDGER DA-ROUND-2 row).
- **Regen fractions are session-only**: the 256ths mana accumulator is not a save field, so a
  load may drop a sub-1-point pending tick; Replenish-Life/leech are future own rows.
- Walking needs a DISCOVERED endpoint (dark↔dark wander refused by tested contract); ¾
  corpse-return recovery NOT built (row 64); procedural layout still open (row 59 — LEDGER
  status BUILDING); curse/aura stack engine and the damage-effect family rows (28–29, 31–35)
  unbuilt — the crit family (30) IS built. Ritual-craft (48) SHIPPED with the v0.5.0 slice
  (cube panel, five recipes); no cube-grid container and no windowed SELL button yet.

## Where truth lives

LEDGER.md — feature rows + per-milestone STATUS lines (the ONE ledger, moves with every commit);
docs/MECHANICS.md — the live laws with their AMENDED dates; docs/ARCHITECTURE.md — module map and
dependency rule; docs/RELEASE-v0.4.0.md — the v0.4.0 tag's shipped/limits/not-shipped record;
docs/RELEASE-v0.5.0.md — the pre-staged v0.5.0 record (tag flips on the owner's green PLAYTEST run).
