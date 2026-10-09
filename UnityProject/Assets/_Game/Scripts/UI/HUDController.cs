using System.Globalization;
using RogueLike.Combat;
using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;
using UnityEngine.UI;

namespace RogueLike.UI
{
    /// <summary>HUD：血条、材料、波次/倒计时、武器栏摘要。全部由事件驱动。</summary>
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private Slider _hpBar;
        [SerializeField] private Slider _xpBar;
        [SerializeField] private Text _hpText;
        [SerializeField] private Text _matText;
        [SerializeField] private Text _waveText;
        [SerializeField] private Text _timerText;
        [SerializeField] private Text _weaponsText;
        [SerializeField] private Text _levelText;
        [SerializeField] private Text _xpText;
        [SerializeField] private Text _tipText;
        [SerializeField] private RectTransform _root;
        [Header("最后 10 秒倒计时")]
        [SerializeField, Range(1f, 1.6f)] private float _urgentTimerScale = 1.3f;
        [SerializeField, Range(0.1f, 1f)] private float _urgentTimerPulseDuration = 0.55f;
        [Header("最后 5 秒倒计时")]
        [SerializeField] private Color _criticalTimerColor = new Color(1f, 0.18f, 0.18f, 1f);
        [SerializeField, Range(1.2f, 4f)] private float _criticalTimerPulsesPerSecond = 2.5f;
        [SerializeField, Range(20, 48)] private int _criticalTimerFontSize = 32;
        [SerializeField, Range(1f, 2f)] private float _criticalTimerScale = 1.45f;
        [SerializeField] private Vector2 _criticalTimerOffset = new Vector2(0f, -26f);
        private Vector3 _timerBaseScale = Vector3.one;
        private Color _timerBaseColor = Color.white;
        private Vector2 _timerBasePosition;
        private int _timerBaseFontSize;
        private FontStyle _timerBaseFontStyle;
        private RectTransform _fpsPanel;
        private RectTransform _gmButton;
        private Text _fpsText;
        private bool _fpsVisible;
        private float _fpsElapsed;
        private float _worstFrameMs;
        private int _fpsFrames;
        private GameState _currentState;

        private void Awake()
        {
            // 场景中的 HUD 由用户编辑；删除单个元素时保留其余布局。
            // 仅当整个 HUD 容器不存在时才创建完整回退界面。
            if (_root == null) Build();
            if (_timerText != null)
            {
                _timerBaseScale = _timerText.rectTransform.localScale;
                _timerBaseColor = _timerText.color;
                _timerBasePosition = _timerText.rectTransform.anchoredPosition;
                _timerBaseFontSize = _timerText.fontSize;
                _timerBaseFontStyle = _timerText.fontStyle;
                if (_timerText.GetComponent<Outline>() == null) AddOutline(_timerText);
            }
            if (_tipText != null) _tipText.gameObject.SetActive(false);
            if (_weaponsText != null) _weaponsText.gameObject.SetActive(false);
            ApplyHudArt();
            _fpsVisible = GameDatabase.Balance == null || GameDatabase.Balance.showFpsOverlay;
            BuildFpsPanel();
            BuildGmButton();
            EventBus.StateChanged += OnStateChanged;
            EventBus.PlayerDamaged += OnPlayerDamaged;
            EventBus.MaterialsChanged += OnMaterialsChanged;
            EventBus.WaveStarted += OnWaveStarted;
            EventBus.WeaponsChanged += OnWeaponsChanged;
            EventBus.LevelChanged += OnLevelChanged;
            OnStateChanged(GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu);
            RefreshWeapons();
        }

        private void OnDestroy()
        {
            EventBus.StateChanged -= OnStateChanged;
            EventBus.PlayerDamaged -= OnPlayerDamaged;
            EventBus.MaterialsChanged -= OnMaterialsChanged;
            EventBus.WaveStarted -= OnWaveStarted;
            EventBus.WeaponsChanged -= OnWeaponsChanged;
            EventBus.LevelChanged -= OnLevelChanged;
        }

