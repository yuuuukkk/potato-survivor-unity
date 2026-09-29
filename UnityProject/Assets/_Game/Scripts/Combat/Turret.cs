using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Enemies;
using RogueLike.Player;
using UnityEngine;

namespace RogueLike.Combat
{
    /// <summary>跟随玩家盘旋的无人机，自行索敌并从机头发射。</summary>
    public class Turret : MonoBehaviour
    {
        private WeaponSystem _owner;
        private WeaponInstance _weapon;
        public WeaponInstance Weapon => _weapon;
        private PlayerStats _stats;
        private ObjectPool _pool;
        private SpriteRenderer _sr;
        private float _cooldown;
        private float _orbitAngle;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        }

        public void Init(WeaponSystem owner, WeaponInstance weapon, PlayerStats stats, ObjectPool pool)
        {
            _owner = owner;
            _weapon = weapon;
            _stats = stats;
            _pool = pool;
            _cooldown = 0f;
            _orbitAngle = Random.Range(0f, 360f);
            _sr.sprite = AssetLoader.LoadWeaponSprite(weapon.Data.id)
                ?? SpriteFactory.Circle(DataLoader.ParseColor(weapon.Data.bulletColor), 0.4f);
            _sr.color = Color.white;
            _sr.sortingOrder = 7;
            float visibleSize = Mathf.Max(_sr.sprite.bounds.size.x, _sr.sprite.bounds.size.y);
            var vertices = _sr.sprite.vertices;
            if (vertices != null && vertices.Length > 0)
            {
                float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                foreach (var v in vertices)
                {
                    minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                    minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                }
                visibleSize = Mathf.Max(maxX - minX, maxY - minY);
            }
            float displaySize = weapon.Data.droneVisualSize > 0f ? weapon.Data.droneVisualSize : 0.9f;
            transform.localScale = Vector3.one * (displaySize / Mathf.Max(0.01f, visibleSize));
            var col = GetComponent<CircleCollider2D>();
            if (col != null) col.enabled = false;
        }

        public void Despawn()
        {
            if (_pool != null) _pool.Release(gameObject);
        }

        private void Update()
        {
            if (_owner == null || _stats == null) { Despawn(); return; }
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

            var data = _weapon.Data;
            _orbitAngle += (data.droneOrbitSpeed > 0f ? data.droneOrbitSpeed : 120f) * Time.deltaTime;
            float orbitRadius = data.droneOrbitRadius > 0f ? data.droneOrbitRadius : 1.05f;
            var orbitOffset = new Vector3(Mathf.Cos(_orbitAngle * Mathf.Deg2Rad),
                Mathf.Sin(_orbitAngle * Mathf.Deg2Rad), 0f) * orbitRadius;
            transform.position = Vector3.Lerp(transform.position,
                _owner.transform.position + orbitOffset, Mathf.Clamp01(16f * Time.deltaTime));

            _cooldown -= Time.deltaTime;
            float range = _weapon.EffectiveRange(_stats);
            var target = EnemyManager.Nearest(transform.position, range);
            if (target == null) return;

            Vector2 dir = ((Vector2)(target.transform.position - transform.position)).normalized;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            _sr.flipY = dir.x < 0f;
            if (_cooldown > 0f) return;
            bool crit = Random.value < _stats.Get(StatType.CritChance) + data.critChance;
            float damage = _weapon.EffectiveDamage(_stats);
            if (crit) damage *= _stats.Get(StatType.CritDamageMult);

            var pool = WeaponPool.Bullets(data);
            if (pool == null) return;
            Vector3 muzzle = transform.position + (Vector3)dir *
                ((data.droneVisualSize > 0f ? data.droneVisualSize : 0.9f) * 0.42f);
            var go = pool.Get(muzzle);
            var proj = go.GetComponent<Projectile>();
            HitImpact.Spawn(muzzle, dir, crit);
            proj.Launch(muzzle, dir, 0, damage, data.projectileSpeed,
                range, data.pierce, data.explodeRadius, crit, false, pool, _stats);

            _cooldown = _weapon.EffectiveInterval(_stats);
        }
    }
}
