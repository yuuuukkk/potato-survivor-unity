using System;
using System.Collections.Generic;

namespace RogueLike.Data
{
    /// <summary>道具定义（数据驱动，来自 items.json）。modifiers 正负均可，作为一组属性修正叠加。</summary>
    [Serializable]
    public class ItemData
    {
        public string id;
        public string displayName;
        public string description;
        public string iconId; // 留空时使用自己的 id；可复用现有道具图标
        public Rarity rarity;
        public float basePrice;
        public int maxStack = 5;
        public bool isConsumable; // 消耗品：购买立即生效（如急救包），不占背包
        public int healAmount;    // 消耗品生效值（急救包回复 HP）
        public bool requiresProjectileWeapon;
        public List<StatModifier> modifiers = new List<StatModifier>();
        public string IconId => string.IsNullOrWhiteSpace(iconId) ? id : iconId;
    }
}
