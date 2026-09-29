using UnityEngine;

namespace RogueLike.Data
{
    /// <summary>
    /// 升级经验曲线配置（ScriptableObject）。
    /// 在 Project 窗口选中 Assets/_Game/Resources/Data/XpConfig.asset，Inspector 直接改数值即可，
    /// 无需改代码/JSON。公式：ExpToNext = expBaseToNext × expGrowth^(level−1)。
    /// </summary>
    [CreateAssetMenu(fileName = "XpConfig", menuName = "土豆幸存者/经验配置")]
    public class XpConfig : ScriptableObject
    {
        [Header("升级所需经验：ExpToNext = 基础值 × 成长系数^(等级-1)")]
        [Tooltip("第 1 级升到第 2 级所需经验")]
        public float expBaseToNext = 10f;

        [Tooltip("每升一级经验需求的成长倍率（>1 越来越难）")]
        public float expGrowth = 1.45f;

        [Tooltip("经验上限等级（达到后不再升级，仅用于预留）")]
        public int maxLevel = 99;
    }
}
