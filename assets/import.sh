#!/usr/bin/env bash
# One-way asset import: assets/raw/ (git truth) -> game/assets/ (engine copy).
# Idempotent, EXCLUDES nothing — every raw render is copied — sorted into the
# asset classes the scripts read (M4-placement §1: tiles·hero·monsters·ui·title).
# Copies are always .import-free: stale *.import sidecars in the destination are
# removed so `--import` regenerates them fresh. A bad render is fixed at
# assets/raw/ (the source), never in game/ (M4-placement §3 hygiene).
set -euo pipefail
cd "$(dirname "$0")/.."

SRC=assets/raw
DEST=game/assets
test -d "$SRC" || { echo "missing $SRC — nothing to import" >&2; exit 1; }

mkdir -p "$DEST"/{tiles,hero,monsters,ui}
shopt -s nullglob
for f in "$SRC"/*; do
  resize=""
  case "$(basename "$f")" in
    tile-*.png)        dest="$DEST/tiles" ;;
    hero-*.png)        dest="$DEST/hero" ;;
    mon-*.png)         dest="$DEST/monsters" ;;
    ui-*.png)          dest="$DEST/ui" ;;
    # M5.5 ART DEBT D5 (MC 11975): a follower sheet is the same two-frame
    # 320x160 character contract as the hero sheets — cleanest existing class,
    # NO new one. A poi-*portrait raw is FULL-SIZE truth; its game/assets/ui/
    # copy is the 128x128 LANCZOS twin, produced by the committed slicer's
    # --resize mode (tools/slice_sheet.py) — never a hand-resized copy.
    fol-*.png)         dest="$DEST/hero" ;;
    poi-*portrait*.png) dest="$DEST/ui"; resize=128x128 ;;
    title-card.png)    dest="$DEST" ;;
    *)                 dest="$DEST" ;;   # EXCLUDES nothing: unknown classes ride at the root
  esac
  if [ -n "$resize" ]; then
    python3 tools/slice_sheet.py --resize "$resize" "$f" "$dest/$(basename "$f")"
  else
    cp -f "$f" "$dest"/
  fi
done
find "$DEST" -name '*.import' -delete
echo "imported: $(find "$DEST" -type f | wc -l) files into $DEST (re-run godot --import to regenerate sidecars)"
