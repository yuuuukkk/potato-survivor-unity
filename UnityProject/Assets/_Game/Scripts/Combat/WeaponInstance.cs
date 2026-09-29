using RogueLike.Data;
using RogueLike.Enemies;
using RogueLike.Player;
using System.Collections.Generic;
using UnityEngine;

namespace RogueLike.Combat
{
    /// <summary>
    /// 单个武器实例：数据 + 等级 + 冷却；按 WeaponKind 分派开火。
    /// 等级成长：伤害与攻速按配置成长，等级上限由波次 SO 配置（默认四级）。
    /// </summary>
    public class WeaponInstance
    {
        public WeaponData Data { get; }
        public int Level { get; private set; }
        public float Cooldown { get; set; }
        public int PaidValue { get; private set; }
        private readonly List<string> _modificationIds = new List<string>();
        private readonly Dictionary<string, int> _modificationLevels = new Dictionary<string, int>();
        public IReadOnlyList<string> ModificationIds => _modificationIds;
        public int ModificationLevel(string id) => _modificationLevels.TryGetValue(id, out var level) ? level : 0;
        public int MaxModificationSlots => GameDatabase.Balance != null && GameDatabase.Balance.maxEquippedModifications > 0
            ? GameDatabase.Balance.maxEquippedModifications : 2;
        public MeleeAnimationStyle EffectiveMeleeAnimation { get; private set; }
        public WeaponModificationData MeleeWaveModification { get; private set; }
        public int ProjectileBounces { get; private set; }
        public float BounceDamageMultiplier { get; private set; } = 1f;
        public int ExtraPierce { get; private set; }
        public float ProjectileRangeMultiplier { get; private set; } = 1f;
        public float ProjectileSizeMultiplier { get; private set; } = 1f;
        public float CharmChance { get; private set; }
        public float CharmDuration { get; private set; }
        public float CharmDamagePerSecond { get; private set; }
        public float ReturnDamageMultiplier { get; private set; } = 1f;
        public float ReturnSpeedMultiplier { get; private set; } = 1f;
        public float CollectionRadius { get; private set; }
        public float ReturnBonusPerPickup { get; private set; }
        public int MaxPickupBonusCount { get; private set; }
        public int VacuumAbsorbLimit { get; private set; }
        public float VacuumDamagePerAbsorb { get; private set; }
        public float VacuumRadius { get; private set; }
        public float SpringKnockback { get; private set; }
        public float SpringCollisionDamageMultiplier { get; private set; }
        public float SpringCollisionWindow { get; private set; }
        private float _damageMultiplier = 1f;
        private float _intervalMultiplier = 1f;

        private bool _hasOrbit;
        private bool _hasTurret;

        public WeaponInstance(WeaponData data, int level, int paidValue = 0)
        {
            Data = data;
            var cfg = GameDatabase.WaveConfig;
            Level = Mathf.Clamp(level, 1, cfg != null ? cfg.weaponMaxLevel : 4);
            PaidValue = paidValue > 0 ? paidValue : Mathf.RoundToInt(data.basePrice * RarityInfo.PriceMult((Rarity)(Level - 1)));
            EffectiveMeleeAnimation = data.meleeAnimation;
        }

        public void AbsorbValue(int amount) => PaidValue += Mathf.Max(0, amount);

        public void LevelUp()
        {
            var cfg = GameDatabase.WaveConfig;
            Level = Mathf.Min(cfg != null ? cfg.weaponMaxLevel : 4, Level + 1);
        }

        public bool HasModificationGroup(string group)
        {
            if (string.IsNullOrWhiteSpace(group)) return false;
            foreach (var id in _modificationIds)
            {
                var applied = GameDatabase.GetWeaponModification(id);
                if (applied != null && applied.exclusiveGroup == group) return true;
            }
            return false;
        }

        public bool HasModification(string id) => !string.IsNullOrWhiteSpace(id) && _modificationIds.Contains(id);

