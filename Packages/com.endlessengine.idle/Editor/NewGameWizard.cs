using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Tools → Endless Engine → New Game Wizard
    ///
    /// Step 1 — pick a Game Type (preset modules + economy values pre-filled).
    /// Step 2 — fine-tune individual module toggles.
    /// Step 3 — click Generate to produce:
    ///   - Configs/ folder with selected module ScriptableObject assets (values pre-set)
    ///   - Scripts/ folder with a generated Bootstrap .cs file
    ///   - Scenes/ folder placeholder note
    ///
    /// Game Types and their presets:
    ///   Pure Idle        — Generator + Prestige, long sessions, slow economy
    ///   Clicker Idle     — Click + Generator + Prestige, fast early game
    ///   Idle-vs / RPG    — Generator + Wave/Combat + Prestige + MultiCurrency
    ///   Merge Idle       — Click, no generators, merge-focused economy
    ///   Research Idle    — Generator + Prestige + MultiCurrency (research tokens)
    ///   Building Idle    — Generator + Zone + Prestige
    ///   Prestige-Heavy   — Generator + Prestige + MultiCurrency (all prestige layers)
    ///   Custom           — All toggles off by default, user picks manually
    /// </summary>
    public class NewGameWizard : EditorWindow
    {
        // ── Game type enum ────────────────────────────────────────────────────────

        private enum GameType
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

        // ── State ──────────────────────────────────────────────────────────────────

        private string _gameName = "MyIdleGame";

        private GameType _gameType = GameType.PureIdle;

        private bool _modGenerator     = true;
        private bool _modCursor        = false;
        private bool _modClick         = false;
        private bool _modZone          = false;
        private bool _modWave          = false;
        private bool _modPrestige      = true;
        private bool _modMultiCurrency = false;

        // UI refs
        private TextField  _nameField;
        private Label      _previewLabel;
        private Label      _statusLabel;
        private Label      _presetDescLabel;

        // ── Menu ───────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Endless Engine/New Game Wizard", priority = 0)]
        public static void Open()
        {
            var win = GetWindow<NewGameWizard>(utility: true, title: "New Game Wizard");
            win.minSize = new Vector2(540, 600);
            win.maxSize = new Vector2(720, 760);
        }

        // ── GUI ────────────────────────────────────────────────────────────────────

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop    = 12;
            root.style.paddingBottom = 12;
            root.style.paddingLeft   = 16;
            root.style.paddingRight  = 16;

            // ── Header ─────────────────────────────────────────────────────────────
            var header = new Label("New Game Wizard");
            header.style.fontSize   = 18;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 4;
            root.Add(header);

            var sub = new Label("Generates an idle game skeleton with pre-configured modules and economy values.");
            sub.style.fontSize    = 11;
            sub.style.color       = new Color(0.6f, 0.6f, 0.6f);
            sub.style.whiteSpace  = WhiteSpace.Normal;
            sub.style.marginBottom = 14;
            root.Add(sub);

            // ── Game Name ──────────────────────────────────────────────────────────
            root.Add(SectionLabel("Project Settings"));

            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems    = Align.Center;
            nameRow.style.marginBottom  = 8;

            var nameLabel = new Label("Game Name");
            nameLabel.style.width        = 90;
            nameLabel.style.fontSize     = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            _nameField = new TextField { value = _gameName };
            _nameField.style.flexGrow = 1;
            _nameField.RegisterValueChangedCallback(evt =>
            {
                _gameName = evt.newValue;
                RefreshPreview();
            });

            nameRow.Add(nameLabel);
            nameRow.Add(_nameField);
            root.Add(nameRow);

            // ── Game Type ──────────────────────────────────────────────────────────
            root.Add(SectionLabel("Game Type"));

            var typeRow = new VisualElement();
            typeRow.style.flexDirection = FlexDirection.Row;
            typeRow.style.alignItems    = Align.Center;
            typeRow.style.marginBottom  = 4;

            var typeLabel = new Label("Type");
            typeLabel.style.width       = 90;
            typeLabel.style.fontSize    = 12;
            typeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var typeNames = new List<string>();
            foreach (GameType t in System.Enum.GetValues(typeof(GameType)))
                typeNames.Add(GameTypeDisplayName(t));

            var typeDropdown = new DropdownField(typeNames, (int)_gameType);
            typeDropdown.style.flexGrow = 1;
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                int idx = typeNames.IndexOf(evt.newValue);
                _gameType = (GameType)idx;
                ApplyPreset(_gameType);
                RefreshPreview();
                RefreshModuleToggles();
                UpdatePresetDesc();
            });

            typeRow.Add(typeLabel);
            typeRow.Add(typeDropdown);
            root.Add(typeRow);

            _presetDescLabel = new Label(GetPresetDescription(_gameType));
            _presetDescLabel.style.fontSize     = 10;
            _presetDescLabel.style.color        = new Color(0.65f, 0.85f, 0.65f);
            _presetDescLabel.style.whiteSpace   = WhiteSpace.Normal;
            _presetDescLabel.style.marginBottom = 10;
            _presetDescLabel.style.marginLeft   = 92;
            root.Add(_presetDescLabel);

            // ── Module Toggles ─────────────────────────────────────────────────────
            root.Add(SectionLabel("Modules  (auto-set by type — override if needed)"));

            root.Add(CoreModuleRow());

            root.Add(ModuleToggleRow(
                "Generator",
                "PassiveIncomeService · GeneratorSystem",
                _modGenerator, v => { _modGenerator = v; RefreshPreview(); }));

            root.Add(ModuleToggleRow(
                "Cursor",
                "CursorYieldService  (Speed / Hover / Distance)",
                _modCursor, v => { _modCursor = v; RefreshPreview(); }));

            root.Add(ModuleToggleRow(
                "Click",
                "ClickYieldService  (combo · crit · auto-click)",
                _modClick, v => { _modClick = v; RefreshPreview(); }));

            root.Add(ModuleToggleRow(
                "Zone",
                "ZoneSystem  (world-space income zones)",
                _modZone, v => { _modZone = v; RefreshPreview(); }));

            root.Add(ModuleToggleRow(
                "Wave / Combat",
                "WaveSpawnManager · EnemyManager · HealthSystem",
                _modWave, v => { _modWave = v; RefreshPreview(); }));

            root.Add(ModuleToggleRow(
                "Prestige",
                "PrestigeStateManager  (reset → multiplier)",
                _modPrestige, v => { _modPrestige = v; RefreshPreview(); }));

            root.Add(ModuleToggleRow(
                "Multi-Currency",
                "CurrencyService  (gems · tokens · secondary currencies)",
                _modMultiCurrency, v => { _modMultiCurrency = v; RefreshPreview(); }));

            // ── Preview ────────────────────────────────────────────────────────────
            root.Add(SectionLabel("Files to Create"));

            var previewBox = new ScrollView();
            previewBox.style.height          = 100;
            previewBox.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            previewBox.style.borderTopLeftRadius     = 4;
            previewBox.style.borderTopRightRadius    = 4;
            previewBox.style.borderBottomLeftRadius  = 4;
            previewBox.style.borderBottomRightRadius = 4;
            previewBox.style.paddingTop    = 6;
            previewBox.style.paddingLeft   = 8;
            previewBox.style.paddingBottom = 6;
            previewBox.style.marginBottom  = 12;

            _previewLabel = new Label();
            _previewLabel.style.fontSize    = 10;
            _previewLabel.style.color       = new Color(0.75f, 0.85f, 0.75f);
            _previewLabel.style.whiteSpace  = WhiteSpace.Normal;
            previewBox.Add(_previewLabel);
            root.Add(previewBox);

            // ── Status ─────────────────────────────────────────────────────────────
            _statusLabel = new Label();
            _statusLabel.style.fontSize     = 11;
            _statusLabel.style.whiteSpace   = WhiteSpace.Normal;
            _statusLabel.style.marginBottom = 8;
            root.Add(_statusLabel);

            // ── Generate Button ────────────────────────────────────────────────────
            var btn = new Button(Generate) { text = "Generate Skeleton" };
            btn.style.height          = 36;
            btn.style.fontSize        = 13;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.backgroundColor = new Color(0.15f, 0.42f, 0.15f);
            btn.style.color           = Color.white;
            btn.style.borderTopLeftRadius     = 4;
            btn.style.borderTopRightRadius    = 4;
            btn.style.borderBottomLeftRadius  = 4;
            btn.style.borderBottomRightRadius = 4;
            root.Add(btn);

            ApplyPreset(_gameType);
            RefreshPreview();
        }

        // ── Game Type Preset Logic ─────────────────────────────────────────────────

        private static string GameTypeDisplayName(GameType t) => t switch
        {
            GameType.PureIdle      => "Pure Idle",
            GameType.ClickerIdle   => "Clicker Idle",
            GameType.IdleVsRPG     => "Idle-vs / RPG",
            GameType.MergeIdle     => "Merge Idle",
            GameType.ResearchIdle  => "Research Idle",
            GameType.BuildingIdle  => "Building Idle",
            GameType.PrestigeHeavy => "Prestige-Heavy",
            _                      => "Custom",
        };

        private static string GetPresetDescription(GameType t) => t switch
        {
            GameType.PureIdle      => "Generator + Prestige. Pure passive income — no combat, no clicking.",
            GameType.ClickerIdle   => "Click + Generator + Prestige. Fast early game, active tap/click.",
            GameType.IdleVsRPG     => "Generator + Wave/Combat + Prestige + Multi-Currency. Run-based arena.",
            GameType.MergeIdle     => "Click only. Income from merging and selling items — no generators.",
            GameType.ResearchIdle  => "Generator + Prestige + Multi-Currency. Long research queues gate content.",
            GameType.BuildingIdle  => "Generator + Zone + Prestige. Grid-based building placement income.",
            GameType.PrestigeHeavy => "Generator + Prestige + Multi-Currency. All three prestige layers active.",
            _                      => "All modules off by default. Configure manually.",
        };

        private void ApplyPreset(GameType t)
        {
            // Reset all
            _modGenerator = _modCursor = _modClick = _modZone = _modWave = _modPrestige = _modMultiCurrency = false;

            switch (t)
            {
                case GameType.PureIdle:
                    _modGenerator = true;
                    _modPrestige  = true;
                    break;

                case GameType.ClickerIdle:
                    _modClick     = true;
                    _modGenerator = true;
                    _modPrestige  = true;
                    break;

                case GameType.IdleVsRPG:
                    _modGenerator     = true;
                    _modWave          = true;
                    _modPrestige      = true;
                    _modMultiCurrency = true;
                    break;

                case GameType.MergeIdle:
                    _modClick = true;
                    break;

                case GameType.ResearchIdle:
                    _modGenerator     = true;
                    _modPrestige      = true;
                    _modMultiCurrency = true;
                    break;

                case GameType.BuildingIdle:
                    _modGenerator = true;
                    _modZone      = true;
                    _modPrestige  = true;
                    break;

                case GameType.PrestigeHeavy:
                    _modGenerator     = true;
                    _modPrestige      = true;
                    _modMultiCurrency = true;
                    break;

                case GameType.Custom:
                    break;
            }
        }

        private void UpdatePresetDesc()
        {
            if (_presetDescLabel != null)
                _presetDescLabel.text = GetPresetDescription(_gameType);
        }

        /// <summary>
        /// Rebuilds the module toggle rows to reflect the current _mod* values.
        /// Because UIElements doesn't directly support mutating toggle values from code
        /// after creation in this pattern, we trigger a full UI refresh.
        /// </summary>
        private void RefreshModuleToggles()
        {
            // Simplest approach: rebuild the whole window
            rootVisualElement.Clear();
            CreateGUI();
        }

        // ── Preset Economy Values ──────────────────────────────────────────────────

        /// <summary>
        /// Applies preset economy field values to a newly-created EconomyConfigSO
        /// based on the selected game type.
        /// </summary>
        private void ApplyEconomyPreset(EndlessEngine.Config.EconomyConfigSO so)
        {
            switch (_gameType)
            {
                case GameType.PureIdle:
                    so.IdleYieldRateBase         = 0.5f;
                    so.BaseMultiplierPerPrestige  = 1.5f;
                    so.ResourceHardCap            = 10_000_000_000L;
                    so.OfflineCapHours            = 12f;
                    break;

                case GameType.ClickerIdle:
                    so.IdleYieldRateBase         = 0.1f;
                    so.BaseMultiplierPerPrestige  = 1.5f;
                    so.ResourceHardCap            = 1_000_000_000L;
                    so.OfflineCapHours            = 8f;
                    break;

                case GameType.IdleVsRPG:
                    so.IdleYieldRateBase         = 0.2f;
                    so.BaseMultiplierPerPrestige  = 2.0f;
                    so.ResourceHardCap            = 100_000_000_000L;
                    so.OfflineCapHours            = 8f;
                    break;

                case GameType.MergeIdle:
                    so.IdleYieldRateBase         = 0f;
                    so.BaseMultiplierPerPrestige  = 1.0f;
                    so.ResourceHardCap            = 1_000_000L;
                    so.OfflineCapHours            = 0f;
                    break;

                case GameType.ResearchIdle:
                    so.IdleYieldRateBase         = 0.3f;
                    so.BaseMultiplierPerPrestige  = 1.8f;
                    so.ResourceHardCap            = 1_000_000_000_000L;
                    so.OfflineCapHours            = 24f;
                    break;

                case GameType.BuildingIdle:
                    so.IdleYieldRateBase         = 0.4f;
                    so.BaseMultiplierPerPrestige  = 1.6f;
                    so.ResourceHardCap            = 10_000_000_000L;
                    so.OfflineCapHours            = 12f;
                    break;

                case GameType.PrestigeHeavy:
                    so.IdleYieldRateBase         = 0.5f;
                    so.BaseMultiplierPerPrestige  = 3.0f;
                    so.ResourceHardCap            = 1_000_000_000_000_000L;
                    so.OfflineCapHours            = 8f;
                    break;

                default: // Custom — leave defaults
                    break;
            }
        }

        private void ApplyPrestigePreset(EndlessEngine.Config.PrestigeConfigSO so)
        {
            switch (_gameType)
            {
                case GameType.PureIdle:
                    so.BaseMultiplierPerPrestige = 1.5f;
                    so.MaxPermanentMultiplier    = 100f;
                    break;
                case GameType.ClickerIdle:
                    so.BaseMultiplierPerPrestige = 1.5f;
                    so.MaxPermanentMultiplier    = 50f;
                    break;
                case GameType.IdleVsRPG:
                    so.BaseMultiplierPerPrestige = 2.0f;
                    so.MaxPermanentMultiplier    = 1000f;
                    break;
                case GameType.ResearchIdle:
                    so.BaseMultiplierPerPrestige = 1.8f;
                    so.MaxPermanentMultiplier    = 500f;
                    break;
                case GameType.BuildingIdle:
                    so.BaseMultiplierPerPrestige = 1.6f;
                    so.MaxPermanentMultiplier    = 200f;
                    break;
                case GameType.PrestigeHeavy:
                    so.BaseMultiplierPerPrestige = 3.0f;
                    so.MaxPermanentMultiplier    = 10_000f;
                    break;
            }
        }

        private void ApplyWavePreset(EndlessEngine.Config.WaveConfigSO so)
        {
            switch (_gameType)
            {
                case GameType.IdleVsRPG:
                    so.TotalWavesPerRun        = 50;
                    so.BaseEnemyCountPerWave   = 5;
                    so.EliteWaveInterval       = 10;
                    break;
                default:
                    so.TotalWavesPerRun        = 30;
                    so.BaseEnemyCountPerWave   = 3;
                    so.EliteWaveInterval       = 5;
                    break;
            }
        }

        // ── UI Helpers ─────────────────────────────────────────────────────────────

        private static Label SectionLabel(string text)
        {
            var lbl = new Label(text);
            lbl.style.fontSize   = 11;
            lbl.style.color      = new Color(0.55f, 0.75f, 1f);
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.marginTop  = 8;
            lbl.style.marginBottom = 4;
            lbl.style.unityTextAlign = TextAnchor.UpperLeft;
            return lbl;
        }

        private static VisualElement CoreModuleRow()
        {
            return MakeRow(
                isCore:    true,
                labelText: "Core",
                tagText:   "TickEngine · EconomyService · SaveService · GameFlow",
                initial:   true,
                onChange:  null);
        }

        private static VisualElement ModuleToggleRow(
            string label, string tag, bool initial,
            System.Action<bool> onChange)
        {
            return MakeRow(isCore: false, labelText: label, tagText: tag,
                           initial: initial, onChange: onChange);
        }

        private static VisualElement MakeRow(
            bool isCore, string labelText, string tagText,
            bool initial, System.Action<bool> onChange)
        {
            var row = new VisualElement();
            row.style.flexDirection  = FlexDirection.Row;
            row.style.alignItems     = Align.Center;
            row.style.height         = 32;
            row.style.marginBottom   = 3;
            row.style.paddingLeft    = 8;
            row.style.paddingRight   = 8;
            row.style.backgroundColor = isCore
                ? new Color(0.18f, 0.22f, 0.18f)
                : new Color(0.16f, 0.16f, 0.16f);
            row.style.borderTopLeftRadius     = 4;
            row.style.borderTopRightRadius    = 4;
            row.style.borderBottomLeftRadius  = 4;
            row.style.borderBottomRightRadius = 4;

            var toggle = new Toggle { value = initial };
            toggle.style.marginRight = 8;
            if (isCore) toggle.SetEnabled(false);
            else toggle.RegisterValueChangedCallback(evt => onChange?.Invoke(evt.newValue));

            var nameLabel = new Label(isCore ? "Core  (always included)" : labelText);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.flexGrow = 0;
            nameLabel.style.flexShrink = 0;
            nameLabel.style.marginRight = 8;

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;

            var tagLabel = new Label(tagText);
            tagLabel.style.fontSize   = 10;
            tagLabel.style.color      = new Color(0.55f, 0.55f, 0.55f);
            tagLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            tagLabel.style.overflow   = Overflow.Hidden;
            tagLabel.style.flexShrink = 1;
            tagLabel.style.maxWidth   = 280;

            row.Add(toggle);
            row.Add(nameLabel);
            row.Add(spacer);
            row.Add(tagLabel);
            return row;
        }

        private void RefreshPreview()
        {
            if (_previewLabel == null) return;
            var lines = BuildFileList();
            _previewLabel.text = string.Join("\n", lines);
        }

        // ── File List ──────────────────────────────────────────────────────────────

        private List<string> BuildFileList()
        {
            string root = $"Assets/{SanitizeName(_gameName)}/";
            var list = new List<string>();

            list.Add($"{root}Configs/EconomyConfig.asset  [{GameTypeDisplayName(_gameType)} preset]");
            list.Add($"{root}Configs/SchemaVersion.asset");
            if (_modGenerator)  { list.Add($"{root}Configs/GoldMine.asset"); list.Add($"{root}Configs/GeneratorDatabase.asset"); }
            if (_modCursor)     list.Add($"{root}Configs/CursorActivityConfig.asset");
            if (_modClick)      list.Add($"{root}Configs/ClickSourceConfig.asset");
            if (_modZone)       list.Add($"{root}Configs/ZoneDatabase.asset");
            if (_modWave)       list.Add($"{root}Configs/WaveConfig.asset");
            if (_modWave)       list.Add($"{root}Configs/EnemyStatConfig.asset");
            if (_modWave)       list.Add($"{root}Configs/RunConfig.asset");
            if (_modPrestige)       list.Add($"{root}Configs/PrestigeConfig.asset");
            if (_modMultiCurrency)  list.Add($"{root}Configs/CurrencyDatabase.asset");

            list.Add($"{root}Scenes/{SanitizeName(_gameName)}.unity  ← OPEN THIS, then press Play");
            return list;
        }

        // ── Generation ─────────────────────────────────────────────────────────────

        private void Generate()
        {
            string name = SanitizeName(_gameName);
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Game name cannot be empty.", error: true);
                return;
            }

            string assetRoot   = $"Assets/{name}";
            string configPath  = $"{assetRoot}/Configs";
            string scriptsPath = $"{assetRoot}/Scripts";
            string scenesPath  = $"{assetRoot}/Scenes";

            CreateDirectories(assetRoot, configPath, scriptsPath, scenesPath);
            CreateConfigs(configPath);

            // Refresh first so asset paths resolve in the scene builder
            AssetDatabase.Refresh();

            // Build the scene with fully-wired GameObjects (no Inspector wiring needed)
            var sceneOpts = new SceneSetupUtility.SetupOptions
            {
                GameName         = name,
                ScenesPath       = scenesPath,
                ConfigsPath      = configPath,
                Type             = (SceneSetupUtility.GameType)(int)_gameType,
                HasGenerator     = _modGenerator,
                HasPrestige      = _modPrestige,
                HasMultiCurrency = _modMultiCurrency,
                HasWave          = _modWave,
                HasClick         = _modClick,
                HasCursor        = _modCursor,
                HasZone          = _modZone,
            };
            bool sceneOk = SceneSetupUtility.BuildScene(sceneOpts);

            AssetDatabase.Refresh();

            if (sceneOk)
            {
                SetStatus($"Done! Open Assets/{name}/Scenes/{name}.unity and press Play.", error: false);
                Debug.Log($"[NewGameWizard] '{name}' ({GameTypeDisplayName(_gameType)}) created at Assets/{name}/");
            }
            else
            {
                // Scene build failed — still useful, just open the scene manually
                SetStatus($"Configs created at Assets/{name}/ — scene build had an issue, see Console.", error: true);
            }
        }

        // ── Config Asset Creation ──────────────────────────────────────────────────

        private void CreateConfigs(string dir)
        {
            var econ = CreateSO<EndlessEngine.Config.EconomyConfigSO>(dir, "EconomyConfig");
            if (econ != null)
            {
                ApplyEconomyPreset(econ);
                EditorUtility.SetDirty(econ);
            }

            // Also create a SchemaVersion asset (required by SaveService)
            var schema = CreateSO<EndlessEngine.Config.SchemaVersionSO>(dir, "SchemaVersion");
            if (schema != null)
                EditorUtility.SetDirty(schema);

            if (_modGenerator)
            {
                // Create a default generator config
                var mine = CreateSO<EndlessEngine.Config.GeneratorConfigSO>(dir, "GoldMine");
                if (mine != null)
                {
                    mine.GeneratorId      = "gold_mine";
                    mine.DisplayName      = "Gold Mine";
                    mine.Description      = "Passively produces gold.";
                    mine.BaseYieldPerSecond = 1f;
                    mine.BaseCost         = 50;
                    mine.CostScalingFactor = 1.15f;
                    EditorUtility.SetDirty(mine);
                }

                // Save path so we can wire it into the database after Refresh
                AssetDatabase.Refresh();
                var db = CreateSO<EndlessEngine.Config.GeneratorDatabaseSO>(dir, "GeneratorDatabase");
                if (db == null)
                    db = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.GeneratorDatabaseSO>($"{dir}/GeneratorDatabase.asset");
                if (db != null && mine != null)
                {
                    db.Generators = new[] { mine };
                    EditorUtility.SetDirty(db);
                }
            }

            if (_modCursor)
                CreateSO<EndlessEngine.Config.CursorActivityConfigSO>(dir, "CursorActivityConfig");

            if (_modClick)
                CreateSO<EndlessEngine.Config.ClickSourceConfigSO>(dir, "ClickSourceConfig");

            if (_modZone)
                CreateSO<EndlessEngine.Config.ZoneDatabaseSO>(dir, "ZoneDatabase");

            if (_modWave)
            {
                var wave = CreateSO<EndlessEngine.Config.WaveConfigSO>(dir, "WaveConfig");
                if (wave != null) { ApplyWavePreset(wave); EditorUtility.SetDirty(wave); }
                CreateSO<EndlessEngine.Config.EnemyStatConfigSO>(dir, "EnemyStatConfig");
                CreateSO<EndlessEngine.Config.RunConfigSO>(dir, "RunConfig");
            }

            if (_modPrestige)
            {
                var prestige = CreateSO<EndlessEngine.Config.PrestigeConfigSO>(dir, "PrestigeConfig");
                if (prestige != null) { ApplyPrestigePreset(prestige); EditorUtility.SetDirty(prestige); }
            }

            if (_modMultiCurrency)
                CreateSO<EndlessEngine.Config.CurrencyDatabaseSO>(dir, "CurrencyDatabase");
        }

        private static T CreateSO<T>(string dir, string assetName) where T : ScriptableObject
        {
            string path = $"{dir}/{assetName}.asset";
            string fullPath = Path.Combine(Application.dataPath, "..", path);
            if (File.Exists(fullPath)) return null;  // already exists — don't overwrite
            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        // ── Bootstrap Script ───────────────────────────────────────────────────────

        private void CreateBootstrap(string dir, string name)
        {
            string path = Path.Combine(Application.dataPath, "..", dir, $"{name}Bootstrap.cs");
            if (File.Exists(path)) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using EndlessEngine.Economy;");
            sb.AppendLine("using EndlessEngine.Flow;");
            sb.AppendLine("using EndlessEngine.SaveAndLoad;");
            if (_modGenerator) sb.AppendLine("using EndlessEngine.Generator;");
            bool needConfig = _modGenerator || _modCursor || _modClick || _modZone || _modWave || _modPrestige || _modMultiCurrency;
            if (needConfig)   sb.AppendLine("using EndlessEngine.Config;");
            if (_modCursor || _modClick || _modZone) sb.AppendLine("using EndlessEngine.Modules;");
            if (_modMultiCurrency) sb.AppendLine("using EndlessEngine.Economy;");
            if (_modWave)    { sb.AppendLine("using EndlessEngine.Wave;"); sb.AppendLine("using EndlessEngine.Enemy;"); sb.AppendLine("using EndlessEngine.Health;"); }
            if (_modPrestige){ sb.AppendLine("using EndlessEngine.Prestige;"); }
            sb.AppendLine();
            sb.AppendLine($"/// <summary>");
            sb.AppendLine($"/// Bootstrap for {name}.");
            sb.AppendLine($"/// Game Type: {GameTypeDisplayName(_gameType)}");
            sb.AppendLine($"/// Generated by Endless Engine New Game Wizard.");
            sb.AppendLine($"/// Assign all [SerializeField] references in the Inspector, then press Play.");
            sb.AppendLine($"/// </summary>");
            sb.AppendLine("[DefaultExecutionOrder(-500)]");
            sb.AppendLine($"public class {name}Bootstrap : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine("    [Header(\"Core\")]");
            sb.AppendLine("    [SerializeField] private SaveService            _saveService;");
            sb.AppendLine("    [SerializeField] private EconomyService         _economyService;");
            sb.AppendLine("    [SerializeField] private GameFlowStateMachine   _gameFlow;");
            sb.AppendLine("    [SerializeField] private TickEngine             _tickEngine;");
            sb.AppendLine("    [SerializeField] private EconomyConfigSO        _economyConfig;");
            sb.AppendLine();

            if (_modGenerator)
            {
                sb.AppendLine("    [Header(\"Generator Module\")]");
                sb.AppendLine("    [SerializeField] private GeneratorSystem        _generatorSystem;");
                sb.AppendLine("    [SerializeField] private PassiveIncomeService   _passiveIncomeService;");
                sb.AppendLine("    [SerializeField] private GeneratorDatabaseSO    _generatorDatabase;");
                sb.AppendLine();
            }
            if (_modCursor)
            {
                sb.AppendLine("    [Header(\"Cursor Module\")]");
                sb.AppendLine("    [SerializeField] private CursorYieldService     _cursorYield;");
                sb.AppendLine("    [SerializeField] private CursorActivityConfigSO _cursorConfig;");
                sb.AppendLine();
            }
            if (_modClick)
            {
                sb.AppendLine("    [Header(\"Click Module\")]");
                sb.AppendLine("    [SerializeField] private ClickYieldService      _clickYield;");
                sb.AppendLine("    [SerializeField] private ClickSourceConfigSO    _clickConfig;");
                sb.AppendLine();
            }
            if (_modZone)
            {
                sb.AppendLine("    [Header(\"Zone Module\")]");
                sb.AppendLine("    [SerializeField] private ZoneSystem             _zoneSystem;");
                sb.AppendLine("    [SerializeField] private ZoneDatabaseSO         _zoneDatabase;");
                sb.AppendLine();
            }
            if (_modWave)
            {
                sb.AppendLine("    [Header(\"Wave / Combat Module\")]");
                sb.AppendLine("    [SerializeField] private WaveSpawnManager       _waveSpawnManager;");
                sb.AppendLine("    [SerializeField] private EnemyManager           _enemyManager;");
                sb.AppendLine("    [SerializeField] private HealthSystem           _healthSystem;");
                sb.AppendLine("    [SerializeField] private WaveConfigSO           _waveConfig;");
                sb.AppendLine("    [SerializeField] private RunConfigSO            _runConfig;");
                sb.AppendLine();
            }
            if (_modPrestige)
            {
                sb.AppendLine("    [Header(\"Prestige Module\")]");
                sb.AppendLine("    [SerializeField] private PrestigeStateManager   _prestigeManager;");
                sb.AppendLine("    [SerializeField] private PrestigeConfigSO       _prestigeConfig;");
                sb.AppendLine();
            }
            if (_modMultiCurrency)
            {
                sb.AppendLine("    [Header(\"Multi-Currency Module\")]");
                sb.AppendLine("    [SerializeField] private CurrencyService        _currencyService;");
                sb.AppendLine("    [SerializeField] private CurrencyDatabaseSO     _currencyDatabase;");
                sb.AppendLine();
            }

            sb.AppendLine("    private IEnumerator Start()");
            sb.AppendLine("    {");
            sb.AppendLine("        // 1. Economy");
            sb.AppendLine("        _economyService?.Initialize(upgradeTreeQuery: null, saveNotifier: _saveService);");
            sb.AppendLine();

            if (_modGenerator)
            {
                sb.AppendLine("        // 2. Generator");
                sb.AppendLine("        if (_generatorSystem != null && _economyService != null)");
                sb.AppendLine("        {");
                sb.AppendLine("            var cfgs = _generatorDatabase != null ? _generatorDatabase.Generators : new GeneratorConfigSO[0];");
                sb.AppendLine("            _generatorSystem.Initialize(cfgs, _economyService, _saveService);");
                sb.AppendLine("        }");
                sb.AppendLine("        if (_passiveIncomeService != null)");
                sb.AppendLine("            _passiveIncomeService.Initialize(_generatorSystem, _economyService, _gameFlow);");
                sb.AppendLine();
            }
            if (_modCursor)
            {
                sb.AppendLine("        // 3. Cursor yield");
                sb.AppendLine("        // _cursorYield?.Initialize(_cursorConfig, _economyService, _gameFlow, inputProvider);");
                sb.AppendLine("        // TODO: assign an IInputProvider before calling Initialize.");
                sb.AppendLine();
            }
            if (_modClick)
            {
                sb.AppendLine("        // 4. Click yield");
                sb.AppendLine("        // _clickYield?.Initialize(_clickConfig, _economyService);");
                sb.AppendLine("        // TODO: call Initialize with your IInputProvider if using one.");
                sb.AppendLine();
            }
            if (_modZone)
            {
                sb.AppendLine("        // 5. Zone system");
                sb.AppendLine("        // _zoneSystem?.Initialize(_zoneDatabase?.Zones, _economyService, _gameFlow, inputProvider, _saveService);");
                sb.AppendLine("        // TODO: assign an IInputProvider before calling Initialize.");
                sb.AppendLine();
            }
            if (_modPrestige)
            {
                sb.AppendLine("        // 6. Prestige wires itself via ISaveStateProvider.");
                sb.AppendLine("        // Registration handled below.");
                sb.AppendLine();
            }
            if (_modMultiCurrency)
            {
                sb.AppendLine("        // 6b. Multi-Currency");
                sb.AppendLine("        _currencyService?.Initialize(_currencyDatabase);");
                sb.AppendLine();
            }

            sb.AppendLine("        // 7. Register save providers");
            sb.AppendLine("        if (_saveService != null)");
            sb.AppendLine("        {");
            sb.AppendLine("            _saveService.RegisterStateProvider(_economyService);");
            if (_modGenerator)     sb.AppendLine("            if (_generatorSystem  != null) _saveService.RegisterStateProvider(_generatorSystem);");
            if (_modZone)          sb.AppendLine("            if (_zoneSystem        != null) _saveService.RegisterStateProvider(_zoneSystem);");
            if (_modPrestige)      sb.AppendLine("            if (_prestigeManager   != null) _saveService.RegisterStateProvider(_prestigeManager);");
            if (_modMultiCurrency) sb.AppendLine("            if (_currencyService   != null) _saveService.RegisterStateProvider(_currencyService);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // 8. Load save");
            sb.AppendLine("        if (_saveService != null)");
            sb.AppendLine("        {");
            sb.AppendLine("            bool done = false;");
            sb.AppendLine("            _ = _saveService.LoadAsync().ContinueWith(_ => done = true,");
            sb.AppendLine("                System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());");
            sb.AppendLine("            yield return new UnityEngine.WaitUntil(() => done);");
            sb.AppendLine("        }");
            sb.AppendLine("        else yield return null;");
            sb.AppendLine();
            sb.AppendLine("        Debug.Log($\"[{GetType().Name}] Bootstrap complete.\");");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(path, sb.ToString());
        }

        // ── Utilities ──────────────────────────────────────────────────────────────

        private static void CreateDirectories(params string[] paths)
        {
            foreach (var p in paths)
            {
                string full = Path.Combine(Application.dataPath, "..",
                    p.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(full))
                    Directory.CreateDirectory(full);
            }
        }

        private static string SanitizeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var sb = new System.Text.StringBuilder();
            bool cap = true;
            foreach (char c in raw)
            {
                if (char.IsLetter(c))
                {
                    sb.Append(cap ? char.ToUpper(c) : c);
                    cap = false;
                }
                else if (char.IsDigit(c))
                {
                    if (sb.Length > 0)
                        sb.Append(c);
                    cap = false;
                }
                else if (c == ' ' || c == '_' || c == '-')
                {
                    cap = true;
                }
            }
            return sb.ToString();
        }

        private void SetStatus(string msg, bool error)
        {
            if (_statusLabel == null) return;
            _statusLabel.text  = msg;
            _statusLabel.style.color = error
                ? new Color(1f, 0.4f, 0.4f)
                : new Color(0.4f, 1f, 0.5f);
        }
    }
}
