"""Slice the generated 6x4 RGBA shop atlas into named Unity item sprites."""

from pathlib import Path
import sys

from PIL import Image


ITEM_IDS = (
    "heal_pack", "knockback_coil", "glass_cannon", "lucky_clover", "iron_helmet", "stim",
    "thorn", "boots", "bandage", "medkit", "scope", "crit_stone",
    "vampiric", "battery", "armor_plate", "lucky_ring", "blood_orb", "chaos_engine",
    "red_core", "red_surge", "red_fortune", "precision_sight", "runner_soles", "vitality_tonic",
)


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: slice_item_atlas.py SOURCE_ATLAS OUTPUT_DIR")
    source = Path(sys.argv[1])
    output_dir = Path(sys.argv[2])
    with Image.open(source) as image:
        atlas = image.convert("RGBA")
    if atlas.width % 6 or atlas.height % 4:
        raise ValueError(f"atlas dimensions must fit a 6x4 grid: {atlas.size}")
    cell_width, cell_height = atlas.width // 6, atlas.height // 4
    if cell_width != cell_height:
        raise ValueError(f"cells must be square: {cell_width}x{cell_height}")
    output_dir.mkdir(parents=True, exist_ok=True)
    for index, item_id in enumerate(ITEM_IDS):
        column, row = index % 6, index // 6
        box = (column * cell_width, row * cell_height,
               (column + 1) * cell_width, (row + 1) * cell_height)
        cell = atlas.crop(box)
        if cell.getchannel("A").getextrema()[0] > 10:
            raise ValueError(f"{item_id} has no transparent gutter")
        cell.save(output_dir / f"item_{item_id}.png", optimize=True)
    print(f"Saved {len(ITEM_IDS)} item sprites to {output_dir}")


if __name__ == "__main__":
    main()
