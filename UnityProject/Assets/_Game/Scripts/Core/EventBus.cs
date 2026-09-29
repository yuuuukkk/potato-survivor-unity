using System;
using RogueLike.Data;
using RogueLike.Enemies;

namespace RogueLike.Core
{
    /// <summary>
    /// 全局事件总线：系统间解耦的唯一通道。UI 只订阅事件，不直接持有系统引用。
    /// </summary>
    public static class EventBus
    {
        public static event Action<int> WaveStarted;
        public static event Action<int, int> WaveEnded;          // (wave, materials)
        public static event Action<Enemy> EnemyKilled;
        public static event Action<int> MaterialsChanged;
        public static event Action<float, float> PlayerDamaged;  // (hp, maxHp)
        public static event Action PlayerDied;
        public static event Action ShopOpened;
        public static event Action ShopClosed;
        public static event Action<GameState> StateChanged;
        public static event Action<CharacterData> RunStarted;
        public static event Action WeaponsChanged;
        public static event Action InventoryChanged;
        public static event Action<int, int, int> LevelChanged;  // (level, exp, expToNext)
        public static event Action<int> LevelUp;                 // (newLevel)，升级瞬间广播（暂停游戏弹三选一）
        public static event Action LevelUpChoiceApplied;         // 升级选择已应用，恢复游戏
        public static event Action<int, int> GameOverEvent;      // (wave, kills)

        public static void RaiseWaveStarted(int wave) => WaveStarted?.Invoke(wave);
        public static void RaiseWaveEnded(int wave, int materials) => WaveEnded?.Invoke(wave, materials);
        public static void RaiseEnemyKilled(Enemy e) => EnemyKilled?.Invoke(e);
        public static void RaiseMaterialsChanged(int m) => MaterialsChanged?.Invoke(m);
        public static void RaisePlayerDamaged(float hp, float maxHp) => PlayerDamaged?.Invoke(hp, maxHp);
        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaiseShopOpened() => ShopOpened?.Invoke();
        public static void RaiseShopClosed() => ShopClosed?.Invoke();
        public static void RaiseStateChanged(GameState s) => StateChanged?.Invoke(s);
        public static void RaiseRunStarted(CharacterData c) => RunStarted?.Invoke(c);
        public static void RaiseWeaponsChanged() => WeaponsChanged?.Invoke();
        public static void RaiseInventoryChanged() => InventoryChanged?.Invoke();
        public static void RaiseLevelChanged(int level, int exp, int expToNext) => LevelChanged?.Invoke(level, exp, expToNext);
        public static void RaiseLevelUp(int newLevel) => LevelUp?.Invoke(newLevel);
        public static void RaiseLevelUpChoiceApplied() => LevelUpChoiceApplied?.Invoke();
        public static void RaiseGameOver(int wave, int kills) => GameOverEvent?.Invoke(wave, kills);
    }
}
