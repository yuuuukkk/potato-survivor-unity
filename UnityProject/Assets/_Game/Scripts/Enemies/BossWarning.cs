using System.Collections.Generic;
using RogueLike.Core;
using UnityEngine;

namespace RogueLike.Enemies
{
    /// <summary>Boss 弹道预警：沿真实飞行方向画轨迹，放射攻击画射线。</summary>
    public class BossWarning : MonoBehaviour
    {
        private static Material _material;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private ObjectPool _pool;
        private float _duration;
        private float _elapsed;
        private int _activeLines;
        private bool _directional;
        private Color _tint;

        public static GameObject CreateGo()
        {
            var go = PrefabProvider.Instantiate("Shared/boss_warning", PoolRoot.Root);
            if (go != null) return go;
            go = new GameObject("BossWarning");
            go.transform.SetParent(PoolRoot.Root);
            go.AddComponent<BossWarning>();
            return go;
        }

        private void Awake()
        {
            var oldSprite = GetComponent<SpriteRenderer>();
            if (oldSprite != null) oldSprite.enabled = false;
            var oldLine = GetComponent<LineRenderer>();
            if (oldLine != null) oldLine.enabled = false;
            if (_material == null) _material = new Material(Shader.Find("Sprites/Default"));
        }

        private LineRenderer EnsureLine(int index)
        {
            while (_lines.Count <= index)
            {
                var child = new GameObject("WarningPath");
                child.transform.SetParent(transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = false;
                line.material = _material;
                line.sortingOrder = 9;
                line.numCapVertices = 2;
                _lines.Add(line);
            }
            return _lines[index];
        }

        private void Prepare(float duration, ObjectPool pool, bool directional, Color tint)
        {
            _duration = Mathf.Max(0.05f, duration);
            _elapsed = 0f;
            _pool = pool;
            _directional = directional;
            _tint = tint;
            _activeLines = 0;
            foreach (var line in _lines) line.enabled = false;
        }

        public void ShowDirectional(Vector3 origin, Vector3 target, float dangerWidth, float duration, ObjectPool pool)
        {
            Prepare(duration, pool, true, new Color(1f, 0.22f, 0.14f));
            Vector2 direction = target - origin;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.right;
            direction.Normalize();
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector3 end = ArenaEdge(origin, direction);
            float halfWidth = Mathf.Max(0.1f, dangerWidth * 0.5f);

            SetPath(0, dangerWidth, origin, end);
            SetPath(1, 0.065f, origin + (Vector3)(perpendicular * halfWidth),
                end + (Vector3)(perpendicular * halfWidth));
            SetPath(2, 0.065f, origin - (Vector3)(perpendicular * halfWidth),
                end - (Vector3)(perpendicular * halfWidth));
            Vector3 arrowBase = end - (Vector3)direction * 0.7f;
            SetPath(3, 0.1f,
                arrowBase + (Vector3)(perpendicular * halfWidth * 0.75f), end,
                arrowBase - (Vector3)(perpendicular * halfWidth * 0.75f));
        }

        public void ShowRadial(Vector3 origin, Vector2 aim, int projectileCount, float duration, ObjectPool pool)
        {
            Prepare(duration, pool, false, new Color(0.80f, 0.42f, 1f));
            if (aim.sqrMagnitude < 0.001f) aim = Vector2.right;
            float baseAngle = Mathf.Atan2(aim.y, aim.x);
            int count = Mathf.Clamp(projectileCount, 6, 24);
            for (int i = 0; i < count; i++)
            {
                float a = baseAngle + Mathf.PI * 2f * i / count;
                Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector3 end = ArenaEdge(origin, direction);
                Vector3 shortEnd = origin + (Vector3)direction * 3.6f;
                if ((end - origin).sqrMagnitude > (shortEnd - origin).sqrMagnitude) end = shortEnd;
                SetPath(i, 0.20f, origin + (Vector3)direction * 0.7f, end);
            }
        }

        public void ShowMagicArea(Vector3 center, float radius, float duration, ObjectPool pool)
        {
            Prepare(duration, pool, false, new Color(0.72f, 0.35f, 1f));
            radius = Mathf.Max(0.5f, radius);
            var ring = new Vector3[33];
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = Mathf.PI * 2f * i / (ring.Length - 1);
                ring[i] = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            SetPath(0, 0.07f, ring);
            float cross = radius * 0.45f;
            SetPath(1, 0.045f, center + Vector3.left * cross, center + Vector3.right * cross);
            SetPath(2, 0.045f, center + Vector3.down * cross, center + Vector3.up * cross);
        }

        private void SetPath(int index, float width, params Vector3[] points)
        {
            var line = EnsureLine(index);
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = line.endWidth = width;
            line.enabled = true;
            _activeLines = Mathf.Max(_activeLines, index + 1);
        }

        private static Vector3 ArenaEdge(Vector3 origin, Vector2 direction)
        {
            Rect r = ArenaBounds.Rect;
            float distance = float.PositiveInfinity;
            if (direction.x > 0.001f) distance = Mathf.Min(distance, (r.xMax - origin.x) / direction.x);
            else if (direction.x < -0.001f) distance = Mathf.Min(distance, (r.xMin - origin.x) / direction.x);
            if (direction.y > 0.001f) distance = Mathf.Min(distance, (r.yMax - origin.y) / direction.y);
            else if (direction.y < -0.001f) distance = Mathf.Min(distance, (r.yMin - origin.y) / direction.y);
            if (float.IsInfinity(distance)) distance = 1f;
            return origin + (Vector3)direction * Mathf.Max(0.1f, distance);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);
            float pulse = 0.05f * Mathf.Sin(_elapsed * 16f);
            for (int i = 0; i < _activeLines; i++)
            {
                var color = _tint;
                color.a = _directional && i == 0
                    ? Mathf.Clamp01(0.12f + 0.12f * progress + pulse)
                    : Mathf.Clamp01(0.52f + 0.40f * progress + pulse);
                _lines[i].startColor = _lines[i].endColor = color;
            }
            if (_elapsed < _duration) return;
            foreach (var line in _lines) line.enabled = false;
            if (_pool != null) _pool.Release(gameObject);
        }
    }
}
