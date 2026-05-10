# Changelog

All notable changes to this package are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html)

---

## [1.3.2] - 2026-05-11

### Fixed
- **SaveService**: Late-registered providers (BuildingBootstrap, ResearchBootstrap, PrestigeBootstrap) now receive `OnAfterLoad` immediately if save data is already loaded — previously all optional-service state was lost on every game launch
- **PrestigeConfigSO**: Default `MinWaveForPrestige` changed from 20 → 0; non-wave games (PureIdle, FarmIdle, BuildingIdle, ResearchIdle) were silently unable to prestige
- **NewGameWizard**: `ApplyPrestigePreset()` now sets correct `MinWaveForPrestige` per game type — 0 for non-wave, 10 for wave/RPG/tower defense; non-wave types gate on gold (1000) instead
- **ConfigRegistry**: `GetArray()` returns `Array.Empty` instead of null when upgrade configs not set — prevented NullReferenceException in `UpgradeTreeService.BuildTree()` foreach for game types with no upgrade nodes (e.g. MergeIdle)

---

## [1.3.1] - 2026-05-11

### Fixed
- **ClickLoop**: `ClickTarget.Awake()` no longer fires with null `_config` — scene builder now disables GO before `AddComponent`, sets config via SerializedObject, then re-enables
- **HarvestIdle**: same Awake-timing fix for `HarvestNode`; `HarvestCursor._harvestLayer` now wired to Everything (-1) so cursor detects nodes
- **ClickLoop**: `ClickLoopBootstrap._clickTargetLayer` now wired to Everything (-1) so `Physics2D.OverlapPoint` detects targets
- **HarvestLoopBootstrap**: cursor/service GameObjects now parented to bootstrap transform; `HarvestCursor.Inject()` extended to accept config + layer
- **MergeIdle**: `InventoryService` + `MergeService` now instantiated and initialized by new `MergeBootstrap` component
- **ResearchIdle**: `ResearchService` now instantiated, hooked into `TickEngine.OnTick`, and registered with SaveService by new `ResearchBootstrap`
- **BuildingIdle / FarmIdle**: `BuildingService` now instantiated and initialized by new `BuildingBootstrap` component
- **PrestigeHeavy** (and all prestige types): `PrestigeStateManager` now instantiated and its `_saveService` wired by new `PrestigeBootstrap`
- **NewGameWizard**: `CreateConfigs()` now actually creates `ResearchDatabase.asset` and `StarterMergeConfig.asset` (were listed in preview but never written)
- **HUD**: `ResearchLabel` added for ResearchIdle — shows active node progress and completion; subscribes to `ResearchService.OnResearchProgress` / `OnNodeCompleted`
- **HUD**: Fixed potential `ComboLabel` duplicate-name conflict when both Harvest and Click modules are active simultaneously (Custom preset)

### Added
- `BuildingBootstrap` — initializes `BuildingService` for BuildingIdle and FarmIdle
- `ResearchBootstrap` — initializes `ResearchService` and hooks TickEngine
- `MergeBootstrap` — initializes `InventoryService` + `MergeService`
- `PrestigeBootstrap` — wires `PrestigeStateManager._saveService` and registers with SaveService

---

## [1.3.0] - 2026-05-11

### Added

**New Game Wizard — 12 Game Types**
Complete overhaul: 12 game types (up from 8), each generating a fully-wired, play-ready scene:

| Type | Press Play and see |
|------|--------------------|
| Pure Idle | Gold accumulates, buy generators, prestige button |
| Clicker Idle | Tap orange sphere → gold + generators |
| Click Loop (new) | 3 HP-targets in triangle formation, click to destroy, combo+crit, respawn |
| Harvest Idle (new) | 5 green resource nodes, drag cursor to harvest, combo meter |
| Idle-vs / RPG | Red enemies spawn and move, wave HUD, auto-battle |
| Tower Defense (new) | Winding tile path, 3 tower slots, enemies walk path |
| Merge Idle | 3×3 merge board with 2 seed items |
| Farm Idle (new) | 6 farm plots in 2×3 grid, planted/growing/ready states |
| Research Idle | Generators + research placeholder |
| Building Idle | 8 building slot grid with placeholder buildings |
| Prestige-Heavy | All prestige layers + multi-currency |
| Custom | All modules off |

