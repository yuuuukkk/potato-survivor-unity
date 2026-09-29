using System;

namespace RogueLike.Data
{
    public enum WeaponModificationKind
    {
        MeleeAnimationOverride,
        MeleeWave,
        Ricochet,
        Piercing,
        EnlargedProjectile,
        CharmProjectile,
        BoomerangReturnSurge,
        VacuumRocket,
        SpringPunch,
        BoomerangCollector
    }

    /// <summary>闭集武器改造定义。AI 只能推荐 SO 中已有的 ID，不提供可执行逻辑。</summary>
    [Serializable]
    public class WeaponModificationData
    {
        public string id;
        public string displayName;
        public string description;
        public string aiHint;
        public string weaponId;
        public string exclusiveGroup = "attack-form";
        public WeaponModificationKind kind;
        public MeleeAnimationStyle meleeAnimation;
        public Rarity rarity = Rarity.Uncommon;
        public int price;
        [UnityEngine.Min(1)] public int maxLevel = 3;
        [UnityEngine.Min(0f)] public float upgradePriceMultiplier = 0.65f;
        [UnityEngine.Min(0f)] public float damageBonusPerLevel = 0.08f;
        [UnityEngine.Min(0f)] public float extraBouncesPerLevel = 1f;
        [UnityEngine.Range(0f, 1f)] public float charmChancePerLevel = 0.08f;
        [UnityEngine.Min(0f)] public float charmDurationPerLevel = 0.4f;
        [UnityEngine.Min(0f)] public float projectileSizePerLevel = 0.15f;
        [UnityEngine.Range(0.2f, 2f)] public float damageMultiplier = 1f;
        [UnityEngine.Range(0.5f, 2f)] public float attackIntervalMultiplier = 1f;
        [UnityEngine.Range(0.1f, 2f)] public float waveDamageMultiplier = 0.6f;
        [UnityEngine.Min(1f)] public float waveSpeed = 9f;
        [UnityEngine.Min(0.5f)] public float waveRange = 5f;
        [UnityEngine.Range(0, 8)] public int wavePierce = 2;
        [UnityEngine.Range(0, 3)] public int extraBounces = 1;
        [UnityEngine.Range(0.1f, 1f)] public float bounceDamageMultiplier = 0.8f;
        [UnityEngine.Range(0, 8)] public int extraPierce = 1;
        [UnityEngine.Range(1f, 4f)] public float projectileRangeMultiplier = 1f;
        [UnityEngine.Range(1f, 3f)] public float projectileSizeMultiplier = 1f;
        [UnityEngine.Range(0f, 1f)] public float charmChance;
        [UnityEngine.Min(0.1f)] public float charmDuration = 2f;
        [UnityEngine.Min(0f)] public float charmDamagePerSecond = 4f;
        [UnityEngine.Range(1f, 3f)] public float returnDamageMultiplier = 1f;
        [UnityEngine.Range(1f, 3f)] public float returnSpeedMultiplier = 1f;
        [UnityEngine.Min(0)] public int vacuumAbsorbLimit = 2;
        [UnityEngine.Min(0)] public int vacuumAbsorbPerLevel = 1;
        [UnityEngine.Range(0f, 2f)] public float vacuumDamagePerAbsorb = 0.18f;
        [UnityEngine.Min(0f)] public float vacuumRadius = 0.65f;
        [UnityEngine.Min(0f)] public float springKnockback = 2.8f;
        [UnityEngine.Min(0f)] public float springKnockbackPerLevel = 0.7f;
        [UnityEngine.Range(0f, 2f)] public float springCollisionDamageMultiplier = 0.5f;
        [UnityEngine.Min(0.05f)] public float springCollisionWindow = 0.35f;
        [UnityEngine.Min(0f)] public float collectionRadius = 0.65f;
        [UnityEngine.Min(0f)] public float returnBonusPerPickup = 0.1f;
        [UnityEngine.Min(0f)] public float returnBonusPerPickupPerLevel = 0.05f;
        [UnityEngine.Min(1)] public int maxPickupBonusCount = 8;

        public bool IsUniversal => weaponId == "*";
        public int MaxLevel => maxLevel > 0 ? maxLevel : 3;
        public bool AppliesTo(WeaponData weapon) => weapon != null && (IsUniversal
            ? weapon.kind == WeaponKind.Projectile && weapon.attackPattern == WeaponAttackPattern.StandardProjectile
            : weapon.id == weaponId);

        public bool IsValid => !string.IsNullOrWhiteSpace(id) &&
                               !string.IsNullOrWhiteSpace(weaponId) &&
                               !string.IsNullOrWhiteSpace(exclusiveGroup);
    }
}
