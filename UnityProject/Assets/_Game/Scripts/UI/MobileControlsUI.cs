using RogueLike.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueLike.UI
{
    /// <summary>手机端触控移动摇杆；自动避开刘海/圆角安全区，桌面端不显示。</summary>
    public sealed class MobileControlsUI : MonoBehaviour
    {
        private static MobileControlsUI _instance;

        private float _joystickSize = 184f;
        private float _safeMargin = 26f;
        private float _deadZone = 0.16f;
        private Color _trackColor = new Color(0.12f, 0.10f, 0.16f, 0.64f);
        private Color _knobColor = new Color(0.86f, 0.79f, 0.65f, 0.86f);

        public static Vector2 Movement { get; private set; }

        private RectTransform _safeRoot;
        private RectTransform _stickArea;
        private RectTransform _knob;
        private GameState _state;
        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;
        private float _moveRadius;

        private void Awake()
        {
            if (!Application.isMobilePlatform)
            {
                enabled = false;
                return;
            }

            _instance = this;
            LoadSettings();
            BuildControls();
            EventBus.StateChanged += OnStateChanged;
            OnStateChanged(GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu);
        }

        private void LoadSettings()
        {
            var settings = GameConfig.Instance != null ? GameConfig.Instance.Settings : null;
            if (settings == null) settings = Resources.Load<GameSettingsSO>("Config/GameSettings");
            if (settings == null) return;
            _joystickSize = Mathf.Max(120f, settings.mobileJoystickSize);
            _safeMargin = Mathf.Max(0f, settings.mobileJoystickSafeMargin);
            _deadZone = Mathf.Clamp(settings.mobileJoystickDeadZone, 0f, 0.5f);
            _trackColor = settings.mobileJoystickTrackColor;
            _knobColor = settings.mobileJoystickKnobColor;
        }

        private void OnDestroy()
        {
            EventBus.StateChanged -= OnStateChanged;
            if (_instance == this)
            {
                _instance = null;
                Movement = Vector2.zero;
            }
        }

        private void Update()
        {
            if (Screen.width == _lastWidth && Screen.height == _lastHeight && Screen.safeArea == _lastSafeArea)
                return;
            ApplySafeArea();
        }

        private void BuildControls()
        {
            var safeGo = new GameObject("MobileSafeArea", typeof(RectTransform));
            _safeRoot = safeGo.GetComponent<RectTransform>();
            _safeRoot.SetParent(transform, false);
            _safeRoot.anchorMin = Vector2.zero;
            _safeRoot.anchorMax = Vector2.one;
            _safeRoot.offsetMin = Vector2.zero;
            _safeRoot.offsetMax = Vector2.zero;

            var stickGo = new GameObject("MoveJoystick", typeof(RectTransform), typeof(Image), typeof(MobileJoystickSurface));
            _stickArea = stickGo.GetComponent<RectTransform>();
            _stickArea.SetParent(_safeRoot, false);
            _stickArea.anchorMin = Vector2.zero;
            _stickArea.anchorMax = Vector2.zero;
            _stickArea.pivot = new Vector2(0.5f, 0.5f);
            _stickArea.sizeDelta = new Vector2(_joystickSize, _joystickSize);
            _stickArea.anchoredPosition = new Vector2(_safeMargin + _joystickSize * 0.5f,
                _safeMargin + _joystickSize * 0.5f);

            var track = stickGo.GetComponent<Image>();
            track.sprite = MakeCircleSprite(true);
            track.color = _trackColor;
            track.raycastTarget = true;
            stickGo.GetComponent<MobileJoystickSurface>().Initialize(this);

            var knobGo = new GameObject("JoystickKnob", typeof(RectTransform), typeof(Image));
            _knob = knobGo.GetComponent<RectTransform>();
            _knob.SetParent(_stickArea, false);
            _knob.anchorMin = new Vector2(0.5f, 0.5f);
            _knob.anchorMax = new Vector2(0.5f, 0.5f);
            _knob.pivot = new Vector2(0.5f, 0.5f);
            _knob.sizeDelta = new Vector2(_joystickSize * 0.42f, _joystickSize * 0.42f);
            var knobImage = knobGo.GetComponent<Image>();
            knobImage.sprite = MakeCircleSprite(false);
            knobImage.color = _knobColor;
            knobImage.raycastTarget = false;
            _moveRadius = _joystickSize * 0.29f;

            ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            if (_safeRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            _safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            _safeRoot.offsetMin = Vector2.zero;
            _safeRoot.offsetMax = Vector2.zero;
            _lastSafeArea = safe;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
        }

        private void OnStateChanged(GameState state)
        {
            _state = state;
            bool visible = state == GameState.Playing;
            if (_stickArea != null) _stickArea.gameObject.SetActive(visible);
            if (!visible) SetMovement(Vector2.zero);
        }

        internal void SetPointerPosition(Vector2 screenPosition, Camera eventCamera)
        {
            if (_state != GameState.Playing || _stickArea == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _stickArea, screenPosition, eventCamera, out var localPoint)) return;

            Vector2 offset = Vector2.ClampMagnitude(localPoint, _moveRadius);
            _knob.anchoredPosition = offset;
            SetMovement(offset / _moveRadius);
        }

        internal void ReleasePointer()
        {
            if (_knob != null) _knob.anchoredPosition = Vector2.zero;
            SetMovement(Vector2.zero);
        }

        private void SetMovement(Vector2 value)
        {
            Movement = value.magnitude < _deadZone ? Vector2.zero : Vector2.ClampMagnitude(value, 1f);
        }

        private static Sprite MakeCircleSprite(bool ring)
        {
            const int resolution = 96;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = ring ? "MobileJoystickRing" : "MobileJoystickThumb",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[resolution * resolution];
            Vector2 center = new Vector2((resolution - 1) * 0.5f, (resolution - 1) * 0.5f);
            float radius = resolution * 0.5f - 1.5f;
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = ring
                    ? Mathf.Clamp01((1.02f - d) * 22f) * (d > 0.78f ? 0.9f : 0.34f)
                    : Mathf.Clamp01((1.02f - d) * 22f);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f), resolution);
        }
    }

    internal sealed class MobileJoystickSurface : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private MobileControlsUI _owner;

        public void Initialize(MobileControlsUI owner) => _owner = owner;

        public void OnPointerDown(PointerEventData eventData) => _owner?.SetPointerPosition(eventData.position, eventData.pressEventCamera);
        public void OnDrag(PointerEventData eventData) => _owner?.SetPointerPosition(eventData.position, eventData.pressEventCamera);

        public void OnPointerUp(PointerEventData eventData) => _owner?.ReleasePointer();
    }
}