**New Runtime Components**
- `HarvestLoopBootstrap` — wires HarvestCursor + HarvestLoopService after AutoSetupBootstrap.IsReady; auto-creates InputProviderUnity if absent
- `ClickLoopBootstrap` — wires ClickLoopService + InputProviderUnity; creates ClickLoopConfigSO fallback if absent

**New Config Assets Created by Wizard**
- `ClickLoopConfig.asset` — pre-tuned combo/crit/auto-click settings
- `ClickTarget_0/1/2.asset` — 3 target configs with escalating HP and yield
- `HarvestAreaConfig.asset` — cursor radius, tick interval, combo parameters
- `HarvestNode.asset` — resource node HP, yield, respawn time
- `StarterBuilding.asset` — for Building/Farm types

**Scene Visual Improvements (SceneSetupUtility)**
- Per-type sky color: harvest=dark green, click=dark blue, wave=dark red, tower=military green, farm=field green, merge=purple
- ClickLoop: 3 colored HP-target circles in triangle + glow rings + HP bars
- HarvestIdle: 5 tree-shaped nodes (body+crown circles), ground plane, cursor radius indicator
- TowerDefense: winding tile path, grass background, 3 tower slots with grey turret
- FarmIdle: sky+ground layers, 6 farm plots with growing/ready crop visuals
- BuildingIdle: 8 colored building slot grid
- Enemy HP bars, click target HP bars (world-space SpriteRenderer)
- All HUD panels: per-type size + position (wave/tower = top-right, others = left strip)

**GeneratedGameHUD Improvements**
- Subscribes to HarvestLoopService.OnYieldAwarded + OnComboChanged
- Subscribes to ClickLoopService.OnYieldAwarded + OnComboChanged
- Shows HarvestLabel, ComboLabel, ClickIncomeLabel, GemLabel
- PrestigeButton now actually calls PrestigeStateManager.TryPrestige()
- Save indicator shows "Saved" on generator purchase, "Prestige!" on prestige

**New Game Wizard UI**
- 12 game types in dropdown with descriptions
- 12 module toggles (added: Click Loop, Harvest, Research, Building, Merge)
- File preview lists ClickTarget/HarvestNode configs

---

## [1.2.1] - 2026-05-10

### Added

**New Game Wizard — Game-Type-Specific Scenes**
- `SceneSetupUtility` now generates scenes tailored to each game type:
  - **Clicker Idle** — orange `ClickTarget` sphere in the world with `CircleCollider2D` + `ClickTargetHandler` (click it to earn gold immediately on Play)
  - **Idle-vs / RPG** — visible red enemy circles with `SpriteRenderer`, `Rigidbody2D`, `EnemyAgent`; enemy prefab auto-wired into `WaveSpawnManager`; `WaveCombatBootstrap` starts wave 1 automatically
  - **Merge Idle** — 3×3 merge board placeholder with two pre-placed tier-1 items
  - **All types** — HUD panels now match game type (wave counter for IdleVsRPG, click income for ClickerIdle, etc.)
- `ClickTargetHandler` — new runtime component attached to click targets; calls `ClickYieldService.SimulateClickForTesting` in dev builds, falls back to direct economy award
- `WaveCombatBootstrap` — new runtime component that initializes `WaveSpawnManager`, `EnemyManager`, `AutoBattleController` after `AutoSetupBootstrap.IsReady`; includes null-safe `IPlayerQuery` and `IDamageDispatcher` stubs so enemies spawn without a full player setup
- `GeneratedGameHUD` — now subscribes to `WaveSpawnManager.OnWaveStarted` and updates WaveLabel / EnemyCountLabel

