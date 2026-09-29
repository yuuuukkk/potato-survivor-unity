using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Player;
using RogueLike.Enemies;
using RogueLike.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueLike.EditorTools
{
    /// <summary>
    /// 一键把固定对象搭进当前场景（GameManager+GameConfig / Main Camera / Player）。
    /// 用途：Hierarchy 可视化摆放 + Inspector 直改配置（GameConfig 面板集中数值），
    /// 不再全部由 GameBootstrap 运行时凭空生成。
    /// 菜单：土豆幸存者 / 搭建场景结构
    /// </summary>
    public static class SceneBuilderMenu
    {
        [MenuItem("土豆幸存者/搭建场景结构")]
        public static void BuildSceneStructure()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("搭建场景结构",
                    "请先打开游戏场景（Assets/_Game/scene/rouge.unity）再执行。", "好");
                return;
            }

            int created = 0;

            // 1. GameManager + GameConfig（集中配置面板）
            var existingManager = Object.FindObjectOfType<GameManager>();
            if (existingManager == null)
            {
                var gmGo = new GameObject("GameManager");
                gmGo.AddComponent<GameManager>();
                var cfg = gmGo.AddComponent<GameConfig>();
                cfg.settings = AssetDatabase.LoadAssetAtPath<GameSettingsSO>("Assets/_Game/Resources/Config/GameSettings.asset");
                Undo.RegisterCreatedObjectUndo(gmGo, "搭建场景结构");
                created++;
                Debug.Log("[SceneBuilder] 已创建 GameManager（含 GameConfig 配置面板）。");
            }
            else if (existingManager.GetComponent<GameConfig>() == null)
            {
                existingManager.gameObject.AddComponent<GameConfig>();
                created++;
            }

            // 2. Main Camera（正交 2D + 跟随 + 震动）
            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.transform.position = new Vector3(0f, 0f, -10f);
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5.2f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.06f, 0.10f);
                camGo.AddComponent<AudioListener>();
                camGo.AddComponent<CameraShake>();
                var follow = camGo.AddComponent<CameraFollow>();
                follow.smooth = 8f;
                Undo.RegisterCreatedObjectUndo(camGo, "搭建场景结构");
                created++;
                Debug.Log("[SceneBuilder] 已创建 Main Camera（size=5.2 + 跟随 + 震动）。");
            }

            // 3. Player（占位，玩家素材走 Shared/player 预制）
            if (Object.FindObjectOfType<PlayerController>() == null && GameObject.Find("Player") == null)
            {
                var playerGo = new GameObject("Player");
                var sr = playerGo.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 5;
                var col = playerGo.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.45f;
                var rb = playerGo.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.isKinematic = true;
                Undo.RegisterCreatedObjectUndo(playerGo, "搭建场景结构");
                created++;
                Debug.Log("[SceneBuilder] 已创建 Player 占位（正式素材用 Shared/player 预制替换）。");
            }

            // 4. UI 直接放进场景，之后可在 Hierarchy 中所见即所得地修改。
            if (GameObject.Find("UICanvas") == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Resources/Prefabs/UI/UICanvas.prefab");
                if (prefab != null)
                {
                    var ui = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    ui.name = "UICanvas";
                    Undo.RegisterCreatedObjectUndo(ui, "搭建可编辑 UI");
                    created++;
                }
            }

            // 5. Arena 根对象放进场景。背景和边界均为 Prefab 实例，可直接拖 Sprite、调尺寸和位置。
            if (GameObject.Find("Arena") == null)
            {
                var arena = new GameObject("Arena");
                Undo.RegisterCreatedObjectUndo(arena, "搭建可编辑竞技场");
                CreatePrefabChild("Assets/_Game/Resources/Prefabs/Shared/arena_bg.prefab", arena.transform, "Background");
                var config = Resources.Load<GameSettingsSO>("Config/GameSettings");
                Vector2 size = config != null ? config.arenaSize : new Vector2(24f, 13.5f);
                var borderPositions = new[]
                {
                    new Vector2(0f, size.y * 0.5f),
                    new Vector2(0f, -size.y * 0.5f),
                    new Vector2(size.x * 0.5f, 0f),
                    new Vector2(-size.x * 0.5f, 0f)
                };
                for (int i = 0; i < 4; i++)
                {
                    var border = CreatePrefabChild("Assets/_Game/Resources/Prefabs/Shared/arena_border.prefab", arena.transform, "Border" + i);
                    if (border != null) border.transform.localPosition = borderPositions[i];
                }
                created++;
            }

            // 6. 运行系统显式放在 Hierarchy，方便检查和挂调试组件。
            if (Object.FindObjectOfType<WaveDirector>() == null)
            {
                var go = new GameObject("WaveDirector");
                go.AddComponent<WaveDirector>();
                Undo.RegisterCreatedObjectUndo(go, "搭建波次系统");
                created++;
            }
            if (Object.FindObjectOfType<ShopSystem>() == null)
            {
                var go = new GameObject("ShopSystem");
                go.AddComponent<ShopSystem>();
                Undo.RegisterCreatedObjectUndo(go, "搭建商店系统");
                created++;
            }

            if (created > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            EditorUtility.DisplayDialog("搭建场景结构",
                created == 0
                    ? "场景里已经有 GameManager / 相机 / Player 了，没有重复创建。"
                    : $"已创建 {created} 个对象并保存场景。\n\n" +
                      "现在 Hierarchy 里能看到 GameManager / Main Camera / Player / Arena / UICanvas / 运行系统；\n" +
                      "选中 GameManager，在 Inspector 的 GameConfig 面板直接改数值。\n" +
                      "Play 时 GameBootstrap 会复用这些对象，不会销毁并重建你的布局。",
                "好");
        }

        private static GameObject CreatePrefabChild(string path, Transform parent, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            go.name = name;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "搭建可编辑对象");
            return go;
        }
    }
}
