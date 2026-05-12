using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EndlessEngine.Bootstrap;
using EndlessEngine.Prestige;
using EndlessEngine.Economy;
using EndlessEngine.Building;
using EndlessEngine.Generator;
using EndlessEngine.Research;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Programmatic scene builder for every Endless Engine game type.
    ///
    /// Each game type gets a tailored play-ready scene:
    ///
    ///   PureIdle / ResearchIdle / BuildingIdle / PrestigeHeavy
    ///     Bootstrap + HUD (gold, income, generators, prestige)
    ///
    ///   ClickerIdle (simple)
    ///     Bootstrap + orange click sphere + ClickTargetHandler fallback
    ///
    ///   ClickLoop (full click loop system)
    ///     Bootstrap + ClickLoopBootstrap + 3 ClickTarget world objects
    ///
    ///   HarvestIdle
    ///     Bootstrap + HarvestLoopBootstrap + 5 green harvest nodes (circles)
    ///     with HarvestNode + CircleCollider2D for cursor overlap detection
    ///
    ///   IdleVsRPG
    ///     Bootstrap + WaveCombatBootstrap + red enemy prefab wired to WaveSpawnManager
    ///
    ///   MergeIdle
    ///     Bootstrap + 3×3 merge board with 2 seed items
    ///
    ///   FarmIdle
    ///     Bootstrap + 6 farm plot placeholders (building slots) in a grid
    ///
    ///   TowerDefense
    ///     Bootstrap + WaveCombatBootstrap + 3 tower slot placeholders + path visual
    ///
    /// All scenes: CanvasScaler 1080×1920, GeneratedGameHUD auto-wired.
    /// </summary>
    public static class SceneSetupUtility
    {
        // ── Game type enum ────────────────────────────────────────────────────────

        public enum GameType
        {
            PureIdle        = 0,
            ClickerIdle     = 1,
            ClickLoop       = 2,
            HarvestIdle     = 3,
            IdleVsRPG       = 4,
            TowerDefense    = 5,
            MergeIdle       = 6,
            FarmIdle        = 7,
            ResearchIdle    = 8,
            BuildingIdle    = 9,
            PrestigeHeavy   = 10,
            Custom          = 11,
        }

        public struct SetupOptions
        {
            public string   GameName;
            public string   ScenesPath;
            public string   ConfigsPath;
            public string   UpgradeTreePath;
            public GameType Type;
            public bool     HasGenerator;
            public bool     HasPrestige;
            public bool     HasMultiCurrency;
            public bool     HasWave;
            public bool     HasClick;
            public bool     HasCursor;
            public bool     HasZone;
            public bool     HasHarvest;
            public bool     HasClickLoop;
            public bool     HasBuilding;
        }

        // ── Entry point ───────────────────────────────────────────────────────────

        public static bool BuildScene(SetupOptions opts)
        {
            string scenePath = $"{opts.ScenesPath}/{opts.GameName}.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);

            // ── Camera ───────────────────────────────────────────────────────────
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam   = camGO.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 5;
            cam.backgroundColor  = GetSkyColor(opts.Type);
            cam.clearFlags       = CameraClearFlags.SolidColor;
            camGO.transform.position = new Vector3(0, 0, -10);

            // ── EventSystem ──────────────────────────────────────────────────────
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();

            // ── Bootstrap ────────────────────────────────────────────────────────
            var bootstrapGO = new GameObject("Bootstrap");
            var bootstrap   = bootstrapGO.AddComponent<AutoSetupBootstrap>();
            WireBootstrapConfigs(bootstrap, opts);

            // ── Game-type world ──────────────────────────────────────────────────
            switch (opts.Type)
            {
                case GameType.ClickerIdle:
                    BuildSimpleClickTarget(bootstrapGO);
                    break;

                case GameType.ClickLoop:
                    BuildClickLoopWorld(bootstrapGO, opts);
                    break;

                case GameType.HarvestIdle:
                    BuildHarvestWorld(bootstrapGO, opts);
                    break;

                case GameType.IdleVsRPG:
                    BuildCombatArena(bootstrapGO, opts);
                    break;

                case GameType.TowerDefense:
                    BuildTowerDefenseLayout(bootstrapGO, opts);
                    break;

                case GameType.MergeIdle:
                    BuildMergeBoard();
                    break;

                case GameType.FarmIdle:
                    BuildFarmLayout(bootstrapGO, opts);
                    break;

                case GameType.BuildingIdle:
                    BuildBuildingGrid();
                    break;
            }

            // ── Optional services (Prestige, Building, Research, Merge) ─────────────
            WireOptionalServices(bootstrapGO, opts);

            // ── HUD (all types) ──────────────────────────────────────────────────
            BuildHUD(opts, bootstrapGO);

            // ── Save ─────────────────────────────────────────────────────────────
            EnsureDirectory(opts.ScenesPath);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.CloseScene(scene, removeScene: false);
            AddSceneToBuildSettings(scenePath);

            Debug.Log($"[SceneSetupUtility] Scene created: {scenePath}");
            return true;
        }

        // ── Optional service wiring ───────────────────────────────────────────────

        private static void WireOptionalServices(GameObject bootstrapGO, SetupOptions opts)
        {
            bool needPrestige = opts.HasPrestige
                || opts.Type == GameType.PrestigeHeavy;

            bool needBuilding = opts.HasBuilding
                || opts.Type == GameType.BuildingIdle
                || opts.Type == GameType.FarmIdle;

            bool needResearch = opts.Type == GameType.ResearchIdle;

            bool needMerge    = opts.Type == GameType.MergeIdle;

            // ── Prestige ──────────────────────────────────────────────────────────
            if (needPrestige)
            {
                bootstrapGO.AddComponent<PrestigeStateManager>();
                bootstrapGO.AddComponent<PrestigeBootstrap>();
            }

            // ── BuildingService ───────────────────────────────────────────────────
            if (needBuilding)
            {
                // Service on child GO; bootstrap on parent (same GO as AutoSetupBootstrap)
                var bsGO = new GameObject("BuildingService");
                bsGO.transform.SetParent(bootstrapGO.transform, false);
                bsGO.AddComponent<BuildingService>();

                var bb = bootstrapGO.AddComponent<BuildingBootstrap>();
                var bbSO = new SerializedObject(bb);
                var startingCfg = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.BuildingConfigSO>(
                    $"{opts.ConfigsPath}/StarterBuilding.asset");
                SetSORef(bbSO, "_starterConfig", startingCfg);
                bbSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── ResearchService ───────────────────────────────────────────────────
            if (needResearch)
            {
                var rsGO = new GameObject("ResearchService");
                rsGO.transform.SetParent(bootstrapGO.transform, false);
                rsGO.AddComponent<ResearchService>();

                var rb = bootstrapGO.AddComponent<ResearchBootstrap>();
                var rbSO = new SerializedObject(rb);
                var resDb = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.ResearchTreeConfigSO>(
                    $"{opts.ConfigsPath}/ResearchDatabase.asset");
                SetSORef(rbSO, "_researchTree", resDb);
                rbSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── MergeService + InventoryService ───────────────────────────────────
            if (needMerge)
            {
                var invGO = new GameObject("InventoryService");
                invGO.transform.SetParent(bootstrapGO.transform, false);
                invGO.AddComponent<InventoryService>();

                var msGO = new GameObject("MergeService");
                msGO.transform.SetParent(bootstrapGO.transform, false);
                msGO.AddComponent<MergeService>();

                bootstrapGO.AddComponent<MergeBootstrap>();
            }
        }

        // ── Camera sky colors per type ────────────────────────────────────────────

        private static Color GetSkyColor(GameType t) => t switch
        {
            GameType.HarvestIdle   => new Color(0.08f, 0.13f, 0.06f),  // dark forest green
            GameType.ClickLoop     => new Color(0.06f, 0.06f, 0.14f),  // dark blue
            GameType.IdleVsRPG    => new Color(0.10f, 0.03f, 0.03f),  // dark red
            GameType.TowerDefense => new Color(0.04f, 0.10f, 0.04f),  // military green
            GameType.FarmIdle     => new Color(0.10f, 0.14f, 0.06f),  // field green
            GameType.MergeIdle    => new Color(0.08f, 0.05f, 0.14f),  // purple
            _                     => new Color(0.08f, 0.08f, 0.10f),  // default dark
        };

        // ── Bootstrap config wiring ───────────────────────────────────────────────

        private static void WireBootstrapConfigs(AutoSetupBootstrap bootstrap, SetupOptions opts)
        {
            var econConfig = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.EconomyConfigSO>(
                $"{opts.ConfigsPath}/EconomyConfig.asset");
            var genDb = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.GeneratorDatabaseSO>(
                $"{opts.ConfigsPath}/GeneratorDatabase.asset");
            var schema = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.SchemaVersionSO>(
                $"{opts.ConfigsPath}/SchemaVersion.asset");
            var prestige = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.PrestigeConfigSO>(
                $"{opts.ConfigsPath}/PrestigeConfig.asset");

            var so = new SerializedObject(bootstrap);
            SetSORef(so, "_economyConfig",    econConfig);
            SetSORef(so, "_generatorDatabase", genDb);
            SetSORef(so, "_schemaVersion",    schema);
            SetSORef(so, "_prestigeConfig",   prestige);
            SetBool(so, "_enableSave",        true);

            // Load UpgradeNodeConfigSO assets from <ConfigsPath>/Upgrades/ for UpgradeTreeService
            if (!string.IsNullOrEmpty(opts.UpgradeTreePath))
            {
                string upgradesDir = System.IO.Path.GetDirectoryName(
                    opts.UpgradeTreePath.Replace('/', System.IO.Path.DirectorySeparatorChar))
                    .Replace(System.IO.Path.DirectorySeparatorChar, '/') + "/Upgrades";

                string[] guids = AssetDatabase.FindAssets("t:UpgradeNodeConfigSO", new[] { upgradesDir });
                if (guids.Length > 0)
                {
                    var upgradesProp = so.FindProperty("_upgradeNodeConfigs");
                    if (upgradesProp != null)
                    {
                        upgradesProp.arraySize = guids.Length;
                        for (int i = 0; i < guids.Length; i++)
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                            var node = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.UpgradeNodeConfigSO>(assetPath);
                            upgradesProp.GetArrayElementAtIndex(i).objectReferenceValue = node;
                        }
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── ClickerIdle: simple one-shot click sphere ─────────────────────────────

        private static void BuildSimpleClickTarget(GameObject bootstrapGO)
        {
            var t = new GameObject("ClickTarget");
            t.transform.position = new Vector3(0, 0.3f, 0);
            var sr = t.AddComponent<SpriteRenderer>();
            sr.sprite = MakeCircle(64, new Color(0.95f, 0.6f, 0.1f));
            sr.sortingOrder = 2;
            t.transform.localScale = Vector3.one * 1.6f;
            var col = t.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;
            t.AddComponent<ClickTargetHandler>();

            // Pulse ring
            var ring = new GameObject("Ring");
            ring.transform.SetParent(t.transform, false);
            var rsr = ring.AddComponent<SpriteRenderer>();
            rsr.sprite = MakeCircle(64, new Color(1f, 0.8f, 0.3f, 0.2f));
            rsr.sortingOrder = 1;
            ring.transform.localScale = Vector3.one * 1.3f;

            // Instruction label in world
            BuildWorldLabel(new Vector3(0, -1.5f, 0), "← Click me!", Color.white, 0.25f);
        }

        // ── ClickLoop: full ClickLoopService + ClickTarget world objects ──────────

        private static void BuildClickLoopWorld(GameObject bootstrapGO, SetupOptions opts)
        {
            // ClickLoopBootstrap component
            var clb = bootstrapGO.AddComponent<ClickLoopBootstrap>();
            var clbSO = new SerializedObject(clb);
            var clConfig = AssetDatabase.LoadAssetAtPath<EndlessEngine.ClickLoop.ClickLoopConfigSO>(
                $"{opts.ConfigsPath}/ClickLoopConfig.asset");
            SetSORef(clbSO, "_clickConfig", clConfig);
            // LayerMask -1 = Everything so OverlapPoint detects ClickTarget colliders on any layer
            var clLayerProp = clbSO.FindProperty("_clickTargetLayer");
            if (clLayerProp != null) clLayerProp.intValue = -1;
            clbSO.ApplyModifiedPropertiesWithoutUndo();

            // Background arena
            BuildArenaBackground(new Color(0.06f, 0.06f, 0.14f), new Color(0.1f, 0.1f, 0.25f));

            // 3 ClickTarget world objects in a triangle formation
            var positions = new Vector3[]
            {
                new Vector3(0f,    1.5f, 0),
                new Vector3(-2f,  -0.8f, 0),
                new Vector3( 2f,  -0.8f, 0),
            };
            var colors = new Color[]
            {
                new Color(0.9f, 0.2f, 0.2f),
                new Color(0.2f, 0.5f, 0.9f),
                new Color(0.2f, 0.9f, 0.4f),
            };
            var targetConfigs = new EndlessEngine.ClickLoop.ClickTargetConfigSO[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                string configPath = $"{opts.ConfigsPath}/ClickTarget_{i}.asset";
                targetConfigs[i] = AssetDatabase.LoadAssetAtPath<EndlessEngine.ClickLoop.ClickTargetConfigSO>(configPath);
                BuildClickTarget(positions[i], colors[i], targetConfigs[i], i);
            }

            BuildWorldLabel(new Vector3(0, -2.8f, 0), "Click the targets!", Color.white, 0.22f);
        }

        private static void BuildClickTarget(Vector3 pos, Color color,
            EndlessEngine.ClickLoop.ClickTargetConfigSO cfg, int colorIndex = 0)
        {
            // Try to use the package prefab (avoids temporary texture allocations)
            string[] prefabNames = { "ClickTarget_Red", "ClickTarget_Blue", "ClickTarget_Green" };
            string pkgName = colorIndex < prefabNames.Length ? prefabNames[colorIndex] : prefabNames[0];
            string pkgPath = $"Packages/com.endlessengine.idle/Runtime/Prefabs/ClickLoop/{pkgName}.prefab";
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(pkgPath);

            GameObject go;
            if (prefabAsset != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                go.transform.position = pos;
            }
            else
            {
                go = new GameObject("ClickTarget");
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 1.2f;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = MakeCircle(64, color);
                sr.sortingOrder = 2;

                var col = go.AddComponent<CircleCollider2D>();
                col.radius = 0.5f;
            }

            // Wire config to ClickTarget component (may already exist on prefab)
            if (cfg != null)
            {
                go.SetActive(false);
                var ct = go.GetComponent<EndlessEngine.ClickLoop.ClickTarget>()
                         ?? go.AddComponent<EndlessEngine.ClickLoop.ClickTarget>();
                var ctso = new SerializedObject(ct);
                SetSORef(ctso, "_config", cfg);
                ctso.ApplyModifiedPropertiesWithoutUndo();
                go.SetActive(true);
            }

            // Only add glow/HP bar if not instantiated from prefab (prefab already has them)
            if (prefabAsset == null)
            {
                var glow = new GameObject("Glow");
                glow.transform.SetParent(go.transform, false);
                var gsr = glow.AddComponent<SpriteRenderer>();
                gsr.sprite = MakeCircle(64, new Color(color.r, color.g, color.b, 0.18f));
                gsr.sortingOrder = 1;
                glow.transform.localScale = Vector3.one * 1.35f;

                BuildWorldHPBar(go, new Color(0f, 0.7f, 0f), new Vector3(0, 0.75f, 0), 1f);
            }
        }

        // ── HarvestIdle: harvest nodes in scene ───────────────────────────────────

        private static void BuildHarvestWorld(GameObject bootstrapGO, SetupOptions opts)
        {
            // HarvestLoopBootstrap
            var hlb = bootstrapGO.AddComponent<HarvestLoopBootstrap>();
            var hlbSO = new SerializedObject(hlb);
            var areaConfig = AssetDatabase.LoadAssetAtPath<EndlessEngine.Harvest.HarvestAreaConfigSO>(
                $"{opts.ConfigsPath}/HarvestAreaConfig.asset");
            SetSORef(hlbSO, "_areaConfig", areaConfig);
            // LayerMask -1 = Everything so HarvestCursor detects HarvestNode colliders on any layer
            var hlLayerProp = hlbSO.FindProperty("_harvestLayer");
            if (hlLayerProp != null) hlLayerProp.intValue = -1;
            hlbSO.ApplyModifiedPropertiesWithoutUndo();

            // Ground plane
            var ground = new GameObject("Ground");
            ground.transform.position = new Vector3(0, -3f, 1f);
            var gsr = ground.AddComponent<SpriteRenderer>();
            gsr.sprite = MakeRect(new Color(0.15f, 0.22f, 0.1f));
            gsr.sortingOrder = -2;
            ground.transform.localScale = new Vector3(12f, 2f, 1f);

            // 5 harvest nodes scattered in the scene
            var nodePositions = new Vector3[]
            {
                new Vector3(-3f,  0.5f, 0),
                new Vector3(-1.2f,-0.5f, 0),
                new Vector3( 1f,  1f,  0),
                new Vector3( 2.8f,-0.3f, 0),
                new Vector3( 0f, -1.2f, 0),
            };
            var nodeColors = new Color[]
            {
                new Color(0.2f, 0.7f, 0.2f),
                new Color(0.3f, 0.8f, 0.3f),
                new Color(0.15f, 0.65f, 0.15f),
                new Color(0.25f, 0.75f, 0.25f),
                new Color(0.2f, 0.8f, 0.4f),
            };

            // Load node config if available
            var nodeConfig = AssetDatabase.LoadAssetAtPath<EndlessEngine.Harvest.HarvestNodeConfigSO>(
                $"{opts.ConfigsPath}/HarvestNode.asset");

            for (int i = 0; i < nodePositions.Length; i++)
            {
                BuildHarvestNode(nodePositions[i], nodeColors[i], nodeConfig, i);
            }

            // Cursor radius visual hint
            BuildWorldLabel(new Vector3(0, 2.5f, 0), "Move cursor over green nodes", Color.white, 0.2f);
            BuildCursorRadiusVisual();
        }

        private static void BuildHarvestNode(Vector3 pos, Color color,
            EndlessEngine.Harvest.HarvestNodeConfigSO cfg, int index)
        {
            // Try to use the package prefab variant based on index
            string[] harvestPrefabs = { "HarvestNode_Green", "HarvestNode_Stone", "HarvestNode_Green",
                                        "HarvestNode_Golden", "HarvestNode_Green" };
            string pkgPrefabName = index < harvestPrefabs.Length ? harvestPrefabs[index] : "HarvestNode_Green";
            string pkgPath = $"Packages/com.endlessengine.idle/Runtime/Prefabs/Harvest/{pkgPrefabName}.prefab";
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(pkgPath);

            GameObject rootGO;
            if (prefabAsset != null)
            {
                rootGO = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                rootGO.name = $"HarvestNode_{index}";
                rootGO.transform.position = pos;
            }
            else
            {
                // Fallback: build procedurally — tree/rock like shape
                rootGO = new GameObject($"HarvestNode_{index}");
                rootGO.transform.position = pos;

                var body = new GameObject("Body");
                body.transform.SetParent(rootGO.transform, false);
                body.transform.localScale = Vector3.one * 1.1f;
                var bsr = body.AddComponent<SpriteRenderer>();
                bsr.sprite = MakeCircle(48, color);
                bsr.sortingOrder = 2;

                var crown = new GameObject("Crown");
                crown.transform.SetParent(rootGO.transform, false);
                crown.transform.localPosition = new Vector3(0, 0.55f, 0);
                crown.transform.localScale = Vector3.one * 0.7f;
                var csr = crown.AddComponent<SpriteRenderer>();
                csr.sprite = MakeCircle(48, new Color(color.r * 0.8f, color.g, color.b * 0.8f));
                csr.sortingOrder = 3;

                var col = rootGO.AddComponent<CircleCollider2D>();
                col.radius = 0.55f;
                col.isTrigger = true;
            }

            // Wire config to HarvestNode component (may already exist on prefab)
            if (cfg != null)
            {
                rootGO.SetActive(false);
                var hn = rootGO.GetComponent<EndlessEngine.Harvest.HarvestNode>()
                         ?? rootGO.AddComponent<EndlessEngine.Harvest.HarvestNode>();
                var hnso = new SerializedObject(hn);
                SetSORef(hnso, "_config", cfg);
                hnso.ApplyModifiedPropertiesWithoutUndo();
                rootGO.SetActive(true);
            }
            else if (prefabAsset == null)
            {
                rootGO.AddComponent<EndlessEngine.Harvest.HarvestNode>();
            }

            if (prefabAsset == null)
                BuildWorldHPBar(rootGO, new Color(0.2f, 0.8f, 0.2f), new Vector3(0, 0.9f, 0), 0.9f);
        }

        private static void BuildCursorRadiusVisual()
        {
            // A dotted circle that shows the harvest cursor radius
            var indicator = new GameObject("CursorRadiusIndicator");
            indicator.transform.position = new Vector3(0, 0, 0.5f);
            var sr = indicator.AddComponent<SpriteRenderer>();
            sr.sprite = MakeCircle(64, new Color(1f, 1f, 0.5f, 0.08f));
            sr.sortingOrder = 10;
            indicator.transform.localScale = Vector3.one * 3f;  // 1.5 * 2 (diameter)
        }

        // ── IdleVsRPG: combat arena ───────────────────────────────────────────────

        private static void BuildCombatArena(GameObject bootstrapGO, SetupOptions opts)
        {
            // Arena background
            BuildArenaBackground(new Color(0.10f, 0.03f, 0.03f), new Color(0.15f, 0.06f, 0.06f));

            // Enemy prefab (in disabled holder) — prefer package prefab
            var holder = new GameObject("EnemyPrefabHolder");
            holder.SetActive(false);

            const string enemyPkgPath = "Packages/com.endlessengine.idle/Runtime/Prefabs/Combat/Enemy_Default.prefab";
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPkgPath);

            GameObject enemy;
            if (enemyPrefab != null)
            {
                enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
                enemy.transform.SetParent(holder.transform, false);
            }
            else
            {
                enemy = new GameObject("Enemy");
                enemy.transform.SetParent(holder.transform, false);
                var esr = enemy.AddComponent<SpriteRenderer>();
                esr.sprite = MakeCircle(48, new Color(0.9f, 0.15f, 0.15f));
                esr.sortingOrder = 3;
                var rb = enemy.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0; rb.freezeRotation = true;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                enemy.AddComponent<CircleCollider2D>();
                BuildWorldHPBar(enemy, new Color(0.1f, 0.8f, 0.1f), new Vector3(0, 0.7f, 0), 0.8f);
            }

            // Player visual (blue diamond)
            var playerGO = new GameObject("Player");
            playerGO.transform.position = new Vector3(0, 0, 0);
            var psr = playerGO.AddComponent<SpriteRenderer>();
            psr.sprite = MakeCircle(48, new Color(0.2f, 0.4f, 0.9f));
            psr.sortingOrder = 2;
            playerGO.transform.localScale = Vector3.one * 0.9f;
            playerGO.AddComponent<CircleCollider2D>();

            // Combat services
            var waveSpawn  = bootstrapGO.AddComponent<EndlessEngine.Wave.WaveSpawnManager>();
            var enemyMgr   = bootstrapGO.AddComponent<EndlessEngine.Enemy.EnemyManager>();
            var autoBattle = bootstrapGO.AddComponent<EndlessEngine.Combat.AutoBattleController>();

            var wsso = new SerializedObject(waveSpawn);
            SetObjRef(wsso, "_enemyPrefab", enemy);
            wsso.ApplyModifiedPropertiesWithoutUndo();

            // WaveCombatBootstrap
            var wcb = bootstrapGO.AddComponent<WaveCombatBootstrap>();
            var waveConfig  = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.WaveConfigSO>(
                $"{opts.ConfigsPath}/WaveConfig.asset");
            var enemyConfig = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.EnemyStatConfigSO>(
                $"{opts.ConfigsPath}/EnemyStatConfig.asset");
            var wcbso = new SerializedObject(wcb);
            SetObjRef(wcbso, "_waveSpawnManager", waveSpawn);
            SetObjRef(wcbso, "_enemyManager",     enemyMgr);
            SetObjRef(wcbso, "_autoBattle",       autoBattle);
            SetSORef(wcbso,  "_waveConfig",       waveConfig);
            SetSORef(wcbso,  "_enemyConfig",      enemyConfig);
            wcbso.ApplyModifiedPropertiesWithoutUndo();

            BuildWorldLabel(new Vector3(0, -3.5f, 0), "Auto-battle active!", Color.white, 0.2f);
        }

        // ── TowerDefense: path + tower slots ─────────────────────────────────────

        private static void BuildTowerDefenseLayout(GameObject bootstrapGO, SetupOptions opts)
        {
            // Winding path of colored tiles
            var pathPositions = new Vector3[]
            {
                new Vector3(-4f, -1f, 0.5f), new Vector3(-2.5f, -1f, 0.5f),
                new Vector3(-1f, -1f, 0.5f), new Vector3(-1f,  0.5f, 0.5f),
                new Vector3( 0.5f, 0.5f, 0.5f), new Vector3(2f, 0.5f, 0.5f),
                new Vector3(3.5f, 0.5f, 0.5f), new Vector3(3.5f,-1.5f, 0.5f),
            };
            foreach (var pp in pathPositions)
            {
                var tile = new GameObject("PathTile");
                tile.transform.position = pp;
                var sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite = MakeRect(new Color(0.35f, 0.28f, 0.18f));
                sr.sortingOrder = -1;
                tile.transform.localScale = new Vector3(1.55f, 1.55f, 1f);
            }

            // Green grass background
            var grass = new GameObject("Grass");
            grass.transform.position = new Vector3(0, 0, 1f);
            var gsr = grass.AddComponent<SpriteRenderer>();
            gsr.sprite = MakeRect(new Color(0.12f, 0.22f, 0.08f));
            gsr.sortingOrder = -2;
            grass.transform.localScale = new Vector3(12f, 10f, 1f);

            // 3 tower slot placeholders — prefer package prefab
            const string towerSlotPkgPath =
                "Packages/com.endlessengine.idle/Runtime/Prefabs/TowerDefense/TowerSlot_Default.prefab";
            var towerSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(towerSlotPkgPath);

            var towerPositions = new Vector3[]
            {
                new Vector3(-3f,  1.5f, 0),
                new Vector3( 1.2f,-0.5f, 0),
                new Vector3( 2.5f, 2f,   0),
            };
            foreach (var tp in towerPositions)
            {
                GameObject slot;
                if (towerSlotPrefab != null)
                {
                    slot = (GameObject)PrefabUtility.InstantiatePrefab(towerSlotPrefab);
                    slot.transform.position = tp;
                }
                else
                {
                    slot = new GameObject("TowerSlot");
                    slot.transform.position = tp;
                    var tsSr = slot.AddComponent<SpriteRenderer>();
                    tsSr.sprite = MakeRect(new Color(0.3f, 0.25f, 0.1f));
                    tsSr.sortingOrder = 1;
                    slot.transform.localScale = Vector3.one * 0.9f;
                    slot.AddComponent<BoxCollider2D>();

                    var tower = new GameObject("Tower");
                    tower.transform.SetParent(slot.transform, false);
                    tower.transform.localPosition = Vector3.zero;
                    var tsr = tower.AddComponent<SpriteRenderer>();
                    tsr.sprite = MakeCircle(32, new Color(0.5f, 0.5f, 0.55f));
                    tsr.sortingOrder = 2;
                    tower.transform.localScale = Vector3.one * 0.7f;
                }
            }

            // Spawn enemies along the path via combat system
            BuildCombatArena(bootstrapGO, opts);
        }

        // ── MergeIdle: 3×3 board ──────────────────────────────────────────────────

        private static void BuildMergeBoard()
        {
            var board = new GameObject("MergeBoard");

            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                var cell = new GameObject($"Cell_{row}_{col}");
                cell.transform.SetParent(board.transform, false);
                cell.transform.localPosition = new Vector3((col - 1) * 1.3f, (row - 1) * 1.3f, 0);

                var sr = cell.AddComponent<SpriteRenderer>();
                sr.sprite = MakeRect(new Color(0.22f, 0.18f, 0.30f));
                sr.sortingOrder = 1;
                cell.transform.localScale = Vector3.one * 1.2f;
                cell.AddComponent<BoxCollider2D>();
                cell.AddComponent<EndlessEngine.Bootstrap.MergeCellHandler>();
            }

            // 2 seed items (T1 gold coins)
            PlaceMergeItem(board.transform.GetChild(0), new Color(0.95f, 0.75f, 0.1f), "T1");
            PlaceMergeItem(board.transform.GetChild(1), new Color(0.95f, 0.75f, 0.1f), "T1");
            BuildWorldLabel(new Vector3(0, -2.5f, 0), "Merge matching items!", Color.white, 0.2f);
        }

        private static void PlaceMergeItem(Transform cell, Color color, string label)
        {
            var item = new GameObject($"Item_{label}");
            item.transform.SetParent(cell, false);
            item.transform.localScale = Vector3.one * 0.65f;
            var sr = item.AddComponent<SpriteRenderer>();
            sr.sprite = MakeCircle(32, color);
            sr.sortingOrder = 3;
        }

        // ── FarmIdle: grid of farm plots ──────────────────────────────────────────

        private static void BuildFarmLayout(GameObject bootstrapGO, SetupOptions opts)
        {
            // Sky background
            var sky = new GameObject("Sky");
            sky.transform.position = new Vector3(0, 2f, 2f);
            var skySr = sky.AddComponent<SpriteRenderer>();
            skySr.sprite = MakeRect(new Color(0.4f, 0.65f, 0.9f));
            skySr.sortingOrder = -3;
            sky.transform.localScale = new Vector3(12f, 6f, 1f);

            // Ground
            var ground = new GameObject("Ground");
            ground.transform.position = new Vector3(0, -2.5f, 1f);
            var gsr = ground.AddComponent<SpriteRenderer>();
            gsr.sprite = MakeRect(new Color(0.45f, 0.3f, 0.15f));
            gsr.sortingOrder = -2;
            ground.transform.localScale = new Vector3(12f, 3f, 1f);

            // 6 farm plots in a 3×2 grid
            var plotColors = new Color[]
            {
                new Color(0.3f, 0.5f, 0.2f),  // planted
                new Color(0.25f, 0.45f, 0.15f),
                new Color(0.2f, 0.65f, 0.2f), // ready to harvest
                new Color(0.3f, 0.5f, 0.2f),
                new Color(0.55f, 0.4f, 0.2f), // empty
                new Color(0.3f, 0.5f, 0.2f),
            };
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++)
            {
                int idx = row * 3 + col;
                var plot = new GameObject($"FarmPlot_{idx}");
                plot.transform.position = new Vector3((col - 1) * 2.8f, row * 1.5f - 0.8f, 0);
                plot.transform.localScale = new Vector3(2.5f, 1.3f, 1f);

                var sr = plot.AddComponent<SpriteRenderer>();
                sr.sprite = MakeRect(plotColors[idx]);
                sr.sortingOrder = 1;
                plot.AddComponent<BoxCollider2D>();

                // Crop visual (small green circle if planted)
                if (idx != 4)
                {
                    var crop = new GameObject("Crop");
                    crop.transform.SetParent(plot.transform, false);
                    crop.transform.localPosition = new Vector3(0, 0.1f, -0.1f);
                    crop.transform.localScale = Vector3.one * (idx == 2 ? 0.35f : 0.22f);
                    var csr = crop.AddComponent<SpriteRenderer>();
                    csr.sprite = MakeCircle(32, idx == 2
                        ? new Color(0.9f, 0.85f, 0.1f)  // gold = ready
                        : new Color(0.25f, 0.7f, 0.25f)); // green = growing
                    csr.sortingOrder = 2;
                }
            }

            BuildWorldLabel(new Vector3(0, 2.8f, 0), "Tap plots to plant & harvest", Color.white, 0.2f);
        }

        // ── BuildingIdle: city grid placeholder ───────────────────────────────────

        private static void BuildBuildingGrid()
        {
            // Simple 4×2 city grid with colored building slots
            var gridColors = new Color[]
            {
                new Color(0.6f, 0.55f, 0.5f), new Color(0.5f, 0.55f, 0.65f),
                new Color(0.55f, 0.6f, 0.5f), new Color(0.6f, 0.5f, 0.5f),
                new Color(0.5f, 0.6f, 0.55f), new Color(0.55f, 0.5f, 0.6f),
                new Color(0.6f, 0.6f, 0.5f),  new Color(0.5f, 0.5f, 0.6f),
            };
            for (int i = 0; i < 8; i++)
            {
                var b = new GameObject($"Building_{i}");
                b.transform.position = new Vector3((i % 4 - 1.5f) * 2.4f, (i / 4 - 0.5f) * 2.2f, 0);
                b.transform.localScale = new Vector3(2.1f, 2.0f, 1f);
                var sr = b.AddComponent<SpriteRenderer>();
                sr.sprite = MakeRect(gridColors[i]);
                sr.sortingOrder = 1;
                b.AddComponent<BoxCollider2D>();
                var slot = b.AddComponent<EndlessEngine.Bootstrap.BuildingSlotHandler>();
                slot.Configure($"building_{i}", i % 4, i / 4);
            }
        }

        // ── HUD ───────────────────────────────────────────────────────────────────

        private static void BuildHUD(SetupOptions opts, GameObject bootstrapGO)
        {
            bool isWave     = opts.HasWave || opts.Type == GameType.IdleVsRPG
                              || opts.Type == GameType.TowerDefense;
            bool isHarvest  = opts.HasHarvest || opts.Type == GameType.HarvestIdle;
            bool isClick    = opts.HasClickLoop || opts.Type == GameType.ClickLoop;
            bool isResearch = opts.Type == GameType.ResearchIdle;
            bool isRight    = isWave || opts.Type == GameType.TowerDefense;

            var canvasGO = new GameObject("Canvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            float panelW = 380f;
            float panelH = CalcPanelHeight(opts);

            // Position: wave/tower = top-right, others = center
            var bgGO  = new GameObject("HUDPanel");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.03f, 0.03f, 0.05f, 0.90f);
            var bgRt  = bgGO.GetComponent<RectTransform>();
            if (isRight)
            {
                bgRt.anchorMin        = new Vector2(1f, 1f);
                bgRt.anchorMax        = new Vector2(1f, 1f);
                bgRt.pivot            = new Vector2(1f, 1f);
                bgRt.anchoredPosition = new Vector2(-10, -10);
            }
            else
            {
                bgRt.anchorMin        = new Vector2(0f, 0f);
                bgRt.anchorMax        = new Vector2(0f, 1f);
                bgRt.pivot            = new Vector2(0f, 0.5f);
                bgRt.anchoredPosition = new Vector2(10, 0);
                panelW = 340f;
            }
            bgRt.sizeDelta = new Vector2(panelW, panelH);

            // Build rows from top down
            float y = 0.97f;

            y = AddLabel(bgGO, "Title", opts.GameName,
                22, true, y, 0.1f, new Color(1f, 0.85f, 0.3f));

            y = AddLabel(bgGO, "GoldLabel", "Gold: 0",
                17, false, y, 0.12f, Color.white);

            y = AddLabel(bgGO, "IncomeLabel", "Income: 0/s",
                12, false, y, 0.08f, new Color(0.5f, 0.9f, 0.5f));

            if (opts.HasGenerator || opts.Type == GameType.PureIdle
                || opts.Type == GameType.BuildingIdle || opts.Type == GameType.ResearchIdle
                || opts.Type == GameType.PrestigeHeavy || opts.Type == GameType.FarmIdle)
            {
                y = AddLabel(bgGO, "GeneratorTitle", "▸ Generators",
                    11, true, y, 0.07f, new Color(0.5f, 0.75f, 1f));

                y = AddButton(bgGO, "BuyGeneratorButton", "Buy Gold Mine (50g)",
                    y, 0.10f, new Color(0.08f, 0.28f, 0.08f));

                y = AddLabel(bgGO, "GeneratorCountLabel", "Owned: 0",
                    10, false, y, 0.06f, new Color(0.55f, 0.55f, 0.55f));
            }

            if (isHarvest)
            {
                y = AddLabel(bgGO, "HarvestLabel", "Harvested: 0",
                    11, false, y, 0.07f, new Color(0.4f, 0.9f, 0.4f));

                y = AddLabel(bgGO, "ComboLabel", "Combo: ×1.0",
                    11, false, y, 0.07f, new Color(0.9f, 0.75f, 0.3f));
            }

            if (isClick)
            {
                y = AddLabel(bgGO, "ClickIncomeLabel", "Click yield: 0",
                    11, false, y, 0.07f, new Color(1f, 0.7f, 0.2f));

                // Only add ComboLabel if harvest hasn't already added it (same GO name → GameObject.Find conflict)
                if (!isHarvest)
                    y = AddLabel(bgGO, "ComboLabel", "Combo: ×1.0",
                        11, false, y, 0.07f, new Color(0.9f, 0.75f, 0.3f));
            }

            if (isResearch)
            {
                y = AddLabel(bgGO, "ResearchLabel", "Research: idle",
                    11, false, y, 0.07f, new Color(0.4f, 0.8f, 1f));
            }

            if (isWave)
            {
                y = AddLabel(bgGO, "WaveLabel", "Wave: 1",
                    13, true, y, 0.08f, new Color(1f, 0.4f, 0.4f));

                y = AddLabel(bgGO, "EnemyCountLabel", "Enemies: 0",
                    10, false, y, 0.06f, new Color(0.8f, 0.3f, 0.3f));
            }

            if (opts.HasMultiCurrency)
            {
                y = AddLabel(bgGO, "GemLabel", "Gems: 0",
                    11, false, y, 0.07f, new Color(0.8f, 0.4f, 1f));
            }

            if (opts.HasPrestige || opts.Type == GameType.PrestigeHeavy)
            {
                // Prestige button near bottom
                AddButtonFixed(bgGO, "PrestigeButton", "✦ Prestige",
                    0.02f, 0.11f, new Color(0.28f, 0.06f, 0.28f));

                AddLabelFixed(bgGO, "PrestigeLabel", "×1.0  (0 prestiges)",
                    10, false, 0.12f, 0.18f, new Color(0.8f, 0.5f, 1f));
            }

            // Save indicator
            AddLabelFixed(bgGO, "SaveLabel", "",
                8, false, 0.0f, 0.025f, new Color(0.3f, 0.3f, 0.3f));

            // UGUI fallback controller (works without UIToolkit)
            var hud   = canvasGO.AddComponent<GeneratedGameHUD>();
            var hudSO = new SerializedObject(hud);
            SetObjRef(hudSO, "_bootstrapSource", bootstrapGO);
            hudSO.ApplyModifiedPropertiesWithoutUndo();

            // UIToolkit screen stack — only added when UXML assets exist in this project
            BuildUIToolkitScreens(opts, bootstrapGO);
        }

        private static void BuildUIToolkitScreens(SetupOptions opts, GameObject bootstrapGO)
        {
            // ── HUD ──────────────────────────────────────────────────────────────────
            const string hudUxmlPath = "Assets/UI/HUD/HUD.uxml";
            var hudUxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(hudUxmlPath);
            if (hudUxml != null)
            {
                var hudGO   = new GameObject("Screen_HUD");
                AddUIDocument(hudGO, hudUxml);

                var hudCtrl   = hudGO.AddComponent<EndlessEngine.UI.HUDController>();
                var hudCtrlSO = new SerializedObject(hudCtrl);
                if (opts.HasPrestige || opts.Type == GameType.PrestigeHeavy)
                {
                    var psm = bootstrapGO.GetComponent<PrestigeStateManager>();
                    if (psm != null) SetObjRef(hudCtrlSO, "_prestigeStateManager", psm);
                }
                hudCtrlSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── Generator Screen ─────────────────────────────────────────────────────
            if (opts.HasGenerator)
            {
                const string genUxmlPath = "Assets/UI/Generator/GeneratorScreen.uxml";
                var genUxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(genUxmlPath);
                if (genUxml != null)
                {
                    var genGO = new GameObject("Screen_Generator");
                    AddUIDocument(genGO, genUxml);

                    var genCtrl   = genGO.AddComponent<EndlessEngine.UI.GeneratorScreenController>();
                    var genCtrlSO = new SerializedObject(genCtrl);
                    // GeneratorSystem is added at runtime by AutoSetupBootstrap, but we add it
                    // explicitly here so it exists as a serialized reference in the scene.
                    var genSys = bootstrapGO.AddComponent<EndlessEngine.Generator.GeneratorSystem>();
                    SetObjRef(genCtrlSO, "_generatorSystem", genSys);
                    genCtrlSO.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // ── Upgrade Screen ───────────────────────────────────────────────────────
            {
                const string upgUxmlPath = "Assets/UI/Upgrade/UpgradeScreen.uxml";
                var upgUxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(upgUxmlPath);
                if (upgUxml != null)
                {
                    var upgGO   = new GameObject("Screen_Upgrades");
                    AddUIDocument(upgGO, upgUxml);
                    var upgCtrl   = upgGO.AddComponent<EndlessEngine.UI.UpgradeScreenController>();
                    var upgCtrlSO = new SerializedObject(upgCtrl);

                    // Wire UpgradeTreeConfigSO from the wizard-generated asset
                    if (!string.IsNullOrEmpty(opts.UpgradeTreePath))
                    {
                        var upgradeTree = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.UpgradeTreeConfigSO>(
                            opts.UpgradeTreePath);
                        if (upgradeTree != null)
                            SetSORef(upgCtrlSO, "_upgradeTree", upgradeTree);
                    }
                    upgCtrlSO.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // ── Prestige Overlay ─────────────────────────────────────────────────────
            if (opts.HasPrestige || opts.Type == GameType.PrestigeHeavy)
            {
                const string presUxmlPath = "Assets/UI/Prestige/PrestigeOverlay.uxml";
                var presUxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(presUxmlPath);
                if (presUxml != null)
                {
                    var presGO = new GameObject("Screen_Prestige");
                    AddUIDocument(presGO, presUxml);
                    presGO.AddComponent<EndlessEngine.UI.PrestigeScreenUI>();
                }
            }

            // ── Research Screen ──────────────────────────────────────────────────────
            if (opts.Type == GameType.ResearchIdle)
            {
                const string resUxmlPath = "Assets/UI/Research/ResearchScreen.uxml";
                var resUxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(resUxmlPath);
                if (resUxml != null)
                {
                    var resGO = new GameObject("Screen_Research");
                    AddUIDocument(resGO, resUxml);
                    resGO.AddComponent<EndlessEngine.UI.ResearchScreenController>();
                }
            }

            // ── Building Screen ──────────────────────────────────────────────────────
            if (opts.HasBuilding)
            {
                const string bldUxmlPath = "Assets/UI/Building/BuildingGridScreen.uxml";
                var bldUxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(bldUxmlPath);
                if (bldUxml != null)
                {
                    var bldGO = new GameObject("Screen_Building");
                    AddUIDocument(bldGO, bldUxml);
                    bldGO.AddComponent<EndlessEngine.UI.BuildingScreenController>();
                }
            }
        }

        private static void AddUIDocument(GameObject go, UnityEngine.UIElements.VisualTreeAsset uxml)
        {
            var doc      = go.AddComponent<UnityEngine.UIElements.UIDocument>();
            var docSO    = new SerializedObject(doc);
            var treeProp = docSO.FindProperty("m_VisualTreeAsset");
            if (treeProp != null) treeProp.objectReferenceValue = uxml;
            docSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private static float CalcPanelHeight(SetupOptions opts)
        {
            float h = 280f;
            if (opts.HasGenerator) h += 90f;
            if (opts.HasPrestige || opts.Type == GameType.PrestigeHeavy) h += 80f;
            if (opts.HasWave || opts.Type == GameType.IdleVsRPG) h += 60f;
            if (opts.HasHarvest || opts.Type == GameType.HarvestIdle) h += 60f;
            if (opts.HasClickLoop || opts.Type == GameType.ClickLoop) h += 60f;
            if (opts.Type == GameType.ResearchIdle) h += 40f;
            if (opts.HasMultiCurrency) h += 40f;
            return Mathf.Clamp(h, 300f, 700f);
        }

        // ── World helpers ─────────────────────────────────────────────────────────

        private static void BuildArenaBackground(Color outer, Color inner)
        {
            var bg = new GameObject("ArenaBG");
            bg.transform.position = new Vector3(0, 0, 2f);
            var sr = bg.AddComponent<SpriteRenderer>();
            sr.sprite = MakeRect(outer);
            sr.sortingOrder = -2;
            bg.transform.localScale = new Vector3(11f, 9f, 1f);

            var ring = new GameObject("ArenaRing");
            ring.transform.position = new Vector3(0, 0, 1.9f);
            var rsr = ring.AddComponent<SpriteRenderer>();
            rsr.sprite = MakeCircle(64, inner);
            rsr.sortingOrder = -1;
            ring.transform.localScale = new Vector3(8.5f, 8.5f, 1f);
        }

        private static void BuildWorldHPBar(GameObject root, Color fillColor, Vector3 offset, float width)
        {
            var bar = new GameObject("HPBar");
            bar.transform.SetParent(root.transform, false);
            bar.transform.localPosition = offset;
            bar.transform.localScale    = new Vector3(width, 0.12f, 1f);

            var bgSr = bar.AddComponent<SpriteRenderer>();
            bgSr.sprite = MakeRect(new Color(0.15f, 0.02f, 0.02f));
            bgSr.sortingOrder = 5;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(bar.transform, false);
            var fsr = fill.AddComponent<SpriteRenderer>();
            fsr.sprite = MakeRect(fillColor);
            fsr.sortingOrder = 6;
            fill.transform.localScale = Vector3.one;
        }

        private static void BuildWorldLabel(Vector3 worldPos, string text, Color color, float scale)
        {
            var go = new GameObject("WorldLabel");
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * scale;
        }

        // ── UI layout helpers ─────────────────────────────────────────────────────

        private static float AddLabel(GameObject parent, string name, string text,
            int fontSize, bool bold, float topY, float height, Color color)
        {
            float bot = topY - height;
            CreateLabel(parent.transform, name, text, fontSize, bold,
                new Vector2(0.04f, bot), new Vector2(0.96f, topY), color);
            return bot;
        }

        private static float AddButton(GameObject parent, string name, string label,
            float topY, float height, Color bgColor)
        {
            float bot = topY - height;
            CreateButton(parent.transform, name, label,
                new Vector2(0.04f, bot), new Vector2(0.96f, topY), bgColor);
            return bot;
        }

        private static void AddButtonFixed(GameObject parent, string name, string label,
            float yMin, float yMax, Color bgColor)
            => CreateButton(parent.transform, name, label,
                new Vector2(0.04f, yMin), new Vector2(0.96f, yMax), bgColor);

        private static void AddLabelFixed(GameObject parent, string name, string text,
            int fontSize, bool bold, float yMin, float yMax, Color color)
            => CreateLabel(parent.transform, name, text, fontSize, bold,
                new Vector2(0.04f, yMin), new Vector2(0.96f, yMax), color);

        private static void CreateLabel(Transform parent, string name, string text,
            int fontSize, bool bold, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(4, 0);
            rt.offsetMax = new Vector2(-4, 0);
#if TMPRO_PRESENT
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.fontStyle = bold ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            tmp.color     = color;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
#else
            var lbl = go.AddComponent<UnityEngine.UI.Text>();
            lbl.text      = text;
            lbl.fontSize  = fontSize;
            lbl.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            lbl.color     = color;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#endif
        }

        private static GameObject CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(8, 3);
            rt.offsetMax = new Vector2(-8, -3);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = bgColor;

            var btn    = go.AddComponent<UnityEngine.UI.Button>();
            var col    = btn.colors;
            col.normalColor      = bgColor;
            col.highlightedColor = bgColor * 1.4f;
            col.pressedColor     = bgColor * 0.6f;
            btn.colors = col;

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lblRt = lblGO.AddComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero;
            lblRt.offsetMax = Vector2.zero;
#if TMPRO_PRESENT
            var tmp = lblGO.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 11;
            tmp.color     = Color.white;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
#else
            var txt = lblGO.AddComponent<UnityEngine.UI.Text>();
            txt.text      = label;
            txt.fontSize  = 11;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#endif
            return go;
        }

        // ── Procedural sprite helpers ─────────────────────────────────────────────

        private static Sprite MakeCircle(int res, Color c)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float r = res * 0.5f;
            float rSq = (r - 1f) * (r - 1f);
            var px = tex.GetPixels32();
            var cc = ToC32(c);
            var tr = new Color32(0, 0, 0, 0);
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                px[y * res + x] = dx * dx + dy * dy <= rSq ? cc : tr;
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        }

        private static Sprite MakeRect(Color c)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = tex.GetPixels32();
            var cc = ToC32(c);
            for (int i = 0; i < px.Length; i++) px[i] = cc;
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }

        private static Color32 ToC32(Color c) =>
            new Color32((byte)(c.r * 255), (byte)(c.g * 255),
                        (byte)(c.b * 255), (byte)(c.a * 255));

        // ── SerializedObject helpers ──────────────────────────────────────────────

        private static void SetSORef(SerializedObject so, string field, Object val)
        { var p = so.FindProperty(field); if (p != null) p.objectReferenceValue = val; }

        private static void SetObjRef(SerializedObject so, string field, Object val)
        { var p = so.FindProperty(field); if (p != null) p.objectReferenceValue = val; }

        private static void SetBool(SerializedObject so, string field, bool val)
        { var p = so.FindProperty(field); if (p != null) p.boolValue = val; }

        // ── File system helpers ───────────────────────────────────────────────────

        private static void EnsureDirectory(string path)
        {
            string full = Path.Combine(Application.dataPath, "..",
                path.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes) if (s.path == scenePath) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
                { new EditorBuildSettingsScene(scenePath, true) };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