### Changed
- `NewGameWizard.Generate()` now passes full game type + all module flags to `SceneSetupUtility.SetupOptions`
- `SceneSetupUtility.SetupOptions` expanded: `Type`, `HasWave`, `HasClick`, `HasCursor`, `HasZone`
- `SceneSetupUtility.GameType` enum mirrors `NewGameWizard.GameType` for type-safe scene dispatch
- HUD panel layout adapts: wave-type scene pins HUD to top-left; other types center it
- Canvas `CanvasScaler` now uses `ScaleWithScreenSize` (1080×1920, match 0.5) for better mobile layout

**Documentation**
- `Documentation~/mastery-guide.md` — comprehensive 33-section guide covering every system: architecture, all 25+ services, SO reference, bootstrap recipes for every game type, full event bus reference, save data schema, troubleshooting table

---

## [1.2.0] - 2026-05-10

### Added

**Zero-Configuration Bootstrap**
- `AutoSetupBootstrap` — drop a single component, assign config SOs, press Play. All MonoBehaviour services auto-created on the same GameObject. No Inspector wiring needed.
- `GeneratedGameHUD` — auto-wiring HUD generated alongside scenes. Finds all named UI elements by name, displays gold/income/generator count/upgrade status without any Inspector references.

**New Game Wizard — Scene Builder**
- Wizard now creates a fully-wired, play-ready `.unity` scene (not just config assets and a .cs file).
- Generated scene includes Bootstrap, Canvas HUD, EventSystem, Camera — opens scene, press Play immediately.
- Default generator config (`GoldMine`) auto-wired into `GeneratorDatabase`.
- `SchemaVersion` asset created automatically.

**Documentation**
- `HIZLI-BASLANGIC.md` — Türkçe hızlı başlangıç kılavuzu (1 sayfa, 5 adım).
- `SceneSetupUtility` — internal editor utility for programmatic scene construction.

**MinimalIdle Sample Improvements**
- `MinimalIdleUI` now auto-finds services at runtime (`FindFirstObjectByType`) — works without Inspector references.
- Displays income rate, generator count with yield, upgrade rank/bonus, mine button with live cost.

### Changed
- `NewGameWizard.Generate()` now builds a complete scene instead of only configs + bootstrap script.
- `README.md` quick start updated to reflect one-click workflow.

---

## [1.1.0] - 2026-05-02

### Added

**Active Gameplay Systems**
- `ClickLoopService` — tap targets with HP, combo multiplier, crit chance, auto-click support, and offline recovery
- `HarvestLoopService` — cursor-drag harvest nodes with area damage, combo, and offline catch-up
- `CursorYieldService` — mouse movement → gold via Speed, Distance, or Hover yield models (was stubbed in 1.0.x, now fully documented)
- `ZoneSystem` — world-space income zones in passive or cursor-active mode (was present in 1.0.x, now fully documented with `SetPrestigeCountGetter` API)
- `TimeBoostService` — temporary `TickEngine.TimeScale` multiplier with `TryActivate` / `TryActivatePaid` API (was present in 1.0.x, now fully documented)

**Samples**
- `ClickerIdle` sample — complete scene demonstrating ClickLoopService with tap targets, combo, crit, offline
- `HarvestLoop` sample — complete scene demonstrating HarvestLoopService with drag-cursor harvest nodes
- `WaveIdle` sample — AutoBattle + wave system + upgrade cards + prestige
- `MergeIdle` sample — MergeService + inventory + economy
- `PrestHeavy` sample — full prestige + ascension + skill tree + research chain

**Editor Tools**
- `ContentPackWizard` (`Tools → Endless Engine → Content Pack Wizard`) — one-click RealmPack generator
- `TraitTreeEditorWindow` (`Tools → Endless Engine → Trait Tree Editor`) — trait tree visual editor

