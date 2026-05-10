# Endless Engine — Complete Mastery Guide

**Version 1.2.0 · Unity 6.3 LTS**

This guide covers every system in the engine from the ground up. Read it in order the first time; use it as a reference after that. Each section explains what a system does, how it works internally, and shows concrete code you can use immediately.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Quick Start — New Game Wizard](#2-quick-start--new-game-wizard)
3. [Quick Start — MinimalIdle Sample](#3-quick-start--minimalide-sample)
4. [Bootstrap System](#4-bootstrap-system)
5. [Economy Service](#5-economy-service)
6. [Tick Engine](#6-tick-engine)
7. [Generator System](#7-generator-system)
8. [Passive Income Service](#8-passive-income-service)
9. [Save & Load System](#9-save--load-system)
10. [Upgrade Tree Service](#10-upgrade-tree-service)
11. [Prestige System](#11-prestige-system)
12. [Click & Cursor Systems](#12-click--cursor-systems)
13. [Wave & Combat System](#13-wave--combat-system)
14. [Multi-Currency System](#14-multi-currency-system)
15. [Research System](#15-research-system)
16. [Building System](#16-building-system)
17. [Pet System](#17-pet-system)
18. [Merge System](#18-merge-system)
19. [Skill Tree Service](#19-skill-tree-service)
20. [Zone System](#20-zone-system)
21. [Event System](#21-event-system)
22. [Milestone Tracker](#22-milestone-tracker)
23. [Statistics Service](#23-statistics-service)
24. [Leaderboard Service](#24-leaderboard-service)
25. [Export Service](#25-export-service)
26. [Config Registry](#26-config-registry)
27. [Number System (BigDouble)](#27-number-system-bigdouble)
28. [Editor Tools](#28-editor-tools)
29. [ScriptableObject Reference](#29-scriptableobject-reference)
30. [Game Type Bootstrap Recipes](#30-game-type-bootstrap-recipes)
31. [Event Bus Reference](#31-event-bus-reference)
32. [Save Data Reference](#32-save-data-reference)
33. [Troubleshooting](#33-troubleshooting)

---

## 1. Architecture Overview

Endless Engine follows these three rules:

**Rule 1 — Services are MonoBehaviours.**  
Every major system (`EconomyService`, `GeneratorSystem`, `SaveService`, etc.) is a `MonoBehaviour` component attached to a `Bootstrap` GameObject. Unity handles their lifetime; you never call `new EconomyService()`.

**Rule 2 — Communication is via static C# events.**  
Systems do not hold references to each other except through constructor injection. Changes broadcast through static events (e.g., `EconomyService.OnResourcesChanged`). Your UI subscribes to events; it never polls.

**Rule 3 — Configuration lives in ScriptableObjects.**  
All game data (generator stats, upgrade costs, wave difficulty, etc.) lives in SO assets. The `ConfigRegistry` static class gives runtime code access to these SOs without any reference wiring.

### Dependency diagram

```
          ┌─────────────────────────────────────────────┐
          │          Bootstrap GameObject               │
          │  AutoSetupBootstrap                         │
          │  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
          │  │TickEngine│  │EconomySvc│  │SaveSvc   │  │
          │  └──────────┘  └──────────┘  └──────────┘  │
          │  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
          │  │Generator │  │Upgrade   │  │Passive   │  │
          │  │System    │  │TreeSvc   │  │IncomeSvc │  │
          │  └──────────┘  └──────────┘  └──────────┘  │
          └─────────────────────────────────────────────┘
                  ↓ static events
          ┌─────────────────────────────────────────────┐
          │        Your UI / Game Logic                 │
          │  EconomyService.OnResourcesChanged          │
          │  GeneratorSystem.OnGeneratorPurchased       │
          │  WaveSpawnManager.OnWaveStarted             │
          │  PrestigeStateManager.OnPrestigeComplete    │
          └─────────────────────────────────────────────┘
```

---

## 2. Quick Start — New Game Wizard

The fastest path from zero to a running game.

**Step 1 — Open the wizard**  
`Tools → Endless Engine → New Game Wizard`

**Step 2 — Set a game name**  
Type your project name (e.g. "StarMiner"). The wizard sanitizes it to a valid C# identifier.

**Step 3 — Pick a game type**  
| Type | What you get at Play |
|------|----------------------|
| Pure Idle | Generators ticking, gold accumulating, prestige button |
| Clicker Idle | Clickable orange target + generators |
| Idle-vs / RPG | Red enemy circles spawning and moving, wave counter in HUD |
| Merge Idle | 3×3 merge board placeholder + basic HUD |
| Research Idle | Generators + prestige + multi-currency slots |
| Building Idle | Generators + zone system wired |
| Prestige-Heavy | Generators + all prestige layers + multi-currency |
| Custom | All toggles off — configure manually |

**Step 4 — Click Generate Skeleton**  
The wizard creates:
- `Assets/{GameName}/Configs/` — all ScriptableObject assets, pre-tuned for the type
- `Assets/{GameName}/Scenes/{GameName}.unity` — a fully-wired scene

**Step 5 — Open the scene and press Play**  
`File → Open Scene → Assets/{GameName}/Scenes/{GameName}.unity`  
Press Play. The HUD appears. Gold accumulates. For Clicker: click the orange circle. For Wave: enemies spawn and move.

**No Inspector wiring required.**

---

## 3. Quick Start — MinimalIdle Sample

The MinimalIdle sample is a complete working idle game in ~300 lines. Use it to understand the wiring pattern.

**Step 1 — Import**  
`Window → Package Manager → Endless Engine → Samples → MinimalIdle → Import`

**Step 2 — Open scene**  
`Assets/Samples/Endless Engine/1.2.0/MinimalIdle/Scenes/MinimalIdle.unity`

**Step 3 — Press Play**  
You see: gold counter, income rate, generator buy button, upgrade button, mine button.

**What to study in MinimalIdleUI.cs:**
- `FindFirstObjectByType<AutoSetupBootstrap>()` — no Inspector refs
- `EconomyService.OnResourcesChanged +=` — event subscription pattern
- `_bootstrap.Generators.TryPurchase("gold_mine")` — purchase API
- `_bootstrap.UpgradeTree.GetNode("speed_boost").CurrentRank` — upgrade API

---

## 4. Bootstrap System

### AutoSetupBootstrap

The simplest way to get a game running.

```csharp
// Assign in Inspector or via New Game Wizard (auto-wired):
[SerializeField] EconomyConfigSO _economyConfig;
[SerializeField] GeneratorDatabaseSO _generatorDatabase;
[SerializeField] SchemaVersionSO _schemaVersion;
[SerializeField] PrestigeConfigSO _prestigeConfig;   // null = no prestige
[SerializeField] bool _enableSave = true;
```

**What it does on Start():**
1. Creates `EconomyService`, `GeneratorSystem`, `UpgradeTreeService`, `TickEngine`, `SaveService` on the same GameObject (via `GetOrAdd<T>()`).
2. Calls `ConfigRegistry.InjectForTesting()` to make SOs accessible globally.
3. Manually calls `UpgradeTree.HandleConfigsLoaded()` before load.
4. Initializes all services.
5. Runs `SaveService.LoadAsync()` and waits.
6. Sets `IsReady = true`.

**Wait for IsReady before accessing services:**

```csharp
IEnumerator Start()
{
    var boot = FindFirstObjectByType<AutoSetupBootstrap>();
    yield return new WaitUntil(() => boot.IsReady);
    
    // Now safe to use:
    double gold = boot.Economy.CurrentResources;
}
```

**Accessing services after boot:**
```csharp
boot.Economy      // EconomyService
boot.Generators   // GeneratorSystem
boot.UpgradeTree  // UpgradeTreeService
boot.Save         // SaveService (null if _enableSave=false)
boot.Tick         // TickEngine
```

### VerticalSliceBootstrap (production)

`AutoSetupBootstrap` uses `ConfigRegistry.InjectForTesting` — fine for Editor/dev builds. For shipping:
- Use `VerticalSliceBootstrap` which loads SOs via Addressables.
- Handles async loading, error screens, migration.

---

## 5. Economy Service

`EconomyService` owns the primary currency (gold/coins/gems — whatever your game calls it).

### Initialization

```csharp
economyService.Initialize(
    upgradeTreeQuery: upgradeTree,   // null = no upgrade multipliers
    saveNotifier:     saveService);  // null = no auto-save on change
```

### Reading balance

```csharp
double balance = economy.CurrentResources;
```

### Adding / deducting

```csharp
economy.AddResources(100);
bool ok = economy.TryDeductResources(50);  // false if insufficient
// or unconditional:
economy.DeductResources(50);  // throws if insufficient — use TryDeduct in UI
```

### Events

```csharp
// (double current, double delta) — fires every time gold changes
EconomyService.OnResourcesChanged += (current, delta) =>
{
    goldText.text = $"Gold: {current:N0}";
};
```

### HardCap

Set in `EconomyConfigSO.ResourceHardCap`. Balance silently clamps at cap. Check with:
```csharp
bool atCap = economy.IsAtHardCap;
```

### SoftCap

`EconomyConfigSO.SoftCapThreshold` + `SoftCapCurveExponent` define a diminishing returns curve beyond the soft cap. This is applied inside `AddResources` automatically.

---

## 6. Tick Engine

`TickEngine` drives all time-based systems with a configurable heartbeat.

```csharp
// Default: 1 tick per second
tickEngine.TickInterval = 1f;

// Speed up time (for debug or time-boost powerups):
tickEngine.TimeScale = 2f;   // 2× speed
tickEngine.TimeScale = 1f;   // normal

// Subscribe to each tick:
tickEngine.OnTick += (deltaTime) =>
{
    // deltaTime = tickInterval * timeScale
    DoSomethingEveryTick(deltaTime);
};
```

`PassiveIncomeService` uses `TickEngine.OnTick` to calculate gold earned this tick.

---

## 7. Generator System

Generators are the core passive income source. Each generator type has an ID, cost, and yield.

### Creating a generator config

In the Project window: `Create → Endless Engine → Config → Generator Config`

Key fields:
| Field | Description |
|-------|-------------|
| `GeneratorId` | Unique string ID (e.g. `"gold_mine"`) |
| `DisplayName` | Shown in UI (e.g. `"Gold Mine"`) |
| `BaseYieldPerSecond` | Gold produced per second per owned unit |
| `BaseCost` | First purchase price |
| `CostScalingFactor` | Multiplier per purchase (1.15 = +15% each) |
| `MaxOwned` | 0 = unlimited |

### Initialization

```csharp
generatorSystem.Initialize(
    configs:      generatorDatabase.Generators,  // GeneratorConfigSO[]
    economy:      economyService,
    saveNotifier: saveService);
```

### Buying generators

```csharp
// Returns true if purchase succeeded
bool ok = generatorSystem.TryPurchase("gold_mine");

// Check cost before buying
double cost = generatorSystem.GetNextCost("gold_mine");
bool canAfford = economy.CurrentResources >= cost;
```

### Querying ownership

```csharp
int owned = generatorSystem.GetOwned("gold_mine");
int total = generatorSystem.TotalGeneratorsOwned();
double yieldPerSec = generatorSystem.GetYieldPerSecond("gold_mine");
```

### Events

```csharp
GeneratorSystem.OnGeneratorPurchased += (id) =>
{
    Debug.Log($"Purchased: {id}");
};
```

---

## 8. Passive Income Service

`PassiveIncomeService` connects `GeneratorSystem` to `EconomyService` via the `TickEngine`.

Each tick: `totalYield = Σ (owned[i] × config[i].BaseYieldPerSecond × upgradeMultiplier) × tickDelta`

```csharp
passiveIncomeService.Initialize(
    generators: generatorSystem,
    economy:    economyService,
    gameFlow:   null);  // null = always earning; pass GameFlowStateMachine to pause during menus
```

You rarely call this directly. `AutoSetupBootstrap` wires it automatically.

---

## 9. Save & Load System

`SaveService` provides atomic save/load with auto-save, migration, and crash safety.

### How it works

1. All saveable systems implement `ISaveStateProvider`.
2. On save: `SaveService` calls `OnBeforeSave(SaveData)` on each provider in `ProviderOrder`.
3. Serializes `SaveData` to JSON, writes to `Application.persistentDataPath/save.json`.
4. On load: deserializes, calls `OnAfterLoad(SaveData)` on each provider.

### Registering a provider

```csharp
saveService.RegisterStateProvider(economyService);    // order=10
saveService.RegisterStateProvider(generatorSystem);   // order=20
saveService.RegisterStateProvider(upgradeTreeService); // order=25
```

Order matters — lower number saves first.

### Manual save/load

```csharp
// Save now (async)
await saveService.SaveAsync();

// Load (called once at startup by AutoSetupBootstrap)
await saveService.LoadAsync();
```

### Auto-save

Enabled by default every 60 seconds. Configure in `SaveService` component in Inspector.

### Schema migration

When you add new save data fields, bump the schema version:
1. `Tools → Endless Engine → Schema Bump`
2. Implements `IMigration` — fill in the migration logic.
3. `SaveService` runs all migrations on load when `SaveData.SchemaVersion` is behind.

### Implementing ISaveStateProvider

```csharp
public class MySystem : MonoBehaviour, ISaveStateProvider
{
    public int ProviderOrder => 50;  // higher = later in chain

    public void OnBeforeSave(SaveData data)
    {
        data.MyCustomInt = _myValue;
    }

    public void OnAfterLoad(SaveData data)
    {
        _myValue = data.MyCustomInt;
        data.EnsureDefaults();  // safe to call multiple times
    }
}
```

---

## 10. Upgrade Tree Service

The upgrade tree is a directed acyclic graph of upgrade nodes. Each node has prerequisites, a rank (0→MaxRank), and stat multipliers.

### Creating upgrade nodes

`Create → Endless Engine → Config → Upgrade Node Config`

Key fields:
| Field | Description |
|-------|-------------|
| `NodeId` | Unique string (`"speed_boost"`) |
| `DisplayName` | UI label |
| `BaseCost` | Cost at rank 1 |
| `CostScalingPerRank` | Multiplier per rank |
| `MaxRank` | Maximum purchasable rank |
| `Prerequisites` | NodeIds that must be maxed first |
| `StatMultipliers` | List of (statKey, baseValue, scalingPerRank) |

### Reading nodes

```csharp
var node = upgradeTree.GetNode("speed_boost");
int rank       = node.CurrentRank;
int maxRank    = node.Config.MaxRank;
double cost    = upgradeTree.GetNodeCostDouble("speed_boost");
bool unlocked  = upgradeTree.IsNodeUnlocked("speed_boost");
```

### Purchasing

```csharp
bool ok = upgradeTree.TryPurchaseNode("speed_boost");
// Deducts gold automatically via economy
```

### Getting stat multipliers

```csharp
float multiplier = upgradeTree.GetStatMultiplier("income_rate");
// Returns 1.0f if no upgrades affect that key
```

---

## 11. Prestige System

Prestige resets the game in exchange for a permanent multiplier to future runs.

### How it works

1. `PrestigeStateManager.CanPrestige` becomes true when `PrestigeConfigSO.MinGoldToPrestige` is reached.
2. Call `TryPrestige()` — triggers a two-save crash-safe sequence.
3. `OnPrestigeStarted` fires → all subscribed systems reset (Economy, Generators, UpgradeTree).
4. `OnPrestigeComplete(count, multiplier)` fires with the new cumulative multiplier.
5. On the next run, `EconomyService` applies the prestige multiplier to all income.

### Setup

```csharp
// PrestigeStateManager has [SerializeField] private SaveService _saveService
// Wire it in Inspector or let AutoSetupBootstrap wire it
```

`PrestigeConfigSO` key fields:
| Field | Description |
|-------|-------------|
| `BaseMultiplierPerPrestige` | Multiplier added per prestige (1.5 = +50%) |
| `MaxPermanentMultiplier` | Cap on total prestige multiplier |
| `MinGoldToPrestige` | Gate — must have this much gold to prestige |

### UI integration

```csharp
PrestigeStateManager.OnPrestigeComplete += (count, multiplier) =>
{
    prestigeLabel.text = $"Prestige {count}  ×{multiplier:F1}";
};

prestigeButton.onClick.AddListener(() =>
{
    var pm = FindFirstObjectByType<PrestigeStateManager>();
    if (pm != null && pm.CanPrestige)
        pm.TryPrestige();
});
```

### Multi-layer prestige

For deep prestige games use `AscensionStateManager` (second layer) on top of `PrestigeStateManager`. Both implement `ISaveStateProvider` and fire their own `OnAscensionComplete` events.

---

## 12. Click & Cursor Systems

### ClickYieldService

Earns gold on player taps/clicks with combo multiplier and crit chance.

**Setup:**
1. Create `ClickSourceConfigSO` (`Create → Endless Engine → Config → Click Source Config`)
2. Add `ClickYieldService` component to Bootstrap
3. Call `Initialize`:

```csharp
clickYield.Initialize(
    config:             clickConfig,
    economy:            economyService,
    passiveYieldGetter: () => generatorSystem.TotalYieldPerSecond());
```

**Key config fields:**
| Field | Description |
|-------|-------------|
| `GoldPerClick` | Base gold per tap |
| `EnableCombo` | Whether combo multiplier builds |
| `MaxComboMultiplier` | Cap on combo |
| `ComboMultiplierStep` | Added per click |
| `CritChance` | 0–1 probability of crit |
| `CritMultiplier` | Gold multiplier on crit |

**Input:**  
`ClickYieldService` reads from an `IInputProvider`. For UI button clicks, call `SimulateClickForTesting()` in development builds. For production, implement `IInputProvider`:

```csharp
public class MyInputProvider : IInputProvider
{
    private bool _clicked;
    public bool GetPointerClickedThisFrame() { bool v = _clicked; _clicked = false; return v; }
    public void NotifyClick() { _clicked = true; }
}
```

Wire it: `clickYield.SetInputProvider(myProvider);`

**Event:**
```csharp
ClickYieldService.OnClick += (earned, combo, wasCrit) =>
{
    ShowFloatingText(earned, wasCrit ? Color.yellow : Color.white);
};
```

### CursorYieldService

Earns gold from mouse/touch movement. Three models:
- `Speed` — gold proportional to cursor speed
- `Distance` — gold per pixel traveled
- `Hover` — gold per second cursor is held still over a zone

```csharp
cursorYield.Initialize(config, economyService, gameFlow, inputProvider);
```

---

## 13. Wave & Combat System

The wave system spawns enemies, tracks their health, and handles auto-battle.

### Key components

| Component | Responsibility |
|-----------|---------------|
| `WaveSpawnManager` | Spawns enemies from `_enemyPrefab`, manages wave lifecycle |
| `EnemyManager` | Updates enemy positions (Rigidbody2D), handles death |
| `AutoBattleController` | Auto-attacks enemies, triggers upgrade selection |
| `HealthSystem` | Tracks entity HP, handles damage events |

### Enemy prefab requirements

Your enemy GameObject MUST have:
- `SpriteRenderer` — visual (circle, sprite sheet, etc.)
- `Rigidbody2D` (gravity=0, freeze rotation) — movement
- `CircleCollider2D` or `BoxCollider2D` — collision
- `EnemyAgent` component — data container

### Initialization sequence

```csharp
// 1. Inject configs
ConfigRegistry.InjectForTesting(wave: waveConfig, enemy: enemyConfig);

// 2. Initialize EnemyManager
enemyManager.Initialize(playerQuery, damageDispatcher);

// 3. Initialize WaveSpawnManager
waveSpawnManager.Initialize(enemyManager, saveService, healthSystem);
// waveSpawnManager._enemyPrefab must be assigned in Inspector!

// 4. Initialize AutoBattleController
autoBattle.Initialize(enemyManager, waveSpawnManager,
    statProvider, playerConfig, waveConfig, playerId: 1);
autoBattle.StartCombat();

// 5. On AfterLoad (handled by SaveService):
waveSpawnManager.StartFirstWave();
```

### WaveConfigSO key fields

| Field | Description |
|-------|-------------|
| `TotalWavesPerRun` | Waves before run ends |
| `BaseEnemyCountPerWave` | Enemies on wave 1 |
| `EnemyCountScalingFactor` | Multiplier per wave |
| `EliteWaveInterval` | Every N waves spawns elite enemies |

### Events

```csharp
WaveSpawnManager.OnWaveStarted  += (wave) => waveLabel.text = $"Wave {wave}";
WaveSpawnManager.OnWaveComplete += (wave) => { /* show upgrade selection */ };
WaveSpawnManager.OnUpgradeSelectionTriggered += ShowUpgradeCards;
```

---

## 14. Multi-Currency System

`CurrencyService` manages secondary currencies (gems, tokens, research points, etc.).

### Setup

1. Create `CurrencyDatabaseSO` → add `CurrencyDefinition` entries (id, displayName, cap)
2. Add `CurrencyService` to Bootstrap
3. Call `Initialize`:

```csharp
currencyService.Initialize(currencyDatabase);
```

### API

```csharp
double gems = currencyService.Get("gems");
currencyService.Add("gems", 10);
bool ok = currencyService.TryDeduct("gems", 5);

CurrencyService.OnCurrencyChanged += (id, newAmount) =>
{
    if (id == "gems") gemsLabel.text = $"{newAmount:N0}";
};
```

---

## 15. Research System

`ResearchService` implements a queue-based tree where nodes take real time to complete.

```csharp
researchService.Initialize(researchDatabase, tickEngine, currencyService);
researchService.EnqueueNode("advanced_mining");

ResearchService.OnNodeCompleted += (nodeId) =>
{
    Debug.Log($"Research complete: {nodeId}");
};
```

Research speed is modified by `EventService` calendar multipliers and upgrade stat modifiers.

---

## 16. Building System

`BuildingService` lets players place buildings on a grid. Each building has passive production and optional upgrade tiers.

```csharp
// Attempt to place a building
bool ok = buildingService.TryPlace("sawmill", gridX: 2, gridY: 3);

// Upgrade a placed building
buildingService.TryUpgrade("sawmill", gridX: 2, gridY: 3);

// Remove
buildingService.Remove(gridX: 2, gridY: 3);
```

`BuildingConfigSO` defines placement cost, per-tick income, upgrade cost curve, and max tier.

---

## 17. Pet System

`PetService` manages equippable companions that grant passive stat bonuses.

```csharp
// Equip a pet
petService.TryEquip("fire_fox");

// Level up
petService.TryLevelUp("fire_fox");

// Evolve (requires max level)
petService.TryEvolve("fire_fox");

// Read stats
float bonus = petService.GetActiveBonus("income_multiplier");
```

`PetService` implements `ISaveStateProvider` — level, equip state, and evolution are saved automatically.

---

## 18. Merge System

`MergeService` handles a merge board where 2× tier-N items merge into 1× tier-(N+1).

```csharp
// Attempt merge at two cells
bool ok = mergeService.TryMerge(cellA: 0, cellB: 1);

// A successful merge fires:
MergeService.OnMergeSuccess += (newTier, goldBonus) =>
{
    economyService.AddResources(goldBonus);
};
```

Board state is saved via `ISaveStateProvider`.

---

## 19. Skill Tree Service

`SkillTreeService` is a free-form talent tree. Unlike `UpgradeTreeService` (which is cost-gated), skill nodes are purchased with skill points earned from prestige/milestones.

```csharp
skillTreeService.Initialize(skillDatabase, economyService, saveService);

// Add skill points (e.g., on prestige)
skillTreeService.AddSkillPoints(3);

// Unlock a node (costs skill points)
bool ok = skillTreeService.TryUnlock("double_tap");

// Stat query
float mult = skillTreeService.GetStatMultiplier("click_gold");
```

Use the Skill Tree Editor: `Tools → Endless Engine → Skill Tree Editor`

---

## 20. Zone System

`ZoneSystem` defines world-space income regions. A zone earns gold while the player is inside it (cursor-active mode) or continuously (passive mode).

```csharp
zoneSystem.Initialize(zoneDatabase.Zones, economyService, gameFlow,
    inputProvider, saveService);
zoneSystem.SetPrestigeCountGetter(() => prestigeManager.PrestigeCount);
```

Zones are defined in `ZoneDatabaseSO`. Each `ZoneConfigSO` sets bounds, income rate, unlock cost, and mode.

---

## 21. Event System

`EventService` drives time-gated seasonal/rotating events that apply multipliers to income, research speed, etc.

```csharp
eventService.Initialize(eventDatabase, tickEngine);

// Check if an event is active
bool active = eventService.IsEventActive("summer_festival");

// Get multiplier (1.0 if no event)
float mult = eventService.GetMultiplier("income_rate");
```

Events fire `OnEventActivated` / `OnEventDeactivated` events. The `EventBannerOverlay` UI screen automatically subscribes to these.

---

## 22. Milestone Tracker

`MilestoneTracker` fires achievements when condition trees are satisfied.

```csharp
// Conditions are defined in MilestoneConfigSO (condition type + threshold)
// Tracker evaluates them on each tick

MilestoneTracker.OnMilestoneUnlocked += (id) =>
{
    Debug.Log($"Achievement unlocked: {id}");
    unlockLogService.RecordUnlock(id);
};
```

Milestone state is saved. The `UnlockLogScreen` UI shows all discovered milestones.

---

## 23. Statistics Service

`StatisticsService` tracks lifetime counters (total gold earned, total clicks, waves completed, etc.) and peaks (highest balance, longest combo).

```csharp
// Read a stat
long totalClicks = statisticsService.Get("total_clicks");

// Increment (call internally from systems, not manually)
statisticsService.Increment("total_clicks");
statisticsService.SetPeak("max_combo", comboValue);
```

Statistics are read-only for UI — they exist to power milestones and leaderboard entries.

---

## 24. Leaderboard Service

`LeaderboardService` stores a local PlayerPrefs leaderboard. For network leaderboards, integrate your own backend and call `SubmitScore`.

```csharp
leaderboardService.SubmitScore("wave_run", score: totalWaves);
var entries = leaderboardService.GetTopEntries("wave_run", count: 10);
```

The `LeaderboardScreen` UI binds automatically.

---

## 25. Export Service

`ExportService` serializes the entire save state to a base64 string for cross-device transfer.

```csharp
string code = exportService.ExportCurrentSave();
// Player copies this to clipboard or shares it

// Import on another device:
exportService.ImportFromCode(code);
```

The `ExportDialog` UI screen handles copy/paste automatically.

---

## 26. Config Registry

`ConfigRegistry` is a static service locator that gives any runtime code access to SOs without reference wiring.

### Accessing configs

```csharp
EconomyConfigSO    econ    = ConfigRegistry.Economy;
WaveConfigSO       wave    = ConfigRegistry.Wave;
EnemyStatConfigSO  enemy   = ConfigRegistry.Enemy;
PrestigeConfigSO   prestige = ConfigRegistry.Prestige;
```

### In Editor/dev builds (AutoSetupBootstrap)

Configs are injected via `ConfigRegistry.InjectForTesting(...)`. No Addressables involved.

### In production (VerticalSliceBootstrap)

Configs are loaded via Addressables. `ConfigRegistry.OnConfigsLoaded` fires when complete.

```csharp
ConfigRegistry.OnConfigsLoaded += () =>
{
    // Now safe to read any config
};
```

---

## 27. Number System (BigDouble)

All gold values are `double` at the API surface. Internally, `BigNumberFactory` wraps a `double` or a `BigDouble` (arbitrary precision) depending on `EconomyConfigSO.NumberBackend`.

### Formatting

```csharp
string formatted = BigNumberFormatter.Format(1_500_000);
// → "1.5M"

string full = BigNumberFormatter.FormatFull(1_500_000);
// → "1,500,000"
```

The formatter always uses `InvariantCulture` (decimal point is always `.`).

### Backend selection

| Backend | Use when |
|---------|----------|
| `Double` (default) | Up to ~1e308 — covers most idle games |
| `BigDouble` | Post-prestige numbers > 1e308 |

Set in `EconomyConfigSO.NumberBackend`.

---

## 28. Editor Tools

### New Game Wizard
`Tools → Endless Engine → New Game Wizard`  
Generates a complete game skeleton with configs and a wired scene. See §2.

### Economy Tuning Window
`Tools → Endless Engine → Economy Tuning`  
Live-edit economy parameters during Play Mode. 6 tabs: Income, Prestige, Upgrade Costs, Offline, Soft Cap, Currency.

### Upgrade Tree Editor
`Tools → Endless Engine → Upgrade Tree Editor`  
GraphView editor for node layout, connections, and stat assignments.

### Skill Tree Editor
`Tools → Endless Engine → Skill Tree Editor`  
Same as Upgrade Tree Editor but for `SkillTreeService` nodes.

### Content Pack Wizard
`Tools → Endless Engine → Content Pack Wizard`  
Creates a complete `RealmPack` with all 9 config SOs pre-wired in one click.

### ID Registry
`Tools → Endless Engine → ID Registry`  
Scans all SOs, reports duplicate IDs, empty IDs, and orphan prerequisites. Run before shipping.

### Schema Bump Utility
`Tools → Endless Engine → Schema Bump`  
Increments `SchemaVersionSO.CurrentSchemaVersion` and scaffolds a migration class.

### Trait Tree Editor
`Tools → Endless Engine → Trait Tree Editor`  
Visual editor for trait/perk trees.

---

## 29. ScriptableObject Reference

All SOs are under `Create → Endless Engine → Config → ...`

| SO | Purpose | Key Fields |
|----|---------|------------|
| `EconomyConfigSO` | Primary currency settings | `ResourceHardCap`, `SoftCapThreshold`, `IdleYieldRateBase`, `OfflineCapHours` |
| `GeneratorConfigSO` | Single generator type | `GeneratorId`, `BaseYieldPerSecond`, `BaseCost`, `CostScalingFactor` |
| `GeneratorDatabaseSO` | Collection of generators | `Generators: GeneratorConfigSO[]` |
| `UpgradeNodeConfigSO` | Single upgrade node | `NodeId`, `MaxRank`, `BaseCost`, `Prerequisites`, `StatMultipliers` |
| `PrestigeConfigSO` | Prestige rules | `BaseMultiplierPerPrestige`, `MaxPermanentMultiplier`, `MinGoldToPrestige` |
| `WaveConfigSO` | Wave difficulty | `TotalWavesPerRun`, `BaseEnemyCountPerWave`, `EnemyCountScalingFactor`, `EliteWaveInterval` |
| `EnemyStatConfigSO` | Enemy stat scaling | `BaseHP`, `HPScalingPerWave`, `BaseMoveSpeed`, `BaseDamage` |
| `RunConfigSO` | Run parameters | `RunDurationSeconds`, `UpgradeSelectionWaveInterval` |
| `ClickSourceConfigSO` | Click income rules | `GoldPerClick`, `CritChance`, `CritMultiplier`, `EnableCombo`, `MaxComboMultiplier` |
| `CursorActivityConfigSO` | Cursor income rules | `YieldModel`, `GoldPerUnit` |
| `ZoneDatabaseSO` | Collection of zones | `Zones: ZoneConfigSO[]` |
| `CurrencyDatabaseSO` | Secondary currencies | `Currencies: CurrencyDefinition[]` |
| `SchemaVersionSO` | Save schema version | `CurrentSchemaVersion` |
| `RealmIdentityConfigSO` | Game world identity | `RealmId`, `DisplayName`, `IconSprite` |
| `PlayerBaseStatConfigSO` | Player base stats | `BaseHP`, `BaseDamage`, `BaseDefense` |
| `RealmPackSO` | Full realm bundle | All config references for a realm |
| `RealmRegistrySO` | All realms in project | `Entries: RealmEntry[]` |

---

## 30. Game Type Bootstrap Recipes

Complete `IEnumerator Start()` bootstrap implementations for every game type.

### Pure Idle

```csharp
// AutoSetupBootstrap handles this automatically.
// For custom bootstrap:
Economy.Initialize(upgradeTreeQuery: UpgradeTree, saveNotifier: Save);
Generators.Initialize(generatorDB.Generators, Economy, Save);
PassiveIncome.Initialize(Generators, Economy, gameFlow: null);
UpgradeTree.HandleConfigsLoaded();

Save.RegisterStateProvider(Economy);
Save.RegisterStateProvider(UpgradeTree);
Save.RegisterStateProvider(Generators);

bool done = false;
_ = Save.LoadAsync().ContinueWith(_ => done = true,
    TaskScheduler.FromCurrentSynchronizationContext());
yield return new WaitUntil(() => done);
```

### Clicker Idle

```csharp
// Everything in Pure Idle, plus:
ClickYield.Initialize(clickConfig, Economy,
    passiveYieldGetter: () => PassiveIncome.CurrentYieldPerSecond);
// No RegisterStateProvider needed for ClickYield (no persistent state by default)
```

### Idle-vs / RPG (Wave Combat)

```csharp
// Pure Idle base, plus after LoadAsync:
ConfigRegistry.InjectForTesting(wave: waveConfig, enemy: enemyConfig);
EnemyMgr.Initialize(playerQuery, damageDispatcher);
WaveSpawn.Initialize(EnemyMgr, Save, HealthSystem);
AutoBattle.Initialize(EnemyMgr, WaveSpawn, statProvider, playerConfig, waveConfig, 1);
AutoBattle.StartCombat();
WaveSpawn.StartFirstWave();
```

### Prestige-Heavy

```csharp
// Pure Idle base, plus:
Save.RegisterStateProvider(PrestigeManager);
Save.RegisterStateProvider(AscensionManager);

// After LoadAsync:
PrestigeStateManager.OnPrestigeStarted += () =>
{
    Economy.ResetForPrestige();
    Generators.ResetForPrestige();
    UpgradeTree.ResetForPrestige();
};
```

### Research Idle

```csharp
// Pure Idle base, plus:
CurrencyService.Initialize(currencyDatabase);
ResearchService.Initialize(researchDatabase, TickEngine, CurrencyService);

Save.RegisterStateProvider(CurrencyService);
Save.RegisterStateProvider(ResearchService);
```

---

## 31. Event Bus Reference

All static events in the engine:

| Class | Event | Signature | When |
|-------|-------|-----------|------|
| `EconomyService` | `OnResourcesChanged` | `(double current, double delta)` | Every gold change |
| `GeneratorSystem` | `OnGeneratorPurchased` | `(string id)` | Successful purchase |
| `ClickYieldService` | `OnClick` | `(long earned, float combo, bool crit)` | Every click |
| `ClickYieldService` | `OnComboReset` | `()` | Combo timer expires |
| `WaveSpawnManager` | `OnWaveStarted` | `(int waveNumber)` | Wave begins |
| `WaveSpawnManager` | `OnWaveComplete` | `(int waveNumber)` | All enemies dead |
| `WaveSpawnManager` | `OnUpgradeSelectionTriggered` | `()` | Upgrade pick every N waves |
| `EnemyManager` | `OnEnemyKilled` | `(EnemyAgent agent)` | Enemy HP → 0 |
| `AutoBattleController` | `OnEnemyKilled` | `(EnemyAgent agent)` | Auto-battle kill |
| `PrestigeStateManager` | `OnPrestigeStarted` | `()` | Before prestige reset |
| `PrestigeStateManager` | `OnPrestigeComplete` | `(int count, float mult)` | After prestige |
| `AscensionStateManager` | `OnAscensionComplete` | `(int count)` | After ascension |
| `ResearchService` | `OnNodeCompleted` | `(string nodeId)` | Research done |
| `MilestoneTracker` | `OnMilestoneUnlocked` | `(string id)` | Condition met |
| `CurrencyService` | `OnCurrencyChanged` | `(string id, double amount)` | Currency changes |
| `MergeService` | `OnMergeSuccess` | `(int tier, long goldBonus)` | Successful merge |
| `EventService` | `OnEventActivated` | `(string id)` | Event starts |
| `EventService` | `OnEventDeactivated` | `(string id)` | Event ends |
| `ConfigRegistry` | `OnConfigsLoaded` | `()` | Addressables loaded |

**Always unsubscribe in OnDestroy:**
```csharp
private void OnDestroy()
{
    EconomyService.OnResourcesChanged -= HandleGoldChanged;
    GeneratorSystem.OnGeneratorPurchased -= HandlePurchase;
}
```

---

## 32. Save Data Reference

`SaveData` is the single serialized object. Key fields:

| Field | Type | Set by |
|-------|------|--------|
| `Gold` | `double` | EconomyService |
| `SchemaVersion` | `int` | SaveService |
| `GeneratorOwned` | `Dictionary<string,int>` | GeneratorSystem |
| `UpgradeNodeRanks` | `Dictionary<string,int>` | UpgradeTreeService |
| `PrestigeCount` | `int` | PrestigeStateManager |
| `PrestigeMultiplier` | `float` | PrestigeStateManager |
| `WaveNumber` | `int` | WaveSpawnManager |
| `BuildingSlots` | `BuildingSlotData[]` | BuildingService |
| `PetLevels` | `Dictionary<string,int>` | PetService |
| `MilestoneIds` | `List<string>` | MilestoneTracker |
| `SkillNodeIds` | `List<string>` | SkillTreeService |
| `Currencies` | `Dictionary<string,double>` | CurrencyService |

Add your own fields by extending `SaveData` and incrementing `SchemaVersionSO`.

---

## 33. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "AutoSetupBootstrap not found" | No bootstrap in scene | Add `AutoSetupBootstrap` component to a GameObject |
| Gold always 0 at start | Missing `EconomyConfigSO` | Assign config in Inspector or use New Game Wizard |
| Generators don't tick | `PassiveIncomeService` not initialized | Check `AutoSetupBootstrap.Start()` ran — see Console for `[AutoSetupBootstrap] Ready` |
| Click button does nothing | `ClickYieldService` needs `IInputProvider` | Call `SimulateClickForTesting()` in dev builds, or implement `IInputProvider` |
| No enemies visible (Wave) | `WaveSpawnManager._enemyPrefab` is null | Assign a prefab with `SpriteRenderer + Rigidbody2D + EnemyAgent` |
| Save file corrupted | Schema version mismatch | Use `Tools → Schema Bump` to add a migration |
| ConfigRegistry.Wave is null | InjectForTesting not called for wave config | Add `ConfigRegistry.InjectForTesting(wave:, enemy:)` before `WaveSpawnManager.Initialize` |
| `HandleConfigsLoaded` NullRef | UpgradeTree not initialized before InjectForTesting | AutoSetupBootstrap calls `HandleConfigsLoaded` manually — copy that pattern |
| IL2CPP build: JSON error | Newtonsoft reflection stripped | Add a `link.xml` preserving `EndlessEngine.SaveAndLoad` and Newtonsoft types |

---

*Endless Engine v1.2.0 · Generated documentation · See also: `cookbook.md`, `api-reference.md`, `HIZLI-BASLANGIC.md`*
