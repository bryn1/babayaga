# Baba Yaga — Release v0.5.0 (the M5 "make it visible" slice) · pre-staged 2026-11-03

**PRE-RELEASE RECORD — TAG NOT YET CUT.** This is the committed release-notes body the
owner's green playtest run flips into the tag (same commit, see the PLAYTEST row below).
The tagged release today remains v0.4.0 (M4).

Original-IP medieval Slavic witch-fantasy single-player ARPG; all product content (names,
creatures, quests, areas, classes, roster) is original. The mechanics verification oracle
was the granted reference tables of the v1.10–1.13 mechanics era — the ONLY place any
reference-era material was used (clean-room rule, docs/ARCHITECTURE.md §R2). Laws with
their amendments: `docs/MECHANICS.md`; feature evidence: `LEDGER.md` rows named below.

Evidence measured for this pre-staging (2026-11-03, base commit 76754b9, worktree card/11991):
**635/635 xunit green** (`Passed! - Failed: 0, Passed: 635, Skipped: 0`); game build twice,
both `0 Error(s)`; `--import` exit 0; the headless story runs twice **byte-identical, exit 0,
stdout md5 `40c1e0a2e23501efa32906b9d894942d`** (the LEDGER row-87 re-pin), 0 ERROR lines,
printing SIM_TICK ×3 + DA_SCENE_ACT_OK, JOURNEY_OK, TRADE_OK, BELT_OK, DEATH_OK, QUEST_OK,
MANA_OK, REGEN_OK, ACT1_OPEN_OK, GEAR_OK + GEAR_STATS_OK, HIRE_OK, STASH_OK, ORCH_M4_OK.
The D2 table-parity harness is the out-of-repo maintainer harness (README §Test): last
recorded 10/10 on a merged tree (LEDGER row 84 and later row appends); it was NOT re-run in
this docs-only pass — treat 10/10 at THIS commit as UNVERIFIED-this-pass.

## WHAT SHIPS — the windowed act-1 slice

- **A windowed Godot 4.7.2 (.NET/mono) game over the deterministic byte-anchored sim.**
  Title card (New Game / Continue / Quit), an isometric world, and the sim underneath that
  replays byte-identically at a seed — the story stdout above IS a shipped-session anchor.
- **Town life in grodzisko.** Vesna's stall: three BUY rows (Soluga, life potion, mana
  potion) plus the face-down **GAMBLE** row (MC 11987) — one offer at a time, tier revealed
  only at buy. The captain: **HIRE / REVIVE / DISMISS** a strzelczyni, her portrait on her
  row. The town chest (C): stash store/take across tabs. Waypoint stones: click a lit stone
  to ride it; walking between neighbours lights nothing and rolls no RNG (rows 49–51, 61,
  65, 69, 74, 80; widgets rows 74/75 LIVE-M5).
