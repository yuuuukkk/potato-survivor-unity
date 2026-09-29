using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Enemies;
using RogueLike.Player;
using UnityEngine;

namespace RogueLike.Combat
{
    /// <summary>
    /// 武器系统（挂在玩家身上）：槽位管理 + 冷却 + 索敌开火。
    /// 每种武器的池按 key 懒创建；开火行为由 WeaponInstance 按 WeaponKind 分派。
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class WeaponSystem : MonoBehaviour
    {
        public int MaxSlots = 6;

        private readonly System.Collections.Generic.List<WeaponInstance> _weapons = new System.Collections.Generic.List<WeaponInstance>();
        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<WeaponModificationData>> _unlockedModifications =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<WeaponModificationData>>();
        private readonly System.Collections.Generic.Dictionary<string, int> _modificationLevels =
            new System.Collections.Generic.Dictionary<string, int>();
        private readonly System.Collections.Generic.List<OrbitBody> _orbits = new System.Collections.Generic.List<OrbitBody>();
        private readonly System.Collections.Generic.List<Turret> _turrets = new System.Collections.Generic.List<Turret>();
        private readonly System.Collections.Generic.List<Enemy> _meleeCandidates =
            new System.Collections.Generic.List<Enemy>(128);
        private readonly System.Collections.Generic.Dictionary<WeaponInstance, MeleeAttackState> _meleeAttacks =
            new System.Collections.Generic.Dictionary<WeaponInstance, MeleeAttackState>();

        private sealed class MeleeAttackState
        {
            public float Damage;
            public float Knockback;
            public float SpringCollisionDamage;
            public float SpringCollisionWindow;
            public bool Critical;
            public Vector2 Aim;
            public bool WaveEmitted;
            public readonly System.Collections.Generic.HashSet<Enemy> HitEnemies =
                new System.Collections.Generic.HashSet<Enemy>();
        }

        private PlayerStats _stats;
        private WeaponVisuals _visuals;
        private PlayerController _controller;
        private CharacterData _character;

        public PlayerStats Stats => _stats;
        public System.Collections.Generic.IReadOnlyList<WeaponInstance> Weapons => _weapons;

        public System.Collections.Generic.IReadOnlyList<WeaponModificationData> UnlockedModifications(string weaponId)
        {
            var result = new System.Collections.Generic.List<WeaponModificationData>();
            if (!string.IsNullOrEmpty(weaponId) && _unlockedModifications.TryGetValue(weaponId, out var list)) result.AddRange(list);
            if (_unlockedModifications.TryGetValue("*", out var universal)) result.AddRange(universal);
            return result;
        }
        public int ModificationLevel(string id) => _modificationLevels.TryGetValue(id, out var level) ? level : 0;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _visuals = GetComponent<WeaponVisuals>();
            _controller = GetComponent<PlayerController>();
        }

        public void Setup(CharacterData character)
        {
            Reset();
            _character = character;
            if (character != null)
            {
                MaxSlots = Mathf.Max(1, character.maxWeaponSlots);
                foreach (var entry in character.startingWeapons)
                {
                    var data = GameDatabase.GetWeapon(entry.weaponId);
                    if (data != null) AddWeapon(data, entry.level);
                }
            }
        }

        public void Reset()
        {
            foreach (var o in _orbits) if (o != null) o.Despawn();
            foreach (var t in _turrets) if (t != null) t.Despawn();
            _orbits.Clear();
            _turrets.Clear();
            _weapons.Clear();
            _unlockedModifications.Clear();
            _modificationLevels.Clear();
            _meleeAttacks.Clear();
            EventBus.RaiseWeaponsChanged();
        }

        public bool CanUse(WeaponData data) => data != null && (_character == null || _character.Allows(data.kind));

        public bool CanApplyModification(WeaponModificationData modification)
        {
            if (modification == null || !modification.IsValid) return false;
            return FindModifiableWeapon(modification) != null &&
                ModificationLevel(modification.id) < modification.MaxLevel;
        }

        public bool HasStandardProjectileWeapon => _weapons.Exists(w => w.Data.kind == WeaponKind.Projectile &&
            w.Data.attackPattern == WeaponAttackPattern.StandardProjectile && w.Data.explodeRadius <= 0f);

