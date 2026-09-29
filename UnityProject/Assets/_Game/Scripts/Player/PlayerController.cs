using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Player
{
    /// <summary>玩家控制器：WASD 移动、竞技场钳制、朝向。攻击全自动（见 WeaponSystem）。</summary>
    [RequireComponent(typeof(PlayerStats), typeof(PlayerHealth))]
    public class PlayerController : MonoBehaviour
    {
        [HideInInspector] public float BaseMoveSpeed = 3.5f;
        public Vector2 Facing { get; private set; } = Vector2.right;

        public PlayerStats Stats { get; private set; }
        public PlayerHealth Health { get; private set; }

        private SpriteRenderer _sr;
        private Vector2 _movementFacing = Vector2.right;
        private Vector2 _combatFacing = Vector2.right;
        private int _combatFacingFrame = -1;
        private CircleCollider2D _bodyCollider;
        private Vector2 _artColliderCenter;
        private Vector2 _hitRecoil;

        private void Awake()
        {
            Stats = GetComponent<PlayerStats>();
            Health = GetComponent<PlayerHealth>();
            _sr = GetComponent<SpriteRenderer>();
            _bodyCollider = GetComponent<CircleCollider2D>();
        }

        /// <summary>开局设置：属性基线 + 血量。角色外观由预制体 Sprite 决定（不按配置色染色，保持素材原色）。</summary>
        public void Setup(CharacterData character)
        {
            _hitRecoil = Vector2.zero;
            if (_sr != null && character != null)
            {
                var configured = AssetLoader.LoadPlayerSprite(character.id);
                if (configured != null) _sr.sprite = configured;
                SyncColliderToArtwork();
            }
            Stats.Setup(character);
            Health.Setup(character);
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

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector2 dir = new Vector2(h, v) + RogueLike.UI.MobileControlsUI.Movement;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            float baseSpeed = GameDatabase.Balance != null ? GameDatabase.Balance.baseMoveSpeed : BaseMoveSpeed;
            float speed = baseSpeed * Stats.Get(StatType.MoveSpeedMult);
            transform.position += (Vector3)(dir * speed * Time.deltaTime);
            transform.position += (Vector3)(_hitRecoil * Time.deltaTime);
            _hitRecoil = Vector2.MoveTowards(_hitRecoil, Vector2.zero, 18f * Time.deltaTime);
            transform.position = ArenaBounds.Clamp(transform.position);

            if (dir.sqrMagnitude > 0.01f) _movementFacing = dir;
        }

        public void SetCombatFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            _combatFacing = direction.normalized;
            _combatFacingFrame = Time.frameCount;
        }

        public void ApplyHitRecoil(Vector2 direction, float speed)
        {
            if (direction.sqrMagnitude < 0.001f || speed <= 0f) return;
            _hitRecoil = direction.normalized * speed;
        }

        private void LateUpdate()
        {
            Facing = _combatFacingFrame == Time.frameCount ? _combatFacing : _movementFacing;
            if (_sr != null && Mathf.Abs(Facing.x) > 0.05f)
                _sr.flipX = Facing.x < 0f;
            if (_bodyCollider != null)
                _bodyCollider.offset = new Vector2(_sr != null && _sr.flipX ? -_artColliderCenter.x : _artColliderCenter.x, _artColliderCenter.y);
        }
    }
}
