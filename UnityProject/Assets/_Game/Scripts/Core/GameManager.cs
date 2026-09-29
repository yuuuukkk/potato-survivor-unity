using RogueLike.Combat;
using RogueLike.Data;
using RogueLike.Enemies;
using RogueLike.Items;
using RogueLike.Player;
using UnityEngine;

namespace RogueLike.Core
{
    /// <summary>
    /// 流程中枢：状态机 + 材料账本 + 系统接线。唯一的“接线层”，UI 不直接引用系统。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.MainMenu;
        public int Wave { get; private set; }
        public int Materials { get; private set; }
        public int Kills { get; private set; }
        public bool RunWon { get; private set; }
        public int LastWaveKills { get; private set; }
        public float LastWaveHpPercent { get; private set; } = 1f;
        private int _killsAtWaveStart;

        public PlayerController Player { get; set; }
        public WaveDirector Director { get; set; }
        public ShopSystem Shop { get; set; }

        private CharacterData _lastCharacter;

        private void Awake()
        {
            Instance = this;
            EventBus.PlayerDied += OnPlayerDied;
            EventBus.LevelUp += OnLevelUp;
            EventBus.LevelUpChoiceApplied += OnLevelUpChoiceApplied;
        }

        private void OnDestroy()
        {
            EventBus.PlayerDied -= OnPlayerDied;
            EventBus.LevelUp -= OnLevelUp;
            EventBus.LevelUpChoiceApplied -= OnLevelUpChoiceApplied;
        }

        /// <summary>升级瞬间：暂停游戏，等待四选一（LevelUpUI 选择后恢复）。防重：已在 LevelUp 状态则忽略重复事件。</summary>
        private void OnLevelUp(int level)
        {
            if (State == GameState.LevelUp)
            {
                return;
            }
            SetState(GameState.LevelUp);
            Time.timeScale = 0f;
        }

        private void OnLevelUpChoiceApplied()
        {
            Time.timeScale = 1f;
            SetState(GameState.Playing);
        }

        public void SetState(GameState state)
        {
            State = state;
            EventBus.RaiseStateChanged(state);
        }

        // ---------- 流程 ----------

        public void StartRun(CharacterData character)
        {
            _lastCharacter = character;
            Time.timeScale = 1f;
            Materials = character != null ? character.startMaterials : 0;
            Kills = 0;
            Wave = 0;
            RunWon = false;
            LastWaveKills = 0;
            LastWaveHpPercent = 1f;
            _killsAtWaveStart = 0;
            if (Shop != null) Shop.ResetForNewRun();

            EventBus.RaiseMaterialsChanged(Materials);
            // 必须先从旧属性上卸下上一局道具，再重建角色基础属性。
            // 反过来会把旧道具修正从新角色属性中再次扣除。
            var inv = Player.GetComponent<Inventory>();
            if (inv != null) inv.Reset();
            Player.Setup(character);
            Player.transform.position = Vector3.zero;
            // 开局广播满血状态：HUD 血条从角色实际 MaxHp 开始（避免显示硬编码 100/100）
            EventBus.RaisePlayerDamaged(Player.Health.CurrentHp, Player.Health.MaxHp);
            var ws = Player.GetComponent<WeaponSystem>();
            if (ws != null) ws.Setup(character);
            var xp = Player.GetComponent<XpSystem>();
            if (xp != null) xp.Reset();
            EventBus.RaiseLevelChanged(xp != null ? xp.Level : 1, xp != null ? xp.Exp : 0, xp != null ? xp.ExpToNext : 10);

            SetState(GameState.Playing);
            EventBus.RaiseRunStarted(character);
            Director.BeginRun();
            Director.StartWave(1);
        }

        public void StartNextWave(DirectorChallengeOffer challenge = null)
        {
            _killsAtWaveStart = Kills;
            Director.PrepareChallenge(challenge);
            SetState(GameState.Playing);
            Director.StartWave(Wave + 1);
        }

        /// <summary>波次计时结束（由 WaveDirector 调用）：入账波奖励 → 进入商店。</summary>
        public void EndWave()
        {
            Wave = Director.CurrentWave;
            LastWaveKills = Mathf.Max(0, Kills - _killsAtWaveStart);
            LastWaveHpPercent = Player != null && Player.Health != null && Player.Health.MaxHp > 0f
                ? Mathf.Clamp01(Player.Health.CurrentHp / Player.Health.MaxHp) : 1f;
            var cfg = GameDatabase.WaveConfig;
            AddMaterials(ItemPool.BagLooseMaterials());
            AddMaterials(cfg.waveBonusBase + cfg.waveBonusPerWave * Wave);
            AddMaterials(Director.CompletedChallengeReward);
            EventBus.RaiseWaveEnded(Wave, Materials);
            if (Wave >= cfg.waveCount)
            {
                RunWon = true;
                Director.StopRun();
                ItemPool.ClearActivePickups();
                SetState(GameState.GameOver);
                EventBus.RaiseGameOver(Wave, Kills);
                return;
            }
            EnterShop();
        }

        public void EnterShop()
        {
            // 先填充商品、后广播状态：ShopUI 在收到 StateChanged 时立即刷新，
            // 若顺序颠倒会先刷新到空列表、导致商店无商品。
            Shop.Open(Wave);
            SetState(GameState.Shop);
            EventBus.RaiseShopOpened();
        }

        public void CloseShop(DirectorChallengeOffer challenge = null)
        {
            if (State != GameState.Shop) return;
            Shop.Close();
            EventBus.RaiseShopClosed();
            StartNextWave(challenge);
        }

        public void OnPlayerDied()
        {
            FailRun();
        }

        public void FailRun()
        {
            if (State == GameState.GameOver) return;
            Director.StopRun();
            Wave = Director.CurrentWave;
            RunWon = false;
            ItemPool.ClearActivePickups();
            SetState(GameState.GameOver);
            EventBus.RaiseGameOver(Wave, Kills);
        }

        // ---------- 账本 ----------

        public void AddMaterials(int amount)
        {
            if (amount <= 0) return;
            Materials += amount;
            EventBus.RaiseMaterialsChanged(Materials);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Materials < amount) return false;
            Materials -= amount;
            EventBus.RaiseMaterialsChanged(Materials);
            return true;
        }

        public void AddKill() => Kills++;

        // ---------- 结算 / 重开 ----------

        public void Restart()
        {
            CleanupRun();
            StartRun(_lastCharacter != null ? _lastCharacter : GameDatabase.GetCharacter(0));
        }

        public void BackToMenu()
        {
            CleanupRun();
            SetState(GameState.MainMenu);
        }

        private void CleanupRun()
        {
            Director.StopRun();
            GamePools.ClearAll();
            WeaponPool.Clear();
            EnemyPool.Clear();
            ItemPool.Clear();
            EnemyManager.Clear();
        }
    }
}
