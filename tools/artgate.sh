#!/usr/bin/env bash
# tools/artgate.sh — content-hash art gate (MC 11935, M5-S2; F1 amendment).
#
# REPLACES the stale plan-c2 §2/S2 gate `diff -r assets game/assets -x '*.import'`.
# That rule is measurably impossible (DA finding F1): assets/raw/ is FLAT while
# assets/import.sh SORTS files into game/assets/{tiles,hero,monsters,ui}/ — a
# recursive diff of two different layouts can never print empty. This gate is the
# content-hash equality the DA ruled instead.
#
# Honest semantics (F1): .gitignore:6 (`game/assets/`) ignores the engine copy
# ENTIRELY — `git ls-files game/assets` lists zero files — so there is NO
# "committed game/assets" to compare against. The committed truth is assets/raw/.
# This gate therefore compares CONTENT HASHES of the current game/assets/ against
# a fresh regeneration: assets/raw/ is copied into a throwaway sandbox, assets/import.sh
# runs there (the classifier stays the SINGLE source of the layout mapping — this
# script duplicates none of it), and sha256 manifests of produced-vs-live are
# compared path-relative. Excluded from both manifests: *.import sidecars (engine-
# generated) and README.md (documentation that rides the copy).
#
# exit 0 = content-hash equal. exit 1 = drift, one line per file:
#   MISSING <rel>  produced by import.sh but absent from game/assets/
#   DIFFERS <rel>  present on both sides, different bytes
#   STALE   <rel>  present in game/assets/ but NOT produced (hand-edit/debris)
# exit 2 = assets/raw/ absent (nothing to gate).
#
# Idempotent and read-only over the repo (sandbox writes only, under $TMPDIR).
# The mechanical fix for drift is: assets/import.sh   (never hand-edit game/).
# In a fresh clone game/assets/ does not exist (git-ignored), so the gate is RED
# until assets/import.sh has run — that is the intended reproducibility property.
set -euo pipefail
cd "$(dirname "$0")/.."

RAW=assets/raw
DEST=game/assets
test -d "$RAW" || { echo "artgate: missing $RAW — nothing to gate" >&2; exit 2; }

# manifest <root> — "sha relpath" lines sorted by relpath; empty if root has no DEST.
# Raw asset names carry no spaces (measured 2026-11-02: 26 png + README.md, flat).
manifest() {
  local root="$1"
  test -d "$root/$DEST" || return 0
  find "$root/$DEST" -type f ! -name '*.import' ! -name 'README.md' -exec sha256sum {} + \
    | sed -e "s|$root/$DEST/||" | sort -k2
}

TMP=$(mktemp -d "${TMPDIR:-/tmp}/artgate.XXXXXX")
trap 'rm -rf "$TMP"' EXIT
mkdir -p "$TMP/assets"
cp -a "$RAW" "$TMP/assets/raw"
cp -a "$(dirname "$RAW")/import.sh" "$TMP/assets/import.sh"
# MC 11975: import.sh may call the committed slicer (poi-*portrait --resize
# 128x128, M4 art-debt D5) — the sandbox carries tools/slice_sheet.py too, so
# the classifier + slicer stay the SINGLE source of the layout mapping; this
# script still duplicates neither.
mkdir -p "$TMP/tools"
cp -a tools/slice_sheet.py "$TMP/tools/"
bash "$TMP/assets/import.sh" >/dev/null

manifest "$TMP" > "$TMP/produced"
manifest .       > "$TMP/live"

# Three joins, each deterministic: STALE, DIFFERS, MISSING; then sorted by path.
{
  awk 'NR==FNR {p[$2]=1; next} !($2 in p) {print "STALE   " $2}' \
      "$TMP/produced" "$TMP/live"
  awk 'NR==FNR {h[$2]=$1; next} ($2 in h) && h[$2]!=$1 {print "DIFFERS " $2}' \
      "$TMP/produced" "$TMP/live"
  awk 'NR==FNR {h[$2]=1; next} {seen[$2]=1} END {for (k in h) if (!(k in seen)) print "MISSING " k}' \
      "$TMP/produced" "$TMP/live"
} | sort -k2 > "$TMP/drift"

n=$(wc -l < "$TMP/produced")
if [ ! -s "$TMP/drift" ]; then
  echo "artgate: OK — $DEST content-hash equal to a fresh assets/import.sh run ($n files compared; .import sidecars + README.md excluded)"
  exit 0
fi
cat "$TMP/drift"
echo "artgate: DRIFT — $(wc -l < "$TMP/drift") file(s); fix mechanically with: assets/import.sh (game/ copies are never hand-edited)"
exit 1
