using System;
using UnityEngine;

namespace RogueLike.Data
{
    public enum MeleeAnimationStyle { Swing, Thrust }
    public enum WeaponAttackPattern { StandardProjectile, FlameCone, LaserBeam }

    /// <summary>武器定义（数据驱动，来自 weapons.json）。</summary>
    [Serializable]
    public class WeaponData
    {
        public string id;
        public string displayName;
        public string description;
        public WeaponKind kind;
        public Rarity rarity;

        public float basePrice;

        // 基础战斗数值
        public float damage;
        public float attackInterval;      // 秒/次
        public int projectileCount;       // 弹数
        public float spreadDeg;           // 散射角（度）
        public float projectileSpeed;     // 子弹速度（单位/秒）
        public float range;               // 射程（单位）
        public int pierce;                // 穿透数
        public float critChance;          // 0–1
        public float critDamageMult;      // 默认 2
        public float explodeRadius;       // 0 = 无爆炸

        // 表现
        public string bulletColor = "#FFFFFF";
        public float bulletRadius = 0.12f;
        public WeaponAttackPattern attackPattern;
        [Range(10f, 120f)] public float flameConeAngle = 48f;
        [Min(0.03f)] public float laserBeamWidth = 0.22f;
        [Min(0.03f)] public float patternEffectDuration = 0.16f;

        // 近战表现：每把武器都可在 GameContent SO 单独配置。
        public MeleeAnimationStyle meleeAnimation;
        public float meleeAnimationDuration = 0.24f;
        public float meleeSwingDegrees = 105f;
        public float meleeThrustDistance = 0.55f;
        public float meleeSwingOutwardDistance = 0.35f;
        [Min(0.05f)] public float meleeHitWidth = 0.28f;
        [Range(0f, 1f)] public float meleeHandleFraction = 0.3f;
        [Range(-0.5f, 0.5f)] public float meleeContactFraction = 0.5f;
        [Range(0f, 1f)] public float meleeActiveStart = 0.22f;
        [Range(0f, 1f)] public float meleeActiveEnd = 0.82f;
        public float targetingRangeBonus = 0f; // 远程索敌宽限；近战按实际攻击轨迹索敌，不使用此值
        public float projectileVisualSize = 0.38f; // 世界单位；只缩放图片，不改变碰撞半径
        public float heldVisualSize = 1f; // 手持武器可见部分的最长边（世界单位）
        public float heldDistance = 0.12f; // 在原武器锚点基础上向外移动的距离
        public int projectilePrewarmCount = 12; // 取得武器时预建弹体，避免战斗首发卡顿
        public float droneOrbitRadius = 1.05f;
        public float droneOrbitSpeed = 120f;
        public float droneVisualSize = 0.9f;
    }
}
