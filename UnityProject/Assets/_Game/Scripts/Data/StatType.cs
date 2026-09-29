using System;

namespace RogueLike.Data
{
    /// <summary>属性类型。最终值 = (基础 + Σflat) × (1 + Σpercent)。</summary>
    public enum StatType
    {
        MaxHp = 0,
        HpRegen = 1,
        Armor = 2,
        DodgeChance = 3,
        DamageMult = 4,      // 乘法区间，1 = 100%
        AttackSpeedMult = 5, // 乘法区间，1 = 100%
        CritChance = 6,      // 0–1
        CritDamageMult = 7,  // 乘法区间，2 = 200%
        RangeMult = 8,       // 乘法区间，1 = 100%
        MoveSpeedMult = 9,   // 乘法区间，1 = 100%
        LifeSteal = 10,      // 0–1，造成伤害回血比例
        Luck = 11,           // 点数，影响商店稀有度权重
        PickupRange = 12,    // 单位，材料磁吸半径
        Knockback = 13       // 击退强度；默认 0，仅由购买的道具提供
    }

    /// <summary>一条属性修正：flat 加算、percent 乘算（同属性 percent 内部相加后一次乘）。</summary>
    [Serializable]
    public class StatModifier
    {
        public StatType type;
        public float flat;
        public float percent;

        public StatModifier() { }

        public StatModifier(StatType t, float f, float p = 0f)
        {
            type = t;
            flat = f;
            percent = p;
        }
    }
}
