using System.Collections;
using System.Collections.Generic;
using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Items;
using UnityEngine;

namespace RogueLike.Enemies
{
    /// <summary>
    /// 波次导演：预算驱动出怪、节奏随波次收紧、敌人属性按曲线缩放、Boss 波调度。
    /// 计时结束 → 清场 → GameManager.EndWave（入账 + 进商店）。
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        public int CurrentWave { get; private set; }
        public float TimeLeft { get; private set; }
        public int CompletedChallengeReward { get; private set; }
        public DirectorChallengeOffer ActiveChallenge { get; private set; }
        private DirectorChallengeOffer _preparedChallenge;

        private float _budget;
        private bool _running;
        private bool _waveResolved;
        private Coroutine _spawnRoutine;
        private readonly List<Enemy> _aliveScratch = new List<Enemy>(128);

        private void OnEnable() => EventBus.EnemyKilled += OnEnemyKilled;
        private void OnDisable() => EventBus.EnemyKilled -= OnEnemyKilled;

        public void BeginRun()
        {
            CurrentWave = 0;
            _preparedChallenge = null;
            ActiveChallenge = null;
            CompletedChallengeReward = 0;
            EnemyPool.PrewarmAll();
        }

        public void PrepareChallenge(DirectorChallengeOffer offer) => _preparedChallenge = offer;

        public void StartWave(int wave)
        {
            CurrentWave = wave;
            _running = true;
            _waveResolved = false;
            var cfg = GameDatabase.WaveConfig;
            _budget = cfg.budgetBase + cfg.budgetPerWave * wave;
            CompletedChallengeReward = 0;
            ActiveChallenge = wave < cfg.waveCount ? _preparedChallenge : null;
            _preparedChallenge = null;
            if (ActiveChallenge != null)
            {
                var balance = GameDatabase.Balance;
                float multiplier = Mathf.Max(1f, ActiveChallenge.Modifier.budgetMultiplier) *
                    Mathf.Max(1f, ActiveChallenge.Tier.budgetMultiplier);
                _budget *= Mathf.Min(balance != null ? Mathf.Max(1f, balance.directorMaxBudgetMultiplier) : 2.2f, multiplier);
            }
            // Brotato-style pacing: start at 20s, add 5s per wave up to 60s,
            // then give the final wave its own longer timer. All values are SO-tunable.
            TimeLeft = wave >= cfg.waveCount
                ? cfg.finalWaveDuration
                : Mathf.Min(cfg.waveDurationCap, cfg.waveDurationStart + cfg.waveDurationPerWave * (wave - 1));

            if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
            _spawnRoutine = StartCoroutine(SpawnLoop());

            SpawnBossesIfNeeded(wave);
            if (ActiveChallenge != null)
            {
                int elites = ActiveChallenge.Modifier.openingEliteCount +
                    ActiveChallenge.Tier.openingEliteBonus;
                var balance = GameDatabase.Balance;
                for (int i = 0; i < Mathf.Min(balance != null ? Mathf.Max(0, balance.directorMaxOpeningElites) : 2, elites); i++)
                    EnemyPool.Spawn("elite_chaser", ArenaBounds.RandomEdgePoint(), HpScale(), DmgScale(), SpeedScale());
            }
            EventBus.RaiseWaveStarted(wave);
        }

        public void StopRun()
        {
            _running = false;
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
            EnemyManager.DespawnAll();
        }

        /// <summary>只供编辑器/Development GM 面板调用：结束本波并正常进入结算商店。</summary>
        public bool ForceEndWaveForTesting()
        {
            if (!_running || GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return false;
            // 最终波不能用测试按钮绕过 Boss 击杀条件。
            if (CurrentWave >= GameDatabase.WaveConfig.waveCount) return false;
            ResolveWave();
            return true;
        }

        private void OnEnemyKilled(Enemy enemy)
        {
            var cfg = GameDatabase.WaveConfig;
            if (!_running || _waveResolved || enemy == null || !enemy.IsBoss || CurrentWave < cfg.waveCount) return;
            EnemyManager.CopyAliveTo(_aliveScratch);
            for (int i = 0; i < _aliveScratch.Count; i++)
                if (_aliveScratch[i] != null && _aliveScratch[i].IsAlive && _aliveScratch[i].IsBoss) return;
            ResolveWave();
        }

        private void ResolveWave()
        {
            if (_waveResolved) return;
            _waveResolved = true;
            _running = false;
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
            EnemyManager.DespawnAll();
            CompletedChallengeReward = ActiveChallenge != null ? ActiveChallenge.Reward : 0;
            ActiveChallenge = null;
            if (GameManager.Instance != null) GameManager.Instance.EndWave();
        }

        private void FailFinalWave()
        {
            if (_waveResolved) return;
            _waveResolved = true;
            _running = false;
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
            if (GameManager.Instance != null) GameManager.Instance.FailRun();
            ActiveChallenge = null;
            CompletedChallengeReward = 0;
        }

        private void Update()
        {
            if (!_running) return;
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f)
            {
                if (CurrentWave >= GameDatabase.WaveConfig.waveCount) FailFinalWave();
                else ResolveWave();
            }
        }