        /// <summary>只替换该分支的装备；其余独立分支保持不变。</summary>
        public bool SetModificationForGroup(WeaponModificationData modification, int level = 1)
        {
            if (modification == null || !modification.AppliesTo(Data) || !modification.IsValid) return false;
            var retained = new List<WeaponModificationData>();
            var retainedLevels = new List<int>();
            foreach (var id in _modificationIds)
            {
                var old = GameDatabase.GetWeaponModification(id);
                if (old != null && old.exclusiveGroup != modification.exclusiveGroup)
                {
                    retained.Add(old);
                    retainedLevels.Add(ModificationLevel(id));
                }
            }
            if (retained.Count >= MaxModificationSlots) return false;
            ResetModifications();
            for (int i = 0; i < retained.Count; i++) ApplyModification(retained[i], retainedLevels[i]);
            return ApplyModification(modification, level);
        }

        public bool RemoveModification(string id)
        {
            if (!HasModification(id)) return false;
            var retained = new List<WeaponModificationData>();
            var levels = new List<int>();
            foreach (var existing in _modificationIds)
            {
                if (existing == id) continue;
                var mod = GameDatabase.GetWeaponModification(existing);
                if (mod == null) continue;
                retained.Add(mod);
                levels.Add(ModificationLevel(existing));
            }
            ResetModifications();
            for (int i = 0; i < retained.Count; i++) ApplyModification(retained[i], levels[i]);
            return true;
        }

        public void RefreshModificationLevels(System.Func<string, int> getLevel)
        {
            var mods = new List<WeaponModificationData>();
            foreach (var id in _modificationIds)
            {
                var mod = GameDatabase.GetWeaponModification(id);
                if (mod != null) mods.Add(mod);
            }
            ResetModifications();
            foreach (var mod in mods) ApplyModification(mod, getLevel(mod.id));
        }

        public bool ClearModifications()
        {
            if (_modificationIds.Count == 0) return false;
            ResetModifications();
            return true;
        }

        private void ResetModifications()
        {
            _modificationIds.Clear();
            _modificationLevels.Clear();
            EffectiveMeleeAnimation = Data.meleeAnimation;
            MeleeWaveModification = null;
            ProjectileBounces = 0;
            BounceDamageMultiplier = 1f;
            ExtraPierce = 0;
            ProjectileRangeMultiplier = 1f;
            ProjectileSizeMultiplier = 1f;
            CharmChance = 0f;
            CharmDuration = 0f;
            CharmDamagePerSecond = 0f;
            ReturnDamageMultiplier = 1f;
            ReturnSpeedMultiplier = 1f;
            CollectionRadius = 0f;
            ReturnBonusPerPickup = 0f;
            MaxPickupBonusCount = 0;
            VacuumAbsorbLimit = 0;
            VacuumDamagePerAbsorb = 0f;
            VacuumRadius = 0f;
            SpringKnockback = 0f;
            SpringCollisionDamageMultiplier = 0f;
            SpringCollisionWindow = 0f;
            _damageMultiplier = 1f;
            _intervalMultiplier = 1f;
        }

