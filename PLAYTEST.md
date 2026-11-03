# Baba Yaga — Owner Playtest Checklist (M5, kit 2026-11-03)

kit 2026-11-03 (rev2): + cube, + follower, art fully alpha

The venue for YOUR run of the windowed game. Sixteen rows: fifteen play rows
(P1–P15) plus Continue. Each row: what you do, what you should SEE, a tick box.
The automated gate walked P1–P12 + Continue green on the 2026-11-02 tree
(`.audits/202610301950-31593a83/out/TEST-verdict-m5-b.md`, VERDICT PASS, JUDGED
f01bcde2). That verdict PREDATES the cube, the follower and the alpha-art cure —
no TEST-verdict file covers P13–P15: those three rows are verified by unit tests
(`tests/CraftPanelTests.cs`, `tests/RitualCraftContentTests.cs`,
`tests/FollowerFieldTests.cs` — all green in `dotnet test BabaYaga.sln` on this
tree) plus each card's own windowed Xvfb legs and the orchestrator's own-eyes
runs (evidence dirs `.audits/202610301950-31593a83/out/leg-m5-cube/`,
`out/leg-m5-fol/`, `out/leg-m5-d7/`; write-ups `out/EVIDENCE-m5-cube.md`,
`out/EVIDENCE-m5-fol.md`). Your job is the FELT check on your own device, not
first discovery.

## Run it

```bash
# one-time, from the repo root (needs dotnet SDK 8):
dotnet build game/Game.csproj && dotnet build game/Game.csproj   # build twice, both "0 Error(s)"
GODOT_BIN=/home/claudecode/Games/.tools/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
HOME=/tmp "$GODOT_BIN" --headless --path game --import           # one-time asset import

# the game (a window opens, NOT headless):
"$GODOT_BIN" --path game
```

Your own device: put any **Godot 4.7.2-stable .NET/mono** build from
godotengine.org in `GODOT_BIN` (official pin sha256 `129f82db…bff247`, see README).
On a desktop session you don't need `HOME=/tmp`. If a click seems missing, first
re-run the two build commands — the build-twice law.

## Controls (all real, wired in `game/project.godot` [input])

| key / button | what it does |
|---|---|
| LEFT mouse | walk there / click a LIT stone to ride it |
| RIGHT mouse | attack (stationary monsters, they counter back) |
| TAB | inventory panel open / close |
| T | town panel (pedlar + captain) — only inside grodzisko |
| C | town chest (stash): store from the pack, take back — only inside grodzisko |
| B | the bone cube (ritual craft): paints the FIVE inscriptions the cube knows (P13), CRAFT burns its inputs from your pack — only inside grodzisko |
| 1 2 3 4 | drink the belt potion in that slot |
| R | throw the koldun's first spark at your engaged target |
| ESC | alive: the GAME MENU (RESUME / SAVE & QUIT) — opens and closes with the key. DEAD: the death menu (authoritative — ESC never opens the game menu over a corpse) |
| F5 | save — Continue on the main menu picks it up |

## The sixteen rows

- [ ] **P1 It opens.** Start the game. SEE: title card "BABA YAGA" + "A DARK SLAVIC
      SAGA", three whole buttons — New Game / Continue / Quit — centred, nothing clipped.
- [ ] **P2 Into town.** Click New Game. SEE: isometric town tiles, your hero standing
      in grodzisko, and the HUD lines Life 50/50 · Mana 30/30 · XP 0/500 · Gold 1000.
- [ ] **P3 Walk.** LEFT-click far walkable ground. SEE: the hero walks there step by step.
- [ ] **P4 Out of town.** Walk onto the edge of the map. SEE: a hint reading exactly
      "the stone carries you to mchy", then the mchy area with pokrzywnik sprites standing
      around, HUD Area line reads `mchy`.
- [ ] **P5 Kill.** RIGHT-click a monster until it dies. SEE: its sprite DISAPPEARS and
      the XP line jumps (a pokrzywnik pays 40 XP). Careful — every survivor hits back.
- [ ] **P6 Loot.** A kill auto-collects the gold + any drop. SEE: Gold line rises; press
      TAB — the panel shows the picked-up item as a named row (e.g. "Oczep (Normal) 2×2").
- [ ] **P7 Heal.** Inside grodzisko press T, buy the life potion (it lands in belt slot 1),
      take a hit or two outside, come back, press 1. SEE: Life rises, belt cell empties;
      at FULL life pressing 1 just prints a hint — no crash.
- [ ] **P8 Hire.** Press T in town, hire the strzelczyni from the captain — her price is
      100 gold. SEE: Gold drops by her price (830→730 on a 1000 start after the P7 potion),
      a Merc line appears on the HUD, she follows you when you travel — and since this kit
      rev she also walks BESIDE you on screen (see P14).
- [ ] **P9 Ride stones.** Enter mchy (the stones light on arrival). LEFT-click the lit
      stone marker. SEE: you are instantly in the other area — no walk steps — HUD Area flips.
