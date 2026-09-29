using System;
using System.Collections.Generic;
using System.IO;
using RogueLike.Combat;
using RogueLike.Data;
using RogueLike.Enemies;
using RogueLike.Items;
using RogueLike.Player;
using UnityEditor;
using UnityEngine;

namespace RogueLike.EditorTools
{
    /// <summary>
    /// 一键生成全部占位预制到 Assets/_Game/Resources/Prefabs/。
    /// 生成后用户直接打开预制替换 SpriteRenderer 的 Sprite（或 Image）即可换素材；
    /// 未生成/未替换时游戏自动回退运行时占位色块。
    /// 菜单：土豆幸存者 / 生成占位预制
    /// </summary>
    public static class GeneratePrefabs
    {
        private const string PrefabRoot = "Assets/_Game/Resources/Prefabs";
        [MenuItem("土豆幸存者/生成占位预制")]
        public static void Run()
        {
            EnsureXpConfigAsset();

            // 武器子弹（每个武器一个弹体预制）
            foreach (var id in LoadIds("weapons.json", "weapons"))
                Build("WeaponPool", "bullet_" + id, typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D), typeof(Projectile));

            // 敌人（每个敌人一个预制）
            foreach (var id in LoadIds("enemies.json", "enemies"))
                Build("EnemyPool", "enemy_" + id, typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D), typeof(Enemy));

            // 战场通用对象
            Build("Shared", "enemy_bullet", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D), typeof(Projectile));
            Build("Items", "item_material", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D), typeof(ItemPickup));
            Build("Shared", "boss_warning", typeof(SpriteRenderer), typeof(BossWarning));
            Build("Shared", "orbit", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D), typeof(OrbitBody));
            Build("Shared", "turret", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(Turret));
            Build("Shared", "weapon_visual", typeof(SpriteRenderer));

            // 玩家（含全部脚本组件，直接改预制里的 Sprite 即可换角色皮肤）
            Build("Shared", "player",
                typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D),
                typeof(PlayerStats), typeof(PlayerHealth), typeof(WeaponSystem), typeof(Inventory), typeof(PlayerController));

            // 竞技场
            Build("Shared", "arena_bg", typeof(SpriteRenderer));
            Build("Shared", "arena_border", typeof(SpriteRenderer));

            AssetDatabase.SaveAssets();
            Debug.Log("[生成占位预制] 完成，共写入 " + PrefabRoot + " 目录。打开预制替换 Sprite 即可换素材。");
        }

        // ---------- 预制构建 ----------

        private static void Build(string folder, string name, params Type[] comps)
        {
            string dir = PrefabRoot + "/" + folder;
            Directory.CreateDirectory(dir);
            string path = dir + "/" + name + ".prefab";

            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            // 保持为空，让用户在 Prefab Inspector 中明确拖入自己的素材。
            // 运行时若仍为空，游戏代码才会生成简单几何占位，保证可测试。
            sr.sprite = null;
            foreach (var t in comps)
            {
                if (go.GetComponent(t) == null) go.AddComponent(t);
            }
            var col = go.GetComponent<CircleCollider2D>();
            if (col != null) col.isTrigger = true;
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.isKinematic = true;
            }
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>生成升级经验配置资产（用户可在 Inspector 直接改数值）。</summary>
        private static void EnsureXpConfigAsset()
        {
            string path = "Assets/_Game/Resources/Data/XpConfig.asset";
            if (File.Exists(path)) return;
            var cfg = ScriptableObject.CreateInstance<XpConfig>();
            cfg.expBaseToNext = 10f;
            cfg.expGrowth = 1.45f;
            AssetDatabase.CreateAsset(cfg, path);
            Debug.Log("[生成占位预制] 已生成经验配置资产 " + path + "（选中后可在 Inspector 直接改升级经验）");
        }

        // ---------- 从内容 JSON 读取 id 清单 ----------

        [Serializable]
        private class WeaponList { public List<WeaponData> weapons = new List<WeaponData>(); }
        [Serializable]
        private class EnemyList { public List<EnemyData> enemies = new List<EnemyData>(); }

        private static List<string> LoadIds(string file, string field)
        {
            var ids = new List<string>();
            string path = "Assets/_Game/Resources/Data/" + file;
            if (!File.Exists(path)) return ids;
            string json = File.ReadAllText(path);
            try
            {
                if (field == "weapons")
                {
                    var list = JsonUtility.FromJson<WeaponList>(json);
                    foreach (var w in list.weapons) if (!string.IsNullOrEmpty(w.id)) ids.Add(w.id);
                }
                else
                {
                    var list = JsonUtility.FromJson<EnemyList>(json);
                    foreach (var e in list.enemies) if (!string.IsNullOrEmpty(e.id)) ids.Add(e.id);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[生成占位预制] 解析 " + file + " 失败: " + ex.Message);
            }
            return ids;
        }
    }
}
