using RogueLike.Core;
using UnityEngine;

namespace RogueLike.Enemies
{
    /// <summary>纯视觉的回池死亡动画；怪物碰撞与攻击已立即停止。</summary>
    public sealed class EnemyDeathVfx : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private ObjectPool _pool;
        private Vector3 _startPosition;
        private Vector3 _startScale;
        private Color _startColor;
        private float _duration;
        private float _elapsed;

        public static GameObject CreateGo()
        {
            var go = new GameObject("EnemyDeathVfx");
            go.transform.SetParent(PoolRoot.Root);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<EnemyDeathVfx>();
            return go;
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Play(SpriteRenderer source, Color tint, Vector3 scale, float duration, ObjectPool pool)
        {
            _pool = pool;
            _startPosition = source.transform.position;
            _startScale = scale;
            _startColor = tint;
            _duration = Mathf.Max(0.05f, duration);
            _elapsed = 0f;
            transform.position = _startPosition;
            transform.localScale = _startScale;
            _renderer.sprite = source.sprite;
            _renderer.color = _startColor;
            _renderer.sortingLayerID = source.sortingLayerID;
            _renderer.sortingOrder = source.sortingOrder + 1;
        }

        private void Update()
        {
            if (_pool == null) return;
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            float ease = t * t * (3f - 2f * t);
            transform.position = _startPosition + Vector3.up * (0.12f * ease);
            transform.localScale = new Vector3(_startScale.x * (1f + 0.16f * ease),
                _startScale.y * (1f - 0.85f * ease), _startScale.z);
            var color = _startColor;
            color.a *= 1f - ease;
            _renderer.color = color;
            if (t >= 1f)
            {
                var pool = _pool;
                _pool = null;
                pool.Release(gameObject);
            }
        }
    }
}
