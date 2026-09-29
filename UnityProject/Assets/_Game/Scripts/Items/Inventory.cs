using System.Collections.Generic;
using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Player;
using UnityEngine;

namespace RogueLike.Items
{
    /// <summary>
    /// 背包：道具栈 + 修正应用。道具购买时把 modifiers 推入 PlayerStats（正负同时生效），重买叠加。
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class Inventory : MonoBehaviour
    {
        public class ItemStack
        {
            public ItemData Data;
            public int Count;
        }

        private readonly List<ItemStack> _stacks = new List<ItemStack>();
        private PlayerStats _stats;

        public IReadOnlyList<ItemStack> Stacks => _stacks;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
        }

        public bool AddItem(ItemData data)
        {
            if (data == null || _stats == null) return false;
            var stack = _stacks.Find(s => s.Data.id == data.id);
            if (stack != null)
            {
                if (stack.Count >= data.maxStack) return false;
                stack.Count++;
            }
            else
            {
                stack = new ItemStack { Data = data, Count = 1 };
                _stacks.Add(stack);
            }
            ApplyMods(data, +1);
            RefreshHealthAfterStatChange();
            EventBus.RaiseInventoryChanged();
            return true;
        }

        public int GetCount(string itemId)
        {
            var s = _stacks.Find(x => x.Data.id == itemId);
            return s != null ? s.Count : 0;
        }

        public void Reset()
        {
            foreach (var s in _stacks) ApplyMods(s.Data, -1);
            _stacks.Clear();
            RefreshHealthAfterStatChange();
            EventBus.RaiseInventoryChanged();
        }

        private void ApplyMods(ItemData data, int sign)
        {
            foreach (var m in data.modifiers)
            {
                if (m == null) continue;
                var mod = new StatModifier(m.type, m.flat * sign, m.percent * sign);
                if (sign > 0) _stats.AddModifier(mod);
                else _stats.RemoveModifier(mod);
            }
        }

        private void RefreshHealthAfterStatChange()
        {
            var health = GetComponent<PlayerHealth>();
            if (health == null) return;
            health.RebuildMaxHp();
            EventBus.RaisePlayerDamaged(health.CurrentHp, health.MaxHp);
        }
    }
}
