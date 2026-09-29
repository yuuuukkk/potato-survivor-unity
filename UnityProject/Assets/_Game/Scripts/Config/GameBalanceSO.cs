using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Core
{
    [CreateAssetMenu(fileName = "GameBalance", menuName = "土豆幸存者/配置/波次与商店")]
    public class GameBalanceSO : ScriptableObject
    {
        public WaveConfig waves = new WaveConfig();
        [Header("商店")]
        [Range(1, 6)] public int offerCount = 3;
        [Range(0f, 1f)] public float weaponOfferChance = 0.35f;
        [Range(0f, 1f)] public float modificationOfferChance = 0.3f;
        [Range(0f, 1f)] public float sellReturnRate = 0.5f;
        [HideInInspector] public int refreshCostPerUse = 2; // 旧存档兼容；现由下方刷新公式字段控制。
        [Range(0, 3)] public int earlyShopGuaranteedWeapons = 2;
        [Range(0, 3)] public int midShopGuaranteedWeapons = 1;
        [Range(0f, 1f)] public float sameWeaponOfferChance = 0.20f;
        [Range(0f, 1f)] public float sameKindOfferChance = 0.15f;
        [Range(0f, 1f)] public float earlySameKindBonus = 0.15f;
        [Range(0f, 1f)] public float earlySameKindBonusDecay = 0.03f;
        [Min(0f)] public float rerollBasePerWave = 0.75f;
        [Min(0f)] public float rerollIncreasePerWave = 0.40f;
        [Min(0f)] public float shopPriceFlatPerWave = 1f;
        [Min(0f)] public float shopPricePercentPerWave = 0.10f;
        [Header("局间挑战安全上限")]
        [Min(1f)] public float directorMaxBudgetMultiplier = 2.2f;
        [Min(0)] public int directorMaxAliveBonus = 12;
        [Min(0)] public int directorMaxOpeningElites = 2;
        [Min(1)] public int challengeShopInterval = 3;
        [Min(1)] public int firstChallengeAfterWave = 1;
        public DirectorChallengeTierData[] directorChallengeTiers = new DirectorChallengeTierData[4];
        [Header("画面与位置")]
        public int targetFrameRate = -1; // -1 = 不限制
        [Range(0, 4)] public int vSyncCount = 0;
        public float normalEnemyScale = 1.12f;
        public float heldWeaponDistanceBonus = 0.10f;
        [Header("弹道构筑")]
        [Range(1, 6)] public int maxEquippedModifications = 2;
        [Range(0, 8)] public int maxProjectileBounces = 3;
        [Min(0f)] public float ricochetExtraRangePerBounce = 1f;
        [Min(1f)] public float maxProjectileSizeMultiplier = 2f;
        [Header("魅惑行为")]
        public Color charmTint = new Color(1f, 0.55f, 0.86f, 1f);
        [Min(0.1f)] public float charmTargetRange = 12f;
        [Min(0.1f)] public float charmAttackInterval = 0.5f;
        [Min(0.05f)] public float charmRetargetInterval = 0.25f;
        [Header("玩家基础数值")]
        public bool showFpsOverlay = true;
        public float baseMaxHp = 100f;
        public float baseCritDamageMultiplier = 2f;
        public float basePickupRange = 1.6f;
        public float baseMoveSpeed = 3.5f;
        [Header("受击反馈")]
        [Range(0f, 1f)] public float screenShakeScale = 0.75f;
        public float playerHitShakeDuration = 0.17f;
        public float playerHitShakeMagnitude = 0.105f;
        public float playerHitFlashDuration = 0.22f;
        public Color playerHitTint = new Color(1f, 0.38f, 0.38f, 1f);
        public float playerHitRecoilSpeed = 2.6f;
        public float enemyHitFlashDuration = 0.12f;
        public Color enemyHitTint = new Color(1f, 0.55f, 0.35f, 1f);
        public float enemyHitSquash = 0.16f;
        public float enemyHitStunDuration = 0.055f;
        public float impactDuration = 0.18f;
        public float impactSize = 1f;
        public Color impactColor = new Color(1f, 0.86f, 0.47f, 1f);
        public float impactShakeMagnitude = 0.025f;
        public int impactMaxActive = 18;
        [Range(0, 100)] public int damageNumberMaxActive = 24;
        [Header("命中音效（留空时使用内置短音）")]
        public AudioClip enemyHitSound;
        public AudioClip criticalHitSound;
        public AudioClip playerHitSound;
        [Range(0f, 1f)] public float hitSoundVolume = 0.24f;
        [Range(0f, 1f)] public float playerHitSoundVolume = 0.33f;
        [Header("爆炸表现")]
        public float explosionVisualDuration = 0.28f;
        public Color explosionOuterColor = new Color(1f, 0.38f, 0.10f, 0.48f);
        public Color explosionInnerColor = new Color(1f, 0.87f, 0.55f, 0.75f);
        public float explosionShakeMagnitude = 0.09f;
        [Header("召唤物")]
        public int maxOrbits = 2;
        public int maxTurrets = 6;
    }
}
