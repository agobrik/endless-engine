# Changelog

All notable changes to this package are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html)

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
