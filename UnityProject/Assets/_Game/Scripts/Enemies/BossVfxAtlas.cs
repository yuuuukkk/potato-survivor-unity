using RogueLike.Core;
using RogueLike.Combat;
using RogueLike.Player;
using UnityEngine;

namespace RogueLike.Enemies
{
    /// <summary>Boss VFX sprite sheet loader and short-lived animation driver.</summary>
    public sealed class BossVfxAtlas : MonoBehaviour
    {
        private const int Columns = 4;
        private static Sprite[] _red;
        private static Sprite[] _magic;

        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private Sprite[] _impactFrames;
        private Vector2 _travelDirection;
        private float _elapsed;
        private float _travelSpeed;
        private float _frameDuration;
        private bool _travelling;
        private bool _redAttack;
        private bool _hasEnteredArena;
        private float _damageRadius;
        private float _projectileRadius;
        private float _damage;
        private Enemy _source;
        private CircleCollider2D _playerCollider;

        public static Sprite[] RedFrames { get { LoadFrames(); return _red; } }
        public static Sprite[] MagicFrames { get { LoadFrames(); return _magic; } }

        private static void LoadFrames()
        {
            if (_red != null && _magic != null) return;
            Texture2D texture = Resources.Load<Texture2D>("Art/Effects/boss_vfx_atlas");
            if (texture == null)
            {
                Debug.LogError("Missing Resources/Art/Effects/boss_vfx_atlas.png");
                _red = _magic = new Sprite[0];
                return;
            }

            _red = new Sprite[Columns];
            _magic = new Sprite[Columns];
            float cellWidth = texture.width / (float)Columns;
            float cellHeight = texture.height / 2f;
            for (int column = 0; column < Columns; column++)
            {
                // Unity texture coordinates start at the bottom; the generated red sequence is the top row.
                _magic[column] = Sprite.Create(texture,
                    new Rect(column * cellWidth, 0f, cellWidth, cellHeight),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _red[column] = Sprite.Create(texture,
                    new Rect(column * cellWidth, cellHeight, cellWidth, cellHeight),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
        }

        public static void SpawnRedThrow(Vector3 from, Vector3 target, float projectileSize,
            float impactSize, float impactRadius, float damage, float speed, Enemy source)
        {
            LoadFrames();
            if (_red == null || _red.Length < 4) return;
            var go = new GameObject("BossRedImpactAttack");
            var effect = go.AddComponent<BossVfxAtlas>();
            effect.BeginTravel(_red, _red, from, target, projectileSize, impactSize,
                impactRadius, damage, speed, source, true);
        }

        public static void SpawnMagicImpact(Vector3 position, float size)
        {
            LoadFrames();
            if (_magic == null || _magic.Length < 4) return;
            var go = new GameObject("BossMagicImpact");
            go.AddComponent<BossVfxAtlas>().BeginBurst(_magic, position, size, 0.085f);
        }

        private void Awake()
        {
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = 12;
        }

        private void BeginTravel(Sprite[] flightFrames, Sprite[] impactFrames, Vector3 from, Vector3 target,
            float projectileSize, float impactSize, float impactRadius, float damage, float speed,
            Enemy source, bool redAttack)
        {
            _frames = flightFrames;
            _impactFrames = impactFrames;
            _travelDirection = ((Vector2)(target - from)).normalized;
            if (_travelDirection.sqrMagnitude < 0.001f) _travelDirection = Vector2.right;
            _travelSpeed = Mathf.Max(0.1f, speed);
            _frameDuration = 0.075f;
            _damageRadius = impactRadius;
            _projectileRadius = projectileSize * 0.3f;
            _damage = damage;
            _source = source;
            _redAttack = redAttack;
            _travelling = true;
            _hasEnteredArena = ArenaBounds.Contains(from);
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            _playerCollider = player != null ? player.GetComponent<CircleCollider2D>() : null;
            transform.position = from;
            _renderer.sprite = _frames[0];
            SetSize(projectileSize);
            transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(_travelDirection.y, _travelDirection.x) * Mathf.Rad2Deg);
            _impactSize = impactSize;
        }

        private float _impactSize;

        private void BeginBurst(Sprite[] frames, Vector3 position, float size, float frameDuration)
        {
            _frames = frames;
            _travelling = false;
            _frameDuration = frameDuration;
            transform.position = position;
            _renderer.sprite = _frames[2];
            SetSize(size);
        }

        private void SetSize(float worldSize)
        {
            float spriteSize = Mathf.Max(0.01f, _renderer.sprite.bounds.size.x);
            transform.localScale = Vector3.one * (worldSize / spriteSize);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_travelling)
            {
                transform.position += (Vector3)(_travelDirection * _travelSpeed * Time.deltaTime);
                int frame = (int)(_elapsed / _frameDuration) % 2;
                _renderer.sprite = _frames[frame];
                bool insideArena = ArenaBounds.Contains(transform.position);
                if (insideArena) _hasEnteredArena = true;
                if (HasHitPlayer()) Impact();
                else if (_hasEnteredArena && !insideArena)
                {
                    transform.position = ArenaBounds.Clamp(transform.position);
                    Impact();
                }
                return;
            }

            int burstFrame = 2 + Mathf.FloorToInt(_elapsed / _frameDuration);
            if (burstFrame >= _frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            _renderer.sprite = _frames[burstFrame];
        }

        private void Impact()
        {
            _travelling = false;
            _elapsed = 0f;
            _frames = _impactFrames;
            _renderer.sprite = _frames[2];
            SetSize(_impactSize);
            if (!_redAttack) return;

            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            float playerRadius = _playerCollider != null
                ? _playerCollider.radius * Mathf.Max(_playerCollider.transform.lossyScale.x, _playerCollider.transform.lossyScale.y)
                : 0.35f;
            if (player != null && Vector2.Distance(player.transform.position, transform.position) <= _damageRadius + playerRadius)
                player.Health.TakeDamage(new DamageInfo(_damage, _source != null ? _source.gameObject : gameObject));
        }

        private bool HasHitPlayer()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return false;
            Vector2 center = player.transform.position;
            float radius = 0.35f;
            if (_playerCollider != null)
            {
                center = _playerCollider.transform.TransformPoint(_playerCollider.offset);
                radius = _playerCollider.radius * Mathf.Max(_playerCollider.transform.lossyScale.x,
                    _playerCollider.transform.lossyScale.y);
            }
            float combined = radius + _projectileRadius;
            return ((Vector2)transform.position - center).sqrMagnitude <= combined * combined;
        }
    }
}
