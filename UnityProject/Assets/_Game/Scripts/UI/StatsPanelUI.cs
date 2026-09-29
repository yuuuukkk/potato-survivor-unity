using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;
using UnityEngine.UI;

namespace RogueLike.UI
{
    /// <summary>升级时右侧的双列属性表；正向变化绿字、负向变化红字。</summary>
    public class StatsPanelUI : MonoBehaviour
    {
        private static readonly string[] Names =
        {
            "生命上限", "生命回复", "护甲", "闪避率", "伤害倍率", "攻速倍率",
            "暴击率", "暴击伤害", "射程倍率", "移速倍率", "吸血", "幸运", "拾取范围", "击退"
        };

        [SerializeField] private RectTransform _panel;
        [SerializeField] private Text _body;
        private readonly Text[] _values = new Text[Names.Length];

        private void Awake()
        {
            if (_panel == null)
                _panel = (RectTransform)UIFactory.CreateImage(transform,
                    new Color(0.07f, 0.07f, 0.12f, 0.95f), Vector2.zero, new Vector2(300f, 580f)).rectTransform;

            _panel.anchoredPosition = new Vector2(465f, 0f);
            _panel.sizeDelta = new Vector2(300f, 580f);
            if (_body != null) _body.gameObject.SetActive(false);

            Text title = null;
            foreach (var label in _panel.GetComponentsInChildren<Text>(true))
                if (label != _body && (label.text.Contains("当前属性") || label.text.Contains("I 关闭")))
                { title = label; break; }
            if (title == null)
                title = UIFactory.CreateText(_panel, "当前属性", 22, Color.white,
                    new Vector2(0f, 252f), new Vector2(260f, 32f));
            title.text = "当前属性";
            title.fontSize = 22;
            title.rectTransform.anchoredPosition = new Vector2(0f, 252f);
            title.rectTransform.sizeDelta = new Vector2(260f, 32f);

            for (int i = 0; i < Names.Length; i++)
            {
                float y = 205f - i * 28f;
                UIFactory.CreateText(_panel, Names[i], 14, new Color(0.85f, 0.83f, 0.8f),
                    new Vector2(-80f, y), new Vector2(110f, 24f), TextAnchor.MiddleLeft);
                _values[i] = UIFactory.CreateText(_panel, "", 14, Color.white,
                    new Vector2(70f, y), new Vector2(130f, 24f), TextAnchor.MiddleRight);
                _values[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                _values[i].verticalOverflow = VerticalWrapMode.Truncate;
            }

            EventBus.StateChanged += OnStateChanged;
            _panel.gameObject.SetActive(false);
        }

        private void OnDestroy() => EventBus.StateChanged -= OnStateChanged;

        private void OnStateChanged(GameState state)
        {
            _panel.gameObject.SetActive(state == GameState.LevelUp);
            if (state == GameState.LevelUp) Refresh();
        }

        private void Update()
        {
            if (_panel.gameObject.activeSelf) Refresh();
        }

        private void Refresh()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;
            var stats = gm.Player.Stats;
            for (int i = 0; i < Names.Length; i++)
            {
                var type = (StatType)i;
                float current = stats.Get(type);
                float difference = current - Baseline(type);
                _values[i].text = Format(type, current);
                _values[i].color = difference > 0.0001f ? new Color(0.46f, 0.95f, 0.54f)
                    : difference < -0.0001f ? new Color(1f, 0.43f, 0.43f) : Color.white;
            }
        }

        private static float Baseline(StatType type)
        {
            var balance = GameDatabase.Balance;
            switch (type)
            {
                case StatType.MaxHp: return balance != null ? balance.baseMaxHp : 100f;
                case StatType.CritDamageMult: return balance != null ? balance.baseCritDamageMultiplier : 2f;
                case StatType.PickupRange: return balance != null ? balance.basePickupRange : 1.6f;
                case StatType.DamageMult:
                case StatType.AttackSpeedMult:
                case StatType.RangeMult:
                case StatType.MoveSpeedMult: return 1f;
                default: return 0f;
            }
        }

        private static string Format(StatType type, float v)
        {
            switch (type)
            {
                case StatType.MaxHp:
                case StatType.Armor:
                case StatType.Luck:
                case StatType.Knockback:
                case StatType.HpRegen: return v.ToString("0.0") + "/秒";
                case StatType.DodgeChance:
                case StatType.CritChance:
                case StatType.LifeSteal: return Mathf.RoundToInt(v * 100f) + "%";
                case StatType.CritDamageMult: return "+" + Mathf.RoundToInt((v - 1f) * 100f) + "%";
                case StatType.DamageMult:
                case StatType.AttackSpeedMult:
                case StatType.RangeMult:
                case StatType.MoveSpeedMult:
                default: return v.ToString("0.00");
            }
        }
    }
}
