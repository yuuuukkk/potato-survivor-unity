using System;
using System.Collections.Generic;
using RogueLike.Data;
using UnityEditor;
using UnityEngine;

namespace RogueLike.EditorTools
{
    /// <summary>将项目内的正式图片自动接到美术库与对应 Prefab；已有手工配置不覆盖。</summary>
    [InitializeOnLoad]
    public static class BindBundledArt
    {
        private const string ArtRoot = "Assets/_Game/Resources/Art/";
        private const string PrefabRoot = "Assets/_Game/Resources/Prefabs/";
        private static bool _binding;

        static BindBundledArt()
        {
            EditorApplication.delayCall += BindWhenReady;
        }

        private static void BindWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                return;
            }
            BindMissing();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall += BindWhenReady;
        }

        [MenuItem("土豆幸存者/自动绑定项目美术")]
        public static void BindMissing()
        {
            if (_binding || EditorApplication.isPlayingOrWillChangePlaymode) return;
            _binding = true;
            try
            {
                var library = AssetDatabase.LoadAssetAtPath<GameArtLibrary>(
                    "Assets/_Game/Resources/Data/GameArtLibrary.asset");
                if (library == null) return;

                int bound = 0;
                bound += BindEntries(library.characters, "Characters/character_", null);
                bound += BindEntries(library.enemies, "Enemies/enemy_", "EnemyPool/enemy_");
                bound += BindEntries(library.weapons, "Weapons/weapon_", null);
                bound += BindEntries(library.projectiles, "Projectiles/projectile_", "WeaponPool/bullet_");

                var map = Load("Map/map_arena");
                if (library.mapBackground == null && map != null)
                {
                    library.mapBackground = map;
                    bound++;
                }
                bound += BindPrefab("Shared/arena_bg", map);

                // 此 Prefab 是默认农夫外观；选其他英雄时 PlayerController.Setup 会换成对应角色图。
                bound += BindPrefab("Shared/player", Load("Characters/character_farmer"));

                if (bound > 0)
                {
                    EditorUtility.SetDirty(library);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[BindBundledArt] 已自动绑定 {bound} 处图片引用。");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _binding = false;
            }
        }

        private static int BindEntries(List<GameArtLibrary.SpriteEntry> entries, string imagePrefix,
            string prefabPrefix)
        {
            if (entries == null) return 0;
            int bound = 0;
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                var sprite = Load(imagePrefix + entry.id);
                if (sprite == null) continue;
                if (entry.sprite == null)
                {
                    entry.sprite = sprite;
                    bound++;
                }
                if (prefabPrefix != null) bound += BindPrefab(prefabPrefix + entry.id, sprite);
            }
            return bound;
        }

        private static Sprite Load(string imagePath)
            => AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + imagePath + ".png");

        private static int BindPrefab(string prefabPath, Sprite sprite)
        {
            if (sprite == null) return 0;
            string path = PrefabRoot + prefabPath + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return 0;
            var renderer = prefab.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite != null) return 0;

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var editableRenderer = contents.GetComponent<SpriteRenderer>();
                if (editableRenderer == null || editableRenderer.sprite != null) return 0;
                editableRenderer.sprite = sprite;
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                return 1;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
