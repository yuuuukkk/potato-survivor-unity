using System.Collections.Generic;
using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Player
{
    /// <summary>玩家控制器：WASD 移动、竞技场钳制、朝向。攻击全自动（见 WeaponSystem）。</summary>
    [RequireComponent(typeof(PlayerStats), typeof(PlayerHealth))]
    public class PlayerController : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite[]> WalkFrameCache = new Dictionary<string, Sprite[]>();
        [HideInInspector] public float BaseMoveSpeed = 3.5f;
        public Vector2 Facing { get; private set; } = Vector2.right;

        public PlayerStats Stats { get; private set; }
        public PlayerHealth Health { get; private set; }
        public SpriteRenderer VisualRenderer => _sr;

        private SpriteRenderer _sr;
        private SpriteRenderer _rootRenderer;
        private SpriteRenderer _moveShadow;
        private Sprite _idleSprite;
        private Sprite[] _walkFrames;
        private int _walkFrameIndex = -1;
        private Animator _visualAnimator;
        [Header("移动时整体缩放（编辑 PlayerArt 子节点）")]
        [SerializeField] private Transform _visual;
        [SerializeField] private bool _driveWalkPulse = true;
        [SerializeField, Range(0f, 0.2f)] private float _walkPulseAmount = 0.08f;
        [SerializeField, Min(0f)] private float _walkSettleSpeed = 18f;
        [SerializeField] private bool _useWalkFrames;
        private float _walkPhase;
        private bool _walking;
        private Vector2 _movementFacing = Vector2.right;
        private Vector2 _combatFacing = Vector2.right;
        private int _combatFacingFrame = -1;
        private CircleCollider2D _bodyCollider;
        private Vector2 _artColliderCenter;
        private Vector3 _visualBaseScale = Vector3.one;
        private Vector3 _visualBasePosition;
        private Quaternion _visualBaseRotation = Quaternion.identity;
        private float _walkBlend;

        private void Awake()
        {
            Stats = GetComponent<PlayerStats>();
            Health = GetComponent<PlayerHealth>();
            _rootRenderer = GetComponent<SpriteRenderer>();
            _sr = _rootRenderer;
            _bodyCollider = GetComponent<CircleCollider2D>();
            if (_visual == null)
            {
                var art = transform.Find("PlayerArt");
                if (art != null && art.GetComponent<SpriteRenderer>() != null) _visual = art;
            }
            if (_visual != null)
            {
                _visualBaseScale = _visual.localScale;
                _visualBasePosition = _visual.localPosition;
                _visualBaseRotation = _visual.localRotation;
                _visualAnimator = _visual.GetComponent<Animator>();
            }
        }

        /// <summary>开局设置：属性基线 + 血量。角色外观由预制体 Sprite 决定（不按配置色染色，保持素材原色）。</summary>
        public void Setup(CharacterData character)
        {
            _walkPhase = 0f;
            _walkBlend = 0f;
            _walkFrameIndex = -1;
            _walking = false;
            if (_rootRenderer != null && character != null)
            {
                var configured = AssetLoader.LoadPlayerSprite(character.id);
                if (configured != null) _rootRenderer.sprite = configured;
                EnsureVisual();
                _sr.sprite = _rootRenderer.sprite;
                _idleSprite = _sr.sprite;
                _walkFrames = _useWalkFrames ? LoadWalkFrames(character.id) : null;
                _visual.localScale = _visualBaseScale;
                _visual.localPosition = _visualBasePosition;
                _visual.localRotation = _visualBaseRotation;
                SyncColliderToArtwork();
                EnsureMoveShadow();
            }
            Stats.Setup(character);
            Health.Setup(character);
        }

        private void EnsureVisual()
        {
            if (_visual != null)
            {
                _sr = _visual.GetComponent<SpriteRenderer>();
                _visualAnimator = _visual.GetComponent<Animator>();
                if (_sr != null)
                {
                    _rootRenderer.enabled = false;
                    return;
                }
            }
            if (_visual == null)
            {
                var existingArt = transform.Find("PlayerArt");
                if (existingArt != null) _visual = existingArt;
            }
            if (_visual != null && _visual.GetComponent<SpriteRenderer>() != null)
            {
                _sr = _visual.GetComponent<SpriteRenderer>();
                _visualAnimator = _visual.GetComponent<Animator>();
                _rootRenderer.enabled = false;
                return;
            }
            var art = new GameObject("PlayerArt");
            _visual = art.transform;
            _visual.SetParent(transform, false);
            _sr = art.AddComponent<SpriteRenderer>();
            _sr.sprite = _rootRenderer.sprite;
            _sr.color = _rootRenderer.color;
            _sr.sortingLayerID = _rootRenderer.sortingLayerID;
            _sr.sortingOrder = _rootRenderer.sortingOrder;
            _sr.sharedMaterial = _rootRenderer.sharedMaterial;
            _visualAnimator = art.GetComponent<Animator>();
            _visualBaseScale = _visual.localScale;
            _visualBasePosition = _visual.localPosition;
            _visualBaseRotation = _visual.localRotation;
            _rootRenderer.enabled = false;
        }

        private void SyncColliderToArtwork()
        {
            var circle = _bodyCollider != null ? _bodyCollider : GetComponent<CircleCollider2D>();
            if (circle == null || _sr == null || _sr.sprite == null) return;
            var vertices = _sr.sprite.vertices;
            if (vertices == null || vertices.Length == 0) return;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            foreach (var vertex in vertices)
            {
                minX = Mathf.Min(minX, vertex.x);
                maxX = Mathf.Max(maxX, vertex.x);
                minY = Mathf.Min(minY, vertex.y);
                maxY = Mathf.Max(maxY, vertex.y);
            }
            // 角色的帽子和手可能超出身体；碰撞圈取可见轮廓约 90%，不再使用固定小圆点。
            circle.radius = Mathf.Clamp(Mathf.Max(maxX - minX, maxY - minY) * 0.45f, 0.28f, 0.65f);
            _artColliderCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            circle.offset = _artColliderCenter;
        }

        private void EnsureMoveShadow()
        {
            var shadow = transform.Find("MoveShadow");
            if (shadow == null)
            {
                var go = new GameObject("MoveShadow");
                shadow = go.transform;
                shadow.SetParent(transform, false);
            }
            shadow.localPosition = new Vector3(0f, -0.48f, 0f);
            shadow.localScale = new Vector3(0.82f, 0.22f, 1f);
            _moveShadow = shadow.GetComponent<SpriteRenderer>();
            if (_moveShadow == null) _moveShadow = shadow.gameObject.AddComponent<SpriteRenderer>();
            _moveShadow.sprite = SpriteFactory.Circle(new Color(0.13f, 0.09f, 0.07f, 0.46f), 0.44f);
            _moveShadow.sortingLayerID = _sr.sortingLayerID;
            _moveShadow.sortingOrder = _sr.sortingOrder - 1;
        }

        private static Sprite[] LoadWalkFrames(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            if (WalkFrameCache.TryGetValue(characterId, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>("Art/Characters/character_" + characterId + "_walk");
            if (texture == null || texture.width < 4 || texture.width % 4 != 0)
            {
                WalkFrameCache[characterId] = null;
                return null;
            }

            // 按原角色的可见身高匹配比例；四帧来自同一张透明横排贴图。
            float pixelsPerUnit = characterId == "farmer" ? 430f :
                characterId == "chili" ? 605f : 550f;
            // 对齐旧待机图的可见身体中心，切换到走路帧时不会整个人跳位。
            Vector2 pivot = characterId == "farmer" ? new Vector2(0.623f, 0.472f) :
                characterId == "chili" ? new Vector2(0.400f, 0.677f) :
                new Vector2(0.396f, 0.343f);
            int frameWidth = texture.width / 4;
            var frames = new Sprite[4];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture,
                    new Rect(i * frameWidth, 0f, frameWidth, texture.height),
                    pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            WalkFrameCache[characterId] = frames;
            return frames;
        }

        private void Update()
        {
            _walking = false;
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector2 dir = new Vector2(h, v) + RogueLike.UI.MobileControlsUI.Movement;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            float baseSpeed = GameDatabase.Balance != null ? GameDatabase.Balance.baseMoveSpeed : BaseMoveSpeed;
            float speed = baseSpeed * Stats.Get(StatType.MoveSpeedMult);
            Vector3 beforeMove = transform.position;
            transform.position += (Vector3)(dir * speed * Time.deltaTime);
            transform.position = ArenaBounds.Clamp(transform.position);

            float moved = Vector3.Distance(beforeMove, transform.position);
            _walking = dir.sqrMagnitude > 0.01f && moved > 0.0001f;
            if (_walking)
            {
                var balance = GameDatabase.Balance;
                _walkPhase += moved * (balance != null ? balance.playerWalkStepFrequency : 5.5f);
            }

            if (dir.sqrMagnitude > 0.01f) _movementFacing = dir;
        }

        public void SetCombatFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            _combatFacing = direction.normalized;
            _combatFacingFrame = Time.frameCount;
        }

        private void LateUpdate()
        {
            Facing = _combatFacingFrame == Time.frameCount ? _combatFacing : _movementFacing;
            if (_sr != null && Mathf.Abs(Facing.x) > 0.05f)
                _sr.flipX = Facing.x < 0f;
            if (_bodyCollider != null)
                _bodyCollider.offset = new Vector2(_sr != null && _sr.flipX ? -_artColliderCenter.x : _artColliderCenter.x, _artColliderCenter.y);
            if (_visual != null)
            {
                bool animatorOwnsScale = _visualAnimator != null && _visualAnimator.runtimeAnimatorController != null;
                if (!animatorOwnsScale)
                {
                    float settleSpeed = _driveWalkPulse ? _walkSettleSpeed : 100f;
                    float blend = 1f - Mathf.Exp(-settleSpeed * Time.unscaledDeltaTime);
                    _walkBlend = Mathf.Lerp(_walkBlend, _driveWalkPulse && _walking ? 1f : 0f, blend);
                    // 以待机大小为中心等比放大、缩小；不压扁、不倾斜、不跳位。
                    float pulse = Mathf.Sin(_walkPhase) * _walkPulseAmount * _walkBlend;
                    _visual.localScale = Vector3.Lerp(_visual.localScale,
                        _visualBaseScale * (1f + pulse), blend);
                    if (_walkFrames != null && _walking)
                    {
                        int frame = Mathf.FloorToInt(_walkPhase * (2f / Mathf.PI)) % _walkFrames.Length;
                        if (frame != _walkFrameIndex)
                        {
                            _sr.sprite = _walkFrames[frame];
                            _walkFrameIndex = frame;
                        }
                    }
                    else if (_walkFrameIndex >= 0)
                    {
                        _sr.sprite = _idleSprite;
                        _walkFrameIndex = -1;
                    }
                }
            }
        }
    }
}
