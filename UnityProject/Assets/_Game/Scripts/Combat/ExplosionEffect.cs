using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Combat
{
    /// <summary>爆炸范围可视化；视觉半径与真实伤害半径一致，不参与碰撞判定。</summary>
    public class ExplosionEffect : MonoBehaviour
    {
        private static Sprite _disc;
        private SpriteRenderer _outer;
        private SpriteRenderer _inner;
        private ObjectPool _pool;
        private float _elapsed;
        private float _duration;
        private float _radius;
        private Color _outerColor;
        private Color _innerColor;

        public static void Spawn(Vector3 position, float radius)
        {
            if (radius <= 0f) return;
            var pool = GamePools.Get("explosion_effect", CreateGo);
            var go = pool.Get(position);
            if (go == null) return;
            go.GetComponent<ExplosionEffect>().Show(pool, radius);
            var balance = GameDatabase.Balance;
            CameraShake.Shake(0.13f, balance != null ? balance.explosionShakeMagnitude : 0.09f);
        }

        private static GameObject CreateGo()
        {
            if (_disc == null) _disc = SpriteFactory.Circle(Color.white, 1f);
            var go = new GameObject("ExplosionEffect");
            go.transform.SetParent(PoolRoot.Root);
            var effect = go.AddComponent<ExplosionEffect>();
            effect._outer = MakeLayer(go.transform, "Outer", 7);
            effect._inner = MakeLayer(go.transform, "Inner", 8);
            return go;
        }

        private static SpriteRenderer MakeLayer(Transform parent, string name, int sort)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _disc;
            sr.sortingOrder = sort;
            return sr;
        }

        private void Show(ObjectPool pool, float radius)
        {
            var balance = GameDatabase.Balance;
            _pool = pool;
            _radius = radius;
            _duration = Mathf.Max(0.05f, balance != null ? balance.explosionVisualDuration : 0.28f);
            _outerColor = balance != null ? balance.explosionOuterColor : new Color(1f, 0.38f, 0.1f, 0.48f);
            _innerColor = balance != null ? balance.explosionInnerColor : new Color(1f, 0.87f, 0.55f, 0.75f);
            _elapsed = 0f;
            Render(0f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
            {
                _pool?.Release(gameObject);
                return;
            }
            Render(_elapsed / _duration);
        }

        private void Render(float progress)
        {
            float reach = Mathf.SmoothStep(0.08f, 1f, progress);
            _outer.transform.localScale = Vector3.one * (_radius * reach);
            _inner.transform.localScale = Vector3.one * (_radius * (0.10f + 0.32f * reach));
            var outer = _outerColor;
            outer.a *= 1f - progress;
            _outer.color = outer;
            var inner = _innerColor;
            inner.a *= Mathf.Pow(1f - progress, 2f);
            _inner.color = inner;
        }
    }
}