**Documentation**
- `Documentation~/kullanim-kilavuzu-tr.md` — comprehensive 58-section Turkish user guide covering all systems
- Added §42 TimeBoostService, §43 ZoneSystem, §44 CursorYieldService, §45 InventoryService, §46 DropResolver, §47 PlayerHealthComponent, §48 GameFlowState + RunSummaryData, §49 IIdleModule
- Restored full API detail for §10 OfflineTimeCalculator, §15 UpgradeApplicationSystem, §16 SkillTreeService, §18 PrestigeStateManager, §19 AscensionStateManager, §20 WaveSpawnManager, §21 AutoBattleController that was reduced in v1.0.x docs
- Added complete bootstrap recipes for all 6 game types (§52–§57)

### Fixed

- **Documentation bootstrap recipes** — removed duplicate `LoadAsync()` calls in Tarif 2 and Tarif 3 (providers must all be registered before the single `LoadAsync()` call)
- **`OnWaveCompleted` → `OnWaveComplete`** — corrected event name in WaveIdle bootstrap recipe
- **`StartCombat()` → `NotifyUpgradeSelected()`** — corrected post-upgrade-selection call in WaveIdle recipe
- **BigDouble section ordering** — §41 BigDouble is now physically in the correct position in the Turkish guide

---

## [1.0.4] - 2026-04-24

### Added

- **`ContentPackWizard`** — new Editor tool (`Tools → Endless Engine → Content Pack Wizard`) that creates a complete RealmPack in one click: generates all 9 canonical config SOs (`EnemyStatConfig`, `WaveConfig`, `EconomyConfig`, `UpgradeSelectionConfig`, `PrestigeConfig`, `RealmIdentityConfig`, `PlayerBaseStatConfig`, `SchemaVersion`, `RealmPack`), wires all SO references into the `RealmPackSO`, and auto-registers a `RealmEntry` in the project's `RealmRegistrySO` (creates one if none exists). Safe to re-run — does not overwrite existing assets.
- **`Documentation~/cookbook.md` Content Pack Wizard section** — usage guide with file list, after-creation workflow, and runtime realm-swap snippet

---

## [1.0.3] - 2026-04-24

### Added

- **`IdRegistryWindow`** — new Editor tool (`Tools → Endless Engine → ID Registry`) that scans all Endless Engine ScriptableObjects, collects string ID fields, and reports duplicate IDs, empty IDs, and orphan prerequisite references with click-to-ping navigation
- **`ConfigValidator.ValidateUpgradeGraph()`** — graph-level validation added to the existing field-level validator: detects duplicate `NodeId` values, orphan prerequisite references, and cycles in the upgrade prerequisite DAG (DFS with White/Grey/Black colouring); runs automatically inside `ConfigValidator.Validate()`
- **`NewGameWizard` Game Type selection** — wizard now starts with a `GameType` dropdown (Pure Idle, Clicker Idle, Idle-vs / RPG, Merge Idle, Research Idle, Building Idle, Prestige-Heavy, Custom) that pre-sets module toggles and fills economy/prestige/wave SO fields with tuned starting values per game style
- **`SchemaBumpUtility`** — Editor tool (`Tools → Endless Engine → Schema Bump`) that increments `SchemaVersionSO.CurrentSchemaVersion` and scaffolds a `Migration_N_to_N+1.cs` file with the correct `IMigration` implementation template
- **`Documentation~/cookbook.md` Section 31** — Developer Toolset section documenting all Editor tools: New Game Wizard (with game type presets), Economy Tuning Window, ID Registry, Config Validator (graph-level), Upgrade/Skill Tree Editors, Schema Bump Utility

---

## [1.0.2] - 2026-04-24

### Fixed

