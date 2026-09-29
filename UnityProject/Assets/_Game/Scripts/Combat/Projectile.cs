using System.Collections.Generic;
using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Enemies;
using RogueLike.Items;
using RogueLike.Player;
using UnityEngine;

namespace RogueLike.Combat
{
    /// <summary>
    /// 投射物：直线飞行、射程寿命、穿透、爆炸、往返（回旋镖）。
    /// team = 0 玩家子弹 / 1 敌方子弹 / 2 被魅惑敌人的子弹。
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public int team;
        public float damage;
        public float speed;
        public float maxDistance;
        public int pierce;
        public float explodeRadius;
        public bool isCrit;
        public bool boomerang;

        private Vector2 _dir;
        private float _travelled;
        private readonly HashSet<Enemy> _hitEnemies = new HashSet<Enemy>();
        private readonly HashSet<Enemy> _areaDamaged = new HashSet<Enemy>();
        private Collider2D[] _areaHits;
        private bool _out = true;
        private ObjectPool _pool;
        private PlayerStats _sourceStats;
        private SpriteRenderer _visual;
        private Sprite _defaultSprite;
        private bool _bossMagicVisual;
        private float _bossMagicAnimTime;
        private SpriteRenderer _bossMagicRenderer;
        private int _bouncesRemaining;
        private bool _ricochetEnabled;
        private float _bounceDamageMultiplier = 1f;
        private Transform _art;
        private Vector3 _defaultArtScale;
        private CircleCollider2D _circle;
        private float _defaultColliderRadius;
        private float _charmChance;
        private float _charmDuration;
        private float _charmDamagePerSecond;
        private float _returnDamageMultiplier = 1f;
        private float _returnSpeedMultiplier = 1f;
        private Enemy _friendlyOwner;
        private Enemy _friendlyTarget;
        private int _vacuumRemaining;
        private float _vacuumDamagePerAbsorb;
        private float _vacuumRadius;
        private Collider2D[] _vacuumHits;
        private readonly List<ItemPickup> _nearbyPickups = new List<ItemPickup>();
        private float _collectionRadius;
        private float _returnBonusPerPickup;
        private int _maxPickupBonusCount;
        private int _collectedPickups;

        public void Launch(Vector3 pos, Vector2 dir, int team, float damage, float speed, float maxDist,
            int pierce, float explodeRadius, bool isCrit, bool boomerang, ObjectPool pool, PlayerStats sourceStats)
        {
            if (_bossMagicVisual) RestoreVisual();
            transform.position = pos;
            this.team = team;
            this.damage = damage;
            this.speed = speed;
            maxDistance = maxDist;
            this.pierce = pierce;
            this.explodeRadius = explodeRadius;
            this.isCrit = isCrit;
            this.boomerang = boomerang;
            _dir = dir.normalized;
            FaceDirection();
            _travelled = 0f;
            _hitEnemies.Clear();
            _out = true;
            _pool = pool;
            _sourceStats = sourceStats;
            _bouncesRemaining = 0;
            _ricochetEnabled = false;
            _bounceDamageMultiplier = 1f;
            _charmChance = 0f;
            _charmDuration = 0f;
            _charmDamagePerSecond = 0f;
            _returnDamageMultiplier = 1f;
            _returnSpeedMultiplier = 1f;
            _friendlyOwner = null;
            _friendlyTarget = null;
            _vacuumRemaining = 0;
            _vacuumDamagePerAbsorb = 0f;
            _vacuumRadius = 0f;
            _collectionRadius = 0f;
            _returnBonusPerPickup = 0f;
            _maxPickupBonusCount = 0;
            _collectedPickups = 0;
            _nearbyPickups.Clear();
            if (_art != null) _art.localScale = _defaultArtScale;
            if (_circle != null) _circle.radius = _defaultColliderRadius;
        }

        public void ConfigureRicochet(int bounces, float damageMultiplier)
        {
            int cap = GameDatabase.Balance != null ? Mathf.Max(0, GameDatabase.Balance.maxProjectileBounces) : 3;
            _bouncesRemaining = Mathf.Clamp(bounces, 0, cap);
            _ricochetEnabled = _bouncesRemaining > 0;
            _bounceDamageMultiplier = Mathf.Clamp(damageMultiplier, 0.1f, 1f);
        }

        public void ConfigureCharm(float chance, float duration, float damagePerSecond)
        {
            _charmChance = Mathf.Clamp01(chance);
            _charmDuration = Mathf.Max(0f, duration);
            _charmDamagePerSecond = Mathf.Max(0f, damagePerSecond);
        }

