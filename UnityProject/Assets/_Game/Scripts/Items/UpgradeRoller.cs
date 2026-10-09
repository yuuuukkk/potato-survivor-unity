using System.Collections.Generic;
using System.Globalization;
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
        // 与 Rarity 的白、绿、蓝、紫、金、红顺序对应。基础数值从配置读取，品阶只放大本次强化。
        private static readonly float[] StatTierMultipliers = { 1f, 1.5f, 2f, 2.5f, 3f, 4f };
        private static readonly float[] DefaultTierWeights = { 50f, 30f, 15f, 4.5f, 0.8f, 0.1f };

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
                opts.Add(MakeStatOption(source));
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
            return MakeStatOption(o);
        }

        private static UpgradeOption MakeStatOption(UpgradeStatOption source)
        {
            Rarity rarity = RollStatRarity();
            float multiplier = StatTierMultipliers[(int)rarity];
            string originalAmount = source.displayName.Substring(source.displayName.LastIndexOf(' ') + 1);
            bool isPercent = originalAmount.Contains("%");
            float flat = source.flat * multiplier;
            float percent = source.percent * multiplier;
            // 展示值与实际生效值使用相同精度，避免卡片写 +5% 而实际得到 +4.5%。
            if (isPercent)
            {
                flat = Mathf.Round(flat * 1000f) / 1000f;
                percent = Mathf.Round(percent * 1000f) / 1000f;
            }
            else
            {
                flat = Mathf.Round(flat * 10f) / 10f;
                percent = Mathf.Round(percent * 10f) / 10f;
            }

            string title = FormatStatTitle(source.displayName, flat, percent, isPercent);
            return new UpgradeOption
            {
                Kind = UpgradeKind.Stat,
                Mod = new StatModifier(source.statType, flat, percent),
                Title = title,
                Description = string.Empty,
                Rarity = rarity
            };
        }

        private static Rarity RollStatRarity()
        {
            var config = GameDatabase.WaveConfig;
            var weights = config != null ? config.rarityWeights : null;
            int wave = GameManager.Instance != null ? GameManager.Instance.Wave : 1;
            float total = 0f;
            for (int tier = 0; tier <= (int)Rarity.Red; tier++)
            {
                if (wave < RarityInfo.UnlockWave((Rarity)tier)) continue;
                total += Mathf.Max(0f, weights != null && tier < weights.Count
                    ? weights[tier] : DefaultTierWeights[tier]);
            }
            if (total <= 0f) return Rarity.Common;

            float roll = Random.value * total;
            for (int tier = 0; tier <= (int)Rarity.Red; tier++)
            {
                if (wave < RarityInfo.UnlockWave((Rarity)tier)) continue;
                roll -= Mathf.Max(0f, weights != null && tier < weights.Count
                    ? weights[tier] : DefaultTierWeights[tier]);
                if (roll <= 0f) return (Rarity)tier;
            }
            return Rarity.Common;
        }

        private static string FormatStatTitle(string displayName, float flat, float percent, bool isPercent)
        {
            int split = displayName.LastIndexOf(' ');
            string name = split > 0 ? displayName.Substring(0, split) : displayName;
            string originalAmount = split > 0 ? displayName.Substring(split + 1) : string.Empty;
            float value = Mathf.Abs(percent) > 0f ? percent : flat;
            if (isPercent) value *= 100f;
            string sign = value < 0f ? "-" : "+";
            string unit = isPercent ? "%" : originalAmount.Contains("/")
                ? originalAmount.Substring(originalAmount.IndexOf('/')) : string.Empty;
            return name + " " + sign + Mathf.Abs(value).ToString("0.#", CultureInfo.InvariantCulture) + unit;
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
