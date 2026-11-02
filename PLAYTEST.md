# Baba Yaga — Owner Playtest Checklist (M5, kit 2026-11-02)

The venue for YOUR run of the windowed game. Twelve rows: eleven play rows (P1–P11)
plus Continue. Each row: what you do, what you should SEE, a tick box. The automated
gate already walked all twelve rows on this build and every one printed/skipped green
(run `.audits/202610301950-31593a83/out/TEST-verdict-m5-b.md`, VERDICT PASS, JUDGED
f01bcde2) — your job is the FELT check on your own device, not first discovery.

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
| 1 2 3 4 | drink the belt potion in that slot |
| R | throw the koldun's first spark at your engaged target |
| ESC | when DEAD: the death menu with RESPAWN. Alive: only a hint (M5 ships no pause) |
| F5 | save — Continue on the main menu picks it up |

## The twelve rows

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
- [ ] **P8 Hire.** Press T in town, hire the strzelczyni from the captain. SEE: Gold
      drops by her price (830→730 from a 1000 start), a merc line appears on the HUD,
      she follows you when you travel.
- [ ] **P9 Ride stones.** Enter mchy (the stones light on arrival). LEFT-click the lit
      stone marker. SEE: you are instantly in the other area — no walk steps — HUD Area flips.
- [ ] **P10 Three lines.** Anywhere: SEE Life, Mana AND XP all visible at once in the HUD.
- [ ] **P11 Die and rise.** Let the monsters kill you. SEE: "YOU DIED — the stones
      remember you" with RESPAWN / QUIT buttons (press ESC if it isn't open). Click
      RESPAWN: SEE you alive again at grodzisko, HUD restored, game plays on. No hang.
- [ ] **Continue.** Press F5 (hint: "the stow-hand copies your kit to the save-stone"),
      quit, restart, click Continue. SEE: you load where you saved, and a LEFT-click
      move still works right after loading.

## Honest limits of this build (not bugs to report back)

- Monsters STAND still and only counter when you attack (M5 rule); audio is off-device
  untested (dummy driver under CI).
- A one-time boot message `Parent node is busy…` prints at startup; harmless — the menu
  paints normally and the judged gate ran with it present.
- ESC while alive prints a hint only; the full game menu is LEDGER row 75's later half.

Anything else that misbehaves on your device: note the row letter + what you saw, and
send it — the fleet will gate it like the rest.