- **BigNumberFormatter locale** — `FormatFixed` now uses `CultureInfo.InvariantCulture` so decimal separator is always `.` regardless of system locale (affected Turkish and other non-English locales)
- **SoftCapEvaluator asymptotic ceiling** — replaced `Math.BitDecrement` (not available in Unity Mono) with a relative-epsilon guard (`ceiling - ceiling * 1e-15`); also fixes `double.Epsilon` being too small to create a representable difference at large ceiling magnitudes
- **PassiveIncomeService EditMode tests** — added `SubscribeForTesting()` / `UnsubscribeForTesting()` (guarded by `#if UNITY_EDITOR || DEVELOPMENT_BUILD`) so `TickEngine.OnTick` subscription works in EditMode test runner where `OnEnable` does not fire for `AddComponent<T>()`
- **RunSummaryData test** — fixed `DateTime(second: 90)` argument which caused `ArgumentOutOfRangeException` (seconds must be 0–59)
- **ClickYieldService** — removed `UnityEngine.Input.GetMouseButtonDown(0)` legacy Input Manager fallback (violated ADR-0007); all pointer input now routes through `IPointerInputProvider`
- **ErrorScreenUI** — replaced empty stub with full implementation: tries `UIDocument` overlay first, falls back to IMGUI (`DontDestroyOnLoad` MonoBehaviour) when no UIDocument is available
- **package.json** — fixed placeholder author URL (`your-org` → `agobrik`) and added author email
- **MinimalIdle sample** — added missing config assets (`EconomyConfig`, `SchemaVersion`, `PrestigeConfig`, `RealmIdentityConfig`) so sample imports as a fully working demo without manual asset creation

### Added

- **`Documentation~/cookbook.md`** — comprehensive 30-section cookbook covering all 25+ systems, 5 game-type recipes (Pure Idle, Idle-vs, Merge, Prestige-Heavy, Deep Idle RPG), and a full ScriptableObject create-menu appendix

---

## [1.0.1] - 2026-04-24

### Added

**Sprint 22 — Integration Tests**
- `integration-003-building-economy-chain_test` — BuildingService↔EconomyService chain: TryPlace debit, OnTick credit, save/load round-trip
- `integration-004-event-research-chain_test` — EventService calendar multiplier: active event → 2× research speed, inactive → 1×
- `integration-005-pet-economy-chain_test` — PetService↔EconomyService chain: equip effects, LevelUp gold consumption, evolution level carry-over, save/load
- `integration-006-unlock-milestone-chain_test` — MilestoneTracker↔UnlockLogService chain: condition met → unlock fired, save/load preserves both states

**Sprint 21 — UI Screens**
- `BuildingGridScreen` (UXML/USS + `BuildingScreenController`) — building placement panel with slot grid and upgrade buttons
- `PetScreen` (UXML/USS + `PetScreenController`) — pet roster with level bars, equip/level-up/evolve actions, active effects bar
- `UnlockLogScreen` (UXML/USS + `UnlockLogScreenController`) — discovery log with category filter tabs; hidden entries shown as "???"
- `EventBannerOverlay` (UXML/USS + `EventBannerController`) — HUD overlay banner with live countdown, income/research bonus badges
- `LeaderboardScreen` (UXML/USS + `LeaderboardScreenController`) — board selector, score table, submit/clear bar
- `ExportDialog` (UXML/USS + `ExportDialogController`) — base64 export/import dialog with clipboard copy

**Sprint 20 — UPM Full Migration**
- All 154 Runtime scripts migrated to `Packages/com.endlessengine.idle/Runtime/` (UPM canonical location)
- `.meta` GUIDs preserved to maintain scene/prefab references
- `EndlessEngine.GameSample.asmdef` added to `Assets/Scripts/` (game-layer sample code, distinct name from `EndlessEngine.Runtime`)

### Fixed
- `PrestigeStateManager`: Added `InjectConfigForTesting`, synchronous `TryPrestige(EconomyService)` test overload, and private `GetConfig()` helper for EditMode tests
- `PrestigeConfigSO`: Added `MinGoldToPrestige` field (long, default 0) for gold gate support
- `SaveService`: Added `GetCurrentSaveData()` and `ApplyImportedSaveData(SaveData)` for export/import flows
- `ExportService`: Added `ExportCurrentSave()` convenience method
- `BootstrapController`: Wired 6 new UI screen controllers (`BuildingScreen`, `PetScreen`, `UnlockLogScreen`, `EventBanner`, `LeaderboardScreen`, `ExportDialog`)
- Resolved asmdef name collision between `Assets/Scripts/` and `Packages/` (both previously had `"name": "EndlessEngine.Runtime"`)

