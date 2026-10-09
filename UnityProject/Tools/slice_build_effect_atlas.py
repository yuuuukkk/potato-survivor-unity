"""Slice a single 2x2 transparent build-effect atlas into Unity-ready sprites."""

from pathlib import Path
import sys

from PIL import Image


NAMES = (
    "build_charm_projectile",
    "build_honk_projectile",
    "build_charm_impact",
    "build_honk_impact",
)


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: slice_build_effect_atlas.py SOURCE_ATLAS OUTPUT_DIR")
    source = Path(sys.argv[1])
    output = Path(sys.argv[2])
    atlas = Image.open(source).convert("RGBA")
    if atlas.width % 2 or atlas.height % 2:
        raise ValueError(f"atlas must fit a 2x2 grid: {atlas.size}")
    width, height = atlas.width // 2, atlas.height // 2
    output.mkdir(parents=True, exist_ok=True)
    for index, name in enumerate(NAMES):
        column, row = index % 2, index // 2
        cell = atlas.crop((column * width, row * height,
                           (column + 1) * width, (row + 1) * height))
        # Low-alpha glow is removed for crisp small-scale rendering and clean cells.
        alpha = cell.getchannel("A").point(lambda value: 0 if value < 32 else value)
        cell.putalpha(alpha)
        if alpha.getbbox() is None:
            raise ValueError(f"empty sprite: {name}")
        cell.thumbnail((256, 256), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (256, 256))
        canvas.alpha_composite(cell, ((256 - cell.width) // 2, (256 - cell.height) // 2))
        canvas.save(output / f"{name}.png", optimize=True)
    print(f"Saved {len(NAMES)} sprites to {output}")


if __name__ == "__main__":
    main()
