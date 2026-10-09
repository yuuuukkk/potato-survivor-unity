using RogueLike.Core;
using RogueLike.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RogueLike.EditorTools
{
    /// <summary>
    /// 一键生成 UICanvas 预制体（Assets/_Game/Resources/Prefabs/UI/UICanvas.prefab）。
    /// 生成后打开预制体即可在 Inspector/场景中可视化调整主要 UI（位置/大小/颜色/文字）；
    /// 运行时 GameBootstrap 优先加载该预制体，未生成时自动回退代码创建（同布局）。
    /// 商品卡直接保存在预制体中；运行时只填充内容。
    /// 此菜单用于重建，会覆盖现有预制体的手动布局。
    /// </summary>
    public static class GenerateUICanvas
    {
        private const string PrefabPath = "Assets/_Game/Resources/Prefabs/UI/UICanvas.prefab";

        [MenuItem("土豆幸存者/重建 UI 画布预制体（覆盖当前布局）")]
        public static void Run()
        {
            System.IO.Directory.CreateDirectory("Assets/_Game/Resources/Prefabs/UI");

            var canvas = UIFactory.CreateCanvas("UICanvas");
            var root = canvas.gameObject;

            // ================= HUD =================
            var hud = root.AddComponent<HUDController>();
            var hudRoot = CreateRect("HUD", root.transform, Vector2.zero, Vector2.zero);
            AnchorRect(hudRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            // 左上固定 HUD：适配横竖屏和不同分辨率。
            var hpBar = UIFactory.CreateSlider(hudRoot, new Vector2(16f, -16f), new Vector2(260f, 24f),
                new Color(0.18f, 0.06f, 0.06f), Color.red);
            AnchorRect(hpBar.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(260f, 24f));
            var hpText = UIFactory.CreateText(hpBar.transform, "15/15", 14, Color.white, Vector2.zero, new Vector2(260f, 24f));
            AddOutline(hpText);
            // 经验条（绿色 Slider，加高到 16px）：Lv 与经验数值都在条内
            var xpBar = UIFactory.CreateSlider(hudRoot, new Vector2(16f, -43f), new Vector2(260f, 18f),
                new Color(0.06f, 0.16f, 0.08f), Color.green);
            AnchorRect(xpBar.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -43f), new Vector2(260f, 18f));
            var levelText = UIFactory.CreateText(xpBar.transform, "Lv.1", 12, new Color(0.7f, 1f, 0.8f),
                new Vector2(106f, 0f), new Vector2(70f, 18f), TextAnchor.MiddleRight);
            AnchorRect(levelText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(70f, 18f));
            var xpText = UIFactory.CreateText(xpBar.transform, "0/10", 12, Color.white, Vector2.zero, new Vector2(260f, 18f));
            AnchorRect(xpText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(112f, 18f));
            xpText.alignment = TextAnchor.MiddleLeft;
            AddOutline(xpText);
            var matIcon = UIFactory.CreateImage(hudRoot, Color.white, new Vector2(20f, -73f), new Vector2(26f, 26f));
            matIcon.name = "MaterialIcon"; matIcon.sprite = AssetLoader.LoadMaterialPickupSprite(); matIcon.preserveAspect = true;
            AnchorRect(matIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -73f), new Vector2(26f, 26f));
            var matText = UIFactory.CreateText(hudRoot, "0", 20, Color.white, new Vector2(52f, -73f), new Vector2(100f, 28f), TextAnchor.MiddleLeft);
            AnchorRect(matText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -73f), new Vector2(100f, 28f));
            var waveText = UIFactory.CreateText(hudRoot, "第 1 波", 22, Color.white, new Vector2(0f, -16f), new Vector2(220f, 30f));
            AnchorRect(waveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(220f, 30f));
            var timerText = UIFactory.CreateText(hudRoot, "30s", 20, Color.white, new Vector2(0f, -61f), new Vector2(160f, 28f));
            timerText.name = "Text_Countdown";
            AnchorRect(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -61f), new Vector2(160f, 28f));
            AddOutline(timerText);
            var tipText = UIFactory.CreateText(hudRoot, "按 I 查看属性", 13, new Color(0.8f, 0.8f, 0.82f), new Vector2(-16f, -78f), new Vector2(210f, 22f), TextAnchor.MiddleRight);
            AnchorRect(tipText.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-16f, -78f), new Vector2(210f, 22f));
            var weaponsText = UIFactory.CreateText(hudRoot, "", 18, Color.white, new Vector2(16f, 16f), new Vector2(300f, 120f), TextAnchor.LowerLeft);
            Set(hud, "_root", hudRoot); Set(hud, "_hpBar", hpBar); Set(hud, "_xpBar", xpBar);
            Set(hud, "_hpText", hpText); Set(hud, "_matText", matText); Set(hud, "_waveText", waveText);
            Set(hud, "_timerText", timerText); Set(hud, "_weaponsText", weaponsText);
            Set(hud, "_levelText", levelText); Set(hud, "_xpText", xpText); Set(hud, "_tipText", tipText);

            // ================= 主菜单 =================
            var menu = root.AddComponent<MainMenuUI>();
            var menuPanel = (RectTransform)UIFactory.CreateImage(root.transform,
                new Color(0.13f, 0.13f, 0.21f, 0.99f), Vector2.zero, new Vector2(1120f, 640f)).rectTransform;
            UIFactory.CreateText(menuPanel, "土豆幸存者", 56, new Color(1f, 0.85f, 0.45f), new Vector2(0f, 200f), new Vector2(600f, 70f));
            UIFactory.CreateText(menuPanel, "选择角色", 24, Color.white, new Vector2(0f, 130f), new Vector2(300f, 32f));
            var charGrid = CreateRect("CharGrid", menuPanel, new Vector2(0f, 10f), new Vector2(700f, 175f));
            var descText = UIFactory.CreateText(menuPanel, "", 19, new Color(0.85f, 0.84f, 0.86f), new Vector2(0f, -125f), new Vector2(800f, 50f));
            var startBtn = UIFactory.CreateButton(menuPanel, "开始游戏", null, new Vector2(0f, -210f), new Vector2(260f, 58f), 26);
            var quitBtn = UIFactory.CreateButton(menuPanel, "退出", null, new Vector2(0f, -275f), new Vector2(160f, 40f), 18);
            Set(menu, "_panel", menuPanel); Set(menu, "_charGrid", charGrid);
            Set(menu, "_descText", descText); Set(menu, "_startBtn", startBtn); Set(menu, "_quitBtn", quitBtn);

            // ================= 商店 =================
            var shop = root.AddComponent<ShopUI>();
            var shopPanel = (RectTransform)UIFactory.CreateImage(root.transform,
                new Color(0.07f, 0.07f, 0.11f, 0.97f), Vector2.zero, new Vector2(1120f, 640f)).rectTransform;
            shopPanel.name = "ShopPanel";
            var shopTitle = UIFactory.CreateText(shopPanel, "商店", 34, Color.white, new Vector2(0f, 225f), new Vector2(300f, 44f));
            var shopMat = UIFactory.CreateText(shopPanel, "0", 24, new Color(1f, 0.85f, 0.25f),
                new Vector2(380f, 225f), new Vector2(140f, 32f), TextAnchor.MiddleLeft);
            var slotGrid = CreateRect("SlotGrid", shopPanel, new Vector2(0f, -30f), new Vector2(900f, 400f));
            var cardFrames = new RectTransform[3];
            for (int i = 0; i < cardFrames.Length; i++)
                cardFrames[i] = CreateShopCardTemplate(slotGrid, i);
            var refreshBtn = UIFactory.CreateButton(shopPanel, "刷新", null, new Vector2(390f, -220f), new Vector2(125f, 46f));
            var nextWaveBtn = UIFactory.CreateButton(shopPanel, "下一波", null, new Vector2(220f, -220f), new Vector2(180f, 46f));
            var materialIcon = UIFactory.CreateImage(shopPanel, Color.white,
                new Vector2(285f, 225f), new Vector2(30f, 30f));
            materialIcon.name = "MaterialIcon";
            materialIcon.sprite = AssetLoader.LoadMaterialPickupSprite();
            materialIcon.color = Color.white;
            materialIcon.preserveAspect = true;
            var challengeBtn = UIFactory.CreateButton(shopPanel, "可选挑战", null,
                new Vector2(30f, -220f), new Vector2(160f, 46f), 17);
            var buildBtn = UIFactory.CreateButton(shopPanel, "武器构筑", null,
                new Vector2(-210f, -220f), new Vector2(160f, 46f), 17);
            var sellGrid = CreateRect("OwnedWeaponsForSale", shopPanel,
                new Vector2(-220f, -175f), new Vector2(640f, 78f));
            UIFactory.CreateText(sellGrid, "持有武器（出售）", 15, Color.white,
                new Vector2(-225f, 34f), new Vector2(190f, 24f)).name = "Title";
            for (int i = 0; i < 6; i++)
            {
                var owned = UIFactory.CreateButton(sellGrid, "", null,
                    new Vector2(-255f + i * 78f, -7f), new Vector2(68f, 62f), 12);
                owned.name = "OwnedWeapon_" + (i + 1);
                var art = UIFactory.CreateImage(owned.transform, Color.white,
                    new Vector2(0f, 5f), new Vector2(46f, 40f));
                art.name = "Art";
                art.sprite = Resources.Load<Sprite>("Art/Weapons/weapon_pistol");
                art.preserveAspect = true;
                UIFactory.CreateText(owned.transform, "Lv.1", 11, Color.white,
                    new Vector2(0f, -22f), new Vector2(58f, 16f)).name = "Level";
            }
            var feedbackText = UIFactory.CreateText(shopPanel, "", 14,
                new Color(0.93f, 0.86f, 0.70f), new Vector2(-250f, -270f),
                new Vector2(500f, 23f), TextAnchor.MiddleLeft);
            Set(shop, "_panel", shopPanel); Set(shop, "_grid", slotGrid);
            SetArray(shop, "_cardFrames", cardFrames);
            Set(shop, "_matText", shopMat); Set(shop, "_titleText", shopTitle);
            Set(shop, "_refreshBtn", refreshBtn); Set(shop, "_nextWaveBtn", nextWaveBtn);
            Set(shop, "_materialIcon", materialIcon); Set(shop, "_challengeBtn", challengeBtn);
            Set(shop, "_buildBtn", buildBtn); Set(shop, "_sellGrid", sellGrid);
            Set(shop, "_feedbackText", feedbackText);

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

            // ================= 波次结束强化四选一 =================
            var lv = root.AddComponent<LevelUpUI>();
            var lvPanel = (RectTransform)UIFactory.CreateImage(root.transform,
                new Color(0.035f, 0.03f, 0.045f, 0.94f), new Vector2(-150f, 0f), new Vector2(920f, 560f)).rectTransform;
            var lvTitle = UIFactory.CreateText(lvPanel, "波次结束 · 选择一项强化", 30, Color.white, new Vector2(0f, 190f), new Vector2(800f, 44f));
            var lvGrid = CreateRect("UpgradeGrid", lvPanel, new Vector2(0f, -18f), new Vector2(880f, 320f));
            Set(lv, "_panel", lvPanel); Set(lv, "_titleText", lvTitle); Set(lv, "_grid", lvGrid);

            // ================= Tooltip =================
            var tip = root.AddComponent<TooltipUI>();
            var tipPanel = (RectTransform)UIFactory.CreateImage(root.transform,
                new Color(0.08f, 0.08f, 0.12f, 0.96f), new Vector2(-600f, 320f), new Vector2(420f, 210f)).rectTransform;
            tipPanel.name = "TooltipPanel";
            var tipTitle = UIFactory.CreateText(tipPanel, "", 17, Color.white, new Vector2(0f, 78f), new Vector2(392f, 32f));
            var tipBody = UIFactory.CreateText(tipPanel, "", 16, new Color(0.80f, 0.80f, 0.88f), new Vector2(0f, -10f), new Vector2(392f, 148f));
            tipBody.alignment = TextAnchor.UpperCenter;
            tipBody.verticalOverflow = VerticalWrapMode.Truncate;
            Set(tip, "_panel", tipPanel); Set(tip, "_bg", tipPanel.GetComponent<Image>());
            Set(tip, "_title", tipTitle); Set(tip, "_body", tipBody);

            // ================= 属性面板 =================
            var stats = root.AddComponent<StatsPanelUI>();
            var statsImage = UIFactory.CreateImage(root.transform, Color.white,
                new Vector2(475f, 0f), new Vector2(330f, 500f));
            statsImage.name = "StatsPanel";
            statsImage.sprite = Resources.Load<Sprite>("Art/UI/ui_stats_panel_v2");
            statsImage.type = Image.Type.Simple;
            var statsPanel = statsImage.rectTransform;
            UIFactory.CreateText(statsPanel, "当前属性", 22, Color.white,
                new Vector2(0f, 200f), new Vector2(260f, 34f));
            var statsBody = UIFactory.CreateText(statsPanel,
                "生命上限\n生命回复\n护甲\n闪避率\n伤害倍率\n攻速倍率\n暴击率\n暴击伤害\n射程倍率\n移速倍率\n吸血\n幸运\n拾取范围\n击退",
                16, new Color(0.88f, 0.84f, 0.78f),
                new Vector2(-82f, -20f), new Vector2(108f, 360f), TextAnchor.UpperLeft);
            statsBody.name = "StatLabels";
            statsBody.lineSpacing = 1.3f;
            statsBody.verticalOverflow = VerticalWrapMode.Truncate;
            var statsValues = UIFactory.CreateText(statsPanel,
                "100\n0/秒\n0\n0%\n1.00\n1.00\n0%\n+100%\n1.00\n1.00\n0%\n0\n1.60\n0",
                16, Color.white, new Vector2(82f, -20f), new Vector2(108f, 360f), TextAnchor.UpperRight);
            statsValues.name = "StatValues";
            statsValues.lineSpacing = 1.3f;
            statsValues.verticalOverflow = VerticalWrapMode.Truncate;
            Set(stats, "_panel", statsPanel); Set(stats, "_body", statsBody);
            Set(stats, "_valueText", statsValues);

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

        private static RectTransform CreateShopCardTemplate(RectTransform grid, int index)
        {
            var card = UIFactory.CreateShopCard(grid,
                new Vector2(-310f + index * 310f, 25f), new Vector2(260f, 330f));
            card.name = "ShopCard_" + (index + 1);
            card.raycastTarget = true;
            var root = card.rectTransform;
            UIFactory.CreateText(root, "商品名称", 21, new Color(1f, 0.92f, 0.7f),
                new Vector2(0f, 100f), new Vector2(218f, 32f)).name = "Name";
            UIFactory.CreateText(root, "类型", 16, new Color(0.82f, 0.81f, 0.83f),
                new Vector2(0f, 70f), new Vector2(218f, 25f)).name = "Category";
            var art = UIFactory.CreateImage(root, Color.white,
                new Vector2(0f, 13f), new Vector2(82f, 82f));
            art.name = "Art";
            art.sprite = Resources.Load<Sprite>("Art/Items/item_heal_pack");
            art.color = Color.white;
            art.preserveAspect = true;
            UIFactory.CreateText(art.transform, "", 43, Color.white,
                Vector2.zero, new Vector2(76f, 68f)).name = "Symbol";
            var description = UIFactory.CreateText(root, "商品说明", 16,
                new Color(0.96f, 0.94f, 0.91f), new Vector2(0f, -58f), new Vector2(210f, 54f));
            description.name = "Description";
            description.verticalOverflow = VerticalWrapMode.Truncate;
            var buy = UIFactory.CreateButton(root, "购买 · 0", null,
                new Vector2(-33f, -112f), new Vector2(140f, 38f), 17);
            buy.name = "BuyButton";
            buy.GetComponentInChildren<Text>().name = "BuyLabel";
            var shopLock = UIFactory.CreateButton(root, "锁定", null,
                new Vector2(87f, -112f), new Vector2(65f, 38f), 14);
            shopLock.name = "LockButton";
            shopLock.GetComponentInChildren<Text>().name = "LockLabel";
            return root;
        }

        private static void AnchorRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
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

        private static void SetArray(Object target, string field, RectTransform[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedProperties();
        }
    }
}
