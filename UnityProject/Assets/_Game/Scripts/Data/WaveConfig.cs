using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueLike.Data
{
    /// <summary>升级可选的属性提升项（来自 waveconfig.json 的 upgradeStatOptions）。</summary>
    [Serializable]
    public class UpgradeStatOption
    {
        public string displayName;
        public StatType statType;
        public float flat;
        public float percent;
    }

    /// <summary>全局波次参数（数据驱动，来自 waveconfig.json）。</summary>
    [Serializable]
    public class WaveConfig
    {
        public int waveCount = 20;
        public float waveDuration = 30f;
        public float waveDurationStart = 20f;
        public float waveDurationPerWave = 5f;
        public float waveDurationCap = 60f;
        public float finalWaveDuration = 90f;

        // 生成预算：预算(w) = budgetBase + budgetPerWave × w
        public float budgetBase = 18f;
        public float budgetPerWave = 12f;
        [Range(0f, 2f)] public float spawnBudgetSustainFraction = 0.6f;

        // 敌人缩放：HP(w) = maxHp × hpGrowth^(w−1)，伤害 × dmgGrowth^(w−1)，移速 × (1 + speedGrowth×(w−1))
        public float hpGrowth = 1.22f;
        public float dmgGrowth = 1.15f;
        public float speedGrowth = 0.03f;

        // 生成节奏
        public float spawnIntervalBase = 1.2f;
        public float spawnIntervalMin = 0.35f;
        public int maxAliveBase = 20;
        public int maxAlivePerWave = 2;

        public int eliteStartWave = 3;
        public List<int> bossWaves = new List<int> { 20 };
        [Range(1, 2)] public int finalWaveBossCount = 1;

        // 经济
        public int waveBonusBase = 5;
        public int waveBonusPerWave = 3;
        public int refreshBase = 4;
        public float pricePerWave = 2f;

        // 旧 JSON 兼容字段；当前商店数量请在 GameBalanceSO.offerCount 修改。
        [HideInInspector] public int shopWeaponSlots = 3;
        [HideInInspector] public int shopItemSlots = 3;

        // 治疗
        public int healBase = 15;
        public int healPerWave = 5;

        // 武器升级（同名叠加）
        public int weaponMaxLevel = 4;
        public float upgradeDamagePerLevel = 0.5f;
        public float upgradeAttackSpeedPerLevel = 0.08f;
        public float upgradeDiscount = 0.8f;

        // 稀有度价格倍率 / 开放波次（下标 0–5 = 普通→红色）
        public List<float> rarityPriceMult = new List<float> { 1f, 1.8f, 3.2f, 5.5f, 9f, 16f };
        public List<int> rarityUnlockWave = new List<int> { 1, 1, 4, 7, 10, 14 };

        // 波次结算强化的白、绿、蓝、紫、金、红品阶权重；商店档位由下方 shopTier* 控制。
        public int shopLevelUpWave = 4;
        public List<float> rarityWeights = new List<float> { 50f, 30f, 15f, 4.5f, 0.8f, 0.1f };
        public float luckWeightBonus = 0.4f;
        // 商店档位 2–6：每波概率、首次开放波次与概率上限。
        // Values are percentages; Luck multiplies each chance by (1 + luck / 100).
        public List<float> shopTierChancePerWave = new List<float> { 0f, 6f, 2f, 0.23f, 0.12f, 0.55f };
        public List<int> shopTierFirstWave = new List<int> { 1, 2, 4, 8, 11, 12 };
        public List<float> shopTierChanceCap = new List<float> { 100f, 60f, 25f, 8f, 3f, 5f };

        // 经验 / 升级
        public int expBaseToNext = 10;
        public float expGrowth = 1.45f;
        public int upgradeStatWeight = 60;
        public int upgradeWeaponWeight = 25;
        public int upgradeItemWeight = 15;
        public List<UpgradeStatOption> upgradeStatOptions = new List<UpgradeStatOption>();
    }
}
