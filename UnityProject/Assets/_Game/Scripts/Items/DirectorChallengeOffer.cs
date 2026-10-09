using RogueLike.Data;
using RogueLike.Core;
using UnityEngine;

namespace RogueLike.Items
{
    public sealed class DirectorChallengeOffer
    {
        public readonly DirectorChallengeData Modifier;
        public readonly DirectorChallengeTierData Tier;
        public readonly int TierIndex;
        public readonly string Title;

        public DirectorChallengeOffer(DirectorChallengeData modifier, DirectorChallengeTierData tier,
            int tierIndex, string title = null)
        {
            Modifier = modifier;
            Tier = tier;
            TierIndex = tierIndex;
            Title = string.IsNullOrWhiteSpace(title)
                ? modifier.displayName
                : title.Trim();
        }

        public int Reward => Mathf.Max(0, Tier.rewardMaterials) + Mathf.Max(0, Modifier.rewardMaterials);
        public string Risk
        {
            get
            {
                var balance = GameDatabase.Balance;
                float budget = Mathf.Min(balance != null ? Mathf.Max(1f, balance.directorMaxBudgetMultiplier) : 2.2f,
                    Mathf.Max(1f, Tier.budgetMultiplier) * Mathf.Max(1f, Modifier.budgetMultiplier));
                float interval = Mathf.Clamp(Tier.spawnIntervalMultiplier, 0.5f, 1f) *
                    Mathf.Clamp(Modifier.spawnIntervalMultiplier, 0.5f, 1f);
                int alive = Mathf.Min(balance != null ? Mathf.Max(0, balance.directorMaxAliveBonus) : 12,
                    Tier.maxAliveBonus + Modifier.maxAliveBonus);
                int elites = Mathf.Min(balance != null ? Mathf.Max(0, balance.directorMaxOpeningElites) : 2,
                    Tier.openingEliteBonus + Modifier.openingEliteCount);
                string pressure = $"敌量×{budget:0.##} · 间隔×{interval:0.##} · 同屏+{alive}" +
                    (elites > 0 ? $" · 精英+{elites}" : "");
                string special = string.Empty;
                float hp = SafeMultiplier(Modifier.enemyHpMultiplier, 0.65f, 1.6f);
                float damage = SafeMultiplier(Modifier.enemyDamageMultiplier, 0.75f, 1.5f);
                float speed = SafeMultiplier(Modifier.enemySpeedMultiplier, 0.75f, 1.35f);
                if (!Mathf.Approximately(hp, 1f)) special += $"生命×{hp:0.##} ";
                if (!Mathf.Approximately(damage, 1f)) special += $"伤害×{damage:0.##} ";
                if (!Mathf.Approximately(speed, 1f)) special += $"移速×{speed:0.##} ";
                if (!string.IsNullOrWhiteSpace(Modifier.focusedEnemyId))
                {
                    var enemy = GameDatabase.GetEnemy(Modifier.focusedEnemyId);
                    string name = enemy != null ? enemy.displayName : Modifier.focusedEnemyId;
                    special += $"{name}权重×{SafeMultiplier(Modifier.focusedEnemyWeightMultiplier, 1f, 5f):0.##}";
                }
                return pressure + "\n" + (string.IsNullOrWhiteSpace(special) ? Modifier.riskDescription : special.Trim());
            }
        }
        private static float SafeMultiplier(float value, float minimum, float maximum) =>
            value > 0f ? Mathf.Clamp(value, minimum, maximum) : 1f;
        public string Key => TierIndex + ":" + Modifier.id;
    }
}
