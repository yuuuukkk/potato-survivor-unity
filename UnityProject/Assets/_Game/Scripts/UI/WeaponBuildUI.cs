using System.Collections.Generic;
using RogueLike.Combat;
using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueLike.UI
{
    /// <summary>商店中的独立武器构筑页：购买解锁型号，装备选择属于单个武器槽位。</summary>
    public class WeaponBuildUI : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _weaponList;
        private RectTransform _modList;
        private Text _detailTitle;
        private Text _status;
        private Button _closeButton;
        private GameObject _returnFocus;
        private int _selectedWeapon;

        private WeaponSystem System => GameManager.Instance != null && GameManager.Instance.Player != null
            ? GameManager.Instance.Player.GetComponent<WeaponSystem>() : null;

        private void Awake()
        {
            Build();
            EventBus.WeaponsChanged += OnWeaponsChanged;
            EventBus.StateChanged += OnStateChanged;
            _root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            EventBus.WeaponsChanged -= OnWeaponsChanged;
            EventBus.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState state)
        {
            if (state != GameState.Shop && _root != null) _root.gameObject.SetActive(false);
        }

        private void OnWeaponsChanged()
        {
            if (_root != null && _root.gameObject.activeSelf) Refresh();
        }

        public void Open()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Shop) return;
            _returnFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Refresh();
            if (EventSystem.current != null)
            {
                var first = _weaponList.GetComponentInChildren<Button>();
                EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : _closeButton.gameObject);
            }
        }

        private void Close()
        {
            _root.gameObject.SetActive(false);
            if (EventSystem.current == null) return;
            if (_returnFocus != null && _returnFocus.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(_returnFocus);
            _returnFocus = null;
        }

        private void Update()
        {
            if (_root != null && _root.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void Build()
        {
            _root = (RectTransform)UIFactory.CreateImage(transform, new Color(0.08f, 0.07f, 0.11f, 0.98f),
                Vector2.zero, new Vector2(1120f, 640f)).rectTransform;
            _root.name = "WeaponBuildScreen";
            _root.GetComponent<Image>().raycastTarget = true;
            UIFactory.CreateText(_root, "武器构筑", 31, Color.white,
                new Vector2(-365f, 274f), new Vector2(300f, 42f), TextAnchor.MiddleLeft);
            UIFactory.CreateText(_root, "商店购买/升级构筑；同型号共用解锁等级，但每一把武器独立装备。", 15,
                new Color(0.88f, 0.83f, 0.74f), new Vector2(67f, 271f), new Vector2(640f, 38f), TextAnchor.MiddleLeft);
            _closeButton = UIFactory.CreateButton(_root, "返回商店", Close,
                new Vector2(465f, -275f), new Vector2(155f, 44f), 17);
            _status = UIFactory.CreateText(_root, "", 15, new Color(0.92f, 0.80f, 0.65f),
                new Vector2(-120f, -275f), new Vector2(800f, 38f), TextAnchor.MiddleLeft);

            UIFactory.CreateText(_root, "持有武器", 21, Color.white,
                new Vector2(-356f, 224f), new Vector2(320f, 32f), TextAnchor.MiddleLeft);
            _detailTitle = UIFactory.CreateText(_root, "可配置构筑", 21, Color.white,
                new Vector2(65f, 224f), new Vector2(600f, 32f), TextAnchor.MiddleLeft);
            _weaponList = CreateScrollList(new Vector2(-350f, -12f), new Vector2(345f, 425f), "OwnedWeaponList");
            _modList = CreateScrollList(new Vector2(155f, -12f), new Vector2(635f, 425f), "AvailableBuildList");
        }

        private RectTransform CreateScrollList(Vector2 pos, Vector2 size, string name)
        {
            var view = (RectTransform)UIFactory.CreateImage(_root, new Color(0.16f, 0.14f, 0.19f),
                pos, size).rectTransform;
            view.name = name;
            view.GetComponent<Image>().raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.viewport = view;

            var contentGo = new GameObject("Content");
            var content = contentGo.AddComponent<RectTransform>();
            content.SetParent(view, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(9, 9, 9, 9);
            layout.spacing = 7f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return content;
        }

        private static void Clear(RectTransform list)
        {
            for (int i = list.childCount - 1; i >= 0; i--)
            {
                var child = list.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }

        private void Refresh()
        {
            var ws = System;
            if (ws == null) return;
            var previousFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool restoreBuildFocus = previousFocus != null && previousFocus.transform.IsChildOf(_modList);
            int previousBuildIndex = 0;
            if (restoreBuildFocus)
            {
                var oldActions = _modList.GetComponentsInChildren<Button>();
                for (int i = 0; i < oldActions.Length; i++)
                    if (oldActions[i].gameObject == previousFocus) { previousBuildIndex = i; break; }
            }
            Clear(_weaponList);
            Clear(_modList);
            if (ws.Weapons.Count == 0)
            {
                _detailTitle.text = "尚无武器";
                _status.text = "先在商店购买武器。";
                return;
            }
            _selectedWeapon = Mathf.Clamp(_selectedWeapon, 0, ws.Weapons.Count - 1);
            for (int i = 0; i < ws.Weapons.Count; i++)
            {
                int index = i;
                var weapon = ws.Weapons[i];
                string marker = i == _selectedWeapon ? "▶  " : "    ";
                string label = marker + weapon.Data.displayName + "  Lv." + weapon.Level +
                               "\n    已装 " + weapon.ModificationIds.Count + " 项";
                var button = UIFactory.CreateButton(_weaponList, label, () =>
                {
                    _selectedWeapon = index;
                    Refresh();
                }, Vector2.zero, new Vector2(310f, 69f), 15);
                button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                button.GetComponent<Image>().color = i == _selectedWeapon
                    ? new Color(0.47f, 0.39f, 0.32f) : new Color(0.28f, 0.25f, 0.31f);
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 69f;
                var icon = UIFactory.CreateImage(button.transform, Color.white,
                    new Vector2(119f, 0f), new Vector2(47f, 47f));
                icon.sprite = AssetLoader.LoadWeaponSprite(weapon.Data.id);
                if (icon.sprite != null) icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
            }

            var selected = ws.Weapons[_selectedWeapon];
            _detailTitle.text = selected.Data.displayName + " · 第 " + (_selectedWeapon + 1) + " 槽";
            _status.text = "构筑槽 " + selected.ModificationIds.Count + "/" + selected.MaxModificationSlots +
                "（随武器等级增加）· 同组互斥" +
                (selected.HasGrowthBuild ? "\n本武器成长 " + selected.GrowthStacks + "/" +
                    selected.GrowthMaxStacks + " 层 · " + selected.GrowthKills + " 击杀" : "");
            AddBuildCard("原始攻击", "卸下这把武器的所有构筑，不影响其他同型号武器。",
                selected.ModificationIds.Count == 0 ? "当前状态" : "卸下全部", selected.ModificationIds.Count != 0,
                () => ws.UnequipModifications(_selectedWeapon));

            var all = new List<WeaponModificationData>();
            foreach (var mod in GameDatabase.WeaponModifications.Values)
                if (mod != null && !mod.IsBranch && mod.AppliesTo(selected.Data)) all.Add(mod);
            all.Sort((a, b) => string.CompareOrdinal(a.displayName, b.displayName));
            foreach (var mod in all)
            {
                AddModificationCard(mod, selected, ws);
                // 只展示这把武器当前已激活基础构筑下的已解锁支线。
                // 未解锁内容在商店购买，不占用装备配置列表。
                if (!selected.HasModification(mod.id)) continue;
                var branches = new List<WeaponModificationData>();
                foreach (var candidate in GameDatabase.WeaponModifications.Values)
                    if (candidate != null && candidate.parentId == mod.id &&
                        candidate.AppliesTo(selected.Data) && ws.ModificationLevel(candidate.id) > 0)
                        branches.Add(candidate);
                branches.Sort((a, b) => string.CompareOrdinal(a.displayName, b.displayName));
                foreach (var branch in branches) AddModificationCard(branch, selected, ws);
            }
            if (all.Count == 0)
                AddBuildCard("暂无专属构筑", "这个型号目前还没有可购买的构筑。", "", false, null);
            // Rebuilding rows destroys the previously focused button; restore a deterministic
            // keyboard/gamepad target instead of leaving focus on an inactive object.
            if (EventSystem.current != null)
            {
                var focused = EventSystem.current.currentSelectedGameObject;
                if (focused != null && !focused.activeInHierarchy)
                {
                    if (restoreBuildFocus)
                    {
                        var actions = _modList.GetComponentsInChildren<Button>();
                        for (int i = Mathf.Min(previousBuildIndex, actions.Length - 1); i >= 0; i--)
                            if (actions[i].interactable)
                            {
                                EventSystem.current.SetSelectedGameObject(actions[i].gameObject);
                                return;
                            }
                    }
                    var rows = _weaponList.GetComponentsInChildren<Button>();
                    if (_selectedWeapon < rows.Length)
                        EventSystem.current.SetSelectedGameObject(rows[_selectedWeapon].gameObject);
                }
            }
        }

        private void AddModificationCard(WeaponModificationData mod, WeaponInstance selected, WeaponSystem ws)
        {
            bool unlocked = ws.ModificationLevel(mod.id) > 0;
            bool equipped = selected.HasModification(mod.id);
            bool parentOwned = !mod.IsBranch || ws.ModificationLevel(mod.parentId) > 0;
            bool parentEquipped = !mod.IsBranch || selected.HasModification(mod.parentId);
            bool hasSpace = mod.IsBranch ? parentEquipped :
                selected.ModificationIds.Count < selected.MaxModificationSlots ||
                selected.HasModificationGroup(mod.exclusiveGroup);
            string state = equipped ? "卸下" : !parentOwned ? "先买基础" : !unlocked ? "未购买" :
                !parentEquipped ? "先装备基础" : hasSpace ? "选择支线" : "先卸一项";
            if (!mod.IsBranch && unlocked && !equipped && hasSpace) state = "装备";
            var captured = mod;
            string level = unlocked ? "  Lv." + ws.ModificationLevel(mod.id) + "/" + mod.MaxLevel : "";
            AddBuildCard((mod.IsBranch ? "└ 支线 · " : mod.IsUniversal ? "通用基础 · " : "基础 · ") +
                mod.displayName + level, mod.description, state,
                equipped || unlocked && parentEquipped && hasSpace,
                () => { if (equipped) ws.UnequipModification(_selectedWeapon, captured.id);
                    else ws.EquipModification(_selectedWeapon, captured.id); });
        }

        private void AddBuildCard(string title, string description, string action, bool enabled, System.Action clicked)
        {
            var card = (RectTransform)UIFactory.CreateImage(_modList, new Color(0.30f, 0.26f, 0.32f),
                Vector2.zero, new Vector2(595f, 124f)).rectTransform;
            var layout = card.gameObject.AddComponent<LayoutElement>();
            var heading = UIFactory.CreateText(card, title, 18, Color.white,
                Vector2.zero, new Vector2(440f, 28f), TextAnchor.MiddleLeft);
            var body = UIFactory.CreateText(card, description, 14, new Color(0.89f, 0.85f, 0.81f),
                Vector2.zero, new Vector2(435f, 72f), TextAnchor.UpperLeft);
            // A build's tradeoff must remain visible. Let the scroll list grow instead of
            // silently cutting the description at a fixed two-line height.
            float bodyHeight = Mathf.Max(72f, Mathf.Ceil(body.preferredHeight) + 8f);
            float cardHeight = Mathf.Max(124f, bodyHeight + 58f);
            layout.preferredHeight = cardHeight;
            card.sizeDelta = new Vector2(card.sizeDelta.x, cardHeight);
            body.rectTransform.sizeDelta = new Vector2(435f, bodyHeight);
            heading.rectTransform.anchoredPosition = new Vector2(-55f, cardHeight * 0.5f - 24f);
            body.rectTransform.anchoredPosition = new Vector2(-57f, cardHeight * 0.5f - 48f - bodyHeight * 0.5f);
            if (string.IsNullOrEmpty(action)) return;
            var button = UIFactory.CreateButton(card, action, () =>
            {
                clicked?.Invoke();
                Refresh();
            }, new Vector2(226f, -3f), new Vector2(105f, 43f), 15);
            button.interactable = enabled;
        }
    }
}
