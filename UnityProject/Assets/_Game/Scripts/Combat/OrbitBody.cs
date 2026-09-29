using System.Collections.Generic;
using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Enemies;
using RogueLike.Player;
using UnityEngine;

namespace RogueLike.Combat
{
    /// <summary>
    /// 环绕体：围绕玩家旋转的切割体，对接触敌人按间隔结算伤害（每敌人独立冷却）。
    /// 数据来自 Orbit 类武器（如回旋镖）。
    /// </summary>
    public class OrbitBody : MonoBehaviour
    {
        private float _angle;
        private WeaponSystem _owner;
        private WeaponInstance _weapon;
        public WeaponInstance Weapon => _weapon;
        private PlayerStats _stats;
        private ObjectPool _pool;
        private SpriteRenderer _sr;
        private readonly Dictionary<Enemy, float> _lastHit = new Dictionary<Enemy, float>();

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
            var col = GetComponent<CircleCollider2D>();
            if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            var rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.isKinematic = true;
            }
        }

        public void Init(WeaponSystem owner, WeaponInstance weapon, PlayerStats stats, ObjectPool pool)
        {
            _owner = owner;
            _weapon = weapon;
            _stats = stats;
            _pool = pool;
            _angle = Random.value * 360f;

            if (_sr.sprite == null) _sr.sprite = SpriteFactory.Circle(DataLoader.ParseColor(weapon.Data.bulletColor), 0.18f);
            _sr.sortingOrder = 4;
            var col = GetComponent<CircleCollider2D>();
            col.radius = 0.18f;
            _lastHit.Clear();
        }

        private void OnEnable() => EventBus.EnemyKilled += OnEnemyKilled;
        private void OnDisable() => EventBus.EnemyKilled -= OnEnemyKilled;
        private void OnEnemyKilled(Enemy e) => _lastHit.Remove(e);

        public void Despawn()
        {
            if (_pool != null) _pool.Release(gameObject);
        }

        private void Update()
        {
            if (_owner == null) { Despawn(); return; }
            _angle += 260f * Time.deltaTime;
            Vector2 center = _owner.transform.position;
            float radius = _weapon != null ? _weapon.EffectiveRange(_stats) : 0f;
            Vector2 offset = new Vector2(Mathf.Cos(_angle * Mathf.Deg2Rad), Mathf.Sin(_angle * Mathf.Deg2Rad)) * radius;
            transform.position = center + offset;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            var enemy = other.GetComponent<Enemy>();
            if (enemy == null || enemy.Hp <= 0f) return;
            float now = Time.time;
            float interval = _weapon != null ? _weapon.EffectiveInterval(_stats) : 1f;
            if (_lastHit.TryGetValue(enemy, out float t) && now - t < interval) return;
            _lastHit[enemy] = now;
            float damage = _weapon != null ? _weapon.EffectiveDamage(_stats) : 0f;
            enemy.TakeDamage(new DamageInfo(damage, gameObject, false,
                Mathf.Max(0f, _stats.Get(StatType.Knockback))));
            _owner.ApplyLifeSteal(damage);
        }
    }
}
