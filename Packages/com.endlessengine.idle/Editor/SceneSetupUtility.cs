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
    /// Builds a fully-wired, play-ready scene with:
    ///   - Bootstrap GameObject (AutoSetupBootstrap + all services)
    ///   - Canvas with gold display and optional generator/upgrade panels
    ///   - EventSystem
    ///   - Main Camera
    ///
    /// The user presses Play immediately — no Inspector wiring needed.
    /// </summary>
    public static class SceneSetupUtility
    {
        public struct SetupOptions
        {
            public string GameName;
            public string ScenesPath;
            public string ConfigsPath;
            public bool   HasGenerator;
            public bool   HasPrestige;
            public bool   HasMultiCurrency;
        }

        /// <summary>
        /// Creates a new scene at scenePath and populates it with a fully-wired
        /// idle game skeleton. Returns true on success.
        /// </summary>
        public static bool BuildScene(SetupOptions opts)
        {
            string scenePath = $"{opts.ScenesPath}/{opts.GameName}.unity";

            // Create and open new empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);

            // ── Main Camera ──────────────────────────────────────────────────────
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam   = camGO.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 5;
            cam.backgroundColor  = new Color(0.1f, 0.1f, 0.12f);
            cam.clearFlags       = CameraClearFlags.SolidColor;
            camGO.transform.position = new Vector3(0, 0, -10);

            // ── EventSystem ──────────────────────────────────────────────────────
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();

            // ── Bootstrap ────────────────────────────────────────────────────────
            var bootstrapGO = new GameObject("Bootstrap");
            var bootstrap   = bootstrapGO.AddComponent<AutoSetupBootstrap>();

            // Wire config assets if they exist
            string econPath     = $"{opts.ConfigsPath}/EconomyConfig.asset";
            string genDbPath    = $"{opts.ConfigsPath}/GeneratorDatabase.asset";
            string schemaPath   = $"{opts.ConfigsPath}/SchemaVersion.asset";
            string prestigePath = $"{opts.ConfigsPath}/PrestigeConfig.asset";

            var econConfig = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.EconomyConfigSO>(econPath);
            var genDb      = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.GeneratorDatabaseSO>(genDbPath);
            var schema     = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.SchemaVersionSO>(schemaPath);
            var prestige   = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.PrestigeConfigSO>(prestigePath);

            // Use SerializedObject to set private [SerializeField] fields
            var so = new SerializedObject(bootstrap);
            SetSOField(so, "_economyConfig",    econConfig);
            SetSOField(so, "_generatorDatabase", genDb);
            SetSOField(so, "_schemaVersion",    schema);
            SetSOField(so, "_prestigeConfig",   prestige);
            SetBoolField(so, "_enableSave",     true);
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── UI Canvas ────────────────────────────────────────────────────────
            BuildHUD(opts, bootstrapGO);

            // ── Save scene ───────────────────────────────────────────────────────
            EnsureDirectory(opts.ScenesPath);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.CloseScene(scene, removeScene: false);

            // Add to build settings
            AddSceneToBuildSettings(scenePath);

            Debug.Log($"[SceneSetupUtility] Scene created: {scenePath}");
            return true;
        }

        private static void BuildHUD(SetupOptions opts, GameObject bootstrapGO)
        {
            // Canvas root
            var canvasGO = new GameObject("Canvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Background panel
            var bgGO  = CreatePanel(canvasGO.transform, "Background",
                new Color(0.05f, 0.05f, 0.07f, 0.95f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(420, 580));

            // Title
            CreateLabel(bgGO.transform, "Title", $"{opts.GameName}",
                fontSize: 22, bold: true,
                anchorMin: new Vector2(0.1f, 0.85f), anchorMax: new Vector2(0.9f, 0.97f),
                color: new Color(1f, 0.85f, 0.3f));

            // Gold label
            CreateLabel(bgGO.transform, "GoldLabel", "Gold: 0",
                fontSize: 18, bold: false,
                anchorMin: new Vector2(0.05f, 0.72f), anchorMax: new Vector2(0.95f, 0.84f),
                color: Color.white);

            // Income rate label
            CreateLabel(bgGO.transform, "IncomeLabel", "Income: 0/s",
                fontSize: 13, bold: false,
                anchorMin: new Vector2(0.05f, 0.63f), anchorMax: new Vector2(0.95f, 0.72f),
                color: new Color(0.6f, 0.9f, 0.6f));

            // Generator section
            if (opts.HasGenerator)
            {
                CreateLabel(bgGO.transform, "GeneratorTitle", "Generators",
                    fontSize: 13, bold: true,
                    anchorMin: new Vector2(0.05f, 0.55f), anchorMax: new Vector2(0.95f, 0.63f),
                    color: new Color(0.7f, 0.85f, 1f));

                CreateButton(bgGO.transform, "BuyGeneratorButton", "Buy Gold Mine (cost: 100)",
                    anchorMin: new Vector2(0.05f, 0.44f), anchorMax: new Vector2(0.95f, 0.55f),
                    normalColor: new Color(0.1f, 0.35f, 0.1f));

                CreateLabel(bgGO.transform, "GeneratorCountLabel", "Owned: 0",
                    fontSize: 11, bold: false,
                    anchorMin: new Vector2(0.05f, 0.37f), anchorMax: new Vector2(0.95f, 0.44f),
                    color: new Color(0.7f, 0.7f, 0.7f));
            }

            // Prestige section
            if (opts.HasPrestige)
            {
                CreateButton(bgGO.transform, "PrestigeButton", "Prestige (resets for multiplier)",
                    anchorMin: new Vector2(0.05f, 0.13f), anchorMax: new Vector2(0.95f, 0.22f),
                    normalColor: new Color(0.35f, 0.1f, 0.35f));

                CreateLabel(bgGO.transform, "PrestigeLabel", "Prestige: 0 | Multiplier: x1",
                    fontSize: 11, bold: false,
                    anchorMin: new Vector2(0.05f, 0.06f), anchorMax: new Vector2(0.95f, 0.13f),
                    color: new Color(0.85f, 0.6f, 1f));
            }

            // Save indicator
            CreateLabel(bgGO.transform, "SaveLabel", "",
                fontSize: 9, bold: false,
                anchorMin: new Vector2(0.05f, 0.01f), anchorMax: new Vector2(0.95f, 0.06f),
                color: new Color(0.4f, 0.4f, 0.4f));

            // Add the HUD controller that wires everything at runtime
            var hud = canvasGO.AddComponent<GeneratedGameHUD>();
            var hudSO = new SerializedObject(hud);
            SetObjectField(hudSO, "_bootstrapSource", bootstrapGO);
            hudSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── UI Helper Methods ─────────────────────────────────────────────────────

        private static GameObject CreatePanel(Transform parent, string name, Color color,
            Vector2 pivot, Vector2 anchor, Vector2 sizeDelta)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot     = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;
            return go;
        }

        private static void CreateLabel(Transform parent, string name, string text,
            int fontSize, bool bold, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.offsetMin        = new Vector2(4, 0);
            rt.offsetMax        = new Vector2(-4, 0);

            // Try TextMeshPro first, fall back to legacy Text
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
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin  = anchorMin;
            rt.anchorMax  = anchorMax;
            rt.offsetMin  = new Vector2(8, 4);
            rt.offsetMax  = new Vector2(-8, -4);

            var img = go.AddComponent<Image>();
            img.color = normalColor;

            var btn = go.AddComponent<Button>();
            var colors       = btn.colors;
            colors.normalColor   = normalColor;
            colors.highlightedColor = normalColor * 1.3f;
            colors.pressedColor  = normalColor * 0.7f;
            btn.colors = colors;

            // Label child
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

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void SetSOField(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static void SetBoolField(SerializedObject so, string fieldName, bool value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null) prop.boolValue = value;
        }

        private static void SetObjectField(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null) prop.objectReferenceValue = value;
        }

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
                if (s.path == scenePath) return; // already in list

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
            {
                new EditorBuildSettingsScene(scenePath, enabled: true)
            };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
