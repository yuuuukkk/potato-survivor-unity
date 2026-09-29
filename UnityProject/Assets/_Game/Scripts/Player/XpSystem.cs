using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Player
{
    /// <summary>
    /// 经验系统（挂玩家）：击杀敌人获得经验，满则升级 → 广播 LevelUp（GameManager 暂停并弹三选一）。
    /// 升级曲线：ExpToNext = expBaseToNext × expGrowth^(level−1)，优先读 XpConfig(ScriptableObject)，
    /// 未指定时自动加载 Resources/Data/XpConfig.asset，找不到则回退内置默认值。
    /// </summary>
    public class XpSystem : MonoBehaviour
    {
        [Header("可选覆盖；默认从 GameSettingsSO 引用经验 SO")]
        public XpConfig Config;

        public int Level { get; private set; } = 1;
        public int Exp { get; private set; }
        public int ExpToNext { get; private set; }

        private XpConfig _cfg;
        private bool _cfgResolved;

        private void Awake()
        {
            Recalc();
        }

        public void Reset()
        {
            Level = 1;
            Exp = 0;
            Recalc();
        }

        private void EnsureConfig()
        {
            if (Config != null)
            {
                _cfg = Config;
                _cfgResolved = true;
                return;
            }
            if (_cfgResolved) return;
            var settings = GameConfig.Instance != null ? GameConfig.Instance.Settings : Resources.Load<GameSettingsSO>("Config/GameSettings");
            _cfg = settings != null ? settings.xpConfig : null;
            if (_cfg == null) _cfg = Resources.Load<XpConfig>("Data/XpConfig");
            _cfgResolved = true;
            if (_cfg == null)
            {
                var fb = ScriptableObject.CreateInstance<XpConfig>();
                fb.expBaseToNext = 10f;
                fb.expGrowth = 1.45f;
                _cfg = fb;
            }
        }

        private void Recalc()
        {
            EnsureConfig();
            float b = _cfg != null ? _cfg.expBaseToNext : 10f;
            float g = _cfg != null ? _cfg.expGrowth : 1.45f;
            ExpToNext = Mathf.Max(1, Mathf.RoundToInt(b * Mathf.Pow(g, Level - 1)));
        }

        public void AddExp(int amount)
        {
            if (amount <= 0) return;
            Exp += amount;
            var gainedLevels = new System.Collections.Generic.List<int>();
            while (Exp >= ExpToNext)
            {
                Exp -= ExpToNext;
                Level++;
                Recalc();
                gainedLevels.Add(Level);
            }
            EventBus.RaiseLevelChanged(Level, Exp, ExpToNext);
            foreach (int gainedLevel in gainedLevels) EventBus.RaiseLevelUp(gainedLevel);
        }

        /// <summary>GM 测试用：直接设置等级，不触发逐级三选一弹窗。</summary>
        public void SetLevelForTesting(int level)
        {
            Level = Mathf.Clamp(level, 1, 999);
            Exp = 0;
            Recalc();
            EventBus.RaiseLevelChanged(Level, Exp, ExpToNext);
        }
    }
}
