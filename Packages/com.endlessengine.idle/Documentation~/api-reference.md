# Endless Engine — API Reference

Package: `com.endlessengine.idle`  
Unity: 6.3 LTS  
Namespace root: `EndlessEngine`

---

## Table of Contents

1. [Core Services](#core-services)
   - [EconomyService](#economyservice)
   - [SaveService](#saveservice)
   - [TickEngine](#tickengine)
   - [ConfigRegistry](#configregistry)
2. [Module System](#module-system)
   - [IIdleModule](#iidlemodule)
   - [ModuleRegistry](#moduleregistry)
3. [Generator System](#generator-system)
   - [GeneratorSystem](#generatorsystem)
   - [PassiveIncomeService](#passiveincomeservice)
4. [Economy Extensions](#economy-extensions)
   - [CurrencyService](#currencyservice)
   - [ConversionService](#conversionservice)
   - [MergeService](#mergeservice)
5. [Progression](#progression)
   - [UpgradeTreeService](#upgradetreeservice)
   - [SkillTreeService](#skilltreeservice)
   - [ResearchService](#researchservice)
   - [AscensionStateManager](#ascensionstatemanager)
6. [Engagement](#engagement)
   - [MinigameService](#minigameservice)
   - [ChallengeService](#challengeservice)
   - [MilestoneTracker](#milestonetracker)
   - [TimeBoostService](#timeboostservice)
7. [Statistics & Tracking](#statistics--tracking)
   - [StatisticsService](#statisticsservice)
8. [Inventory](#inventory)
   - [InventoryService](#inventoryservice)
9. [Flow](#flow)
   - [GameFlowStateMachine](#gameflowstatemachine)
   - [RunSessionManager](#runsessionmanager)
10. [Content & Social](#content--social)
    - [BuildingService](#buildingservice)
    - [PetService](#petservice)
    - [EventService](#eventservice)
    - [LeaderboardService](#leaderboardservice)
    - [ExportService](#exportservice)
    - [UnlockLogService](#unlocklogservice)
    - [NotificationService](#notificationservice)

---

## Core Services

### EconomyService

`EndlessEngine.Economy.EconomyService : MonoBehaviour, ISaveStateProvider`

Central gold/resource ledger. All economy reads and writes go through here.

#### Initialization

```csharp
economyService.Initialize(
    upgradeTreeQuery: upgradeTreeService,  // optional — provides upgrade multipliers
    saveNotifier:     saveService);        // optional — registers for dirty tracking
```

#### Key Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `AddResources` | `void AddResources(long amount)` | Add gold. Fires `OnResourcesChanged`. |
| `DeductResources` | `bool DeductResources(long amount)` | Deduct if sufficient. Returns false if insufficient. |
| `CurrentResources` | `long CurrentResources { get; }` | Current gold balance. |

#### Events

| Event | Signature | When |
|-------|-----------|------|
| `OnResourcesChanged` | `static Action<long>` | After any Add or Deduct. |

---

### SaveService

`EndlessEngine.SaveAndLoad.SaveService : MonoBehaviour`

Orchestrates serialization. All ISaveStateProviders must register here before `LoadAsync`.

#### Key Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `RegisterStateProvider` | `void Register(ISaveStateProvider)` | Add a provider to the save/load chain. |
| `LoadAsync` | `Task LoadAsync()` | Load from disk, call OnAfterLoad on all providers. |
| `SaveAsync` | `Task SaveAsync()` | Call OnBeforeSave on all providers, write to disk. |

#### Provider Order (SaveConstants)

Providers are called in ascending `ProviderOrder` order during load:

| System | Order |
|--------|-------|
| Economy | 10 |
| UpgradeTree | 20 |
| PrestigeState | 30 |
| WaveSpawn | 35 |
| Generator | 40 |
| SkillTree | 45 |
| Statistics | 85 |
| Research | 90 |

---

### TickEngine

`EndlessEngine.Flow.TickEngine : MonoBehaviour`

Discrete-time heartbeat. Fires `OnTick` at a fixed interval (default 1 Hz).

#### Key Members

| Member | Type | Description |
|--------|------|-------------|
| `OnTick` | `static Action<float>` | Fired every tick. Arg = deltaTime (interval × TimeScale). |
| `TimeScale` | `float` | Multiplier applied to all tick deltas (writable — used by TimeBoostService). |
| `TickInterval` | `float` | Base interval in seconds (default 1.0). |

---

### ConfigRegistry

`EndlessEngine.Config.ConfigRegistry` (static)

Provides access to game config ScriptableObjects. In production, configs are loaded via Addressables. In tests and the Vertical Slice, use `InjectForTesting`.

```csharp
// Production (async, via Addressables)
await ConfigRegistry.LoadAllAsync();

// Testing / Vertical Slice
#if UNITY_EDITOR || DEVELOPMENT_BUILD
ConfigRegistry.InjectForTesting(enemy: cfg, wave: wCfg, economy: eCfg, player: pCfg, run: rCfg);
#endif
```

#### Accessors

`ConfigRegistry.Enemy`, `.Wave`, `.Economy`, `.Player`, `.Run`

---

## Module System

### IIdleModule

`EndlessEngine.Modules.IIdleModule`

Core lifecycle interface. Implement this to create a registerable module.

```csharp
public interface IIdleModule
{
    string   ModuleId     { get; }   // unique id, e.g. "economy"
    string[] Dependencies { get; }   // module ids that must init first
    int      InitOrder    { get; }   // tie-break within same dep tier
    bool     ReceivesTick { get; }   // true → Tick(dt) called by TickEngine
    IEnumerator Init();
    void Tick(float dt);
    void Shutdown();
}
```

---

### ModuleRegistry

`EndlessEngine.Modules.ModuleRegistry`

Manages all IIdleModule instances. Performs topological sort for init order, detects circular dependencies, routes ticks.

```csharp
var registry = new ModuleRegistry();
registry.Register(economyModule);
registry.Register(generatorModule); // depends on "economy"

yield return registry.InitAllAsync(); // inits economy first, then generator

// Wire ticks after init
TickEngine.OnTick += dt => { /* manually route or use BindTick */ };

// On shutdown
registry.ShutdownAll();
```

#### Key Methods

| Method | Description |
|--------|-------------|
| `Register(IIdleModule)` | Add a module. Throws on duplicate id or null. |
| `InitAllAsync()` | Coroutine — inits all in dependency order. |
| `ShutdownAll()` | Calls Shutdown in reverse init order, clears registry. |
| `Get(string)` | Returns module by id, null if not found. |
| `Get<T>(string)` | Returns module cast to T, null if not found or wrong type. |
| `TopologicalSort()` | Returns ordered list. Throws on circular/missing deps. |
| `IsRegistered(string)` | Whether a module id is registered. |
| `IsInitialized(string)` | Whether a module has completed Init. |

---

## Generator System

### GeneratorSystem

`EndlessEngine.Generator.GeneratorSystem : MonoBehaviour, ISaveStateProvider`

Manages a collection of passive gold producers. Each generator has an unlock level and a rate scaled by upgrades.

```csharp
generatorSystem.Initialize(
    configs:      generatorDatabase.Generators,
    economy:      economyService,
    saveNotifier: saveService);
```

---

### PassiveIncomeService

`EndlessEngine.Flow.PassiveIncomeService : MonoBehaviour`

Polls GeneratorSystem every tick and calls `EconomyService.AddResources` with the combined yield.

```csharp
passiveIncomeService.Initialize(
    generators: generatorSystem,
    economy:    economyService,
    gameFlow:   gameFlowStateMachine); // null = always active
```

---

## Economy Extensions

### MergeService

`EndlessEngine.Economy.MergeService : MonoBehaviour`

Combines two identical-tier inventory items into one higher-tier item. Optional gold bonus per merge.

```csharp
mergeService.Initialize(mergeConfigs, inventoryService, economyService);

MergeResult result = mergeService.TryMerge(itemConfig);
if (result.Success)
    Debug.Log($"Merged → {result.ResultItem.ItemId} (+{result.GoldBonus} gold)");
else
    Debug.Log($"Merge failed: {result.FailReason}");
// FailReasons: NullItem, NotMergeable, NoInventory,
//              InsufficientCount, NoMergeConfig, NoRuleForTier
```

#### Events

| Event | Args | When |
|-------|------|------|
| `OnMergeCompleted` | `string itemId, int tier, MergeResult` | Successful merge |
| `OnMergeFailed` | `string itemId, int tier, string reason` | Failed merge |

---

## Progression

### ResearchService

`EndlessEngine.Research.ResearchService : MonoBehaviour, ISaveStateProvider`

Queue-based research tree. Nodes unlock sequentially — the active node ticks down until complete.

```csharp
researchService.Initialize(researchTreeConfigs, economyService);

// Subscribe to TickEngine for progress
TickEngine.OnTick += researchService.OnTick;

// Queue a node
bool queued = researchService.TryEnqueue("tech-tree", "laser_upgrade");
// Dequeue (only non-active nodes)
bool dequeued = researchService.TryDequeue("tech-tree", "laser_upgrade");

// Query
(int done, int total) = researchService.GetActiveProgress();
```

#### Events

| Event | Args | When |
|-------|------|------|
| `OnNodeQueued` | `string treeId, string nodeId` | Node added to queue |
| `OnResearchProgress` | `string treeId, string nodeId, int done, int total` | Every tick while researching |
| `OnNodeCompleted` | `string treeId, string nodeId` | Research finished |
| `OnEnqueueFailed` | `string treeId, string nodeId, string reason` | Failed to queue |

---

## Engagement

### MinigameService

`EndlessEngine.Minigame.MinigameService : MonoBehaviour`

Cooldown-gated active skill sessions. Player performs actions during a session window to earn bonus gold.

```csharp
minigameService.Initialize(activeSkillConfigs, economyService);

if (minigameService.CanTrigger("tap_frenzy"))
    minigameService.TryTrigger("tap_frenzy");

// In tap handler:
minigameService.RecordAction();

// Ends automatically after MinigameDurationSeconds, or manually:
minigameService.EndSession();
```

**Reward formula**: `reward = BaseGoldReward × min(1 + SessionActions × PerActionBonus, MaxRewardMultiplier)`

#### Events

| Event | Args | When |
|-------|------|------|
| `OnMinigameStarted` | `ActiveSkillConfigSO` | Session begins |
| `OnActionRecorded` | `ActiveSkillConfigSO, int actionCount` | Each `RecordAction()` |
| `OnMinigameEnded` | `ActiveSkillConfigSO, long reward` | Session ends |
| `OnSkillReady` | `string skillId` | Cooldown expires |

---

### TimeBoostService

`EndlessEngine.Flow.TimeBoostService : MonoBehaviour`

Temporarily scales `TickEngine.TimeScale` to speed up passive income.

```csharp
timeBoostService.Initialize(tickEngine, economyService);

// Free activation
timeBoostService.TryActivate(boostConfig);

// Paid activation (deducts GoldCost)
bool success = timeBoostService.TryActivatePaid(boostConfig);

// Cancel early
timeBoostService.Cancel();
```

---

## Statistics & Tracking

### StatisticsService

`EndlessEngine.Statistics.StatisticsService : MonoBehaviour, ISaveStateProvider`

Accumulates lifetime counters and peak values. Persists across save/load.

```csharp
statisticsService.Initialize(statDefinitions);

statisticsService.Add("gold_earned", 500);           // counter stat
statisticsService.SetIfHigher("max_wave_reached", 42); // peak stat

double total = statisticsService.Get("gold_earned");
```

#### Events

| Event | Args | When |
|-------|------|------|
| `OnStatChanged` | `string statId, double newValue` | After any Add or SetIfHigher |

---

## Inventory

### InventoryService

`EndlessEngine.Economy.InventoryService : MonoBehaviour, ISaveStateProvider`

Slot-based item inventory with stack counts.

```csharp
inventoryService.Initialize(itemConfigs, maxSlots: 30);

inventoryService.Add("ore_t0", 5);
int count = inventoryService.GetCount("ore_t0");
inventoryService.Remove("ore_t0", 2);
```

---

## ScriptableObject Config Types

| Config SO | Key Fields | Used By |
|-----------|------------|---------|
| `GeneratorConfigSO` | GeneratorId, GoldPerTick, UnlockCost | GeneratorSystem |
| `UpgradeNodeConfigSO` | NodeId, Cost, StatMultiplier, Prerequisites | UpgradeTreeService |
| `SkillNodeConfigSO` | NodeId, SkillPointCost, Effects, Prerequisites | SkillTreeService |
| `ResearchNodeConfigSO` | NodeId, GoldCost, ResearchTicks, Effects | ResearchService |
| `ChallengeConfigSO` | ChallengeId, Modifiers, RequiredWave, RewardMultiplier | ChallengeService |
| `ActiveSkillConfigSO` | SkillId, MinigameType, CooldownSeconds, BaseGoldReward | MinigameService |
| `MergeConfigSO` | MergeGroupId, Rules (InputTier→ResultItem+GoldBonus) | MergeService |
| `ItemConfigSO` | ItemId, MaxStackSize, MergeGroupId, MergeTier | InventoryService, MergeService |
| `TimeBoostConfigSO` | BoostId, TimeScaleMultiplier, DurationSeconds, GoldCost | TimeBoostService |
| `StatDefinitionSO` | StatId, DisplayName, IsPeakValue | StatisticsService |

---

## ISaveStateProvider

```csharp
public interface ISaveStateProvider
{
    int  ProviderOrder  { get; }                // lower = loaded first
    void OnBeforeSave(SaveData saveData);       // write state into SaveData
    void OnAfterLoad(SaveData saveData);        // read state from SaveData
}
```

All services that persist state implement this interface and must be registered via `SaveService.RegisterStateProvider()` before `LoadAsync()` is called.

---

## Content & Social

### BuildingService

`EndlessEngine.Building.BuildingService : MonoBehaviour, ISaveStateProvider`

Grid-based building placement. Each building occupies a tile, produces resources on tick, and can be upgraded.

```csharp
buildingService.Initialize(buildingConfigs, economyService);

// Wire ticks
TickEngine.OnTick += buildingService.OnTick;

PlaceResult result = buildingService.TryPlace("mine_t0", gridX: 2, gridY: 3);
if (result.Success)
    Debug.Log($"Placed {result.BuildingId} at ({result.GridX},{result.GridY})");

buildingService.TryUpgrade("mine_t0");
buildingService.Remove("mine_t0");
```

#### Key Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `TryPlace` | `PlaceResult TryPlace(string buildingId, int gridX, int gridY)` | Place a building on the grid. Returns result with Success flag. |
| `TryUpgrade` | `bool TryUpgrade(string buildingId)` | Upgrade an existing building (deducts cost). |
| `Remove` | `bool Remove(string buildingId)` | Remove a placed building. |
| `OnTick` | `void OnTick(float dt)` | Called by TickEngine; triggers production for all active buildings. |

#### Events

| Event | Args | When |
|-------|------|------|
| `OnBuildingPlaced` | `string buildingId, int gridX, int gridY` | Successful placement |
| `OnBuildingUpgraded` | `string buildingId, int newLevel` | Successful upgrade |
| `OnBuildingRemoved` | `string buildingId` | Building removed |
| `OnBuildingProduced` | `string buildingId, long goldAmount` | Production tick fires |
| `OnPlaceFailed` | `string buildingId, string reason` | Placement failed |

---

### PetService

`EndlessEngine.Pet.PetService : MonoBehaviour`

Companion pet system. One pet is active at a time; pets apply passive multipliers and can level up and evolve.

```csharp
petService.Initialize(petConfigs, economyService);

petService.TryEquip("fox_t0");
petService.TryLevelUp("fox_t0");   // deducts gold cost
petService.TryEvolve("fox_t0");    // requires max level
petService.Unequip();
```

#### Key Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `TryEquip` | `void TryEquip(string petId)` | Equip a pet. Fires `OnPetEquipped`. |
| `Unequip` | `void Unequip()` | Unequip the active pet. |
| `TryLevelUp` | `void TryLevelUp(string petId)` | Level up pet (deducts cost). |
| `TryEvolve` | `void TryEvolve(string petId)` | Evolve pet to next tier (requires max level). |

#### Events

| Event | Args | When |
|-------|------|------|
| `OnPetEquipped` | `string petId` | Pet equipped |
| `OnPetUnequipped` | `string petId` | Pet unequipped |
| `OnPetLeveledUp` | `string petId, int newLevel` | Level-up successful |
| `OnPetEvolved` | `string petId, string newPetId` | Evolution successful |
| `OnActionFailed` | `string petId, string reason` | Equip/level/evolve failed |

---

### EventService

`EndlessEngine.Event.EventService : MonoBehaviour`

Time-gated seasonal/special events that apply global income and research multipliers.

```csharp
eventService.Initialize(eventConfigs);

// Call periodically or on game focus to activate scheduled events
eventService.CheckSchedule();

bool active = eventService.IsActive("winter_festival");
var activeEvents = eventService.GetActiveEvents();

float incomeBonus  = eventService.GetCombinedIncomeMultiplier();   // e.g. 1.5
float researchBonus = eventService.GetCombinedResearchMultiplier(); // e.g. 1.2
```

#### Key Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `CheckSchedule` | `void CheckSchedule()` | Activate/deactivate events based on current UTC time and config windows. |
| `IsActive` | `bool IsActive(string eventId)` | Whether a named event is currently active. |
| `GetActiveEvents` | `IReadOnlyList<EventConfigSO> GetActiveEvents()` | All currently active events. |
| `GetCombinedIncomeMultiplier` | `float GetCombinedIncomeMultiplier()` | Product of all active events' income multipliers. |
| `GetCombinedResearchMultiplier` | `float GetCombinedResearchMultiplier()` | Product of all active events' research multipliers. |

#### Events

| Event | Args | When |
|-------|------|------|
| `OnEventActivated` | `EventConfigSO config` | Event window begins |
| `OnEventDeactivated` | `EventConfigSO config` | Event window ends |

---

### LeaderboardService

`EndlessEngine.Leaderboard.LeaderboardService : MonoBehaviour`

Local leaderboard (no backend). Tracks high scores per board; persists via `ISaveStateProvider`.

```csharp
leaderboardService.Initialize(boardConfigs, saveService);

bool isNew = leaderboardService.SubmitScore("wave_board", "Player1", score: 42);
var entries = leaderboardService.GetBoard("wave_board");  // sorted descending
int rank    = leaderboardService.GetRank("wave_board", score: 42);
bool isHigh = leaderboardService.IsHighScore("wave_board", score: 42);
```

#### Key Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `SubmitScore` | `bool SubmitScore(string boardId, string playerName, long score)` | Submit a score. Returns true if it's a new entry (not necessarily the top). |
| `GetBoard` | `IReadOnlyList<LeaderboardEntry> GetBoard(string boardId)` | Retrieve all entries, sorted by score descending. |
| `GetRank` | `int GetRank(string boardId, long score)` | 1-based rank for a given score value. |
| `IsHighScore` | `bool IsHighScore(string boardId, long score)` | Whether the score would appear on the board. |

#### Events

| Event | Args | When |
|-------|------|------|
| `OnScoreSubmitted` | `string boardId, string playerName, long score` | Score submitted successfully |

---

### ExportService

`EndlessEngine.SaveAndLoad.ExportService : MonoBehaviour`

Export and import save state as a portable base-64 encoded string (share codes).

```csharp
// Export current save to a share code
string code = exportService.ExportCurrentSave();

// Export arbitrary SaveData to code
string code2 = exportService.ExportToCode(saveData);

// Import from a share code
if (exportService.TryImportFromCode(code, out SaveData imported))
    Debug.Log("Import successful");
```

#### Key Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `ExportCurrentSave` | `string ExportCurrentSave()` | Serialize and encode the live save state. |
| `ExportToCode` | `string ExportToCode(SaveData saveData)` | Encode an arbitrary `SaveData` instance. |
| `TryImportFromCode` | `bool TryImportFromCode(string code, out SaveData saveData)` | Decode and deserialize. Returns false on malformed input. |

#### Events

| Event | Args | When |
|-------|------|------|
| `OnExportComplete` | `string code` | Export finished |
| `OnImportComplete` | `SaveData saveData` | Import succeeded |
| `OnImportFailed` | `string reason` | Decode or parse failure |

---

### UnlockLogService

`EndlessEngine.UnlockLog.UnlockLogService : MonoBehaviour, ISaveStateProvider`

Persistent codex/lore log. Entries are unlocked by gameplay events and categorised.

```csharp
unlockLogService.Initialize(entryConfigs, saveService);

unlockLogService.Unlock("enemy_goblin");              // unlock by config id
unlockLogService.UnlockDynamic("run_001_milestone");  // unlock by runtime id

bool seen = unlockLogService.IsUnlocked("enemy_goblin");
var all      = unlockLogService.GetAll();
var enemies  = unlockLogService.GetUnlocked(UnlockCategory.Enemy);
```

#### Key Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `Unlock` | `void Unlock(string entryId)` | Unlock a config-defined entry. No-op if already unlocked. |
| `UnlockDynamic` | `void UnlockDynamic(string entryId)` | Unlock a runtime-generated entry id. |
| `IsUnlocked` | `bool IsUnlocked(string entryId)` | Whether an entry has been unlocked. |
| `GetAll` | `IReadOnlyList<UnlockLogEntry> GetAll()` | All entries (locked and unlocked). |
| `GetUnlocked` | `IReadOnlyList<UnlockLogEntry> GetUnlocked(UnlockCategory category)` | Unlocked entries filtered by category. |

#### Events

| Event | Args | When |
|-------|------|------|
| `OnEntryUnlocked` | `UnlockLogEntry entry` | Entry unlocked for the first time |

---

### NotificationService

`EndlessEngine.Notification.NotificationService` (Singleton)

Screen notification queue. Displays timed toast/banner notifications to the player.

```csharp
// Enqueue with config defaults
NotificationService.Instance.Enqueue(notificationConfig);

// Enqueue with override text
NotificationService.Instance.Enqueue(notificationConfig, overrideText: "Wave 10 cleared!");

// Clear all pending notifications
NotificationService.Instance.Clear();

int pending = NotificationService.Instance.QueueCount;
```

#### Key Members

| Member | Type | Description |
|--------|------|-------------|
| `QueueCount` | `int` | Number of notifications waiting to be shown. |
| `Enqueue` | `void Enqueue(NotificationConfigSO config, string overrideText = null)` | Add notification to display queue. |
| `Clear` | `void Clear()` | Remove all queued and active notifications. |

#### Events

| Event | Args | When |
|-------|------|------|
| `OnNotificationShown` | `NotificationConfigSO config, string text` | Notification appears on screen |
| `OnNotificationDismissed` | `NotificationConfigSO config` | Notification dismissed or expired |

---

## ScriptableObject Config Types (Content & Social)

| Config SO | Key Fields | Used By |
|-----------|------------|---------|
| `BuildingConfigSO` | BuildingId, ProductionRate, UpgradeCost, GridSize | BuildingService |
| `PetConfigSO` | PetId, Tier, IncomeMultiplier, LevelUpCost, MaxLevel, EvolvesInto | PetService |
| `EventConfigSO` | EventId, StartUTC, EndUTC, IncomeMultiplier, ResearchMultiplier | EventService |
| `LeaderboardConfigSO` | BoardId, DisplayName, MaxEntries | LeaderboardService |
| `UnlockLogEntryConfigSO` | EntryId, Category, DisplayTitle, Description | UnlockLogService |
| `NotificationConfigSO` | NotificationId, DefaultText, DurationSeconds, Priority | NotificationService |
