using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Core
{
    [CreateAssetMenu(fileName = "GameSettings", menuName = "土豆幸存者/配置/场景与开局")]
    public class GameSettingsSO : ScriptableObject
    {
        public Vector2 arenaSize = new Vector2(24f, 13.5f);
        public float cameraSize = 5.2f;
        public float cameraFollowSmooth = 8f;
        [Tooltip("竞技场外的纯色背景，用于替代突兀的天空盒。")]
        public Color cameraBackgroundColor = new Color(0.055f, 0.05f, 0.08f, 1f);
        [Header("手机触控")]
        public float mobileJoystickSize = 184f;
        public float mobileJoystickSafeMargin = 26f;
        [Range(0f, 0.5f)] public float mobileJoystickDeadZone = 0.16f;
        public Color mobileJoystickTrackColor = new Color(0.12f, 0.10f, 0.16f, 0.64f);
        public Color mobileJoystickKnobColor = new Color(0.86f, 0.79f, 0.65f, 0.86f);
        public string defaultCharacterId = "farmer";
        public int startWave = 1;
        public bool skipMenu;
        public XpConfig xpConfig;
        [Header("可选 AI 构筑建议服务")]
        [Tooltip("可选 HTTPS 代理地址。Api Key 非空时优先由 Unity 直连 DeepSeek；两项都留空时使用本地改造测试。")]
        public string weaponForgeEndpoint;
        [Min(1)] public int weaponForgeTimeoutSeconds = 25;
        [Tooltip("可选：Unity 直连 DeepSeek 的 API Key。打包后可被提取；仅在你接受此风险时填写。留空则使用上方代理地址或本地测试。")]
        public string weaponForgeApiKey;
        [Tooltip("Unity 直连 DeepSeek 使用的模型名称。")]
        public string weaponForgeModel = "deepseek-flash";
    }
}
