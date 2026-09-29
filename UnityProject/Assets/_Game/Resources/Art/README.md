# Art 素材装填对照表（v2 + v3 + v4）

素材来源：多张素材总览图（透明底 PNG，直接按元素连通域切割，无背景残留）。
装填方式：把 PNG 拖到对应预制体的 SpriteRenderer / Image 的 **Sprite** 字段。

> 导入提示：Unity 中全选 PNG → `Texture Type = Sprite (2D and UI)`，像素风建议 `Filter Mode = Point`。

## v4 正式版（带标签图，当前主力：敌人/武器已按配置 id 命名）

| 目录 | 内容 | 数量 | 对应预制体 / key |
|---|---|---|---|
| `Enemies/enemy_{id}.png` | **8 个正式敌人**，与 enemies.json 一一对应 | 8 | `EnemyPool/enemy_{id}`（chaser/ranger/exploder/tank/splitter/elite_chaser/boss1/boss2） |
| `Weapons/weapon_{id}.png` | **11 个正式武器**，与 weapons.json 一一对应 | 11 | `Shared/weapon_visual`（pistol/smg/shotgun/sniper/laser/rocket/minigun/flamethrower + sword/fist/boomerang） |
| `Effects/effect_weapon_hit_XX` | 武器命中特效（8~9 张，顺序对武器） | ~9 | 子弹命中处特效 |
| `Effects/effect_weapon_shot_XX` | 武器弹道/枪口特效（5~9 张，多切碎片可删） | ~9 | 子弹/枪口特效 |
| `Effects/effect_enemy_{id}` | 敌人攻击特效（ranger 红点弹 / boss1 投掷冲击 / boss2 魔法） | 3 | 敌人弹幕特效 |

v4 未切：主角（用户要求无视）、地图物件（用户要求无视）。

## 历史版本（v2/v3，备用）

| 目录 | 内容 | 数量 |
|---|---|---|
| `Characters/character_01~39` | v3 角色（三大组 × 大本体/小表情/死亡） | 39 |
| `Enemies/enemy_01~15` + `enemy_16~33` | v2 常规怪 + v3 新怪（序号命名，v4 已按 id 覆盖主力） | 33 |
| `Weapons/weapon_01~17` | v2 枪械序号版（v4 已按 id 覆盖主力） | 17 |
| `Effects/effect_01~28` | v2 通用特效（爆炸/血迹/烟雾等） | 28 |
| `MapObjects/object_01~14` | 地图物件（岩石/枯树/草丛/木箱/油桶等） | 14 |
| `Map/`（若有） | 地图背景 | - |

## 说明

- **v4 为当前主力**：敌人/武器文件名即配置 id，拖进对应预制体即可用；旧序号版保留备用不冲突。
- 切割脚本：`_cut_preview\v2\cut_final.py`、`_cut_preview\v3\cut_v3.py`、`_cut_preview\v4\cut_v4.py`（可重跑）。
