using System.Collections.Generic;
using UnityEngine;

namespace RogueLike.Enemies
{
    /// <summary>场上存活敌人的静态注册表：注册/注销、最近敌人查询、清场。</summary>
    public static class EnemyManager
    {
        private static readonly HashSet<Enemy> Alive = new HashSet<Enemy>();

        public static int Count => Alive.Count;

        public static void Register(Enemy e) => Alive.Add(e);

        public static void Unregister(Enemy e) => Alive.Remove(e);

        public static void CopyAliveTo(List<Enemy> destination)
        {
            destination.Clear();
            foreach (var enemy in Alive)
                if (enemy != null) destination.Add(enemy);
        }

        public static void Clear()
        {
            Alive.Clear();
        }

        public static void DespawnAll()
        {
            if (Alive.Count == 0) return;
            var copy = new List<Enemy>(Alive);
            foreach (var e in copy)
            {
                if (e != null) e.Despawn();
            }
        }

        /// <summary>在 maxRange 内返回最近敌人；无则 null。</summary>
        public static Enemy Nearest(Vector2 pos, float maxRange)
        {
            Enemy best = null;
            float bestSq = maxRange * maxRange;
            foreach (var e in Alive)
            {
                if (e == null || !e.IsAlive || e.IsCharmed) continue;
                float d = ((Vector2)e.transform.position - pos).sqrMagnitude;
                if (d <= bestSq)
                {
                    bestSq = d;
                    best = e;
                }
            }
            return best;
        }

        public static Enemy NearestOther(Vector2 pos, float maxRange, Enemy excluded)
        {
            Enemy best = null;
            float bestSq = maxRange * maxRange;
            foreach (var enemy in Alive)
            {
                if (enemy == null || enemy == excluded || !enemy.IsAlive || enemy.IsCharmed) continue;
                float sq = ((Vector2)enemy.transform.position - pos).sqrMagnitude;
                if (sq > bestSq) continue;
                bestSq = sq;
                best = enemy;
            }
            return best;
        }
    }
}
