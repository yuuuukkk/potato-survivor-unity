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
    /// 波次结束后的四选一强化卡；波内获得的等级会在结算阶段逐级发放。
    /// </summary>
    public class LevelUpUI : MonoBehaviour
    {
        /// <summary>静态单例：场景残留画布 + 运行时画布并存时，只保留一个实例处理升级（跨实例去重）。</summary>
        public static LevelUpUI Instance { get; private set; }

        [SerializeField] private RectTransform _panel;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _subtitleText;
        [SerializeField] private RectTransform _grid;

        private List<UpgradeOption> _options = new List<UpgradeOption>();

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
            if (_subtitleText != null) _subtitleText.gameObject.SetActive(false);
            EventBus.WaveEndUpgradeReady += OnWaveEndUpgradeReady;
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EventBus.WaveEndUpgradeReady -= OnWaveEndUpgradeReady;
        }

        private void Build()
        {
            _panel = (RectTransform)UIFactory.CreateImage(transform,
                new Color(0.06f, 0.06f, 0.10f, 0.97f), new Vector2(-150f, 0f), new Vector2(920f, 560f)).rectTransform;

            _titleText = UIFactory.CreateText(_panel, "升级！选择一项强化", 32, Color.white,
                new Vector2(0f, 190f), new Vector2(800f, 44f));
            var gridGo = new GameObject("UpgradeGrid");
            _grid = gridGo.AddComponent<RectTransform>();
            _grid.SetParent(_panel, false);
            _grid.anchoredPosition = new Vector2(0f, -18f);
            _grid.sizeDelta = new Vector2(880f, 320f);
        }

        private void Hide() => _panel.gameObject.SetActive(false);

        private void OnWaveEndUpgradeReady(int level)
        {
            ShowLevelUp(level);
        }

        private void ShowLevelUp(int level)
        {
            int wave = GameManager.Instance != null ? GameManager.Instance.Wave : 1;
            _titleText.text = $"第 {wave} 波结束  ·  选择等级 {level} 强化";
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
            float x = -285f + index * 190f;
            Color rc = RarityInfo.Color(opt.Rarity);
            var cardImage = UIFactory.CreateCard(_grid, Color.white,
                new Vector2(x, 0f), new Vector2(190f, 300f));
            // The frame remains the same art for every tier; only the card face carries rarity.
            cardImage.color = Color.white;
            var card = cardImage.rectTransform;
            var rarityFill = UIFactory.CreateImage(card,
                new Color(rc.r, rc.g, rc.b, 1f),
                Vector2.zero, new Vector2(162f, 258f));
            rarityFill.name = "RarityFill";
            rarityFill.sprite = null;
            rarityFill.type = Image.Type.Simple;
            rarityFill.raycastTarget = false;
            var accent = UIFactory.CreateImage(card, rc, new Vector2(0f, 143f), new Vector2(172f, 5f));
            accent.sprite = null;
            accent.type = Image.Type.Simple;

            string kindTag = RarityName(opt.Rarity);
            if (opt.Kind == UpgradeKind.Weapon) kindTag += " · 武器";
            else if (opt.Kind == UpgradeKind.Item) kindTag += " · 道具";
            bool lightFace = opt.Rarity == Rarity.Common || opt.Rarity == Rarity.Uncommon || opt.Rarity == Rarity.Legendary;
            Color copyColor = lightFace ? new Color(0.13f, 0.10f, 0.10f) : Color.white;
            UIFactory.CreateText(card, $"{index + 1:00}   {kindTag}", 16, copyColor,
                new Vector2(0f, 116f), new Vector2(180f, 28f));
            Color titleColor = copyColor;
            if (opt.Kind == UpgradeKind.Stat && opt.Mod != null)
                titleColor = opt.Mod.flat < 0f || opt.Mod.percent < 0f
                    ? (lightFace ? new Color(0.62f, 0.07f, 0.07f) : new Color(1f, 0.83f, 0.83f))
                    : (lightFace ? new Color(0.06f, 0.30f, 0.09f) : new Color(0.79f, 1f, 0.80f));
            if (opt.Kind == UpgradeKind.Stat)
            {
                string title = opt.Title ?? "";
                int split = title.LastIndexOf(' ');
                string name = split > 0 ? title.Substring(0, split) : title;
                string amount = split > 0 ? title.Substring(split + 1) : "提升";
                UIFactory.CreateText(card, name, 19, copyColor,
                    new Vector2(0f, 60f), new Vector2(184f, 32f));
                UIFactory.CreateText(card, amount, 30, titleColor,
                    new Vector2(0f, -2f), new Vector2(184f, 56f));
            }
            else
            {
                UIFactory.CreateText(card, opt.Title, 21, titleColor,
                    new Vector2(0f, 75f), new Vector2(184f, 38f));
            }
            var icon = opt.Kind == UpgradeKind.Weapon && opt.Weapon != null
                ? AssetLoader.LoadWeaponSprite(opt.Weapon.id)
                : opt.Kind == UpgradeKind.Item && opt.Item != null
                    ? AssetLoader.LoadItemSprite(opt.Item.IconId) : null;
            if (icon != null)
            {
                var art = UIFactory.CreateImage(card, Color.white, new Vector2(0f, 12f), new Vector2(74f, 74f));
                art.sprite = icon;
                art.type = Image.Type.Simple;
                art.preserveAspect = true;
            }
            if (opt.Kind != UpgradeKind.Stat)
            {
                var detail = UIFactory.CreateText(card, opt.Description, 15,
                    copyColor, new Vector2(0f, -62f), new Vector2(180f, 58f));
                detail.verticalOverflow = VerticalWrapMode.Truncate;
            }

            var btn = UIFactory.CreateButton(card, "选择", () => Apply(opt),
                new Vector2(0f, -115f), new Vector2(158f, 42f), 18);
            btn.GetComponentInChildren<Text>().color = UIFactory.ButtonTextColor;
        }

        private static string RarityName(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return "优秀";
                case Rarity.Rare: return "稀有";
                case Rarity.Epic: return "史诗";
                case Rarity.Legendary: return "传说";
                case Rarity.Red: return "红色";
                default: return "普通";
            }
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
            EventBus.RaiseLevelUpChoiceApplied();
        }
    }
}
