# 《土豆幸存者》Unity 架构设计

| 字段 | 内容 |
|---|---|
| 版本 | v0.1（与 GDD 同版） |
| 引擎 | Unity 2022.3 LTS（2021.3+ 兼容，Unity 6 可升） |
| 渲染 | 内置渲染管线 2D（SpriteRenderer），无 URP 依赖 |
| UI | uGUI（com.unity.ugui），运行时构建，无场景预设 |
| 输入 | 旧版 Input Manager（`Input.GetAxisRaw`），无 Input System 依赖 |
| 数据 | Resources 下 JSON + JsonUtility，运行时加载 |

---

## 1. 技术目标与约束

1. **开箱即玩**：从空场景按 Play 即可运行——`GameBootstrap` 通过 `[RuntimeInitializeOnLoadMethod]` 自举，运行时创建摄像机、竞技场、玩家、管理器、Canvas 与 EventSystem，无需任何手工配置。
2. **零美术资源**：占位美术（角色/敌人/子弹/道具图标）由 `SpriteFactory` 运行时生成纯色圆片，换皮只需替换视觉层。
3. **数据驱动**：武器/敌人/道具/角色/波次全部来自 `Assets/_Game/Data/*.json`，新增内容不改代码。
4. **零第三方依赖**：只用 UnityEngine 内置模块，降低环境差异导致的编译失败风险。

---

## 2. 目录结构与命名空间

```
Assets/_Game/
├─ Scripts/
│  ├─ Core/      RogueLike.Core      # 自举、状态机、事件、对象池、竞技场
│  ├─ Data/      RogueLike.Data      # 数据类、数据库、JSON 加载、占位美术
│  ├─ Player/    RogueLike.Player    # 玩家移动、属性聚合、血量
│  ├─ Combat/    RogueLike.Combat    # 伤害接口、投射物、武器系统
│  ├─ Enemies/   RogueLike.Enemies   # 敌人 AI、波次导演、生成器、敌人注册表
│  ├─ Items/     RogueLike.Items     # 背包、商店、材料拾取
│  └─ UI/        RogueLike.UI        # uGUI 工厂、HUD、商店/主菜单/结算界面
└─ Data/                            # weapons.json / enemies.json / items.json / characters.json / waveconfig.json
```

依赖方向：`UI → Items/Player → Combat → Enemies → Data`，`Core` 被所有人依赖，事件通过 `EventBus` 反向解耦（UI 不持有系统引用，只订阅事件）。

---

## 3. 模块职责

| 模块 | 职责 | 关键类 |
|---|---|---|
| 自举 | 构建整棵运行时对象树 | `GameBootstrap` |
| 流程 | 状态机（主菜单/战斗/商店/结算）、材料账本 | `GameManager` |
| 事件 | 系统间解耦 | `EventBus` |
| 池化 | 子弹/敌人/伤害数字复用 | `ObjectPool` |
| 数据 | 内容定义、加载、查询 | `GameDatabase` `DataLoader` |
| 玩家 | 移动钳制、属性聚合、受击/回血/死亡 | `PlayerController` `PlayerStats` `PlayerHealth` |
| 武器 | 槽位、冷却、索敌、开火模式 | `WeaponSystem` `WeaponInstance` |
| 投射物 | 飞行、碰撞、穿透、爆炸 | `Projectile` |
| 敌人 | 行为 AI、受击、死亡掉落 | `Enemy` `EnemyManager` |
| 波次 | 预算、节奏、缩放、Boss 调度 | `WaveDirector` `EnemySpawner` |
| 商店 | 生成报价、购买/刷新/锁定 | `ShopSystem` |
| 背包 | 道具栈、修正聚合 | `Inventory` |
| UI | 全部界面 | `UIFactory` `HUDController` `ShopUI` `MainMenuUI` `GameOverUI` |

---

## 4. 核心类说明（实现要点）

### 4.1 GameBootstrap（Core）
- `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` 静态入口，幂等保护（静态标志位）。
- 创建顺序：摄像机（正交，视口覆盖竞技场）→ 竞技场边界 → Canvas + EventSystem → 玩家（含 PlayerStats/Health/Controller/WeaponSystem）→ WaveDirector → ShopSystem → HUD → MainMenu。
- 公共字段可调（竞技场尺寸、默认角色、是否跳过主菜单直接开打），便于快速试玩。

