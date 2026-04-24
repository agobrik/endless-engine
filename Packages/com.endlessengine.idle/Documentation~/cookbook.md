# Endless Engine — Idle Game Cookbook

**Package:** `com.endlessengine.idle` v1.0.1  
**Engine:** Unity 6.3 LTS  
**Audience:** Unity developers building idle / incremental games from scratch.

This document teaches you how to build any idle game using Endless Engine — from a
one-screen cookie clicker to a deep idle-RPG with prestige, research trees, pets,
buildings, minigames, and live events. Read it cover-to-cover once, then use it as
a reference.

---

## Table of Contents

1. [Philosophy & Architecture](#1-philosophy--architecture)
2. [Installation](#2-installation)
3. [The Bootstrap Pattern](#3-the-bootstrap-pattern)
4. [Core Systems (always needed)](#4-core-systems-always-needed)
   - 4.1 [EconomyService — gold ledger](#41-economyservice--gold-ledger)
   - 4.2 [TickEngine — heartbeat](#42-tickengine--heartbeat)
   - 4.3 [SaveService — persistence](#43-saveservice--persistence)
   - 4.4 [ConfigRegistry — config access](#44-configregistry--config-access)
5. [Generators — passive income](#5-generators--passive-income)
6. [Upgrade Tree — stat progression](#6-upgrade-tree--stat-progression)
7. [Prestige — soft reset loop](#7-prestige--soft-reset-loop)
8. [Secondary Currencies](#8-secondary-currencies)
9. [Skill Tree — permanent bonuses](#9-skill-tree--permanent-bonuses)
10. [Research Tree — timed unlocks](#10-research-tree--timed-unlocks)
11. [Ascension — deep reset](#11-ascension--deep-reset)
12. [Combat & Wave System](#12-combat--wave-system)
13. [Minigames — active skill sessions](#13-minigames--active-skill-sessions)
14. [Merge Mechanic](#14-merge-mechanic)
15. [Buildings — grid placement](#15-buildings--grid-placement)
16. [Pets — equippable companions](#16-pets--equippable-companions)
17. [Events — timed calendar bonuses](#17-events--timed-calendar-bonuses)
18. [Challenges — optional objectives](#18-challenges--optional-objectives)
19. [Milestones — achievement gates](#19-milestones--achievement-gates)
20. [Leaderboard & Export](#20-leaderboard--export)
21. [Statistics & Run Summary](#21-statistics--run-summary)
22. [Notifications](#22-notifications)
23. [UI System](#23-ui-system)
24. [Offline Progression](#24-offline-progression)
25. [Click & Cursor Yield Modules](#25-click--cursor-yield-modules)
26. [Conversion System — cross-currency exchange](#26-conversion-system--cross-currency-exchange)
27. [Inventory & Loot](#27-inventory--loot)
28. [Game Flow State Machine](#28-game-flow-state-machine)
29. [Testing Your Game](#29-testing-your-game)
30. [Recipes — complete game types](#30-recipes--complete-game-types)
31. [Developer Toolset](#31-developer-toolset)
   - 31.1 [CI/CD Setup (GitHub Actions)](#cicd-setup-github-actions)
   - 31.2 [OpenUPM — Publishing the Package](#openupm--publishing-the-package)

---

## 1. Philosophy & Architecture

### One Rule: Everything is a Module

Endless Engine is built around one principle: **every system is optional**. You pick
only what your game needs. A simple cookie clicker uses 4 systems. A full idle-RPG
uses all 25+. The architecture never punishes you for keeping it simple.

### The Dependency Stack

```
ConfigRegistry (read-only SO config access)
      ↓
SaveService (persistence — all systems register here)
      ↓
EconomyService (the gold ledger — almost everything talks to this)
      ↓
TickEngine (1 Hz heartbeat — drives passive income, research, buildings)
      ↓
Optional Systems (generators, prestige, upgrades, pets, research, ...)
```

Every system initializes in this top-to-bottom order. The Bootstrap class wires them.

### ScriptableObjects Are Config, Not State

All gameplay values live in **ScriptableObjects** (`.asset` files). Code never has
magic numbers. If a number could ever be tuned in a spreadsheet, it belongs in an SO.

State (current gold, wave number, upgrade ranks) lives in `SaveData` — a plain C#
class serialized to JSON on disk.

### Static Events for UI

All services communicate via **static C# events** (`static event Action<T>`). UI
components subscribe to these events — they never poll. This means UI and gameplay
logic are completely decoupled: you can swap or remove UI without touching gameplay.

---

## 2. Installation

### Option A — Local Package (development)

In `Packages/manifest.json`:
```json
"com.endlessengine.idle": "file:../Packages/com.endlessengine.idle"
```

### Option B — Git URL

```json
"com.endlessengine.idle": "https://github.com/agobrik/endless-engine.git?path=Packages/com.endlessengine.idle"
```

### Option C — OpenUPM

```bash
openupm add com.endlessengine.idle
```

### Required Dependencies

These are declared in `package.json` and installed automatically:

| Package | Version | Purpose |
|---------|---------|---------|
| `com.unity.inputsystem` | 1.14.2 | Input abstraction layer |
| `com.unity.addressables` | 2.7.6 | Config asset loading |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | Save data serialization |

### Import the MinimalIdle Sample

1. **Window → Package Manager** → select **Endless Engine**
2. **Samples → MinimalIdle → Import**
3. Open `Assets/Samples/MinimalIdle/Scenes/MinimalIdle.unity`
4. Press **Play** — you have a working idle game in under 60 seconds.

---

## 3. The Bootstrap Pattern

Every Endless Engine game has a **Bootstrap MonoBehaviour** that wires all systems
together in the correct order. You never call `Initialize()` from within services
themselves — the Bootstrap owns the wiring.

### Minimal Bootstrap (4 systems)

```csharp
[DefaultExecutionOrder(-500)]   // run before everything else
public class MyBootstrap : MonoBehaviour
{
    [SerializeField] private SaveService          _saveService;
    [SerializeField] private EconomyService       _economyService;
    [SerializeField] private GeneratorSystem      _generators;
    [SerializeField] private PassiveIncomeService _passive;
    [SerializeField] private TickEngine           _tick;

    [SerializeField] private EconomyConfigSO    _econConfig;
    [SerializeField] private GeneratorDatabaseSO _genDatabase;
    // ... other config SOs

    private IEnumerator Start()
    {
        // 1. Inject config (in production, use Addressables; in samples, use InjectForTesting)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ConfigRegistry.InjectForTesting(economy: _econConfig, schema: _schemaVersion,
            prestige: _prestigeConfig, realm: _realmConfig);
#endif

        // 2. Wire systems in dependency order
        _economyService.Initialize(upgradeTreeQuery: null, saveNotifier: _saveService);
        _generators.Initialize(_genDatabase.Generators, _economyService, _saveService);
        _passive.Initialize(_generators, _economyService, gameFlow: null);

        // 3. Register save providers
        _saveService.RegisterStateProvider(_economyService);
        _saveService.RegisterStateProvider(_generators);

        // 4. Load save data
        bool done = false;
        _ = _saveService.LoadAsync().ContinueWith(_ => done = true,
            TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => done);

        Debug.Log("Ready!");
    }
}
```

### Full Bootstrap

See `Packages/com.endlessengine.idle/Runtime/Bootstrap/VerticalSliceBootstrap.cs`
for the complete example with all 25+ systems wired.

### Bootstrap Rules

1. **`[DefaultExecutionOrder(-500)]`** — always. Ensures the bootstrap runs before
   any MonoBehaviour that might read from services in `Start()`.
2. **Wire before Load** — always call `Initialize()` on all services before calling
   `_saveService.LoadAsync()`. Save providers must be registered before the load fires
   `OnAfterLoad` on each provider.
3. **Null-safe optional modules** — wrap optional systems in `if (_service != null)`.
   Leaving an Inspector field empty disables that module cleanly.

---

## 4. Core Systems (always needed)

### 4.1 EconomyService — gold ledger

`EndlessEngine.Economy.EconomyService`

The primary resource ledger. Tracks `CurrentResources` (gold/primary currency) and
enforces `ResourceHardCap`.

#### Config: `EconomyConfigSO`

| Field | Type | Description |
|-------|------|-------------|
| `ResourceHardCap` | `long` | Maximum gold the player can hold. 0 = no cap. |
| `StartingGold` | `long` | Gold on new game. Usually 0. |
| `BaseGoldPerTick` | `float` | Flat bonus added every tick before generators. |

Create via: **right-click in Project → Create → Endless Engine → Economy Config**

#### Initialization

```csharp
_economyService.Initialize(
    upgradeTreeQuery: _upgradeTreeService,  // null if no upgrades
    saveNotifier:     _saveService);        // null if no save
```

#### Key API

```csharp
economyService.AddResources(1000);           // add 1000 gold
economyService.DeductResources(500);         // deduct 500 (returns false if insufficient)
long gold = economyService.CurrentResources; // read balance

EconomyService.OnResourcesChanged += amount => UpdateGoldLabel(amount);
EconomyService.OnUpgradePurchased += nodeId => RefreshUpgradePanel();
```

#### Save/Load

`EconomyService` implements `ISaveStateProvider` — register it with SaveService and
it persists automatically. No extra code needed.

---

### 4.2 TickEngine — heartbeat

`EndlessEngine.Flow.TickEngine`

A 1 Hz (by default) timer that drives all time-based systems. Passive income,
research, and buildings are all driven by `TickEngine.OnTick`.

#### Config

No SO needed. Configure in Inspector on the TickEngine component:

| Field | Type | Description |
|-------|------|-------------|
| `TickIntervalSeconds` | `float` | Default 1.0. Lower = faster ticks (use with caution). |
| `TimeScale` | `float` | Multiplier. 2.0 = 2× speed. Used by TimeBoostService. |

#### Usage

```csharp
// Anything can subscribe to the tick
TickEngine.OnTick += dt => mySystem.Update(dt);

// Fire manually in tests
TickEngine.FireTickForTesting(1f);
```

`dt` is the effective delta — `TickIntervalSeconds * TimeScale`. Always use `dt`
for income calculations, not a hardcoded `1.0f`.

---

### 4.3 SaveService — persistence

`EndlessEngine.SaveAndLoad.SaveService`

Handles load-on-start, periodic auto-save, and atomic writes. Uses Newtonsoft.Json.
Save file location: `Application.persistentDataPath/save.json`.

#### The ISaveStateProvider Pattern

Every system that needs to persist state implements `ISaveStateProvider`:

```csharp
public class MySystem : MonoBehaviour, ISaveStateProvider
{
    public int ProviderOrder => 10;  // lower = runs first during save/load

    public void OnBeforeSave(SaveData saveData)
    {
        saveData.MyData = _currentState;
    }

    public void OnAfterLoad(SaveData saveData)
    {
        _currentState = saveData.MyData ?? defaultValue;
    }
}
```

Register with `saveService.RegisterStateProvider(mySystem)` **before** calling
`saveService.LoadAsync()`.

#### Loading

```csharp
_saveService.OnSaveLoaded += (saveData, isNewGame) =>
{
    if (isNewGame) Debug.Log("New game started!");
    else Debug.Log($"Loaded save: Wave {saveData.WaveNumber}");
};

yield return StartCoroutine(LoadAsync());
```

#### Auto-Save

SaveService auto-saves after any `ISaveStateProvider` marks itself dirty. You can
also trigger manual saves:

```csharp
_ = _saveService.SaveAsync();
```

---

### 4.4 ConfigRegistry — config access

`EndlessEngine.Config.ConfigRegistry`

A global read-only registry for ScriptableObject configs. In production, configs are
loaded via Addressables. In the editor and in samples, use `InjectForTesting()`.

#### In Production (Addressables)

Tag your config SOs with the Addressables address matching the registry key (e.g.
`"economy-config"`). The registry loads them on boot automatically.

#### In Development / Samples

```csharp
ConfigRegistry.InjectForTesting(
    economy:  myEconomyConfigSO,
    schema:   mySchemaVersionSO,
    prestige: myPrestigeConfigSO,
    realm:    myRealmIdentityConfigSO);
```

#### Reading Config

```csharp
EconomyConfigSO econCfg = ConfigRegistry.Economy;
WaveConfigSO    waveCfg = ConfigRegistry.Wave;
```

All properties throw `InvalidOperationException` if the registry hasn't been loaded.
Use `ConfigRegistry.IsLoaded` to guard before reading outside of bootstrap.

---

## 5. Generators — passive income

Generators produce gold automatically every tick. The classic idle game loop:
buy generators → earn more gold → buy more generators.

### Config: `GeneratorConfigSO`

| Field | Type | Description |
|-------|------|-------------|
| `GeneratorId` | `string` | Unique key (e.g. `"gold_mine"`). |
| `DisplayName` | `string` | Shown in UI. |
| `BaseYieldPerSecond` | `float` | Gold produced per second per unit. |
| `BaseCost` | `long` | First purchase cost. |
| `CostScalingFactor` | `float` | 1.15 = each purchase costs 15% more. |
| `MaxCount` | `int` | -1 = unlimited. |

Create: **right-click → Create → Endless Engine → Generator Config**

Bundle multiple generators into a **`GeneratorDatabaseSO`**:

```
right-click → Create → Endless Engine → Generator Database
```

Drag all `GeneratorConfigSO` assets into the `Generators` array.

### Wiring

```csharp
_generatorSystem.Initialize(
    configs:      _generatorDatabase.Generators,
    economy:      _economyService,
    saveNotifier: _saveService);

_passiveIncomeService.Initialize(
    generators: _generatorSystem,
    economy:    _economyService,
    gameFlow:   _gameFlow);  // null = always full income; non-null = applies run modifier

_saveService.RegisterStateProvider(_generatorSystem);
```

### Purchasing Generators

```csharp
bool bought = generatorSystem.TryPurchase("gold_mine");

// Listen for purchases
GeneratorSystem.OnGeneratorPurchased += (id, newCount) => RefreshGeneratorUI(id);
```

### How Yield Is Calculated

```
totalYield = Σ (generator.BaseYieldPerSecond × count × upgradeMultiplier)
income per tick = totalYield × runModifier × dt
```

`upgradeMultiplier` comes from the `UpgradeTreeService` — upgrade nodes tagged with
`AffectedStat = GeneratorYield` are factored in automatically.

### Editor Tool: Generator Asset Creator

**Window → Endless Engine → Generator Asset Creator** — creates generator SO + config
asset with one click, with sensible defaults filled in.

---

## 6. Upgrade Tree — stat progression

A directed acyclic graph (DAG) of unlock nodes. Each node applies a stat multiplier
when purchased. Nodes can have prerequisites.

### Config: `UpgradeNodeConfigSO` (inside `UpgradeTreeConfigSO`)

| Field | Type | Description |
|-------|------|-------------|
| `NodeId` | `string` | Unique key (e.g. `"mine_efficiency_1"`). |
| `AffectedStat` | `UpgradeStatType` | Which stat this modifies. |
| `EffectPerRank` | `float` | Multiplier added per rank. |
| `EffectType` | `EffectType` | Additive or Multiplicative. |
| `MaxRank` | `int` | How many times this can be purchased. |
| `BaseCost` | `long` | Cost at rank 0. |
| `PrerequisiteNodeIDs` | `string[]` | Must be unlocked first. |
| `SelectionWeight` | `int` | Weight for upgrade card random selection. |

### Wiring

```csharp
_upgradeTreeService.Initialize(/* no params — reads from ConfigRegistry */);
// or inject directly:
_upgradeTreeService.InitializeWithNodes(upgradeNodeConfigs);

_saveService.RegisterStateProvider(_upgradeTreeService);
```

### Purchasing Upgrades

```csharp
// Check if affordable and unlockable
bool canBuy = upgradeTreeService.CanUnlock("mine_efficiency_1");

// Purchase (costs are paid via EconomyService automatically)
bool bought = upgradeTreeService.TryPurchase("mine_efficiency_1", economyService);

// Read current rank
int rank = upgradeTreeService.GetRank("mine_efficiency_1");
```

### Reading Stat Multipliers

```csharp
float mult = upgradeTreeService.GetTotalMultiplier(UpgradeStatType.GeneratorYield);
// GeneratorSystem reads this automatically — you don't need to call it manually.
```

### Upgrade Card System (for run-based games)

For rogue-lite runs where the player picks from 3 random upgrade cards:

```csharp
// Draw 3 cards weighted by SelectionWeight
UpgradeNodeConfigSO[] cards = upgradeCardPool.Draw(3);

// Player picks one
upgradeTreeService.TryPurchase(cards[selectedIndex].NodeId, economyService);
```

### Editor Tool: Upgrade Tree Editor

**Window → Endless Engine → Upgrade Tree Editor** — visual node graph editor.
Drag to create nodes, draw edges for prerequisites.

---

## 7. Prestige — soft reset loop

The core idle loop extender. Player resets progress in exchange for a permanent
multiplier that makes future runs faster.

### Config: `PrestigeConfigSO`

| Field | Type | Description |
|-------|------|-------------|
| `BaseMultiplierPerPrestige` | `float` | e.g. 1.5 = each prestige adds ×1.5 multiplier. |
| `MinGoldToPrestige` | `long` | Gold required to unlock the prestige button. |

### Wiring

```csharp
// PrestigeStateManager is a MonoBehaviour — add to Bootstrap GameObject
_saveService.RegisterStateProvider(_prestigeManager);
```

`PrestigeStateManager` handles everything else via its own `OnEnable` subscription
to `EconomyService`.

### Triggering Prestige

```csharp
// Check eligibility
bool canPrestige = prestigeManager.CanPrestige();

// Execute prestige (resets gold, upgrades; keeps prestige count + multiplier)
bool done = prestigeManager.TryPrestige();
```

### Events

```csharp
PrestigeStateManager.OnPrestigeStarted += () => ShowPrestigeAnimation();
PrestigeStateManager.OnPrestigeCompleted += count => UpdatePrestigeCounter(count);
```

### What Resets on Prestige

By default: gold, upgrade ranks, wave number, run state.  
By config (on secondary currencies): set `ResetsOnPrestige = true`.  
Generators survive prestige (by default) — they're the "permanent investment".

---

## 8. Secondary Currencies

Beyond gold, you can add unlimited secondary currencies: gems, crystals, tokens,
shards — anything.

### Config: `CurrencyConfigSO`

| Field | Type | Description |
|-------|------|-------------|
| `CurrencyId` | `string` | Unique key (e.g. `"gems"`). |
| `HardCap` | `double` | 0 = no cap. |
| `StartingAmount` | `double` | Balance on new game. |
| `ResetsOnPrestige` | `bool` | Whether this currency resets on prestige. |
| `Notation` | `BigNumberNotation` | Letter / Scientific / Engineering. |
| `DecimalPlaces` | `int` | Display decimal places. |

Bundle into `CurrencyDatabaseSO`: **Create → Endless Engine → Currency Database**

### Wiring

```csharp
_currencyService.Initialize(_currencyDatabase);
_saveService.RegisterStateProvider(_currencyService);
```

### Usage

```csharp
currencyService.Add("gems", 100);
bool spent = currencyService.TrySpend("gems", 50);
double balance = currencyService.GetBalance("gems");
bool canAfford = currencyService.CanAfford("gems", 200);

string display = currencyService.GetFormatted("gems");  // e.g. "1.5K"
```

### Events

```csharp
CurrencyService.OnCurrencyChanged += (id, newBalance, delta) => RefreshCurrencyUI(id);
CurrencyService.OnSpendFailed     += (id, attempted, current) => PlayErrorSound();
```

---

## 9. Skill Tree — permanent bonuses

Unlike the upgrade tree (run-scoped), the skill tree provides **permanent** bonuses
that survive prestige. Typically unlocked by spending a prestige currency.

### Config: `SkillTreeConfigSO`

Each `SkillTreeConfigSO` defines one tree (e.g. "Combat Tree", "Economy Tree").
Contains a list of `SkillNodeConfig`:

| Field | Type | Description |
|-------|------|-------------|
| `SkillId` | `string` | Unique key. |
| `DisplayName` | `string` | Shown in editor. |
| `UnlockCost` | `long` | Cost in prestige currency or gold. |
| `PassiveBonusType` | `SkillBonusType` | What this skill improves. |
| `BonusAmount` | `float` | Amount of the bonus. |
| `PrerequisiteSkillIds` | `string[]` | DAG prerequisites. |

### Wiring

```csharp
_skillTreeService.Initialize(_skillTreeConfigs);
_saveService.RegisterStateProvider(_skillTreeService);
```

### Usage

```csharp
bool unlocked = skillTreeService.TryUnlock("combat_power_1", currencyService);
bool isActive = skillTreeService.IsUnlocked("combat_power_1");
float bonus   = skillTreeService.GetTotalBonus(SkillBonusType.DamageMultiplier);
```

### Editor Tool: Skill Tree Editor

**Window → Endless Engine → Skill Tree Editor** — same visual node editor as the
upgrade tree, but for skill trees.

---

## 10. Research Tree — timed unlocks

Research is progress that takes real time (or ticks) to complete. The player starts
research, waits, then collects the result.

### Config: `ResearchTreeConfigSO`

Each tree contains `ResearchNodeConfig` entries:

| Field | Type | Description |
|-------|------|-------------|
| `ResearchId` | `string` | Unique key. |
| `DurationTicks` | `int` | How many ticks to complete. |
| `Cost` | `long` | Upfront cost when starting research. |
| `UnlockEffect` | `ResearchEffect` | What becomes available on completion. |
| `PrerequisiteIds` | `string[]` | Must be completed first. |

### Wiring

```csharp
_researchService.Initialize(_researchTrees, _economyService);
_saveService.RegisterStateProvider(_researchService);

// Research is driven by TickEngine
TickEngine.OnTick += _researchService.OnTick;
```

### Usage

```csharp
bool started = researchService.TryStartResearch("advanced_mining");

float progress = researchService.GetProgress("advanced_mining");  // 0.0 to 1.0
bool done = researchService.IsCompleted("advanced_mining");

ResearchService.OnResearchCompleted += id => ShowResearchCompletePopup(id);
```

### EventService Integration

If you have `EventService` active, research speed is automatically multiplied by the
active event's `ResearchSpeedMultiplier`. No extra wiring needed.

---

## 11. Ascension — deep reset

A deeper reset than prestige. The player resets *everything* (including prestige
count) in exchange for permanent Ascension bonuses that persist forever.

### Config: `AscensionDatabaseSO`

Contains `AscensionTierConfig` entries, each defining bonuses for that tier.

### Wiring

```csharp
_ascensionManager.Initialize(
    database:        _ascensionDatabase,
    prestigeManager: _prestigeManager,
    saveService:     _saveService,
    economyService:  _economyService,
    generatorSystem: _generatorSystem,
    currencyService: _currencyService);

_saveService.RegisterStateProvider(_ascensionManager);
```

### Triggering Ascension

```csharp
bool canAscend = ascensionManager.CanAscend();
ascensionManager.TryAscend(AscensionScope.Full);  // Full or Deep reset
```

---

## 12. Combat & Wave System

For idle-vs games — enemies spawn in waves, the player (auto-battle or manual) fights
them, and killing enemies earns gold.

### Configs

**`WaveConfigSO`** — global wave parameters:

| Field | Type | Description |
|-------|------|-------------|
| `TotalWavesPerRun` | `int` | Waves before run ends. |
| `EnemiesPerWave` | `int` | Base enemy count. |
| `EnemyCountScaling` | `float` | Multiplier per wave (1.1 = 10% more each wave). |
| `GoldPerKillBase` | `long` | Base gold per kill. |

**`EnemyStatConfigSO`** — enemy stat baseline:

| Field | Type | Description |
|-------|------|-------------|
| `BaseMaxHP` | `float` | HP at wave 1. |
| `HpScalingPerWave` | `float` | HP multiplier per wave. |
| `MoveSpeed` | `float` | Movement speed (valid range: 0.5–20). |
| `ContactDamage` | `float` | Damage on contact with player. |

**`PlayerBaseStatConfigSO`** — player stats:

| Field | Type | Description |
|-------|------|-------------|
| `BaseMaxHP` | `float` | Player HP. |
| `AttackDamage` | `float` | Base auto-attack damage. |
| `AttackSpeed` | `float` | Attacks per second. |

### Wiring

```csharp
_waveSpawnManager.Initialize(_enemyManager, prefabPool: null, _healthSystem);
_enemyManager.Initialize(playerQuery: _playerHealth, damageDispatcher: _damageDispatchAdapter);

// Connect to GameFlow
GameFlowStateMachine.OnEnteredRun     += () => _waveSpawnManager.StartFirstWave();
GameFlowStateMachine.OnEnteredPostRun += () => _waveSpawnManager.StopWaves();

// Gold on kill
EnemyManager.OnEnemyKilled += agent => _economyService.AddResources(agent.GoldDropAmount);
```

### Auto-Battle

`AutoBattleController` handles combat automatically without player input:

```csharp
_autoBattle.Initialize(
    enemyManager:     _enemyManager,
    waveSpawnManager: _waveSpawnManager,
    statProvider:     new BaseStatUpgradeProvider(_playerConfig),
    playerConfig:     _playerConfig,
    waveConfig:       ConfigRegistry.Wave,
    playerId:         _playerHealth.EntityId);

_autoBattle.StartCombat();
```

---

## 13. Minigames — active skill sessions

Active skills give players a timed mini-session where repeated actions (taps, clicks,
key presses) earn bonus gold. Adds an "active play" layer to an idle game.

### Config: `ActiveSkillConfigSO`

| Field | Type | Description |
|-------|------|-------------|
| `SkillId` | `string` | Unique key. |
| `MinigameDurationSeconds` | `float` | How long the session lasts. |
| `CooldownSeconds` | `float` | Time before skill can be used again. |
| `BaseGoldReward` | `long` | Gold at zero actions. |
| `PerActionBonus` | `float` | Multiplier added per player action. |
| `MaxRewardMultiplier` | `float` | Cap on total multiplier. |

### Wiring

```csharp
_minigameService.Initialize(_activeSkills, _economyService);
```

### Usage

```csharp
// Check and trigger
if (minigameService.CanTrigger("tap_frenzy"))
    minigameService.TryTrigger("tap_frenzy");

// Player taps/clicks during session
minigameService.RecordAction();

// End early (e.g. UI close button)
minigameService.EndSession();

// Events
MinigameService.OnMinigameStarted += skill  => ShowMinigamePanel(skill);
MinigameService.OnActionRecorded  += (s, n) => UpdateActionCounter(n);
MinigameService.OnMinigameEnded   += (s, r) => ShowReward(r);
MinigameService.OnSkillReady      += id     => ShowReadyIndicator(id);
```

### Cooldown UI

```csharp
float remaining = minigameService.GetCooldown("tap_frenzy");
// 0 = ready, >0 = seconds remaining
```

---

## 14. Merge Mechanic

Players combine inventory items of the same tier to produce higher-tier items.
Classic merge-3 / merge-2 pattern.

### Config: `MergeConfigSO`

| Field | Type | Description |
|-------|------|-------------|
| `InputItemId` | `string` | Item to merge (must have 2+ in inventory). |
| `OutputItemId` | `string` | Result item. |
| `InputCount` | `int` | How many inputs consumed per merge. |
| `GoldBonus` | `long` | Bonus gold awarded on merge. |

### Wiring

```csharp
_mergeService.Initialize(_mergeConfigs, _inventoryService, _economyService);
```

### Usage

```csharp
bool merged = mergeService.TryMerge("iron_ore");
// Consumes InputCount of "iron_ore", produces 1 "steel_ingot", awards GoldBonus

MergeService.OnMerged += (inputId, outputId, gold) => ShowMergeEffect(outputId);
MergeService.OnMergeFailed += (inputId, reason) => ShowMergeError(reason);
```

---

## 15. Buildings — grid placement

Players place buildings on a grid. Each building generates resources on tick.

### Config: `BuildingConfigSO`

| Field | Type | Description |
|-------|------|-------------|
| `BuildingId` | `string` | Unique key. |
| `DisplayName` | `string` | Shown in UI. |
| `PlacementCost` | `long` | Gold to place. |
| `GoldPerTick` | `float` | Income when placed. |
| `UpgradeTiers` | `BuildingUpgradeTier[]` | Cost and bonus per upgrade tier. |
| `GridWidth` / `GridHeight` | `int` | Size on grid. |

### Wiring

```csharp
_buildingService.Initialize(_buildingConfigs, _economyService);
_saveService.RegisterStateProvider(_buildingService);

// BuildingService is tick-driven
TickEngine.OnTick += _buildingService.OnTick;
```

### Usage

```csharp
// Place at grid position
bool placed = buildingService.TryPlace("lumber_mill", gridX: 2, gridY: 0,
    instanceId: Guid.NewGuid().ToString());

// Upgrade a placed building
bool upgraded = buildingService.TryUpgrade(instanceId);

// Remove
buildingService.Remove(instanceId);

BuildingService.OnBuildingPlaced   += (id, inst) => SpawnBuildingVisual(id, inst);
BuildingService.OnBuildingUpgraded += (inst, tier) => UpdateBuildingVisual(inst, tier);
BuildingService.OnTickIncome       += gold => ShowTickEffect(gold);
```

---

## 16. Pets — equippable companions

Pets are unlockable companions that apply passive bonuses. Players level them up,
evolve them, and equip one active pet at a time.

### Config: `PetConfigSO`

| Field | Type | Description |
|-------|------|-------------|
| `PetId` | `string` | Unique key. |
| `DisplayName` | `string` | Pet name. |
| `UnlockCost` | `long` | Cost to unlock. |
| `LevelUpCost` | `long` | Base cost to level up. |
| `EvolutionLevel` | `int` | Level at which pet evolves. |
| `PassiveEffect` | `PetEffectType` | What the pet improves. |
| `EffectPerLevel` | `float` | Bonus per level. |

### Wiring

```csharp
_petService.Initialize(_petConfigs, _economyService);
_saveService.RegisterStateProvider(_petService);
```

### Usage

```csharp
petService.TryUnlock("phoenix");
petService.TryEquip("phoenix");
petService.TryLevelUp("phoenix");

float bonus = petService.GetActiveBonus(PetEffectType.GoldMultiplier);

PetService.OnPetEquipped  += id   => UpdatePetUI(id);
PetService.OnPetLeveledUp += (id, level) => ShowLevelUpEffect(id, level);
PetService.OnPetEvolved   += id   => ShowEvolutionCutscene(id);
```

---

## 17. Events — timed calendar bonuses

Limited-time events (weekend bonuses, holiday events) that temporarily boost income
or research speed.

### Config: `EventScheduleConfigSO`

| Field | Type | Description |
|-------|------|-------------|
| `EventId` | `string` | Unique key. |
| `DisplayName` | `string` | e.g. "Golden Weekend". |
| `StartHour` / `EndHour` | `int` | Hour-of-day window (UTC). |
| `StartDayOfWeek` | `DayOfWeek` | Start day. |
| `DurationHours` | `float` | Length of event. |
| `GoldMultiplier` | `float` | Gold income boost (1.0 = no boost). |
| `ResearchSpeedMultiplier` | `float` | Research speed boost. |

### Wiring

```csharp
_eventService.Initialize(_eventSchedules);
```

### Usage

```csharp
bool active = eventService.IsEventActive("golden_weekend");
float goldMult = eventService.GetGoldMultiplier();

EventService.OnEventStarted += id => ShowEventBanner(id);
EventService.OnEventEnded   += id => HideEventBanner(id);
```

`ResearchService` automatically reads `EventService.GetResearchSpeedMultiplier()`
on each tick — no extra wiring.

---

## 18. Challenges — optional objectives

Time-limited or condition-based optional objectives that reward the player upon
completion.

### Config: `ChallengeConfigSO`

| Field | Type | Description |
|-------|------|-------------|
| `ChallengeId` | `string` | Unique key. |
| `DisplayName` | `string` | Challenge title. |
| `ObjectiveType` | `ChallengeObjectiveType` | Reach gold / upgrade count / wave. |
| `TargetValue` | `double` | Required value. |
| `Reward` | `long` | Gold reward on completion. |
| `TimeLimit` | `float` | Seconds (0 = no limit). |

### Wiring

```csharp
_challengeService.Initialize(_economyService, _upgradeTreeService);
```

### Usage

```csharp
challengeService.ActivateChallenge("kill_100_enemies");
float progress = challengeService.GetProgress("kill_100_enemies");
bool done = challengeService.IsCompleted("kill_100_enemies");

ChallengeService.OnChallengeCompleted += (id, reward) => ShowReward(id, reward);
ChallengeService.OnChallengeFailed    += id => ShowFailureUI(id);
```

---

## 19. Milestones — achievement gates

Milestones track long-term player achievements (e.g. "reach 1M gold", "complete 10
prestiges"). They fire events and can trigger `UnlockLogService` entries.

### Config: `MilestoneDatabaseSO`

Contains `MilestoneConfig` entries:

| Field | Type | Description |
|-------|------|-------------|
| `MilestoneId` | `string` | Unique key. |
| `DisplayName` | `string` | Achievement title. |
| `ConditionType` | `MilestoneCondition` | Gold / Prestige / Wave / Custom. |
| `TargetValue` | `double` | Required value. |
| `ResetsOnPrestige` | `bool` | Whether this re-locks on prestige. |

### Wiring

```csharp
_milestoneTracker.Initialize(
    database:        _milestoneDatabase,
    economyService:  _economyService,
    prestigeManager: _prestigeManager,
    currencyService: _currencyService,
    generatorSystem: _generatorSystem);

_saveService.RegisterStateProvider(_milestoneTracker);

// Connect wave events if using combat
WaveSpawnManager.OnWaveStarted += wave => _milestoneTracker.NotifyWaveChanged(wave);
```

### Events

```csharp
MilestoneTracker.OnMilestoneCompleted += config => ShowAchievementBanner(config.DisplayName);
```

---

## 20. Leaderboard & Export

### Leaderboard

Tracks high scores across multiple boards (e.g. "Most Gold", "Fastest Prestige").

```csharp
_leaderboardService.Initialize(_leaderboardConfigs);
leaderboardService.SubmitScore("most_gold", playerScore);
LeaderboardEntry[] top10 = leaderboardService.GetTopScores("most_gold", count: 10);
```

### Export / Import

Lets players export their save as a base64 string (for backup or sharing).

```csharp
_exportService.Initialize(_saveService);

string exportCode = exportService.ExportCurrentSave();
// Show to player — they copy it

bool imported = exportService.TryImport(playerEnteredCode);
ExportService.OnImportCompleted += () => ReloadScene();
```

---

## 21. Statistics & Run Summary

Tracks numeric statistics across the session (total gold earned, enemies killed, etc.)
and produces a per-run summary at run end.

### Config: `StatDefinitionSO`

| Field | Type | Description |
|-------|------|-------------|
| `StatId` | `string` | Unique key (e.g. `"total_gold_earned"`). |
| `DisplayName` | `string` | Shown in statistics screen. |
| `ResetOnPrestige` | `bool` | Whether this stat resets on prestige. |

### Wiring

```csharp
_statisticsService.Initialize(_statDefinitions);
_saveService.RegisterStateProvider(_statisticsService);
```

### Usage

```csharp
statisticsService.Add("enemies_killed", 1);
statisticsService.Set("current_wave", waveNumber);
double total = statisticsService.Get("total_gold_earned");
```

### Run Summary

```csharp
var summary = RunSummaryData.Create(
    startTime:            runStartTime,
    endTime:              DateTime.UtcNow,
    goldEarned:           goldThisRun,
    killCount:            killsThisRun,
    maxWave:              highestWave,
    prestigeCountAtStart: prestigeCount,
    prestigePerformed:    didPrestige,
    upgradesAccepted:     upgradeCount,
    cascadeMultiplier:    cascadeMult,
    finalIncomeRate:      currentIncome);

float duration = summary.DurationSeconds;
```

---

## 22. Notifications

Push in-game notifications to the player (e.g. "Research complete!", "New event!").

```csharp
notificationService.Push("Research complete: Advanced Mining!", duration: 3f);
notificationService.Push("Golden Weekend has started!", duration: 5f,
    category: NotificationCategory.Event);

NotificationService.OnNotificationPushed += msg => ShowToastUI(msg);
```

`NotificationAutoTrigger` — a helper component that automatically pushes a
notification when a configured event fires (e.g. ResearchCompleted). Wire it in
the Inspector without any code.

---

## 23. UI System

Endless Engine ships a complete UI Toolkit (UXML/USS) UI for every system. Each
screen has a controller class and a paired UXML document.

### Available Screens

| Screen | Controller | UXML |
|--------|-----------|------|
| HUD | `HUDController` | `HUD.uxml` |
| Generator | `GeneratorScreenController` | `GeneratorScreen.uxml` |
| Upgrade | `UpgradeScreenController` | `UpgradeScreen.uxml` |
| Upgrade Card Overlay | `UpgradeCardUI` | `UpgradeCardOverlay.uxml` |
| Prestige | `PrestigeScreenUI` | `PrestigeOverlay.uxml` |
| Ascension | `AscensionOverlayController` | `AscensionOverlay.uxml` |
| Post-Run | `PostRunController` | `PostRun.uxml` |
| Building Grid | `BuildingScreenController` | `BuildingGridScreen.uxml` |
| Pet | `PetScreenController` | `PetScreen.uxml` |
| Inventory | `InventoryScreenController` | `InventoryScreen.uxml` |
| Research | `ResearchScreenController` | `ResearchScreen.uxml` |
| Challenge | `ChallengeScreenController` | `ChallengeScreen.uxml` |
| Statistics | `StatisticsScreenController` | `StatisticsScreen.uxml` |
| Unlock Log | `UnlockLogScreenController` | `UnlockLogScreen.uxml` |
| Event Banner | `EventBannerController` | `EventBannerOverlay.uxml` |
| Leaderboard | `LeaderboardScreenController` | `LeaderboardScreen.uxml` |
| Export Dialog | `ExportDialogController` | `ExportDialog.uxml` |
| Main Menu | `MainMenuController` | (Canvas-based) |

### Adding a Screen to Your Scene

1. Add a `UIDocument` component to a GameObject in your scene.
2. Assign the UXML asset to the `Source Asset` field.
3. Add the matching Controller component to the same GameObject.
4. Wire the service references in the Inspector.

### Subscribing to UI Events

All screens use static service events — they subscribe in `OnEnable` and unsubscribe
in `OnDisable`. You never need to call "refresh" manually.

### Customizing Appearance

Edit the `.uss` file paired with each `.uxml`. All screens use USS variables for
colors and fonts — override them in a root USS to retheme the entire UI at once:

```css
/* MyTheme.uss */
:root {
    --color-gold: rgb(255, 200, 50);
    --color-bg-panel: rgba(20, 20, 30, 0.95);
    --font-size-title: 18px;
}
```

---

## 24. Offline Progression

When the player returns after being away, `OfflineTimeCalculator` computes how much
gold they earned during the offline period.

### Config: `RunConfigSO`

| Field | Type | Description |
|-------|------|-------------|
| `MaxOfflineHours` | `float` | Cap on offline time credited (e.g. 8h). |
| `OfflineEfficiency` | `float` | 0.5 = earn 50% of online rate offline. |
| `ActiveRunPassiveModifier` | `float` | Passive income multiplier during a run. |

### Wiring

```csharp
_offlineCalculator.Initialize();  // no params
// Offline calculation fires automatically after SaveService.LoadAsync()
```

### Events

```csharp
OfflineTimeCalculator.OnOfflineGainCalculated += (hours, gold) =>
    ShowOfflineRewardPopup(hours, gold);
```

---

## 25. Click & Cursor Yield Modules

Two ways to add active player input to your idle game.

### ClickYieldService — tap/click for gold

```csharp
_clickYieldService.Initialize(
    config:             _clickConfig,      // ClickSourceConfigSO
    economy:            _economyService,
    passiveYieldGetter: () => _passive.TotalYieldPerSecond);

_clickYieldService.SetInputProvider(_inputProvider);
```

**`ClickSourceConfigSO`** key fields:

| Field | Type | Description |
|-------|------|-------------|
| `GoldPerClick` | `float` | Base gold per click. |
| `YieldRateClickFraction` | `float` | Bonus = fraction of current income/s (0 = disabled). |
| `EnableCombo` | `bool` | Combo multiplier for rapid clicks. |
| `MaxComboMultiplier` | `float` | Cap on combo multiplier. |
| `CritChance` | `float` | 0–1. Probability of crit hit. |
| `CritMultiplier` | `float` | Crit damage multiplier. |
| `MaxClicksPerSecondCap` | `int` | Anti-cheat: max clicks/s counted. |
| `BaseAutoClicksPerSecond` | `float` | Auto-clicker rate (0 = manual only). |

### CursorYieldService — mouse movement earns gold

```csharp
_cursorYieldService.Initialize(
    config:   _cursorConfig,    // CursorActivityConfigSO
    economy:  _economyService,
    gameFlow: _gameFlow,
    input:    _inputProvider);
```

---

## 26. Conversion System — cross-currency exchange

Players exchange one currency for another at configured rates.

### Config: `ConversionDatabaseSO`

Contains `ConversionRecipeSO` entries:

| Field | Type | Description |
|-------|------|-------------|
| `InputCurrencyId` | `string` | Currency to spend. |
| `InputAmount` | `double` | Amount to spend. |
| `OutputCurrencyId` | `string` | Currency to receive. |
| `OutputAmount` | `double` | Amount received. |

### Usage

```csharp
_conversionService.Initialize(_conversionDatabase, _economyService, _currencyService);

bool converted = conversionService.TryConvert("gold_to_gems");
ConversionService.OnConverted += (recipeId, inAmt, outAmt) => ShowConversionEffect();
```

---

## 27. Inventory & Loot

### InventoryService

Tracks item stacks. Used by Merge, Loot drops, and Challenges.

```csharp
_inventoryService.Initialize(_itemDatabase);
_saveService.RegisterStateProvider(_inventoryService);

inventoryService.Add("iron_ore", 3);
int count = inventoryService.GetCount("iron_ore");
bool removed = inventoryService.TryRemove("iron_ore", 2);
```

### Loot / DropResolver

```csharp
// DropTableConfigSO defines weighted item drops
var drops = dropResolver.Resolve(_dropTable, rollCount: 5);
foreach (var drop in drops)
    inventoryService.Add(drop.ItemId, drop.Count);
```

---

## 28. Game Flow State Machine

`GameFlowStateMachine` manages the Menu → InRun → PostRun cycle for games with
timed runs.

### States

```
Menu ──StartRun()──► InRun ──EndRun()──► PostRun ──ReturnToMenu()──► Menu
```

### Usage

```csharp
_gameFlow.StartRun();      // transition to InRun
_gameFlow.EndRun();        // transition to PostRun
_gameFlow.ReturnToMenu();  // transition to Menu

bool inRun = _gameFlow.IsInRun;

GameFlowStateMachine.OnEnteredRun     += () => StartWaves();
GameFlowStateMachine.OnEnteredPostRun += () => ShowPostRunScreen();
GameFlowStateMachine.OnEnteredMenu    += () => ShowMainMenu();
```

### GameFlow + PassiveIncome

When `IsInRun == true`, `PassiveIncomeService` applies `RunConfigSO.ActiveRunPassiveModifier`
(default 0.5) to passive income — because the player is also earning from kills.
When in Menu, full passive income applies.

---

## 29. Testing Your Game

All Endless Engine services have test helper methods guarded by
`#if UNITY_EDITOR || DEVELOPMENT_BUILD`. Use Unity Test Runner (EditMode) for logic
tests.

### Test Pattern

```csharp
[SetUp]
public void SetUp()
{
    // Inject minimal config
    var econConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
    econConfig.ResourceHardCap = 1_000_000L;
    ConfigRegistry.InjectForTesting(economy: econConfig);

    // Create service
    var go = new GameObject();
    _economy = go.AddComponent<EconomyService>();
    _economy.Initialize(null, null);
    _economy.OnAfterLoad(new SaveData { SchemaVersion = 1 });
}

[TearDown]
public void TearDown()
{
    ConfigRegistry.ClearForTesting();
    Object.DestroyImmediate(_economy.gameObject);
}

[Test]
public void AddResources_IncreasesBalance()
{
    _economy.AddResources(1000);
    Assert.AreEqual(1000L, _economy.CurrentResources);
}
```

### EditMode MonoBehaviour Gotcha

`OnEnable` does NOT fire for `AddComponent<T>()` in EditMode unless the class has
`[ExecuteInEditMode]`. Services that subscribe to events in `OnEnable` expose
`SubscribeForTesting()` / `UnsubscribeForTesting()` helpers for this reason:

```csharp
_currency.SubscribeForTesting();   // call after Initialize() in SetUp
_currency.UnsubscribeForTesting(); // call in TearDown
```

Services with this pattern: `EconomyService`, `CurrencyService`, `MilestoneTracker`,
`PassiveIncomeService`.

---

## 30. Recipes — complete game types

### Recipe A: Pure Idle (Cookie Clicker / AdVenture Capitalist)

**Systems:** EconomyService + TickEngine + SaveService + GeneratorSystem + PassiveIncomeService + UpgradeTreeService + PrestigeStateManager

No combat. No run timer. Generators accumulate forever.

```
ConfigRegistry → EconomyService → GeneratorSystem → PassiveIncomeService
                              ↓
                        UpgradeTreeService → multipliers feed into generators
                              ↓
                       PrestigeStateManager → reset + multiplier
```

Start with MinimalIdle sample. Add prestige when the core loop is fun.

### Recipe B: Idle-vs / Idle-RPG (Endless Engine default)

**Systems:** All of Recipe A + WaveSpawnManager + EnemyManager + HealthSystem + AutoBattleController + GameFlowStateMachine + RunSessionManager

Run-based: player starts a run, kills waves, earns gold, ends run, sees PostRun screen.

Use `VerticalSliceBootstrap` as your starting point.

### Recipe C: Merge Game

**Systems:** EconomyService + SaveService + InventoryService + MergeService + (optional: UpgradeTreeService for merge speed)

No generators needed. Gold comes from merges and sells.

### Recipe D: Prestige-Heavy Idle (Adventure Capitalist / NGU Idle style)

**Systems:** Recipe A + CurrencyService (prestige tokens) + SkillTreeService (permanent tree) + AscensionStateManager

Three progression layers: run upgrades → prestige multiplier → ascension bonuses.

### Recipe E: Deep Idle RPG (Melvor Idle style)

**Systems:** All systems. Research unlocks content. Events drive engagement. Skill tree for long-term build diversity. Challenges for daily goals. Statistics for player satisfaction.

Start with Recipe B. Add research + skill tree after the combat loop is fun.
Add events + challenges in the content polish phase.

---

## 31. Developer Toolset

All tools live under **Tools → Endless Engine** in the Unity menu bar.

### New Game Wizard

**Tools → Endless Engine → New Game Wizard**

The fastest way to start a new game. Select a **Game Type** from the dropdown and
the wizard pre-configures modules and fills economy config values with sensible
presets for that game style:

| Game Type | Pre-enabled Modules | Economy Preset |
|-----------|--------------------|-|
| Pure Idle | Generator + Prestige | IdleYieldBase 0.5, PrestigeMultiplier 1.5× |
| Clicker Idle | Click + Generator + Prestige | IdleYieldBase 0.1, fast early game |
| Idle-vs / RPG | Generator + Wave/Combat + Prestige + Multi-Currency | PrestigeMultiplier 2×, 50-wave runs |
| Merge Idle | Click only | IdleYieldBase 0, merge-focused |
| Research Idle | Generator + Prestige + Multi-Currency | OfflineCap 24h |
| Building Idle | Generator + Zone + Prestige | OfflineCap 12h |
| Prestige-Heavy | Generator + Prestige + Multi-Currency | PrestigeMultiplier 3×, deep economy |
| Custom | None (manual) | Defaults |

You can override individual module toggles after selecting a type. Click
**Generate Skeleton** to produce:

- `Assets/[GameName]/Configs/` — SO assets with preset values
- `Assets/[GameName]/Scripts/[GameName]Bootstrap.cs` — fully wired bootstrap class
- `Assets/[GameName]/Scenes/` — placeholder folder

```
Tools → Endless Engine → New Game Wizard
```

---

### Economy Tuning Window

**Tools → Endless Engine → Economy Tuning**

A multi-tab visual analysis and editing tool for your economy configs.

| Tab | What it shows |
|-----|--------------|
| Gold Curve | Passive income over generator copies, with prestige overlay |
| Gen Costs | Cost scaling bar charts per generator |
| Prestige | Permanent multiplier curve + threshold table |
| Wave Economy | Gold drops per wave, enemy count scaling |
| **Simulation** | Full cross-system snapshot — player state → projected income at 5m / 30m / 8h |
| Config Editor | Inline edit all economy SO fields and save |
| Conversion | Exchange rate visualisation |
| Soft Cap | Soft-cap curve preview |

---

### ID Registry

**Tools → Endless Engine → ID Registry**

Scans every `ScriptableObject` in the project (Endless Engine namespace) and
collects all string fields whose name ends in `Id` or `ID`. Reports:

- **Duplicates** — the same ID value used across two or more assets of the same type
- **Empty** — blank or whitespace ID values
- **Orphan Refs** — `PrerequisiteNodeIDs` entries in `UpgradeNodeConfigSO` that
  reference a `NodeId` that doesn't exist anywhere in the project

Click any row to ping and select the asset in the Project window. Use the filter
buttons (All / Duplicates / Empty / Orphans) and the search field to narrow results.

```
Tools → Endless Engine → ID Registry  →  Scan All SOs
```

---

### Config Validator

`ConfigValidator.Validate(resolvedConfigs)` runs automatically inside
`ConfigLoadingService` before the registry is populated. It now performs both
**field-level** and **graph-level** checks:

**Field-level** (always active):
- Negative or zero values where positive is required
- Out-of-range float fields (MoveSpeed, CritChance, etc.)
- Schema version consistency

**Graph-level** (upgrade node set):
- **Duplicate NodeIds** — two nodes with the same string ID
- **Orphan references** — a `PrerequisiteNodeID` that points to a non-existent node
- **Cycle detection** — DFS detects and reports the full cycle path if the
  prerequisite graph contains a loop

You can also call the graph check directly:

```csharp
bool ok = ConfigValidator.ValidateUpgradeGraph(
    upgradeNodes, realmSlug: "base", ConfigValidator.ValidationMode.Error);
```

---

### Upgrade Tree Editor / Skill Tree Editor

**Window → Endless Engine → Upgrade Tree Editor**  
**Window → Endless Engine → Skill Tree Editor**

Visual node graph editors. Drag to create nodes, draw edges for prerequisites.
Changes write directly to `UpgradeNodeConfigSO` / `SkillNodeConfigSO` assets.

---

### Schema Bump Utility

**Tools → Endless Engine → Schema Bump**

When you change `SaveData.cs` (add, remove, or rename a field), run Schema Bump to:

1. Increment `SchemaVersionSO.CurrentSchemaVersion` by 1
2. Generate a migration scaffold at `Assets/Scripts/Runtime/SaveAndLoad/Migrations/Migration_N_to_N+1.cs`

Fill in the generated `Migrate()` method with the field mutations, then register
the migration in `MigrationPipeline`:

```csharp
var pipeline = new MigrationPipeline(new IMigration[]
{
    new Migration_0_to_1(),
    new Migration_1_to_2(),   // ← generated by Schema Bump
});
```

The scaffold is also callable from code (useful in tests):

```csharp
SchemaBumpUtility.Bump(schemaVersionSO);  // Editor-only
```

---

### Content Pack Wizard

**Tools → Endless Engine → Content Pack Wizard**

Creates a complete **RealmPack** — a self-contained set of all 9 canonical config SOs
for a new game realm or content expansion — in a single click.

```
Tools → Endless Engine → Content Pack Wizard
```

#### What it creates

Given slug `fire-realm` and output folder `Assets/Configs/Realms`:

```
Assets/Configs/Realms/fire-realm/
  EnemyStatConfig_fire-realm.asset
  WaveConfig_fire-realm.asset
  EconomyConfig_fire-realm.asset
  UpgradeSelectionConfig_fire-realm.asset
  PrestigeConfig_fire-realm.asset
  RealmIdentityConfig_fire-realm.asset    ← RealmSlug and DisplayName pre-set
  PlayerBaseStatConfig_fire-realm.asset
  SchemaVersion_fire-realm.asset
  RealmPack_fire-realm.asset              ← all 9 SOs wired as references
```

The `RealmPack_fire-realm.asset` is immediately usable as a
`ConfigRegistry.BeginRealmSwap()` argument to hot-swap all config values atomically.

#### RealmRegistrySO auto-registration

The wizard finds the project's `RealmRegistrySO` via `AssetDatabase` and adds a
`RealmEntry` for the new realm automatically. If no registry exists, it creates one
next to the output folder.

Use **Find Registry** button to ping the existing registry in the Project window.

#### After creation

1. Tune the 9 SO assets in the Inspector (or use `Economy Tuning Window`).
2. Add `UpgradeNodeConfigSO` assets to `RealmPack.UpgradeNodeConfigs[]`.
3. Run `Tools → Endless Engine → ID Registry` and `Config Validator` to verify no issues.
4. Wire the pack: `ConfigRegistry.BeginRealmSwap(realmPack)`.

#### Switching realms at runtime

```csharp
// Swap all config values atomically to a different realm
ConfigRegistry.BeginRealmSwap(realmRegistry.GetPack("fire-realm"));
```

---

### Generator Asset Creator / Upgrade Tree Asset Creator

**Window → Endless Engine → Generator Asset Creator**  
**Window → Endless Engine → Upgrade Tree Asset Creator**

One-click creators that produce a correctly-named SO asset with sane defaults.
Faster than right-clicking in the Project window for iterative content work.

---

### CI/CD Setup (GitHub Actions)

The package ships with a `.github/workflows/tests.yml` workflow that runs all
EditMode unit tests on every push to `main`. It uses
[Game CI](https://game.ci/) (`game-ci/unity-test-runner@v4`) and requires three
repository secrets.

#### Step 1 — Get your Unity license XML

1. Install [Unity Hub](https://unity.com/download) locally (already done if you
   develop locally).
2. Run the activation step from the Game CI docs — the easiest path:

   ```bash
   # Clone a minimal repo or use this project's workflow
   # Trigger the manual activation workflow once:
   # https://game.ci/docs/github/activation
   ```

   Alternatively, use the **manual license file** approach:
   - Open Unity Hub → Preferences → Licenses → Add License (Activate).
   - Unity creates a `.ulf` file on disk at:
     - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
     - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Open that file in a text editor and copy the entire contents — this is
     your `UNITY_LICENSE` value (a multi-line XML string).

   > For CI to work the license type **must match** what the CI runner needs.
   > Personal/Student licenses work fine for open-source projects.

#### Step 2 — Add secrets to GitHub

1. Go to your repository on GitHub.
2. Click **Settings → Secrets and variables → Actions → New repository secret**.
3. Add all three secrets:

   | Secret name | Value |
   |-------------|-------|
   | `UNITY_LICENSE` | The full contents of your `.ulf` file (XML, ~30 lines) |
   | `UNITY_EMAIL` | Your Unity account email (e.g. `mskirboga@yahoo.com`) |
   | `UNITY_PASSWORD` | Your Unity account password |

4. After adding all three, push any commit to `main` — the **Actions** tab will
   show the `Run Unity Tests` workflow running.

#### Step 3 — Verify CI is green

- Go to **Actions → Run Unity Tests → latest run**.
- If it fails with `License activation error`: double-check the `UNITY_LICENSE`
  value — it must be the raw XML text, not base64-encoded.
- If it fails with `No tests found`: make sure the `Tests/unit/` folder is inside
  the Unity project root and the assembly definition references `NUnit.Framework`.
- A green run badge appears in your README automatically once the workflow passes.

---

### OpenUPM — Publishing the Package

[OpenUPM](https://openupm.com) is the open-source Unity package registry. After
registration, developers install the package with a single command:

```bash
openupm add com.endlessengine.idle
```

#### Requirements before submitting

Your `package.json` must have:
- `"name"` matching the scoped package format (`com.[scope].[name]`)
- `"version"` following semver (`1.0.4`)
- `"unity"` field set (`"6000.0"`)
- `"author"` with `name`, `email`, and `url`
- `"repository"` with `type: "git"` and a valid GitHub URL
- A public GitHub repository — OpenUPM reads tags to determine versions

All of these are already in place in `package.json`.

#### Step 1 — Tag the release on GitHub

OpenUPM discovers versions by scanning git tags. Each tag must match the version
in `package.json` exactly:

```bash
git tag v1.0.4
git push origin v1.0.4
```

Future releases: bump `package.json` version → update `CHANGELOG.md` → tag →
push. OpenUPM picks up new versions automatically within ~24 hours.

#### Step 2 — Submit to OpenUPM

1. Go to [openupm.com/packages/add](https://openupm.com/packages/add/).
2. Paste your package's GitHub URL:
   ```
   https://github.com/agobrik/endless-engine
   ```
3. OpenUPM auto-reads `package.json` and validates the manifest.
4. Fill in the category (choose **Framework** or **Tools**) and submit.
5. The OpenUPM team reviews submissions — typical approval is within 1–3 days.
6. Once approved, the package page appears at
   `openupm.com/packages/com.endlessengine.idle/`.

#### Step 3 — Installing (what users do after approval)

```bash
# Install the OpenUPM CLI once
npm install -g openupm-cli

# Inside their Unity project root
openupm add com.endlessengine.idle
```

This automatically adds the scoped registry to `Packages/manifest.json` and
installs the package — no manual registry editing needed.

#### Updating the package

```bash
# 1. Bump version in package.json  (e.g. 1.0.4 → 1.0.5)
# 2. Add CHANGELOG.md [1.0.5] entry
# 3. Commit and tag
git tag v1.0.5
git push origin v1.0.5
# OpenUPM picks up the new tag automatically — no re-submission needed
```

---

## Appendix: Field Reference Quick-Look

All ScriptableObjects and their create menu paths:

| SO Class | Create Path |
|----------|------------|
| `EconomyConfigSO` | Endless Engine → Economy Config |
| `WaveConfigSO` | Endless Engine → Wave Config |
| `EnemyStatConfigSO` | Endless Engine → Enemy Stat Config |
| `PlayerBaseStatConfigSO` | Endless Engine → Player Base Stat Config |
| `RunConfigSO` | Endless Engine → Run Config |
| `PrestigeConfigSO` | Endless Engine → Prestige Config |
| `SchemaVersionSO` | Endless Engine → Schema Version |
| `RealmIdentityConfigSO` | Endless Engine → Realm Identity Config |
| `GeneratorConfigSO` | Endless Engine → Generator Config |
| `GeneratorDatabaseSO` | Endless Engine → Generator Database |
| `UpgradeTreeConfigSO` | Endless Engine → Upgrade Tree Config |
| `CurrencyConfigSO` | Endless Engine → Currency Config |
| `CurrencyDatabaseSO` | Endless Engine → Currency Database |
| `SkillTreeConfigSO` | Endless Engine → Skill Tree Config |
| `ResearchTreeConfigSO` | Endless Engine → Research Tree Config |
| `AscensionDatabaseSO` | Endless Engine → Ascension Database |
| `ActiveSkillConfigSO` | Endless Engine → Active Skill Config |
| `MergeConfigSO` | Endless Engine → Merge Config |
| `BuildingConfigSO` | Endless Engine → Building Config |
| `PetConfigSO` | Endless Engine → Pet Config |
| `MilestoneDatabaseSO` | Endless Engine → Milestone Database |
| `EventScheduleConfigSO` | Endless Engine → Event Schedule Config |
| `ChallengeConfigSO` | Endless Engine → Challenge Config |
| `LeaderboardConfigSO` | Endless Engine → Leaderboard Config |
| `ClickSourceConfigSO` | Endless Engine → Click Source Config |
| `CursorActivityConfigSO` | Endless Engine → Cursor Activity Config |
| `DropTableConfigSO` | Endless Engine → Drop Table Config |
| `ConversionDatabaseSO` | Endless Engine → Conversion Database |
| `ItemConfigSO` | Endless Engine → Item Config |
| `StatDefinitionSO` | Endless Engine → Stat Definition |

---

*Endless Engine v1.0.4 — © 2026 Endless Engine Contributors*
