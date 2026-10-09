"""Slice the approved whole-image atlases into stable Unity sprite filenames.

The generated sheets remain in Tools so individual images can be recut without
regenerating them. Existing Unity .meta files are deliberately left untouched.
"""

from pathlib import Path
from PIL import Image


PROJECT = Path(__file__).resolve().parents[1]
TOOLS = Path(__file__).resolve().parent
ART = PROJECT / "Assets" / "_Game" / "Resources" / "Art"

SHEETS = (
    ("art_pickups_v2_atlas.png", 2, 1, "Effects", (
        "pickup_experience", "pickup_material",
    )),
    ("art_fx_v2_atlas.png", 3, 2, "Effects", (
        "pickup_healing_seed", "build_charm_projectile", "build_charm_impact",
        "build_honk_projectile", "build_honk_impact", "build_growth_mark",
    )),
    ("art_items_a_v2_atlas.png", 3, 4, "Items", (
        "item_armor_plate", "item_bandage", "item_battery",
        "item_blood_orb", "item_boots", "item_chaos_engine",
        "item_crit_stone", "item_glass_cannon", "item_heal_pack",
        "item_iron_helmet", "item_knockback_coil", "item_lucky_clover",
    )),
    ("art_items_b_v2_atlas.png", 3, 4, "Items", (
        "item_lucky_ring", "item_medkit", "item_precision_sight",
        "item_red_core", "item_red_fortune", "item_red_surge",
        "item_runner_soles", "item_scope", "item_stim",
        "item_thorn", "item_vampiric", "item_vitality_tonic",
    )),
    ("art_projectiles_v2_atlas.png", 4, 4, "Projectiles", (
        "projectile_pistol", "projectile_smg", "projectile_shotgun", "projectile_sniper",
        "projectile_minigun", "projectile_rocket", "projectile_laser", "projectile_flamethrower",
        "projectile_boomerang", "projectile_drone", "projectile_fist", "projectile_sword",
        "projectile_enemy_spore", "projectile_enemy_elite", "projectile_boss_spike", "projectile_boss_fire",
    )),
)


def slice_sheet(filename: str, columns: int, rows: int, category: str, names: tuple[str, ...]) -> None:
    atlas = Image.open(TOOLS / filename).convert("RGBA")
    assert len(names) == rows * columns
    output = ART / category
    output.mkdir(parents=True, exist_ok=True)

    for index, name in enumerate(names):
        column, row = index % columns, index // columns
        x0 = round(column * atlas.width / columns)
        x1 = round((column + 1) * atlas.width / columns)
        y0 = round(row * atlas.height / rows)
        y1 = round((row + 1) * atlas.height / rows)
        cell = atlas.crop((x0, y0, x1, y1))
        alpha = cell.getchannel("A").point(lambda value: 0 if value < 40 else value)
        bounds = alpha.getbbox()
        if bounds is None:
            raise ValueError(f"Empty sprite: {filename} / {name}")
        cell.putalpha(alpha)
        cell = cell.crop(bounds)
        # One consistent occupancy works with existing square sprite pivots and
        # also prevents generated fringe particles from crossing adjacent cells.
        cell.thumbnail((208, 208), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (256, 256))
        canvas.alpha_composite(cell, ((256 - cell.width) // 2, (256 - cell.height) // 2))
        canvas.save(output / f"{name}.png", optimize=True)
        print(f"{category}/{name}.png")


def main() -> None:
    for filename, columns, rows, category, names in SHEETS:
        slice_sheet(filename, columns, rows, category, names)
    item_sheet = Image.new("RGBA", (1536, 1024))
    item_names = SHEETS[2][4] + SHEETS[3][4]
    for index, name in enumerate(item_names):
        icon = Image.open(ART / "Items" / f"{name}.png").convert("RGBA")
        item_sheet.alpha_composite(icon, ((index % 6) * 256, (index // 6) * 256))
    item_sheet.save(ART / "Items" / "item_icons_atlas.png", optimize=True)
    print("Items/item_icons_atlas.png")
    # BossVfxAtlas slices a 4x2 grid at runtime; use exact integer cell edges.
    boss = Image.open(TOOLS / "art_boss_vfx_v2_atlas.png").convert("RGBA")
    boss = boss.resize((2048, 1024), Image.Resampling.LANCZOS)
    boss_alpha = boss.getchannel("A").point(lambda value: 0 if value < 40 else value)
    boss.putalpha(boss_alpha)
    boss.save(ART / "Effects" / "boss_vfx_atlas.png", optimize=True)
    print("Effects/boss_vfx_atlas.png")
    map_source = Image.open(TOOLS / "art_map_v2_source.png").convert("RGB")
    map_target = ART / "Map" / "map_arena.png"
    map_source.resize((2048, 1152), Image.Resampling.LANCZOS).save(map_target, optimize=True)
    print("Map/map_arena.png")


if __name__ == "__main__":
    main()