### 4.2 GameManager（Core，单例）
- `GameState`：`MainMenu / Playing / Shop / GameOver`，进入/离开有事件广播。
- 账本：`Materials`（int），`AddMaterials / TrySpend`，变化广播 `MaterialsChanged`。
- 流程：`StartRun(CharacterData)` → `StartWave(1)` → 波内由 WaveDirector 驱动 → `EndWave` 结算入账 → `EnterShop` → `CloseShop` → `StartWave(w+1)`；`PlayerDied` → `GameOver`。
- 持有所有系统引用（Player、WaveDirector、ShopSystem、UI 屏），是唯一的"接线层"。

### 4.3 EventBus（Core）
静态事件 + `Raise*` 方法，覆盖：`WaveStarted(int)`、`WaveEnded(int,int)`、`EnemyKilled`、`MaterialsChanged(int)`、`PlayerDied`、`ShopOpened/Closed`、`RunStarted(CharacterData)`、`GameOver`、`WeaponsChanged`。UI 全部通过订阅驱动，避免直接引用。

### 4.4 ObjectPool（Core）
- 实例化池：`Get(Vector3) / Release(GameObject)`，`Factory` 委托创建模板，无模板则运行时组装（如敌人 = 空 GO + SpriteRenderer + CircleCollider2D + Enemy）。
- 池内对象 `SetActive(false)` 复用；场景卸载时自动清空（`OnDestroy`）。

### 4.5 ArenaBounds（Core）
静态矩形（默认 24×13.5）；`Clamp`、`RandomEdgePoint(margin)`、`Contains`。

### 4.6 PlayerStats（Player）
- 基础属性来自 `CharacterData.baseStats`（一组 `StatModifier`，视作 flat 基线）。
- 持有 `Dictionary<StatType, (flatSum, percentSum)>` 运行时聚合；`Get(StatType)` 按 `(base + flat) × (1 + percent)` 返回。
- `AddModifier / RemoveModifier / Rebuild()`；道具增删与武器系统都通过它结算。

### 4.7 WeaponSystem / WeaponInstance（Combat）
- `WeaponSystem`：槽位上限（角色定），`List<WeaponInstance>`；`AddWeapon(data, level)` 同 id 升级；`LevelUp(id)`；`WeaponsChanged` 广播给 HUD。
- `WeaponInstance`：持有 data/level/冷却；`TryFire(owner, aimDir, stats)` 按 `WeaponKind` 分派：
  - `Projectile`：N 发扇形散射（`spreadDeg`），目标 = `EnemyManager.Nearest`；
  - `Melee`：冷却到对面前扇形 OverlapCircleAll 结算伤害+击退；
  - `Orbit`：场上无该武器实例的环绕体时生成一个 `OrbitBody`（随玩家移动的旋转切割体）；
  - `Turret`：场上该武器炮台 < 2 时生成固定炮台（自行索敌发射子弹）；
  - `Boomerang`：发射往返投射物（`Projectile` 带 `boomerang` 标志，去程命中后折返再命中）。
- 有效属性：`伤害 = data.damage × (1+0.5×(level−1)) × (1+DamageMult)`；`间隔 = data.attackInterval ÷ (1+AttackSpeedMult) ÷ (1+0.08×(level−1))`；`射程 = data.range × (1+RangeMult)`。

### 4.8 Projectile（Combat）
- 字段：`team`（0 玩家/1 敌方）、伤害、速度、射程寿命、穿透数、爆炸半径、暴击信息。
- `OnTriggerEnter2D`：命中敌人/玩家按 team 结算；`pierce--` 后 0 则释放回池；`explodeRadius>0` 时对范围内全体结算。
- 敌方子弹复用同一组件（team=1），命中玩家走 `PlayerHealth.TakeDamage`。

### 4.9 Enemy（Enemies）
- `EnemyData` 驱动：行为分支（追/远程风筝/自爆/坦克/分裂/Boss），波次缩放系数在生成时注入。
- 接触伤害：与玩家重叠时按自身攻击间隔结算（0.5s 内置冷却）。
- 受击：闪白 + 击退 + `DamageNumber`；死亡：掉材料（`MaterialPickup`）、分裂（生成子体）、`EnemyManager.Unregister`、回池、广播 `EnemyKilled`。
- Boss：`behavior=5`，周期性放射 8 发弹幕（1s 蓄力红圈，用 `SpriteFactory` 生成预警圆环）。

### 4.10 WaveDirector（Enemies）
- 从 `WaveConfig` 读：波数、时长、预算公式、缩放公式、Boss 波、精英起始波。
- 运行时：`SpawnBudget(w)` 递减 → 按节奏协程出怪（间隔随波次与场上存活数收敛到下限）→ 场上存活上限封顶 → 计时结束 `GameManager.EndWave`。
- 生成位置 `ArenaBounds.RandomEdgePoint`，敌人属性注入 `hpScale/dmgScale/speedScale`。

