using System.Collections.Generic;
using RogueLike.Combat;
using RogueLike.Data;
using RogueLike.Enemies;
using RogueLike.Player;
using UnityEngine;

namespace RogueLike.Core
{
    /// <summary>运行时 GM 面板：桌面端按 F1，手机端通过 HUD 按钮打开。</summary>
    public sealed class GMPanel : MonoBehaviour
    {
        private readonly List<string> _weaponIds = new List<string>();
        private readonly List<string> _enemyIds = new List<string>();
        private readonly List<string> _itemIds = new List<string>();
        private readonly List<string> _modificationIds = new List<string>();
        private int _weaponIndex;
        private int _enemyIndex;
        private int _itemIndex;
        private int _modificationIndex;
        private bool _visible;
        private string _message = string.Empty;
        private float _messageUntil;
        private Vector2 _scroll;
        private string _levelInput = "10";
        private string _statInput = "10";
        private int _statIndex;
        private static readonly StatType[] EditableStats = (StatType[])System.Enum.GetValues(typeof(StatType));

        private void Awake()
        {
            _weaponIds.Clear();
            foreach (var pair in GameDatabase.Weapons) _weaponIds.Add(pair.Key);
            _weaponIds.Sort();
            _enemyIds.Clear();
            foreach (var pair in GameDatabase.Enemies) _enemyIds.Add(pair.Key);
            _enemyIds.Sort();
            foreach (var pair in GameDatabase.Items)
                if (!pair.Value.isConsumable) _itemIds.Add(pair.Key);
            _itemIds.Sort();
            foreach (var pair in GameDatabase.WeaponModifications) _modificationIds.Add(pair.Key);
            _modificationIds.Sort();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) _visible = !_visible;
        }

        public void Toggle() => _visible = !_visible;

