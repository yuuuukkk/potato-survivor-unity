using System.Collections.Generic;
using RogueLike.Combat;
using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Items;
using UnityEngine;

namespace RogueLike.Enemies
{
    /// <summary>
    /// 敌人专用对象池：按敌人 id 分池，统一挂载到 ObjectPools/EnemyPool 节点下。
    /// 全游戏敌人的唯一出处（含分裂子体/Boss）；换皮改 BuildEnemyGo 一处即可。
    /// </summary>
    public static class EnemyPool
    {
        private static readonly Dictionary<string, ObjectPool> Pools = new Dictionary<string, ObjectPool>();
        private static Transform _root;

        private static Transform Root => _root != null ? _root : (_root = PoolRoot.CreateChild("EnemyPool"));

        /// <summary>从池中取出并初始化一个敌人（注入波次缩放系数）。</summary>
        public static Enemy Spawn(string id, Vector3 pos, float hpScale, float dmgScale, float speedScale)
        {
            var data = GameDatabase.GetEnemy(id);
            if (data == null)
            {
                Debug.LogWarning("[EnemyPool] 未知敌人: " + id);
                return null;
            }
            var pool = Pool(id, data);
            var go = pool.Get(pos);
            var enemy = go.GetComponent<Enemy>();
            enemy.Init(data, hpScale, dmgScale, speedScale, pool);
            return enemy;
        }

        /// <summary>开新局前预热：全部敌人池 + 战场通用小对象池，消除首波生成尖峰。</summary>
        public static void PrewarmAll()
        {
            foreach (var kv in GameDatabase.Enemies)
            {
                var data = kv.Value;
                int n = data.isBoss ? 1 : data.isElite ? 4 : 12;
                Pool(kv.Key, data).Prewarm(n);
            }
            GamePools.Prewarm("enemy_bullet", Enemy.BuildEnemyBulletGo, 48);
            GamePools.Prewarm("boss_warning", BossWarning.CreateGo, 2);
            ItemPool.Prewarm(32);
            GamePools.Prewarm("dmg", DamageNumber.CreateGo, 24);
        }

        /// <summary>销毁本池全部实例并清空（重开新局时调用）。</summary>
        public static void Clear()
        {
            foreach (var pool in Pools.Values) pool.ClearAll();
            Pools.Clear();
        }

        private static ObjectPool Pool(string id, EnemyData data)
        {
            if (!Pools.TryGetValue(id, out var pool))
            {
                pool = new ObjectPool(() => BuildEnemyGo(data));
                Pools[id] = pool;
            }
            return pool;
        }

        /// <summary>敌人"预制"：结构 + 占位美术 + 物理。优先加载 Resources/Prefabs/EnemyPool/enemy_{id}，无则运行时构建。</summary>
        private static GameObject BuildEnemyGo(EnemyData data)
        {
            var prefab = PrefabProvider.Instantiate("EnemyPool/enemy_" + data.id, Root);
            if (prefab != null) return prefab;

            var go = new GameObject("Enemy_" + data.id);
            go.transform.SetParent(Root);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = data.radius;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.isKinematic = true;
            go.AddComponent<Enemy>();
            return go;
        }
    }
}
