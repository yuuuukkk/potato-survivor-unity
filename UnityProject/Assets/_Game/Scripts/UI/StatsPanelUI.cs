using RogueLike.Core;
using RogueLike.Data;
using System.Text;
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
        [SerializeField] private Text _valueText;
        private readonly StringBuilder _valueBuilder = new StringBuilder(320);

        private void Awake()
        {
            if (_panel == null)
            {
                var image = UIFactory.CreateImage(transform, Color.white,
                    new Vector2(475f, 0f), new Vector2(330f, 500f));
                image.name = "StatsPanel";
                image.sprite = Resources.Load<Sprite>("Art/UI/ui_stats_panel_v2");
                image.type = Image.Type.Simple;
                _panel = image.rectTransform;
            }

            if (_body == null)
            {
                _body = UIFactory.CreateText(_panel, "", 16, new Color(0.88f, 0.84f, 0.78f),
                    new Vector2(-82f, -20f), new Vector2(108f, 360f), TextAnchor.UpperLeft);
                _body.lineSpacing = 1.3f;
                _body.verticalOverflow = VerticalWrapMode.Truncate;
            }
            if (string.IsNullOrEmpty(_body.text)) _body.text = string.Join("\n", Names);
            _body.gameObject.SetActive(true);

            if (_valueText == null)
            {
                _valueText = UIFactory.CreateText(_panel, "", 16, Color.white,
                    new Vector2(82f, -20f), new Vector2(108f, 360f), TextAnchor.UpperRight);
                _valueText.lineSpacing = 1.3f;
                _valueText.verticalOverflow = VerticalWrapMode.Truncate;
            }
            _valueText.supportRichText = true;

            Text title = null;
            foreach (var label in _panel.GetComponentsInChildren<Text>(true))
                if (label != _body && (label.text.Contains("当前属性") || label.text.Contains("I 关闭")))
                { title = label; break; }
            if (title == null)
                title = UIFactory.CreateText(_panel, "当前属性", 22, Color.white,
                    new Vector2(0f, 200f), new Vector2(260f, 34f));

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
            _valueBuilder.Clear();
            for (int i = 0; i < Names.Length; i++)
            {
                var type = (StatType)i;
                float current = stats.Get(type);
                float difference = current - Baseline(type);
                if (i > 0) _valueBuilder.Append('\n');
                _valueBuilder.Append(difference > 0.0001f ? "<color=#75F28A>"
                    : difference < -0.0001f ? "<color=#FF7070>" : "<color=#F7F2EC>");
                _valueBuilder.Append(Format(type, current));
                _valueBuilder.Append("</color>");
            }
            string valueText = _valueBuilder.ToString();
            if (_valueText.text != valueText) _valueText.text = valueText;
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
                case StatType.Knockback: return v.ToString("0.#");
                case StatType.HpRegen: return v.ToString("0.#") + "/秒";
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
