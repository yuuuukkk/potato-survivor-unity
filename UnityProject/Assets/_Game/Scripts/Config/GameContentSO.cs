using System.Collections.Generic;
using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Core
{
    [CreateAssetMenu(fileName = "GameContent", menuName = "土豆幸存者/配置/武器敌人道具")]
    public class GameContentSO : ScriptableObject
    {
        public List<WeaponData> weapons = new List<WeaponData>();
        public List<WeaponModificationData> weaponModifications = new List<WeaponModificationData>();
        public List<DirectorChallengeData> directorChallengeModifiers = new List<DirectorChallengeData>();
        public List<EnemyData> enemies = new List<EnemyData>();
        public List<ItemData> items = new List<ItemData>();
    }

}