        private void Build()
        {
            // 独立容器：HUD 显隐只控制自己的对象，绝不影响 Canvas 下其它 UI（主菜单/商店/升级/结算）
            var rootGo = new GameObject("HUD");
            _root = rootGo.AddComponent<RectTransform>();
            _root.SetParent(transform, false);
            StretchToParent(_root);

            // 血条（红色 Slider，加粗）：文字作为 Slider 子节点，永远居中在条内
            _hpBar = UIFactory.CreateSlider(_root, new Vector2(16f, -16f), new Vector2(260f, 24f),
                new Color(0.18f, 0.06f, 0.06f), Color.red);
            _hpText = UIFactory.CreateText(_hpBar.transform, "15/15", 14, Color.white, Vector2.zero, new Vector2(260f, 24f));
            AddOutline(_hpText);

            // 经验条（绿色 Slider，加高到 16px）：Lv 与经验数值都在条内
            _xpBar = UIFactory.CreateSlider(_root, new Vector2(16f, -43f), new Vector2(260f, 18f),
                new Color(0.06f, 0.16f, 0.08f), Color.green);
            _levelText = UIFactory.CreateText(_xpBar.transform, "Lv.1", 12, new Color(0.7f, 1f, 0.8f),
                new Vector2(106f, 0f), new Vector2(70f, 18f), TextAnchor.MiddleRight);
            _xpText = UIFactory.CreateText(_xpBar.transform, "0/10", 12, Color.white, Vector2.zero, new Vector2(260f, 18f));
            AddOutline(_xpText);

            // 材料
            _matText = UIFactory.CreateText(_root, "0", 20, Color.white, new Vector2(52f, -73f), new Vector2(92f, 28f), TextAnchor.MiddleLeft);

            // 波次 + 倒计时
            _waveText = UIFactory.CreateText(_root, "第 1 波", 22, Color.white, Vector2.zero, new Vector2(220f, 30f));
            _timerText = UIFactory.CreateText(_root, "30s", 20, Color.white, new Vector2(0f, -61f), new Vector2(160f, 28f));

            // 操作提示
            _tipText = UIFactory.CreateText(_root, "", 13, new Color(0.8f, 0.8f, 0.82f), new Vector2(-16f, -78f), new Vector2(210f, 22f), TextAnchor.MiddleRight);

            // 武器栏
            _weaponsText = UIFactory.CreateText(_root, "", 18, Color.white, new Vector2(16f, 16f), new Vector2(300f, 120f), TextAnchor.LowerLeft);
            SetAnchored(_hpBar.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(260f, 24f));
            SetAnchored(_xpBar.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -43f), new Vector2(260f, 18f));
            SetAnchored(_xpText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(112f, 18f));
            SetAnchored(_levelText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(70f, 18f));
            _xpText.alignment = TextAnchor.MiddleLeft;
            SetAnchored(_matText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -73f), new Vector2(100f, 28f));
            SetAnchored(_waveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(220f, 30f));
            SetAnchored(_timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -61f), new Vector2(160f, 28f));
            SetAnchored(_tipText.rectTransform, Vector2.one, Vector2.one, new Vector2(-16f, -78f), new Vector2(210f, 22f));
        }

        /// <summary>给文字加黑色描边，保证在亮色条上清晰可见（不淡）。</summary>
        private static void AddOutline(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void ApplyHudArt()
        {
            var theme = Resources.Load<UIThemeSO>("Config/UITheme");
            StyleBar(_hpBar, theme != null ? theme.hudBarFrame : null, theme != null ? theme.healthFillColor : Color.red);
            StyleBar(_xpBar, theme != null ? theme.hudBarFrame : null, theme != null ? theme.experienceFillColor : Color.green);
            if (_matText == null || _root == null) return;
            _matText.text = "0";
            _matText.alignment = TextAnchor.MiddleLeft;
            var icon = _root.Find("MaterialIcon")?.GetComponent<Image>();
            if (icon == null)
            {
                icon = UIFactory.CreateImage(_root, Color.white, new Vector2(24f, -73f), new Vector2(26f, 26f));
                icon.name = "MaterialIcon";
                SetAnchored(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -73f), new Vector2(26f, 26f));
            }
            icon.sprite = AssetLoader.LoadMaterialPickupSprite();
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
        }

        private static void StretchToParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchored(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (rt == null) return;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static void StyleBar(Slider bar, Sprite frame, Color fillColor)
        {
            if (bar == null || bar.fillRect == null) return;
            var fill = bar.fillRect.GetComponent<Image>();
            if (fill != null)
            {
                fill.sprite = null;
                fill.type = Image.Type.Simple;
                fill.color = fillColor;
            }
            var background = bar.transform.Find("Image")?.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = frame;
                background.type = Image.Type.Simple;
                background.color = frame != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                background.raycastTarget = false;
            }
        }

        private void BuildFpsPanel()
        {
            var go = new GameObject("FpsPanel");
            _fpsPanel = go.AddComponent<RectTransform>();
            _fpsPanel.SetParent(_root, false);
            _fpsPanel.anchorMin = Vector2.one;
            _fpsPanel.anchorMax = Vector2.one;
            _fpsPanel.pivot = Vector2.one;
            _fpsPanel.anchoredPosition = new Vector2(-12f, -112f);
            _fpsPanel.sizeDelta = new Vector2(220f, 30f);
            var backdrop = go.AddComponent<Image>();
            backdrop.color = new Color(0.06f, 0.06f, 0.09f, 0.78f);
            backdrop.raycastTarget = false;
            _fpsText = UIFactory.CreateText(_fpsPanel, "FPS -- | 最慢 --ms", 14, Color.white,
                Vector2.zero, new Vector2(212f, 28f));
            AddOutline(_fpsText);
        }

        private void BuildGmButton()
        {
            if (!Application.isMobilePlatform) return;
            var go = new GameObject("GMButton");
            _gmButton = go.AddComponent<RectTransform>();
            _gmButton.SetParent(_root, false);
            _gmButton.anchorMin = new Vector2(1f, 1f);
            _gmButton.anchorMax = new Vector2(1f, 1f);
            _gmButton.pivot = new Vector2(1f, 1f);
            _gmButton.anchoredPosition = new Vector2(-24f, -150f);
            _gmButton.sizeDelta = new Vector2(96f, 48f);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.16f, 0.12f, 0.20f, 0.9f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                var panel = FindObjectOfType<GMPanel>();
                if (panel != null) panel.Toggle();
            });
            var label = UIFactory.CreateText(_gmButton, "GM", 20, Color.white, Vector2.zero, _gmButton.sizeDelta);
            label.raycastTarget = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3))
            {
                _fpsVisible = !_fpsVisible;
                if (_fpsPanel != null)
                    _fpsPanel.gameObject.SetActive(_fpsVisible && _currentState == GameState.Playing);
            }
            if (_fpsVisible && _currentState == GameState.Playing)
            {
                float frameSeconds = Time.unscaledDeltaTime;
                _fpsElapsed += frameSeconds;
                _fpsFrames++;
                _worstFrameMs = Mathf.Max(_worstFrameMs, frameSeconds * 1000f);
                if (_fpsElapsed >= 0.5f)
                {
                    if (_fpsText != null)
                        _fpsText.text = $"FPS {_fpsFrames / _fpsElapsed:0} | 最慢 {_worstFrameMs:0}ms";
                    _fpsElapsed = 0f;
                    _fpsFrames = 0;
                    _worstFrameMs = 0f;
                }
            }
            var gm = GameManager.Instance;
            if (_timerText != null && gm != null && gm.Director != null && gm.State == GameState.Playing)
            {
                float timeLeft = Mathf.Max(0f, gm.Director.TimeLeft);
                int seconds = Mathf.CeilToInt(timeLeft);
                bool critical = timeLeft > 0f && timeLeft <= 5f;
                _timerText.text = critical
                    ? (Mathf.Ceil(timeLeft * 10f) / 10f).ToString("0.0", CultureInfo.InvariantCulture) + "s"
                    : seconds + "s";
                _timerText.color = critical ? _criticalTimerColor : _timerBaseColor;
                _timerText.fontSize = critical ? _criticalTimerFontSize : _timerBaseFontSize;
                _timerText.fontStyle = critical ? FontStyle.Bold : _timerBaseFontStyle;
                _timerText.rectTransform.anchoredPosition = critical
                    ? _timerBasePosition + _criticalTimerOffset : _timerBasePosition;
                // 每秒跳字时放大再回到用户在 Inspector 里设定的原始大小。
                float pulse = 0f;
                if (critical)
                {
                    float phase = Mathf.Repeat((5f - timeLeft) * _criticalTimerPulsesPerSecond, 1f);
                    pulse = Mathf.Sin(phase * Mathf.PI);
                }
                else if (seconds > 0 && seconds <= 10)
                {
                    float elapsedInSecond = seconds - timeLeft;
                    float progress = Mathf.Clamp01(elapsedInSecond / Mathf.Max(0.1f, _urgentTimerPulseDuration));
                    pulse = Mathf.Sin(progress * Mathf.PI);
                }
                _timerText.rectTransform.localScale = _timerBaseScale *
                    (critical ? Mathf.Lerp(1.15f, _criticalTimerScale, pulse)
                        : Mathf.Lerp(1f, _urgentTimerScale, pulse));
            }
        }

        private void OnStateChanged(GameState state)
        {
            _currentState = state;
            bool show = state == GameState.Playing || state == GameState.Shop || state == GameState.LevelUp;
            if (_root != null) _root.gameObject.SetActive(show);
            if (_gmButton != null) _gmButton.gameObject.SetActive(state == GameState.Playing);
            if (_fpsPanel != null) _fpsPanel.gameObject.SetActive(_fpsVisible && state == GameState.Playing);
            if (state != GameState.Playing && _timerText != null)
            {
                _timerText.text = string.Empty;
                _timerText.rectTransform.localScale = _timerBaseScale;
                _timerText.color = _timerBaseColor;
                _timerText.fontSize = _timerBaseFontSize;
                _timerText.fontStyle = _timerBaseFontStyle;
                _timerText.rectTransform.anchoredPosition = _timerBasePosition;
            }
            _fpsElapsed = 0f;
            _fpsFrames = 0;
            _worstFrameMs = 0f;
        }

        private void OnPlayerDamaged(float hp, float maxHp)
        {
            if (_hpBar != null) _hpBar.value = Mathf.Clamp01(hp / Mathf.Max(1f, maxHp));
            if (_hpText != null) _hpText.text = $"HP {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";
        }

        private void OnMaterialsChanged(int materials)
        {
            if (_matText != null) _matText.text = materials.ToString();
        }

        private void OnLevelChanged(int level, int exp, int expToNext)
        {
            if (_levelText != null) _levelText.text = "Lv." + level;
            if (_xpBar != null) _xpBar.value = Mathf.Clamp01(exp / Mathf.Max(1f, expToNext));
            if (_xpText != null) _xpText.text = $"{exp}/{expToNext}";
        }

        private void OnWaveStarted(int wave)
        {
            if (_waveText != null) _waveText.text = $"第 {wave} 波";
        }

        private void OnWeaponsChanged()
        {
            RefreshWeapons();
        }

        private void RefreshWeapons()
        {
            if (_weaponsText == null) return;
            _weaponsText.text = ""; // 物品栏已展示武器，不再在左下角重复绘制文字。
        }
    }
}
