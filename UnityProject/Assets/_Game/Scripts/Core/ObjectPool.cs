using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueLike.Core
{
    /// <summary>通用对象池：Get 激活复用，Release 回收到栈中；场景重启时 ClearAll。</summary>
    public class ObjectPool
    {
        private readonly Func<GameObject> _factory;
        private readonly Stack<GameObject> _stack = new Stack<GameObject>();
        private readonly List<GameObject> _active = new List<GameObject>();

        public ObjectPool(Func<GameObject> factory)
        {
            _factory = factory;
        }

        public int ActiveCount => _active.Count;
        public int AvailableCount => _stack.Count;

        public GameObject Get(Vector3 pos)
        {
            GameObject go = _stack.Count > 0 ? _stack.Pop() : _factory();
            if (go == null) return null;
            go.transform.position = pos;
            go.SetActive(true);
            _active.Add(go);
            return go;
        }

        public void Release(GameObject go)
        {
            if (go == null) return;
            // 同一对象可能在同一帧同时触发射程结束与碰撞；只允许归还一次。
            if (!_active.Remove(go)) return;
            go.SetActive(false);
            _stack.Push(go);
        }

        /// <summary>预热：预创建 count 个实例入栈（保持 inactive），消除运行期首个尖峰。</summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = _factory();
                if (go == null) continue;
                go.SetActive(false);
                _stack.Push(go);
            }
        }

        public void ClearAll()
        {
            foreach (var go in _stack)
                if (go != null) UnityEngine.Object.Destroy(go);
            _stack.Clear();
            foreach (var go in _active)
                if (go != null) UnityEngine.Object.Destroy(go);
            _active.Clear();
        }
    }

    /// <summary>统一池根节点：所有池对象挂到其下，Hierarchy 不再散落根对象。</summary>
    public static class PoolRoot
    {
        private static Transform _root;

        public static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("ObjectPools");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _root = go.transform;
                }
                return _root;
            }
        }

        /// <summary>创建池子节点（如 WeaponPool / EnemyPool），并返回其 Transform。</summary>
        public static Transform CreateChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root);
            return go.transform;
        }
    }

    /// <summary>按 key 持有的池注册表，供全游戏共享（子弹/敌人/拾取/伤害数字等）。</summary>
    public static class GamePools
    {
        private static readonly Dictionary<string, ObjectPool> Pools = new Dictionary<string, ObjectPool>();

        public static ObjectPool Get(string key, Func<GameObject> factory)
        {
            if (!Pools.TryGetValue(key, out var pool))
            {
                pool = new ObjectPool(factory);
                Pools[key] = pool;
            }
            return pool;
        }

        /// <summary>预热指定 key 的池（无则先按 factory 创建）。</summary>
        public static void Prewarm(string key, Func<GameObject> factory, int count)
        {
            Get(key, factory).Prewarm(count);
        }

        public static void ClearAll()
        {
            foreach (var pool in Pools.Values) pool.ClearAll();
            Pools.Clear();
        }
    }
}
