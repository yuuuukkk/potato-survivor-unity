using RogueLike.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RogueLike.UI
{
    /// <summary>
    /// 通用悬停描述（Tooltip）：底部物品栏/属性面板等鼠标悬停时显示名称 + 描述。
    /// 跟随鼠标，Overlay 画布坐标（含 CanvasScaler 缩放换算）。
    /// </summary>
    public class TooltipUI : MonoBehaviour
    {
        public static TooltipUI Instance { get; private set; }

        [SerializeField] private RectTransform _panel;
        [SerializeField] private Image _bg;
        [SerializeField] private Text _title;
        [SerializeField] private Text _body;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private bool _visible;

        private void Awake()
        {
            Instance = this;
            _canvas = GetComponentInParent<Canvas>();
            _canvasRect = (RectTransform)_canvas.transform;

            // 优先使用预制体里的面板；无预制体时回退代码创建
            if (_panel == null)
            {
                _panel = (RectTransform)UIFactory.CreateImage(transform,
                    new Color(0.08f, 0.08f, 0.12f, 0.96f), new Vector2(-600f, 320f), new Vector2(420f, 210f)).rectTransform;
                _bg = _panel.GetComponent<Image>();
                _title = UIFactory.CreateText(_panel, "", 17, Color.white, new Vector2(0f, 78f), new Vector2(392f, 32f));
                _body = UIFactory.CreateText(_panel, "", 16, new Color(0.80f, 0.80f, 0.88f), new Vector2(0f, -10f), new Vector2(392f, 148f));
                _body.alignment = TextAnchor.UpperCenter;
            }

            _panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_visible) return;
            // Overlay 画布：anchoredPosition = (鼠标像素 - 屏幕中心) / scaleFactor
            float scale = Mathf.Max(0.001f, _canvas.scaleFactor);
            Vector2 sizePixels = _panel.rect.size * scale;
            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f)
                safe = new Rect(0f, 0f, Screen.width, Screen.height);
            Vector2 pointer = Input.mousePosition;
            Vector2 center = pointer + sizePixels * 0.5f + new Vector2(12f, 12f);
            center.x = Mathf.Clamp(center.x, safe.xMin + sizePixels.x * 0.5f, safe.xMax - sizePixels.x * 0.5f);
            center.y = Mathf.Clamp(center.y, safe.yMin + sizePixels.y * 0.5f, safe.yMax - sizePixels.y * 0.5f);
            _panel.anchoredPosition = (center - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)) / scale;
            _panel.SetAsLastSibling();
        }

        public void Show(string title, string body, Color color, Vector2 size)
        {
            _visible = true;
            _title.text = title;
            _title.color = color;
            _body.text = body;
            _panel.gameObject.SetActive(true);
        }

        public void Hide()
        {
            _visible = false;
            _panel.gameObject.SetActive(false);
        }
    }
}
