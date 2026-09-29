namespace RogueLike.Data
{
    /// <summary>武器行为类型：决定 WeaponInstance.Fire 的分派。</summary>
    public enum WeaponKind
    {
        Projectile = 0, // 直射子弹（可散射/穿透/爆炸）
        Melee = 1,      // 近战扇形范围伤害
        Orbit = 2,      // 环绕玩家的切割体
        Turret = 3,     // 固定炮台自行索敌
        Boomerang = 4   // 往返弹道，去回各命中一次
    }
}