        private WeaponInstance FindModifiableWeapon(WeaponModificationData modification)
        {
            return _weapons.Find(w => modification.AppliesTo(w.Data) && CanUse(w.Data) &&
                (modification.kind == WeaponModificationKind.MeleeAnimationOverride && w.Data.kind == WeaponKind.Melee ||
                 modification.kind == WeaponModificationKind.MeleeWave && w.Data.kind == WeaponKind.Melee ||
                 modification.kind == WeaponModificationKind.BoomerangReturnSurge && w.Data.kind == WeaponKind.Boomerang ||
                 modification.kind == WeaponModificationKind.BoomerangCollector && w.Data.kind == WeaponKind.Boomerang ||
                 modification.kind == WeaponModificationKind.SpringPunch &&
                    w.Data.id == "fist" && w.Data.kind == WeaponKind.Melee ||
                 modification.kind == WeaponModificationKind.VacuumRocket &&
                    w.Data.kind == WeaponKind.Projectile && w.Data.explodeRadius > 0f &&
                    w.Data.attackPattern == WeaponAttackPattern.StandardProjectile ||
                 (modification.kind == WeaponModificationKind.Ricochet ||
                  modification.kind == WeaponModificationKind.Piercing ||
                  modification.kind == WeaponModificationKind.EnlargedProjectile ||
                  modification.kind == WeaponModificationKind.CharmProjectile) &&
                 w.Data.kind == WeaponKind.Projectile &&
                 w.Data.attackPattern == WeaponAttackPattern.StandardProjectile &&
                 (w.Data.explodeRadius <= 0f ||
                  modification.kind == WeaponModificationKind.EnlargedProjectile)));
        }

        public bool TryApplyModification(WeaponModificationData modification)
        {
            if (!CanApplyModification(modification)) return false;
            var weapon = FindModifiableWeapon(modification);
            if (weapon == null) return false;
            if (!_unlockedModifications.TryGetValue(modification.weaponId, out var unlocked))
            {
                unlocked = new System.Collections.Generic.List<WeaponModificationData>();
                _unlockedModifications.Add(modification.weaponId, unlocked);
            }
            if (!unlocked.Exists(m => m.id == modification.id)) unlocked.Add(modification);
            _modificationLevels[modification.id] = ModificationLevel(modification.id) + 1;
            foreach (var owned in _weapons)
                if (owned.HasModification(modification.id))
                    owned.RefreshModificationLevels(ModificationLevel);
            // Buying unlocks the model's branch; equipment stays an explicit per-copy choice.
            if (modification.kind == WeaponModificationKind.MeleeWave)
                WeaponPool.Prewarm(weapon.Data, 8);
            EventBus.RaiseWeaponsChanged();
            return true;
        }

