using RogueLike.Combat;
using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueLike.UI
{
    /// <summary>商店界面：三项混合报价、刷新、锁定、出售已持有武器。</summary>
    public class ShopUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private RectTransform _grid;
        [SerializeField] private Text _matText;
        [SerializeField] private Text _titleText;
        [SerializeField] private Button _refreshBtn;
        [SerializeField] private Button _nextWaveBtn;
        private Button _challengeBtn;
        private Button _buildBtn;
        private WeaponBuildUI _buildUI;
        private RectTransform _sellGrid;
        private GameObject _sellDialog;
        private Text _sellDialogText;
        private Button _sellCancelBtn;
        private Button _combineBtn;
        private Button _forgeButton;
        private GameObject _challengeDialog;
        private GameObject _challengeOrigin;
        private Text[] _challengeTitles;
        private Text[] _challengeRisks;
        private Text[] _challengeRewards;
        private Text _challengeStatus;
        private Text _feedbackText;
        private WeaponInstance _pendingSale;
        private Button[] _buyButtons;
        private Button[] _lockButtons;
        private GameObject _saleOrigin;
        private readonly System.Collections.Generic.Dictionary<Button, bool> _modalButtonStates =
            new System.Collections.Generic.Dictionary<Button, bool>();

        private ShopSystem Shop => GameManager.Instance != null ? GameManager.Instance.Shop : null;

        private void Awake()
        {
            // 优先使用预制体里的面板/网格/按钮；无预制体时回退代码创建
            if (_panel == null) Build();
            _grid.anchoredPosition = Vector2.zero;
            _grid.sizeDelta = new Vector2(900f, 400f);
            if (_refreshBtn != null)
            {
                var rt = (RectTransform)_refreshBtn.transform;
                rt.anchoredPosition = new Vector2(490f, -270f);
                rt.sizeDelta = new Vector2(125f, 46f);
            }
            if (_nextWaveBtn != null)
            {
                var rt = (RectTransform)_nextWaveBtn.transform;
                rt.anchoredPosition = new Vector2(300f, -270f);
                rt.sizeDelta = new Vector2(180f, 46f);
            }
            _challengeBtn = UIFactory.CreateButton(_panel, "可选挑战", OpenChallengeDialog,
                new Vector2(82f, -270f), new Vector2(160f, 46f), 17);
            _buildUI = GetComponent<WeaponBuildUI>();
            if (_buildUI == null) _buildUI = gameObject.AddComponent<WeaponBuildUI>();
            _buildBtn = UIFactory.CreateButton(_panel, "武器构筑", () => _buildUI.Open(),
                new Vector2(-278f, -270f), new Vector2(160f, 46f), 17);
            BuildSellGrid();
            BuildSellDialog();
            BuildChallengeDialog();
            _feedbackText = UIFactory.CreateText(_panel, "", 14,
                new Color(0.93f, 0.86f, 0.70f), new Vector2(-250f, -306f),
                new Vector2(500f, 23f), TextAnchor.MiddleLeft);
            HookButtons();
            EventBus.StateChanged += OnStateChanged;
            EventBus.MaterialsChanged += OnMaterialsChanged;
            Hide();
        }

        private void HookButtons()
        {
            if (_refreshBtn != null)
                _refreshBtn.onClick.AddListener(() => { if (Shop != null && Shop.Refresh()) Refresh(); });
            if (_nextWaveBtn != null)
                _nextWaveBtn.onClick.AddListener(() => GameManager.Instance?.CloseShop());
        }

        private void Hide()
        {
            if (_sellDialog != null) _sellDialog.SetActive(false);
            if (_challengeDialog != null) _challengeDialog.SetActive(false);
            RestoreModalButtons();
            _saleOrigin = null;
            _challengeOrigin = null;
            _pendingSale = null;
            _panel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            EventBus.StateChanged -= OnStateChanged;
            EventBus.MaterialsChanged -= OnMaterialsChanged;
        }

        private void Build()
        {
            _panel = (RectTransform)UIFactory.CreateImage(transform,
                new Color(0.07f, 0.07f, 0.11f, 0.97f), Vector2.zero, new Vector2(1120f, 640f)).rectTransform;

            _titleText = UIFactory.CreateText(_panel, "商店", 34, Color.white, new Vector2(0f, 268f), new Vector2(300f, 44f));
            _matText = UIFactory.CreateText(_panel, "材料 0", 24, new Color(1f, 0.85f, 0.25f), new Vector2(0f, 226f), new Vector2(300f, 32f));

            var gridGo = new GameObject("SlotGrid");
            _grid = gridGo.AddComponent<RectTransform>();
            _grid.SetParent(_panel, false);
            _grid.anchoredPosition = new Vector2(0f, 60f);
            _grid.sizeDelta = new Vector2(840f, 300f);

            _refreshBtn = UIFactory.CreateButton(_panel, "刷新", null,
                new Vector2(490f, -270f), new Vector2(125f, 46f));
            _nextWaveBtn = UIFactory.CreateButton(_panel, "下一波", null,
                new Vector2(300f, -270f), new Vector2(180f, 46f));
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Shop)
            {
                if (_feedbackText != null) _feedbackText.text = "选择商品，或锁定留到下一波";
                Refresh();
                _panel.gameObject.SetActive(true);
                UIFactory.FocusFirstButton(_panel);
            }
            else
            {
                Hide();
            }
        }

        private void OnMaterialsChanged(int materials)
        {
            if (_panel.gameObject.activeSelf) Refresh();
        }

        private void BuildSellGrid()
        {
            var go = new GameObject("OwnedWeaponsForSale");
            _sellGrid = go.AddComponent<RectTransform>();
            _sellGrid.SetParent(_panel, false);
            _sellGrid.anchoredPosition = new Vector2(-220f, -202f);
            _sellGrid.sizeDelta = new Vector2(640f, 78f);
        }

        private void BuildSellDialog()
        {
            var shade = UIFactory.CreateImage(_panel, new Color(0f, 0f, 0f, 0.72f),
                Vector2.zero, new Vector2(1120f, 640f));
            shade.sprite = null;
            shade.type = Image.Type.Simple;
            shade.color = new Color(0f, 0f, 0f, 0.72f);
            shade.raycastTarget = true;
            _sellDialog = shade.gameObject;
            var cardImage = UIFactory.CreateImage(shade.transform,
                new Color(0.19f, 0.17f, 0.22f), Vector2.zero, new Vector2(470f, 235f));
            cardImage.color = new Color(0.19f, 0.17f, 0.22f);
            var card = (RectTransform)cardImage.rectTransform;
            UIFactory.CreateText(card, "武器操作", 26, Color.white,
                new Vector2(0f, 78f), new Vector2(340f, 38f));
            _sellDialogText = UIFactory.CreateText(card, "", 18, new Color(0.92f, 0.87f, 0.76f),
                new Vector2(0f, 20f), new Vector2(330f, 68f));
            UIFactory.CreateButton(card, "确认出售", ConfirmSale,
                new Vector2(-150f, -76f), new Vector2(130f, 42f), 17);
            _combineBtn = UIFactory.CreateButton(card, "合并", ConfirmCombine,
                new Vector2(0f, -76f), new Vector2(130f, 42f), 17);
            _sellCancelBtn = UIFactory.CreateButton(card, "取消", CloseSellDialog,
                new Vector2(150f, -76f), new Vector2(130f, 42f), 17);
            _sellDialog.SetActive(false);
        }

        private void BuildChallengeDialog()
        {
            var shade = UIFactory.CreateImage(_panel, new Color(0f, 0f, 0f, 0.76f),
                Vector2.zero, new Vector2(1120f, 640f));
            shade.sprite = null;
            shade.type = Image.Type.Simple;
            shade.color = new Color(0f, 0f, 0f, 0.76f);
            shade.raycastTarget = true;
            _challengeDialog = shade.gameObject;
            var card = (RectTransform)UIFactory.CreateImage(shade.transform,
                new Color(0.22f, 0.20f, 0.25f), Vector2.zero, new Vector2(920f, 550f)).rectTransform;
            UIFactory.CreateText(card, "挑战契约 · 四档风险递增，任选一档", 25, Color.white,
                new Vector2(0f, 235f), new Vector2(800f, 46f));
            _challengeStatus = UIFactory.CreateText(card, "", 15, new Color(0.93f, 0.84f, 0.68f),
                new Vector2(0f, 196f), new Vector2(820f, 32f));
            _challengeTitles = new Text[4];
            _challengeRisks = new Text[4];
            _challengeRewards = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var box = (RectTransform)UIFactory.CreateImage(card, new Color(0.38f, 0.31f, 0.34f),
                    new Vector2(i % 2 == 0 ? -218f : 218f, i < 2 ? 94f : -99f),
                    new Vector2(420f, 180f)).rectTransform;
                _challengeTitles[i] = UIFactory.CreateText(box, "", 20, Color.white,
                    new Vector2(0f, 65f), new Vector2(390f, 30f));
                _challengeTitles[i].supportRichText = false;
                _challengeRisks[i] = UIFactory.CreateText(box, "", 16, new Color(0.96f, 0.81f, 0.72f),
                    new Vector2(0f, 19f), new Vector2(385f, 64f));
                _challengeRisks[i].verticalOverflow = VerticalWrapMode.Truncate;
                _challengeRewards[i] = UIFactory.CreateText(box, "", 17, new Color(0.73f, 0.95f, 0.68f),
                    new Vector2(0f, -29f), new Vector2(380f, 26f));
                UIFactory.CreateButton(box, "接受挑战", () =>
                {
                    if (Shop == null || !Shop.ConfirmChallenge(index))
                        _challengeStatus.text = "挑战已失效，请返回商店。";
                }, new Vector2(0f, -65f), new Vector2(235f, 40f), 16);
            }
            _forgeButton = UIFactory.CreateButton(card, "AI 改写挑战", RequestDirectorChallenges,
                new Vector2(-125f, -238f), new Vector2(195f, 44f), 17);
            UIFactory.CreateButton(card, "返回商店", CloseChallengeDialog,
                new Vector2(125f, -238f), new Vector2(195f, 44f), 17);
            _challengeDialog.SetActive(false);
        }

        private void OpenChallengeDialog()
        {
            if (Shop == null || GameManager.Instance == null) return;
            if (Shop.ChallengeOffers.Count == 0)
            {
                return;
            }
            _challengeOrigin = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _challengeDialog.SetActive(true);
            _challengeDialog.transform.SetAsLastSibling();
            _modalButtonStates.Clear();
            foreach (var button in _panel.GetComponentsInChildren<Button>(true))
            {
                if (button.transform.IsChildOf(_challengeDialog.transform)) continue;
                _modalButtonStates[button] = button.interactable;
                button.interactable = false;
            }
            _challengeStatus.text = $"上波击杀 {GameManager.Instance.LastWaveKills} · 剩余生命 {Mathf.RoundToInt(GameManager.Instance.LastWaveHpPercent * 100f)}%";
            RefreshChallengeDialog();
            if (EventSystem.current != null)
            {
                var first = _challengeDialog.GetComponentInChildren<Button>(true);
                if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }

        private void RefreshChallengeDialog()
        {
            for (int i = 0; i < 4; i++)
            {
                var offer = Shop != null && i < Shop.ChallengeOffers.Count ? Shop.ChallengeOffers[i] : null;
                _challengeTitles[i].text = offer != null ? offer.Tier.displayName + " · " + offer.Title : "暂无挑战";
                _challengeRisks[i].text = offer != null ? offer.Risk : "返回商店后可直接进入下一波";
                _challengeRewards[i].text = offer != null ? $"胜利奖励 +{offer.Reward} 材料" : "";
            }
            var configured = IsAIChallengeConfigured();
            _forgeButton.interactable = configured && Shop != null && !Shop.DirectorAIUsed;
            _forgeButton.GetComponentInChildren<Text>().text = configured ? "AI 改写挑战" : "仅本地挑战";
        }

        private void RequestDirectorChallenges()
        {
            if (Shop == null) return;
            _forgeButton.interactable = false;
            _challengeStatus.text = "正在生成挑战；本地选项仍然有效……";
            Shop.RequestDirectorChallenges((success, message) =>
            {
                if (_challengeDialog == null || !_challengeDialog.activeSelf) return;
                _challengeStatus.text = message;
                RefreshChallengeDialog();
            });
        }

        private void CloseChallengeDialog()
        {
            _challengeDialog.SetActive(false);
            RestoreModalButtons();
            if (_challengeOrigin != null && _challengeOrigin.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_challengeOrigin);
            else UIFactory.FocusFirstButton(_panel);
            _challengeOrigin = null;
        }

        private static bool IsAIChallengeConfigured()
        {
            var settings = GameConfig.Instance != null ? GameConfig.Instance.Settings :
                Resources.Load<GameSettingsSO>("Config/GameSettings");
            return settings != null && !string.IsNullOrWhiteSpace(settings.weaponForgeApiKey);
        }

        private void ShowSellDialog(WeaponInstance weapon)
        {
            _saleOrigin = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _pendingSale = weapon;
            float rate = GameDatabase.Balance != null ? GameDatabase.Balance.sellReturnRate : 0.5f;
            int value = Mathf.Max(0, Mathf.FloorToInt(weapon.PaidValue * Mathf.Clamp01(rate)));
            _sellDialogText.text = $"{weapon.Data.displayName}  {weapon.Level} 级\n出售后获得 {value} 材料";
            var ws = GameManager.Instance != null && GameManager.Instance.Player != null
                ? GameManager.Instance.Player.GetComponent<WeaponSystem>() : null;
            bool canCombine = false;
            int maxTier = GameDatabase.WaveConfig != null ? Mathf.Clamp(GameDatabase.WaveConfig.weaponMaxLevel, 1, 4) : 4;
            if (ws != null && weapon.Level < maxTier)
                for (int i = 0; i < ws.Weapons.Count; i++)
                    if (!ReferenceEquals(ws.Weapons[i], weapon) && ws.Weapons[i].Data.id == weapon.Data.id &&
                        ws.Weapons[i].Level == weapon.Level)
                    {
                        canCombine = true;
                        if (ws.Weapons[i].ModificationIds.Count > 0 && weapon.ModificationIds.Count > 0)
                            _sellDialogText.text += "\n合并保留所选武器的装备构筑";
                        break;
                    }
            _combineBtn.gameObject.SetActive(canCombine);
            _sellDialog.SetActive(true);
            _sellDialog.transform.SetAsLastSibling();
            _modalButtonStates.Clear();
            foreach (var button in _panel.GetComponentsInChildren<Button>(true))
            {
                if (button.transform.IsChildOf(_sellDialog.transform)) continue;
                _modalButtonStates[button] = button.interactable;
                button.interactable = false;
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_sellCancelBtn.gameObject);
        }

        private void Update()
        {
            if (_sellDialog != null && _sellDialog.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                CloseSellDialog();
            else if (_challengeDialog != null && _challengeDialog.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                CloseChallengeDialog();
        }

        private void CloseSellDialog()
        {
            _pendingSale = null;
            _sellDialog.SetActive(false);
            RestoreModalButtons();
            if (_saleOrigin != null && _saleOrigin.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_saleOrigin);
            else UIFactory.FocusFirstButton(_panel);
            _saleOrigin = null;
        }

        private void RestoreModalButtons()
        {
            foreach (var pair in _modalButtonStates)
                if (pair.Key != null) pair.Key.interactable = pair.Value;
            _modalButtonStates.Clear();
        }

        private void ConfirmSale()
        {
            var gm = GameManager.Instance;
            var ws = gm != null && gm.Player != null ? gm.Player.GetComponent<WeaponSystem>() : null;
            if (ws != null && _pendingSale != null)
                for (int i = 0; i < ws.Weapons.Count; i++)
                    if (ReferenceEquals(ws.Weapons[i], _pendingSale))
                    {
                        ws.SellWeapon(i, out _);
                        break;
                    }
            _pendingSale = null;
            _sellDialog.SetActive(false);
            RestoreModalButtons();
            _saleOrigin = null;
            if (_feedbackText != null) _feedbackText.text = "武器已出售";
            Refresh();
        }

        private void ConfirmCombine()
        {
            var gm = GameManager.Instance;
            var ws = gm != null && gm.Player != null ? gm.Player.GetComponent<WeaponSystem>() : null;
            bool combined = false;
            if (ws != null && _pendingSale != null)
                for (int i = 0; i < ws.Weapons.Count; i++)
                    if (ReferenceEquals(ws.Weapons[i], _pendingSale)) { combined = ws.CombineWeapon(i); break; }
            _pendingSale = null;
            _sellDialog.SetActive(false);
            RestoreModalButtons();
            _saleOrigin = null;
            if (_feedbackText != null) _feedbackText.text = combined ? "同级武器已合并升级" : "无法合并这件武器";
            Refresh();
        }

        private void Refresh()
        {
            var shop = Shop;
            if (shop == null) return;
            int focusedSlot = -1;
            bool focusLock = false;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && _buyButtons != null)
                for (int i = 0; i < _buyButtons.Length; i++)
                {
                    if (_buyButtons[i] != null && selected == _buyButtons[i].gameObject) focusedSlot = i;
                    if (_lockButtons[i] != null && selected == _lockButtons[i].gameObject)
                    {
                        focusedSlot = i;
                        focusLock = true;
                    }
                }
            _matText.text = "材料 " + GameManager.Instance.Materials;
            _titleText.text = $"商店 — 第 {shop.Wave} 波";
            if (_refreshBtn != null)
            {
                var label = _refreshBtn.GetComponentInChildren<Text>();
                if (label != null) label.text = shop.RefreshCost == 0 ? "免费刷新" : "刷新 " + shop.RefreshCost;
                _refreshBtn.interactable = GameManager.Instance.Materials >= shop.RefreshCost;
            }
            if (_challengeBtn != null)
                _challengeBtn.gameObject.SetActive(shop.ChallengeOffers.Count == 4);

            // 清空旧槽
            for (int i = _grid.childCount - 1; i >= 0; i--)
            {
                _grid.GetChild(i).gameObject.SetActive(false);
                Destroy(_grid.GetChild(i).gameObject);
            }

            int n = shop.Slots.Count;
            _buyButtons = new Button[n];
            _lockButtons = new Button[n];
            for (int i = 0; i < n; i++)
                CreateSlot(i, shop.Slots[i]);
            RefreshSellGrid();
            if (focusedSlot >= 0 && focusedSlot < n && EventSystem.current != null)
            {
                var target = focusLock ? _lockButtons[focusedSlot] : _buyButtons[focusedSlot];
                if (target != null && target.interactable) EventSystem.current.SetSelectedGameObject(target.gameObject);
                else if (_lockButtons[focusedSlot] != null)
                    EventSystem.current.SetSelectedGameObject(_lockButtons[focusedSlot].gameObject);
            }
        }

        private void RefreshSellGrid()
        {
            for (int i = _sellGrid.childCount - 1; i >= 0; i--)
            {
                _sellGrid.GetChild(i).gameObject.SetActive(false);
                Destroy(_sellGrid.GetChild(i).gameObject);
            }
            var gm = GameManager.Instance;
            var ws = gm != null && gm.Player != null ? gm.Player.GetComponent<RogueLike.Combat.WeaponSystem>() : null;
            if (ws == null) return;
            UIFactory.CreateText(_sellGrid, "持有武器（出售）", 15, Color.white,
                new Vector2(-225f, 34f), new Vector2(190f, 24f));
            for (int i = 0; i < ws.Weapons.Count; i++)
            {
                var weapon = ws.Weapons[i];
                var btn = UIFactory.CreateButton(_sellGrid, "", () => ShowSellDialog(weapon),
                    new Vector2(-255f + i * 78f, -7f), new Vector2(68f, 62f), 12);
                var tint = RarityInfo.Color((Rarity)Mathf.Clamp(weapon.Level - 1, 0, 4));
                btn.GetComponent<Image>().color = tint;
                var art = UIFactory.CreateImage(btn.transform, Color.white,
                    new Vector2(0f, 5f), new Vector2(46f, 40f));
                art.sprite = AssetLoader.LoadWeaponSprite(weapon.Data.id);
                if (art.sprite != null) art.type = Image.Type.Simple;
                art.preserveAspect = true;
                art.raycastTarget = false;
                var levelText = UIFactory.CreateText(btn.transform, $"Lv.{weapon.Level}", 11, Color.white,
                    new Vector2(0f, -22f), new Vector2(58f, 16f));
                levelText.raycastTarget = false;
            }
        }

        private void CreateSlot(int index, ShopSystem.ShopSlot slot)
        {
            int col = index % 3;
            int row = index / 3;
            Vector2 pos = new Vector2(-310f + col * 310f, 25f - row * 375f);

            // 槽背景：稀有度色边框（外框）+ 暗色内底
            var frame = (RectTransform)UIFactory.CreateImage(_grid,
                RarityInfo.Color(slot.Rarity), pos, new Vector2(260f, 360f)).rectTransform;
            var bg = (RectTransform)UIFactory.CreateImage(frame,
                RarityInfo.Color(slot.Rarity) * 0.15f + new Color(0.12f, 0.12f, 0.16f),
                Vector2.zero, new Vector2(252f, 352f)).rectTransform;

            var ws = GameManager.Instance.Player != null ? GameManager.Instance.Player.GetComponent<RogueLike.Combat.WeaponSystem>() : null;
            int ownedBuildLevel = slot.IsModification && ws != null ? ws.ModificationLevel(slot.Modification.id) : 0;
            string name = slot.IsWeapon ? slot.Weapon.displayName :
                slot.IsModification ? slot.Modification.displayName : slot.Item.displayName;
            string sub;
            if (slot.IsWeapon) sub = $"{slot.WeaponLevel} 级 · {slot.Weapon.description}";
            else if (slot.IsModification) sub = ModificationPreview(slot.Modification, ownedBuildLevel + 1) +
                "\n" + slot.Modification.description;
            else if (slot.IsHeal) sub = $"立即回复 {slot.Item.healAmount} HP";
            else sub = slot.Item.description;

            UIFactory.CreateText(bg, name, 21, RarityInfo.Color(slot.Rarity), new Vector2(0f, 143f), new Vector2(230f, 30f));
            string targetName = slot.IsModification
                ? slot.Modification.IsUniversal
                    ? slot.Modification.kind == WeaponModificationKind.EnlargedProjectile
                        ? "投射物枪械" : "非爆炸子弹枪械"
                    :
                    GameDatabase.GetWeapon(slot.Modification.weaponId)?.displayName : null;
            UIFactory.CreateText(bg, slot.IsWeapon ? $"{slot.WeaponLevel} 级武器" :
                slot.IsModification ? slot.Modification.IsUniversal
                    ? $"通用弹药 · Lv.{ownedBuildLevel + 1}/{slot.Modification.MaxLevel}"
                    : (targetName ?? slot.Modification.weaponId) + $" · Lv.{ownedBuildLevel + 1}/{slot.Modification.MaxLevel}"
                    : "道具", 13,
                new Color(0.75f, 0.73f, 0.72f), new Vector2(0f, 113f), new Vector2(224f, 20f));
            if (slot.IsModification && slot.Modification.IsUniversal)
            {
                var emblem = (RectTransform)UIFactory.CreateImage(bg, RarityInfo.Color(slot.Rarity),
                    new Vector2(0f, 32f), new Vector2(82f, 82f)).rectTransform;
                UIFactory.CreateText(emblem, "弹", 43, Color.white,
                    Vector2.zero, new Vector2(72f, 66f));
            }
            else if (slot.IsWeapon || slot.IsModification)
            {
                var art = UIFactory.CreateImage(bg, Color.white, new Vector2(0f, 32f), new Vector2(105f, 105f));
                art.sprite = AssetLoader.LoadWeaponSprite(slot.IsWeapon ? slot.Weapon.id : slot.Modification.weaponId);
                if (art.sprite != null) art.type = Image.Type.Simple;
                art.preserveAspect = true;
            }
            else
            {
                var sprite = AssetLoader.LoadItemSprite(slot.Item.IconId);
                if (sprite != null)
                {
                    var art = UIFactory.CreateImage(bg, Color.white, new Vector2(0f, 32f), new Vector2(105f, 105f));
                    art.sprite = sprite;
                    art.type = Image.Type.Simple;
                    art.preserveAspect = true;
                }
                else
                {
                    var emblem = (RectTransform)UIFactory.CreateImage(bg, RarityInfo.Color(slot.Rarity),
                        new Vector2(0f, 32f), new Vector2(88f, 88f)).rectTransform;
                    UIFactory.CreateText(emblem, slot.IsHeal ? "+" : string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1), 43, Color.white,
                        Vector2.zero, new Vector2(76f, 68f));
                }
            }
            var description = UIFactory.CreateText(bg, sub, 14, new Color(0.9f, 0.88f, 0.85f),
                new Vector2(0f, -65f), new Vector2(224f, 82f));
            description.verticalOverflow = VerticalWrapMode.Truncate;

            var buyBtn = UIFactory.CreateButton(bg, "购买 · " + slot.Price,
                () =>
                {
                    bool bought = Shop != null && Shop.Buy(index);
                    if (_feedbackText != null) _feedbackText.text = bought
                        ? slot.IsModification ? "构筑已解锁/升级：打开「武器构筑」逐把装备" : "购买成功"
                        : "无法购买，请检查材料或容量";
                    Refresh();
                },
                new Vector2(-33f, -140f), new Vector2(140f, 38f), 17);
            bool affordable = GameManager.Instance.Materials >= slot.Price;
            var remaining = UIFactory.CreateText(bg,
                affordable ? "购买后剩余 " + (GameManager.Instance.Materials - slot.Price) + " 材料" :
                    "还差 " + (slot.Price - GameManager.Instance.Materials) + " 材料",
                13, affordable ? new Color(0.76f, 0.92f, 0.72f) : UIFactory.UnaffordableColor,
                new Vector2(0f, -107f), new Vector2(222f, 22f));
            remaining.verticalOverflow = VerticalWrapMode.Truncate;
            bool usable = slot.IsWeapon
                ? ws != null && ws.CanAcquire(slot.Weapon, slot.WeaponLevel)
                : slot.IsModification ? ws != null && ws.CanApplyModification(slot.Modification)
                : slot.Item == null || !slot.Item.requiresProjectileWeapon ||
                    ws != null && ws.HasStandardProjectileWeapon;
            var inventory = GameManager.Instance.Player != null ? GameManager.Instance.Player.GetComponent<Inventory>() : null;
            bool itemFull = !slot.IsWeapon && !slot.IsHeal && !slot.IsModification && inventory != null &&
                inventory.GetCount(slot.Item.id) >= slot.Item.maxStack;
            buyBtn.interactable = affordable && usable && !itemFull;
            _buyButtons[index] = buyBtn;
            var buyLabel = buyBtn.GetComponentInChildren<Text>();
            if (!affordable) buyLabel.text = "差 " + (slot.Price - GameManager.Instance.Materials) + " 材料";
            else if (slot.IsModification && !usable) buyLabel.text = "已满级/不可用";
            else if (!usable) buyLabel.text = "当前无法使用";
            else if (itemFull) buyLabel.text = "携带已满";
            buyLabel.color = buyBtn.interactable ? UIFactory.ButtonTextColor : UIFactory.UnaffordableColor;

            var lockBtn = UIFactory.CreateButton(bg, slot.Locked ? "解锁" : "锁定",
                () => { if (Shop != null) Shop.ToggleLock(index); Refresh(); },
                new Vector2(87f, -140f), new Vector2(65f, 38f), 14);
            _lockButtons[index] = lockBtn;
            lockBtn.GetComponentInChildren<Text>().color = slot.Locked ? new Color(0.65f, 0.38f, 0.05f) : UIFactory.ButtonTextColor;
        }

        private static string ModificationPreview(WeaponModificationData mod, int level)
        {
            int upgrades = Mathf.Max(0, level - 1);
            switch (mod.kind)
            {
                case WeaponModificationKind.Ricochet:
                    return "本级：反弹 " + (mod.extraBounces + Mathf.RoundToInt(upgrades * mod.extraBouncesPerLevel)) + " 次";
                case WeaponModificationKind.CharmProjectile:
                    return "本级：魅惑 " + Mathf.RoundToInt(100f * Mathf.Clamp01(
                        mod.charmChance + upgrades * mod.charmChancePerLevel)) + "% / " +
                        (mod.charmDuration + upgrades * mod.charmDurationPerLevel).ToString("0.0") + " 秒";
                case WeaponModificationKind.EnlargedProjectile:
                    return "本级：弹体 ×" + (mod.projectileSizeMultiplier +
                        upgrades * mod.projectileSizePerLevel).ToString("0.00");
                default:
                    return "购买后 Lv." + level + "/" + mod.MaxLevel;
            }
        }
    }
}
