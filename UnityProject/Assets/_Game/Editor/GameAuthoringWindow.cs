using RogueLike.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueLike.EditorTools
{
    /// <summary>把常用编辑入口集中到一个窗口，避免记路径和依赖运行时生成。</summary>
    public class GameAuthoringWindow : EditorWindow
    {
        private const string ArtLibraryPath = "Assets/_Game/Resources/Data/GameArtLibrary.asset";
        private const string UiPrefabPath = "Assets/_Game/Resources/Prefabs/UI/UICanvas.prefab";
        private const string ScenePath = "Assets/_Game/scene/rouge.unity";
        private const string GameSettingsPath = "Assets/_Game/Resources/Config/GameSettings.asset";
        private const string GameBalancePath = "Assets/_Game/Resources/Config/GameBalance.asset";
        private const string UiThemePath = "Assets/_Game/Resources/Config/UITheme.asset";
        private const string CharacterRosterPath = "Assets/_Game/Resources/Config/CharacterRoster.asset";
        private Editor _libraryEditor;

        [MenuItem("土豆幸存者/打开可视化编辑中心", priority = 0)]
        public static void Open() => GetWindow<GameAuthoringWindow>("土豆编辑中心");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("土豆幸存者 · 可视化编辑中心", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("场景对象、玩家动画、美术、UI 和数值都可以在 Unity 窗口里编辑。先搭建场景结构，再从 Hierarchy 选 Player/PlayerArt 用 Animation 窗口制作动画。代码负责玩法和触发时机。", MessageType.Info);

            EditorGUILayout.Space();
            if (GUILayout.Button("搭建/补齐场景可编辑对象")) SceneBuilderMenu.BuildSceneStructure();
            if (GUILayout.Button("打开主场景")) EditorSceneManager.OpenScene(ScenePath);
            if (GUILayout.Button("选中场景中的 PlayerArt")) SelectSceneObject("Player/PlayerArt");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("可视化配置入口", EditorStyles.boldLabel);
            DrawAssetButton("游戏设置（镜头、地图、启动选项）", GameSettingsPath);
            DrawAssetButton("游戏平衡（移动、动画反馈、波次参数）", GameBalancePath);
            DrawAssetButton("UI 主题（颜色、字体、按钮样式）", UiThemePath);
            DrawAssetButton("角色列表", CharacterRosterPath);

            var library = AssetDatabase.LoadAssetAtPath<GameArtLibrary>(ArtLibraryPath);
            if (library == null)
            {
                EditorGUILayout.HelpBox("GameArtLibrary 丢失，请重新创建。", MessageType.Error);
                if (GUILayout.Button("创建美术配置")) CreateLibrary();
                return;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("在 Project 中定位美术配置")) Select(library);
            if (GUILayout.Button("打开 UI Prefab"))
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabPath));
            if (GUILayout.Button("定位敌人 Prefab 文件夹"))
                Select(AssetDatabase.LoadAssetAtPath<Object>("Assets/_Game/Resources/Prefabs/EnemyPool"));
            if (GUILayout.Button("定位武器弹体 Prefab 文件夹"))
                Select(AssetDatabase.LoadAssetAtPath<Object>("Assets/_Game/Resources/Prefabs/WeaponPool"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("美术配置（直接拖 Sprite）", EditorStyles.boldLabel);
            if (_libraryEditor == null || _libraryEditor.target != library)
                _libraryEditor = Editor.CreateEditor(library);
            _libraryEditor.OnInspectorGUI();
        }

        private static void DrawAssetButton(string label, string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label);
                if (GUILayout.Button("打开", GUILayout.Width(54f)))
                {
                    if (asset != null) AssetDatabase.OpenAsset(asset);
                    else Debug.LogWarning("找不到配置资源：" + path);
                }
            }
        }

        private static void SelectSceneObject(string hierarchyPath)
        {
            var sceneObject = GameObject.Find(hierarchyPath);
            if (sceneObject == null)
            {
                EditorUtility.DisplayDialog("找不到对象", "请先点“搭建/补齐场景可编辑对象”，再试一次。", "好");
                return;
            }
            Selection.activeGameObject = sceneObject;
            EditorGUIUtility.PingObject(sceneObject);
        }

        private static void Select(Object obj)
        {
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        private static void CreateLibrary()
        {
            var library = CreateInstance<GameArtLibrary>();
            AssetDatabase.CreateAsset(library, ArtLibraryPath);
            AssetDatabase.SaveAssets();
            Select(library);
        }
    }
}
