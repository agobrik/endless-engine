using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EndlessEngine.Bootstrap;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Programmatic scene builder for Endless Engine games.
    ///
    /// Called by NewGameWizard after config assets are created.
    /// Builds a fully-wired, play-ready scene tailored to the chosen game type:
    ///
    ///   Pure Idle / Research / Building / Prestige-Heavy
    ///     → Bootstrap · HUD (gold, income, generator buy, optional prestige)
    ///
    ///   Clicker Idle
    ///     → Bootstrap · HUD · ClickTarget (visible circle, Collider2D, ClickHandler)
    ///
    ///   Idle-vs / RPG
    ///     → Bootstrap · HUD · EnemyPrefab (circle sprite, Rigidbody2D) wired into
    ///       WaveSpawnManager · AutoBattleController · combat services
    ///
    ///   Merge Idle
    ///     → Bootstrap · HUD · MergeBoard placeholder panel (5×5 grid labels)
    ///
    /// Press Play immediately — no Inspector wiring needed.
    /// </summary>
    public static class SceneSetupUtility
    {
        // ── Game type mirror (kept in sync with NewGameWizard.GameType) ──────────────

        public enum GameType
        {
            PureIdle        = 0,
            ClickerIdle     = 1,
            IdleVsRPG       = 2,
            MergeIdle       = 3,
            ResearchIdle    = 4,
            BuildingIdle    = 5,
            PrestigeHeavy   = 6,
            Custom          = 7,
        }

        public struct SetupOptions
        {
            public string   GameName;
            public string   ScenesPath;
            public string   ConfigsPath;
            public GameType Type;
            public bool     HasGenerator;
            public bool     HasPrestige;
            public bool     HasMultiCurrency;
            public bool     HasWave;
            public bool     HasClick;
            public bool     HasCursor;
            public bool     HasZone;
        }

        // ── Entry point ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new scene at ScenesPath/GameName.unity and populates it with a
        /// fully-wired idle game skeleton for the selected game type. Returns true on success.
        /// </summary>
        public static bool BuildScene(SetupOptions opts)
        {
            string scenePath = $"{opts.ScenesPath}/{opts.GameName}.unity";

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);

            // ── Main Camera ──────────────────────────────────────────────────────────
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam   = camGO.AddComponent<Camera>();
            cam.orthographic      = true;
            cam.orthographicSize  = 5;
            cam.backgroundColor   = new Color(0.08f, 0.08f, 0.1f);
            cam.clearFlags        = CameraClearFlags.SolidColor;
            camGO.transform.position = new Vector3(0, 0, -10);

            // ── EventSystem ──────────────────────────────────────────────────────────
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();

            // ── Bootstrap ────────────────────────────────────────────────────────────
            var bootstrapGO = new GameObject("Bootstrap");
            var bootstrap   = bootstrapGO.AddComponent<AutoSetupBootstrap>();
            WireBootstrapConfigs(bootstrap, opts);

            // ── Game-type-specific world objects ─────────────────────────────────────
            switch (opts.Type)
            {
                case GameType.ClickerIdle:
                    BuildClickTarget(bootstrapGO);
                    break;

                case GameType.IdleVsRPG:
                    BuildCombatArena(bootstrapGO, opts);
                    break;

                case GameType.MergeIdle:
                    BuildMergeBoard();
                    break;
            }

            // ── UI Canvas (always) ───────────────────────────────────────────────────
            BuildHUD(opts, bootstrapGO);

            // ── Save scene ───────────────────────────────────────────────────────────
            EnsureDirectory(opts.ScenesPath);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.CloseScene(scene, removeScene: false);

            AddSceneToBuildSettings(scenePath);

            Debug.Log($"[SceneSetupUtility] Scene created: {scenePath}");
            return true;
        }

        // ── Bootstrap wiring ─────────────────────────────────────────────────────────

        private static void WireBootstrapConfigs(AutoSetupBootstrap bootstrap, SetupOptions opts)
        {
            string econPath     = $"{opts.ConfigsPath}/EconomyConfig.asset";
            string genDbPath    = $"{opts.ConfigsPath}/GeneratorDatabase.asset";
            string schemaPath   = $"{opts.ConfigsPath}/SchemaVersion.asset";
            string prestigePath = $"{opts.ConfigsPath}/PrestigeConfig.asset";

            var econConfig = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.EconomyConfigSO>(econPath);
            var genDb      = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.GeneratorDatabaseSO>(genDbPath);
            var schema     = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.SchemaVersionSO>(schemaPath);
            var prestige   = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.PrestigeConfigSO>(prestigePath);

            var so = new SerializedObject(bootstrap);
            SetSOField(so, "_economyConfig",    econConfig);
            SetSOField(so, "_generatorDatabase", genDb);
            SetSOField(so, "_schemaVersion",    schema);
            SetSOField(so, "_prestigeConfig",   prestige);
            SetBoolField(so, "_enableSave",     true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Clicker: click target ─────────────────────────────────────────────────────

        private static void BuildClickTarget(GameObject bootstrapGO)
        {
            // Visible circle sprite target the player clicks/taps
            var targetGO = new GameObject("ClickTarget");
            targetGO.transform.position = new Vector3(0, 0.5f, 0);

            var sr = targetGO.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite(64, new Color(0.9f, 0.6f, 0.1f));
            sr.sortingOrder = 1;
            targetGO.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

            // Collider so pointer events register
            var col = targetGO.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            // Click handler that forwards to ClickYieldService
            targetGO.AddComponent<ClickTargetHandler>();

            // Glow ring behind the target
            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(targetGO.transform, false);
            var glowSr = glowGO.AddComponent<SpriteRenderer>();
            glowSr.sprite = CreateCircleSprite(64, new Color(1f, 0.75f, 0.2f, 0.25f));
            glowSr.sortingOrder = 0;
            glowGO.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
        }

        // ── Wave/RPG: combat arena ────────────────────────────────────────────────────

        private static void BuildCombatArena(GameObject bootstrapGO, SetupOptions opts)
        {
            // Enemy prefab (stored as a child of a disabled holder — WaveSpawnManager clones it)
            var prefabHolder = new GameObject("EnemyPrefabHolder");
            prefabHolder.SetActive(false);   // keep disabled so it doesn't run

            var enemyPrefab = new GameObject("Enemy");
            enemyPrefab.transform.SetParent(prefabHolder.transform, false);

            // Visible enemy: red circle
            var sr = enemyPrefab.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite(48, new Color(0.85f, 0.15f, 0.15f));
            sr.sortingOrder = 2;

            // Physics movement (EnemyAgent uses Rigidbody2D)
            var rb = enemyPrefab.AddComponent<Rigidbody2D>();
            rb.gravityScale  = 0;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            enemyPrefab.AddComponent<CircleCollider2D>();
            enemyPrefab.AddComponent<EndlessEngine.Enemy.EnemyAgent>();

            // HP bar (world-space canvas above enemy)
            BuildEnemyHPBar(enemyPrefab);

            // Arena background (dark rectangle)
            var arenaGO = new GameObject("Arena");
            arenaGO.transform.position = new Vector3(0, -0.5f, 1f);
            var arenaSr = arenaGO.AddComponent<SpriteRenderer>();
            arenaSr.sprite = CreateRectSprite(new Color(0.12f, 0.08f, 0.08f));
            arenaSr.sortingOrder = -1;
            arenaGO.transform.localScale = new Vector3(9f, 5f, 1f);

            // Wave combat services on Bootstrap
            var waveSpawn    = bootstrapGO.AddComponent<EndlessEngine.Wave.WaveSpawnManager>();
            var enemyMgr     = bootstrapGO.AddComponent<EndlessEngine.Enemy.EnemyManager>();
            var autoBattle   = bootstrapGO.AddComponent<EndlessEngine.Combat.AutoBattleController>();

            // Wire enemy prefab into WaveSpawnManager
            var wsso = new SerializedObject(waveSpawn);
            SetObjectField(wsso, "_enemyPrefab", enemyPrefab);
            SetObjectField(wsso, "_spawnParent", null);   // will spawn under scene root
            wsso.ApplyModifiedPropertiesWithoutUndo();

            // Wire configs
            string waveConfigPath  = $"{opts.ConfigsPath}/WaveConfig.asset";
            string enemyConfigPath = $"{opts.ConfigsPath}/EnemyStatConfig.asset";
            var waveConfig  = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.WaveConfigSO>(waveConfigPath);
            var enemyConfig = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.EnemyStatConfigSO>(enemyConfigPath);

            // WaveBootstrapper wires these at runtime — attach a helper component
            var waveBootstrap = bootstrapGO.AddComponent<WaveCombatBootstrap>();
            var wbso = new SerializedObject(waveBootstrap);
            SetObjectField(wbso, "_waveSpawnManager", waveSpawn);
            SetObjectField(wbso, "_enemyManager",     enemyMgr);
            SetObjectField(wbso, "_autoBattle",       autoBattle);
            SetSOField(wbso,     "_waveConfig",       waveConfig);
            SetSOField(wbso,     "_enemyConfig",      enemyConfig);
            wbso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildEnemyHPBar(GameObject enemyRoot)
        {
            var barGO = new GameObject("HPBar");
            barGO.transform.SetParent(enemyRoot.transform, false);
            barGO.transform.localPosition = new Vector3(0, 0.7f, 0);

            var bgSr = barGO.AddComponent<SpriteRenderer>();
            bgSr.sprite = CreateRectSprite(new Color(0.2f, 0f, 0f));
            bgSr.sortingOrder = 3;
            barGO.transform.localScale = new Vector3(0.8f, 0.1f, 1f);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(barGO.transform, false);
            fillGO.transform.localPosition = new Vector3(0, 0, 0);
            var fillSr = fillGO.AddComponent<SpriteRenderer>();
            fillSr.sprite = CreateRectSprite(new Color(0.1f, 0.8f, 0.1f));
            fillSr.sortingOrder = 4;
            fillGO.transform.localScale = Vector3.one;
        }

        // ── Merge: board placeholder ──────────────────────────────────────────────────

        private static void BuildMergeBoard()
        {
            // Simple 3×3 visual grid of colored squares as placeholder merge cells
            var boardGO = new GameObject("MergeBoard");
            boardGO.transform.position = new Vector3(0, 0, 0);

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    var cell = new GameObject($"Cell_{row}_{col}");
                    cell.transform.SetParent(boardGO.transform, false);
                    cell.transform.localPosition = new Vector3((col - 1) * 1.2f, (row - 1) * 1.2f, 0);

                    var sr = cell.AddComponent<SpriteRenderer>();
                    sr.sprite = CreateRectSprite(new Color(0.2f, 0.18f, 0.25f));
                    sr.sortingOrder = 1;
                    cell.transform.localScale = new Vector3(1.1f, 1.1f, 1f);

                    cell.AddComponent<BoxCollider2D>();
                }
            }

            // Seed two starter items so the board isn't completely empty
            PlaceMergeItem(boardGO.transform.GetChild(0), new Color(0.9f, 0.7f, 0.1f), "1");
            PlaceMergeItem(boardGO.transform.GetChild(1), new Color(0.9f, 0.7f, 0.1f), "1");
        }

        private static void PlaceMergeItem(Transform cell, Color color, string tier)
        {
            var item = new GameObject($"Item_T{tier}");
            item.transform.SetParent(cell, false);
            item.transform.localPosition = Vector3.zero;
            item.transform.localScale    = new Vector3(0.7f, 0.7f, 1f);

            var sr = item.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite(32, color);
            sr.sortingOrder = 2;
        }

        // ── HUD ───────────────────────────────────────────────────────────────────────

        private static void BuildHUD(SetupOptions opts, GameObject bootstrapGO)
        {
            var canvasGO = new GameObject("Canvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Panel width/height depends on game type
            bool isWave  = opts.Type == GameType.IdleVsRPG;
            bool isMerge = opts.Type == GameType.MergeIdle;

            float panelW = 380f;
            float panelH = isWave ? 480f : isMerge ? 320f : opts.HasPrestige ? 560f : 480f;
            float anchorX = isWave ? 0.02f : 0.5f;
            float anchorY = isWave ? 0.98f : 0.5f;
            Vector2 pivot = isWave ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);

            var bgGO = new GameObject("HUDPanel");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.06f, 0.92f);
            var bgRt = bgGO.GetComponent<RectTransform>();
            bgRt.anchorMin          = new Vector2(anchorX, anchorY);
            bgRt.anchorMax          = new Vector2(anchorX, anchorY);
            bgRt.pivot              = pivot;
            bgRt.sizeDelta          = new Vector2(panelW, panelH);
            bgRt.anchoredPosition   = isWave ? new Vector2(10, -10) : Vector2.zero;

            // Title bar
            CreateLabel(bgGO.transform, "Title", opts.GameName,
                fontSize: 20, bold: true,
                anchorMin: new Vector2(0.05f, 0.88f), anchorMax: new Vector2(0.95f, 0.99f),
                color: new Color(1f, 0.85f, 0.3f));

            // Gold
            CreateLabel(bgGO.transform, "GoldLabel", "Gold: 0",
                fontSize: 17, bold: false,
                anchorMin: new Vector2(0.05f, 0.76f), anchorMax: new Vector2(0.95f, 0.87f),
                color: Color.white);

            // Income
            CreateLabel(bgGO.transform, "IncomeLabel", "Income: 0/s",
                fontSize: 12, bold: false,
                anchorMin: new Vector2(0.05f, 0.68f), anchorMax: new Vector2(0.95f, 0.76f),
                color: new Color(0.55f, 0.9f, 0.55f));

            float nextY = 0.68f;

            if (opts.HasGenerator)
            {
                CreateLabel(bgGO.transform, "GeneratorTitle", "▶ Generators",
                    fontSize: 12, bold: true,
                    anchorMin: new Vector2(0.05f, nextY - 0.09f), anchorMax: new Vector2(0.95f, nextY),
                    color: new Color(0.6f, 0.8f, 1f));
                nextY -= 0.09f;

                CreateButton(bgGO.transform, "BuyGeneratorButton", "Buy Gold Mine (cost: 50)",
                    anchorMin: new Vector2(0.05f, nextY - 0.12f), anchorMax: new Vector2(0.95f, nextY),
                    normalColor: new Color(0.08f, 0.32f, 0.08f));
                nextY -= 0.12f;

                CreateLabel(bgGO.transform, "GeneratorCountLabel", "Owned: 0",
                    fontSize: 11, bold: false,
                    anchorMin: new Vector2(0.05f, nextY - 0.07f), anchorMax: new Vector2(0.95f, nextY),
                    color: new Color(0.6f, 0.6f, 0.6f));
                nextY -= 0.07f;
            }

            if (opts.HasClick)
            {
                CreateLabel(bgGO.transform, "ClickIncomeLabel", "Click gold: 0",
                    fontSize: 12, bold: false,
                    anchorMin: new Vector2(0.05f, nextY - 0.08f), anchorMax: new Vector2(0.95f, nextY),
                    color: new Color(1f, 0.75f, 0.2f));
                nextY -= 0.08f;
            }

            if (opts.HasWave)
            {
                CreateLabel(bgGO.transform, "WaveLabel", "Wave: 1",
                    fontSize: 14, bold: true,
                    anchorMin: new Vector2(0.05f, nextY - 0.09f), anchorMax: new Vector2(0.95f, nextY),
                    color: new Color(1f, 0.5f, 0.5f));
                nextY -= 0.09f;

                CreateLabel(bgGO.transform, "EnemyCountLabel", "Enemies: 0",
                    fontSize: 11, bold: false,
                    anchorMin: new Vector2(0.05f, nextY - 0.07f), anchorMax: new Vector2(0.95f, nextY),
                    color: new Color(0.8f, 0.4f, 0.4f));
                nextY -= 0.07f;
            }

            if (opts.HasPrestige)
            {
                CreateButton(bgGO.transform, "PrestigeButton", "✦ Prestige (reset for multiplier)",
                    anchorMin: new Vector2(0.05f, 0.09f), anchorMax: new Vector2(0.95f, 0.18f),
                    normalColor: new Color(0.3f, 0.08f, 0.3f));

                CreateLabel(bgGO.transform, "PrestigeLabel", "Prestige: 0  ×1.0",
                    fontSize: 11, bold: false,
                    anchorMin: new Vector2(0.05f, 0.02f), anchorMax: new Vector2(0.95f, 0.09f),
                    color: new Color(0.85f, 0.55f, 1f));
            }

            // Auto-save indicator (bottom strip)
            CreateLabel(bgGO.transform, "SaveLabel", "",
                fontSize: 9, bold: false,
                anchorMin: new Vector2(0.05f, 0f), anchorMax: new Vector2(0.95f, 0.03f),
                color: new Color(0.35f, 0.35f, 0.35f));

            // Attach runtime HUD controller
            var hud   = canvasGO.AddComponent<GeneratedGameHUD>();
            var hudSO = new SerializedObject(hud);
            SetObjectField(hudSO, "_bootstrapSource", bootstrapGO);
            hudSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Procedural sprite helpers ──────────────────────────────────────────────────

        private static Sprite CreateCircleSprite(int resolution, Color color)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float r    = resolution * 0.5f;
            float rSq  = (r - 1f) * (r - 1f);
            var pixels = tex.GetPixels32();
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                pixels[y * resolution + x] = (dx * dx + dy * dy <= rSq)
                    ? new Color32((byte)(color.r * 255), (byte)(color.g * 255),
                                  (byte)(color.b * 255), (byte)(color.a * 255))
                    : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex,
                new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f), resolution);
        }

        private static Sprite CreateRectSprite(Color color)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var c32 = new Color32((byte)(color.r*255),(byte)(color.g*255),(byte)(color.b*255),(byte)(color.a*255));
            var px  = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i] = c32;
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0,0,4,4), new Vector2(0.5f,0.5f), 4);
        }

        // ── UI helper methods ─────────────────────────────────────────────────────────

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
            var lbl = go.AddComponent<Text>();
            lbl.text      = text;
            lbl.fontSize  = fontSize;
            lbl.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            lbl.color     = color;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#endif
        }

        private static GameObject CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color normalColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(8, 4);
            rt.offsetMax = new Vector2(-8, -4);

            var img = go.AddComponent<Image>();
            img.color = normalColor;

            var btn    = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = normalColor;
            colors.highlightedColor = normalColor * 1.35f;
            colors.pressedColor     = normalColor * 0.65f;
            btn.colors = colors;

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
            tmp.fontSize  = 12;
            tmp.color     = Color.white;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
#else
            var txt = lblGO.AddComponent<Text>();
            txt.text      = label;
            txt.fontSize  = 12;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#endif
            return go;
        }

        // ── SerializedObject helpers ──────────────────────────────────────────────────

        private static void SetSOField(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetBoolField(SerializedObject so, string field, bool value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.boolValue = value;
        }

        private static void SetObjectField(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }

        // ── File system helpers ───────────────────────────────────────────────────────

        private static void EnsureDirectory(string path)
        {
            string full = Path.Combine(Application.dataPath, "..",
                path.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full))
                Directory.CreateDirectory(full);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == scenePath) return;

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
            {
                new EditorBuildSettingsScene(scenePath, enabled: true)
            };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
