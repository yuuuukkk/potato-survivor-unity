using RogueLike.Core;
using UnityEngine;

namespace RogueLike.Combat
{
    /// <summary>面向扇形火焰与瞬时激光束的轻量网格特效；对象池复用，不创建子弹或伤害碰撞体。</summary>
    public sealed class WeaponPatternEffect : MonoBehaviour
    {
        private static Material _material;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private ObjectPool _pool;
        private Color[] _baseColors;
        private Color[] _fadeColors;
        private float _elapsed;
        private float _duration;

        public static void SpawnFlame(Vector3 origin, Vector2 direction, float range, float halfAngle, float duration)
            => Spawn(origin, direction, range, halfAngle, duration, true);

        public static void SpawnLaser(Vector3 origin, Vector2 direction, float range, float width, float duration)
            => Spawn(origin, direction, range, width, duration, false);

        private static void Spawn(Vector3 origin, Vector2 direction, float range, float shape, float duration, bool flame)
        {
            var pool = GamePools.Get("weapon_pattern_effect", CreateGo);
            if (pool == null) return;
            var go = pool.Get(origin);
            if (go == null) return;
            go.GetComponent<WeaponPatternEffect>().Show(pool, origin, direction, range, shape,
                Mathf.Max(0.03f, duration), flame);
        }

        private static GameObject CreateGo()
        {
            var go = new GameObject("WeaponPatternEffect");
            go.transform.SetParent(PoolRoot.Root);
            var effect = go.AddComponent<WeaponPatternEffect>();
            effect._filter = go.AddComponent<MeshFilter>();
            effect._renderer = go.AddComponent<MeshRenderer>();
            if (_material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                _material = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
                _material.name = "WeaponPatternEffectMaterial";
            }
            effect._renderer.sharedMaterial = _material;
            effect._renderer.sortingOrder = 8;
            effect._mesh = new Mesh { name = "WeaponPatternEffectMesh" };
            effect._filter.sharedMesh = effect._mesh;
            return go;
        }

        private void Show(ObjectPool pool, Vector3 origin, Vector2 direction, float range,
            float shape, float duration, bool flame)
        {
            _pool = pool;
            _elapsed = 0f;
            _duration = duration;
            transform.position = origin;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            BuildMesh(Mathf.Max(0.1f, range), shape, flame);
            _renderer.enabled = true;
        }

        private void BuildMesh(float range, float shape, bool flame)
        {
            if (flame)
            {
                // Separate tapered tongues read as pressurized flame; a filled cone reads like a flashlight.
                const int tongues = 7;
                const int slices = 6;
                const int layers = 2;
                int verticesPerTongue = slices * 2 * layers;
                var vertices = new Vector3[tongues * verticesPerTongue];
                var triangles = new int[tongues * (slices - 1) * 6 * layers];
                var colors = new Color[vertices.Length];
                float spread = Mathf.Clamp(shape, 5f, 60f) * Mathf.Deg2Rad;
                int tri = 0;
                for (int tongue = 0; tongue < tongues; tongue++)
                {
                    float lane = (tongue - (tongues - 1) * 0.5f) / ((tongues - 1) * 0.5f);
                    float length = range * (0.72f + 0.25f * (0.5f + 0.5f * Mathf.Sin(tongue * 2.17f)));
                    float phase = tongue * 1.71f;
                    for (int layer = 0; layer < layers; layer++)
                    {
                        int start = tongue * verticesPerTongue + layer * slices * 2;
                        float layerWidth = range * (layer == 0 ? 0.042f : 0.017f);
                        for (int i = 0; i < slices; i++)
                        {
                            float t = i / (float)(slices - 1);
                            float x = length * t;
                            float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.08f, 0.94f, t)), 0.72f);
                            float drift = lane * Mathf.Tan(spread) * length * t * 0.24f;
                            float wiggle = Mathf.Sin(t * 10f + phase) * range * 0.018f * t;
                            float centerY = drift + wiggle;
                            float halfWidth = layerWidth * envelope;
                            int left = start + i * 2;
                            vertices[left] = new Vector3(x, centerY - halfWidth, 0f);
                            vertices[left + 1] = new Vector3(x, centerY + halfWidth, 0f);
                            Color c = layer == 0
                                ? new Color(1f, 0.22f + 0.12f * (1f - t), 0.015f, 0.78f * (1f - t * 0.55f))
                                : new Color(1f, 0.78f - 0.35f * t, 0.12f, 0.88f * (1f - t * 0.7f));
                            colors[left] = colors[left + 1] = c;
                            if (i < slices - 1)
                            {
                                triangles[tri++] = left; triangles[tri++] = left + 2; triangles[tri++] = left + 3;
                                triangles[tri++] = left; triangles[tri++] = left + 3; triangles[tri++] = left + 1;
                            }
                        }
                    }
                }
                SetMesh(vertices, triangles, colors);
            }
            else
            {
                float halfWidth = Mathf.Clamp(shape, 0.03f, 1f) * 0.5f;
                var vertices = new[]
                {
                    new Vector3(0f, -halfWidth, 0f), new Vector3(range, -halfWidth, 0f),
                    new Vector3(range, halfWidth, 0f), new Vector3(0f, halfWidth, 0f),
                    new Vector3(0f, -halfWidth * 0.38f, -0.005f), new Vector3(range, -halfWidth * 0.38f, -0.005f),
                    new Vector3(range, halfWidth * 0.38f, -0.005f), new Vector3(0f, halfWidth * 0.38f, -0.005f)
                };
                var triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
                var colors = new Color[8];
                for (int i = 0; i < 4; i++) colors[i] = new Color(1f, 0.12f, 0.22f, 0.62f);
                for (int i = 4; i < 8; i++) colors[i] = new Color(1f, 0.94f, 0.82f, 0.95f);
                SetMesh(vertices, triangles, colors);
            }
        }

        private void SetMesh(Vector3[] vertices, int[] triangles, Color[] colors)
        {
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.colors = colors;
            _mesh.RecalculateBounds();
            _baseColors = colors;
            if (_fadeColors == null || _fadeColors.Length != colors.Length) _fadeColors = new Color[colors.Length];
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float fade = Mathf.Clamp01(1f - _elapsed / _duration);
            if (_baseColors != null)
            {
                for (int i = 0; i < _baseColors.Length; i++)
                {
                    _fadeColors[i] = _baseColors[i];
                    _fadeColors[i].a *= fade;
                }
                _mesh.colors = _fadeColors;
            }
            if (_elapsed >= _duration)
            {
                _renderer.enabled = false;
                _pool?.Release(gameObject);
            }
        }
    }
}
