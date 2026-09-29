using RogueLike.Core;
using UnityEditor;
using UnityEngine;

namespace RogueLike.EditorTools
{
    [InitializeOnLoad]
    public static class UIThemeAssetBinder
    {
        static UIThemeAssetBinder() => EditorApplication.delayCall += Bind;

        [MenuItem("土豆幸存者/绑定统一 UI 素材")]
        public static void Bind()
        {
            const string root = "Assets/_Game/Resources/Art/UI/";
            var panel = AssetDatabase.LoadAssetAtPath<Sprite>(root + "ui_panel_round.png");
            var button = AssetDatabase.LoadAssetAtPath<Sprite>(root + "ui_button_round.png");
            var theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>("Assets/_Game/Resources/Config/UITheme.asset");
            if (theme == null || panel == null || button == null) return;
            bool changed = false;
            if (theme.roundedPanel == null) { theme.roundedPanel = panel; changed = true; }
            if (theme.roundedButton == null) { theme.roundedButton = button; changed = true; }
            if (theme.roundedCard == null) { theme.roundedCard = panel; changed = true; }
            if (!changed) return;
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("[UI] 已绑定统一圆角面板、卡片和按钮图。 ");
        }
    }
}
