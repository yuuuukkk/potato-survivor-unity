using System.Collections.Generic;
using RogueLike.Combat;
using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Enemies;
using UnityEngine;

namespace RogueLike.Player
{
    /// <summary>
    /// 武器挂件（土豆兄弟式）：玩家身上预留 6 个武器位置，持有武器时生成可视化挂件跟随玩家。
    /// 不同武器类型位置不同：直射 = 肩/腰环形、近战 = 左右手、回旋镖 = 背、环绕/炮台 = 世界可见（不生成挂件）。
    /// 挂件可被预制 Resources/Prefabs/Shared/weapon_visual 替换素材（改预制 Sprite 即换皮）。
    /// </summary>
    public class WeaponVisuals : MonoBehaviour
    {
        private readonly List<GameObject> _visuals = new List<GameObject>();
        private readonly Dictionary<WeaponInstance, SpriteRenderer> _weaponRenderers = new Dictionary<WeaponInstance, SpriteRenderer>();
        private readonly Dictionary<WeaponInstance, Transform> _weaponPivots = new Dictionary<WeaponInstance, Transform>();
        private readonly Dictionary<WeaponInstance, Vector2> _meleeTipOffsets = new Dictionary<WeaponInstance, Vector2>();
        private readonly Dictionary<WeaponInstance, Matrix4x4> _meleePreviousTransforms = new Dictionary<WeaponInstance, Matrix4x4>();
        private readonly Dictionary<WeaponInstance, Vector3> _anchors = new Dictionary<WeaponInstance, Vector3>();
        private readonly Dictionary<Sprite, Vector2> _muzzleBySprite = new Dictionary<Sprite, Vector2>();
        private readonly Dictionary<WeaponInstance, float> _meleeTimers = new Dictionary<WeaponInstance, float>();
        private readonly Dictionary<WeaponInstance, Vector2> _meleeDirections = new Dictionary<WeaponInstance, Vector2>();
        private readonly List<WeaponInstance> _activeMelee = new List<WeaponInstance>(6);
        private readonly List<Enemy> _targetCandidates = new List<Enemy>(128);
        private WeaponSystem _weaponSystem;

        // 通用锚点：肩/腰环形（直射、炮台类武器挂载位）
        private static readonly Vector3[] GenericAnchors =
        {
            new Vector3(0.55f, 0.28f, 0f), new Vector3(-0.55f, 0.28f, 0f),
            new Vector3(0.55f, -0.10f, 0f), new Vector3(-0.55f, -0.10f, 0f),
            new Vector3(0f, 0.52f, 0f), new Vector3(0f, -0.48f, 0f)
        };

        private void Awake()
        {
            _weaponSystem = GetComponent<WeaponSystem>();
        }

        private void OnEnable()
        {
            EventBus.WeaponsChanged += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            EventBus.WeaponsChanged -= Rebuild;
        }

        /// <summary>按当前武器列表重建挂件（购买/升级/重开时由事件触发）。</summary>
        public void Rebuild()
        {
            if (_weaponSystem != null)
                foreach (var weapon in _meleeTimers.Keys)
                    _weaponSystem.CompleteMeleeAttack(weapon);
            foreach (var v in _visuals)
                if (v != null) Destroy(v);
            _visuals.Clear();
            _weaponRenderers.Clear();
            _weaponPivots.Clear();
            _meleeTipOffsets.Clear();
            _meleePreviousTransforms.Clear();
            _anchors.Clear();
            _meleeTimers.Clear();
            _meleeDirections.Clear();
            _activeMelee.Clear();

            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;
            var ws = gm.Player.GetComponent<WeaponSystem>();
            if (ws == null) return;

            int index = 0;
            foreach (var w in ws.Weapons)
            {
                // 环绕/炮台在世界中已有可见表现（环绕体/地面炮台），不生成玩家挂件
                if (w.Data.kind == WeaponKind.Orbit || w.Data.kind == WeaponKind.Turret ||
                    w.Data.kind == WeaponKind.Boomerang)
                {
                    index++;
                    continue;
                }
                var anchor = NextAnchor(w.Data.kind, index);
                if (anchor.sqrMagnitude > 0.001f)
                {
                    float bonus = GameDatabase.Balance != null ? GameDatabase.Balance.heldWeaponDistanceBonus : 0.10f;
                    anchor += anchor.normalized * Mathf.Max(0f, w.Data.heldDistance + bonus);
                }
                CreateVisual(w, anchor);
                index++;
            }
        }

