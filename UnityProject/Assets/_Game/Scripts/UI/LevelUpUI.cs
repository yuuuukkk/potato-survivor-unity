using System.Collections.Generic;
using RogueLike.Combat;
using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Items;
using RogueLike.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RogueLike.UI
{
    /// <summary>
    /// 升级四选一：从不同的属性提升中选择一项，武器与道具由商店构筑。
    /// 升级队列：面板显示中收到的新升级会排队（同等级去重），选完自动弹下一个——保证任何情况下
    /// 同一时刻只有一个升级界面、同一等级绝不弹两次；真正的连续升级（Lv.4→Lv.5）仍会依次弹出。
    /// </summary>
    public class LevelUpUI : MonoBehaviour
    {
        /// <summary>静态单例：场景残留画布 + 运行时画布并存时，只保留一个实例处理升级（跨实例去重）。</summary>
        public static LevelUpUI Instance { get; private set; }

        [SerializeField] private RectTransform _panel;
        [SerializeField] private Text _titleText;
        [SerializeField] private RectTransform _grid;

        private List<UpgradeOption> _options = new List<UpgradeOption>();
        private int _lastHandledLevel = -1;
        private readonly List<int> _pendingLevels = new List<int>();

        private void Awake()
        {
            // 双实例去重：任一实例存活时，后创建的自我销毁（避免双升级弹窗/双事件处理）
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 优先使用预制体里的面板/标题/网格；无预制体时回退代码创建
            if (_panel == null) Build();
            _panel.anchoredPosition = new Vector2(-170f, 0f);
            _panel.sizeDelta = new Vector2(900f, 500f);
            _grid.anchoredPosition = new Vector2(0f, -25f);
            _titleText.rectTransform.anchoredPosition = new Vector2(0f, 195f);
            _titleText.rectTransform.sizeDelta = new Vector2(820f, 44f);
            EventBus.LevelUp += OnLevelUp;
            EventBus.RunStarted += OnRunStarted;
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EventBus.LevelUp -= OnLevelUp;
            EventBus.RunStarted -= OnRunStarted;
        }

        /// <summary>新一局开始：清除防重标记与队列（等级重新从 1 累计）。</summary>
        private void OnRunStarted(CharacterData character)
        {
            _lastHandledLevel = -1;
            _pendingLevels.Clear();
        }

        private void Build()
        {
            _panel = (RectTransform)UIFactory.CreateImage(transform,
                new Color(0.06f, 0.06f, 0.10f, 0.97f), Vector2.zero, new Vector2(960f, 460f)).rectTransform;

            _titleText = UIFactory.CreateText(_panel, "升级！选择一项强化", 32, Color.white,
                new Vector2(0f, 180f), new Vector2(700f, 44f));

            var gridGo = new GameObject("UpgradeGrid");
            _grid = gridGo.AddComponent<RectTransform>();
            _grid.SetParent(_panel, false);
            _grid.anchoredPosition = new Vector2(0f, 20f);
            _grid.sizeDelta = new Vector2(880f, 320f);
        }

        private void Hide() => _panel.gameObject.SetActive(false);

        private void OnLevelUp(int level)
        {
            // 面板正在显示（上一次升级还没选完）：不重复弹窗，把新升级排队
            if (_panel.gameObject.activeSelf)
            {
                if (!_pendingLevels.Contains(level)) _pendingLevels.Add(level);
                return;
            }

            // 过期/重复等级（已被处理过）：忽略
            if (level <= _lastHandledLevel)
            {
                return;
            }

            ShowLevelUp(level);
        }

        private void ShowLevelUp(int level)
        {
            _lastHandledLevel = level;

            _titleText.text = $"升级！Lv.{level} 选择一项强化";
            _options = UpgradeRoller.Roll(4);

            for (int i = _grid.childCount - 1; i >= 0; i--)
                Destroy(_grid.GetChild(i).gameObject);

            for (int i = 0; i < _options.Count; i++)
                CreateCard(i, _options[i]);

            _panel.gameObject.SetActive(true);
            _panel.SetAsLastSibling();
            UIFactory.FocusFirstButton(_panel);
        }

        private void CreateCard(int index, UpgradeOption opt)
        {
            float x = -330f + index * 220f;
            Color rc = RarityInfo.Color(opt.Rarity);

            // 稀有度色边框（外框 + 内层暗底）
            var frame = (RectTransform)UIFactory.CreateImage(_grid, rc, new Vector2(x, 0f), new Vector2(208f, 300f)).rectTransform;
            var inner = (RectTransform)UIFactory.CreateImage(frame,
                rc * 0.12f + new Color(0.10f, 0.10f, 0.14f), Vector2.zero, new Vector2(198f, 288f)).rectTransform;

            string kindTag = opt.Kind == UpgradeKind.Stat ? "属性提升"
                : opt.Kind == UpgradeKind.Weapon ? "新武器"
                : "新道具";
            UIFactory.CreateText(inner, kindTag, 14, rc, new Vector2(0f, 120f), new Vector2(186f, 22f));
            Color titleColor = Color.white;
            if (opt.Kind == UpgradeKind.Stat && opt.Mod != null)
                titleColor = opt.Mod.flat < 0f || opt.Mod.percent < 0f
                    ? new Color(1f, 0.43f, 0.43f) : new Color(0.46f, 0.95f, 0.54f);
            UIFactory.CreateText(inner, opt.Title, 19, titleColor, new Vector2(0f, 84f), new Vector2(184f, 42f));
            var icon = opt.Kind == UpgradeKind.Weapon && opt.Weapon != null
                ? AssetLoader.LoadWeaponSprite(opt.Weapon.id)
                : opt.Kind == UpgradeKind.Item && opt.Item != null
                    ? AssetLoader.LoadItemSprite(opt.Item.IconId) : null;
            if (icon != null)
            {
                var art = UIFactory.CreateImage(inner, Color.white, new Vector2(0f, 32f), new Vector2(56f, 56f));
                art.sprite = icon;
                art.type = Image.Type.Simple;
                art.preserveAspect = true;
            }
            UIFactory.CreateText(inner, opt.Description, 15, new Color(0.9f, 0.88f, 0.85f),
                icon != null ? new Vector2(0f, -39f) : new Vector2(0f, 17f),
                icon != null ? new Vector2(182f, 62f) : new Vector2(182f, 90f));

            var btn = UIFactory.CreateButton(inner, "选择", () => Apply(opt),
                new Vector2(0f, -110f), new Vector2(150f, 40f), 16);
            btn.GetComponentInChildren<Text>().color = UIFactory.ButtonTextColor;
        }

        private void Apply(UpgradeOption opt)
        {

            var gm = GameManager.Instance;
            if (gm != null && gm.Player != null)
            {
                if (opt.Kind == UpgradeKind.Stat && opt.Mod != null)
                {
                    gm.Player.Stats.AddModifier(opt.Mod);
                    gm.Player.Health.RebuildMaxHp();
                    EventBus.RaisePlayerDamaged(gm.Player.Health.CurrentHp, gm.Player.Health.MaxHp);
                }
                else if (opt.Kind == UpgradeKind.Weapon && opt.Weapon != null)
                {
                    var ws = gm.Player.GetComponent<WeaponSystem>();
                    if (ws != null) ws.AddWeapon(opt.Weapon, 1);
                }
                else if (opt.Kind == UpgradeKind.Item && opt.Item != null)
                {
                    var inv = gm.Player.GetComponent<Inventory>();
                    if (inv != null) inv.AddItem(opt.Item);
                }
            }

            Hide();
            EventBus.RaiseLevelUpChoiceApplied(); // GameManager 恢复 timeScale=1 与 Playing

            // 处理排队中的下一次升级（真连续升级：选完自动弹下一个并重新暂停）
            if (_pendingLevels.Count > 0)
            {
                _pendingLevels.Sort();
                int next = _pendingLevels[0];
                _pendingLevels.RemoveAt(0);
                if (next > _lastHandledLevel)
                {
                    ShowLevelUp(next);
                    Time.timeScale = 0f;
                    if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.LevelUp);
                }
            }
        }
    }
}