- **RITUAL CRAFT — the bone cube (B, in town).** FIVE oracle-witnessed, balance-audited
  cube recipes shipped in `data/cube-recipes.json`: Resin into Stone, Stone upon Stone,
  Amber Heart, Three Rings One Ward, The Resin-Coated Blade. Deterministic multiset match
  over the CARRIED pack, honest refusals (missing reagents name the count; a full pack is
  refused before anything burns), magic-forced outputs riding the existing generation
  pipeline. The balance audit is PAID: MC 11985 verdict FIX, the one-row fix landed MC 11986
  (Amber Heart's fixed mod = the honey row verbatim), the other four shipped as audited
  (rows 48, 75; MC 11969/11978/11975).
- **A hired strzelczyni walks beside you — VISUAL-ONLY.** Her sprite holds the free tile
  next to the hero in every area; she fights nothing and nothing fights her (her combat is
  a later card). The presentation class calls NO sim door and writes NO session state,
  xunit-pinned (FollowerField/FollowerFieldTests; row 85 D5, MC 11977).
- **Hand-painted true-alpha art.** SEVEN monster idle sheets (kikimora, kretownik, leszun,
  pokrzywnik, ponurak, strzyga, widlast) — every monster kind the live act-1 spawn rows
  stand has its painted sheet; the D7 grey-box defect is CURED (four sheets re-rendered
  true-alpha, every sheet pixel-checked; this pass re-measured corner alpha: cured sheets
  0/255) (row 85 D1–D4/D5/D6 CURED, MC 11958/11975/11977). Hero: 4-direction idle art, he
  TURNS as he travels; walk frames are committed but the renderer reads the facing idle
  frame only (HeroController.HeroFrame). Mercenary sheet + captain portrait; six tile
  classes, UI icons (orbs, potion, gold, sword, shield, scroll, waypoint), title card.
  `tools/artgate.sh` OK, 32 files.
- **HUD + controls.** Life/mana orbs, belt row (1–4 hotkeys), XP, gold, area, hint line,
  mercenary line (Hud.cs); RMB attack, R the koldun's first spark (row 78), TAB inventory,
  T town panel, C chest, B cube — all keyboard/mouse doors into the logged command surface.
- **Menus + persistence.** ESC = the GAME MENU (RESUME / SAVE & QUIT) when alive; the death
  menu when dead (authoritative, ESC never opens the game menu over a corpse) (rows 64, 75,
  MC 11957). **Save/load = v7 JSON** (SaveSerializer `SaveVersion = 7`), foreign versions
  refused by name; F5 saves, Continue picks it up (row 84's Continue row gate-passed).
- **635 tests** (`dotnet test BabaYaga.sln`, engine-process-free xunit rule contracts).

## KNOWN LIMITS (each verified against the tree this pass)

- **The follower has NO combat and NO pathing** — she re-paints beside you, that is all
  (FollowerField header; PLAYTEST P14 states it too).
- **Act-gated hires are unproven content with sprites pending:** witez (act 2) and rzutca
  (act 5) load and are gated by their unbuilt acts, and they have NEITHER sheet NOR portrait
  — they ride the palette placeholder / text-only row honestly (FollowerFieldLogic header;
  `data/hirelings.json` acts 1/2/5).
- **Hero walk frames are committed but not animated** — the renderer reads the idle-only
  path (HeroController.cs `HeroFrame`, the only hero-texture loader; no `hero-walk` load
  exists in game/).
- **"Three Rings, One Ward" is reachable ONLY through the RNG gamble.** Pierscien drops from
  nothing (zero references in `data/treasureclasses.json`), the pedlar sells no rings (her
  window: Soluga + the two potions) — the ring rides the gamble Stock (jewelry is admitted,
  uniform draw), so luck, never a guaranteed farm (row 75 MC 11987/11989 reading, proven on
  the stock rule).
- **Balance-audit scope = the cube.** The audit (MC 11985/11986) covered the five cube
  recipes; the wider gold/vendor economy remains its own future row, unfenced otherwise.
- The cube reads the **carried pack** — no cube-grid container yet (later card).
- **Windowed SELL does not exist yet:** the stall paints BUY rows + GAMBLE; selling is a
  CLI/sim door only (`sell last`, row 50 BUILT-M2; zero SELL widgets in TownPanel/Inventory).
- Monsters STAND still and only counter when attacked (M5 rule); audio untested off-device.
- Still open from before: equipment paper-doll panel + weapon sets (68), skill hotkey bar
  (70), character sheet (71), automap (72), town-portal scroll (73), procedural layout
  (row 59, LEDGER status BUILDING), curse/aura stack engine and the damage-effect family
  rows (28–29, 31–35 — the crit family row 30 is BUILT-M1), ¾ corpse-return recovery (64),
  acts 2–5 (parse-tested skeletons only), walk needs a DISCOVERED endpoint (80), regen
  fractions are session-only (79), the mid-special-fight plain-restore read (DA-ROUND-2),
  the `+energy` affix is code-live but data-silent (zero energy rows in `affixes.json`).

## PROMISED, NOT SHIPPED (replace-not-delete: promised in the M5 slice, ships later)

- Follower COMBAT — promised as "she walks beside you"; the sim-side mercenary already
  fights in the CLI sim, but the WINDOWED follower's own combat ships later (P14 named).
- The hero WALK CYCLE animation — the four walk frames shipped, the animation reads idle.
- The CUBE-GRID CONTAINER — the cube panel ships, its own container grid ships later.
- A guaranteed RING SOURCE — promised honestly as luck-only; a farmable source is a future
  row (row 48's "pierscien source = named FUTURE row" stands, now with the gamble route).
- A windowed SELL row at the stall — the sim door shipped in M2; the panel button ships later.
- The OWNER-DEVICE playtest itself: promised as M5's closing row (76), still open — see below.

## PLAYTEST row (the tag gate)

owner playtest: **RELEASE VIA OWNER ORDER 2026-11-04** (verbatim ruling: "make the
release and make sure it contains a .exe file for windows") — the kit (rev4) was NOT played;
the owner chose release; LEDGER row 76 flipped LIVE on that order and PLAYTEST.md remains
available for him any time.

WINDOWS SHIPPED: self-contained BabaYaga.exe (109 MB, no .NET install needed) exported per
owner order; RUNS AND PAINTS the title screen on real Windows (vm120, sha-matched transfer,
screenshot proof out/evidence/vm120/ in the audit run dir); gameplay beyond title on Windows
is UNVERIFIED (input tooling on that box) — Linux-side runtime remains fully anchored.

**Flip instruction (exact, for the owner's green run):** in ONE commit — tag `v0.5.0` on the
green-run tree + flip LEDGER row 76 (OPEN → owner-run green, dated, with the run's evidence
path) + update this line to record the green run (date + device notes) + stamp README §State
"v0.5.0" — per the GitHub-Release doctrine (owner ruling 2026-10-25): the tag and these
notes + README + doc-sync land in the SAME push. NOTE: a fresh clone also needs
`bash assets/import.sh` once before PLAY (the README §Build & run carries the step) — the
runtime `game/assets/` copy is not in git.
