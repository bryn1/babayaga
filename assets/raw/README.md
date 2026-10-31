# assets/raw — art truth for the Baba Yaga presentation layer

Source: fleet 3090 imagegen ORIGINALS (run .audits/202610301950-31593a83 art
pass; spec verbatim in out/M4-placement.md §3 — 26 renders: 6 tiles · 8 hero ·
3 monsters · 8 UI · 1 title). Palette: **"Bobrowisko Dusk"** (22 named tokens,
dark gothic Slavic). Every render passed the R2 clean-room wall — original work,
no game-brand references.

Layout contract (M4-placement §3): tile art 256×128 (2:1 iso diamond) drawn at
scale 0.5 into 128×64 cells; hero frames 128×128 and monster frames 160×160/frame
drawn at scale 1.0, feet at cell centre.

Dated deviations:

* **Ponurak (mon-ponurak-idle.png) — quadruped-silhouette deviation.** The §3
  prompt reads "hunched fur-and-shadow predator" (bipedal suggestion); the 3090
  render landed as a low quadruped shadow-crouched silhouette. Accepted as-is
  for the skeleton wave (the silhouette reads as the Gloom Lurker); a re-render
  decision rides the art-review pass, fixed HERE at the source, never in game/.

Pipeline: edit/re-render here → `assets/import.sh` → `"$GODOT" --headless --path
game --import`. game/assets/ is gitignored — never hand-edit anything under game/.