### 4.11 ShopSystem（Items）
- `Open(wave)`：生成 4 武器 + 4 道具 + 1 治疗槽；稀有度按权重表（波次提升 + 幸运加成）掷骰；价格 `基础价×稀有倍率 + 2×wave`；已有武器显示升级价（×0.8）。
- `Buy / Refresh / Lock`；`RefreshCost = 4 + wave`；`Close` 时未消费资金直接结转下一波（波内新拾取累加）。

### 4.12 Inventory（Items）
- `List<ItemStack(data, count)>`；`AddItem` 将道具的 modifiers 推入 `PlayerStats`（正负同时生效，重买累加，`maxStack` 封顶）；无出售。

### 4.13 UIFactory（UI）
- 方法：`CreateCanvas`（ScreenSpaceOverlay + CanvasScaler 1280×720）、`CreateEventSystem`、`CreateText`、`CreateButton`（含点击音占位）、`CreatePanel`（Image 纯色）、`CreateImage`（含 fillAmount 支持，用于血条）。
- 所有元素用锚点+归一化坐标布局，分辨率自适应。

---

## 5. 数据流（关键路径）

```
波次开始
  GameManager.StartWave(w)
    → WaveDirector 加载缩放系数 + 启动生成协程
    → EventBus.RaiseWaveStarted
  WeaponSystem.Update（每帧）
    → EnemyManager.Nearest(目标) → 冷却到 → 开火 → Projectile 命中 → 伤害结算
  Enemy 死亡
    → MaterialPickup 生成 → 磁吸拾取 → GameManager.AddMaterials
    → EventBus.RaiseEnemyKilled（HUD 击杀计数）

波次结束
  WaveDirector 计时到 → GameManager.EndWave
    → 波奖励入账 → EventBus.RaiseWaveEnded → EnterShop
    → ShopSystem.Open(w) → ShopUI 渲染报价

商店结算
  购买 → ShopSystem.Buy → 武器进 WeaponSystem / 道具进 Inventory → PlayerStats.Rebuild
  → EventBus.RaiseWeaponsChanged / MaterialsChanged（HUD 同步）
  → CloseShop → StartWave(w+1)
```

## 6. 状态机

```
                 StartRun(选角色)
   ┌─ MainMenu ───────────────► Playing ──波 30s 结束──► Shop
   │    ▲                        │  ▲                     │
   │    │                        │  │      CloseShop(下一波)│
   │    └── 重开/回主菜单 ◄───────┴──┴──────────────────────┘
   │          GameOver ◄────── PlayerDied（任何状态）
   └────────────────────────────────────────────────────┘
```
- 状态进入即广播事件，UI 只订阅事件切换显隐，杜绝状态穿透。
- 异常路径：商店中死亡不可能（商店无敌人）；主菜单直接开打（调试开关）跳过选角；`GameOver` 后 `Restart` 重置全部单例状态（池清空、属性重建）。

---

## 7. 关键设计决策（含理由）

| 决策 | 理由 | 代价/扩展点 |
|---|---|---|
| 运行时自举而非场景预设 | 免配置开箱即玩 | 无法可视化编辑场景；正式版可迁移到预设（仅换 Bootstrap 注入方式） |
| 运行时生成占位美术 | 零资源依赖 | 换皮时替换 SpriteFactory 或给 Prefab 挂 Sprite |
| JSON + JsonUtility 而非 ScriptableObject 资产 | 文本可 diff、可版本管理、内容者友好 | 枚举存 int，需对照表；正式版可换 SO 编辑器 |
| Tag(Player) + GetComponent 判定而非 LayerMatrix | 免图层配置 | 敌人超过 500 时建议转 Layer |
| 静态 EnemyManager 注册表而非空间分区 | 原型规模（≤60 敌）足够 | 敌人过密时换网格分区 |
| 旧版 Input 而非 Input System | 零额外包 | 迁移成本低，见路线图 |

## 8. 性能预算

- 峰值：60 敌人 + ~200 子弹 + 100 伤害数字，全部池化，无每帧分配（事件无参缓存）。
- 目标：1080p 桌面 ≥ 60 FPS；若目标移动端，先加空间分区与子弹合并渲染。

## 9. 扩展点

- 新武器/敌人/道具/角色：加 JSON 条目即可（行为需新增时扩展 `WeaponKind`/`behavior` 分支）。
- 音效：`UIFactory`/武器开火处预留 `AudioSource` 占位。
- 存档：`SaveSystem`（PlayerPrefs + 版本号），P1 接入。
- 换皮：`SpriteFactory` 改为从 Resources 加载 Sprite 的映射层。