        private IEnumerator SpawnLoop()
        {
            while (_running && _budget > 0f)
            {
                SpawnBatch();
                yield return new WaitForSeconds(CurrentInterval());
            }
        }

        private float CurrentInterval()
        {
            var cfg = GameDatabase.WaveConfig;
            float aliveRatio = EnemyManager.Count / (float)MaxAlive();
            float baseInt = cfg.spawnIntervalBase * Mathf.Pow(0.9f, CurrentWave - 1);
            if (ActiveChallenge != null)
                baseInt *= Mathf.Clamp(ActiveChallenge.Modifier.spawnIntervalMultiplier, 0.5f, 1f) *
                    Mathf.Clamp(ActiveChallenge.Tier.spawnIntervalMultiplier, 0.5f, 1f);
            return Mathf.Max(cfg.spawnIntervalMin, baseInt * (1f - 0.35f * aliveRatio));
        }

        private int MaxAlive()
        {
            var cfg = GameDatabase.WaveConfig;
            int bonus = ActiveChallenge != null ? ActiveChallenge.Modifier.maxAliveBonus +
                ActiveChallenge.Tier.maxAliveBonus : 0;
            var balance = GameDatabase.Balance;
            return cfg.maxAliveBase + cfg.maxAlivePerWave * CurrentWave +
                Mathf.Clamp(bonus, 0, balance != null ? Mathf.Max(0, balance.directorMaxAliveBonus) : 12);
        }

        private void SpawnBatch()
        {
            var cfg = GameDatabase.WaveConfig;
            if (EnemyManager.Count >= MaxAlive()) return;

            int perBatch = Mathf.Clamp(1 + CurrentWave / 8, 1, 4);
            for (int i = 0; i < perBatch && _budget > 0f && EnemyManager.Count < MaxAlive(); i++)
            {
                // 精英替换（预算 4 单位）
                if (CurrentWave >= cfg.eliteStartWave && Random.value < 0.08f && _budget >= 4f)
                {
                    EnemyPool.Spawn("elite_chaser", ArenaBounds.RandomEdgePoint(), HpScale(), DmgScale(), SpeedScale());
                    _budget -= 4f;
                    continue;
                }

                string id = PickWeightedEnemy();
                if (id == null) break;
                EnemyPool.Spawn(id, ArenaBounds.RandomEdgePoint(), HpScale(), DmgScale(), SpeedScale());
                _budget -= 1f;
            }
        }

        private string PickWeightedEnemy()
        {
            float total = 0f;
            foreach (var kv in GameDatabase.Enemies)
            {
                if (kv.Value.isBoss || kv.Value.isElite) continue;
                total += kv.Value.spawnWeight;
            }
            if (total <= 0f) return null;

            float r = Random.value * total;
            foreach (var kv in GameDatabase.Enemies)
            {
                if (kv.Value.isBoss || kv.Value.isElite) continue;
                r -= kv.Value.spawnWeight;
                if (r <= 0f) return kv.Key;
            }
            return null;
        }

        private void SpawnBossesIfNeeded(int wave)
        {
            var cfg = GameDatabase.WaveConfig;
            if (wave < cfg.waveCount && !cfg.bossWaves.Contains(wave)) return;

            if (wave >= cfg.waveCount)
            {
                EnemyPool.Spawn("boss1", ArenaBounds.RandomEdgePoint(), HpScale(), DmgScale(), SpeedScale());
                if (cfg.finalWaveBossCount >= 2)
                    EnemyPool.Spawn("boss2", ArenaBounds.RandomEdgePoint(), HpScale(), DmgScale(), SpeedScale());
            }
            else
            {
                EnemyPool.Spawn("boss1", ArenaBounds.RandomEdgePoint(), HpScale(), DmgScale(), SpeedScale());
            }
        }

        public float HpScale() => Mathf.Pow(GameDatabase.WaveConfig.hpGrowth, CurrentWave - 1);
        public float DmgScale() => Mathf.Pow(GameDatabase.WaveConfig.dmgGrowth, CurrentWave - 1);
        public float SpeedScale() => 1f + GameDatabase.WaveConfig.speedGrowth * (CurrentWave - 1);
    }
}
