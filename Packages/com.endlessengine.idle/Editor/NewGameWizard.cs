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
    /// Generates a fully-wired, play-ready Unity scene plus all config assets for any
    /// idle/incremental game type. Press Generate → open scene → press Play.
    ///
    /// Supported game types:
    ///   Pure Idle        — Generator + Prestige. Classic idle accumulation.
    ///   Clicker Idle     — Generator + Tap/Click + Prestige. Active tapping.
    ///   Click Loop       — Full ClickLoopService: click targets with HP, combo, crit, offline.
    ///   Harvest Idle     — HarvestLoopService: drag cursor over world nodes, combo, offline.
    ///   Idle-vs / RPG    — Wave auto-battle + Generator + Prestige + Multi-Currency.
    ///   Tower Defense    — Wave combat on a path + tower slot placeholders.
    ///   Merge Idle       — Merge 2×tier-N items → 1×tier-(N+1). No generators.
    ///   Farm Idle        — Grid building + generators + prestige.
    ///   Research Idle    — Long research queues gate progression.
    ///   Building Idle    — Grid city with zone income.
    ///   Prestige-Heavy   — Full prestige + ascension + multi-currency.
    ///   Custom           — All toggles off — configure manually.
    /// </summary>
    public class NewGameWizard : EditorWindow
    {
        // ── Game type enum ────────────────────────────────────────────────────────

        private enum GameType
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

        // ── State ─────────────────────────────────────────────────────────────────

        private string   _gameName  = "MyIdleGame";
        private GameType _gameType  = GameType.PureIdle;

        private bool _modGenerator     = true;
        private bool _modClick         = false;
        private bool _modClickLoop     = false;
        private bool _modHarvest       = false;
        private bool _modCursor        = false;
        private bool _modZone          = false;
        private bool _modWave          = false;
        private bool _modPrestige      = true;
        private bool _modMultiCurrency = false;
        private bool _modResearch      = false;
        private bool _modBuilding      = false;
        private bool _modMerge         = false;

        private TextField _nameField;
        private Label     _previewLabel;
        private Label     _statusLabel;
        private Label     _presetDescLabel;

        // ── Menu ──────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Endless Engine/New Game Wizard", priority = 0)]
        public static void Open()
        {
            var win = GetWindow<NewGameWizard>(utility: true, title: "New Game Wizard");
            win.minSize = new Vector2(560, 680);
            win.maxSize = new Vector2(760, 880);
        }

        // ── GUI ───────────────────────────────────────────────────────────────────

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop    = 12;
            root.style.paddingBottom = 12;
            root.style.paddingLeft   = 16;
            root.style.paddingRight  = 16;

            // Header
            var header = new Label("New Game Wizard");
            header.style.fontSize = 18;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 3;
            root.Add(header);

            var sub = new Label("Generates a fully-wired, play-ready scene + all config assets.");
            sub.style.fontSize    = 11;
            sub.style.color       = new Color(0.55f, 0.55f, 0.55f);
            sub.style.whiteSpace  = WhiteSpace.Normal;
            sub.style.marginBottom = 12;
            root.Add(sub);

            // Game name
            root.Add(SectionLabel("Project"));
            var nameRow = Row();
            var nl = Bold("Game Name", 90);
            _nameField = new TextField { value = _gameName };
            _nameField.style.flexGrow = 1;
            _nameField.RegisterValueChangedCallback(evt => { _gameName = evt.newValue; RefreshPreview(); });
            nameRow.Add(nl); nameRow.Add(_nameField);
            root.Add(nameRow);

            // Game type
            root.Add(SectionLabel("Game Type"));
            var typeRow = Row();
            typeRow.style.marginBottom = 3;
            var tl = Bold("Type", 90);
            var typeNames = new List<string>();
            foreach (GameType t in System.Enum.GetValues(typeof(GameType)))
                typeNames.Add(DisplayName(t));
            var typeDD = new DropdownField(typeNames, (int)_gameType);
            typeDD.style.flexGrow = 1;
            typeDD.RegisterValueChangedCallback(evt =>
            {
                _gameType = (GameType)typeNames.IndexOf(evt.newValue);
                ApplyPreset(_gameType);
                RefreshPreview();
                rootVisualElement.Clear();
                CreateGUI();
            });
            typeRow.Add(tl); typeRow.Add(typeDD);
            root.Add(typeRow);

            _presetDescLabel = new Label(Desc(_gameType));
            _presetDescLabel.style.fontSize     = 10;
            _presetDescLabel.style.color        = new Color(0.6f, 0.88f, 0.6f);
            _presetDescLabel.style.whiteSpace   = WhiteSpace.Normal;
            _presetDescLabel.style.marginBottom = 10;
            _presetDescLabel.style.marginLeft   = 92;
            root.Add(_presetDescLabel);

            // Module toggles
            root.Add(SectionLabel("Modules  (auto-set — override if needed)"));
            root.Add(CoreRow());

            root.Add(ModRow("Generator",     "GeneratorSystem · PassiveIncomeService",
                _modGenerator,  v => { _modGenerator = v; RefreshPreview(); }));
            root.Add(ModRow("Click (simple)", "ClickTargetHandler (tap sphere → gold)",
                _modClick,      v => { _modClick = v; RefreshPreview(); }));
            root.Add(ModRow("Click Loop",    "ClickLoopService · ClickTarget HP/combo/crit/offline",
                _modClickLoop,  v => { _modClickLoop = v; RefreshPreview(); }));
            root.Add(ModRow("Harvest",       "HarvestLoopService · HarvestCursor · combo/offline",
                _modHarvest,    v => { _modHarvest = v; RefreshPreview(); }));
            root.Add(ModRow("Cursor",        "CursorYieldService  (Speed / Distance / Hover)",
                _modCursor,     v => { _modCursor = v; RefreshPreview(); }));
            root.Add(ModRow("Zone",          "ZoneSystem  (world-space income zones)",
                _modZone,       v => { _modZone = v; RefreshPreview(); }));
            root.Add(ModRow("Wave / Combat", "WaveSpawnManager · EnemyManager · AutoBattle",
                _modWave,       v => { _modWave = v; RefreshPreview(); }));
            root.Add(ModRow("Prestige",      "PrestigeStateManager  (reset → multiplier)",
                _modPrestige,   v => { _modPrestige = v; RefreshPreview(); }));
            root.Add(ModRow("Research",      "ResearchService  (time-gated tech tree)",
                _modResearch,   v => { _modResearch = v; RefreshPreview(); }));
            root.Add(ModRow("Building",      "BuildingService  (place/upgrade buildings)",
                _modBuilding,   v => { _modBuilding = v; RefreshPreview(); }));
            root.Add(ModRow("Merge",         "MergeService · InventoryService  (2×N → N+1)",
                _modMerge,      v => { _modMerge = v; RefreshPreview(); }));
            root.Add(ModRow("Multi-Currency","CurrencyService  (gems · tokens · secondary)",
                _modMultiCurrency, v => { _modMultiCurrency = v; RefreshPreview(); }));

            // File preview
            root.Add(SectionLabel("Files to Create"));
            var scroll = new ScrollView();
            scroll.style.height          = 90;
            scroll.style.backgroundColor = new Color(0.11f, 0.11f, 0.11f);
            scroll.style.paddingTop      = 5;
            scroll.style.paddingLeft     = 8;
            scroll.style.marginBottom    = 10;
            scroll.style.borderTopLeftRadius     = 4;
            scroll.style.borderTopRightRadius    = 4;
            scroll.style.borderBottomLeftRadius  = 4;
            scroll.style.borderBottomRightRadius = 4;
            _previewLabel = new Label();
            _previewLabel.style.fontSize   = 10;
            _previewLabel.style.color      = new Color(0.7f, 0.85f, 0.7f);
            _previewLabel.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(_previewLabel);
            root.Add(scroll);

            // Status
            _statusLabel = new Label();
            _statusLabel.style.fontSize     = 11;
            _statusLabel.style.whiteSpace   = WhiteSpace.Normal;
            _statusLabel.style.marginBottom = 8;
            root.Add(_statusLabel);

            // Generate button
            var btn = new Button(Generate) { text = "  Generate Skeleton  " };
            btn.style.height          = 38;
            btn.style.fontSize        = 13;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.backgroundColor = new Color(0.13f, 0.40f, 0.13f);
            btn.style.color           = Color.white;
            btn.style.borderTopLeftRadius     = 4;
            btn.style.borderTopRightRadius    = 4;
            btn.style.borderBottomLeftRadius  = 4;
            btn.style.borderBottomRightRadius = 4;
            root.Add(btn);

            ApplyPreset(_gameType);
            RefreshPreview();
        }

        // ── Display names ─────────────────────────────────────────────────────────

        private static string DisplayName(GameType t) => t switch
        {
            GameType.PureIdle      => "Pure Idle",
            GameType.ClickerIdle   => "Clicker Idle",
            GameType.ClickLoop     => "Click Loop (full)",
            GameType.HarvestIdle   => "Harvest Idle",
            GameType.IdleVsRPG     => "Idle-vs / RPG",
            GameType.TowerDefense  => "Tower Defense",
            GameType.MergeIdle     => "Merge Idle",
            GameType.FarmIdle      => "Farm Idle",
            GameType.ResearchIdle  => "Research Idle",
            GameType.BuildingIdle  => "Building Idle",
            GameType.PrestigeHeavy => "Prestige-Heavy",
            _                      => "Custom",
        };

        private static string Desc(GameType t) => t switch
        {
            GameType.PureIdle      => "Passive income from generators. Buy upgrades. Prestige for multiplier. The classic idle formula.",
            GameType.ClickerIdle   => "Tap an orange sphere to earn gold + passive generators. Combo builds with rapid taps.",
            GameType.ClickLoop     => "Click targets with HP bars — destroy them for gold. Combo × crit × auto-click × offline.",
            GameType.HarvestIdle   => "Drag cursor over green harvest nodes. Combo builds as you harvest. Nodes respawn.",
            GameType.IdleVsRPG     => "Auto-battle waves of enemies. Generators fund upgrades between waves. Prestige after runs.",
            GameType.TowerDefense  => "Enemies walk a path. Tower placeholders earn income. Wave auto-combat built-in.",
            GameType.MergeIdle     => "Combine 2×tier-N items into 1×tier-(N+1). Sell for gold. No passive income.",
            GameType.FarmIdle      => "Plant & harvest farm plots (generator analogy). Building placement income.",
            GameType.ResearchIdle  => "Generators fund research. Unlock upgrades via time-gated tech tree.",
            GameType.BuildingIdle  => "Place buildings on a grid — each produces income per tick.",
            GameType.PrestigeHeavy => "All prestige layers (Prestige + Ascension) + multi-currency + generators.",
            _                      => "All modules off. Configure manually.",
        };

        // ── Presets ───────────────────────────────────────────────────────────────

        private void ApplyPreset(GameType t)
        {
            _modGenerator = _modClick = _modClickLoop = _modHarvest = _modCursor =
            _modZone = _modWave = _modPrestige = _modMultiCurrency =
            _modResearch = _modBuilding = _modMerge = false;

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
                case GameType.ClickLoop:
                    _modClickLoop = true;
                    _modGenerator = true;
                    _modPrestige  = true;
                    break;
                case GameType.HarvestIdle:
                    _modHarvest   = true;
                    _modGenerator = true;
                    _modPrestige  = true;
                    break;
                case GameType.IdleVsRPG:
                    _modGenerator     = true;
                    _modWave          = true;
                    _modPrestige      = true;
                    _modMultiCurrency = true;
                    break;
                case GameType.TowerDefense:
                    _modGenerator     = true;
                    _modWave          = true;
                    _modPrestige      = true;
                    break;
                case GameType.MergeIdle:
                    _modMerge = true;
                    break;
                case GameType.FarmIdle:
                    _modGenerator = true;
                    _modBuilding  = true;
                    _modPrestige  = true;
                    break;
                case GameType.ResearchIdle:
                    _modGenerator     = true;
                    _modPrestige      = true;
                    _modResearch      = true;
                    _modMultiCurrency = true;
                    break;
                case GameType.BuildingIdle:
                    _modGenerator = true;
                    _modZone      = true;
                    _modBuilding  = true;
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

        // ── Economy preset values ─────────────────────────────────────────────────

        private void ApplyEconomyPreset(EndlessEngine.Config.EconomyConfigSO so)
        {
            switch (_gameType)
            {
                case GameType.PureIdle:
                    so.IdleYieldRateBase        = 0.5f;
                    so.BaseMultiplierPerPrestige = 1.5f;
                    so.ResourceHardCap          = 10_000_000_000L;
                    so.OfflineCapHours          = 12f;
                    break;
                case GameType.ClickerIdle:
                case GameType.ClickLoop:
                    so.IdleYieldRateBase        = 0.1f;
                    so.BaseMultiplierPerPrestige = 1.5f;
                    so.ResourceHardCap          = 1_000_000_000L;
                    so.OfflineCapHours          = 8f;
                    break;
                case GameType.HarvestIdle:
                    so.IdleYieldRateBase        = 0f;
                    so.BaseMultiplierPerPrestige = 1.5f;
                    so.ResourceHardCap          = 1_000_000_000L;
                    so.OfflineCapHours          = 8f;
                    break;
                case GameType.IdleVsRPG:
                case GameType.TowerDefense:
                    so.IdleYieldRateBase        = 0.2f;
                    so.BaseMultiplierPerPrestige = 2.0f;
                    so.ResourceHardCap          = 100_000_000_000L;
                    so.OfflineCapHours          = 8f;
                    break;
                case GameType.MergeIdle:
                    so.IdleYieldRateBase        = 0f;
                    so.BaseMultiplierPerPrestige = 1.0f;
                    so.ResourceHardCap          = 1_000_000L;
                    so.OfflineCapHours          = 0f;
                    break;
                case GameType.FarmIdle:
                    so.IdleYieldRateBase        = 0.4f;
                    so.BaseMultiplierPerPrestige = 1.6f;
                    so.ResourceHardCap          = 10_000_000_000L;
                    so.OfflineCapHours          = 16f;
                    break;
                case GameType.ResearchIdle:
                    so.IdleYieldRateBase        = 0.3f;
                    so.BaseMultiplierPerPrestige = 1.8f;
                    so.ResourceHardCap          = 1_000_000_000_000L;
                    so.OfflineCapHours          = 24f;
                    break;
                case GameType.BuildingIdle:
                    so.IdleYieldRateBase        = 0.4f;
                    so.BaseMultiplierPerPrestige = 1.6f;
                    so.ResourceHardCap          = 10_000_000_000L;
                    so.OfflineCapHours          = 12f;
                    break;
                case GameType.PrestigeHeavy:
                    so.IdleYieldRateBase        = 0.5f;
                    so.BaseMultiplierPerPrestige = 3.0f;
                    so.ResourceHardCap          = 1_000_000_000_000_000L;
                    so.OfflineCapHours          = 8f;
                    break;
            }
        }

        private void ApplyPrestigePreset(EndlessEngine.Config.PrestigeConfigSO so)
        {
            switch (_gameType)
            {
                case GameType.IdleVsRPG:
                case GameType.TowerDefense:
                    so.BaseMultiplierPerPrestige = 2.0f;
                    so.MaxPermanentMultiplier    = 1000f;
                    so.MinWaveForPrestige        = 10;
                    break;
                case GameType.PrestigeHeavy:
                    so.BaseMultiplierPerPrestige = 3.0f;
                    so.MaxPermanentMultiplier    = 10_000f;
                    so.MinWaveForPrestige        = 0;
                    break;
                case GameType.ResearchIdle:
                    so.BaseMultiplierPerPrestige = 1.8f;
                    so.MaxPermanentMultiplier    = 500f;
                    so.MinWaveForPrestige        = 0;
                    break;
                default:
                    // Non-wave game types: gate on gold instead of waves
                    so.BaseMultiplierPerPrestige = 1.5f;
                    so.MaxPermanentMultiplier    = 200f;
                    so.MinWaveForPrestige        = 0;
                    so.MinGoldToPrestige         = 1_000;
                    break;
            }
        }

        private void ApplyWavePreset(EndlessEngine.Config.WaveConfigSO so)
        {
            so.TotalWavesPerRun       = _gameType == GameType.IdleVsRPG ? 50 : 30;
            so.BaseEnemyCountPerWave  = _gameType == GameType.IdleVsRPG ? 5 : 3;
            so.EliteWaveInterval      = 10;
        }

        // ── File list preview ─────────────────────────────────────────────────────

        private List<string> BuildFileList()
        {
            string r = $"Assets/{Sanitize(_gameName)}/";
            var l = new List<string>();
            l.Add($"{r}Configs/EconomyConfig.asset  [{DisplayName(_gameType)} preset]");
            l.Add($"{r}Configs/SchemaVersion.asset");
            if (_modGenerator)  { l.Add($"{r}Configs/GoldMine.asset");
                                  l.Add($"{r}Configs/GeneratorDatabase.asset"); }
            if (_modClickLoop)  l.Add($"{r}Configs/ClickLoopConfig.asset");
            if (_modClickLoop)  l.Add($"{r}Configs/ClickTarget_0.asset  (×3)");
            if (_modHarvest)    l.Add($"{r}Configs/HarvestAreaConfig.asset");
            if (_modHarvest)    l.Add($"{r}Configs/HarvestNode.asset");
            if (_modCursor)     l.Add($"{r}Configs/CursorActivityConfig.asset");
            if (_modClick)      l.Add($"{r}Configs/ClickSourceConfig.asset");
            if (_modZone)       l.Add($"{r}Configs/ZoneDatabase.asset");
            if (_modWave)       { l.Add($"{r}Configs/WaveConfig.asset");
                                  l.Add($"{r}Configs/EnemyStatConfig.asset"); }
            if (_modPrestige)   l.Add($"{r}Configs/PrestigeConfig.asset");
            if (_modResearch)   l.Add($"{r}Configs/ResearchDatabase.asset");
            if (_modMerge)      l.Add($"{r}Configs/StarterMergeConfig.asset");
            if (_modBuilding)   l.Add($"{r}Configs/StarterBuilding.asset");
            if (_modMultiCurrency) l.Add($"{r}Configs/CurrencyDatabase.asset");
            l.Add($"{r}Configs/UpgradeTreeConfig.asset  (upgrade tree for UI screen)");
            l.Add($"{r}Configs/Upgrades/  ({UpgradeNodeCount(_gameType)} UpgradeNodeConfigSO files for game logic)");
            l.Add($"");
            l.Add($"{r}Scenes/{Sanitize(_gameName)}.unity  ← Open this, then press Play");
            return l;
        }

        private static int UpgradeNodeCount(GameType t) => t switch
        {
            GameType.MergeIdle => 3,
            _                  => 4,
        };

        private void RefreshPreview()
        {
            if (_previewLabel == null) return;
            _previewLabel.text = string.Join("\n", BuildFileList());
        }

        // ── Generation ────────────────────────────────────────────────────────────

        private void Generate()
        {
            string name = Sanitize(_gameName);
            if (string.IsNullOrEmpty(name)) { SetStatus("Name cannot be empty.", true); return; }

            string root     = $"Assets/{name}";
            string cfgPath  = $"{root}/Configs";
            string scenePath = $"{root}/Scenes";

            CreateDirs(root, cfgPath, scenePath);
            CreateConfigs(cfgPath);
            AssetDatabase.Refresh();

            var opts = new SceneSetupUtility.SetupOptions
            {
                GameName         = name,
                ScenesPath       = scenePath,
                ConfigsPath      = cfgPath,
                UpgradeTreePath  = $"{cfgPath}/UpgradeTreeConfig.asset",
                Type             = (SceneSetupUtility.GameType)(int)_gameType,
                HasGenerator     = _modGenerator,
                HasPrestige      = _modPrestige,
                HasMultiCurrency = _modMultiCurrency,
                HasWave          = _modWave,
                HasClick         = _modClick,
                HasCursor        = _modCursor,
                HasZone          = _modZone,
                HasHarvest       = _modHarvest,
                HasClickLoop     = _modClickLoop,
                HasBuilding      = _modBuilding,
            };

            bool ok = SceneSetupUtility.BuildScene(opts);
            AssetDatabase.Refresh();

            if (ok)
            {
                SetStatus($"Done! Open Assets/{name}/Scenes/{name}.unity → Press Play", false);
                Debug.Log($"[NewGameWizard] '{name}' ({DisplayName(_gameType)}) → Assets/{name}/");
            }
            else
            {
                SetStatus("Scene build had an issue — check Console.", true);
            }
        }

        // ── Config creation ───────────────────────────────────────────────────────

        private void CreateConfigs(string dir)
        {
            var econ = CreateSO<EndlessEngine.Config.EconomyConfigSO>(dir, "EconomyConfig");
            if (econ != null) { ApplyEconomyPreset(econ); EditorUtility.SetDirty(econ); }

            var schema = CreateSO<EndlessEngine.Config.SchemaVersionSO>(dir, "SchemaVersion");
            if (schema != null) EditorUtility.SetDirty(schema);

            if (_modGenerator)
            {
                var mine = CreateSO<EndlessEngine.Config.GeneratorConfigSO>(dir, "GoldMine");
                if (mine != null)
                {
                    mine.GeneratorId        = "gold_mine";
                    mine.DisplayName        = "Gold Mine";
                    mine.Description        = "Passively produces gold.";
                    mine.BaseYieldPerSecond = 1f;
                    mine.BaseCost           = 50;
                    mine.CostScalingFactor  = 1.15f;
                    EditorUtility.SetDirty(mine);
                }
                AssetDatabase.Refresh();
                var db = CreateSO<EndlessEngine.Config.GeneratorDatabaseSO>(dir, "GeneratorDatabase");
                if (db == null) db = AssetDatabase.LoadAssetAtPath<EndlessEngine.Config.GeneratorDatabaseSO>(
                    $"{dir}/GeneratorDatabase.asset");
                if (db != null && mine != null)
                {
                    db.Generators = new[] { mine };
                    EditorUtility.SetDirty(db);
                }
            }

            if (_modClickLoop)
            {
                var clCfg = CreateSO<EndlessEngine.ClickLoop.ClickLoopConfigSO>(dir, "ClickLoopConfig");
                if (clCfg != null) EditorUtility.SetDirty(clCfg);

                // 3 click target configs
                for (int i = 0; i < 3; i++)
                {
                    var ct = CreateSO<EndlessEngine.ClickLoop.ClickTargetConfigSO>(dir, $"ClickTarget_{i}");
                    if (ct != null)
                    {
                        ct.TargetId    = $"target_{i}";
                        ct.DisplayName = $"Target {i + 1}";
                        ct.MaxHP       = 10f + i * 5f;
                        ct.BaseYield   = 3f + i * 2f;
                        ct.RespawnSeconds = 3f;
                        EditorUtility.SetDirty(ct);
                    }
                }
            }

            if (_modHarvest)
            {
                var area = CreateSO<EndlessEngine.Harvest.HarvestAreaConfigSO>(dir, "HarvestAreaConfig");
                if (area != null) EditorUtility.SetDirty(area);

                var node = CreateSO<EndlessEngine.Harvest.HarvestNodeConfigSO>(dir, "HarvestNode");
                if (node != null)
                {
                    node.NodeId        = "default_node";
                    node.DisplayName   = "Resource Node";
                    node.MaxHP         = 10f;
                    node.DamagePerTick = 1f;
                    node.BaseYield     = 5f;
                    node.RespawnSeconds = 4f;
                    node.AwardYieldPerTick = true;
                    EditorUtility.SetDirty(node);
                }
            }

            if (_modClick)
                CreateSO<EndlessEngine.Config.ClickSourceConfigSO>(dir, "ClickSourceConfig");

            if (_modCursor)
                CreateSO<EndlessEngine.Config.CursorActivityConfigSO>(dir, "CursorActivityConfig");

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
                var pres = CreateSO<EndlessEngine.Config.PrestigeConfigSO>(dir, "PrestigeConfig");
                if (pres != null) { ApplyPrestigePreset(pres); EditorUtility.SetDirty(pres); }
            }

            if (_modMultiCurrency)
                CreateSO<EndlessEngine.Config.CurrencyDatabaseSO>(dir, "CurrencyDatabase");

            if (_modBuilding)
                CreateSO<EndlessEngine.Config.BuildingConfigSO>(dir, "StarterBuilding");

            if (_modResearch)
                CreateSO<EndlessEngine.Config.ResearchTreeConfigSO>(dir, "ResearchDatabase");

            if (_modMerge)
                CreateSO<EndlessEngine.Config.MergeConfigSO>(dir, "StarterMergeConfig");

            CreateUpgradeNodes(dir);
        }

        private void CreateUpgradeNodes(string dir)
        {
            // UpgradeTreeConfigSO — used by UpgradeScreenController (UI visual tree)
            var tree = CreateSO<EndlessEngine.Config.UpgradeTreeConfigSO>(dir, "UpgradeTreeConfig");
            if (tree != null)
            {
                tree.ProgressiveReveal = true;
                tree.Nodes             = BuildUpgradeNodes(_gameType);
                EditorUtility.SetDirty(tree);
            }

            // UpgradeNodeConfigSO assets — used by ConfigRegistry + UpgradeTreeService (game logic)
            // Written into a sub-folder so they don't clutter the Configs root
            string upgradeDir = $"{dir}/Upgrades";
            string fullPath   = System.IO.Path.Combine(Application.dataPath, "..",
                upgradeDir.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (!System.IO.Directory.Exists(fullPath))
                System.IO.Directory.CreateDirectory(fullPath);

            var defs = BuildUpgradeNodes(_gameType);
            foreach (var def in defs)
                CreateUpgradeNodeSO(upgradeDir, def);
        }

        private static void CreateUpgradeNodeSO(string dir, EndlessEngine.Config.UpgradeNodeDefinition def)
        {
            var node = CreateSO<EndlessEngine.Config.UpgradeNodeConfigSO>(dir, def.NodeId);
            if (node == null) return;
            node.NodeId              = def.NodeId;
            node.DisplayName         = def.DisplayName;
            node.Description         = def.Description;
            node.MaxRank             = def.MaxRank;
            node.BaseCost            = def.BaseCost;
            node.CostScalingFactor   = def.CostScalingFactor;
            node.AffectedStat        = def.AffectedStat;
            node.EffectPerRank       = def.EffectPerRank;
            node.EffectType          = def.EffectType;
            node.SelectionWeight     = def.SelectionWeight;
            node.PrerequisiteNodeIDs = def.PrerequisiteNodeIDs;
            EditorUtility.SetDirty(node);
        }

        private static System.Collections.Generic.List<EndlessEngine.Config.UpgradeNodeDefinition>
            BuildUpgradeNodes(GameType type)
        {
            var list = new System.Collections.Generic.List<EndlessEngine.Config.UpgradeNodeDefinition>();
            switch (type)
            {
                case GameType.PureIdle:
                case GameType.ClickerIdle:
                case GameType.ResearchIdle:
                case GameType.PrestigeHeavy:
                    list.Add(MakeDef("yield_1", "Yield Boost I",
                        "Increases generator output by 20% per rank.",
                        EndlessEngine.Config.StatType.GeneratorSpeed, 0.20f, 100, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Production, 0, 0));
                    list.Add(MakeDef("yield_2", "Yield Boost II",
                        "Further increases generator output.",
                        EndlessEngine.Config.StatType.GeneratorSpeed, 0.25f, 300, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Production, 1, 0,
                        prereq: "yield_1"));
                    list.Add(MakeDef("offline_1", "Offline Bonus",
                        "Increases offline yield rate by 30% per rank.",
                        EndlessEngine.Config.StatType.OfflineYieldRate, 0.30f, 200, 1.6f,
                        EndlessEngine.Config.UpgradeCategory.Production, 0, 1));
                    list.Add(MakeDef("prestige_multi_1", "Prestige Edge",
                        "Increases prestige multiplier by 10% per rank.",
                        EndlessEngine.Config.StatType.PrestigeMultiplier, 0.10f, 500, 1.8f,
                        EndlessEngine.Config.UpgradeCategory.Prestige, 2, 0,
                        prereq: "yield_2"));
                    break;

                case GameType.ClickLoop:
                    list.Add(MakeDef("click_dmg_1", "Click Power I",
                        "Increases click damage by 20% per rank.",
                        EndlessEngine.Config.StatType.ClickDamage, 0.20f, 75, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Combat, 0, 0));
                    list.Add(MakeDef("click_dmg_2", "Click Power II",
                        "Further increases click damage.",
                        EndlessEngine.Config.StatType.ClickDamage, 0.25f, 200, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Combat, 1, 0,
                        prereq: "click_dmg_1"));
                    list.Add(MakeDef("combo_1", "Combo Master",
                        "Boosts click combo multiplier.",
                        EndlessEngine.Config.StatType.ClickComboMultiplier, 0.15f, 150, 1.6f,
                        EndlessEngine.Config.UpgradeCategory.Economy, 0, 1));
                    list.Add(MakeDef("click_yield_1", "Yield Multiplier",
                        "Increases click gold yield.",
                        EndlessEngine.Config.StatType.ClickYieldMultiplier, 0.20f, 250, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Economy, 1, 1,
                        prereq: "combo_1"));
                    break;

                case GameType.HarvestIdle:
                    list.Add(MakeDef("harvest_yield_1", "Harvest Boost",
                        "Increases harvest yield by 20% per rank.",
                        EndlessEngine.Config.StatType.HarvestYieldMultiplier, 0.20f, 75, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Production, 0, 0));
                    list.Add(MakeDef("harvest_radius_1", "Wide Harvest",
                        "Increases cursor harvest radius.",
                        EndlessEngine.Config.StatType.HarvestRadius, 0.15f, 100, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Production, 1, 0));
                    list.Add(MakeDef("harvest_combo_1", "Harvest Combo",
                        "Boosts harvest combo multiplier.",
                        EndlessEngine.Config.StatType.HarvestComboMultiplier, 0.20f, 150, 1.6f,
                        EndlessEngine.Config.UpgradeCategory.Economy, 0, 1,
                        prereq: "harvest_yield_1"));
                    list.Add(MakeDef("harvest_respawn_1", "Quick Respawn",
                        "Decreases node respawn time.",
                        EndlessEngine.Config.StatType.HarvestNodeRespawnRate, 0.15f, 200, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Production, 1, 1,
                        prereq: "harvest_radius_1"));
                    break;

                case GameType.IdleVsRPG:
                case GameType.TowerDefense:
                    list.Add(MakeDef("damage_1", "Attack Boost I",
                        "Increases attack damage by 20% per rank.",
                        EndlessEngine.Config.StatType.Damage, 0.20f, 100, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Combat, 0, 0));
                    list.Add(MakeDef("damage_2", "Attack Boost II",
                        "Further increases attack damage.",
                        EndlessEngine.Config.StatType.Damage, 0.25f, 300, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Combat, 1, 0,
                        prereq: "damage_1"));
                    list.Add(MakeDef("crit_1", "Critical Hit",
                        "Increases critical hit chance.",
                        EndlessEngine.Config.StatType.CritChance, 0.05f, 200, 1.6f,
                        EndlessEngine.Config.UpgradeCategory.Combat, 0, 1));
                    list.Add(MakeDef("gold_drop_1", "Gold Rush",
                        "Increases gold dropped by enemies.",
                        EndlessEngine.Config.StatType.GoldDropMultiplier, 0.30f, 250, 1.6f,
                        EndlessEngine.Config.UpgradeCategory.Economy, 1, 1,
                        prereq: "damage_2"));
                    break;

                case GameType.MergeIdle:
                    list.Add(MakeDef("merge_gold_1", "Sell Bonus",
                        "Increases gold earned from selling items.",
                        EndlessEngine.Config.StatType.GoldDropMultiplier, 0.25f, 50, 1.4f,
                        EndlessEngine.Config.UpgradeCategory.Economy, 0, 0));
                    list.Add(MakeDef("merge_idle_1", "Passive Income",
                        "Adds a small passive gold rate.",
                        EndlessEngine.Config.StatType.IdleYieldRate, 0.10f, 100, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Production, 1, 0));
                    list.Add(MakeDef("merge_gold_2", "Sell Bonus II",
                        "Further increases gold from selling.",
                        EndlessEngine.Config.StatType.GoldDropMultiplier, 0.30f, 200, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Economy, 0, 1,
                        prereq: "merge_gold_1"));
                    break;

                case GameType.FarmIdle:
                case GameType.BuildingIdle:
                    list.Add(MakeDef("build_yield_1", "Yield Boost I",
                        "Increases building income by 20% per rank.",
                        EndlessEngine.Config.StatType.GeneratorSpeed, 0.20f, 100, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Production, 0, 0));
                    list.Add(MakeDef("build_yield_2", "Yield Boost II",
                        "Further increases building income.",
                        EndlessEngine.Config.StatType.GeneratorSpeed, 0.25f, 300, 1.5f,
                        EndlessEngine.Config.UpgradeCategory.Production, 1, 0,
                        prereq: "build_yield_1"));
                    list.Add(MakeDef("build_offline_1", "Offline Bonus",
                        "Increases offline yield.",
                        EndlessEngine.Config.StatType.OfflineYieldRate, 0.30f, 200, 1.6f,
                        EndlessEngine.Config.UpgradeCategory.Production, 0, 1));
                    list.Add(MakeDef("build_prestige_1", "Prestige Edge",
                        "Increases prestige multiplier.",
                        EndlessEngine.Config.StatType.PrestigeMultiplier, 0.10f, 500, 1.8f,
                        EndlessEngine.Config.UpgradeCategory.Prestige, 1, 1,
                        prereq: "build_yield_2"));
                    break;
            }
            return list;
        }

        private static EndlessEngine.Config.UpgradeNodeDefinition MakeDef(
            string id, string displayName, string desc,
            EndlessEngine.Config.StatType stat, float effectPerRank,
            float baseCost, float costScale,
            EndlessEngine.Config.UpgradeCategory category,
            int gridX, int gridY,
            string prereq = null)
        {
            return new EndlessEngine.Config.UpgradeNodeDefinition
            {
                NodeId               = id,
                DisplayName          = displayName,
                Description          = desc,
                AffectedStat         = stat,
                EffectPerRank        = effectPerRank,
                EffectType           = EndlessEngine.Config.UpgradeEffectType.PercentBonus,
                MaxRank              = 5,
                BaseCost             = baseCost,
                CostScalingFactor    = costScale,
                Category             = category,
                GridX                = gridX,
                GridY                = gridY,
                SelectionWeight      = 10f,
                PrerequisiteNodeIDs  = string.IsNullOrEmpty(prereq)
                                           ? System.Array.Empty<string>()
                                           : new[] { prereq },
            };
        }

        // ── SO factory ────────────────────────────────────────────────────────────

        private static T CreateSO<T>(string dir, string assetName) where T : ScriptableObject
        {
            string path = $"{dir}/{assetName}.asset";
            if (File.Exists(Path.Combine(Application.dataPath, "..", path))) return null;
            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        // ── UI helpers ────────────────────────────────────────────────────────────

        private static Label SectionLabel(string text)
        {
            var l = new Label(text);
            l.style.fontSize   = 11;
            l.style.color      = new Color(0.5f, 0.72f, 1f);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop  = 8;
            l.style.marginBottom = 3;
            return l;
        }

        private static VisualElement Row()
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems    = Align.Center;
            r.style.marginBottom  = 6;
            return r;
        }

        private static Label Bold(string text, float width)
        {
            var l = new Label(text);
            l.style.width       = width;
            l.style.fontSize    = 12;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        private static VisualElement CoreRow() => MakeRow(
            isCore: true, name: "Core (always)",
            tag:    "TickEngine · EconomyService · SaveService · UpgradeTree",
            value:  true, onChange: null);

        private static VisualElement ModRow(string name, string tag, bool val, System.Action<bool> cb)
            => MakeRow(isCore: false, name: name, tag: tag, value: val, onChange: cb);

        private static VisualElement MakeRow(bool isCore, string name, string tag,
            bool value, System.Action<bool> onChange)
        {
            var row = new VisualElement();
            row.style.flexDirection  = FlexDirection.Row;
            row.style.alignItems     = Align.Center;
            row.style.height         = 30;
            row.style.marginBottom   = 2;
            row.style.paddingLeft    = 8;
            row.style.paddingRight   = 8;
            row.style.backgroundColor = isCore ? new Color(0.17f, 0.21f, 0.17f) : new Color(0.15f, 0.15f, 0.15f);
            row.style.borderTopLeftRadius     = 3;
            row.style.borderTopRightRadius    = 3;
            row.style.borderBottomLeftRadius  = 3;
            row.style.borderBottomRightRadius = 3;

            var tog = new Toggle { value = value };
            tog.style.marginRight = 6;
            if (isCore) tog.SetEnabled(false);
            else tog.RegisterValueChangedCallback(e => onChange?.Invoke(e.newValue));

            var nl = new Label(name);
            nl.style.fontSize = 11;
            nl.style.unityFontStyleAndWeight = FontStyle.Bold;
            nl.style.minWidth = 100;

            var spacer = new VisualElement(); spacer.style.flexGrow = 1;

            var tl = new Label(tag);
            tl.style.fontSize   = 9;
            tl.style.color      = new Color(0.5f, 0.5f, 0.5f);
            tl.style.unityTextAlign = TextAnchor.MiddleRight;
            tl.style.overflow   = Overflow.Hidden;
            tl.style.flexShrink = 1;
            tl.style.maxWidth   = 300;

            row.Add(tog); row.Add(nl); row.Add(spacer); row.Add(tl);
            return row;
        }

        // ── Utilities ─────────────────────────────────────────────────────────────

        private static void CreateDirs(params string[] paths)
        {
            foreach (var p in paths)
            {
                string full = Path.Combine(Application.dataPath, "..",
                    p.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(full)) Directory.CreateDirectory(full);
            }
        }

        private static string Sanitize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var sb = new System.Text.StringBuilder();
            bool cap = true;
            foreach (char c in raw)
            {
                if (char.IsLetter(c)) { sb.Append(cap ? char.ToUpper(c) : c); cap = false; }
                else if (char.IsDigit(c)) { if (sb.Length > 0) sb.Append(c); cap = false; }
                else if (c == ' ' || c == '_' || c == '-') cap = true;
            }
            return sb.ToString();
        }

        private void SetStatus(string msg, bool error)
        {
            if (_statusLabel == null) return;
            _statusLabel.text  = msg;
            _statusLabel.style.color = error ? new Color(1f, 0.35f, 0.35f) : new Color(0.35f, 1f, 0.45f);
        }
    }
}
