using RogueLike.Core;
using UnityEngine;

namespace RogueLike.UI
{
    /// <summary>结算界面：波次/击杀/材料统计 + 重开/回主菜单。</summary>
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private UnityEngine.UI.Text _statsText;
        [SerializeField] private UnityEngine.UI.Button _restartBtn;
        [SerializeField] private UnityEngine.UI.Button _menuBtn;

        private void Awake()
        {
            // 优先使用预制体里的面板/按钮；无预制体时回退代码创建
            if (_panel == null) Build();
            HookButtons();
            EventBus.StateChanged += OnStateChanged;
            EventBus.GameOverEvent += OnGameOver;
            Hide();
        }

        private void HookButtons()
        {
            if (_restartBtn != null)
                _restartBtn.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.Restart(); });
            if (_menuBtn != null)
                _menuBtn.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.BackToMenu(); });
        }

        private void OnDestroy()
        {
            EventBus.StateChanged -= OnStateChanged;
            EventBus.GameOverEvent -= OnGameOver;
        }

        private void Build()
        {
            _panel = (RectTransform)UIFactory.CreateImage(transform,
                new Color(0.05f, 0.04f, 0.08f, 0.97f), Vector2.zero, new Vector2(1120f, 640f)).rectTransform;

            UIFactory.CreateText(_panel, "游戏结束", 52, new Color(1f, 0.45f, 0.4f), new Vector2(0f, 220f), new Vector2(600f, 70f));
            _statsText = UIFactory.CreateText(_panel, "", 26, Color.white, new Vector2(0f, 90f), new Vector2(700f, 160f));

            UIFactory.CreateButton(_panel, "重新开始", () =>
            {
                if (GameManager.Instance != null) GameManager.Instance.Restart();
            }, new Vector2(-150f, -140f), new Vector2(240f, 56f), 24);

            UIFactory.CreateButton(_panel, "回主菜单", () =>
            {
                if (GameManager.Instance != null) GameManager.Instance.BackToMenu();
            }, new Vector2(150f, -140f), new Vector2(240f, 56f), 24);
        }

        private void OnStateChanged(GameState state)
        {
            bool show = state == GameState.GameOver;
            _panel.gameObject.SetActive(show);
            if (show) UIFactory.FocusFirstButton(_panel);
        }

        private void OnGameOver(int wave, int kills)
        {
            var gm = GameManager.Instance;
            int materials = gm != null ? gm.Materials : 0;
            string result = gm != null && gm.RunWon ? "20 波通关！" : "本局结束";
            _statsText.text = $"{result}\n到达波次：第 {wave} 波\n击杀敌人：{kills}\n剩余材料：{materials}";
        }

        private void Hide() => _panel.gameObject.SetActive(false);
    }
}
