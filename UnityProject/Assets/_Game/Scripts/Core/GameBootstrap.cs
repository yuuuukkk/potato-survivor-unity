using RogueLike.Combat;
using RogueLike.Data;
using RogueLike.Enemies;
using RogueLike.Items;
using RogueLike.Player;
using RogueLike.UI;
using UnityEngine;

namespace RogueLike.Core
{
    /// <summary>
    /// 自举入口：Play 即自动搭建对象树。优先复用场景已摆放的对象
    /// （菜单"土豆幸存者/搭建场景结构"一键生成 GameManager/相机/玩家），只补齐缺失部分。
    /// 可调字段：竞技场尺寸 / 相机 / 跳过主菜单 / 起始波次 / 默认角色；
    /// 场景有 GameConfig（挂在 GameManager 上）时以其为准（Inspector 集中配置）。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        [HideInInspector] public Vector2 arenaSize = new Vector2(24f, 13.5f);
        [HideInInspector] public float cameraSize = 5.2f;
        [HideInInspector] public float cameraFollowSmooth = 8f;
        [HideInInspector] public Color cameraBackgroundColor = new Color(0.055f, 0.05f, 0.08f, 1f);
        [HideInInspector] public bool skipMenu = false;
        [HideInInspector] public int startWave = 1;
        [HideInInspector] public string defaultCharacterId = "farmer";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (Instance != null) return;
            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            try
            {
                Init();
            }
            catch (System.Exception e)
            {
                // 任何启动异常都显式打到 Console，避免"黑屏/卡住但无提示"
                Debug.LogError("[GameBootstrap] 启动失败，请把此错误发给我排查：\n" + e);
            }
        }

        private void Init()
        {
            GameDatabase.Load();
            var balance = GameDatabase.Balance;
            QualitySettings.vSyncCount = balance != null ? Mathf.Clamp(balance.vSyncCount, 0, 4) : 0;
            if (Application.isMobilePlatform)
            {
                // Unity's mobile default (-1) is usually 30 FPS for battery savings.
                // Target the device's native refresh rate instead; actual FPS remains
                // bounded by display refresh, thermal limits, and rendering performance.
                int refreshRate = Screen.currentResolution.refreshRate;
                Application.targetFrameRate = refreshRate > 0 ? refreshRate : 60;
            }
            else
            {
                Application.targetFrameRate = balance != null ? balance.targetFrameRate : -1;
            }

            // 场景已摆放 GameManager（含 GameConfig）则复用；否则运行时创建
            var gm = FindObjectOfType<GameManager>();
            if (gm == null)
            {
                var gmGo = new GameObject("GameManager");
                gmGo.AddComponent<GameManager>();
            }

            // 场景有 GameConfig 时，以 Inspector 集中配置覆盖本组件字段
            ApplyGameConfig();

            ArenaBounds.Size = arenaSize;

            BuildCamera();
            BuildArena();
            BuildCanvases();
            BuildPlayer();
            BuildSystems();

            if (skipMenu)
            {
                var c = GameDatabase.Characters.Find(ch => ch.id == defaultCharacterId);
                if (c == null) c = GameDatabase.GetCharacter(0);
                GameManager.Instance.StartRun(c);
                if (startWave > 1) GameManager.Instance.Director.StartWave(startWave); // 调试：从指定波开始
            }
            else
            {
                GameManager.Instance.SetState(GameState.MainMenu);
                // MainMenuUI 由状态事件驱动显隐，此处确保初始可见
                if (MainMenuUI.Instance != null) MainMenuUI.Instance.Show();
                else Debug.LogWarning("[GameBootstrap] MainMenuUI 未创建，请检查 UICanvas 预制体。 ");
            }
        }

        /// <summary>场景有 GameConfig（挂在 GameManager 上，Inspector 集中配置）时覆盖本组件可调字段。</summary>
        private void ApplyGameConfig()
        {
            var cfg = GameConfig.Instance != null ? GameConfig.Instance.Settings : Resources.Load<GameSettingsSO>("Config/GameSettings");
            if (cfg == null) return;
            arenaSize = cfg.arenaSize;
            cameraSize = cfg.cameraSize;
            cameraFollowSmooth = cfg.cameraFollowSmooth;
            cameraBackgroundColor = cfg.cameraBackgroundColor;
            skipMenu = cfg.skipMenu;
            startWave = cfg.startWave;
            defaultCharacterId = cfg.defaultCharacterId;
        }

        private void BuildCamera()
        {
            // 场景已摆放主相机（tag=MainCamera）则复用，否则创建；多余相机（残留透视相机等）清理
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                // 必须放在世界平面前方（z=-10）：相机若在 (0,0,0) 会与 z=0 的物体完全重合，
                // 全部落在近裁剪面内被裁掉，导致 Game 视图只见 UI 不见世界。
                go.transform.position = new Vector3(0f, 0f, -10f);
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            // 无论相机来自场景还是运行时，都使用固定俯视和纯色场外背景，
            // 避免场景相机残留的透视/天空盒在竞技场边缘露出来。
            cam.orthographic = true;
            cam.orthographicSize = cameraSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cameraBackgroundColor;
            if (cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();
            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.smooth = cameraFollowSmooth;
        }

        private void BuildArena()
        {
            // 复用场景结构，只补齐缺失对象/素材；手工位置、缩放和 Sprite 优先。
            var root = GameObject.Find("Arena");
            bool sceneAuthored = root != null;
            if (root == null) root = new GameObject("Arena");

            var bgTransform = root.transform.Find("Background");
            var bg = bgTransform != null ? bgTransform.gameObject : null;
            bool createdBg = bg == null;
            if (bg == null) bg = PrefabProvider.Instantiate("Shared/arena_bg", root.transform);
            if (bg == null)
            {
                bg = new GameObject("Background");
                bg.transform.SetParent(root.transform);
            }

            var sr = bg.GetComponent<SpriteRenderer>();
            if (sr == null) sr = bg.AddComponent<SpriteRenderer>();
            var configuredMap = AssetLoader.LoadMapSprite();
            if (sr.sprite == null && configuredMap != null) sr.sprite = configuredMap;
            if (sr.sprite == null)
            {
                sr.sprite = SpriteFactory.Square(arenaSize.x, arenaSize.y);
                sr.color = new Color(0.10f, 0.10f, 0.16f);
            }
            else if (!sceneAuthored || createdBg)
            {
                // 只自动适配运行时创建的背景；场景中的背景尺寸由 Inspector 决定。
                var b = sr.sprite.bounds;
                float baseW = arenaSize.x;
                float scale = baseW / Mathf.Max(b.size.x, 0.001f);
                bg.transform.localScale = new Vector3(scale, scale, 1f);
                arenaSize = new Vector2(baseW, b.size.y * scale);
            }
            sr.sortingOrder = -10;

            float t = 0.08f;
            var cfg = new (Vector2 size, Vector2 pos)[]
            {
                (new Vector2(arenaSize.x + t, t), new Vector2(0f, arenaSize.y * 0.5f)),
                (new Vector2(arenaSize.x + t, t), new Vector2(0f, -arenaSize.y * 0.5f)),
                (new Vector2(t, arenaSize.y + t), new Vector2(arenaSize.x * 0.5f, 0f)),
                (new Vector2(t, arenaSize.y + t), new Vector2(-arenaSize.x * 0.5f, 0f))
            };
            for (int i = 0; i < cfg.Length; i++)
            {
                var lineTransform = root.transform.Find("Border" + i);
                var line = lineTransform != null ? lineTransform.gameObject : null;
                bool createdLine = line == null;
                if (line == null) line = PrefabProvider.Instantiate("Shared/arena_border", root.transform);
                if (line == null)
                {
                    line = new GameObject("Border" + i);
                    line.transform.SetParent(root.transform);
                }
                line.name = "Border" + i;
                var lr = line.GetComponent<SpriteRenderer>();
                if (lr == null) lr = line.AddComponent<SpriteRenderer>();
                if (lr.sprite == null)
                {
                    lr.sprite = SpriteFactory.Square(cfg[i].size.x, cfg[i].size.y);
                    lr.color = new Color(0.32f, 0.30f, 0.42f);
                }
                lr.sortingOrder = -9;
                if (createdLine) line.transform.position = cfg[i].pos;
            }
            ArenaBounds.Size = arenaSize;
        }

        private void BuildCanvases()
        {
            UIFactory.CreateEventSystem();

            // 场景中的 UICanvas 优先：用户可以直接在场景里拖素材、调布局。
            // 场景未放置时才实例化 Prefab；两者都没有才用代码兜底。
            var sceneCanvas = GameObject.Find("UICanvas");
            if (sceneCanvas != null && sceneCanvas.GetComponent<Canvas>() != null)
            {
                UIFactory.MainCanvas = sceneCanvas.GetComponent<Canvas>();
            }
            else
            {
                var uiPrefab = Resources.Load<GameObject>("Prefabs/UI/UICanvas");
                if (uiPrefab != null)
                {
                    var inst = Instantiate(uiPrefab);
                    inst.name = "UICanvas";
                    UIFactory.MainCanvas = inst.GetComponent<Canvas>();
                }
                else
                {
                    var uiCanvas = UIFactory.CreateCanvas("UICanvas");
                    UIFactory.MainCanvas = uiCanvas;
                    var canvasGo = uiCanvas.gameObject;
                    canvasGo.AddComponent<HUDController>();
                    canvasGo.AddComponent<MainMenuUI>();
                    canvasGo.AddComponent<ShopUI>();
                    canvasGo.AddComponent<GameOverUI>();
                    canvasGo.AddComponent<InventoryBarUI>();
                    canvasGo.AddComponent<LevelUpUI>();
                    canvasGo.AddComponent<TooltipUI>();
                    canvasGo.AddComponent<StatsPanelUI>();
                }
            }

            if (UIFactory.MainCanvas != null && UIFactory.MainCanvas.GetComponent<UIResponsiveScaler>() == null)
                UIFactory.MainCanvas.gameObject.AddComponent<UIResponsiveScaler>();
            UIFactory.ApplyThemeToExistingUI(UIFactory.MainCanvas);
            if (UIFactory.MainCanvas != null && UIFactory.MainCanvas.GetComponent<MobileControlsUI>() == null)
                UIFactory.MainCanvas.gameObject.AddComponent<MobileControlsUI>();

            // 伤害数字画布（Overlay，跟随屏幕坐标）
            var damageGo = GameObject.Find("DamageCanvas");
            var dmgCanvas = damageGo != null ? damageGo.GetComponent<Canvas>() : null;
            if (dmgCanvas == null) dmgCanvas = UIFactory.CreateCanvas("DamageCanvas");
            DamageNumber.Canvas = dmgCanvas;
            GamePools.Prewarm("dmg", DamageNumber.CreateGo, 8);
        }

        private void BuildPlayer()
        {
            // 场景已摆放 Player（菜单"土豆幸存者/搭建场景结构"）则复用；否则优先用预制，无预制再运行时构建。
            var go = FindObjectOfType<PlayerController>()?.gameObject;
            if (go == null) go = GameObject.Find("Player");
            if (go == null)
            {
                go = PrefabProvider.Instantiate("Shared/player", null);
                if (go == null)
                {
                    go = new GameObject("Player");
                    var sr = go.AddComponent<SpriteRenderer>();
                    if (sr.sprite == null) sr.sprite = SpriteFactory.Circle(new Color(0.91f, 0.78f, 0.48f), 0.45f);
                    sr.sortingOrder = 5;
                    var col = go.AddComponent<CircleCollider2D>();
                    col.isTrigger = true;
                    col.radius = 0.45f;
                    var rb = go.AddComponent<Rigidbody2D>();
                    rb.gravityScale = 0f;
                    rb.isKinematic = true;
                }
            }

            // 补缺组件（预制/场景对象里可少挂，代码保证完整）
            if (go.GetComponent<SpriteRenderer>() == null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                if (sr.sprite == null) sr.sprite = SpriteFactory.Circle(new Color(0.91f, 0.78f, 0.48f), 0.45f);
            }
            else if (go.GetComponent<SpriteRenderer>().sprite == null)
                go.GetComponent<SpriteRenderer>().sprite = SpriteFactory.Circle(new Color(0.91f, 0.78f, 0.48f), 0.45f);
            if (go.GetComponent<CircleCollider2D>() == null)
            {
                var c = go.AddComponent<CircleCollider2D>();
                c.isTrigger = true;
                c.radius = 0.45f;
            }
            if (go.GetComponent<Rigidbody2D>() == null)
            {
                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.isKinematic = true;
            }
            foreach (var t in new System.Type[] { typeof(PlayerStats), typeof(PlayerHealth), typeof(WeaponSystem), typeof(Inventory), typeof(PlayerController), typeof(XpSystem), typeof(WeaponVisuals) })
            {
                if (go.GetComponent(t) == null) go.AddComponent(t);
            }
            GameManager.Instance.Player = go.GetComponent<PlayerController>();
        }

        private void BuildSystems()
        {
            var director = FindObjectOfType<WaveDirector>();
            if (director == null)
            {
                var dir = new GameObject("WaveDirector");
                director = dir.AddComponent<WaveDirector>();
            }
            GameManager.Instance.Director = director;

            var shopSystem = FindObjectOfType<ShopSystem>();
            if (shopSystem == null)
            {
                var shop = new GameObject("ShopSystem");
                shopSystem = shop.AddComponent<ShopSystem>();
            }
            GameManager.Instance.Shop = shopSystem;
            if (GetComponent<GMPanel>() == null) gameObject.AddComponent<GMPanel>();
        }
    }
}
