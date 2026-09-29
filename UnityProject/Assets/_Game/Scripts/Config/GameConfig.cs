using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Core
{
    /// <summary>
    /// 场景中的配置入口；实际可调值统一放在 GameSettingsSO。
    /// </summary>
    public class GameConfig : MonoBehaviour
    {
        public static GameConfig Instance { get; private set; }

        public GameSettingsSO settings;
        public GameSettingsSO Settings => settings != null ? settings : Resources.Load<GameSettingsSO>("Config/GameSettings");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
    }
}
