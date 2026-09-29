using RogueLike.Combat;
using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Player
{
    /// <summary>
    /// 玩家血量：最大生命来自属性、每秒回血、护甲/闪避减伤、0.2s 无敌帧、死亡广播。
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        public float MaxHp { get; private set; } = 100f;
        public float CurrentHp { get; private set; } = 100f;
        public bool DebugInvincible { get; set; }

        private PlayerStats _stats;
        private float _regenAcc;
        private float _iframes;
        private bool _dead;
        private SpriteRenderer _sprite;
        private Color _restColor = Color.white;
        private float _hitFlashTimer;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _sprite = GetComponent<SpriteRenderer>();
            if (_sprite != null) _restColor = _sprite.color;
        }

        public void Setup(CharacterData character)
        {
            RebuildMaxHp();
            CurrentHp = MaxHp;
            _dead = false;
            _iframes = 0f;
            _regenAcc = 0f;
            _hitFlashTimer = 0f;
            if (_sprite != null) _sprite.color = _restColor;
        }

        /// <summary>重新计算最大生命（升级/道具改变 MaxHp 后调用）。</summary>
        public void RebuildMaxHp()
        {
            MaxHp = _stats.Get(StatType.MaxHp);
            if (CurrentHp > MaxHp) CurrentHp = MaxHp;
        }

        public void Heal(float amount)
        {
            if (_dead || amount <= 0f) return;
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
            EventBus.RaisePlayerDamaged(CurrentHp, MaxHp);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (_dead || _iframes > 0f || DebugInvincible) return;

            // 闪避
            if (Random.value < _stats.Get(StatType.DodgeChance)) return;

            // 护甲（先减甲，下限 0）
            float armor = _stats.Get(StatType.Armor);
            float dmg = Mathf.Max(0f, info.amount - armor);
            if (dmg <= 0f) return;

            CurrentHp -= dmg;
            _iframes = 0.2f;
            EventBus.RaisePlayerDamaged(CurrentHp, MaxHp);
            DamageNumber.Spawn(transform.position + Vector3.up * 0.75f, dmg, false,
                new Color(1f, 0.36f, 0.36f));
            var balance = GameDatabase.Balance;
            CameraShake.Shake(balance != null ? balance.playerHitShakeDuration : 0.17f,
                balance != null ? balance.playerHitShakeMagnitude : 0.105f);
            _hitFlashTimer = balance != null ? balance.playerHitFlashDuration : 0.22f;
            if (_sprite != null) _sprite.color = balance != null ? balance.playerHitTint : new Color(1f, 0.38f, 0.38f);
            Vector2 away = info.source != null
                ? (Vector2)(transform.position - info.source.transform.position) : Vector2.zero;
            var controller = GetComponent<PlayerController>();
            if (away.sqrMagnitude < 0.001f) away = controller != null ? -controller.Facing : Vector2.left;
            if (controller != null) controller.ApplyHitRecoil(away, balance != null ? balance.playerHitRecoilSpeed : 2.6f);
            HitImpact.Spawn(transform.position + Vector3.up * 0.2f, away, false, true);
            CombatAudio.PlayPlayerHit();

            if (CurrentHp <= 0f)
            {
                CurrentHp = 0f;
                _dead = true;
                EventBus.RaisePlayerDied();
            }
        }

        private void Update()
        {
            if (_hitFlashTimer > 0f)
            {
                _hitFlashTimer -= Time.deltaTime;
                if (_hitFlashTimer <= 0f && _sprite != null) _sprite.color = _restColor;
            }
            if (_dead) return;
            if (_iframes > 0f) _iframes -= Time.deltaTime;

            float regen = _stats.Get(StatType.HpRegen);
            if (regen > 0f && CurrentHp < MaxHp)
            {
                _regenAcc += regen * Time.deltaTime;
                while (_regenAcc >= 1f)
                {
                    _regenAcc -= 1f;
                    Heal(1f);
                }
            }
        }
    }
}
