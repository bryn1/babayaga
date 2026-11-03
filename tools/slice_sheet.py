#!/usr/bin/env python3
"""tools/slice_sheet.py — deterministic two-frame creature-sheet slicer (MC 11958).

WHAT IT REPLACES (name, per replace-do-not-add): the ad-hoc, NEVER-COMMITTED
slicing that produced the 11 earlier assets/raw/ sheets (M4 art pass — the
26-render wave whose edits were hand-run one-liners; the pipeline note in
assets/raw/README.md names "edit/re-render here -> assets/import.sh", but the
sheet->frame step itself had no committed tool). This file is that step, now
in git truth: any raw monster sheet is reproducible from its library original.

CONTRACT: a wide two-frame sheet in -> a 320x160 two-frame sheet out (two
160x160 frames side by side — the exact shape MonsterField.MonsterTexture
reads via AtlasTexture, and the shape of every mon-*-idle.png in assets/raw/).
Each input HALF (left/right of the mid-width line) is CENTER-CROPPED to a
square FIRST, then resized to 160x160. The one resampler: Image.LANCZOS —
stated here because the output is byte-deterministic only while it is pinned
(Pillow's LANCZOS is a fixed-window resampling filter — no random seed
exists anywhere in the path). RGBA output; PNG save carries no timestamps, so
the same input file always yields the same output bytes (self-test C pins it).

CLI:  python3 tools/slice_sheet.py IN OUT.png
      python3 tools/slice_sheet.py --resize WxH IN OUT.png   (MC 11975)
The --resize mode is the SINGLE-IMAGE twin of the sheet contract for the
portrait path (poi-*-portrait raw truth is full-size; only the game/assets/
copy is resized): center-crop to the largest centered square, then ONE
pinned LANCZOS resize to WxH — same one-resampler law as the sheet path,
same byte-determinism (no timestamps in the PNG). W and H are positive
integer pixels; anything else is a usage error, never a silent fallback.
exit 2 = bad usage/input (the square crop is inside each half by
construction, so any sheet size is fine; a non-image or unwritable dest
raises loudly, never a silent half-slice).

Self-test: tools/test_slice_sheet.py (exit codes; RED-first demonstrated on
the halves-swapped plant, 2026-11-03; row E pins the --resize mode —
planted-wrong-size RED, 2026-11-03 MC 11975).
"""
import os
import sys

from PIL import Image

FRAME = 160  # the pinned frame edge (160x160/frame — assets/raw/README.md layout contract)


def slice_sheet(src, dst):
    """Center-crop each half to square, resize each to FRAME^2 (LANCZOS), paste
    side by side into a 320x160 sheet, save as PNG."""
    im = Image.open(src).convert("RGBA")
    w, h = im.size
    half = w // 2
    frames = []
    for i in (0, 1):
        f = im.crop((i * half, 0, (i + 1) * half, h))
        s = min(f.size)  # center-crop to the largest centered square
        left = (f.width - s) // 2
        top = (f.height - s) // 2
        f = f.crop((left, top, left + s, top + s))
        frames.append(f.resize((FRAME, FRAME), Image.LANCZOS))
    out = Image.new("RGBA", (FRAME * 2, FRAME))
    out.paste(frames[0], (0, 0))
    out.paste(frames[1], (FRAME, 0))
    out.save(dst)


def resize_square(src, dst, w, h):
    """Center-crop the whole image to a square, ONE LANCZOS resize to (w,h),
    save PNG — the --resize mode (MC 11975). Same pinned-resampler law as
    slice_sheet; full-size raw in, exact WxH copy out."""
    im = Image.open(src).convert("RGBA")
    s = min(im.size)  # center-crop to the largest centered square
    left = (im.width - s) // 2
    top = (im.height - s) // 2
    im = im.crop((left, top, left + s, top + s))
    im.resize((w, h), Image.LANCZOS).save(dst)


def main(argv):
    if len(argv) == 5 and argv[1] == "--resize":
        src, dst = argv[3], argv[4]
        if not os.path.isfile(src):
            sys.stderr.write(f"slice_sheet: no such input: {src}\n")
            return 2
        try:
            w, h = (int(v) for v in argv[2].split("x"))
            assert w > 0 and h > 0
        except (ValueError, AssertionError):
            sys.stderr.write(f"slice_sheet: bad --resize size: {argv[2]} (want WxH, positive ints)\n")
            return 2
        resize_square(src, dst, w, h)
        return 0
    if len(argv) != 3 or argv[1] == "--resize":
        sys.stderr.write("usage: slice_sheet.py [--resize WxH] IN OUT.png\n")
        return 2
    src, dst = argv[1], argv[2]
    if not os.path.isfile(src):
        sys.stderr.write(f"slice_sheet: no such input: {src}\n")
        return 2
    slice_sheet(src, dst)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