        public void ConfigureFriendlyEnemyShot(Enemy owner, Enemy target)
        {
            _friendlyOwner = owner;
            _friendlyTarget = target;
        }

        public void ConfigureVacuum(int limit, float damagePerAbsorb, float radius)
        {
            _vacuumRemaining = Mathf.Clamp(limit, 0, 16);
            _vacuumDamagePerAbsorb = Mathf.Clamp(damagePerAbsorb, 0f, 2f);
            _vacuumRadius = Mathf.Clamp(radius, 0.1f, 3f);
        }

        private bool TryAbsorbEnemyBullet()
        {
            if (!gameObject.activeInHierarchy || team != 1 || _pool == null) return false;
            if (_bossMagicVisual) RestoreVisual();
            Release();
            return true;
        }

        public void ConfigureBoomerangReturn(float damageMultiplier, float speedMultiplier)
        {
            _returnDamageMultiplier = Mathf.Clamp(damageMultiplier, 1f, 3f);
            _returnSpeedMultiplier = Mathf.Clamp(speedMultiplier, 1f, 3f);
        }

        public void ConfigureBoomerangCollection(float radius, float returnBonusPerPickup, int maxBonusCount)
        {
            _collectionRadius = Mathf.Max(0f, radius);
            _returnBonusPerPickup = Mathf.Max(0f, returnBonusPerPickup);
            _maxPickupBonusCount = Mathf.Clamp(maxBonusCount, 0, 32);
        }

        public void SetVisualScale(float multiplier)
        {
            if (_art == null)
            {
                _art = transform.Find("ProjectileArt");
                if (_art != null) _defaultArtScale = _art.localScale;
            }
            if (_art != null) _art.localScale = _defaultArtScale * Mathf.Max(0.1f, multiplier);
            if (_circle == null)
            {
                _circle = GetComponent<CircleCollider2D>();
                if (_circle != null) _defaultColliderRadius = _circle.radius;
            }
            if (_circle != null) _circle.radius = _defaultColliderRadius * Mathf.Max(0.1f, multiplier);
        }

        public void SetBossMagicVisual(bool enabled)
        {
            if (_visual == null) _visual = GetComponent<SpriteRenderer>();
            if (_visual == null) return;
            if (_defaultSprite == null) _defaultSprite = _visual.sprite;
            _bossMagicVisual = enabled;
            _bossMagicAnimTime = 0f;
            if (enabled)
            {
                var frames = BossVfxAtlas.MagicFrames;
                if (frames == null || frames.Length < 2) return;
                _visual.enabled = false;
                if (_bossMagicRenderer == null)
                {
                    var visualGo = new GameObject("BossMagicVisual");
                    visualGo.transform.SetParent(transform, false);
                    _bossMagicRenderer = visualGo.AddComponent<SpriteRenderer>();
                    _bossMagicRenderer.sortingOrder = _visual.sortingOrder + 1;
                }
                _bossMagicRenderer.enabled = true;
                _bossMagicRenderer.color = Color.white;
                _bossMagicRenderer.sprite = frames[0];
                float spriteWidth = Mathf.Max(0.01f, frames[0].bounds.size.x);
                _bossMagicRenderer.transform.localScale = Vector3.one * (0.9f / spriteWidth);
            }
            else RestoreVisual();
        }

        private void RestoreVisual()
        {
            if (_visual == null) _visual = GetComponent<SpriteRenderer>();
            if (_visual != null && _defaultSprite != null)
            {
                _visual.sprite = _defaultSprite;
                _visual.color = Color.white;
                _visual.enabled = true;
            }
            if (_bossMagicRenderer != null) _bossMagicRenderer.enabled = false;
            _bossMagicVisual = false;
        }

