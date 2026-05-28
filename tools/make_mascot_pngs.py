"""
Turn a folder of mascot JPGs into transparent PNGs.

The mascot illustrations Karen receives are flat-color cartoons with a thick
navy outline on a near-white background. We want the background gone (alpha 0)
while the wool, teeth, eye highlights, and other interior white details stay
opaque.

Algorithm (connected-component "magic wand" applied from every edge):
  1. Build a binary mask of background-like pixels: bright (min channel high)
     and low-saturation (channels close together). This captures both the white
     background AND the white wool — but the wool is enclosed by the navy
     outline, so its component never reaches the image edge.
  2. Label the mask with 4-connectivity.
  3. Any component that touches the image border = background -> make transparent.
  4. Components that do not touch any border = legitimate interior whites -> keep.

Why not a corner-seeded flood fill? Some poses have background pockets between
limbs or text shapes that don't connect to a corner. Seeding from every edge
pixel via labeling catches those.

Usage:
  python tools/make_mascot_pngs.py SRC_DIR [--out OUT_DIR]
  # defaults: OUT_DIR = SRC_DIR (PNGs land next to the JPGs)

  python tools/make_mascot_pngs.py src/LakeCountrySpanish.Web/wwwroot/img/mascot

Skips files listed in SKIP — those have baked-in transparency checkerboards
that overlap the wool's shading values and need manual cleanup.

Requires: pillow, numpy, scipy.
"""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

SKIP = {
    # Saved as a JPG from a PNG with transparency -> the gray checkerboard preview
    # got rasterized in. Its alternating ~220/~245 grays sit inside the wool's
    # shading range so we'd erase real content trying to remove the checker.
    "llama-mascot-waving-transparent.jpg",
    # Reference sheets, not site assets.
    "llama-mascot-style-reference.jpg",
    "llama-mascot-black-white.jpg",
    # Real photo of the plush toy — not a vector illustration, would need
    # photo-style background removal (rembg / U-2-Net), not a flood fill.
    "llama-mascot-plush.jpg",
}

# A pixel is "background-like" if it's bright AND low-saturation.
BG_BRIGHT_MIN = 200       # minimum value of the dimmest channel
BG_SATURATION_MAX = 25    # max(channel) - min(channel)


def convert(src: Path, dst: Path) -> tuple[int, int]:
    """Return (edge_components, total_components)."""
    arr = np.array(Image.open(src).convert("RGB"))
    r, g, b = arr[..., 0], arr[..., 1], arr[..., 2]
    mn = np.minimum(np.minimum(r, g), b)
    mx = np.maximum(np.maximum(r, g), b)
    bg_mask = (mn > BG_BRIGHT_MIN) & ((mx - mn) < BG_SATURATION_MAX)

    labeled, n_total = ndimage.label(bg_mask)

    edge_labels: set[int] = set()
    edge_labels.update(np.unique(labeled[0, :]).tolist())
    edge_labels.update(np.unique(labeled[-1, :]).tolist())
    edge_labels.update(np.unique(labeled[:, 0]).tolist())
    edge_labels.update(np.unique(labeled[:, -1]).tolist())
    edge_labels.discard(0)  # 0 = not part of the mask

    if not edge_labels:
        # No edge background detected — leave the file alone.
        return 0, n_total

    transparent = np.isin(labeled, list(edge_labels))
    alpha = np.where(transparent, 0, 255).astype(np.uint8)
    rgba = np.dstack([arr, alpha])
    Image.fromarray(rgba, "RGBA").save(dst, "PNG", optimize=True)
    return len(edge_labels), n_total


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("src_dir", help="Directory containing mascot JPGs")
    p.add_argument("--out", default=None, help="Output directory (defaults to src_dir)")
    args = p.parse_args()

    src_dir = Path(args.src_dir).resolve()
    out_dir = Path(args.out).resolve() if args.out else src_dir
    out_dir.mkdir(parents=True, exist_ok=True)

    if not src_dir.is_dir():
        print(f"src_dir does not exist: {src_dir}", file=sys.stderr)
        return 2

    converted = skipped = 0
    for entry in sorted(os.listdir(src_dir)):
        if not entry.lower().endswith(".jpg"):
            continue
        if entry in SKIP:
            print(f"skip  {entry}")
            skipped += 1
            continue
        dst = out_dir / (Path(entry).stem + ".png")
        n_edge, n_total = convert(src_dir / entry, dst)
        if n_edge == 0:
            print(f"warn  {entry}: no edge-background detected, file unchanged")
        else:
            print(f"wrote {dst.name}  (edge-bg components: {n_edge}, total: {n_total})")
            converted += 1
    print(f"---\n{converted} converted, {skipped} skipped")
    return 0


if __name__ == "__main__":
    sys.exit(main())
