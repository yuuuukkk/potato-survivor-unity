using System.Collections.Generic;
using RogueLike.Combat;
using RogueLike.Data;
using RogueLike.Enemies;
using RogueLike.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RogueLike.Core
{
    /// <summary>运行时 GM 独立界面：桌面端按 F1，手机端通过 HUD 按钮打开。</summary>
    public sealed class GMPanel : MonoBehaviour
    {
        private enum GmTab { Player, Weapons, Items, Modifications, Enemies, Flow }
        private readonly List<string> _weaponIds = new List<string>();
        private readonly List<string> _enemyIds = new List<string>();
        private readonly List<string> _itemIds = new List<string>();
        private readonly List<string> _modificationIds = new List<string>();
        private GmTab _tab = GmTab.Weapons;
        private bool _visible;
        private string _search = string.Empty;
        private float _previousTimeScale = 1f;
        private GameState _stateAtOpen;
        private EventSystem _eventSystem;
        private bool _eventSystemWasEnabled;
        private string _message = string.Empty;
        private float _messageUntil;
        private Vector2 _scroll;
        private string _levelInput = "10";
        private string _statInput = "10";
        private static readonly StatType[] EditableStats = (StatType[])System.Enum.GetValues(typeof(StatType));

        private void Awake()
        {
            _weaponIds.Clear();
            foreach (var pair in GameDatabase.Weapons) _weaponIds.Add(pair.Key);
            _weaponIds.Sort();
            _enemyIds.Clear();
            foreach (var pair in GameDatabase.Enemies) _enemyIds.Add(pair.Key);
            _enemyIds.Sort((a, b) =>
            {
                var left = GameDatabase.GetEnemy(a);
                var right = GameDatabase.GetEnemy(b);
                int tier = EnemyTier(left).CompareTo(EnemyTier(right));
                if (tier != 0) return tier;
                int wave = (left != null ? left.firstWave : 1).CompareTo(right != null ? right.firstWave : 1);
                if (wave != 0) return wave;
                int hp = (left != null ? left.maxHp : 0f).CompareTo(right != null ? right.maxHp : 0f);
                if (hp != 0) return hp;
                return string.CompareOrdinal(a, b);
            });
            foreach (var pair in GameDatabase.Items) _itemIds.Add(pair.Key);
            _itemIds.Sort();
            foreach (var pair in GameDatabase.WeaponModifications) _modificationIds.Add(pair.Key);
            _modificationIds.Sort();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) Toggle();
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_visible)
            {
                _previousTimeScale = Time.timeScale;
                _stateAtOpen = GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu;
                Time.timeScale = 0f;
                _eventSystem = EventSystem.current;
                if (_eventSystem != null)
                {
                    _eventSystemWasEnabled = _eventSystem.enabled;
                    _eventSystem.enabled = false;
                }
            }
            else RestoreGameInput();
        }

        private void OnDisable()
        {
            if (!_visible) return;
            _visible = false;
            RestoreGameInput();
        }

        private void RestoreGameInput()
        {
            if (_eventSystem != null) _eventSystem.enabled = _eventSystemWasEnabled;
            _eventSystem = null;
            var gm = GameManager.Instance;
            Time.timeScale = gm != null && gm.State != _stateAtOpen
                ? (gm.State == GameState.LevelUp ? 0f : 1f) : _previousTimeScale;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            var gm = GameManager.Instance;
            if (gm == null) return;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1100f, Screen.height / 650f), 0.8f, 1.6f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.83f);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUILayout.BeginArea(new Rect(22f, 18f, width - 44f, height - 36f), GUI.skin.window);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"GM 测试中心    {gm.State}  ·  第 {gm.Wave} 波  ·  材料 {gm.Materials}", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("关闭  F1", GUILayout.Width(110f), GUILayout.Height(32f))) Toggle();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            TabButton("玩家/属性", GmTab.Player);
            TabButton("武器", GmTab.Weapons);
            TabButton("道具", GmTab.Items);
            TabButton("武器改造", GmTab.Modifications);
            TabButton("敌人", GmTab.Enemies);
            TabButton("流程", GmTab.Flow);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            if (_tab == GmTab.Weapons || _tab == GmTab.Items ||
                _tab == GmTab.Modifications || _tab == GmTab.Enemies)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("搜索名称", GUILayout.Width(76f));
                _search = GUILayout.TextField(_search, GUILayout.Height(30f));
                if (GUILayout.Button("清空", GUILayout.Width(60f), GUILayout.Height(30f))) _search = string.Empty;
                GUILayout.EndHorizontal();
            }
            if (_tab == GmTab.Modifications)
            {
                GUILayout.Label("这里只显示当前持有武器适用的构筑；解锁后可在商店 → 武器构筑中装备。");
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            switch (_tab)
            {
                case GmTab.Player: DrawPlayer(gm); break;
                case GmTab.Weapons: DrawWeapons(gm); break;
                case GmTab.Items: DrawItems(gm); break;
                case GmTab.Modifications: DrawModifications(gm); break;
                case GmTab.Enemies: DrawEnemies(gm); break;
                case GmTab.Flow: DrawFlow(gm); break;
            }
            GUILayout.EndScrollView();
            if (Time.unscaledTime < _messageUntil) GUILayout.Label(_message);
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }

        private void TabButton(string label, GmTab tab)
        {
            if (!GUILayout.Button((_tab == tab ? "● " : "") + label, GUILayout.Height(36f))) return;
            if (_tab == tab) return;
            _tab = tab;
            _search = string.Empty;
            _scroll = Vector2.zero;
        }

        private bool Matches(string id, string name, string description = null)
        {
            if (string.IsNullOrWhiteSpace(_search)) return true;
            return id != null && id.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name != null && name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   description != null && description.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void WrappedLabel(string value)
        {
            var style = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label(value ?? string.Empty, style, GUILayout.ExpandWidth(true));
        }

        private static void SectionLabel(string value, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 17 };
            style.normal.textColor = color;
            GUILayout.Space(8f);
            GUILayout.Label(value, style);
        }

        private void DrawPlayer(GameManager gm)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+500 材料", GUILayout.Height(34f))) { gm.AddMaterials(500); Tell("材料 +500"); }
            if (GUILayout.Button("回满生命", GUILayout.Height(34f))) { gm.Player?.Health.Heal(99999f); Tell("生命已回满"); }
            GUILayout.EndHorizontal();
            var health = gm.Player != null ? gm.Player.Health : null;
            if (health != null) health.DebugInvincible = GUILayout.Toggle(health.DebugInvincible, "无敌（测试）");
            GUILayout.BeginHorizontal();
            GUILayout.Label("设等级", GUILayout.Width(70f));
            _levelInput = GUILayout.TextField(_levelInput, GUILayout.Width(90f));
            if (GUILayout.Button("应用", GUILayout.Width(90f)))
            {
                var xp = gm.Player != null ? gm.Player.GetComponent<XpSystem>() : null;
                if (xp != null && int.TryParse(_levelInput, out int level))
                { xp.SetLevelForTesting(level); Tell($"等级已设为 {xp.Level}"); }
                else Tell("请输入有效等级");
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("属性调整量", GUILayout.Width(90f));
            _statInput = GUILayout.TextField(_statInput, GUILayout.Width(90f));
            GUILayout.Label("点击对应属性的 + 或 −；百分比属性可输入 0.1 表示增加 10%。");
            GUILayout.EndHorizontal();
            foreach (var type in EditableStats)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                float value = gm.Player != null && gm.Player.Stats != null ? gm.Player.Stats.Get(type) : 0f;
                GUILayout.Label($"{StatName(type)}    当前 {value:0.##}", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("+", GUILayout.Width(52f))) AdjustStat(gm, type, 1f);
                if (GUILayout.Button("−", GUILayout.Width(52f))) AdjustStat(gm, type, -1f);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawWeapons(GameManager gm)
        {
            var ws = gm.Player != null ? gm.Player.GetComponent<WeaponSystem>() : null;
            GUILayout.Label(ws != null ? $"已持有 {ws.Weapons.Count}/{ws.MaxSlots} 把武器；点击右侧直接获得 Lv.1。" : "开始游戏后可获得武器。");
            for (int group = 0; group < 2; group++)
            {
                bool hasRows = false;
                foreach (var id in _weaponIds)
                {
                    var data = GameDatabase.GetWeapon(id);
                    if (data == null || (data.kind == WeaponKind.Melee) != (group == 0) ||
                        !Matches(id, data.displayName, data.description)) continue;
                    if (!hasRows)
                    {
                        SectionLabel(group == 0 ? "近战武器" : "远程及特殊武器", Color.white);
                        hasRows = true;
                    }
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                    GUILayout.Label($"{data.displayName}    [{id}]");
                    WrappedLabel(data.description);
                    GUILayout.EndVertical();
                    bool enabled = GUI.enabled;
                    GUI.enabled = ws != null && ws.CanAcquire(data, 1);
                    if (GUILayout.Button("获得 Lv.1", GUILayout.Width(120f), GUILayout.Height(48f)))
                        Tell(ws.AddWeapon(data, 1) ? $"已获得 {data.displayName}" : "无法加入武器");
                    GUI.enabled = enabled;
                    GUILayout.EndHorizontal();
                }
            }
        }

        private void DrawItems(GameManager gm)
        {
            var inventory = gm.Player != null ? gm.Player.GetComponent<RogueLike.Items.Inventory>() : null;
            var ws = gm.Player != null ? gm.Player.GetComponent<WeaponSystem>() : null;
            GUILayout.Label("常驻道具加入背包；消耗品点击后立即生效。");
            for (int tier = (int)Rarity.Common; tier <= (int)Rarity.Red; tier++)
            {
                bool hasRows = false;
                foreach (var id in _itemIds)
                {
                    var item = GameDatabase.GetItem(id);
                    if (item == null || (int)item.rarity != tier ||
                        !Matches(id, item.displayName, item.description)) continue;
                    if (!hasRows)
                    {
                        SectionLabel(RarityName(item.rarity), RarityInfo.Color(item.rarity));
                        hasRows = true;
                    }
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                    var nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                    nameStyle.normal.textColor = RarityInfo.Color(item.rarity);
                    string count = item.isConsumable ? "消耗品" : $"{inventory?.GetCount(id) ?? 0}/{item.maxStack}";
                    GUILayout.Label($"{item.displayName}    [{id}]    {count}", nameStyle);
                    WrappedLabel(item.description);
                    GUILayout.EndVertical();
                    bool usable = item.isConsumable ? gm.Player != null :
                        inventory != null && inventory.GetCount(id) < item.maxStack &&
                        (!item.requiresProjectileWeapon || ws != null && ws.HasStandardProjectileWeapon);
                    bool enabled = GUI.enabled;
                    GUI.enabled = usable;
                    if (GUILayout.Button(item.isConsumable ? "立即使用" : "获得道具",
                            GUILayout.Width(120f), GUILayout.Height(48f)))
                    {
                        if (item.isConsumable)
                        {
                            gm.Player.Health.Heal(item.healAmount);
                            Tell($"已使用 {item.displayName}");
                        }
                        else Tell(inventory.AddItem(item) ? $"已获得 {item.displayName}" : "道具无法加入");
                    }
                    GUI.enabled = enabled;
                    GUILayout.EndHorizontal();
                }
            }
        }

        private void DrawModifications(GameManager gm)
        {
            var ws = gm.Player != null ? gm.Player.GetComponent<WeaponSystem>() : null;
            int shown = 0;
            foreach (var id in _modificationIds)
            {
                var mod = GameDatabase.GetWeaponModification(id);
                if (mod == null || !Matches(id, mod.displayName, mod.description)) continue;
                if (!HasOwnedWeaponForModification(ws, mod)) continue;
                bool applicable = ws.CanApplyModification(mod);
                shown++;
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                GUILayout.Label($"{mod.displayName}    [{id}]    适用：{(mod.weaponId == "*" ? "通用" : WeaponName(mod.weaponId))}    已解锁 Lv.{(ws != null ? ws.ModificationLevel(id) : 0)}");
                WrappedLabel(mod.description);
                GUILayout.EndVertical();
                bool enabled = GUI.enabled;
                GUI.enabled = applicable;
                string action = ws.ModificationLevel(id) >= mod.MaxLevel ? "已满级" : "解锁 / 升级";
                if (GUILayout.Button(action, GUILayout.Width(120f), GUILayout.Height(52f)))
                    Tell(ws.TryApplyModification(mod) ? $"已解锁 {mod.displayName}；去商店的武器构筑页装备" : "无法解锁改造");
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();
            }
            if (shown == 0) GUILayout.Label("没有匹配的构筑。先在武器页获得对应武器。");
        }

        private void DrawEnemies(GameManager gm)
        {
            if (GUILayout.Button("清除当前敌人（计击杀和掉落）", GUILayout.Height(34f))) KillAll(gm);
            int lastTier = -1;
            foreach (var id in _enemyIds)
            {
                var data = GameDatabase.GetEnemy(id);
                if (data == null || !Matches(id, data.displayName)) continue;
                int tier = EnemyTier(data);
                if (tier != lastTier)
                {
                    lastTier = tier;
                    SectionLabel(tier == 0 ? "普通敌人 · 按首次出现波次" :
                        tier == 1 ? "精英敌人" : "Boss", Color.white);
                }
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(tier == 0
                    ? $"{data.displayName}    [{id}]    第 {data.firstWave} 波起"
                    : $"{data.displayName}    [{id}]    {EnemyTierName(data)}",
                    GUILayout.ExpandWidth(true));
                if (GUILayout.Button("在玩家附近生成", GUILayout.Width(150f))) SpawnSelectedEnemy(gm, id);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawFlow(GameManager gm)
        {
            GUILayout.Label("波次与商店流程");
            if (GUILayout.Button("结束当前波（正常结算、升级、进入商店）", GUILayout.Height(42f)))
            {
                if (gm.Director != null && gm.Director.CurrentWave >= GameDatabase.WaveConfig.waveCount)
                    Tell("最终波必须击败 Boss 才能结束");
                else Tell(gm.Director != null && gm.Director.ForceEndWaveForTesting() ? "当前波已结束" : "当前不在战斗波次");
            }
            WrappedLabel("GM 界面打开时会暂停战斗和倒计时；关闭后继续。改造需在商店的「武器构筑」页装备到具体武器。");
        }

        private void SpawnSelectedEnemy(GameManager gm, string id)
        {
            if (string.IsNullOrEmpty(id) || gm.Player == null || gm.Director == null) { Tell("当前没有可生成的敌人"); return; }
            Vector2 offset = Random.insideUnitCircle.normalized * 2.5f;
            var enemy = EnemyPool.Spawn(id, gm.Player.transform.position + (Vector3)offset,
                gm.Director.HpScale(), gm.Director.DmgScale(), gm.Director.SpeedScale());
            Tell(enemy != null ? "敌人已生成" : "敌人生成失败");
        }

        private void AdjustStat(GameManager gm, StatType type, float sign)
        {
            if (gm.Player == null || gm.Player.Stats == null || !float.TryParse(_statInput, out float amount))
            { Tell("请输入有效数值并确保玩家已生成"); return; }
            var modifier = new StatModifier(type, amount * sign);
            gm.Player.Stats.AddModifier(modifier);
            if (type == StatType.MaxHp)
            {
                var health = gm.Player.GetComponent<PlayerHealth>();
                if (health != null) { health.RebuildMaxHp(); health.Heal(health.MaxHp); }
            }
            else if (type == StatType.Armor || type == StatType.DodgeChance || type == StatType.HpRegen)
            {
                // Attributes are read live by combat systems; no explicit refresh required.
            }
            Tell($"{StatName(type)} {(sign > 0 ? "+" : "−")}{amount}");
        }

        private static string WeaponName(string id)
        { var d = GameDatabase.GetWeapon(id); return d != null && !string.IsNullOrEmpty(d.displayName) ? d.displayName : id; }
        private static bool HasOwnedWeaponForModification(WeaponSystem ws, WeaponModificationData mod)
        {
            if (ws == null || mod == null) return false;
            foreach (var weapon in ws.Weapons)
                if (weapon != null && mod.AppliesTo(weapon.Data)) return true;
            return false;
        }

        private static int EnemyTier(EnemyData data)
        { return data == null ? 0 : data.isBoss ? 2 : data.isElite ? 1 : 0; }

        private static string EnemyTierName(EnemyData data)
        { return data.isBoss ? "Boss" : data.isElite ? "精英" : "普通"; }

        private static string RarityName(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return "普通 · 白色";
                case Rarity.Uncommon: return "优秀 · 绿色";
                case Rarity.Rare: return "稀有 · 蓝色";
                case Rarity.Epic: return "史诗 · 紫色";
                case Rarity.Legendary: return "传说 · 金色";
                default: return "红色";
            }
        }
        private static string StatName(StatType type)
        {
            switch (type)
            {
                case StatType.MaxHp: return "最大生命"; case StatType.HpRegen: return "生命回复";
                case StatType.Armor: return "护甲"; case StatType.DodgeChance: return "闪避率";
                case StatType.DamageMult: return "伤害倍率"; case StatType.AttackSpeedMult: return "攻击速度";
                case StatType.CritChance: return "暴击率"; case StatType.CritDamageMult: return "暴击伤害";
                case StatType.RangeMult: return "攻击范围"; case StatType.MoveSpeedMult: return "移动速度";
                case StatType.LifeSteal: return "生命偷取"; case StatType.Luck: return "幸运";
                case StatType.PickupRange: return "拾取范围"; case StatType.Knockback: return "击退";
                default: return type.ToString();
            }
        }

        private static void KillAll(GameManager gm)
        {
            var enemies = new List<Enemy>();
            // Killing splitters creates children, so take bounded snapshots until the field is clear.
            for (int pass = 0; pass < 16; pass++)
            {
                EnemyManager.CopyAliveTo(enemies);
                if (enemies.Count == 0) break;
                foreach (var enemy in enemies)
                    if (enemy != null && enemy.IsAlive)
                        enemy.TakeDamage(new DamageInfo(100000f, gm.Player != null ? gm.Player.gameObject : null, true));
            }
        }

        private void Tell(string message)
        {
            _message = message;
            _messageUntil = Time.unscaledTime + 2.5f;
        }
    }
}
