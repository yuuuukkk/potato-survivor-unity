using System.Collections.Generic;
using UnityEngine;
using RogueLike.Data;

namespace RogueLike.Core
{
    /// <summary>
    /// 优先从 GameArtLibrary 读取显式配置的 Sprite；未配置时按资源命名读取。
    /// 用户可在 Inspector 中覆盖默认美术；确实缺图时由 Prefab 或几何占位兜底。
    /// </summary>
    public static class AssetLoader
    {
        private static GameArtLibrary _library;
        private static readonly Dictionary<string, Sprite> RuntimeSprites = new Dictionary<string, Sprite>();
        private static GameArtLibrary Library => _library != null
            ? _library
            : (_library = Resources.Load<GameArtLibrary>("Data/GameArtLibrary"));

        private static Sprite LoadNamed(string resourcePath)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null) return sprite;

            if (RuntimeSprites.TryGetValue(resourcePath, out sprite)) return sprite;

            // 即使图片尚未被 Unity 设成 Sprite，也能直接点击 Play 使用。
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            RuntimeSprites[resourcePath] = sprite;
            return sprite;
        }

        public static Sprite LoadEnemySprite(string id)
            => (Library != null ? Library.Find(Library.enemies, id) : null)
               ?? LoadNamed("Art/Enemies/enemy_" + id);

        public static Sprite LoadWeaponSprite(string id)
            => (Library != null ? Library.Find(Library.weapons, id) : null)
               ?? LoadNamed("Art/Weapons/weapon_" + id);

        public static Sprite LoadProjectileSprite(string id)
            => (Library != null ? Library.Find(Library.projectiles, id) : null)
               ?? LoadNamed("Art/Projectiles/projectile_" + id);

        public static Sprite LoadItemSprite(string id)
            => (Library != null ? Library.Find(Library.items, id) : null)
               ?? LoadNamed("Art/Items/item_" + id);

        public static Sprite LoadPlayerSprite(string charId)
            => (Library != null ? Library.Find(Library.characters, charId) : null)
               ?? LoadNamed("Art/Characters/character_" + charId);

        public static Sprite LoadMapSprite()
            => (Library != null ? Library.mapBackground : null)
               ?? LoadNamed("Art/Map/map_arena");

        public static Sprite LoadExperienceSprite()
            => LoadNamed("Art/Effects/pickup_experience");

        public static Sprite LoadMaterialPickupSprite()
            => LoadNamed("Art/Effects/pickup_material");

        public static Sprite LoadHealingPickupSprite()
            => LoadNamed("Art/Effects/pickup_healing_seed");

        public static Sprite LoadBuildEffectSprite(string id)
            => LoadNamed("Art/Effects/build_" + id);
    }
}