        private void OnGUI()
        {
            if (!_visible) return;
            var gm = GameManager.Instance;
            if (gm == null) return;
            GUILayout.BeginArea(new Rect(14f, 14f, 370f, Screen.height - 28f), GUI.skin.window);
            GUILayout.Label("GM 测试面板  ·  F1 关闭");
            GUILayout.Label($"状态 {gm.State}    波次 {gm.Wave}    材料 {gm.Materials}");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            GUILayout.Space(5f);
            GUILayout.Label("玩家 / 资源");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+500 材料")) { gm.AddMaterials(500); Tell("材料 +500"); }
            if (GUILayout.Button("回满生命")) { gm.Player?.Health.Heal(99999f); Tell("生命已回满"); }
            GUILayout.EndHorizontal();
            var health = gm.Player != null ? gm.Player.Health : null;
            bool invincible = health != null && health.DebugInvincible;
            bool nextInvincible = GUILayout.Toggle(invincible, "无敌（测试）");
            if (health != null) health.DebugInvincible = nextInvincible;
            GUILayout.BeginHorizontal();
            GUILayout.Label("直接设等级", GUILayout.Width(72f));
            _levelInput = GUILayout.TextField(_levelInput, GUILayout.Width(48f));
            if (GUILayout.Button("应用等级"))
            {
                var xp = gm.Player != null ? gm.Player.GetComponent<XpSystem>() : null;
                if (xp != null && int.TryParse(_levelInput, out int level))
                { xp.SetLevelForTesting(level); Tell($"等级已设为 {xp.Level}"); }
                else Tell("请输入有效等级");
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("经验测试：击杀怪物仍按配置 expReward 发放；GM 可直接设等级快速跳过升级流程。");

            GUILayout.BeginHorizontal();
            GUILayout.Label("调整属性", GUILayout.Width(60f));
            if (GUILayout.Button("‹", GUILayout.Width(28f))) _statIndex = (_statIndex - 1 + EditableStats.Length) % EditableStats.Length;
            var selectedStat = EditableStats[_statIndex];
            float currentValue = gm.Player != null && gm.Player.Stats != null ? gm.Player.Stats.Get(selectedStat) : 0f;
            GUILayout.Label(StatName(selectedStat) + " " + currentValue.ToString("0.##"), GUILayout.Width(100f));
            if (GUILayout.Button("›", GUILayout.Width(28f))) _statIndex = (_statIndex + 1) % EditableStats.Length;
            _statInput = GUILayout.TextField(_statInput, GUILayout.Width(45f));
            if (GUILayout.Button("+", GUILayout.Width(28f))) AdjustStat(gm, 1f);
            if (GUILayout.Button("−", GUILayout.Width(28f))) AdjustStat(gm, -1f);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("武器 / 敌人");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹ 武器")) Cycle(_weaponIds, ref _weaponIndex, -1);
            GUILayout.Label(_weaponIds.Count > 0 ? WeaponName(_weaponIds[_weaponIndex]) : "无武器数据", GUILayout.Width(155f));
            if (GUILayout.Button("武器 ›")) Cycle(_weaponIds, ref _weaponIndex, 1);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("获得所选武器（Lv.1）"))
            {
                if (gm.Player != null && _weaponIds.Count > 0)
                {
                    var ws = gm.Player.GetComponent<WeaponSystem>();
                    bool added = ws != null && ws.AddWeapon(GameDatabase.GetWeapon(_weaponIds[_weaponIndex]), 1);
                    Tell(added ? "武器已加入" : "无法加入：槽位已满或角色不允许使用");
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹ 道具")) Cycle(_itemIds, ref _itemIndex, -1);
            GUILayout.Label(_itemIds.Count > 0 ? GameDatabase.GetItem(_itemIds[_itemIndex]).displayName : "无道具数据", GUILayout.Width(155f));
            if (GUILayout.Button("道具 ›")) Cycle(_itemIds, ref _itemIndex, 1);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("获得所选道具（测试叠加）"))
            {
                var item = _itemIds.Count > 0 ? GameDatabase.GetItem(_itemIds[_itemIndex]) : null;
                var ws = gm.Player != null ? gm.Player.GetComponent<WeaponSystem>() : null;
                var inv = gm.Player != null ? gm.Player.GetComponent<RogueLike.Items.Inventory>() : null;
                bool usable = item != null && (!item.requiresProjectileWeapon ||
                    ws != null && ws.HasStandardProjectileWeapon);
                Tell(usable && inv != null && inv.AddItem(item) ? "道具已加入" : "无法加入：角色不适用或达到堆叠上限");
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹ 改造")) Cycle(_modificationIds, ref _modificationIndex, -1);
            var modification = _modificationIds.Count > 0 ?
                GameDatabase.GetWeaponModification(_modificationIds[_modificationIndex]) : null;
            GUILayout.Label(modification != null ? modification.displayName : "无改造数据", GUILayout.Width(155f));
            if (GUILayout.Button("改造 ›")) Cycle(_modificationIds, ref _modificationIndex, 1);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("应用所选武器改造"))
            {
                var ws = gm.Player != null ? gm.Player.GetComponent<WeaponSystem>() : null;
                Tell(ws != null && ws.TryApplyModification(modification) ? "构筑已解锁；在商店的武器构筑页装备" :
                    "无法应用：缺少对应武器、已有冲突改造或已应用");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹ 敌人")) Cycle(_enemyIds, ref _enemyIndex, -1);
            GUILayout.Label(_enemyIds.Count > 0 ? EnemyName(_enemyIds[_enemyIndex]) : "无敌人数据", GUILayout.Width(155f));
            if (GUILayout.Button("敌人 ›")) Cycle(_enemyIds, ref _enemyIndex, 1);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("在玩家旁生成所选敌人")) SpawnSelectedEnemy(gm);
            if (GUILayout.Button("清除当前敌人（计击杀/掉落）")) KillAll(gm);

            GUILayout.Space(8f);
            GUILayout.Label("流程");
            if (GUILayout.Button("结束当前波（正常结算进商店）"))
            {
                if (gm.Director != null && gm.Director.CurrentWave >= GameDatabase.WaveConfig.waveCount)
                    Tell("最终波必须击败 Boss 才能结束");
                else Tell(gm.Director != null && gm.Director.ForceEndWaveForTesting() ? "当前波已结束" : "当前不在战斗波次");
            }
            GUILayout.EndScrollView();
            if (Time.unscaledTime < _messageUntil) GUILayout.Label(_message);
            GUILayout.EndArea();
        }

        private void SpawnSelectedEnemy(GameManager gm)
        {
            if (_enemyIds.Count == 0 || gm.Player == null || gm.Director == null) { Tell("当前没有可生成的敌人"); return; }
            Vector2 offset = Random.insideUnitCircle.normalized * 2.5f;
            var enemy = EnemyPool.Spawn(_enemyIds[_enemyIndex], gm.Player.transform.position + (Vector3)offset,
                gm.Director.HpScale(), gm.Director.DmgScale(), gm.Director.SpeedScale());
            Tell(enemy != null ? "敌人已生成" : "敌人生成失败");
        }

        private void AdjustStat(GameManager gm, float sign)
        {
            if (gm.Player == null || gm.Player.Stats == null || !float.TryParse(_statInput, out float amount))
            { Tell("请输入有效数值并确保玩家已生成"); return; }
            var type = EditableStats[_statIndex];
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
        private static string EnemyName(string id)
        { var d = GameDatabase.GetEnemy(id); return d != null && !string.IsNullOrEmpty(d.displayName) ? d.displayName : id; }
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

        private static void Cycle(List<string> values, ref int index, int delta)
        {
            if (values.Count == 0) return;
            index = (index + delta + values.Count) % values.Count;
        }

        private void Tell(string message)
        {
            _message = message;
            _messageUntil = Time.unscaledTime + 2.5f;
        }
    }
}
