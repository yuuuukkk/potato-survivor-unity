using System.Collections.Generic;
using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Combat
{
    /// <summary>
    /// 武器子弹专用对象池：按武器 id 分池，统一挂载到 ObjectPools/WeaponPool 节点下。
    /// 全游戏玩家子弹的唯一出处；换皮/换弹体改 BuildBulletGo 一处即可。
    /// </summary>
    public static class WeaponPool
    {
        private static readonly Dictionary<string, ObjectPool> Pools = new Dictionary<string, ObjectPool>();
        private static Transform _root;

        private static Transform Root => _root != null ? _root : (_root = PoolRoot.CreateChild("WeaponPool"));

        /// <summary>按武器数据取子弹池（同武器共享一个池，复用弹体预制）。</summary>
        public static ObjectPool Bullets(WeaponData data)
        {
            if (data == null) return null;
            if (!Pools.TryGetValue(data.id, out var pool))
            {
                pool = new ObjectPool(() => BuildBulletGo(data));
                Pools[data.id] = pool;
            }
            return pool;
        }

        /// <summary>预热指定武器的子弹池（开局/获得武器时调用）。</summary>
        public static void Prewarm(WeaponData data, int count)
        {
            var pool = Bullets(data);
            if (pool != null) pool.Prewarm(Mathf.Max(0, count - pool.AvailableCount));
        }

        /// <summary>销毁本池全部实例并清空（重开新局时调用）。</summary>
        public static void Clear()
        {
            foreach (var pool in Pools.Values) pool.ClearAll();
            Pools.Clear();
        }

        /// <summary>子弹"预制"：结构 + 占位美术 + 物理。优先加载 Resources/Prefabs/WeaponPool/bullet_{id}，无则运行时构建。</summary>
        private static GameObject BuildBulletGo(WeaponData data)
        {
            var prefab = PrefabProvider.Instantiate("WeaponPool/bullet_" + data.id, Root);
            if (prefab == null)
            {
                prefab = new GameObject("Bullet_" + data.id);
                prefab.transform.SetParent(Root);
                prefab.AddComponent<SpriteRenderer>();
                var collider = prefab.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                var body = prefab.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.isKinematic = true;
                prefab.AddComponent<Projectile>();
            }
            ConfigureVisual(prefab, data);
            return prefab;
        }

        private static void ConfigureVisual(GameObject bullet, WeaponData data)
        {
            var rootRenderer = bullet.GetComponent<SpriteRenderer>();
            var sprite = AssetLoader.LoadProjectileSprite(data.id);
            if (sprite == null && rootRenderer != null) sprite = rootRenderer.sprite;
            if (sprite == null) sprite = SpriteFactory.Circle(DataLoader.ParseColor(data.bulletColor), data.bulletRadius);
            if (rootRenderer != null) rootRenderer.enabled = false;

            var art = new GameObject("ProjectileArt");
            art.transform.SetParent(bullet.transform, false);
            var renderer = art.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 4;
            float visibleSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            Vector2 visibleCenter = Vector2.zero;
            var vertices = sprite.vertices;
            if (vertices != null && vertices.Length > 0)
            {
                float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                foreach (var v in vertices)
                {
                    minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                    minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                }
                visibleSize = Mathf.Max(maxX - minX, maxY - minY);
                visibleCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            }
            float desiredSize = data.projectileVisualSize > 0f ? data.projectileVisualSize : 0.38f;
            float scale = desiredSize / Mathf.Max(0.01f, visibleSize);
            art.transform.localScale = Vector3.one * scale;
            art.transform.localPosition = -(Vector3)(visibleCenter * scale);
            var collider = bullet.GetComponent<CircleCollider2D>();
            if (collider != null) collider.radius = Mathf.Max(0.04f, data.bulletRadius);
        }
    }
}
