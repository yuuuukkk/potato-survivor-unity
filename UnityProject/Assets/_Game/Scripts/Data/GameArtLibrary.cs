using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueLike.Data
{
    /// <summary>
    /// 全部美术引用的可视化入口。选中 Resources/Data/GameArtLibrary 后，
    /// 可在 Inspector 直接拖入图片，不需要改文件名、JSON 或运行时代码。
    /// </summary>
    [CreateAssetMenu(fileName = "GameArtLibrary", menuName = "土豆幸存者/美术配置")]
    public class GameArtLibrary : ScriptableObject
    {
        [Serializable]
        public class SpriteEntry
        {
            public string id;
            public Sprite sprite;
        }

        [Header("场景")]
        public Sprite mapBackground;

        [Header("按角色 id 配置")]
        public List<SpriteEntry> characters = new List<SpriteEntry>();

        [Header("按敌人 id 配置")]
        public List<SpriteEntry> enemies = new List<SpriteEntry>();

        [Header("按武器 id 配置（玩家身上的武器外观）")]
        public List<SpriteEntry> weapons = new List<SpriteEntry>();

        [Header("按武器 id 配置（飞行弹体）")]
        public List<SpriteEntry> projectiles = new List<SpriteEntry>();

        [Header("按道具 id 配置（商店与背包图标）")]
        public List<SpriteEntry> items = new List<SpriteEntry>();

        public Sprite Find(List<SpriteEntry> entries, string id)
        {
            if (entries == null || string.IsNullOrEmpty(id)) return null;
            var entry = entries.Find(x => x != null && x.id == id);
            return entry != null ? entry.sprite : null;
        }
    }
}
