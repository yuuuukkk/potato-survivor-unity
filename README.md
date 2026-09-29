# 《土豆幸存者》—— Unity 肉鸽生存游戏

一款参照《土豆兄弟 Brotato》的 2D 俯视角 Roguelite 生存游戏。自动攻击、手动走位、波间商店构筑，20 波生存挑战。

## 快速开始（3 步）

1. 用 Unity Hub 打开 `UnityProject` 目录（推荐 **Unity 2022.3 LTS**，2021.3+ 亦可，首次打开请让它升级/解析包）。
2. 打开任意场景（新建空场景即可），直接按 **Play** —— 游戏由 `GameBootstrap` 运行时自动搭建，无需任何手工配置或美术资源。
3. WASD/方向键移动，武器自动索敌开火；击杀敌人攒经验升级（三选一强化）；波间在商店购买武器与道具，撑过 20 波。

> 首次打开若报 `ProjectVersion.txt` 版本不匹配，直接用你本机 Unity 版本打开即可（提示升级/降级，点确认）。若 `Packages/manifest.json` 缺包，Unity 会自动解析补齐。

## 文档导航

| 文件 | 内容 |
|---|---|
| `docs/01-游戏设计文档.md` | 完整 GDD：定位、走读、循环、机制、数值、内容、P0 范围 |
| `docs/02-Unity架构设计.md` | 架构：模块划分、类职责、状态机、数据流、设计决策 |
| `docs/03-实现清单与扩展路线.md` | 已实现内容、运行方式、配置字段说明、P1/P2 路线图 |
| `可视化/架构总览.html` | 浏览器打开的架构与循环可视化（ECharts） |

## 目录结构

```
rougelike/
├─ README.md
├─ docs/                     # 设计文档
├─ 可视化/                    # 架构可视化 HTML
└─ UnityProject/
   ├─ Packages/manifest.json
   ├─ ProjectSettings/
   └─ Assets/_Game/
      ├─ Scripts/            # 全部 C# 源码（Core/Data/Player/Combat/Enemies/Items/UI）
      └─ Resources/Data/     # JSON 内容配置（武器/敌人/道具/角色/波次）
```

## 当前版本

- v0.1 可玩原型：完整 20 波循环、8 种敌人、12 种武器、17 件道具（含急救包消耗品）、3 个角色、6 卡可刷新商店、经验升级三选一、底部物品栏 + 悬停描述、属性面板（I 键）、**玩家武器挂件（土豆兄弟式，不同武器位置不同）**、三大对象池（WeaponPool/EnemyPool/ItemPool）、数据驱动。
- 所有美术默认是运行时生成的占位色块；素材有统一预留位置（见下），无任何第三方依赖，开箱即玩。

## 美术与可视化编辑

仓库现已包含一套可直接运行的原创扁平粗线条美术：3 名英雄、8 个敌人、12 把武器、12 个弹体和竞技场地图。打开场景点击 Play 即会自动读取。

程序按固定路径读取 `Assets/_Game/Resources/Art`：角色 `Characters/character_<id>.png`、敌人 `Enemies/enemy_<id>.png`、武器 `Weapons/weapon_<id>.png`、弹体 `Projectiles/projectile_<id>.png`、地图 `Map/map_arena.png`。同名替换图片即可换皮，不需要改代码。

1. Unity 菜单打开 **「土豆幸存者 / 打开可视化编辑中心」**。
2. 角色、敌人、武器、弹体和地图图片直接拖到 `GameArtLibrary` 对应槽位。
3. 需要调整尺寸、碰撞和层级时，打开对应 Prefab；UI 位置、颜色和文字直接编辑 `UICanvas.prefab`。
4. 若希望所有内容直接出现在 Hierarchy，打开主场景后执行 **「土豆幸存者 / 搭建场景结构」**。运行时会复用场景对象，不再销毁你的 Canvas 或重建布局。

| 对象 | 预制路径 | 换皮位置 |
|---|---|---|
| 玩家子弹（12 种武器各自一个） | `Prefabs/WeaponPool/bullet_{id}.prefab` | SpriteRenderer.Sprite |
| 敌人（8 种各自一个） | `Prefabs/EnemyPool/enemy_{id}.prefab` | SpriteRenderer.Sprite |
| 掉落物（材料） | `Prefabs/Items/item_material.prefab` | SpriteRenderer.Sprite |
| 敌方子弹 / Boss 预警 / 环绕体 / 炮台 | `Prefabs/Shared/enemy_bullet|boss_warning|orbit|turret.prefab` | SpriteRenderer.Sprite |
| 玩家 | `Prefabs/Shared/player.prefab` | SpriteRenderer.Sprite（含全部脚本组件） |
| 武器挂件（玩家身上） | `Prefabs/Shared/weapon_visual.prefab` | SpriteRenderer.Sprite（近战=方块贴手、远程=圆点挂肩腰、回旋镖=背） |
| 竞技场背景 / 边界 | `Prefabs/Shared/arena_bg|arena_border.prefab` | SpriteRenderer.Sprite |

> 数值调整统一在 `Assets/_Game/Resources/Data/waveconfig.json` 一个文件里（价格、商店槽位、升级成长、稀有度倍率/开放波次、治疗量等）；武器/敌人/道具/角色内容在对应 JSON。