---

## [1.0.0] - 2026-04-23

### Added

**Core Systems (P0)**
- `ConfigRegistry` — SO config access via Addressables (+ InjectForTesting)
- `SaveService` — atomic write, auto-save, ISaveStateProvider chain (ADR-0002, ADR-0004)
- `EconomyService` — gold economy, AddResources/DeductResources, OnResourcesChanged
- `TickEngine` — 1 Hz heartbeat, TimeScale override
- `PrestigeStateManager` — prestige lifecycle, crash safety (ADR-0010)
- `UpgradeTreeService` — node graph, prereqs, stat multipliers, save/load (ADR-0009)
- `GeneratorSystem` + `PassiveIncomeService` — tick-driven passive income
- `WaveSpawnManager` — wave scaling (ADR-0011), WaveConfigSO
- `OfflineTimeCalculator` — offline gain formula

**Economy Extensions (P0/P1)**
- `CurrencyService` — secondary currencies
- `ConversionService` — cross-currency conversion
- `InventoryService` — slot-based item stacks, save/load
- `MergeService` — 2×tier-N → 1×tier-N+1 with gold bonus
- `MilestoneTracker` — condition-tree achievement system

**Progression (P1/P2)**
- `SkillTreeService` + `SkillTreeEditorWindow` — free-form talent tree
- `ResearchService` — queue-based tick-driven research tree
- `AscensionStateManager` — multi-layer prestige
- `ChallengeService` — restriction modifier modes

**Engagement (P1/P2)**
- `MinigameService` + `ActiveSkillConfigSO` — cooldown-gated active skills
- `TimeBoostService` — TickEngine.TimeScale multiplier
- `StatisticsService` — lifetime counters + peaks
- `EventService` — calendar + rotation time-gated events
- `LeaderboardService` — local PlayerPrefs leaderboard
- `ExportService` — base64 build code export/import

**Buildings & Companions (P2)**
- `BuildingService` — place/upgrade/remove with passive production
- `PetService` — equip/level/evolve with passive stat effects
- `UnlockLogService` — discovery log with category filtering

**Module System**
- `IIdleModule` + `ModuleRegistry` — dependency-ordered module lifecycle

**Editor Tools**
- Generator Editor Window, Upgrade Tree Editor (GraphView), Skill Tree Editor (GraphView)
- Economy Tuning Window (6 tabs), New Game Wizard

**Documentation & Samples**
- `Documentation~/api-reference.md` — full API reference (all 25 systems)
- `Documentation~/getting-started.md` — 8-step setup tutorial
- `Samples~/MinimalIdle/` — working minimal idle game sample

**Testing**
- 300+ EditMode unit tests (all systems)
- Integration test suite (economy→prestige chain, loot→inventory→merge chain)

---

## [0.1.0] - 2026-04-23

### Added
- Core: TickEngine, EconomyService, SaveService, GameFlowStateMachine
- Generator module: GeneratorSystem, PassiveIncomeService, GeneratorDatabaseSO
- Cursor module: CursorYieldService (Speed / Hover / Distance models)
- Click module: ClickYieldService (combo, crit, auto-click)
- Zone module: ZoneSystem (world-space income zones, unlock/upgrade)
- Prestige module: PrestigeStateManager
- Wave/Combat layer: WaveSpawnManager, EnemyManager, HealthSystem
- Editor: Generator Editor Window
- Editor: Upgrade Tree Editor Window (GraphView)
- Editor: Economy Tuning Window (6 tabs)
- Editor: New Game Wizard