        public bool ApplyModification(WeaponModificationData modification, int level = 1)
        {
            if (modification == null || !modification.IsValid ||
                !modification.AppliesTo(Data) || _modificationIds.Contains(modification.id) ||
                HasModificationGroup(modification.exclusiveGroup) ||
                _modificationIds.Count >= MaxModificationSlots) return false;
            level = Mathf.Clamp(level, 1, modification.MaxLevel);
            int upgrades = level - 1;

            switch (modification.kind)
            {
                case WeaponModificationKind.MeleeAnimationOverride:
                    if (Data.kind != WeaponKind.Melee) return false;
                    EffectiveMeleeAnimation = modification.meleeAnimation;
                    break;
                case WeaponModificationKind.MeleeWave:
                    if (Data.kind != WeaponKind.Melee) return false;
                    MeleeWaveModification = modification;
                    break;
                case WeaponModificationKind.Ricochet:
                    if (Data.kind != WeaponKind.Projectile ||
                        Data.attackPattern != WeaponAttackPattern.StandardProjectile) return false;
                    ProjectileBounces += Mathf.Clamp(modification.extraBounces +
                        Mathf.RoundToInt(upgrades * modification.extraBouncesPerLevel), 1, 8);
                    BounceDamageMultiplier *= Mathf.Clamp(modification.bounceDamageMultiplier, 0.1f, 1f);
                    ProjectileRangeMultiplier *= Mathf.Max(1f, modification.projectileRangeMultiplier);
                    break;
                case WeaponModificationKind.Piercing:
                    if (Data.kind != WeaponKind.Projectile || Data.explodeRadius > 0f ||
                        Data.attackPattern != WeaponAttackPattern.StandardProjectile) return false;
                    ExtraPierce += Mathf.Clamp(modification.extraPierce, 1, 8);
                    break;
                case WeaponModificationKind.EnlargedProjectile:
                    if (Data.kind != WeaponKind.Projectile ||
                        Data.attackPattern != WeaponAttackPattern.StandardProjectile) return false;
                    ProjectileSizeMultiplier *= Mathf.Clamp(modification.projectileSizeMultiplier +
                        upgrades * modification.projectileSizePerLevel, 1f, 4f);
                    break;
                case WeaponModificationKind.CharmProjectile:
                    if (Data.kind != WeaponKind.Projectile || Data.explodeRadius > 0f ||
                        Data.attackPattern != WeaponAttackPattern.StandardProjectile) return false;
                    float addedChance = Mathf.Clamp01(modification.charmChance + upgrades * modification.charmChancePerLevel);
                    CharmChance = 1f - (1f - CharmChance) * (1f - addedChance);
                    CharmDuration = Mathf.Max(CharmDuration,
                        Mathf.Max(0.1f, modification.charmDuration + upgrades * modification.charmDurationPerLevel));
                    CharmDamagePerSecond = Mathf.Max(CharmDamagePerSecond,
                        Mathf.Max(0f, modification.charmDamagePerSecond));
                    break;
                case WeaponModificationKind.BoomerangReturnSurge:
                    if (Data.kind != WeaponKind.Boomerang) return false;
                    ReturnDamageMultiplier = Mathf.Clamp(modification.returnDamageMultiplier, 1f, 3f);
                    ReturnSpeedMultiplier = Mathf.Clamp(modification.returnSpeedMultiplier, 1f, 3f);
                    break;
                case WeaponModificationKind.BoomerangCollector:
                    if (Data.kind != WeaponKind.Boomerang) return false;
                    CollectionRadius = Mathf.Max(0.1f, modification.collectionRadius);
                    ReturnBonusPerPickup = Mathf.Max(0f, modification.returnBonusPerPickup +
                        upgrades * modification.returnBonusPerPickupPerLevel);
                    MaxPickupBonusCount = Mathf.Clamp(modification.maxPickupBonusCount, 1, 32);
                    break;
                case WeaponModificationKind.VacuumRocket:
                    if (Data.kind != WeaponKind.Projectile || Data.explodeRadius <= 0f ||
                        Data.attackPattern != WeaponAttackPattern.StandardProjectile) return false;
                    VacuumAbsorbLimit = Mathf.Max(1, modification.vacuumAbsorbLimit +
                        upgrades * modification.vacuumAbsorbPerLevel);
                    VacuumDamagePerAbsorb = Mathf.Max(0f, modification.vacuumDamagePerAbsorb);
                    VacuumRadius = Mathf.Max(0.1f, modification.vacuumRadius);
                    break;
                case WeaponModificationKind.SpringPunch:
                    if (Data.kind != WeaponKind.Melee) return false;
                    SpringKnockback = Mathf.Max(0f, modification.springKnockback +
                        upgrades * modification.springKnockbackPerLevel);
                    SpringCollisionDamageMultiplier = Mathf.Max(0f, modification.springCollisionDamageMultiplier);
                    SpringCollisionWindow = Mathf.Max(0.05f, modification.springCollisionWindow);
                    break;
                default:
                    return false;
            }

            _damageMultiplier *= (modification.damageMultiplier > 0f
                ? Mathf.Clamp(modification.damageMultiplier, 0.2f, 2f) : 1f) *
                (1f + upgrades * Mathf.Max(0f, modification.damageBonusPerLevel));
            _intervalMultiplier *= modification.attackIntervalMultiplier > 0f
                ? Mathf.Clamp(modification.attackIntervalMultiplier, 0.5f, 2f) : 1f;
            _modificationIds.Add(modification.id);
            _modificationLevels[modification.id] = level;
            return true;
        }

        public float EffectiveDamage(PlayerStats stats)
        {
            var cfg = GameDatabase.WaveConfig;
            float g = cfg != null ? cfg.upgradeDamagePerLevel : 0.5f;
            float specialist = Data.kind == WeaponKind.Projectile && stats.Character != null
                ? Mathf.Max(0f, stats.Character.projectileDamageMultiplier) : 1f;
            return Data.damage * (1f + g * (Level - 1)) * stats.Get(StatType.DamageMult) * specialist * _damageMultiplier;
        }

