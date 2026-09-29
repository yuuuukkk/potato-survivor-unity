using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Combat
{
    /// <summary>短促的命中火花；对象池复用，颜色、尺寸和时长由 GameBalance SO 控制。</summary>
    public class HitImpact : MonoBehaviour
    {
        private static Sprite _streak;
        private readonly SpriteRenderer[] _rays = new SpriteRenderer[3];
        private ObjectPool _pool;
        private float _elapsed;
        private float _duration;
        private float _size;
        private Color _color;

        public static void Spawn(Vector3 position, Vector2 direction, bool critical, bool playerHit = false)
        {
            var balance = GameDatabase.Balance;
            var pool = GamePools.Get("hit_impact", CreateGo);
            if (pool.ActiveCount >= (balance != null ? balance.impactMaxActive : 36)) return;
            var go = pool.Get(position);
            if (go == null) return;
            go.GetComponent<HitImpact>().Show(pool, position, direction, critical, playerHit, balance);
        }

        private static GameObject CreateGo()
        {
            if (_streak == null) _streak = SpriteFactory.Square(0.28f, 0.055f);
            var go = new GameObject("HitImpact");
            go.transform.SetParent(PoolRoot.Root);
            var impact = go.AddComponent<HitImpact>();
            for (int i = 0; i < impact._rays.Length; i++)
            {
                var ray = new GameObject("Ray" + i);
                ray.transform.SetParent(go.transform, false);
                var renderer = ray.AddComponent<SpriteRenderer>();
                renderer.sprite = _streak;
                renderer.sortingOrder = 9;
                impact._rays[i] = renderer;
            }
            return go;
        }

        private void Show(ObjectPool pool, Vector3 position, Vector2 direction, bool critical,
            bool playerHit, GameBalanceSO balance)
        {
            _pool = pool;
            _elapsed = 0f;
            _duration = Mathf.Max(0.04f, balance != null ? balance.impactDuration : 0.18f);
            _size = Mathf.Max(0.1f, balance != null ? balance.impactSize : 1f) * (critical ? 1.4f : 1f);
            _color = playerHit ? new Color(1f, 0.35f, 0.32f) :
                balance != null ? balance.impactColor : new Color(1f, 0.86f, 0.47f);
            transform.position = position;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.right;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            UpdateVisual(0f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
            {
                _pool?.Release(gameObject);
                return;
            }
            UpdateVisual(_elapsed / _duration);
        }

        private void UpdateVisual(float progress)
        {
            transform.localScale = Vector3.one * (_size * Mathf.Lerp(0.75f, 1.25f, progress));
            for (int i = 0; i < _rays.Length; i++)
            {
                float angle = (i - 1) * 55f;
                float radians = angle * Mathf.Deg2Rad;
                float distance = 0.08f + progress * 0.27f;
                var ray = _rays[i];
                ray.transform.localPosition = new Vector3(Mathf.Cos(radians) * distance,
                    Mathf.Sin(radians) * distance, 0f);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                var tint = _color;
                tint.a *= 1f - progress;
                ray.color = tint;
            }
        }
    }
}
