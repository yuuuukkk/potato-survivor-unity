using System;
using UnityEngine;

namespace RogueLike.Data
{
    // A bounded, reusable wave modifier. AI may combine catalog entries but cannot set numbers.
    [Serializable]
    public class DirectorChallengeData
    {
        public string id;
        public string displayName;
        [TextArea(1, 3)] public string riskDescription;
        [Min(1)] public int minWave = 2;
        [Min(1f)] public float budgetMultiplier = 1f;
        [Range(0.5f, 1f)] public float spawnIntervalMultiplier = 1f;
        [Min(0)] public int maxAliveBonus;
        [Range(0, 2)] public int openingEliteCount;
        // Challenge-specific enemy pressure. Values are clamped again when used so edited assets stay safe.
        [Range(0.65f, 1.6f)] public float enemyHpMultiplier = 1f;
        [Range(0.75f, 1.5f)] public float enemyDamageMultiplier = 1f;
        [Range(0.75f, 1.35f)] public float enemySpeedMultiplier = 1f;
        public string focusedEnemyId;
        [Range(1f, 5f)] public float focusedEnemyWeightMultiplier = 1f;
        [Min(0)] public int rewardMaterials;

        public bool IsValid => !string.IsNullOrWhiteSpace(id) &&
                               !string.IsNullOrWhiteSpace(displayName) &&
                               budgetMultiplier >= 1f && spawnIntervalMultiplier > 0f;
    }

    [Serializable]
    public class DirectorChallengeTierData
    {
        public string displayName;
        [Min(1f)] public float budgetMultiplier = 1f;
        [Range(0.5f, 1f)] public float spawnIntervalMultiplier = 1f;
        [Min(0)] public int maxAliveBonus;
        [Min(0)] public int openingEliteBonus;
        [Min(0)] public int rewardMaterials;
    }
}
