using System.Collections.Generic;
using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Items
{
    /// <summary>
    /// 掉落物专用对象池（与子弹 WeaponPool / 敌人 EnemyPool 完全独立）：
    /// 按掉落物 id 分池，统一挂载到 ObjectPools/ItemPool 节点下。
    /// 材料和经验按 id 分池；优先读取 Resources/Prefabs/Items/item_*，缺失时才运行时兜底。
    /// </summary>
    public static class ItemPool
    {
        private static readonly Dictionary<string, ObjectPool> Pools = new Dictionary<string, ObjectPool>();
        private static readonly HashSet<ItemPickup> ActivePickups = new HashSet<ItemPickup>();
        private static Transform _root;

        private static Transform Root => _root != null ? _root : (_root = PoolRoot.CreateChild("ItemPool"));

        /// <summary>生成材料掉落物。</summary>
        public static void Spawn(Vector3 pos, int value)
        {
            var pool = Pool("material");
            var go = pool.Get(pos);
            var p = go.GetComponent<ItemPickup>();
            p.Init(value, pool);
        }

        public static void SpawnExperience(Vector3 pos, int value)
        {
            if (value <= 0) return;
            var pool = Pool("experience");
            var go = pool.Get(pos);
            go.GetComponent<ItemPickup>().Init(value, pool, true);
        }

        public static void SpawnHealth(Vector3 pos, int heal)
        {
            if (heal <= 0) return;
            var pool = Pool("health");
            var go = pool.Get(pos);
            go.GetComponent<ItemPickup>().Init(heal, pool, false, true);
        }

        public static void Register(ItemPickup pickup)
        {
            if (pickup != null) ActivePickups.Add(pickup);
        }

        public static void Unregister(ItemPickup pickup)
        {
            if (pickup != null) ActivePickups.Remove(pickup);
        }

        public static void CopyActiveTo(List<ItemPickup> target)
        {
            target.Clear();
            foreach (var pickup in ActivePickups)
                if (pickup != null && pickup.gameObject.activeInHierarchy) target.Add(pickup);
        }

        /// <summary>波次结算：将场上未拾取材料直接入账；经验球保留至下一战斗波。</summary>
        public static int BagLooseMaterials()
        {
            var snapshot = new List<ItemPickup>(ActivePickups);
            int materials = 0;
            foreach (var pickup in snapshot)
            {
                if (pickup == null || pickup.IsExperience || pickup.IsHealth) continue;
                materials += pickup.Value;
                pickup.Despawn();
            }
            return materials;
        }

        public static void ClearActivePickups()
        {
            var snapshot = new List<ItemPickup>(ActivePickups);
            foreach (var pickup in snapshot)
                if (pickup != null) pickup.Despawn();
            ActivePickups.Clear();
        }

        /// <summary>预热（开局时调用，默认 32 个）。</summary>
        public static void Prewarm(int count = 32)
        {
            Pool("material").Prewarm(count);
            Pool("experience").Prewarm(count);
            Pool("health").Prewarm(Mathf.Max(4, count / 4));
        }

        /// <summary>销毁本池全部实例并清空（重开新局时调用）。</summary>
        public static void Clear()
        {
            ActivePickups.Clear();
            foreach (var pool in Pools.Values) pool.ClearAll();
            Pools.Clear();
        }

        private static ObjectPool Pool(string id)
        {
            if (!Pools.TryGetValue(id, out var pool))
            {
                pool = new ObjectPool(() => BuildItemGo(id));
                Pools[id] = pool;
            }
            return pool;
        }

        /// <summary>优先加载可编辑的掉落物预制体，缺失时生成带拾取器的兜底对象。</summary>
        private static GameObject BuildItemGo(string id)
        {
            bool experience = id == "experience";
            bool health = id == "health";
            var sprite = experience ? AssetLoader.LoadExperienceSprite() :
                health ? AssetLoader.LoadHealingPickupSprite() : AssetLoader.LoadMaterialPickupSprite();
            var prefab = PrefabProvider.Instantiate("Items/item_" + id, Root);
            if (prefab != null)
            {
                ConfigureArtwork(prefab, sprite, experience ? 0.38f : health ? 0.48f : 0.42f);
                return prefab;
            }

            var go = new GameObject("Item_" + id);
            go.transform.SetParent(Root);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.enabled = false;
            ConfigureArtwork(go, sprite, experience ? 0.38f : health ? 0.48f : 0.42f);
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = experience || health ? 0.2f : 0.16f;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.isKinematic = true;
            go.AddComponent<ItemPickup>();
            return go;
        }

        private static void ConfigureArtwork(GameObject pickup, Sprite sprite, float worldSize)
        {
            var rootRenderer = pickup.GetComponent<SpriteRenderer>();
            if (rootRenderer != null) rootRenderer.enabled = false;
            var visual = pickup.transform.Find("PickupArt");
            if (visual == null)
            {
                var artGo = new GameObject("PickupArt");
                visual = artGo.transform;
                visual.SetParent(pickup.transform, false);
            }
            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : SpriteFactory.Circle(new Color(0.5f, 0.95f, 0.45f), 0.16f);
            renderer.color = Color.white;
            renderer.sortingOrder = 7;
            float span = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
            visual.localScale = Vector3.one * (worldSize / Mathf.Max(0.01f, span));
        }
    }
}
