using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueLike.Data
{
    [Serializable]
    public class WeaponEntry
    {
        public string weaponId;
        public int level = 1;
    }

    /// <summary>角色定义。运行时优先从 CharacterRosterSO 读取。</summary>
    [Serializable]
    public class CharacterData
    {
        public string id;
        public string displayName;
        public string description;
        public string color = "#FFFFFF";
        public int maxWeaponSlots = 6;
        public int startMaterials = 0;
        public List<StatModifier> baseStats = new List<StatModifier>();
        public List<WeaponEntry> startingWeapons = new List<WeaponEntry>();
        [Tooltip("留空代表可使用全部类型；辣椒只配置 Melee。")]
        public List<WeaponKind> allowedWeaponKinds = new List<WeaponKind>();
        public float projectileDamageMultiplier = 1f;

        public bool Allows(WeaponKind kind)
            => allowedWeaponKinds == null || allowedWeaponKinds.Count == 0 || allowedWeaponKinds.Contains(kind);
    }
}
