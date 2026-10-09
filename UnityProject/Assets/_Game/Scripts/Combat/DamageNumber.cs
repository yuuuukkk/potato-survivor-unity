using RogueLike.Core;
using RogueLike.Data;
using RogueLike.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RogueLike.Combat
{
    /// <summary>飘字伤害数字（Overlay 画布 + 对象池）。暴击放大、橙色。</summary>
    public class DamageNumber : MonoBehaviour
    {
        public static Canvas Canvas; // 由 GameBootstrap 注入

        private Text _text;
        private float _life;
        private float _t;
        private Vector2 _startPosition;
        private float _sideDrift;
        private Color _baseColor;
        private bool _critical;
        private ObjectPool _pool;

        private void Awake()
        {
            _text = GetComponent<Text>();
        }

        /// <summary>全局入口：在世界坐标生成一个伤害数字（懒创建池）。</summary>
        public static void Spawn(Vector3 worldPos, float amount, bool crit, Color? customColor = null)
        {
            if (Canvas == null) return;
            var pool = GamePools.Get("dmg", CreateGo);
            int limit = GameDatabase.Balance != null ? GameDatabase.Balance.damageNumberMaxActive : 24;
            if (pool.ActiveCount >= Mathf.Max(0, limit)) return;
            var go = pool.Get(worldPos);
            var dn = go.GetComponent<DamageNumber>();
            dn.Show(Mathf.RoundToInt(amount).ToString(),
                customColor ?? (crit ? new Color(1f, 0.62f, 0.15f) : Color.white),
                worldPos, crit, pool);
        }

        public static GameObject CreateGo()
        {
            var go = new GameObject("DamageNumber");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(140f, 44f);
            var text = go.AddComponent<Text>();
            text.font = UIFactory.DefaultFont;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            go.transform.SetParent(Canvas.transform, false);
            go.AddComponent<DamageNumber>();
            return go;
        }

        public void Show(string content, Color color, Vector3 worldPos, bool crit, ObjectPool pool)
        {
            _pool = pool;
            _text.text = content;
            _baseColor = color;
            _text.color = _baseColor;
            _text.fontSize = crit ? 30 : 20;
            _text.fontStyle = crit ? FontStyle.Bold : FontStyle.Normal;
            _critical = crit;

            if (Canvas != null && Camera.main != null)
            {
                Vector2 sp = Camera.main.WorldToScreenPoint(worldPos);
                var rt = (RectTransform)transform;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)Canvas.transform, sp, null, out var local))
                    rt.anchoredPosition = local;
            }
            _startPosition = ((RectTransform)transform).anchoredPosition;
            _sideDrift = Random.Range(-16f, 16f);
            transform.localScale = Vector3.one * (crit ? 1.35f : 1.12f);
            _life = crit ? 0.8f : 0.6f;
            _t = 0f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t >= _life)
            {
                if (_pool != null) _pool.Release(gameObject);
                return;
            }
            float progress = Mathf.Clamp01(_t / _life);
            float rise = Mathf.SmoothStep(0f, 1f, progress);
            var rt = (RectTransform)transform;
            rt.anchoredPosition = _startPosition + new Vector2(_sideDrift * rise, 42f * rise);
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / 0.18f));
            rt.localScale = Vector3.one * Mathf.Lerp(_critical ? 1.35f : 1.12f, 1f, settle);
            var c = _baseColor;
            c.a *= 1f - progress * progress;
            _text.color = c;
        }
    }
}
