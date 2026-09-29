using System.Collections.Generic;
using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Player
{
    /// <summary>
    /// 属性聚合器：基础值（角色）+ 平铺修正（道具）+ 百分比修正。
    /// 最终值 = (基础 + Σflat) × (1 + Σpercent)；乘法型属性默认基数为 1。
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        private readonly Dictionary<StatType, float> _base = new Dictionary<StatType, float>();
        private readonly Dictionary<StatType, float> _flat = new Dictionary<StatType, float>();
        private readonly Dictionary<StatType, float> _pct = new Dictionary<StatType, float>();
        public CharacterData Character { get; private set; }

        public void Setup(CharacterData character)
        {
            Character = character;
            _base.Clear();
            _flat.Clear();
            _pct.Clear();

            // 默认值
            var balance = GameDatabase.Balance;
            _base[StatType.MaxHp] = balance != null ? balance.baseMaxHp : 100f;
            _base[StatType.CritDamageMult] = balance != null ? balance.baseCritDamageMultiplier : 2f;
            _base[StatType.DamageMult] = 1f;
            _base[StatType.AttackSpeedMult] = 1f;
            _base[StatType.RangeMult] = 1f;
            _base[StatType.MoveSpeedMult] = 1f;
            _base[StatType.PickupRange] = balance != null ? balance.basePickupRange : 1.6f;

            if (character != null)
            {
                foreach (var m in character.baseStats)
                {
                    if (m == null) continue;
                    _base[m.type] = _base.TryGetValue(m.type, out float b) ? b + m.flat : m.flat;
                    _pct[m.type] = _pct.TryGetValue(m.type, out float p) ? p + m.percent : m.percent;
                }
            }
        }

        public float Get(StatType type)
        {
            float b = _base.TryGetValue(type, out float bb) ? bb : 0f;
            float f = _flat.TryGetValue(type, out float ff) ? ff : 0f;
            float p = _pct.TryGetValue(type, out float pp) ? pp : 0f;
            return (b + f) * (1f + p);
        }

        public void AddModifier(StatModifier m)
        {
            if (m == null) return;
            _flat[m.type] = _flat.TryGetValue(m.type, out float f) ? f + m.flat : m.flat;
            _pct[m.type] = _pct.TryGetValue(m.type, out float p) ? p + m.percent : m.percent;
        }

        public void RemoveModifier(StatModifier m)
        {
            if (m == null) return;
            _flat[m.type] = (_flat.TryGetValue(m.type, out float f) ? f : 0f) - m.flat;
            _pct[m.type] = (_pct.TryGetValue(m.type, out float p) ? p : 0f) - m.percent;
        }

        public void Reset()
        {
            _base.Clear();
            _flat.Clear();
            _pct.Clear();
        }
    }
}
