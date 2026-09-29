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
        private Editor _libraryEditor;

        [MenuItem("土豆幸存者/打开可视化编辑中心", priority = 0)]
        public static void Open() => GetWindow<GameAuthoringWindow>("土豆编辑中心");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("土豆幸存者 · 可视化编辑中心", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("图片统一拖到下方美术配置；对象尺寸、碰撞和层级编辑对应 Prefab；UI 布局编辑 UICanvas Prefab。运行时代码只在资源为空时生成简单占位。", MessageType.Info);

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
            if (GUILayout.Button("打开主场景")) EditorSceneManager.OpenScene(ScenePath);
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
