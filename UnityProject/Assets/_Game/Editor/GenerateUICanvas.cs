using RogueLike.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RogueLike.EditorTools
{
    /// <summary>
    /// 一键生成 UICanvas 预制体（Assets/_Game/Resources/Prefabs/UI/UICanvas.prefab）。
    /// 生成后打开预制体即可在 Inspector/场景中可视化调整所有 UI（位置/大小/颜色/文字）；
    /// 运行时 GameBootstrap 优先加载该预制体，未生成时自动回退代码创建（同布局）。
    /// 动态内容（商店卡/升级卡/角色按钮/物品栏格子）由代码按数据填充进预制体容器。
    /// 菜单：土豆幸存者 / 生成 UI 画布预制体
    /// </summary>
    public static class GenerateUICanvas
    {
        private const string PrefabPath = "Assets/_Game/Resources/Prefabs/UI/UICanvas.prefab";

        [MenuItem("土豆幸存者/生成 UI 画布预制体")]
        public static void Run()
        {
            System.IO.Directory.CreateDirectory("Assets/_Game/Resources/Prefabs/UI");

            var canvas = UIFactory.CreateCanvas("UICanvas");
            var root = canvas.gameObject;

            // ================= HUD =================
            var hud = root.AddComponent<HUDController>();
            var hudRoot = CreateRect("HUD", root.transform, Vector2.zero, Vector2.zero);
            // 血条（红色 Slider，加粗）：文字作为 Slider 子节点，永远居中在条内
            var hpBar = UIFactory.CreateSlider(hudRoot, new Vector2(-430f, 320f), new Vector2(300f, 26f),
                new Color(0.18f, 0.06f, 0.06f), new Color(0.95f, 0.30f, 0.25f));
            var hpText = UIFactory.CreateText(hpBar.transform, "HP 100/100", 16, Color.white, Vector2.zero, new Vector2(300f, 26f));
            AddOutline(hpText);
            // 经验条（绿色 Slider，加高到 16px）：Lv 与经验数值都在条内
            var xpBar = UIFactory.CreateSlider(hudRoot, new Vector2(-430f, 290f), new Vector2(300f, 16f),
                new Color(0.06f, 0.16f, 0.08f), new Color(0.35f, 1.0f, 0.5f));
            var levelText = UIFactory.CreateText(xpBar.transform, "Lv.1", 12, new Color(0.7f, 1f, 0.8f),
                new Vector2(148f, 0f), new Vector2(80f, 16f), TextAnchor.MiddleRight);
            var xpText = UIFactory.CreateText(xpBar.transform, "0/10", 13, Color.white, Vector2.zero, new Vector2(300f, 16f));
            AddOutline(xpText);
            var matText = UIFactory.CreateText(hudRoot, "材料 0", 22, new Color(1f, 0.85f, 0.25f), new Vector2(430f, 320f), new Vector2(260f, 30f));
            var waveText = UIFactory.CreateText(hudRoot, "第 1 波", 26, Color.white, new Vector2(0f, 318f), new Vector2(300f, 34f));
            var timerText = UIFactory.CreateText(hudRoot, "30.0", 20, Color.white, new Vector2(0f, 286f), new Vector2(200f, 26f));
            var tipText = UIFactory.CreateText(hudRoot, "按 I 查看属性", 13, new Color(0.55f, 0.55f, 0.62f), new Vector2(430f, 293f), new Vector2(220f, 18f), TextAnchor.MiddleRight);
            var weaponsText = UIFactory.CreateText(hudRoot, "", 18, Color.white, new Vector2(-560f, -320f), new Vector2(300f, 120f), TextAnchor.LowerLeft);
            Set(hud, "_root", hudRoot); Set(hud, "_hpBar", hpBar); Set(hud, "_xpBar", xpBar);
            Set(hud, "_hpText", hpText); Set(hud, "_matText", matText); Set(hud, "_waveText", waveText);
            Set(hud, "_timerText", timerText); Set(hud, "_weaponsText", weaponsText);
            Set(hud, "_levelText", levelText); Set(hud, "_xpText", xpText); Set(hud, "_tipText", tipText);

            // ================= 主菜单 =================
            var menu = root.AddComponent<MainMenuUI>();
            var menuPanel = (RectTransform)UIFactory.CreateImage(root.transform,
                new Color(0.13f, 0.13f, 0.21f, 0.99f), Vector2.zero, new Vector2(1120f, 640f)).rectTransform;
            UIFactory.CreateText(menuPanel, "土豆幸存者", 56, new Color(1f, 0.85f, 0.45f), new Vector2(0f, 250f), new Vector2(600f, 70f));
            UIFactory.CreateText(menuPanel, "自动攻击 · 走位生存 · 商店构筑 · 20 波", 20, new Color(0.75f, 0.75f, 0.82f), new Vector2(0f, 196f), new Vector2(700f, 30f));
            UIFactory.CreateText(menuPanel, "选择角色", 24, Color.white, new Vector2(0f, 130f), new Vector2(300f, 32f));
            var charGrid = CreateRect("CharGrid", menuPanel, new Vector2(0f, 60f), new Vector2(700f, 70f));
            var descText = UIFactory.CreateText(menuPanel, "", 16, new Color(0.75f, 0.75f, 0.82f), new Vector2(0f, -10f), new Vector2(800f, 50f));
            var startBtn = UIFactory.CreateButton(menuPanel, "开始游戏", null, new Vector2(0f, -110f), new Vector2(260f, 58f), 26);
            var quitBtn = UIFactory.CreateButton(menuPanel, "退出", null, new Vector2(0f, -190f), new Vector2(160f, 40f), 18);
            Set(menu, "_panel", menuPanel); Set(menu, "_charGrid", charGrid);
            Set(menu, "_descText", descText); Set(menu, "_startBtn", startBtn); Set(menu, "_quitBtn", quitBtn);

            // ================= 商店 =================
            var shop = root.AddComponent<ShopUI>();
            var shopPanel = (RectTransform)UIFactory.CreateImage(root.transform,
                new Color(0.07f, 0.07f, 0.11f, 0.97f), Vector2.zero, new Vector2(1120f, 640f)).rectTransform;
            var shopTitle = UIFactory.CreateText(shopPanel, "商店", 34, Color.white, new Vector2(0f, 268f), new Vector2(300f, 44f));
            var shopMat = UIFactory.CreateText(shopPanel, "材料 0", 24, new Color(1f, 0.85f, 0.25f), new Vector2(0f, 226f), new Vector2(300f, 32f));
            var slotGrid = CreateRect("SlotGrid", shopPanel, new Vector2(0f, 60f), new Vector2(840f, 300f));
            var refreshBtn = UIFactory.CreateButton(shopPanel, "刷新", null, new Vector2(-300f, -260f), new Vector2(180f, 46f));
            var nextWaveBtn = UIFactory.CreateButton(shopPanel, "下一波", null, new Vector2(300f, -260f), new Vector2(180f, 46f));
            Set(shop, "_panel", shopPanel); Set(shop, "_grid", slotGrid);
            Set(shop, "_matText", shopMat); Set(shop, "_titleText", shopTitle);
            Set(shop, "_refreshBtn", refreshBtn); Set(shop, "_nextWaveBtn", nextWaveBtn);

            // ================= 结算 =================
            var over = root.AddComponent<GameOverUI>();
            var overPanel = (RectTransform)UIFactory.CreateImage(root.transform,
                new Color(0.05f, 0.04f, 0.08f, 0.97f), Vector2.zero, new Vector2(1120f, 640f)).rectTransform;
            UIFactory.CreateText(overPanel, "游戏结束", 52, new Color(1f, 0.45f, 0.4f), new Vector2(0f, 220f), new Vector2(600f, 70f));
            var statsText = UIFactory.CreateText(overPanel, "", 26, Color.white, new Vector2(0f, 90f), new Vector2(700f, 160f));
            var restartBtn = UIFactory.CreateButton(overPanel, "重新开始", null, new Vector2(-150f, -140f), new Vector2(240f, 56f), 24);
            var menuBtn = UIFactory.CreateButton(overPanel, "回主菜单", null, new Vector2(150f, -140f), new Vector2(240f, 56f), 24);
            Set(over, "_panel", overPanel); Set(over, "_statsText", statsText);
            Set(over, "_restartBtn", restartBtn); Set(over, "_menuBtn", menuBtn);

            // ================= 物品栏 =================
            var invBar = root.AddComponent<InventoryBarUI>();
            var invRoot = CreateRect("InventoryBar", root.transform, Vector2.zero, Vector2.zero);
            Set(invBar, "_root", invRoot);

            // ================= 升级三选一 =================
            var lv = root.AddComponent<LevelUpUI>();
            var lvPanel = (RectTransform)UIFactory.CreateImage(root.transform,
                new Color(0.06f, 0.06f, 0.10f, 0.97f), Vector2.zero, new Vector2(960f, 460f)).rectTransform;
            var lvTitle = UIFactory.CreateText(lvPanel, "升级！选择一项强化", 32, Color.white, new Vector2(0f, 180f), new Vector2(700f, 44f));
            var lvGrid = CreateRect("UpgradeGrid", lvPanel, new Vector2(0f, 20f), new Vector2(880f, 320f));
            Set(lv, "_panel", lvPanel); Set(lv, "_titleText", lvTitle); Set(lv, "_grid", lvGrid);

            // ================= Tooltip =================
            var tip = root.AddComponent<TooltipUI>();
            var tipPanel = (RectTransform)UIFactory.CreateImage(root.transform,
                new Color(0.08f, 0.08f, 0.12f, 0.96f), new Vector2(-600f, 320f), new Vector2(320f, 110f)).rectTransform;
            var tipTitle = UIFactory.CreateText(tipPanel, "", 17, Color.white, new Vector2(0f, 30f), new Vector2(296f, 26f));
            var tipBody = UIFactory.CreateText(tipPanel, "", 13, new Color(0.80f, 0.80f, 0.88f), new Vector2(0f, -4f), new Vector2(296f, 40f));
            tipBody.alignment = TextAnchor.UpperCenter;
            Set(tip, "_panel", tipPanel); Set(tip, "_bg", tipPanel.GetComponent<Image>());
            Set(tip, "_title", tipTitle); Set(tip, "_body", tipBody);

            // ================= 属性面板 =================
            var stats = root.AddComponent<StatsPanelUI>();
            var statsPanel = (RectTransform)UIFactory.CreateImage(root.transform,
                new Color(0.07f, 0.07f, 0.12f, 0.95f), new Vector2(430f, 60f), new Vector2(380f, 560f)).rectTransform;
            UIFactory.CreateText(statsPanel, "属性 (I 关闭)", 24, Color.white, new Vector2(0f, 250f), new Vector2(340f, 34f));
            var statsBody = UIFactory.CreateText(statsPanel, "", 15, new Color(0.85f, 0.85f, 0.92f),
                new Vector2(0f, 40f), new Vector2(350f, 440f), TextAnchor.UpperCenter);
            statsBody.alignment = TextAnchor.MiddleLeft;
            Set(stats, "_panel", statsPanel); Set(stats, "_body", statsBody);

            // 初始显隐：面板全部隐藏（运行时按状态驱动），HUD 保持可见
            menuPanel.gameObject.SetActive(false);
            shopPanel.gameObject.SetActive(false);
            overPanel.gameObject.SetActive(false);
            lvPanel.gameObject.SetActive(false);
            tipPanel.gameObject.SetActive(false);
            statsPanel.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[生成 UI 画布预制体] 完成：" + PrefabPath + "。打开预制体可在 Inspector 可视化调整所有 UI（位置/大小/颜色/文字）。");
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>给文字加黑色描边，保证在亮色条上清晰可见（不淡）。</summary>
        private static void AddOutline(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
