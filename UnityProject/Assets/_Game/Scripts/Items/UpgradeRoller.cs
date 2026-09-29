using System.Collections.Generic;
using RogueLike.Combat;
using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Player;
using UnityEngine;

namespace RogueLike.Items
{
    /// <summary>升级选项类型。</summary>
    public enum UpgradeKind
    {
        Stat,   // 属性提升（永久）
        Weapon, // 获得新武器
        Item    // 获得道具
    }

    /// <summary>一次升级的一个可选奖励。</summary>
    public class UpgradeOption
    {
        public UpgradeKind Kind;
        public StatModifier Mod;   // Kind=Stat
        public WeaponData Weapon;  // Kind=Weapon
        public ItemData Item;      // Kind=Item
        public string Title;
        public string Description;
        public Rarity Rarity;
    }

    /// <summary>升级时提供四个互不重复的属性选项；武器与道具由商店构筑。</summary>
    public static class UpgradeRoller
    {
        public static List<UpgradeOption> Roll(int count = 4)
        {
            var opts = new List<UpgradeOption>();
            var used = new HashSet<string>();
            var cfg = GameDatabase.WaveConfig;
            var pool = cfg != null ? cfg.upgradeStatOptions : null;
            if (pool == null || pool.Count == 0) return opts;
            var indices = new List<int>();
            for (int i = 0; i < pool.Count; i++) indices.Add(i);
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }
            for (int i = 0; i < Mathf.Min(count, indices.Count); i++)
            {
                var source = pool[indices[i]];
                if (source == null || string.IsNullOrEmpty(source.displayName) || !used.Add(source.displayName)) continue;
                opts.Add(new UpgradeOption
                {
                    Kind = UpgradeKind.Stat,
                    Mod = new StatModifier(source.statType, source.flat, source.percent),
                    Title = source.displayName,
                    Description = StatDesc(source),
                    Rarity = Rarity.Common
                });
            }
            return opts;
        }

        private static UpgradeOption MakeUnique(HashSet<string> used)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var opt = Make();
                if (opt == null || used.Contains(opt.Title)) continue;
                return opt;
            }
            return MakeStat(); // 兜底：必出属性
        }

        private static UpgradeOption Make()
        {
            var cfg = GameDatabase.WaveConfig;
            if (cfg == null) return MakeStat();

            var gm0 = GameManager.Instance;
            var ws = gm0 != null && gm0.Player != null ? gm0.Player.GetComponent<WeaponSystem>() : null;
            bool weaponFull = ws == null || !HasAvailableWeapon(ws);
            int statW = cfg.upgradeStatWeight;
            int weaponW = weaponFull ? 0 : cfg.upgradeWeaponWeight;
            int itemW = cfg.upgradeItemWeight;
            int total = statW + weaponW + itemW;
            if (total <= 0) return MakeStat();

            int roll = Random.Range(0, total);
            if (roll < statW) return MakeStat();
            if (roll < statW + weaponW)
            {
                var w = MakeWeapon();
                if (w != null) return w;
                return MakeStat();
            }
            var it = MakeItem();
            return it != null ? it : MakeStat();
        }

        private static UpgradeOption MakeStat()
        {
            var cfg = GameDatabase.WaveConfig;
            var pool = cfg != null && cfg.upgradeStatOptions != null && cfg.upgradeStatOptions.Count > 0
                ? cfg.upgradeStatOptions : null;
            if (pool == null) return null;
            var o = pool[Random.Range(0, pool.Count)];
            return new UpgradeOption
            {
                Kind = UpgradeKind.Stat,
                Mod = new StatModifier(o.statType, o.flat, o.percent),
                Title = o.displayName,
                Description = StatDesc(o),
                Rarity = Rarity.Common
            };
        }

        private static string StatDesc(UpgradeStatOption o)
        {
            if (o.percent > 0f) return $"属性提升：{o.displayName}（永久）";
            return $"属性提升：{o.displayName}（永久）";
        }

        private static UpgradeOption MakeWeapon()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return null;
            var ws = gm.Player.GetComponent<WeaponSystem>();
            if (ws == null) return null;

            var cand = new List<WeaponData>();
            foreach (var w in GameDatabase.Weapons.Values)
                if (ws.CanAcquire(w, 1)) cand.Add(w);
            if (cand.Count == 0) return null;

            var pick = cand[Random.Range(0, cand.Count)];
            return new UpgradeOption
            {
                Kind = UpgradeKind.Weapon,
                Weapon = pick,
                Title = pick.displayName,
                Description = "新武器：" + pick.description,
                Rarity = pick.rarity
            };
        }

        private static bool HasAvailableWeapon(WeaponSystem ws)
        {
            foreach (var weapon in GameDatabase.Weapons.Values)
                if (ws.CanAcquire(weapon, 1)) return true;
            return false;
        }

        private static UpgradeOption MakeItem()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return null;
            var inv = gm.Player.GetComponent<Inventory>();
            if (inv == null) return null;
            var ws = gm.Player.GetComponent<RogueLike.Combat.WeaponSystem>();

            var cand = new List<ItemData>();
            foreach (var it in GameDatabase.Items.Values)
            {
                if (it.isConsumable) continue; // 消耗品不进升级池
                if (it.requiresProjectileWeapon && (ws == null || !ws.HasStandardProjectileWeapon)) continue;
                if (inv.GetCount(it.id) >= it.maxStack) continue;
                cand.Add(it);
            }
            if (cand.Count == 0) return null;

            var pick = cand[Random.Range(0, cand.Count)];
            return new UpgradeOption
            {
                Kind = UpgradeKind.Item,
                Item = pick,
                Title = pick.displayName,
                Description = pick.description,
                Rarity = pick.rarity
            };
        }
    }
}
