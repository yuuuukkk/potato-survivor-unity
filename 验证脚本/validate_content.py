# -*- coding: utf-8 -*-
"""《土豆幸存者》内容数据验证：JSON 合法性 + 交叉引用 + 枚举范围检查。"""
import json, os, sys, glob

BASE = r"D:\wenjian\rougelike\UnityProject\Assets\_Game\Resources\Data"
errors, warnings = [], []

def err(msg): errors.append(msg)
def warn(msg): warnings.append(msg)

def load(name):
    p = os.path.join(BASE, name)
    if not os.path.exists(p):
        err(f"缺少文件: {name}")
        return None
    try:
        with open(p, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception as e:
        err(f"{name} 解析失败: {e}")
        return None

weapons = load("weapons.json")
enemies = load("enemies.json")
items = load("items.json")
chars = load("characters.json")
wave = load("waveconfig.json")

# ---- 枚举范围 ----
STAT_RANGE = range(0, 14)
KIND_RANGE = range(0, 5)
RARITY_RANGE = range(0, 5)
BEHAVIOR_RANGE = range(0, 6)

# ---- 武器 ----
w_ids = set()
if weapons:
    for w in weapons["weapons"]:
        wid = w.get("id")
        if not wid: err("weapons: 存在无 id 条目"); continue
        if wid in w_ids: err(f"weapons: 重复 id {wid}")
        w_ids.add(wid)
        if w.get("kind") not in KIND_RANGE: err(f"武器 {wid}: kind 越界 {w.get('kind')}")
        if w.get("rarity") not in RARITY_RANGE: err(f"武器 {wid}: rarity 越界 {w.get('rarity')}")
        for f in ("damage","attackInterval","range","basePrice"):
            if not isinstance(w.get(f), (int, float)): err(f"武器 {wid}: {f} 缺失或非数字")
        if w.get("bulletRadius", 0.1) <= 0: err(f"武器 {wid}: bulletRadius 必须 > 0")
        if w.get("attackInterval", 0) <= 0: err(f"武器 {wid}: attackInterval 必须 > 0")
        if w.get("projectileCount", 1) < 1: err(f"武器 {wid}: projectileCount 必须 >= 1")
        if w.get("kind") == 4 and w.get("projectileSpeed", 0) <= 0:
            err(f"武器 {wid}: 回旋镖 projectileSpeed 必须 > 0")
        if w.get("projectileVisualSize", 0.38) <= 0:
            err(f"武器 {wid}: projectileVisualSize 必须 > 0")
    print(f"[OK] 武器 {len(w_ids)} 种")

# ---- 敌人 ----
e_ids = set()
if enemies:
    for e in enemies["enemies"]:
        eid = e.get("id")
        if not eid: err("enemies: 存在无 id 条目"); continue
        if eid in e_ids: err(f"enemies: 重复 id {eid}")
        e_ids.add(eid)
        if e.get("behavior") not in BEHAVIOR_RANGE: err(f"敌人 {eid}: behavior 越界 {e.get('behavior')}")
        for f in ("maxHp","moveSpeed","contactDamage"):
            if not isinstance(e.get(f), (int, float)): err(f"敌人 {eid}: {f} 缺失或非数字")
        sid = e.get("splitEnemyId")
        if sid and sid not in e_ids: warnings.append(f"敌人 {eid}: splitEnemyId '{sid}' 未在敌人表中（可能定义顺序在后）")
    print(f"[OK] 敌人 {len(e_ids)} 种")
    # 分裂引用最终检查
    for e in enemies["enemies"]:
        sid = e.get("splitEnemyId")
        if sid and sid not in e_ids: err(f"敌人 {e['id']}: splitEnemyId '{sid}' 不存在")

# ---- 道具 ----
i_ids = set()
if items:
    for it in items["items"]:
        iid = it.get("id")
        if not iid: err("items: 存在无 id 条目"); continue
        if iid in i_ids: err(f"items: 重复 id {iid}")
        i_ids.add(iid)
        if it.get("rarity") not in RARITY_RANGE: err(f"道具 {iid}: rarity 越界")
        if not isinstance(it.get("basePrice"), (int, float)): err(f"道具 {iid}: basePrice 缺失")
        mods = it.get("modifiers") or []
        consumable = bool(it.get("isConsumable"))
        heal = it.get("healAmount", 0) or 0
        if not mods and not (consumable and heal > 0):
            warn(f"道具 {iid}: 无 modifiers（纯文本道具？）")
        for m in mods:
            if m.get("type") not in STAT_RANGE: err(f"道具 {iid}: modifier type 越界 {m.get('type')}")
        # 正负混合检查（支柱 2：高稀有度道具必须带可读代价；普通/罕见允许基线成长）
        # 消耗品（如急救包）以 healAmount 作为正面效果
        pos = any((m.get("flat",0)>0 or m.get("percent",0)>0) for m in mods) or (consumable and heal > 0)
        neg = any((m.get("flat",0)<0 or m.get("percent",0)<0) for m in mods)
        if not pos: err(f"道具 {iid}: 无正面效果")
        if not neg and it.get("rarity", 0) >= 2: err(f"道具 {iid}: 稀有度 {it.get('rarity')} 无代价，违反支柱 2")
    print(f"[OK] 道具 {len(i_ids)} 件")

# ---- 角色 ----
c_ids = set()
if chars:
    for c in chars["characters"]:
        cid = c.get("id")
        if not cid: err("characters: 存在无 id 条目"); continue
        if cid in c_ids: err(f"characters: 重复 id {cid}")
        c_ids.add(cid)
        if not isinstance(c.get("maxWeaponSlots"), int): err(f"角色 {cid}: maxWeaponSlots 缺失")
        for sw in c.get("startingWeapons") or []:
            wid = sw.get("weaponId")
            if wid not in w_ids: err(f"角色 {cid}: 起始武器 '{wid}' 不存在于 weapons.json")
        for m in c.get("baseStats") or []:
            if m.get("type") not in STAT_RANGE: err(f"角色 {cid}: baseStats type 越界 {m.get('type')}")
    print(f"[OK] 角色 {len(c_ids)} 名")

# ---- 波次配置 ----
if wave and wave.get("config"):
    cfg = wave["config"]
    for f in ("waveCount","waveDuration","hpGrowth","dmgGrowth","budgetBase","budgetPerWave","spawnIntervalBase","spawnIntervalMin"):
        if not isinstance(cfg.get(f), (int, float)): err(f"waveconfig: {f} 缺失或非数字")
    if not isinstance(cfg.get("bossWaves"), list) or len(cfg["bossWaves"]) == 0: err("waveconfig: bossWaves 为空")
    if not isinstance(cfg.get("rarityWeights"), list) or len(cfg["rarityWeights"]) != 5: err("waveconfig: rarityWeights 必须 5 个")
    for bw in cfg.get("bossWaves", []):
        if bw > cfg.get("waveCount", 20): err(f"waveconfig: boss 波 {bw} 超出总波数")
    # Boss 表必须存在
    if "boss1" not in e_ids or "boss2" not in e_ids: err("waveconfig: 需要 boss1/boss2 敌人定义")
    if "elite_chaser" not in e_ids: err("waveconfig: 需要 elite_chaser 敌人定义")
    print(f"[OK] 波次配置 {cfg.get('waveCount')} 波")

# ---- 汇总 ----
print("\n========== 结果 ==========")
print(f"错误: {len(errors)}  警告: {len(warnings)}")
for e in errors: print(" [ERR]", e)
for w in warnings: print(" [WARN]", w)
sys.exit(1 if errors else 0)
