using System.Collections.Generic;
using RogueLike.Combat;
using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueLike.UI
{
    /// <summary>
    /// 底部物品栏：可见武器格数量由角色配置决定，武器后显示道具。
    /// 购买/拾取后自动刷新；鼠标悬停显示名称 + 描述（Tooltip）。
    /// 素材未配置前用稀有度色块占位。
    /// </summary>
    public class InventoryBarUI : MonoBehaviour
    {
        private const int SlotCount = 12; // 6 武器 + 6 道具
        private readonly Slot[] _slots = new Slot[SlotCount];
        private int _itemOffset;
        private Button _itemPrev;
        private Button _itemNext;
        [SerializeField] private RectTransform _root;
        [SerializeField] private RectTransform[] _slotFrames;
        [SerializeField] private Button _authoredItemPrev;
        [SerializeField] private Button _authoredItemNext;
        private bool HasAuthoredSlots
        {
            get
            {
                if (_slotFrames == null || _slotFrames.Length != SlotCount) return false;
                for (int i = 0; i < _slotFrames.Length; i++)
                    if (_slotFrames[i] != null) return true;
                return false;
            }
        }

        private class Slot
        {
            public RectTransform Frame;
            public Image Icon;
            public Text MainText;
            public Text BadgeText;
            public int Index;
        }

        private void Awake()
        {
            // 用户从场景中删除物品栏时，不在运行时把它重新创建出来。
            if (_root == null)
            {
                enabled = false;
                return;
            }
            EnsureSlotsBuilt();
            EventBus.WeaponsChanged += Refresh;
            EventBus.InventoryChanged += Refresh;
            EventBus.StateChanged += OnStateChanged;
            Refresh();
        }

        private void OnDestroy()
        {
            EventBus.WeaponsChanged -= Refresh;
            EventBus.InventoryChanged -= Refresh;
            EventBus.StateChanged -= OnStateChanged;
        }

        /// <summary>使用场景中的格子；旧场景没有格子时才创建回退布局。</summary>
        private void EnsureSlotsBuilt()
        {
            if (_slots[0] != null) return; // 已创建过

            for (int i = 0; i < SlotCount; i++)
            {
                float x = -286f + i * 52f;
                var s = new Slot { Index = i };

                // 外框（稀有度色）+ 内底
                s.Frame = HasAuthoredSlots ? _slotFrames[i] : null;
                if (s.Frame == null)
                    s.Frame = (RectTransform)UIFactory.CreateImage(_root,
                        new Color(0.18f, 0.18f, 0.24f), new Vector2(x, -320f), new Vector2(46f, 46f)).rectTransform;
                s.Frame.GetComponent<Image>().raycastTarget = true;
                s.Icon = s.Frame.Find("Icon")?.GetComponent<Image>();
                if (s.Icon == null)
                    s.Icon = UIFactory.CreateImage(s.Frame,
                        new Color(0.10f, 0.10f, 0.14f), Vector2.zero, new Vector2(42f, 42f));
                s.MainText = s.Frame.Find("MainText")?.GetComponent<Text>();
                if (s.MainText == null)
                    s.MainText = UIFactory.CreateText(s.Frame, "", 15, Color.white, Vector2.zero, new Vector2(40f, 28f));
                s.BadgeText = s.Frame.Find("BadgeText")?.GetComponent<Text>();
                if (s.BadgeText == null)
                    s.BadgeText = UIFactory.CreateText(s.Frame, "", 11, new Color(1f, 0.9f, 0.5f),
                        new Vector2(14f, -13f), new Vector2(28f, 16f), TextAnchor.MiddleRight);

                // 悬停 tooltip
                var et = s.Frame.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => OnPointerEnter(s));
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => { if (TooltipUI.Instance != null) TooltipUI.Instance.Hide(); });
                et.triggers.Add(enter);
                et.triggers.Add(exit);
                if (i >= 6)
                {
                    var scroll = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
                    scroll.callback.AddListener(data =>
                    {
                        var pointer = data as PointerEventData;
                        if (pointer != null) ScrollItems(pointer.scrollDelta.y > 0f ? -1 : 1);
                    });
                    et.triggers.Add(scroll);
                }

                _slots[i] = s;
            }
            _itemPrev = _authoredItemPrev != null ? _authoredItemPrev :
                UIFactory.CreateButton(_root, "‹", null,
                    new Vector2(0f, -286f), new Vector2(28f, 24f), 18);
            _itemNext = _authoredItemNext != null ? _authoredItemNext :
                UIFactory.CreateButton(_root, "›", null,
                    new Vector2(0f, -286f), new Vector2(28f, 24f), 18);
            _itemPrev.onClick.AddListener(() => ScrollItems(-1));
            _itemNext.onClick.AddListener(() => ScrollItems(1));
        }

        private void ScrollItems(int direction)
        {
            var gm = GameManager.Instance;
            var inv = gm != null && gm.Player != null ? gm.Player.GetComponent<Inventory>() : null;
            int count = inv != null ? inv.Stacks.Count : 0;
            _itemOffset = Mathf.Clamp(_itemOffset + direction, 0, Mathf.Max(0, count - 6));
            if (TooltipUI.Instance != null) TooltipUI.Instance.Hide();
            Refresh();
        }

        private void OnPointerEnter(Slot s)
        {
            if (TooltipUI.Instance == null) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;

            if (s.Index < 6)
            {
                var ws = gm.Player.GetComponent<WeaponSystem>();
                if (ws == null || s.Index >= ws.Weapons.Count) return;
                var w = ws.Weapons[s.Index];
                TooltipUI.Instance.Show(
                    $"{w.Data.displayName} Lv.{w.Level}",
                    w.Data.description + $"\n伤害 {w.EffectiveDamage(gm.Player.Stats):F0}  间隔 {w.EffectiveInterval(gm.Player.Stats):F2}s",
                    RarityInfo.Color((Rarity)Mathf.Clamp(w.Level - 1, 0, 4)), new Vector2(320f, 120f));
            }
            else
            {
                var inv = gm.Player.GetComponent<Inventory>();
                if (inv == null) return;
                int idx = s.Index - 6 + _itemOffset;
                if (idx >= inv.Stacks.Count) return;
                var st = inv.Stacks[idx];
                TooltipUI.Instance.Show(
                    $"{st.Data.displayName} ×{st.Count}",
                    st.Data.description,
                    RarityInfo.Color(st.Data.rarity), new Vector2(320f, 100f));
            }
        }

        private void OnStateChanged(GameState state)
        {
            bool show = state == GameState.Playing || state == GameState.Shop || state == GameState.LevelUp;
            _root.gameObject.SetActive(show);
        }

        private void Refresh()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null)
            {
                for (int i = 0; i < SlotCount; i++) SetSlotEmpty(i);
                _itemPrev.gameObject.SetActive(false);
                _itemNext.gameObject.SetActive(false);
                return;
            }

            var ws = gm.Player.GetComponent<WeaponSystem>();
            var inv = gm.Player.GetComponent<Inventory>();
            int wc = ws != null ? ws.Weapons.Count : 0;
            int weaponLimit = ws != null ? Mathf.Clamp(ws.MaxSlots, 1, 6) : 6;
            int ic = inv != null ? inv.Stacks.Count : 0;
            _itemOffset = Mathf.Clamp(_itemOffset, 0, Mathf.Max(0, ic - 6));

            int visibleCount = weaponLimit + 6;
            float firstItemX = -(visibleCount - 1) * 26f + weaponLimit * 52f;
            if (!HasAuthoredSlots)
            {
                _itemPrev.GetComponent<RectTransform>().anchoredPosition = new Vector2(firstItemX, -285f);
                _itemNext.GetComponent<RectTransform>().anchoredPosition = new Vector2(firstItemX + 5 * 52f, -285f);
            }
            _itemPrev.gameObject.SetActive(ic > 6);
            _itemNext.gameObject.SetActive(ic > 6);
            _itemPrev.interactable = _itemOffset > 0;
            _itemNext.interactable = _itemOffset < ic - 6;
            for (int i = 0; i < SlotCount; i++)
            {
                bool visible = i >= 6 || i < weaponLimit;
                _slots[i].Frame.gameObject.SetActive(visible);
                if (!visible) continue;
                int visibleIndex = i < 6 ? i : weaponLimit + i - 6;
                if (!HasAuthoredSlots)
                    _slots[i].Frame.anchoredPosition = new Vector2(-(visibleCount - 1) * 26f + visibleIndex * 52f, -320f);
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (i < 6)
                {
                    if (i < wc)
                    {
                        var w = ws.Weapons[i];
                        Color c = RarityInfo.Color((Rarity)Mathf.Clamp(w.Level - 1, 0, 4));
                        SetSlot(i, c, AssetLoader.LoadWeaponSprite(w.Data.id), "", $"Lv{w.Level}");
                    }
                    else SetSlotEmpty(i);
                }
                else
                {
                    int idx = i - 6 + _itemOffset;
                    if (idx < ic)
                    {
                        var st = inv.Stacks[idx];
                        Color c = RarityInfo.Color(st.Data.rarity);
                        SetSlot(i, c, AssetLoader.LoadItemSprite(st.Data.IconId), st.Data.displayName.Substring(0, 1), "×" + st.Count);
                    }
                    else SetSlotEmpty(i);
                }
            }
        }

        private void SetSlot(int i, Color rarityColor, Sprite sprite, string main, string badge)
        {
            var s = _slots[i];
            s.Frame.GetComponent<Image>().color = rarityColor * 0.5f + new Color(0.10f, 0.10f, 0.14f);
            s.Icon.sprite = sprite != null ? sprite : Resources.Load<Sprite>("Art/UI/ui_panel_round");
            s.Icon.type = sprite != null ? Image.Type.Simple : Image.Type.Sliced;
            s.Icon.preserveAspect = true;
            s.Icon.color = sprite != null ? Color.white : rarityColor * 0.18f + new Color(0.10f, 0.10f, 0.14f);
            s.MainText.text = main;
            s.MainText.color = Color.white;
            s.BadgeText.text = badge;
        }

        private void SetSlotEmpty(int i)
        {
            var s = _slots[i];
            s.Frame.GetComponent<Image>().color = new Color(0.14f, 0.14f, 0.18f);
            s.Icon.color = new Color(0.08f, 0.08f, 0.11f);
            s.Icon.sprite = Resources.Load<Sprite>("Art/UI/ui_panel_round");
            s.Icon.type = s.Icon.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            s.MainText.text = "";
            s.BadgeText.text = "";
        }
    }
}
