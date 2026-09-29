using System;
using RogueLike.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueLike.UI
{
    /// <summary>uGUI 运行时构建工厂：画布、事件系统、文本、图片、按钮。全部代码创建，无场景预设依赖。</summary>
    public static class UIFactory
    {
        public static Canvas MainCanvas;

        private static Font _font;
        private static UIThemeSO _theme;
        private static UIThemeSO Theme => _theme != null ? _theme : (_theme = Resources.Load<UIThemeSO>("Config/UITheme"));
        public static Color ButtonTextColor => Theme != null ? Theme.buttonTextColor : Color.white;
        public static Color UnaffordableColor => Theme != null ? Theme.unaffordableColor : new Color(1f, 0.3f, 0.3f);

        public static Font DefaultFont
        {
            get
            {
                if (_font != null) return _font;
                if (Theme != null && Theme.font != null) return _font = Theme.font;
                if (Theme != null && !string.IsNullOrEmpty(Theme.fallbackFontName))
                    _font = Font.CreateDynamicFontFromOSFont(Theme.fallbackFontName, 16);
                if (_font != null) return _font;
                try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { _font = null; }
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { _font = null; }
                }
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0f;
            go.AddComponent<UIResponsiveScaler>();
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>为场景/旧预制体中尚未指定贴图的 UI 换上统一圆角素材。</summary>
        public static void ApplyThemeToExistingUI(Canvas canvas)
        {
            if (canvas == null || Theme == null) return;
            foreach (var img in canvas.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite != null || img.type == Image.Type.Filled) continue;
                if (img.GetComponentInParent<Slider>() != null) continue;
                bool isButton = img.GetComponent<Button>() != null;
                var sprite = isButton ? Theme.roundedButton : Theme.roundedPanel;
                if (sprite == null) continue;
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = isButton ? Theme.buttonColor : PanelTint(img.color);
            }
            foreach (var label in canvas.GetComponentsInChildren<Text>(true))
            {
                label.font = DefaultFont;
                if (label.GetComponentInParent<Button>() != null && label.color == Color.white)
                    label.color = ButtonTextColor;
            }
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<ButtonFeel>() != null) continue;
                var feel = button.gameObject.AddComponent<ButtonFeel>();
                feel.Configure((RectTransform)button.transform, button.transform.localScale);
                button.onClick.AddListener(feel.PlayClick);
            }
        }

        public static EventSystem CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            var es = go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            return es;
        }

        /// <summary>Give newly opened screens a deterministic keyboard/gamepad starting focus.</summary>
        public static void FocusFirstButton(RectTransform screen)
        {
            if (screen == null || EventSystem.current == null) return;
            var buttons = screen.GetComponentsInChildren<Button>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (!buttons[i].interactable || !buttons[i].gameObject.activeInHierarchy) continue;
                EventSystem.current.SetSelectedGameObject(buttons[i].gameObject);
                return;
            }
        }

        public static Text CreateText(Transform parent, string text, int size, Color color,
            Vector2 anchoredPos, Vector2 sizeDelta, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Text_" + (text.Length > 12 ? text.Substring(0, 12) : text));
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var t = go.AddComponent<Text>();
            t.font = DefaultFont;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            // 文字超容器宽度自动换行（避免描述长文本被截断/溢出）；垂直方向允许溢出（多行内容）
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Image CreateImage(Transform parent, Color color, Vector2 anchoredPos, Vector2 sizeDelta, bool filled = false)
        {
            var go = new GameObject("Image");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.color = color;
            if (!filled && Theme != null && Theme.roundedPanel != null)
            {
                img.sprite = Theme.roundedPanel;
                img.type = Image.Type.Sliced;
                img.color = PanelTint(color);
            }
            img.raycastTarget = false;
            if (filled)
            {
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillAmount = 1f;
            }
            return img;
        }

        private static Color PanelTint(Color color)
        {
            // 底图本身已有深色颜料；再次乘深色会变成一整块近黑。
            return Mathf.Max(color.r, color.g, color.b) < 0.45f
                ? new Color(1f, 1f, 1f, color.a) : color;
        }

        public static Button CreateButton(Transform parent, string label, Action onClick,
            Vector2 anchoredPos, Vector2 sizeDelta, int fontSize = 22)
        {
            var go = new GameObject("Button_" + label);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.color = Theme != null ? Theme.buttonColor : new Color(0.23f, 0.23f, 0.30f);
            if (Theme != null && Theme.roundedButton != null)
            {
                img.sprite = Theme.roundedButton;
                img.type = Image.Type.Sliced;
            }
            img.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.88f, 0.66f);
            colors.pressedColor = new Color(0.86f, 0.7f, 0.48f);
            colors.selectedColor = new Color(1f, 0.94f, 0.78f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            // Short scale feedback is cosmetic only; it never delays button activation.
            var feedback = go.AddComponent<ButtonFeel>();
            feedback.Configure(rt, Vector3.one);
            btn.onClick.AddListener(feedback.PlayClick);

            CreateText(rt, label, fontSize, ButtonTextColor, Vector2.zero, sizeDelta);
            return btn;
        }

        /// <summary>
        /// 创建标准 uGUI Slider（Background + Fill Area + Fill 三层结构，层级清晰、可在 Inspector 可视化调整）。
        /// 仅用于数值显示（interactable=false），代码只需设置 value 即可控制填充。
        /// </summary>
        public static Slider CreateSlider(Transform parent, Vector2 anchoredPos, Vector2 sizeDelta,
            Color? bgColor = null, Color? fillColor = null)
        {
            var go = new GameObject("Slider");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var slider = go.AddComponent<Slider>();
            slider.interactable = false; // 只读显示，不响应拖拽
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.direction = Slider.Direction.LeftToRight;

            // Background：拉伸填满
            var bg = CreateImage(rt, bgColor ?? new Color(0.18f, 0.06f, 0.06f), Vector2.zero, Vector2.zero);
            var bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Fill Area：留 2px 内边距，避免 fill 贴边
            var fillAreaGo = new GameObject("Fill Area");
            var fillAreaRt = fillAreaGo.AddComponent<RectTransform>();
            fillAreaRt.SetParent(rt, false);
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = new Vector2(2f, 2f);
            fillAreaRt.offsetMax = new Vector2(-2f, -2f);

            // Fill：初始锚点拉满，Slider 按 value 控制 anchorMax.x（LeftToRight）
            var fill = CreateImage(fillAreaRt, fillColor ?? new Color(0.92f, 0.25f, 0.22f), Vector2.zero, Vector2.zero);
            var fillRt = (RectTransform)fill.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            slider.fillRect = fillRt;
            slider.value = 1f;
            return slider;
        }
    }

}
