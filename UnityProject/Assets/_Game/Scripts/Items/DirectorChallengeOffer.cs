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
                return $"敌量×{budget:0.##} · 出怪间隔×{interval:0.##} · 同屏+{alive}" +
                    (elites > 0 ? $" · 精英+{elites}" : "") + "\n" + Modifier.riskDescription;
            }
        }
        public string Key => TierIndex + ":" + Modifier.id;
    }
}
