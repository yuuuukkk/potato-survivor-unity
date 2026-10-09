using UnityEngine;

namespace RogueLike.Combat
{
    /// <summary>统一伤害入参。</summary>
    public struct DamageInfo
    {
        public float amount;
        public GameObject source;
        public bool isCrit;
        public float knockback;
        public float impact; // 纯视觉反馈强度，不产生位移
        public WeaponInstance weapon; // 击杀归属，用于逐把武器的成长/掉落构筑

        public DamageInfo(float amount, GameObject source, bool isCrit = false,
            float knockback = 0f, float impact = 0f, WeaponInstance weapon = null)
        {
            this.amount = amount;
            this.source = source;
            this.isCrit = isCrit;
            this.knockback = knockback;
            this.impact = impact;
            this.weapon = weapon;
        }
    }

    /// <summary>可受击对象（玩家与敌人都实现）。</summary>
    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
    }
}
