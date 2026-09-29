using System.Collections.Generic;
using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueLike.UI
{
    /// <summary>
    /// 主菜单：标题 + 选角色 + 开始/退出。
    /// 面板/按钮来自 UICanvas 预制体（可在 Inspector 可视化调整）；角色按钮按数据动态生成进容器。
    /// 无预制体时回退代码创建（同布局）。
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }

        [SerializeField] private RectTransform _panel;
        [SerializeField] private RectTransform _charGrid;   // 角色按钮容器
        [SerializeField] private Text _descText;
        [SerializeField] private Button _startBtn;
        [SerializeField] private Button _quitBtn;
        private Button _settingsBtn;
        private GameObject _settingsOverlay;
        private RectTransform _settingsPanel;
        private Text _resolutionText;
        private Text _fullscreenText;
        private readonly List<Resolution> _resolutions = new List<Resolution>();
        private int _resolutionIndex;
        private bool _fullscreen;
        private bool _settingsOpen;
        private bool _settingsReturnToMenu;
        private bool _pausedInGame;

        private const string ResolutionWidthKey = "RogueLike.ResolutionWidth";
        private const string ResolutionHeightKey = "RogueLike.ResolutionHeight";
        private const string FullscreenKey = "RogueLike.Fullscreen";

        private int _selectedIndex;
        private readonly List<Button> _charButtons = new List<Button>();

        private void Awake()
        {
            Instance = this;
            if (_panel == null) Build(); // 回退：无预制体时代码创建
            LoadDisplaySettings();
            BuildSettingsButton();
            BuildSettingsPanel();
            HookButtons();
            RefreshCharButtons();
            EventBus.StateChanged += OnStateChanged;
            Hide();
        }

        private void OnDestroy()
        {
            EventBus.StateChanged -= OnStateChanged;
            if (_pausedInGame) Time.timeScale = 1f;
        }

        /// <summary>回退布局：与预制体一致的结构（仅当 UICanvas 预制体不存在时使用）。</summary>
        private void Build()
        {
            // 面板底色与相机背景(0.06,0.06,0.10)区分开，保证可见
            _panel = (RectTransform)UIFactory.CreateImage(transform,
                new Color(0.13f, 0.13f, 0.21f, 0.99f), Vector2.zero, new Vector2(1120f, 640f)).rectTransform;

            UIFactory.CreateText(_panel, "土豆幸存者", 56, new Color(1f, 0.85f, 0.45f), new Vector2(0f, 250f), new Vector2(600f, 70f));
            UIFactory.CreateText(_panel, "自动攻击 · 走位生存 · 商店构筑 · 20 波", 20, new Color(0.75f, 0.75f, 0.82f), new Vector2(0f, 196f), new Vector2(700f, 30f));
            UIFactory.CreateText(_panel, "选择角色", 24, Color.white, new Vector2(0f, 130f), new Vector2(300f, 32f));

            var gridGo = new GameObject("CharGrid");
            _charGrid = gridGo.AddComponent<RectTransform>();
            _charGrid.SetParent(_panel, false);
            _charGrid.anchoredPosition = new Vector2(0f, 10f);
            _charGrid.sizeDelta = new Vector2(700f, 175f);

            _descText = UIFactory.CreateText(_panel, "", 16, new Color(0.75f, 0.75f, 0.82f), new Vector2(0f, -125f), new Vector2(800f, 50f));

            _startBtn = UIFactory.CreateButton(_panel, "开始游戏", null, new Vector2(0f, -210f), new Vector2(260f, 58f), 26);
            _quitBtn = UIFactory.CreateButton(_panel, "退出", null, new Vector2(0f, -275f), new Vector2(160f, 40f), 18);
        }

        private void HookButtons()
        {
            if (_startBtn != null)
                _startBtn.onClick.AddListener(() =>
                {
                    var c = GameDatabase.GetCharacter(_selectedIndex);
                    if (c != null && GameManager.Instance != null) GameManager.Instance.StartRun(c);
                });
            if (_quitBtn != null) _quitBtn.onClick.AddListener(Application.Quit);
            if (_settingsBtn != null) _settingsBtn.onClick.AddListener(OpenSettings);
        }

        private void BuildSettingsButton()
        {
            _settingsBtn = UIFactory.CreateButton(_panel, "设置",
                null, new Vector2(460f, 260f), new Vector2(124f, 44f), 18);
        }

        private void BuildSettingsPanel()
        {
            var shade = UIFactory.CreateImage(transform, new Color(0f, 0f, 0f, 0.78f),
                Vector2.zero, Vector2.zero);
            _settingsOverlay = shade.gameObject;
            var shadeRect = (RectTransform)shade.transform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;
            shade.raycastTarget = true;

            var panelImage = UIFactory.CreateImage(_settingsOverlay.transform,
                new Color(0.15f, 0.13f, 0.19f, 1f), Vector2.zero, new Vector2(560f, 360f));
            _settingsPanel = (RectTransform)panelImage.transform;
            UIFactory.CreateText(_settingsPanel, "设置", 34, Color.white,
                new Vector2(0f, 140f), new Vector2(420f, 48f));
            UIFactory.CreateText(_settingsPanel, "分辨率", 19, new Color(0.9f, 0.88f, 0.83f),
                new Vector2(-190f, 70f), new Vector2(110f, 42f), TextAnchor.MiddleLeft);
            UIFactory.CreateButton(_settingsPanel, "◀", PreviousResolution,
                new Vector2(-95f, 70f), new Vector2(48f, 44f), 20);
            _resolutionText = UIFactory.CreateText(_settingsPanel, "", 18, Color.white,
                new Vector2(45f, 70f), new Vector2(220f, 42f));
            UIFactory.CreateButton(_settingsPanel, "▶", NextResolution,
                new Vector2(185f, 70f), new Vector2(48f, 44f), 20);

            UIFactory.CreateText(_settingsPanel, "显示模式", 19, new Color(0.9f, 0.88f, 0.83f),
                new Vector2(-190f, 5f), new Vector2(110f, 42f), TextAnchor.MiddleLeft);
            UIFactory.CreateButton(_settingsPanel, "切换全屏", ToggleFullscreen,
                new Vector2(-20f, 5f), new Vector2(150f, 44f), 17);
            _fullscreenText = UIFactory.CreateText(_settingsPanel, "", 16, new Color(0.8f, 0.86f, 0.75f),
                new Vector2(155f, 5f), new Vector2(130f, 42f));

            UIFactory.CreateButton(_settingsPanel, "应用", ApplyDisplaySettings,
                new Vector2(-95f, -112f), new Vector2(150f, 48f), 20);
            UIFactory.CreateButton(_settingsPanel, "返回", CloseSettings,
                new Vector2(95f, -112f), new Vector2(150f, 48f), 20);
            _settingsOverlay.SetActive(false);
            RefreshSettingsLabels();
        }

        private void LoadDisplaySettings()
        {
            int savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.currentResolution.width);
            int savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.currentResolution.height);
            _fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;

            var available = Screen.resolutions;
            for (int i = 0; i < available.Length; i++)
            {
                bool duplicate = false;
                for (int j = 0; j < _resolutions.Count; j++)
                    if (_resolutions[j].width == available[i].width && _resolutions[j].height == available[i].height)
                    { duplicate = true; break; }
                if (!duplicate) _resolutions.Add(available[i]);
            }
            if (_resolutions.Count == 0) _resolutions.Add(Screen.currentResolution);
            _resolutions.Sort((a, b) => a.width != b.width
                ? a.width.CompareTo(b.width) : a.height.CompareTo(b.height));

            _resolutionIndex = 0;
            long bestDistance = long.MaxValue;
            for (int i = 0; i < _resolutions.Count; i++)
            {
                long dx = _resolutions[i].width - savedWidth;
                long dy = _resolutions[i].height - savedHeight;
                long distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                _resolutionIndex = i;
            }
            Screen.SetResolution(savedWidth, savedHeight,
                _fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        private void PreviousResolution()
        {
            if (_resolutions.Count == 0) return;
            _resolutionIndex = (_resolutionIndex - 1 + _resolutions.Count) % _resolutions.Count;
            RefreshSettingsLabels();
        }

        private void NextResolution()
        {
            if (_resolutions.Count == 0) return;
            _resolutionIndex = (_resolutionIndex + 1) % _resolutions.Count;
            RefreshSettingsLabels();
        }

        private void ToggleFullscreen()
        {
            _fullscreen = !_fullscreen;
            RefreshSettingsLabels();
        }

        private void RefreshSettingsLabels()
        {
            if (_resolutionText != null && _resolutions.Count > 0)
            {
                var resolution = _resolutions[Mathf.Clamp(_resolutionIndex, 0, _resolutions.Count - 1)];
                _resolutionText.text = $"{resolution.width} × {resolution.height}";
            }
            if (_fullscreenText != null) _fullscreenText.text = _fullscreen ? "全屏：开" : "全屏：关";
        }

        private void ApplyDisplaySettings()
        {
            if (_resolutions.Count == 0) return;
            var resolution = _resolutions[Mathf.Clamp(_resolutionIndex, 0, _resolutions.Count - 1)];
            Screen.SetResolution(resolution.width, resolution.height,
                _fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            PlayerPrefs.SetInt(ResolutionWidthKey, resolution.width);
            PlayerPrefs.SetInt(ResolutionHeightKey, resolution.height);
            PlayerPrefs.SetInt(FullscreenKey, _fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OpenSettings()
        {
            if (_settingsOpen) return;
            var gm = GameManager.Instance;
            _settingsReturnToMenu = gm == null || gm.State == GameState.MainMenu;
            _pausedInGame = gm != null && gm.State == GameState.Playing;
            if (_pausedInGame) Time.timeScale = 0f;
            _settingsOpen = true;
            _panel.gameObject.SetActive(false);
            _settingsOverlay.SetActive(true);
            RefreshSettingsLabels();
            if (EventSystem.current != null) UIFactory.FocusFirstButton(_settingsPanel);
        }

        private void CloseSettings()
        {
            if (!_settingsOpen) return;
            _settingsOpen = false;
            _settingsOverlay.SetActive(false);
            if (_pausedInGame) Time.timeScale = 1f;
            _pausedInGame = false;
            var gm = GameManager.Instance;
            _panel.gameObject.SetActive(_settingsReturnToMenu && (gm == null || gm.State == GameState.MainMenu));
            if (_panel.gameObject.activeSelf) UIFactory.FocusFirstButton(_panel);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_settingsOpen) CloseSettings();
                else if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
                    OpenSettings();
            }
        }

        /// <summary>角色按钮按数据动态生成进容器（内容随 characters.json 变化）。</summary>
        private void RefreshCharButtons()
        {
            _charGrid.anchoredPosition = new Vector2(0f, 10f);
            _charGrid.sizeDelta = new Vector2(700f, 175f);
            if (_descText != null) _descText.rectTransform.anchoredPosition = new Vector2(0f, -125f);
            if (_startBtn != null) ((RectTransform)_startBtn.transform).anchoredPosition = new Vector2(0f, -210f);
            if (_quitBtn != null) ((RectTransform)_quitBtn.transform).anchoredPosition = new Vector2(0f, -275f);
            for (int i = _charGrid.childCount - 1; i >= 0; i--)
                Destroy(_charGrid.GetChild(i).gameObject);
            _charButtons.Clear();

            for (int i = 0; i < GameDatabase.Characters.Count; i++)
            {
                var c = GameDatabase.Characters[i];
                int idx = i;
                float x = -220f + i * 220f;
                var btn = UIFactory.CreateButton(_charGrid, "", () => SelectCharacter(idx),
                    new Vector2(x, 0f), new Vector2(190f, 150f), 18);
                var portrait = UIFactory.CreateImage(btn.transform, Color.white,
                    new Vector2(0f, 25f), new Vector2(96f, 96f));
                portrait.sprite = AssetLoader.LoadPlayerSprite(c.id);
                if (portrait.sprite != null) portrait.type = Image.Type.Simple;
                portrait.preserveAspect = true;
                if (portrait.sprite == null) portrait.color = new Color(0.45f, 0.4f, 0.32f);
                UIFactory.CreateText(btn.transform, c.displayName, 20, UIFactory.ButtonTextColor,
                    new Vector2(0f, -56f), new Vector2(175f, 28f));
                _charButtons.Add(btn);
            }

            if (GameDatabase.Characters.Count > 0) SelectCharacter(0);
        }

        private void SelectCharacter(int index)
        {
            if (index < 0 || index >= GameDatabase.Characters.Count) return;
            _selectedIndex = index;
            var c = GameDatabase.Characters[index];
            for (int i = 0; i < _charButtons.Count; i++)
            {
                var colors = _charButtons[i].colors;
                colors.normalColor = i == index ? new Color(1f, 0.82f, 0.54f) : Color.white;
                _charButtons[i].colors = colors;
            }
            if (_descText != null) _descText.text = $"{c.displayName}：{c.description}";
        }

        private void OnStateChanged(GameState state)
        {
            if (_settingsOpen) CloseSettings();
            bool show = state == GameState.MainMenu;
            _panel.gameObject.SetActive(show);
            if (show) UIFactory.FocusFirstButton(_panel);
        }

        public void Show() => _panel.gameObject.SetActive(true);
        public void Hide() => _panel.gameObject.SetActive(false);
    }
}
