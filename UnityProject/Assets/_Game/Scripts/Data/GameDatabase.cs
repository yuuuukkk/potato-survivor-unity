using System.Collections.Generic;
using RogueLike.Core;

namespace RogueLike.Data
{
    /// <summary>运行时内容数据库：加载后按 id 查询武器/敌人/道具/角色。</summary>
    public static class GameDatabase
    {
        public static Dictionary<string, WeaponData> Weapons = new Dictionary<string, WeaponData>();
        public static Dictionary<string, WeaponModificationData> WeaponModifications = new Dictionary<string, WeaponModificationData>();
        public static Dictionary<string, DirectorChallengeData> DirectorChallengeModifiers = new Dictionary<string, DirectorChallengeData>();
        public static Dictionary<string, EnemyData> Enemies = new Dictionary<string, EnemyData>();
        public static Dictionary<string, ItemData> Items = new Dictionary<string, ItemData>();
        public static List<CharacterData> Characters = new List<CharacterData>();
        public static WaveConfig WaveConfig = new WaveConfig();
        public static GameBalanceSO Balance;

        private static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            DataLoader.LoadAll();
            _loaded = true;
        }

        public static WeaponData GetWeapon(string id)
        {
            return id != null && Weapons.TryGetValue(id, out var w) ? w : null;
        }

        public static WeaponModificationData GetWeaponModification(string id)
        {
            return id != null && WeaponModifications.TryGetValue(id, out var modification) ? modification : null;
        }

        public static EnemyData GetEnemy(string id)
        {
            return id != null && Enemies.TryGetValue(id, out var e) ? e : null;
        }

        public static ItemData GetItem(string id)
        {
            return id != null && Items.TryGetValue(id, out var i) ? i : null;
        }

        public static CharacterData GetCharacter(int index)
        {
            return index >= 0 && index < Characters.Count ? Characters[index] : null;
        }
    }
}