        private void Update()
        {
            if (_bossMagicVisual)
            {
                _bossMagicAnimTime += Time.deltaTime;
                var frames = BossVfxAtlas.MagicFrames;
                if (_bossMagicRenderer != null && frames != null && frames.Length >= 2)
                    _bossMagicRenderer.sprite = frames[(int)(_bossMagicAnimTime / 0.09f) % 2];
            }
            if (boomerang && !_out && _sourceStats != null)
            {
                Vector2 homeDirection = (Vector2)(_sourceStats.transform.position - transform.position);
                if (homeDirection.sqrMagnitude < 0.20f)
                {
                    Release();
                    return;
                }
                _dir = homeDirection.normalized;
            }
            transform.position += (Vector3)(_dir * speed * Time.deltaTime);
            _travelled += speed * Time.deltaTime;
            if (boomerang) transform.Rotate(0f, 0f, 720f * Time.deltaTime);

            if (boomerang && _out && team == 0 && _collectionRadius > 0f)
            {
                ItemPool.CopyActiveTo(_nearbyPickups);
                float radiusSquared = _collectionRadius * _collectionRadius;
                foreach (var pickup in _nearbyPickups)
                {
                    if (pickup == null || ((Vector2)(pickup.transform.position - transform.position)).sqrMagnitude > radiusSquared)
                        continue;
                    if (pickup.CollectNow())
                    {
                        _collectedPickups++;
                        HitImpact.Spawn(pickup.transform.position, _dir, false);
                    }
                }
            }

            if (team == 0 && _ricochetEnabled && !ArenaBounds.Contains(transform.position))
            {
                if (_bouncesRemaining <= 0)
                {
                    Release();
                    return;
                }
                Rect bounds = ArenaBounds.Rect;
                Vector3 p = transform.position;
                bool hitX = p.x <= bounds.xMin || p.x >= bounds.xMax;
                bool hitY = p.y <= bounds.yMin || p.y >= bounds.yMax;
                p.x = Mathf.Clamp(p.x, bounds.xMin + 0.02f, bounds.xMax - 0.02f);
                p.y = Mathf.Clamp(p.y, bounds.yMin + 0.02f, bounds.yMax - 0.02f);
                transform.position = p;
                if (hitX) _dir.x = -_dir.x;
                if (hitY) _dir.y = -_dir.y;
                _bouncesRemaining--;
                damage *= _bounceDamageMultiplier;
                _hitEnemies.Clear();
                FaceDirection();
                HitImpact.Spawn(p, _dir, false);
            }

            if (team == 0 && explodeRadius > 0f && !ArenaBounds.Contains(transform.position))
            {
                transform.position = ArenaBounds.Clamp(transform.position);
                Detonate();
                return;
            }

            if (team == 0 && _vacuumRemaining > 0)
            {
                if (_vacuumHits == null) _vacuumHits = new Collider2D[32];
                int count = Physics2D.OverlapCircleNonAlloc(transform.position, _vacuumRadius, _vacuumHits);
                for (int i = 0; i < count && _vacuumRemaining > 0; i++)
                {
                    var hit = _vacuumHits[i];
                    var bullet = hit != null ? hit.GetComponent<Projectile>() : null;
                    if (bullet == null || bullet == this || !bullet.TryAbsorbEnemyBullet()) continue;
                    _vacuumRemaining--;
                    damage *= 1f + _vacuumDamagePerAbsorb;
                    HitImpact.Spawn(hit.transform.position, _dir, false);
                }
            }

            if (team == 1 && _bossMagicVisual && !ArenaBounds.Contains(transform.position))
            {
                transform.position = ArenaBounds.Clamp(transform.position);
                BossVfxAtlas.SpawnMagicImpact(transform.position, 2.2f);
                RestoreVisual();
                Release();
                return;
            }

            // 回旋镖：半程折返向出生点
            if (boomerang && _out && _travelled >= maxDistance * 0.5f)
            {
                _out = false;
                damage *= _returnDamageMultiplier *
                    (1f + Mathf.Min(_collectedPickups, _maxPickupBonusCount) * _returnBonusPerPickup);
                speed *= _returnSpeedMultiplier;
                _hitEnemies.Clear();
            }

            if (_travelled >= maxDistance * (boomerang ? 2f : 1f))
            {
                if (explodeRadius > 0f) Detonate();
                else if (team == 1 && _bossMagicVisual)
                {
                    BossVfxAtlas.SpawnMagicImpact(transform.position, 2.2f);
                    RestoreVisual();
                    Release();
                }
                else Release();
                return;
            }

            // 敌方弹幕：物理 trigger 在 kinematic-kinematic 组合下可能不触发，补距离命中兜底
            if (team == 1)
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.Player != null)
                {
                    const float hitDist = 0.45f + 0.12f; // 玩家半径 + 弹体半径
                    if (Vector2.Distance(transform.position, gm.Player.transform.position) <= hitDist)
                    {
                        gm.Player.Health.TakeDamage(new DamageInfo(damage, gameObject));
                        HandleHit();
                    }
                }
            }
            else if (team == 2 && _friendlyTarget != null && _friendlyTarget.IsAlive &&
                     !_friendlyTarget.IsCharmed &&
                     Vector2.Distance(transform.position, _friendlyTarget.transform.position) <=
                     _friendlyTarget.BodyCollider.radius * _friendlyTarget.transform.localScale.x + 0.14f)
            {
                HitFriendlyEnemy(_friendlyTarget);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (team == 0)
            {
                var enemy = other.GetComponent<Enemy>();
                if (enemy == null || enemy.IsCharmed || !_hitEnemies.Add(enemy)) return;
                // 爆炸伤害由范围结算统一处理，避免直接命中目标承受两次完整伤害。
                if (explodeRadius > 0f) HandleHit();
                else HitEnemy(enemy);
            }
            else if (team == 2)
            {
                var enemy = other.GetComponent<Enemy>();
                if (enemy != null) HitFriendlyEnemy(enemy);
            }
            else
            {
                var ph = other.GetComponent<PlayerHealth>();
                if (ph == null) return;
                ph.TakeDamage(new DamageInfo(damage, gameObject));
                HandleHit();
            }
        }

        private void HitFriendlyEnemy(Enemy enemy)
        {
            if (enemy == null || !enemy.IsAlive || enemy == _friendlyOwner || enemy.IsCharmed ||
                !_hitEnemies.Add(enemy)) return;
            enemy.TakeDamage(new DamageInfo(damage,
                _friendlyOwner != null && _friendlyOwner.IsAlive ? _friendlyOwner.gameObject : gameObject));
            HandleHit();
        }

        private void HitEnemy(Enemy enemy)
        {
            enemy.TakeDamage(new DamageInfo(damage, gameObject, isCrit,
                _sourceStats != null ? Mathf.Max(0f, _sourceStats.Get(StatType.Knockback)) : 0f));
            if (enemy.IsAlive && _charmChance > 0f && Random.value < _charmChance)
                enemy.ApplyCharm(_charmDuration, _charmDamagePerSecond);
            ApplyLifeSteal(damage);
            HandleHit();
        }

        private void ApplyLifeSteal(float dealt)
        {
            if (_sourceStats == null || dealt <= 0f) return;
            float steal = Mathf.Max(0f, _sourceStats.Get(StatType.LifeSteal));
            if (steal <= 0f) return;
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player != null) player.Health.Heal(dealt * steal);
        }

        private void HandleHit()
        {
            if (team == 1 && _bossMagicVisual)
            {
                BossVfxAtlas.SpawnMagicImpact(transform.position, 2.2f);
                RestoreVisual();
            }
            if (explodeRadius > 0f)
            {
                Explode();
                Release();
                return;
            }
            if (boomerang)
            {
                // 去程/回程各可击中多个目标；切换回程时清空单程命中记录。
                return;
            }
            pierce--;
            if (pierce < 0) Release();
        }

        public void Detonate()
        {
            if (explodeRadius <= 0f) return;
            Explode();
            Release();
        }

        private void Explode()
        {
            ExplosionEffect.Spawn(transform.position, explodeRadius);
            if (_areaHits == null) _areaHits = new Collider2D[256];
            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, explodeRadius, _areaHits);
            Collider2D[] hits = _areaHits;
            if (hitCount == hits.Length)
            {
                hits = Physics2D.OverlapCircleAll(transform.position, explodeRadius);
                hitCount = hits.Length;
            }
            _areaDamaged.Clear();
            for (int i = 0; i < hitCount; i++)
            {
                var col = hits[i];
                if (team == 0)
                {
                    var enemy = col.GetComponent<Enemy>();
                    if (enemy != null && !enemy.IsCharmed && _areaDamaged.Add(enemy))
                    {
                        enemy.TakeDamage(new DamageInfo(damage, gameObject, isCrit,
                            _sourceStats != null ? Mathf.Max(0f, _sourceStats.Get(StatType.Knockback)) : 0f,
                            1.4f));
                        ApplyLifeSteal(damage);
                    }
                }
                else
                {
                    var ph = col.GetComponent<PlayerHealth>();
                    if (ph != null) ph.TakeDamage(new DamageInfo(damage, gameObject));
                }
            }
        }

        private void Release() => _pool?.Release(gameObject);

        private void FaceDirection()
        {
            if (boomerang) { transform.rotation = Quaternion.identity; return; }
            if (_dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg);
        }
    }
}
