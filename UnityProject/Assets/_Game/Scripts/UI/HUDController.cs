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
            // 优先使用预制体（UICanvas.prefab）里的序列化引用；
            // 无预制体、或旧版预制体字段类型变更导致引用丢失（_hpBar/_xpBar 为 null）时回退代码重建
            if (_root == null || _hpBar == null || _xpBar == null)
            {
                if (_root != null) Destroy(_root.gameObject); // 清理旧预制体结构，避免重复
                Build();
            }
            if (_tipText != null) _tipText.gameObject.SetActive(false);
            if (_weaponsText != null) _weaponsText.gameObject.SetActive(false);
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
            _root.anchoredPosition = Vector2.zero;
            _root.sizeDelta = Vector2.zero;

            // 血条（红色 Slider，加粗）：文字作为 Slider 子节点，永远居中在条内
            _hpBar = UIFactory.CreateSlider(_root, new Vector2(-430f, 320f), new Vector2(300f, 26f),
                new Color(0.18f, 0.06f, 0.06f), new Color(0.95f, 0.30f, 0.25f));
            _hpText = UIFactory.CreateText(_hpBar.transform, "HP 100/100", 16, Color.white, Vector2.zero, new Vector2(300f, 26f));
            AddOutline(_hpText);

            // 经验条（绿色 Slider，加高到 16px）：Lv 与经验数值都在条内
            _xpBar = UIFactory.CreateSlider(_root, new Vector2(-430f, 290f), new Vector2(300f, 16f),
                new Color(0.06f, 0.16f, 0.08f), new Color(0.35f, 1.0f, 0.5f));
            _levelText = UIFactory.CreateText(_xpBar.transform, "Lv.1", 12, new Color(0.7f, 1f, 0.8f),
                new Vector2(148f, 0f), new Vector2(80f, 16f), TextAnchor.MiddleRight);
            _xpText = UIFactory.CreateText(_xpBar.transform, "0/10", 13, Color.white, Vector2.zero, new Vector2(300f, 16f));
            AddOutline(_xpText);

            // 材料
            _matText = UIFactory.CreateText(_root, "材料 0", 22, new Color(1f, 0.85f, 0.25f), new Vector2(430f, 320f), new Vector2(260f, 30f));

            // 波次 + 倒计时
            _waveText = UIFactory.CreateText(_root, "第 1 波", 26, Color.white, new Vector2(0f, 318f), new Vector2(300f, 34f));
            _timerText = UIFactory.CreateText(_root, "30.0", 20, Color.white, new Vector2(0f, 286f), new Vector2(200f, 26f));

            // 操作提示
            _tipText = UIFactory.CreateText(_root, "", 13, new Color(0.55f, 0.55f, 0.62f), new Vector2(430f, 293f), new Vector2(220f, 18f), TextAnchor.MiddleRight);

            // 武器栏
            _weaponsText = UIFactory.CreateText(_root, "", 18, Color.white, new Vector2(-560f, -320f), new Vector2(300f, 120f), TextAnchor.LowerLeft);
        }

        /// <summary>给文字加黑色描边，保证在亮色条上清晰可见（不淡）。</summary>
        private static void AddOutline(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void BuildFpsPanel()
        {
            var go = new GameObject("FpsPanel");
            _fpsPanel = go.AddComponent<RectTransform>();
            _fpsPanel.SetParent(transform, false);
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
            _gmButton.SetParent(transform, false);
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
                    _fpsText.text = $"FPS {_fpsFrames / _fpsElapsed:0} | 最慢 {_worstFrameMs:0}ms";
                    _fpsElapsed = 0f;
                    _fpsFrames = 0;
                    _worstFrameMs = 0f;
                }
            }
            var gm = GameManager.Instance;
            if (gm != null && gm.Director != null && gm.State == GameState.Playing)
            {
                _timerText.text = Mathf.CeilToInt(gm.Director.TimeLeft).ToString() + "s";
            }
        }

        private void OnStateChanged(GameState state)
        {
            _currentState = state;
            bool show = state == GameState.Playing || state == GameState.Shop || state == GameState.LevelUp;
            _root.gameObject.SetActive(show);
            if (_gmButton != null) _gmButton.gameObject.SetActive(state == GameState.Playing);
            _fpsPanel.gameObject.SetActive(_fpsVisible && state == GameState.Playing);
            _fpsElapsed = 0f;
            _fpsFrames = 0;
            _worstFrameMs = 0f;
        }

        private void OnPlayerDamaged(float hp, float maxHp)
        {
            _hpBar.value = Mathf.Clamp01(hp / Mathf.Max(1f, maxHp));
            _hpText.text = $"HP {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";
        }

        private void OnMaterialsChanged(int materials)
        {
            _matText.text = "材料 " + materials;
        }

        private void OnLevelChanged(int level, int exp, int expToNext)
        {
            _levelText.text = "Lv." + level;
            _xpBar.value = Mathf.Clamp01(exp / Mathf.Max(1f, expToNext));
            _xpText.text = $"{exp}/{expToNext}";
        }

        private void OnWaveStarted(int wave)
        {
            _waveText.text = $"第 {wave} 波";
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