        public float EffectiveInterval(PlayerStats stats)
        {
            var cfg = GameDatabase.WaveConfig;
            float g = cfg != null ? cfg.upgradeAttackSpeedPerLevel : 0.08f;
            return Data.attackInterval * _intervalMultiplier / (1f + g * (Level - 1)) / stats.Get(StatType.AttackSpeedMult);
        }

        public float EffectiveRange(PlayerStats stats)
            => Data.range * stats.Get(StatType.RangeMult);

        public void Fire(WeaponSystem owner, Vector2 aim, PlayerStats stats, Enemy target = null, bool pointBlank = false)
        {
            float critChance = stats.Get(StatType.CritChance) + Data.critChance;
            float critMult = stats.Get(StatType.CritDamageMult) + (Data.critDamageMult - 2f);

            if (Data.attackPattern == WeaponAttackPattern.FlameCone)
            {
                bool crit = Random.value < critChance;
                owner.PerformFlameConeAttack(this, aim, EffectiveDamage(stats) * (crit ? critMult : 1f), crit);
                return;
            }
            if (Data.attackPattern == WeaponAttackPattern.LaserBeam)
            {
                bool crit = Random.value < critChance;
                owner.PerformLaserBeamAttack(this, aim, EffectiveDamage(stats) * (crit ? critMult : 1f), crit);
                return;
            }

            switch (Data.kind)
            {
                case WeaponKind.Projectile:
                    FireProjectile(owner, aim, stats, critChance, critMult, target, pointBlank);
                    break;
                case WeaponKind.Melee:
                    FireMelee(owner, aim, stats, critChance, critMult);
                    break;
                case WeaponKind.Orbit:
                    if (!_hasOrbit)
                        _hasOrbit = owner.SpawnOrbit(this, stats);
                    break;
                case WeaponKind.Turret:
                    if (!_hasTurret)
                        _hasTurret = owner.SpawnTurret(this, stats);
                    break;
                case WeaponKind.Boomerang:
                    FireBoomerang(owner, aim, stats, critChance, critMult);
                    break;
            }
        }

        private void FireProjectile(WeaponSystem owner, Vector2 aim, PlayerStats stats, float critChance, float critMult,
            Enemy target, bool pointBlank)
        {
            Vector2 dir = aim.normalized;
            int count = Mathf.Max(1, Data.projectileCount);
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float spread = count > 1 ? Data.spreadDeg : 0f;
            float damage = EffectiveDamage(stats);
            float critDmg = damage * critMult;
            owner.PlayMuzzleFlash(this, dir);

            for (int i = 0; i < count; i++)
            {
                float a = count > 1 ? baseAngle - spread * 0.5f + spread * i / (count - 1) : baseAngle;
                Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                bool crit = Random.value < critChance;
                owner.SpawnProjectile(owner.GetFireOrigin(this, d), d, this, crit ? critDmg : damage, crit, target, pointBlank);
            }
        }

        private void FireMelee(WeaponSystem owner, Vector2 aim, PlayerStats stats, float critChance, float critMult)
        {
            bool crit = Random.value < critChance;
            float damage = EffectiveDamage(stats) * (crit ? critMult : 1f);
            owner.PerformMeleeAttack(this, aim.normalized, damage, crit);
        }

        private void FireBoomerang(WeaponSystem owner, Vector2 aim, PlayerStats stats, float critChance, float critMult)
        {
            bool crit = Random.value < critChance;
            float damage = EffectiveDamage(stats) * (crit ? critMult : 1f);
            var pool = WeaponPool.Bullets(Data);
            if (pool == null) return;
            var origin = owner.GetFireOrigin(this, aim);
            HitImpact.Spawn(origin, aim, crit);
            var go = pool.Get(origin);
            var proj = go.GetComponent<Projectile>();
            proj.Launch(origin, aim.normalized, 0, damage, Data.projectileSpeed,
                EffectiveRange(stats) * 2f, 0, 0f, crit, true, pool, stats);
            proj.ConfigureBoomerangReturn(ReturnDamageMultiplier, ReturnSpeedMultiplier);
            proj.ConfigureBoomerangCollection(CollectionRadius, ReturnBonusPerPickup, MaxPickupBonusCount);
        }
    }
}
