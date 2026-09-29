using System;
using RogueLike.Data;
using UnityEditor;
using UnityEngine;

namespace RogueLike.EditorTools
{
    /// <summary>验证点击 Play 前所有正式美术都能按运行时路径读取。</summary>
    public static class AssetSmokeTest
    {
        private static readonly string[] Characters = { "farmer", "chili", "mushroom" };
        private static readonly string[] Enemies =
        {
            "chaser", "ranger", "exploder", "tank", "splitter", "elite_chaser", "boss1", "boss2"
        };
        private static readonly string[] Weapons =
        {
            "pistol", "smg", "shotgun", "sniper", "laser", "rocket",
            "sword", "fist", "boomerang", "drone", "minigun", "flamethrower"
        };

        [MenuItem("土豆幸存者/验证全部美术")]
        public static void RunFromMenu()
        {
            try
            {
                Validate();
                EditorUtility.DisplayDialog("美术验证", "39 张素材均可读取，尺寸正常，主场景已加入 Build Settings。", "好");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("美术验证失败", ex.Message, "好");
            }
        }

        public static void RunBatch()
        {
            try
            {
                BindBundledArt.BindMissing();
                Validate();
                Debug.Log("[AssetSmokeTest] PASS: 39/39 sprites and 23 prefabs bound correctly; build scene is enabled.");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static void Validate()
        {
            var library = Resources.Load<GameArtLibrary>("Data/GameArtLibrary");
            if (library == null) throw new InvalidOperationException("GameArtLibrary 未加载");
            foreach (string id in Characters)
            {
                var sprite = Check("Art/Characters/character_" + id, 1.4f, 1.75f);
                CheckBinding(library.Find(library.characters, id), sprite, "角色 " + id);
            }
            foreach (string id in Enemies)
            {
                var sprite = Check("Art/Enemies/enemy_" + id, 0.7f, 1.1f);
                CheckBinding(library.Find(library.enemies, id), sprite, "敌人 " + id);
                CheckPrefab("EnemyPool/enemy_" + id, sprite);
            }
            foreach (string id in Weapons)
            {
                var weapon = Check("Art/Weapons/weapon_" + id, 0.85f, 1.15f);
                var projectile = Check("Art/Projectiles/projectile_" + id, 0.12f, 0.3f);
                CheckBinding(library.Find(library.weapons, id), weapon, "武器 " + id);
                CheckBinding(library.Find(library.projectiles, id), projectile, "弹体 " + id);
                CheckPrefab("WeaponPool/bullet_" + id, projectile);
            }
            var map = Check("Art/Map/map_arena", 10f, 30f);
            CheckBinding(library.mapBackground, map, "地图");
            CheckPrefab("Shared/arena_bg", map);
            CheckPrefab("Shared/player", library.Find(library.characters, "farmer"));
            var experience = Check("Art/Effects/pickup_experience", 0.35f, 0.5f);
            CheckPrefab("Items/item_experience", experience);
            var theme = Resources.Load<RogueLike.Core.UIThemeSO>("Config/UITheme");
            if (theme == null) throw new InvalidOperationException("UITheme SO 未加载");
            var panel = Check("Art/UI/ui_panel_round", 0.9f, 1.1f);
            var button = Check("Art/UI/ui_button_round", 0.9f, 1.1f);
            if (panel.border.x <= 0f || button.border.x <= 0f)
                throw new InvalidOperationException("UI 圆角图未设置九宫格边框");
            if (theme.roundedPanel != panel || theme.roundedCard != panel || theme.roundedButton != button)
                throw new InvalidOperationException("UITheme 未绑定圆角图");

            bool hasScene = false;
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled && scene.path == "Assets/_Game/scene/rouge.unity") hasScene = true;
            if (!hasScene) throw new InvalidOperationException("主场景未启用：Assets/_Game/scene/rouge.unity");
        }

        private static Sprite Check(string path, float minSize, float maxSize)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("无法读取 Sprite: " + path);
            float size = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            if (size < minSize || size > maxSize)
                throw new InvalidOperationException($"Sprite 尺寸异常: {path}, {size:0.###} 世界单位");
            return sprite;
        }

        private static void CheckBinding(Sprite actual, Sprite expected, string label)
        {
            if (actual != expected) throw new InvalidOperationException(label + " 尚未绑定到美术库");
        }

        private static void CheckPrefab(string relativePath, Sprite expected)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/" + relativePath);
            var sprite = prefab != null ? prefab.GetComponent<SpriteRenderer>()?.sprite : null;
            if (sprite != expected)
                throw new InvalidOperationException("Prefab 未绑定: " + relativePath);
        }
    }
}