        /// <summary>不同武器类型挂在不同位置：近战=手、回旋镖=背、其余=肩腰环形。</summary>
        private static Vector3 NextAnchor(WeaponKind kind, int index)
        {
            if (kind == WeaponKind.Melee)
                return index % 2 == 0 ? new Vector3(0.42f, 0.05f, 0f) : new Vector3(-0.42f, 0.05f, 0f);
            if (kind == WeaponKind.Boomerang)
                return new Vector3(0f, -0.5f, 0f);
            return GenericAnchors[index % GenericAnchors.Length];
        }

        private void CreateVisual(WeaponInstance weapon, Vector3 anchor)
        {
            var data = weapon.Data;
            // 显式美术库只为空 Sprite 槽补图。
            var autoSprite = RogueLike.Core.AssetLoader.LoadWeaponSprite(data.id);
            var go = PrefabProvider.Instantiate("Shared/weapon_visual", transform);
            if (go == null)
            {
                go = new GameObject("WeaponVisual_" + data.id);
                go.transform.SetParent(transform);
                var sr = go.AddComponent<SpriteRenderer>();
                Color c = RarityInfo.Color(data.rarity);
                // 美术库有图则使用；无图才显示几何占位（近战=方块，远程=圆点）。
                sr.sprite = autoSprite != null
                    ? autoSprite
                    : (data.kind == WeaponKind.Melee
                        ? SpriteFactory.Square(0.24f, 0.24f)
                        : SpriteFactory.Circle(c, 0.14f));
                sr.color = autoSprite != null ? Color.white : c;
                sr.sortingOrder = 6;
            }
            else
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    if (autoSprite != null) sr.sprite = autoSprite;
                    if (sr.sprite == null)
                    {
                        Color c = RarityInfo.Color(data.rarity);
                        sr.sprite = data.kind == WeaponKind.Melee
                            ? SpriteFactory.Square(0.24f, 0.24f)
                            : SpriteFactory.Circle(c, 0.14f);
                    }
                    // 已配置素材时保持原图颜色。
                    sr.color = Color.white;
                    sr.sortingOrder = 6;
                }
            }
            var renderer = go.GetComponent<SpriteRenderer>();
            Transform movingPivot = go.transform;
            if (renderer != null)
            {
                if (renderer.sprite != null && data.heldVisualSize > 0f)
                {
                    var vertices = renderer.sprite.vertices;
                    float visibleSize = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
                    if (vertices != null && vertices.Length > 0)
                    {
                        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                        foreach (var vertex in vertices)
                        {
                            minX = Mathf.Min(minX, vertex.x); maxX = Mathf.Max(maxX, vertex.x);
                            minY = Mathf.Min(minY, vertex.y); maxY = Mathf.Max(maxY, vertex.y);
                        }
                        visibleSize = Mathf.Max(maxX - minX, maxY - minY);
                    }
                    renderer.transform.localScale = Vector3.one *
                        (data.heldVisualSize / Mathf.Max(0.01f, visibleSize));
                }

                if (data.kind == WeaponKind.Melee)
                {
                    var pivotObject = new GameObject("MeleePivot_" + data.id);
                    var pivot = pivotObject.transform;
                    pivot.SetParent(transform, false);
                    pivot.localPosition = anchor;
                    pivot.localRotation = Quaternion.identity;

                    // Place the authored handle point at the moving pivot. Rotation and thrust
                    // now originate from the grip, while the contact marker stays at the blade tip.
                    go.transform.SetParent(pivot, false);
                    go.transform.localRotation = Quaternion.identity;
                    Vector2 meshMinMax = GetSpriteXBounds(renderer.sprite);
                    float handleX = Mathf.Lerp(meshMinMax.x, meshMinMax.y,
                        Mathf.Clamp01(data.meleeHandleFraction));
                    go.transform.localPosition = new Vector3(
                        -handleX * go.transform.localScale.x, 0f, 0f);
                    float contactX = Mathf.Lerp(meshMinMax.x, meshMinMax.y,
                        0.5f + Mathf.Clamp(data.meleeContactFraction, -0.5f, 0.5f));
                    _meleeTipOffsets[weapon] = new Vector2(
                        (contactX - handleX) * go.transform.localScale.x, 0f);
                    movingPivot = pivot;
                    _visuals.Add(pivotObject);
                }
                else
                {
                    go.transform.localPosition = anchor;
                    _visuals.Add(go);
                }
                _weaponRenderers[weapon] = renderer;
                _weaponPivots[weapon] = movingPivot;
                _anchors[weapon] = anchor;
            }
            else
            {
                go.transform.localPosition = anchor;
                _visuals.Add(go);
            }
        }

        private static Vector2 GetSpriteXBounds(Sprite sprite)
        {
            if (sprite == null) return new Vector2(-0.5f, 0.5f);
            var vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0)
                return new Vector2(sprite.bounds.min.x, sprite.bounds.max.x);
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            foreach (var vertex in vertices)
            {
                minX = Mathf.Min(minX, vertex.x);
                maxX = Mathf.Max(maxX, vertex.x);
            }
            return new Vector2(minX, maxX);
        }

        public void AimAt(WeaponInstance weapon, Vector2 direction)
        {
            if (IsMeleeAttacking(weapon)) return;
            if (!_weaponRenderers.TryGetValue(weapon, out var sr) || sr == null || direction.sqrMagnitude < 0.001f) return;
            direction = transform.InverseTransformVector(direction);
            var anchor = _anchors[weapon];
            var pivot = GetMovingPivot(weapon, sr);
            pivot.localPosition = anchor;
            pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            sr.flipY = direction.x < 0f;
        }

        public bool AimMeleeAtTarget(WeaponInstance weapon, Vector2 target, out Vector2 direction)
        {
            direction = Vector2.zero;
            if (!_weaponRenderers.TryGetValue(weapon, out var sr) || sr == null) return false;
            var pivot = GetMovingPivot(weapon, sr);
            pivot.localPosition = _anchors[weapon];
            direction = target - (Vector2)pivot.position;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            direction.Normalize();
            AimAt(weapon, direction);
            return true;
        }

        public Vector3 GetWeaponPivot(WeaponInstance weapon)
        {
            return _weaponRenderers.TryGetValue(weapon, out var sr) && sr != null
                ? GetMovingPivot(weapon, sr).position : transform.position;
        }

        private Transform GetMovingPivot(WeaponInstance weapon, SpriteRenderer renderer)
            => _weaponPivots.TryGetValue(weapon, out var pivot) && pivot != null ? pivot : renderer.transform;

        /// <summary>返回实际攻击轨迹能碰到的最近敌人；索敌与伤害共用同一条剑尖轨迹。</summary>
        public Enemy FindNearestMeleeTarget(WeaponInstance weapon, float rangeMultiplier)
        {
            if (!_weaponRenderers.TryGetValue(weapon, out var renderer) || renderer == null ||
                !_meleeTipOffsets.TryGetValue(weapon, out var tipLocal)) return null;

            EnemyManager.CopyAliveTo(_targetCandidates);
            Enemy nearest = null;
            float nearestSq = float.PositiveInfinity;
            Vector2 origin = transform.TransformPoint(_anchors[weapon]);
            float weaponRadius = MeleeAttackGeometry.ContactRadius(weapon.Data, transform.lossyScale);

            for (int i = 0; i < _targetCandidates.Count; i++)
            {
                var enemy = _targetCandidates[i];
                if (enemy == null || enemy.IsCharmed || enemy.BodyCollider == null) continue;
                var collider = enemy.BodyCollider;
                Vector2 center = collider.transform.TransformPoint(collider.offset);
                float enemyRadius = collider.radius * Mathf.Max(Mathf.Abs(collider.transform.lossyScale.x),
                    Mathf.Abs(collider.transform.lossyScale.y));
                float distanceSq = (center - origin).sqrMagnitude;
                if (distanceSq >= nearestSq) continue;
                if (!MeleeTrajectoryTouches(weapon, tipLocal, center,
                    weaponRadius + enemyRadius, rangeMultiplier)) continue;
                nearestSq = distanceSq;
                nearest = enemy;
            }
            return nearest;
        }

        private bool MeleeTrajectoryTouches(WeaponInstance weapon, Vector2 tipLocal,
            Vector2 target, float contactRadius, float rangeMultiplier)
        {
            Vector2 anchor = _anchors[weapon];
            Vector2 toTarget = (Vector2)transform.InverseTransformPoint(target) - anchor;
            if (toTarget.sqrMagnitude < 0.0001f) toTarget = Vector2.right;
            // Broad phase only skips targets beyond every possible point on the tip path.
            float travel = weapon.EffectiveMeleeAnimation == MeleeAnimationStyle.Thrust
                ? weapon.Data.meleeThrustDistance : weapon.Data.meleeSwingOutwardDistance;
            float maxReach = tipLocal.magnitude + Mathf.Max(0f, travel) * Mathf.Max(0.05f, rangeMultiplier);
            float ownerScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            if ((target - (Vector2)transform.TransformPoint(anchor)).magnitude > maxReach * ownerScale + contactRadius)
                return false;
            float activeStart = Mathf.Clamp01(weapon.Data.meleeActiveStart);
            float activeEnd = Mathf.Clamp(weapon.Data.meleeActiveEnd, activeStart, 1f);
            Vector2 previous = Vector2.zero;

            for (int i = 0; i <= MeleeAttackGeometry.TrajectorySegments; i++)
            {
                float phase = Mathf.Lerp(activeStart, activeEnd, i / (float)MeleeAttackGeometry.TrajectorySegments);
                Vector2 tip = transform.TransformPoint(EvaluateMeleeTip(weapon, toTarget, rangeMultiplier, phase));
                if (MeleeAttackGeometry.TipSweepTouches(target, contactRadius, i == 0 ? tip : previous, tip)) return true;
                previous = tip;
            }
            return false;
        }

        private Vector2 EvaluateMeleeTip(WeaponInstance weapon, Vector2 direction, float rangeMultiplier, float phase)
        {
            MeleeAttackGeometry.EvaluatePose(weapon.Data, weapon.EffectiveMeleeAnimation, direction,
                rangeMultiplier, phase, out var offset, out var angle);
            return MeleeAttackGeometry.TipPosition(_anchors[weapon], _meleeTipOffsets[weapon], offset, angle);
        }

        public bool IsMeleeAttacking(WeaponInstance weapon) => _meleeTimers.ContainsKey(weapon);

        public bool PlayMeleeAttack(WeaponInstance weapon, Vector2 direction)
        {
            if (!_meleeTipOffsets.ContainsKey(weapon)) return false;
            _meleeDirections[weapon] = ((Vector2)transform.InverseTransformVector(direction)).normalized;
            _meleeTimers[weapon] = 0f;
            _meleePreviousTransforms[weapon] = transform.localToWorldMatrix;
            return true;
        }

        private void LateUpdate()
        {
            if (_meleeTimers.Count == 0 || Time.deltaTime <= 0f ||
                GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            _activeMelee.Clear();
            _activeMelee.AddRange(_meleeTimers.Keys);
            foreach (var weapon in _activeMelee)
            {
                if (!_weaponRenderers.TryGetValue(weapon, out var sr) || sr == null ||
                    !_weaponPivots.TryGetValue(weapon, out var pivot) || pivot == null)
                {
                    _meleeTimers.Remove(weapon);
                    _meleeDirections.Remove(weapon);
                    _meleePreviousTransforms.Remove(weapon);
                    if (_weaponSystem != null) _weaponSystem.CompleteMeleeAttack(weapon);
                    continue;
                }
                Vector2 dir = _meleeDirections[weapon];
                float duration = Mathf.Max(0.05f, weapon.Data.meleeAnimationDuration);
                if (_weaponSystem != null && _weaponSystem.Stats != null)
                    duration = Mathf.Min(duration, weapon.EffectiveInterval(_weaponSystem.Stats));
                float previousElapsed = _meleeTimers[weapon];
                float elapsed = previousElapsed + Time.deltaTime;
                float previousPhase = Mathf.Clamp01(previousElapsed / duration);
                float phase = Mathf.Clamp01(elapsed / duration);
                float rangeScale = _weaponSystem != null && _weaponSystem.Stats != null
                    ? Mathf.Max(0.05f, _weaponSystem.Stats.Get(StatType.RangeMult)) : 1f;
                MeleeAttackGeometry.EvaluatePose(weapon.Data, weapon.EffectiveMeleeAnimation,
                    dir, rangeScale, phase, out var handleOffset, out var angle);
                pivot.localPosition = _anchors[weapon] + (Vector3)handleOffset;
                pivot.localRotation = Quaternion.Euler(0f, 0f, angle);

                float activeStart = Mathf.Clamp01(weapon.Data.meleeActiveStart);
                float activeEnd = Mathf.Clamp(weapon.Data.meleeActiveEnd, activeStart, 1f);
                float traceStart = Mathf.Max(previousPhase, activeStart);
                float traceEnd = Mathf.Min(phase, activeEnd);
                if (traceEnd >= traceStart && _weaponSystem != null &&
                    _meleeTipOffsets.TryGetValue(weapon, out var tipLocal))
                {
                    Matrix4x4 currentTransform = transform.localToWorldMatrix;
                    Vector2 previousTip = transform.TransformPoint(
                        EvaluateMeleeTip(weapon, dir, rangeScale, traceStart));
                    if (previousPhase >= activeStart && _meleePreviousTransforms.TryGetValue(weapon, out var previousTransform))
                        previousTip = previousTransform.MultiplyPoint3x4(
                            EvaluateMeleeTip(weapon, dir, rangeScale, previousPhase));
                    int samples = Mathf.Max(1,
                        Mathf.CeilToInt((traceEnd - traceStart) * MeleeAttackGeometry.TrajectorySegments));
                    for (int i = 1; i <= samples; i++)
                    {
                        float samplePhase = Mathf.Lerp(traceStart, traceEnd, i / (float)samples);
                        var poseOffset = Vector2.zero;
                        var poseAngle = 0f;
                        MeleeAttackGeometry.EvaluatePose(weapon.Data, weapon.EffectiveMeleeAnimation,
                            dir, rangeScale, samplePhase, out poseOffset, out poseAngle);
                        Vector2 localTip = MeleeAttackGeometry.TipPosition(_anchors[weapon], tipLocal, poseOffset, poseAngle);
                        Vector2 currentTip = currentTransform.MultiplyPoint3x4(localTip);
                        Vector2 localBladeDirection = new Vector2(
                            Mathf.Cos(poseAngle * Mathf.Deg2Rad),
                            Mathf.Sin(poseAngle * Mathf.Deg2Rad));
                        Vector2 bladeDirection = currentTransform.MultiplyVector(localBladeDirection);
                        _weaponSystem.ResolveMeleeAttackHit(weapon, previousTip, currentTip,
                            samplePhase, bladeDirection);
                        previousTip = currentTip;
                    }
                }

                _meleePreviousTransforms[weapon] = transform.localToWorldMatrix;
                _meleeTimers[weapon] = elapsed;
                if (elapsed >= duration)
                {
                    _meleeTimers.Remove(weapon);
                    _meleeDirections.Remove(weapon);
                    _meleePreviousTransforms.Remove(weapon);
                    if (_weaponSystem != null) _weaponSystem.CompleteMeleeAttack(weapon);
                    pivot.localPosition = _anchors[weapon];
                    pivot.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                }
            }
        }

        /// <summary>
        /// 用枪口几何解瞄准方向。目标在枪口前方时让枪管与枪口到目标的射线共线；
        /// 目标贴近到枪管以内时保持枪朝目标，不允许一次修正把枪翻到反方向。
        /// </summary>
        public bool AimAtTarget(WeaponInstance weapon, Vector2 target, out Vector2 direction, out bool pointBlank)
        {
            direction = target - (Vector2)transform.position;
            pointBlank = false;
            if (!_weaponRenderers.TryGetValue(weapon, out var sr) || sr == null) return false;

            float side = target.x < transform.position.x ? -1f : 1f;
            var anchor = _anchors[weapon];
            sr.transform.localPosition = anchor;
            sr.flipY = side < 0f;

            Vector2 pivotToTarget = target - (Vector2)sr.transform.position;
            if (pivotToTarget.sqrMagnitude < 0.0001f)
                pivotToTarget = new Vector2(side, 0f);

            Vector2 centerToTarget = target - (Vector2)transform.position;
            if (centerToTarget.sqrMagnitude < 0.0001f) centerToTarget = new Vector2(side, 0f);
            float angle = Mathf.Atan2(pivotToTarget.y, pivotToTarget.x);
            if (TryGetLocalMuzzle(sr, out var localMuzzle))
            {
                float mx = localMuzzle.x * Mathf.Abs(sr.transform.lossyScale.x);
                float my = localMuzzle.y * Mathf.Abs(sr.transform.lossyScale.y);
                float distance = pivotToTarget.magnitude;
                float forward = Mathf.Sqrt(Mathf.Max(0f, distance * distance - my * my));
                pointBlank = distance <= Mathf.Abs(my) || forward <= mx + 0.05f;
                if (!pointBlank)
                    angle -= Mathf.Asin(Mathf.Clamp(my / distance, -1f, 1f));
            }

            // 贴身目标可能已经越过武器枢轴；此时仍让枪朝玩家所见的目标方向。
            if (pointBlank) angle = Mathf.Atan2(centerToTarget.y, centerToTarget.x);

            sr.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            if (!pointBlank && TryGetMuzzle(weapon, out var muzzle))
                direction = target - (Vector2)muzzle;
            else
                direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (direction.sqrMagnitude < 0.0001f) direction = new Vector2(side, 0f);
            direction.Normalize();
            return true;
        }

        /// <summary>用精灵网格的最右端定位枪口，避免透明留白让子弹从人物中心或画布边缘出现。</summary>
        public bool TryGetMuzzle(WeaponInstance weapon, out Vector3 position)
        {
            position = transform.position;
            if (!_weaponRenderers.TryGetValue(weapon, out var sr) || sr == null) return false;
            if (!TryGetLocalMuzzle(sr, out var localMuzzle)) return false;
            position = sr.transform.TransformPoint(localMuzzle);
            return true;
        }

        private bool TryGetLocalMuzzle(SpriteRenderer sr, out Vector2 muzzle)
        {
            muzzle = Vector2.zero;
            if (sr.sprite == null) return false;
            if (!_muzzleBySprite.TryGetValue(sr.sprite, out var unflipped))
            {
                unflipped = FindMuzzleInSprite(sr.sprite);
                _muzzleBySprite[sr.sprite] = unflipped;
            }
            muzzle = sr.flipY ? new Vector2(unflipped.x, -unflipped.y) : unflipped;
            return true;
        }

        private static Vector2 FindMuzzleInSprite(Sprite sprite)
        {
            var vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0) return new Vector2(sprite.bounds.max.x, 0f);
            float right = float.NegativeInfinity;
            float left = float.PositiveInfinity;
            foreach (var vertex in vertices)
            {
                right = Mathf.Max(right, vertex.x);
                left = Mathf.Min(left, vertex.x);
            }
            float threshold = right - (right - left) * 0.08f;
            float y = 0f;
            int count = 0;
            foreach (var vertex in vertices)
            {
                if (vertex.x < threshold) continue;
                y += vertex.y;
                count++;
            }
            if (count > 0) y /= count;
            return new Vector2(right, y);
        }
    }
}