        public bool EquipModification(int weaponIndex, string modificationId)
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Shop ||
                weaponIndex < 0 || weaponIndex >= _weapons.Count) return false;
            var weapon = _weapons[weaponIndex];
            var unlocked = UnlockedModifications(weapon.Data.id);
            foreach (var modification in unlocked)
                if (modification.id == modificationId)
                {
                    if (!weapon.SetModificationForGroup(modification, ModificationLevel(modification.id))) return false;
                    EventBus.RaiseWeaponsChanged();
                    return true;
                }
            return false;
        }

        public bool UnequipModifications(int weaponIndex)
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Shop ||
                weaponIndex < 0 || weaponIndex >= _weapons.Count) return false;
            bool changed = _weapons[weaponIndex].ClearModifications();
            if (changed) EventBus.RaiseWeaponsChanged();
            return changed;
        }

        public bool UnequipModification(int weaponIndex, string modificationId)
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Shop ||
                weaponIndex < 0 || weaponIndex >= _weapons.Count) return false;
            bool changed = _weapons[weaponIndex].RemoveModification(modificationId);
            if (changed) EventBus.RaiseWeaponsChanged();
            return changed;
        }

        public bool CanAcquire(WeaponData data, int level)
        {
            if (!CanUse(data)) return false;
            int tier = Mathf.Clamp(level, 1, MaxTier);
            // Brotato 规则：有空槽时先保留重复武器；只有槽位已满，重复武器才可购入并合并。
            if (_weapons.Count < MaxSlots) return true;
            if (tier < MaxTier && _weapons.Exists(w => w.Data.id == data.id && w.Level == tier)) return true;
            return false;
        }

        private static int MaxTier => Mathf.Clamp(GameDatabase.WaveConfig != null ? GameDatabase.WaveConfig.weaponMaxLevel : 4, 1, 4);

        /// <summary>重复武器先占独立槽；槽满后获得相同等级武器时自动合并。</summary>
        public bool AddWeapon(WeaponData data, int level = 1, int paidValue = 0)
        {
            if (!CanAcquire(data, level)) return false;
            int tier = Mathf.Clamp(level, 1, MaxTier);
            var existing = _weapons.Count >= MaxSlots && tier < MaxTier
                ? _weapons.Find(w => w.Data.id == data.id && w.Level == tier) : null;
            if (existing != null)
            {
                existing.AbsorbValue(paidValue > 0 ? paidValue : Mathf.RoundToInt(data.basePrice * RarityInfo.PriceMult((Rarity)(tier - 1))));
                existing.LevelUp();
            }
            else
            {
                existing = new WeaponInstance(data, tier, paidValue);
                _weapons.Add(existing);
                if ((data.kind == WeaponKind.Projectile && data.attackPattern == WeaponAttackPattern.StandardProjectile) || data.kind == WeaponKind.Boomerang ||
                    data.kind == WeaponKind.Turret)
                    WeaponPool.Prewarm(data, Mathf.Clamp(data.projectilePrewarmCount, 0, 64));
            }
            EventBus.RaiseWeaponsChanged();
            return true;
        }

        /// <summary>在商店主动合并一对相同等级武器；等级4不能继续合并。</summary>
        public bool CombineWeapon(int index)
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Shop ||
                index < 0 || index >= _weapons.Count) return false;
            var weapon = _weapons[index];
            if (weapon.Level >= MaxTier) return false;
            var other = _weapons.Find(w => w != weapon && w.Data.id == weapon.Data.id && w.Level == weapon.Level);
            if (other == null) return false;
            if (weapon.ModificationIds.Count == 0)
            {
                foreach (var id in other.ModificationIds)
                {
                    var modification = GameDatabase.GetWeaponModification(id);
                    if (modification != null) weapon.ApplyModification(modification, ModificationLevel(id));
                }
            }
            weapon.AbsorbValue(other.PaidValue);
            RemoveOwned(other);
            weapon.LevelUp();
            EventBus.RaiseWeaponsChanged();
            return true;
        }

        private void MergeChain(WeaponInstance upgraded)
        {
            while (upgraded.Level < MaxTier)
            {
                var other = _weapons.Find(w => w != upgraded && w.Data.id == upgraded.Data.id && w.Level == upgraded.Level);
                if (other == null) break;
                upgraded.AbsorbValue(other.PaidValue);
                RemoveOwned(other);
                upgraded.LevelUp();
            }
        }

        private void RemoveOwned(WeaponInstance weapon)
        {
            for (int i = _orbits.Count - 1; i >= 0; i--)
                if (_orbits[i] != null && _orbits[i].Weapon == weapon) { _orbits[i].Despawn(); _orbits.RemoveAt(i); }
            for (int i = _turrets.Count - 1; i >= 0; i--)
                if (_turrets[i] != null && _turrets[i].Weapon == weapon) { _turrets[i].Despawn(); _turrets.RemoveAt(i); }
            _weapons.Remove(weapon);
        }

        public bool SellWeapon(int index, out int refund)
        {
            refund = 0;
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Shop) return false;
            if (index < 0 || index >= _weapons.Count) return false;
            var weapon = _weapons[index];
            float rate = GameDatabase.Balance != null ? GameDatabase.Balance.sellReturnRate : 0.5f;
            refund = Mathf.Max(0, Mathf.FloorToInt(weapon.PaidValue * Mathf.Clamp01(rate)));
            RemoveOwned(weapon);
            GameManager.Instance.AddMaterials(refund);
            EventBus.RaiseWeaponsChanged();
            return true;
        }

        public bool LevelUp(string weaponId)
        {
            var w = _weapons.Find(x => x.Data.id == weaponId);
            if (w == null || w.Level >= MaxTier) return false;
            w.LevelUp();
            EventBus.RaiseWeaponsChanged();
            return true;
        }

        public void RemoveWeapon(string weaponId)
        {
            int idx = _weapons.FindIndex(w => w.Data.id == weaponId);
            if (idx >= 0) RemoveOwned(_weapons[idx]);
            EventBus.RaiseWeaponsChanged();
        }

        // ---------- 池 ----------

        public ObjectPool GetOrbitPool(string weaponId)
        {
            return GamePools.Get("orbit:" + weaponId, () => BuildOrbitGo());
        }

        private static GameObject BuildOrbitGo()
        {
            var prefab = PrefabProvider.Instantiate("Shared/orbit", PoolRoot.Root);
            if (prefab != null) return prefab;

            var go = new GameObject("OrbitBody");
            go.transform.SetParent(PoolRoot.Root);
            go.AddComponent<OrbitBody>();
            return go;
        }

        public ObjectPool GetTurretPool(string weaponId)
        {
            return GamePools.Get("turret:" + weaponId, () => BuildTurretGo());
        }

        private static GameObject BuildTurretGo()
        {
            var prefab = PrefabProvider.Instantiate("Shared/turret", PoolRoot.Root);
            if (prefab != null) return prefab;

            var go = new GameObject("Turret");
            go.transform.SetParent(PoolRoot.Root);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 4;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.isKinematic = true;
            go.AddComponent<Turret>();
            return go;
        }

        public void TrackOrbit(OrbitBody o) => _orbits.Add(o);
        public void TrackTurret(Turret t) => _turrets.Add(t);
        public void UntrackOrbit(OrbitBody o) => _orbits.Remove(o);
        public void UntrackTurret(Turret t) => _turrets.Remove(t);

        // ---------- 开火 ----------

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            if (_stats == null) return;
            if (_visuals == null) _visuals = GetComponent<WeaponVisuals>();
            if (_controller == null) _controller = GetComponent<PlayerController>();

            for (int i = 0; i < _weapons.Count; i++)
            {
                var w = _weapons[i];
                w.Cooldown -= Time.deltaTime;
                if (w.Data.kind == WeaponKind.Melee && _visuals != null && _visuals.IsMeleeAttacking(w))
                    continue;
                Enemy target = w.Data.kind == WeaponKind.Melee && _visuals != null
                    ? _visuals.FindNearestMeleeTarget(w, _stats.Get(StatType.RangeMult))
                    : EnemyManager.Nearest(transform.position,
                        w.EffectiveRange(_stats) + Mathf.Max(0f, w.Data.targetingRangeBonus));
                if (target == null && w.MeleeWaveModification != null)
                    target = EnemyManager.Nearest(transform.position,
                        Mathf.Max(w.EffectiveRange(_stats), w.MeleeWaveModification.waveRange));
                Vector2 aim = target != null
                    ? (Vector2)(target.transform.position - transform.position)
                    : (_controller != null ? _controller.Facing : Vector2.right);
                if (aim.sqrMagnitude < 0.001f) aim = Vector2.right;
                bool pointBlank = false;
                if (_visuals != null)
                {
                    if (target != null && w.Data.kind == WeaponKind.Melee)
                    {
                        Vector2 targetPoint = target.BodyCollider != null
                            ? (Vector2)target.BodyCollider.transform.TransformPoint(target.BodyCollider.offset)
                            : (Vector2)target.transform.position;
                        _visuals.AimMeleeAtTarget(w, targetPoint, out aim);
                    }
                    else if (target == null || !_visuals.AimAtTarget(w, target.transform.position, out aim, out pointBlank))
                        _visuals.AimAt(w, aim);
                }
                if (target != null && _controller != null) _controller.SetCombatFacing(aim);

                if (w.Cooldown > 0f) continue;
                if (target == null && w.Data.kind != WeaponKind.Orbit && w.Data.kind != WeaponKind.Turret) continue;

                w.Fire(this, aim, _stats, target, pointBlank);
                w.Cooldown = w.EffectiveInterval(_stats);
            }
        }

        public Vector3 GetFireOrigin(WeaponInstance weapon, Vector2 direction)
        {
            if (_visuals != null && _visuals.TryGetMuzzle(weapon, out var muzzle)) return muzzle;
            return transform.position + (Vector3)direction.normalized * 0.4f;
        }

        public void PlayMuzzleFlash(WeaponInstance weapon, Vector2 direction)
        {
            HitImpact.Spawn(GetFireOrigin(weapon, direction), direction, false);
        }

        public void PerformFlameConeAttack(WeaponInstance weapon, Vector2 aim, float damage, bool crit)
        {
            if (weapon == null) return;
            Vector2 direction = aim.sqrMagnitude > 0.001f ? aim.normalized : Vector2.right;
            float range = weapon.EffectiveRange(_stats);
            float halfAngle = Mathf.Clamp(weapon.Data.flameConeAngle, 10f, 120f) * 0.5f;
            Vector2 origin = GetFireOrigin(weapon, direction);
            WeaponPatternEffect.SpawnFlame(origin, direction, range, halfAngle, weapon.Data.patternEffectDuration);

            EnemyManager.CopyAliveTo(_meleeCandidates);
            for (int i = 0; i < _meleeCandidates.Count; i++)
            {
                var enemy = _meleeCandidates[i];
                if (enemy == null || enemy.IsCharmed || enemy.BodyCollider == null) continue;
                Vector2 center = enemy.BodyCollider.transform.TransformPoint(enemy.BodyCollider.offset);
                Vector2 toEnemy = center - origin;
                float distance = toEnemy.magnitude;
                float radius = enemy.BodyCollider.radius * Mathf.Max(Mathf.Abs(enemy.transform.lossyScale.x),
                    Mathf.Abs(enemy.transform.lossyScale.y));
                float angleAllowance = distance > 0.001f ? Mathf.Asin(Mathf.Clamp01(radius / distance)) * Mathf.Rad2Deg : 180f;
                if (distance > range + radius || Vector2.Angle(direction, toEnemy) > halfAngle + angleAllowance) continue;
                enemy.TakeDamage(new DamageInfo(damage, gameObject, crit,
                    Mathf.Max(0f, _stats.Get(StatType.Knockback)), 0.45f));
                ApplyLifeSteal(damage);
            }
        }

        public void PerformLaserBeamAttack(WeaponInstance weapon, Vector2 aim, float damage, bool crit)
        {
            if (weapon == null) return;
            Vector2 direction = aim.sqrMagnitude > 0.001f ? aim.normalized : Vector2.right;
            Vector2 origin = GetFireOrigin(weapon, direction);
            float range = weapon.EffectiveRange(_stats);
            float width = Mathf.Max(0.03f, weapon.Data.laserBeamWidth);
            WeaponPatternEffect.SpawnLaser(origin, direction, range, width, weapon.Data.patternEffectDuration);

            EnemyManager.CopyAliveTo(_meleeCandidates);
            for (int i = 0; i < _meleeCandidates.Count; i++)
            {
                var enemy = _meleeCandidates[i];
                if (enemy == null || enemy.IsCharmed || enemy.BodyCollider == null) continue;
                Vector2 center = enemy.BodyCollider.transform.TransformPoint(enemy.BodyCollider.offset);
                Vector2 fromOrigin = center - origin;
                float along = Vector2.Dot(fromOrigin, direction);
                float radius = enemy.BodyCollider.radius * Mathf.Max(Mathf.Abs(enemy.transform.lossyScale.x),
                    Mathf.Abs(enemy.transform.lossyScale.y));
                if (along < -radius || along > range + radius) continue;
                float nearestAlong = Mathf.Clamp(along, 0f, range);
                Vector2 nearestPoint = origin + direction * nearestAlong;
                if ((center - nearestPoint).sqrMagnitude > Mathf.Pow(width * 0.5f + radius, 2f)) continue;
                enemy.TakeDamage(new DamageInfo(damage, gameObject, crit,
                    Mathf.Max(0f, _stats.Get(StatType.Knockback)), 0.7f));
                ApplyLifeSteal(damage);
            }
        }

        public void SpawnProjectile(Vector3 pos, Vector2 dir, WeaponInstance weapon, float damage, bool crit,
            Enemy target = null, bool pointBlank = false)
        {
            var data = weapon.Data;
            var targetCollider = target != null ? target.GetComponent<Collider2D>() : null;
            if (target != null && (pointBlank || targetCollider != null && targetCollider.OverlapPoint(pos)))
            {
                // 敌人已贴到枪管/枪口时，枪口在目标背后；继续发射只会让子弹飞离目标。
                if (data.explodeRadius > 0f)
                {
                    var explosivePool = WeaponPool.Bullets(data);
                    if (explosivePool == null) return;
                    var explosion = explosivePool.Get(target.transform.position);
                    var explosive = explosion.GetComponent<Projectile>();
                    explosive.Launch(target.transform.position, dir, 0, damage, data.projectileSpeed,
                        data.range * weapon.ProjectileRangeMultiplier,
                        data.pierce + weapon.ExtraPierce, data.explodeRadius, crit, false, explosivePool, _stats);
                    explosive.Detonate();
                    return;
                }
                target.TakeDamage(new DamageInfo(damage, gameObject, crit,
                    Mathf.Max(0f, _stats.Get(StatType.Knockback))));
                if (target.IsAlive && weapon.CharmChance > 0f && Random.value < weapon.CharmChance)
                    target.ApplyCharm(weapon.CharmDuration, weapon.CharmDamagePerSecond);
                ApplyLifeSteal(damage);
                return;
            }
            var pool = WeaponPool.Bullets(data);
            if (pool == null) return;
            var go = pool.Get(pos);
            var proj = go.GetComponent<Projectile>();
            var balance = GameDatabase.Balance;
            bool standardBullet = data.attackPattern == WeaponAttackPattern.StandardProjectile && data.explodeRadius <= 0f;
            int bounceCap = balance != null ? Mathf.Max(0, balance.maxProjectileBounces) : 3;
            int totalBounces = standardBullet ? Mathf.Clamp(weapon.ProjectileBounces, 0, bounceCap) : 0;
            float extraRange = totalBounces > 0
                ? 1f + totalBounces * (balance != null ? Mathf.Max(0f, balance.ricochetExtraRangePerBounce) : 1f)
                : 1f;
            float flightRange = data.explodeRadius > 0f
                ? Mathf.Max(data.range, ArenaBounds.Rect.width + ArenaBounds.Rect.height)
                : data.range * _stats.Get(StatType.RangeMult) * weapon.ProjectileRangeMultiplier * extraRange;
            proj.Launch(pos, dir, 0, damage, data.projectileSpeed,
                flightRange,
                data.pierce + weapon.ExtraPierce,
                data.explodeRadius, crit, false, pool, _stats);
            if (totalBounces > 0)
                proj.ConfigureRicochet(totalBounces, weapon.BounceDamageMultiplier);
            if (weapon.VacuumAbsorbLimit > 0)
                proj.ConfigureVacuum(weapon.VacuumAbsorbLimit, weapon.VacuumDamagePerAbsorb, weapon.VacuumRadius);
            if (data.attackPattern == WeaponAttackPattern.StandardProjectile)
            {
                proj.SetVisualScale(Mathf.Clamp(weapon.ProjectileSizeMultiplier, 1f,
                    balance != null ? Mathf.Max(1f, balance.maxProjectileSizeMultiplier) : 2f));
            }
            if (standardBullet)
            {
                proj.ConfigureCharm(weapon.CharmChance, weapon.CharmDuration, weapon.CharmDamagePerSecond);
            }
        }

        /// <summary>启动一次近战动作；实际伤害在可见攻击帧与敌人碰撞时结算。</summary>
        public void PerformMeleeAttack(WeaponInstance weapon, Vector2 aim, float damage, bool crit)
        {
            // 只登记本次攻击数据；真正命中留给可见挥砍/刺击进入有效帧后检测。
            _meleeAttacks[weapon] = new MeleeAttackState
            {
                Damage = damage,
                Critical = crit,
                Knockback = Mathf.Max(0f, _stats.Get(StatType.Knockback)) + weapon.SpringKnockback,
                SpringCollisionDamage = damage * weapon.SpringCollisionDamageMultiplier,
                SpringCollisionWindow = weapon.SpringCollisionWindow,
                Aim = aim.sqrMagnitude > 0.001f ? aim.normalized : Vector2.right
            };
            if (_visuals != null)
            {
                if (_visuals.PlayMeleeAttack(weapon, aim)) return;
            }
            {
                Vector2 facing = aim.sqrMagnitude > 0.001f ? aim.normalized : Vector2.right;
                float heldSize = Mathf.Max(0.01f, weapon.Data.heldVisualSize * transform.lossyScale.x);
                float tipDistance = (0.5f + Mathf.Clamp(weapon.Data.meleeContactFraction, -0.5f, 0.5f) -
                                     Mathf.Clamp01(weapon.Data.meleeHandleFraction)) * heldSize;
                Vector2 tip = (Vector2)transform.position + facing * tipDistance;
                ResolveMeleeAttackHit(weapon, tip, tip);
                CompleteMeleeAttack(weapon);
            }
        }

        /// <summary>只检测剑尖/拳尖在两帧间扫过的路径；同一敌人每次攻击只受击一次。</summary>
        public void ResolveMeleeAttackHit(WeaponInstance weapon, Vector2 previousTip, Vector2 tip)
        {
            if (weapon == null || !_meleeAttacks.TryGetValue(weapon, out var attack)) return;
            if (!attack.WaveEmitted && weapon.MeleeWaveModification != null)
            {
                attack.WaveEmitted = true;
                var mod = weapon.MeleeWaveModification;
                var pool = WeaponPool.Bullets(weapon.Data);
                if (pool != null)
                {
                    var origin = (Vector3)tip;
                    var go = pool.Get(origin);
                    var wave = go.GetComponent<Projectile>();
                    wave.Launch(origin, attack.Aim, 0,
                        attack.Damage * Mathf.Max(0.1f, mod.waveDamageMultiplier),
                        Mathf.Max(1f, mod.waveSpeed), Mathf.Max(0.5f, mod.waveRange),
                        Mathf.Clamp(mod.wavePierce, 0, 8), 0f, attack.Critical, false, pool, _stats);
                    wave.SetVisualScale(2f);
                    HitImpact.Spawn(origin, attack.Aim, false);
                }
            }
            float weaponRadius = MeleeAttackGeometry.ContactRadius(weapon.Data, transform.lossyScale);
            EnemyManager.CopyAliveTo(_meleeCandidates);
            for (int i = 0; i < _meleeCandidates.Count; i++)
            {
                var enemy = _meleeCandidates[i];
                if (enemy == null || enemy.IsCharmed || enemy.BodyCollider == null) continue;
                var col = enemy.BodyCollider;
                Vector2 enemyCenter = col.transform.TransformPoint(col.offset);
                float enemyRadius = col.radius * Mathf.Max(Mathf.Abs(col.transform.lossyScale.x),
                    Mathf.Abs(col.transform.lossyScale.y));
                if (!MeleeAttackGeometry.TipSweepTouches(enemyCenter,
                    weaponRadius + enemyRadius, previousTip, tip)) continue;
                if (!attack.HitEnemies.Add(enemy)) continue;
                bool springEligible = !enemy.IsBoss && enemy.Data != null && !enemy.Data.isElite;
                float knockback = springEligible ? attack.Knockback :
                    Mathf.Max(0f, attack.Knockback - weapon.SpringKnockback);
                enemy.TakeDamage(new DamageInfo(attack.Damage, gameObject, attack.Critical, knockback, 1f));
                if (springEligible && enemy.IsAlive && attack.SpringCollisionDamage > 0f && knockback > 0f)
                    enemy.ArmSpringCollision(attack.SpringCollisionDamage, attack.SpringCollisionWindow, gameObject);
                ApplyLifeSteal(attack.Damage);
            }
        }

        public void CompleteMeleeAttack(WeaponInstance weapon) => _meleeAttacks.Remove(weapon);

        public void ApplyLifeSteal(float dealt)
        {
            if (_stats == null || dealt <= 0f) return;
            float steal = Mathf.Max(0f, _stats.Get(StatType.LifeSteal));
            if (steal > 0f && GameManager.Instance != null && GameManager.Instance.Player != null)
                GameManager.Instance.Player.Health.Heal(dealt * steal);
        }

        public bool SpawnOrbit(WeaponInstance weapon, PlayerStats stats)
        {
            if (_orbits.Count >= (GameDatabase.Balance != null ? GameDatabase.Balance.maxOrbits : 2)) return false;
            var pool = GetOrbitPool(weapon.Data.id);
            var go = pool.Get(transform.position);
            if (go == null) return false;
            var orbit = go.GetComponent<OrbitBody>();
            if (orbit == null) { pool.Release(go); return false; }
            orbit.Init(this, weapon, stats, pool);
            TrackOrbit(orbit);
            return true;
        }

        public bool SpawnTurret(WeaponInstance weapon, PlayerStats stats)
        {
            if (_turrets.Count >= (GameDatabase.Balance != null ? GameDatabase.Balance.maxTurrets : 6)) return false;
            var pool = GetTurretPool(weapon.Data.id);
            var go = pool.Get(transform.position + new Vector3(0.5f, 0f, 0f));
            if (go == null) return false;
            var turret = go.GetComponent<Turret>();
            if (turret == null) { pool.Release(go); return false; }
            turret.Init(this, weapon, stats, pool);
            TrackTurret(turret);
            return true;
        }
    }
}
