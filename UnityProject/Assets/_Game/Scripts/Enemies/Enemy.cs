using System.Collections;
using RogueLike.Combat;
using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Items;
using RogueLike.Player;
using UnityEngine;

namespace RogueLike.Enemies
{
    /// <summary>
    /// 敌人：数据驱动行为（追/远程/自爆/坦克/分裂/Boss），波次缩放注入，受击/死亡流程。
    /// 死亡：掉落材料和经验、分裂、计数、回池。Boss 技能由各自配置选择。
    /// </summary>
    public class Enemy : MonoBehaviour, IDamageable
    {
        public EnemyData Data { get; private set; }
        public float Hp { get; private set; }
        public bool IsAlive => !_dead;
        public bool IsBoss => Data != null && Data.isBoss;
        public bool IsCharmed => !_dead && _charmTimer > 0f;
        public WeaponInstance LastHitWeapon { get; private set; }
        public CircleCollider2D BodyCollider => _col;

        public float HpScale { get; private set; } = 1f;
        public float DmgScale { get; private set; } = 1f;
        public float SpeedScale { get; private set; } = 1f;

        private SpriteRenderer _sr;
        private SpriteRenderer _flashRenderer;
        private static Material _whiteFlashMaterial;
        private CircleCollider2D _col;
        private Color _baseColor;
        private Color _restColor = Color.white; // 受击闪白后恢复的目标色（配图=白/原色，占位=配置色）
        private Color _normalRestColor = Color.white;
        private float _charmTimer;
        private float _charmDamagePerSecond;
        private float _charmAttackCooldown;
        private float _charmRetargetTimer;
        private Enemy _charmTarget;
        private float _honkTimer;
        private float _honkMoveMultiplier = 1f;
        private Color _honkTint;
        private float _scale = 1f;
        private float _flashTimer;
        private float _hitStunTimer;
        private static float _nextImpactShakeTime;
        private Vector2 _knockVel;
        private float _springCollisionTimer;
        private float _springCollisionDamage;
        private GameObject _springSource;
        private static readonly Collider2D[] SpringHits = new Collider2D[64];
        private float _contactCooldown;
        private float _rangedCooldown;
        private float _bossCooldown;
        private bool _bossCharging;
        private bool _dead;
        private ObjectPool _pool;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
            if (_whiteFlashMaterial == null)
            {
                var shader = Resources.Load<Shader>("Shaders/SpriteWhiteFlash");
                if (shader != null)
                {
                    _whiteFlashMaterial = new Material(shader);
                    _whiteFlashMaterial.SetFloat("_FlashAmount", 1f);
                }
            }
            if (_whiteFlashMaterial != null)
            {
                var flashVisual = new GameObject("HitFlashVisual");
                flashVisual.transform.SetParent(transform, false);
                _flashRenderer = flashVisual.AddComponent<SpriteRenderer>();
                _flashRenderer.sharedMaterial = _whiteFlashMaterial;
                _flashRenderer.enabled = false;
            }
            _col = GetComponent<CircleCollider2D>();
            if (_col == null) _col = gameObject.AddComponent<CircleCollider2D>();
            _col.isTrigger = true;
            var rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.isKinematic = true;
            }
        }

        public void Init(EnemyData data, float hpScale, float dmgScale, float speedScale, ObjectPool pool)
        {
            Data = data;
            HpScale = hpScale;
            DmgScale = dmgScale;
            SpeedScale = speedScale;
            Hp = data.maxHp * hpScale;
            _pool = pool;
            _dead = false;
            LastHitWeapon = null;
            _contactCooldown = 0f;
            _rangedCooldown = 1.2f;
            _bossCooldown = 3f;
            _knockVel = Vector2.zero;
            _springCollisionTimer = 0f;
            _springCollisionDamage = 0f;
            _springSource = null;
            _flashTimer = 0f;
            _hitStunTimer = 0f;
            _bossCharging = false;
            _charmTimer = 0f;
            _charmDamagePerSecond = 0f;
            _charmAttackCooldown = 0f;
            _charmRetargetTimer = 0f;
            _charmTarget = null;
            _honkTimer = 0f;
            _honkMoveMultiplier = 1f;

            float normalScale = GameDatabase.Balance != null ?
                Mathf.Max(0.1f, GameDatabase.Balance.normalEnemyScale) : 1.12f;
            _scale = data.isBoss ? 2.2f : data.isElite ? 1.6f : normalScale;
            transform.localScale = Vector3.one * _scale;

            _baseColor = DataLoader.ParseColor(data.color);
            // Prefab Inspector 中手工配置的 Sprite 优先；空槽才读取显式美术库。
            if (_sr.sprite == null)
            {
                var autoSprite = RogueLike.Core.AssetLoader.LoadEnemySprite(data.id);
                if (autoSprite != null) _sr.sprite = autoSprite;
            }

            if (_sr.sprite == null)
            {
                // 预制未配图：占位圆形按配置色显示
                _sr.sprite = SpriteFactory.Circle(_baseColor, data.radius);
                _restColor = _baseColor;
            }
            else
            {
                // 已配置素材时保持原图颜色，不按配置色覆盖。
                _restColor = Color.white;
            }
            _normalRestColor = _restColor;
            _sr.color = _restColor;
            _sr.sortingOrder = data.isBoss ? 6 : 2;
            if (_flashRenderer != null)
            {
                _flashRenderer.sprite = _sr.sprite;
                _flashRenderer.sortingLayerID = _sr.sortingLayerID;
                _flashRenderer.sortingOrder = _sr.sortingOrder + 1;
                _flashRenderer.enabled = false;
            }
            _col.radius = data.radius;

            EnemyManager.Register(this);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (_dead) return;
            LastHitWeapon = info.weapon;
            Hp -= info.amount;
            var balance = GameDatabase.Balance;
            _flashTimer = Mathf.Max(0.03f, balance != null ? balance.enemyHitFlashDuration : 0.12f);
            // A stream of tiny SMG hits must not permanently stun an enemy.
            if (info.isCrit || info.amount >= Data.maxHp * HpScale * 0.12f)
                _hitStunTimer = Mathf.Max(0f, balance != null ? balance.enemyHitStunDuration : 0.055f);
            SetWhiteFlash(1f);

            Vector2 away = Vector2.right;
            if (info.source != null)
            {
                away = (Vector2)(transform.position - info.source.transform.position);
                if (away.sqrMagnitude > 0.001f) away.Normalize();
                else away = Vector2.right;
                _knockVel += away * info.knockback;
            }

            Vector3 impactPoint = transform.position - (Vector3)away * (Data.radius * _scale * 0.75f);
            HitImpact.Spawn(impactPoint, away, info.isCrit);
            CombatAudio.PlayEnemyHit(info.isCrit);
            bool meleeHit = info.weapon != null && info.weapon.Data != null &&
                info.weapon.Data.kind == WeaponKind.Melee;
            if ((meleeHit || info.isCrit || info.impact > 0f) && Time.time >= _nextImpactShakeTime)
            {
                float magnitude = meleeHit
                    ? balance != null ? balance.meleeHitShakeMagnitude : 0.018f
                    : balance != null ? balance.impactShakeMagnitude : 0.025f;
                float duration = meleeHit
                    ? balance != null ? balance.meleeHitShakeDuration : 0.06f
                    : 0.075f;
                CameraShake.Shake(duration, magnitude * Mathf.Max(info.isCrit ? 1.5f : 1f, info.impact));
                _nextImpactShakeTime = Time.time + 0.10f;
            }

            DamageNumber.Spawn(transform.position + Vector3.up * 0.5f, info.amount, info.isCrit);

            if (Hp <= 0f) Die();
        }

        public void ApplyCharm(float duration, float damagePerSecond)
        {
            if (_dead || Data == null || Data.isBoss || Data.isElite || duration <= 0f) return;
            if (_charmTimer <= 0f)
            {
                _charmTarget = null;
                _charmRetargetTimer = 0f;
            }
            _charmTimer = Mathf.Max(_charmTimer, duration);
            _charmDamagePerSecond = Mathf.Max(_charmDamagePerSecond, damagePerSecond);
            _restColor = GameDatabase.Balance != null ? GameDatabase.Balance.charmTint :
                new Color(1f, 0.55f, 0.86f);
            if (_flashTimer <= 0f) _sr.color = _restColor;
        }

        /// <summary>音波只短暂拖慢普通怪，保留原本的近战/远程攻击类型。</summary>
        public void ApplyHonkSlow(float duration, float moveMultiplier, Color tint)
        {
            if (_dead || Data == null || Data.isBoss || Data.isElite || duration <= 0f) return;
            _honkTimer = Mathf.Max(_honkTimer, duration);
            _honkMoveMultiplier = Mathf.Min(_honkMoveMultiplier, Mathf.Clamp(moveMultiplier, 0.1f, 1f));
            _honkTint = tint;
            if (!IsCharmed)
            {
                _restColor = _honkTint;
                if (_flashTimer <= 0f) _sr.color = _restColor;
            }
        }

        public void ArmSpringCollision(float damage, float duration, GameObject source)
        {
            if (_dead || damage <= 0f) return;
            _springCollisionDamage = damage;
            _springCollisionTimer = Mathf.Max(0.05f, duration);
            _springSource = source;
        }

        private void ResolveSpringCollision(float deltaTime)
        {
            if (_springCollisionTimer <= 0f) return;
            _springCollisionTimer -= deltaTime;
            if (_knockVel.sqrMagnitude < 0.04f) return;
            float radius = Data.radius * _scale + 0.18f;
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, radius, SpringHits);
            for (int i = 0; i < count; i++)
            {
                var other = SpringHits[i] != null ? SpringHits[i].GetComponent<Enemy>() : null;
                if (other == null || other == this || !other.IsAlive || other.IsCharmed) continue;
                float dealt = _springCollisionDamage;
                _springCollisionTimer = 0f;
                other.TakeDamage(new DamageInfo(dealt, _springSource != null ? _springSource : gameObject));
                HitImpact.Spawn(transform.position, _knockVel.normalized, false);
                break;
            }
        }

        private void Die()
        {
            if (_dead) return;
            _dead = true;

            // 分裂
            if (Data.splitCount > 0 && !string.IsNullOrEmpty(Data.splitEnemyId))
            {
                for (int i = 0; i < Data.splitCount; i++)
                {
                    Vector2 off = Random.insideUnitCircle * 0.4f;
                    EnemyPool.Spawn(Data.splitEnemyId, transform.position + (Vector3)off,
                        HpScale * 0.6f, DmgScale, SpeedScale);
                }
            }

            // 材料掉落
            int m = Random.Range(Data.materialDropMin, Data.materialDropMax + 1);
            if (m > 0) ItemPool.Spawn(transform.position + Vector3.left * 0.24f, m);

            // 经验留在地面，玩家靠近后磁吸拾取。
            if (Data.expReward > 0)
                ItemPool.SpawnExperience(transform.position + Vector3.right * 0.24f, Data.expReward);

            if (GameManager.Instance != null) GameManager.Instance.AddKill();
            EnemyManager.Unregister(this);
            EventBus.RaiseEnemyKilled(this);

            if (_sr.sprite != null)
            {
                var deathPool = GamePools.Get("enemy_death_vfx", EnemyDeathVfx.CreateGo);
                int maxActive = GameDatabase.Balance != null ? GameDatabase.Balance.enemyDeathMaxActive : 30;
                if (deathPool.ActiveCount < maxActive)
                {
                    var deathGo = deathPool.Get(transform.position);
                    if (deathGo != null)
                    {
                        float duration = GameDatabase.Balance != null ? GameDatabase.Balance.enemyDeathDuration : 0.24f;
                        deathGo.GetComponent<EnemyDeathVfx>().Play(_sr, _restColor, transform.localScale,
                            duration, deathPool);
                    }
                }
            }

            if (_pool != null) _pool.Release(gameObject);
        }

        /// <summary>波次结束清场：直接回收，不掉落、不计击杀。</summary>
        public void Despawn()
        {
            if (_dead) return;
            _dead = true;
            EnemyManager.Unregister(this);
            if (_pool != null) _pool.Release(gameObject);
        }

        private void Update()
        {
            if (_dead) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                var balance = GameDatabase.Balance;
                float duration = Mathf.Max(0.03f, balance != null ? balance.enemyHitFlashDuration : 0.12f);
                float intensity = Mathf.Clamp01(_flashTimer / duration);
                SetWhiteFlash(intensity);
                float squash = balance != null ? balance.enemyHitSquash : 0.16f;
                transform.localScale = new Vector3(_scale * (1f + squash * intensity),
                    _scale * (1f - squash * intensity), _scale);
                if (_flashTimer <= 0f)
                {
                    SetWhiteFlash(0f);
                    transform.localScale = Vector3.one * _scale;
                }
            }

            if (_hitStunTimer > 0f) _hitStunTimer -= Time.deltaTime;

            if (_honkTimer > 0f)
            {
                _honkTimer -= Time.deltaTime;
                if (_honkTimer <= 0f)
                {
                    _honkMoveMultiplier = 1f;
                    _restColor = IsCharmed
                        ? GameDatabase.Balance != null ? GameDatabase.Balance.charmTint :
                            new Color(1f, 0.55f, 0.86f)
                        : _normalRestColor;
                    if (_flashTimer <= 0f) _sr.color = _restColor;
                }
            }

            _knockVel = Vector2.MoveTowards(_knockVel, Vector2.zero, 14f * Time.deltaTime);

            if (_charmTimer > 0f)
            {
                _charmTimer -= Time.deltaTime;
                if (_charmTimer <= 0f)
                {
                    _charmTarget = null;
                    _restColor = _honkTimer > 0f ? _honkTint : _normalRestColor;
                    if (_flashTimer <= 0f) _sr.color = _restColor;
                }
                else
                {
                    var balance = GameDatabase.Balance;
                    _charmRetargetTimer -= Time.deltaTime;
                    if (_charmRetargetTimer <= 0f || _charmTarget == null || !_charmTarget.IsAlive ||
                        _charmTarget.IsCharmed)
                    {
                        _charmTarget = EnemyManager.NearestOther(transform.position,
                            balance != null ? Mathf.Max(0.1f, balance.charmTargetRange) : 12f, this);
                        _charmRetargetTimer = balance != null
                            ? Mathf.Max(0.05f, balance.charmRetargetInterval) : 0.25f;
                    }
                    var victim = _charmTarget;
                    if (victim != null)
                    {
                        Vector2 delta = victim.transform.position - transform.position;
                        float distance = delta.magnitude;
                        float charmedMoveSpeed = Data.moveSpeed * SpeedScale * _honkMoveMultiplier;
                        Vector2 movement = delta.normalized * charmedMoveSpeed;
                        // Keep the original ranged enemy's preferred distance and projectile attack.
                        if (Data.ranged && Data.behavior == 1)
                        {
                            if (distance < 3.5f) movement = -delta.normalized * charmedMoveSpeed;
                            else if (distance <= 5.5f) movement = Vector2.Perpendicular(delta).normalized * charmedMoveSpeed * 0.6f;
                        }
                        if (_hitStunTimer <= 0f) transform.position += (Vector3)(movement * Time.deltaTime);
                        _charmAttackCooldown -= Time.deltaTime;
                        if (Data.behavior == 2 && distance <= Data.explodeRadius + 0.3f)
                        {
                            ExplodeCharmed();
                            return;
                        }
                        if (Data.ranged)
                        {
                            if (distance <= 6.5f && _charmAttackCooldown <= 0f)
                            {
                                _charmAttackCooldown = Mathf.Max(0.1f, Data.rangedInterval);
                                ShootRangedAt(victim);
                            }
                        }
                        else
                        {
                            float reach = (Data.radius * _scale + victim.Data.radius * victim.transform.localScale.x) * 0.9f;
                            if (distance <= reach && _charmAttackCooldown <= 0f)
                            {
                                _charmAttackCooldown = balance != null
                                    ? Mathf.Max(0.1f, balance.charmAttackInterval) : 0.5f;
                                float charmedDamage = Mathf.Max(Data.contactDamage * DmgScale,
                                    _charmDamagePerSecond * _charmAttackCooldown);
                                victim.TakeDamage(new DamageInfo(charmedDamage, gameObject));
                            }
                        }
                    }
                    transform.position += (Vector3)_knockVel * Time.deltaTime;
                    transform.position = ArenaBounds.Clamp(transform.position);
                    return;
                }
            }

            Vector2 toPlayer = (Vector2)(gm.Player.transform.position - transform.position);
            float dist = toPlayer.magnitude;
            float speed = Data.moveSpeed * SpeedScale * _honkMoveMultiplier;
            Vector2 move = Vector2.zero;

            switch (Data.behavior)
            {
                case 0: // 近战追击
                case 3: // 坦克
                case 4: // 分裂怪
                    move = toPlayer.normalized * speed;
                    break;
                case 1: // 远程风筝
                    if (dist > 5.5f) move = toPlayer.normalized * speed;
                    else if (dist < 3.5f) move = -toPlayer.normalized * speed;
                    else move = Vector2.Perpendicular(toPlayer).normalized * speed * 0.6f;
                    break;
                case 2: // 自爆逼近
                    move = toPlayer.normalized * speed;
                    break;
                case 5: // Boss
                    move = toPlayer.normalized * speed;
                    break;
            }

            if (_hitStunTimer > 0f || _bossCharging) move = Vector2.zero;
            transform.position += (Vector3)(move * Time.deltaTime);
            if (!_bossCharging) transform.position += (Vector3)_knockVel * Time.deltaTime;
            transform.position = ArenaBounds.Clamp(transform.position);
            ResolveSpringCollision(Time.deltaTime);

            // 远程攻击
            if (Data.ranged && Data.behavior != 5)
            {
                _rangedCooldown -= Time.deltaTime;
                if (dist <= 6.5f && _rangedCooldown <= 0f)
                {
                    _rangedCooldown = Data.rangedInterval;
                    ShootRanged();
                }
            }

            // 自爆
            if (Data.behavior == 2 && dist <= Data.explodeRadius + 0.3f)
            {
                ExplodeSelf();
                return;
            }

            // Boss 技能
            if (Data.behavior == 5)
            {
                _bossCooldown -= Time.deltaTime;
                if (_bossCooldown <= 0f && dist <= 9f)
                {
                    _bossCooldown = 4.5f;
                    StartCoroutine(BossSkillRoutine());
                }
            }

            // 接触伤害
            _contactCooldown -= Time.deltaTime;
            float contactDist = Data.radius * _scale + 0.45f;
            if (dist <= contactDist && _contactCooldown <= 0f)
            {
                _contactCooldown = 0.5f;
                gm.Player.Health.TakeDamage(new DamageInfo(Data.contactDamage * DmgScale, gameObject));
            }
        }

        private void ShootRanged()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;
            Vector2 dir = ((Vector2)(gm.Player.transform.position - transform.position)).normalized;
            var pool = GamePools.Get("enemy_bullet", BuildEnemyBulletGo);
            var go = pool.Get(transform.position);
            var proj = go.GetComponent<Projectile>();
            proj.Launch(transform.position, dir, 1, Data.rangedDamage * DmgScale,
                Data.projectileSpeed, 8f, 0, 0f, false, false, pool, null);
            SetEnemyBulletArt(go, "enemy_spore", Color.white);
        }

        private void ShootRangedAt(Enemy target)
        {
            if (target == null || !target.IsAlive || Data == null) return;
            Vector2 dir = ((Vector2)(target.transform.position - transform.position)).normalized;
            var pool = GamePools.Get("enemy_bullet", BuildEnemyBulletGo);
            var go = pool.Get(transform.position);
            var projectile = go.GetComponent<Projectile>();
            float charmedDamage = Mathf.Max(Data.rangedDamage * DmgScale,
                _charmDamagePerSecond * Mathf.Max(0.1f, Data.rangedInterval));
            projectile.Launch(transform.position, dir, 2, charmedDamage,
                Data.projectileSpeed, 8f, 0, 0f, false, false, pool, null);
            SetEnemyBulletArt(go, "enemy_elite", GameDatabase.Balance != null
                ? GameDatabase.Balance.charmTint : new Color(1f, 0.75f, 0.95f));
            projectile.ConfigureFriendlyEnemyShot(this, target);
        }

        private void ExplodeSelf()
        {
            ExplosionEffect.Spawn(transform.position, Data.explodeRadius);
            var gm = GameManager.Instance;
            if (gm != null && gm.Player != null)
            {
                float d = Vector2.Distance(gm.Player.transform.position, transform.position);
                if (d <= Data.explodeRadius + 0.45f)
                    gm.Player.Health.TakeDamage(new DamageInfo(Data.contactDamage * 2f * DmgScale, gameObject));
            }
            Die();
        }

        private void ExplodeCharmed()
        {
            float radius = Mathf.Max(0.1f, Data.explodeRadius);
            ExplosionEffect.Spawn(transform.position, radius);
            var hits = Physics2D.OverlapCircleAll(transform.position, radius);
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<Enemy>();
                if (enemy == null || enemy == this || !enemy.IsAlive || enemy.IsCharmed) continue;
                enemy.TakeDamage(new DamageInfo(Data.contactDamage * 2f * DmgScale, gameObject));
            }
            Die();
        }

        private IEnumerator BossSkillRoutine()
        {
            bool redThrow = Data.bossAttackPattern == 0;
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) yield break;

            // 红色攻击锁定弹道；魔法攻击先在魔王身边蓄力，再锁定落点预警。
            _bossCharging = true;
            _knockVel = Vector2.zero;
            Vector3 launchOrigin = transform.position;
            Vector3 target = gm.Player.transform.position;
            float impactRadius = Mathf.Max(0.5f, Data.bossImpactRadius);
            float warningDuration = 0.85f;
            if (!redThrow)
            {
                var balance = GameDatabase.Balance;
                float chargeDuration = balance != null ? balance.bossMagicChargeDuration : 0.32f;
                warningDuration = balance != null ? balance.bossMagicWarningDuration : 0.8f;
                var charge = BossVfxAtlas.SpawnMagicCharge(transform, chargeDuration);
                yield return new WaitForSeconds(Mathf.Max(0.05f, chargeDuration));
                if (charge != null) charge.EndCharge();
                if (_dead || GameManager.Instance == null || GameManager.Instance.Player == null)
                {
                    _bossCharging = false;
                    yield break;
                }
                target = GameManager.Instance.Player.transform.position;
            }
            var wpool = GamePools.Get("boss_warning", BossWarning.CreateGo);
            var wgo = wpool.Get(launchOrigin);
            var w = wgo.GetComponent<BossWarning>();
            if (redThrow) w.ShowDirectional(launchOrigin, target, 1.8f, warningDuration, wpool);
            else w.ShowMagicArea(target, impactRadius, warningDuration, wpool);

            yield return new WaitForSeconds(warningDuration);
            _bossCharging = false;

            if (_dead || GameManager.Instance == null || GameManager.Instance.Player == null) yield break;

            if (redThrow)
            {
                // Launch a visible red impact attack toward the locked location after the warning.
                BossVfxAtlas.SpawnRedThrow(launchOrigin, target, 1.8f, Mathf.Min(3.2f, impactRadius * 1.8f),
                    impactRadius, Data.bossAttackDamage * DmgScale, Data.projectileSpeed, this);
                yield break;
            }

            // 魔王的法术落在预警时锁定的地点，不生成任何普通弹丸。
            BossVfxAtlas.SpawnMagicImpact(target, impactRadius * 2f);
            var player = GameManager.Instance.Player;
            var playerCollider = player.GetComponent<CircleCollider2D>();
            Vector2 playerCenter = playerCollider != null
                ? playerCollider.transform.TransformPoint(playerCollider.offset)
                : player.transform.position;
            float playerRadius = playerCollider != null
                ? playerCollider.radius * Mathf.Max(playerCollider.transform.lossyScale.x, playerCollider.transform.lossyScale.y)
                : 0.35f;
            float hitDistance = impactRadius + playerRadius;
            if (((Vector2)target - playerCenter).sqrMagnitude <= hitDistance * hitDistance)
                player.Health.TakeDamage(new DamageInfo(Data.bossAttackDamage * DmgScale, gameObject));
        }

        public static GameObject BuildEnemyBulletGo()
        {
            var prefab = PrefabProvider.Instantiate("Shared/enemy_bullet", PoolRoot.Root);
            if (prefab != null)
            {
                var prefabSr = prefab.GetComponent<SpriteRenderer>();
                if (prefabSr != null)
                    prefabSr.sprite = AssetLoader.LoadProjectileSprite("enemy_spore") ??
                        SpriteFactory.Circle(new Color(1f, 0.35f, 0.35f), 0.11f);
                return prefab;
            }

            var go = new GameObject("EnemyBullet");
            go.transform.SetParent(PoolRoot.Root);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetLoader.LoadProjectileSprite("enemy_spore") ??
                SpriteFactory.Circle(new Color(1f, 0.35f, 0.35f), 0.11f);
            sr.sortingOrder = 3;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.11f;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.isKinematic = true;
            go.AddComponent<Projectile>();
            return go;
        }

        private static void SetEnemyBulletArt(GameObject bullet, string spriteId, Color tint)
        {
            var renderer = bullet != null ? bullet.GetComponent<SpriteRenderer>() : null;
            if (renderer == null) return;
            renderer.sprite = AssetLoader.LoadProjectileSprite(spriteId) ??
                AssetLoader.LoadProjectileSprite("enemy_spore") ?? renderer.sprite;
            renderer.color = tint;
        }

        private void SetWhiteFlash(float amount)
        {
            if (_flashRenderer == null) return;
            float alpha = Mathf.Clamp01(amount);
            _flashRenderer.enabled = alpha > 0.001f;
            if (_flashRenderer.enabled)
                _flashRenderer.color = new Color(1f, 1f, 1f, alpha);
        }
    }
}
