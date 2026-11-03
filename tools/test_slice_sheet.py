#!/usr/bin/env python3
"""tools/test_slice_sheet.py — self-test for tools/slice_sheet.py (MC 11958).

The tools/ idiom (state reading, per the brief's choice): a python self-test
with EXIT CODES lives in tools/ beside the slicer — xunit belongs to the Sim
sln (src/); tools/ gates are manual CLI runs like artgate.sh, so this file is
run by:  python3 tools/test_slice_sheet.py   (exit 0 all green, 1 any failure)

RED-first design (the gate proves it can fail):
  A) exact output dims — a 320x160 contract violation goes RED;
  B) half-pixel identity — a synthetic sheet whose LEFT half is solid RED and
     RIGHT half solid BLUE; the LEFT output half must carry the LEFT input
     color. A slicer that SWAPPED the halves fails here (planted-red: this is
     the check that makes a halves-swap go RED, demonstrated 2026-11-03 by
     running this test against a deliberately swapped slicer copy — RED, then
     against the true slicer — GREEN).
  C) determinism — the same input sliced twice yields byte-identical output.
"""
import os
import subprocess
import sys
import tempfile

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SLICER = os.path.join(HERE, "slice_sheet.py")

FAILURES = []


def check(cond, msg):
    print(("PASS " if cond else "FAIL ") + msg)
    if not cond:
        FAILURES.append(msg)


def run_slicer(src, dst):
    r = subprocess.run([sys.executable, SLICER, src, dst],
                       capture_output=True, text=True)
    return r


def main():
    tmp = tempfile.mkdtemp(prefix="slice-selftest.")
    sheet = os.path.join(tmp, "in.png")
    # Non-square halves (200x160) on purpose: the center-crop must square them
    # to 160x160 BEFORE the resize — solid colors survive that crop exactly.
    im = Image.new("RGB", (400, 160))
    im.paste(Image.new("RGB", (200, 160), (255, 0, 0)), (0, 0))     # left  = red
    im.paste(Image.new("RGB", (200, 160), (0, 0, 255)), (200, 0))   # right = blue
    im.save(sheet)

    out1 = os.path.join(tmp, "out1.png")
    r = run_slicer(sheet, out1)
    check(r.returncode == 0, f"slicer exit 0 on a good sheet (got {r.returncode}: {r.stderr.strip()})")
    if r.returncode != 0:
        print("RESULT: FAILURES " + str(FAILURES))
        return 1

    got = Image.open(out1)
    # A) exact dims, read the same way a checker reads a PNG (decoded size).
    check(got.size == (320, 160), f"output dims == (320,160) (got {got.size})")

    # B) half-pixel identity: left output half carries LEFT input color.
    px = got.convert("RGB").load()
    left_samples = [px[40, 80], px[120, 40], px[159, 159]]
    right_samples = [px[200, 80], px[319, 40], px[240, 159]]
    check(all(p == (255, 0, 0) for p in left_samples),
          f"left output half carries LEFT input color (got {left_samples})")
    check(all(p == (0, 0, 255) for p in right_samples),
          f"right output half carries RIGHT input color (got {right_samples})")

    # C) determinism: same input twice, byte-identical output.
    out2 = os.path.join(tmp, "out2.png")
    r2 = run_slicer(sheet, out2)
    same = r2.returncode == 0 and open(out1, "rb").read() == open(out2, "rb").read()
    check(same, "same input sliced twice -> byte-identical output (determinism)")

    # D) usage gate: missing args exit non-zero (never a silent half-slice).
    r3 = subprocess.run([sys.executable, SLICER], capture_output=True, text=True)
    check(r3.returncode != 0, f"no-args run exits non-zero (got {r3.returncode})")

    print("RESULT: " + ("FAILURES=" + str(len(FAILURES)) if FAILURES else "ALL GREEN"))
    return 1 if FAILURES else 0


if __name__ == "__main__":
    sys.exit(main())
