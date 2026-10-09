using UnityEngine;

namespace RogueLike.Core
{
    [CreateAssetMenu(fileName = "UITheme", menuName = "土豆幸存者/配置/界面主题")]
    public class UIThemeSO : ScriptableObject
    {
        [Header("字体和通用圆角素材")]
        public Font font;
        public string fallbackFontName = "Microsoft YaHei";
        public Sprite roundedPanel;
        public Sprite roundedButton;
        public Sprite roundedCard;
        public Sprite shopCardFrame;
        public Sprite hudBarFrame;
        [Header("颜色")]
        public Color panelColor = new Color(0.13f, 0.12f, 0.18f, 0.98f);
        public Color buttonColor = new Color(0.3f, 0.27f, 0.34f);
        public Color textColor = new Color(1f, 0.95f, 0.85f);
        public Color buttonTextColor = new Color(0.2f, 0.14f, 0.16f);
        public Color unaffordableColor = new Color(0.95f, 0.3f, 0.3f);
        public Color healthFillColor = Color.red;
        public Color experienceFillColor = Color.green;
        [Header("交互反馈")]
        public bool reduceUiMotion;
        public float buttonHoverScale = 1.045f;
        public float buttonPressedScale = 0.95f;
        public float buttonResponseSpeed = 18f;
    }
}
