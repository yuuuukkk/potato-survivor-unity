using System;
using UnityEngine;

namespace RogueLike.Data
{
    /// <summary>敌人定义（数据驱动，来自 enemies.json）。behavior 见枚举对照表：0 追 / 1 远程 / 2 自爆 / 3 坦克 / 4 分裂 / 5 Boss。</summary>
    [Serializable]
    public class EnemyData
    {
        public string id;
        public string displayName;
        public int behavior;
        public bool isBoss;
        public bool isElite;

        public float maxHp;
        public float moveSpeed;
        public float contactDamage;

        // 远程
        public bool ranged;
        public float rangedDamage;
        public float rangedInterval;
        public float projectileSpeed;
        public int projectileCount;
        [Min(0f)] public float bossWarningRadiusMultiplier = 1.8f;
        // Boss専属 attack: 0 = locked ground slam, 1 = radial projectile burst.
        public int bossAttackPattern;
        [Min(0f)] public float bossImpactRadius = 1.8f;
        [Min(0f)] public float bossAttackDamage = 24f;

        // 自爆
        public float explodeRadius;

        // 分裂
        public int splitCount;
        public string splitEnemyId;

        // 掉落
        public int materialDropMin;
        public int materialDropMax;
        public int expReward; // 击杀经验（升级系统）

        // 表现与生成
        public string color = "#00FF88";
        public float radius = 0.45f;
        public int spawnWeight = 10;
    }
}
