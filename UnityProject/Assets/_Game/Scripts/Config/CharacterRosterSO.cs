using System.Collections.Generic;
using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Core
{
    [CreateAssetMenu(fileName = "CharacterRoster", menuName = "土豆幸存者/配置/角色列表")]
    public class CharacterRosterSO : ScriptableObject
    {
        public List<CharacterData> characters = new List<CharacterData>();
    }
}