- [ ] **P10 Three lines.** Anywhere: SEE Life, Mana AND XP all visible at once in the HUD.
- [ ] **P11 Die and rise.** Let the monsters kill you. SEE: "YOU DIED — the stones
      remember you" with RESPAWN / QUIT buttons (press ESC if it isn't open). Click
      RESPAWN: SEE you alive again at grodzisko, HUD restored, game plays on. No hang.
- [ ] **P12 The game menu.** ALIVE, press ESC. SEE: a dimmed screen with "GAME MENU" and
      EXACTLY two buttons — RESUME / SAVE & QUIT. Press ESC again: it closes. Open it over
      an open town panel (T, then ESC): the menu paints OVER the pedlar rows; click
      RESUME: the town panel is STILL OPEN under where the menu was. Now ESC again and
      click SAVE & QUIT: SEE the title card (BABA YAGA) again — and your save is current:
      Continue picks up exactly where you stood. (While DEAD, ESC opens ONLY the death
      menu of P11 — the game menu never appears over a corpse.)
- [ ] **P13 The bone cube.** Inside grodzisko press B. SEE: a "The Bone Cube" panel with
      the FIVE inscriptions it knows: **Resin into Stone** (3 × Raw Amber), **Stone upon
      Stone** (3 × Swamp Garnet), **Amber Heart** (Raw Amber + Swamp Garnet + Fen Peridot),
      **Three Rings, One Ward** (3 × Pierscien), **The Resin-Coated Blade** (2 × Palica +
      Raw Amber). Press CRAFT on a row your pack can pay: the inputs burn and the hint
      reads "the cube cools — <name> lands in your pack" (TAB — the output is in your
      pack; the magic rows land magic items). Press CRAFT with reagents MISSING: the hint
      refuses honestly — e.g. "recipe 'smola_w_kamien' found no match — missing 2 × jantar"
      — and NOTHING burns; a FULL pack is refused BEFORE anything is consumed ("the pack
      has no cell left"). Outside grodzisko the B key only hints "the bone cube stands in
      grodzisko". What you CAN feel today, honestly: Palica drops from mchy kills; the
      three gems DO drop from kills in kopcowisko (walk mchy → widlan → kopcowisko along
      the map edges — ponuraki and kretowniki roam there) — RNG rolls, never a guaranteed
      farm. Pierscien drops from NOTHING today and the pedlar sells no rings (her window:
      Soluga + the two potions), so "Three Rings, One Ward" stays honestly refused until
      a ring source ships — and since the town panel gained its GAMBLE row, the face-down
      offer CAN draw a Pierscien (the ring rides the gamble Stock — RNG rolls, still never
      a guaranteed farm). The cube reads your CARRIED PACK — no cube-grid container yet.
- [ ] **P14 The follower.** Hire the strzelczyni (P8), then leave town. SEE: she STANDS
      AND MOVES BESIDE YOU — her sprite holds one free tile next to the hero and steps
      along as you walk, in every area. Honest by design: she is VISUAL-ONLY so far — she
      fights nothing and nothing fights her (her combat is a later card). Press T at the
      captain: her row shows her PORTRAIT plus two real buttons — REVIVE Strzelczyni
      (bow-kin): brings a dead follower back for a gold cost the engine sets (a dead one
      stands no sprite until you do), and DISMISS: sends her away for good — sprite gone,
      HUD Merc line gone. Never hired / dismissed: the captain row is just the HIRE button.
- [ ] **P15 Art: no more boxes.** Walk out of town and look hard at the sprites. SEE:
      EVERYTHING blends into the ground — hero, follower and every monster draw with ZERO
      background box. The grey-boxed sprites (the D7 defect) are CURED: the four worst
      sheets (kretownik, kikimora, strzyga, widlast) were re-rendered true-alpha on
      2026-11-03 and every shipped sheet was pixel-checked. The HERO has FOUR-DIRECTION
      art: walk each way and SEE him TURN to face E / W / S / N as he travels (his
      4-direction walk frames are committed too; the walk cycle itself is later polish —
      today he walks on the facing idle frame).
- [ ] **Continue.** Press F5 (hint: "the stow-hand copies your kit to the save-stone"),
      quit, restart, click Continue. SEE: you load where you saved, and a LEFT-click
      move still works right after loading.

## Honest limits of this build (not bugs to report back)

- Monsters STAND still and only counter when you attack (M5 rule); audio is off-device
  untested (dummy driver under CI).
- The hired follower is VISUAL-ONLY (P14): no follower combat, no follower pathing —
  she re-paints on the free tile beside you and never lags or overlaps the hero.
- The cube reads your CARRIED PACK for inputs (the cube-grid container is a later
  card), and "Three Rings, One Ward" is uncraftable until some source ships Pierscien.
- The hero's walk cycle still paints the facing idle frame (idle + 4-direction walk
  sheets are committed; the animation is later polish).

Anything else that misbehaves on your device: note the row letter + what you saw, and
send it — the fleet will gate it like the rest.
