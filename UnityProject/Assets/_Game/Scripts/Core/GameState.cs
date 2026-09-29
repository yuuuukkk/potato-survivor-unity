namespace RogueLike.Core
{
    /// <summary>游戏全局状态。</summary>
    public enum GameState
    {
        MainMenu, // 主菜单（选角色）
        Playing,  // 波次战斗
        Shop,     // 波间商店
        LevelUp,  // 升级三选一（timeScale=0 暂停）
        GameOver  // 结算
    }
}
