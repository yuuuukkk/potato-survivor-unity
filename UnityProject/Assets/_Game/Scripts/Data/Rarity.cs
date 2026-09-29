using UnityEngine;

namespace RogueLike.Data
{
    public enum Rarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
        Red = 5
    }

    public static class RarityInfo
    {
        public static Color Color(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return new Color(0.85f, 0.85f, 0.85f);
                case Rarity.Uncommon: return new Color(0.45f, 0.85f, 0.45f);
                case Rarity.Rare: return new Color(0.45f, 0.65f, 1.00f);
                case Rarity.Epic: return new Color(0.75f, 0.45f, 1.00f);
                case Rarity.Legendary: return new Color(1.00f, 0.75f, 0.25f);
                default: return new Color(1.00f, 0.25f, 0.28f);
            }
        }

        public static float PriceMult(Rarity r)
        {
            var cfg = GameDatabase.WaveConfig;
            if (cfg != null && cfg.rarityPriceMult != null && (int)r < cfg.rarityPriceMult.Count)
                return cfg.rarityPriceMult[(int)r];
            return 1.8f;
        }

        /// <summary>稀有度首次开放波次（波次 < 值则权重为 0）。</summary>
        public static int UnlockWave(Rarity r)
        {
            var cfg = GameDatabase.WaveConfig;
            if (cfg != null && cfg.rarityUnlockWave != null && (int)r < cfg.rarityUnlockWave.Count)
                return cfg.rarityUnlockWave[(int)r];
            return 1;
        }
    }
}
