# Endless Engine — Getting Started Guide

Build a complete idle game from scratch in Unity 6.3 LTS.

---

## Prerequisites

- Unity 6.3 LTS or later
- Universal Render Pipeline (URP) 2D Renderer
- Newtonsoft.Json package (via Package Manager → `com.unity.nuget.newtonsoft-json`)

## Step 1: Install Endless Engine

### Via Package Manager (UPM)

```json
// Add to Packages/manifest.json:
"com.endlessengine.idle": "https://github.com/agobrik/endless-engine.git"
```

Or install from local path during development:

```json
"com.endlessengine.idle": "file:../Packages/com.endlessengine.idle"
```

### Import the MinimalIdle Sample

1. Open **Window → Package Manager**
2. Select **Endless Engine**
3. Click **Samples → MinimalIdle → Import**
4. Open `Assets/Samples/MinimalIdle/Scenes/MinimalIdle.unity`

---

## Step 2: Configure the Core Economy

Every idle game starts with a resource. Endless Engine calls this **gold** by default.

### 2a. Create EconomyConfigSO

Right-click in Project → **Create → Endless Engine → Economy Config**

Set:
- `BaseGoldPerTick`: 0 (generators handle production)
- `StartingGold`: 0

### 2b. Create a Generator

Right-click → **Create → Endless Engine → Generator Config**

Set:
- `GeneratorId`: `gold_mine`
- `DisplayName`: Gold Mine
- `GoldPerTick`: 5
- `BaseCost`: 0 (auto-unlocked)

Assign to a **GeneratorDatabaseSO** (Create → Endless Engine → Generator Database).

---

## Step 3: Wire the Bootstrap

Create a **GameObject** in your scene named `Bootstrap`.
Add the `VerticalSliceBootstrap` component (or your own bootstrap).

Assign in Inspector:
- **SaveService** → `Assets/Scripts/Runtime/SaveAndLoad/SaveService` component
- **EconomyService** → EconomyService component
- **TickEngine** → TickEngine component (1 Hz heartbeat)
- **GeneratorSystem** → GeneratorSystem component
- **PassiveIncomeService** → PassiveIncomeService component
- **EconomyConfig** → your EconomyConfigSO
- **GeneratorDatabase** → your GeneratorDatabaseSO

Press **Play** — gold begins accumulating at 5/tick.

---

## Step 4: Add Upgrades

### 4a. Create an Upgrade Node

Right-click → **Create → Endless Engine → Upgrade Node Config**

Set:
- `NodeId`: `mine_efficiency`
- `StatMultiplier`: 2.0 (doubles production)
- `Cost`: 50

### 4b. Wire to Bootstrap

Add an `UpgradeTreeService` component to your Bootstrap object.
Assign your upgrade node in the **UpgradeTreeConfigs** array.
Also assign the `UpgradeTreeService` to Bootstrap's `UpgradeTreeService` field.

When the player spends 50 gold on this upgrade, mine production doubles.

---

## Step 5: Add Prestige

Once the player has earned enough, they can reset for a permanent multiplier.

### 5a. Create PrestigeConfigSO

Right-click → **Create → Endless Engine → Prestige Config**

Set:
- `MinGoldToPrestige`: 10000
- `MultiplierPerPrestige`: 1.5

### 5b. Add PrestigeStateManager

Add `PrestigeStateManager` component to Bootstrap.
Assign to Bootstrap's `PrestigeManager` field.
Register with SaveService.

Call `prestigeManager.TryPrestige()` from UI button.

---

## Step 6: Add Save/Load

SaveService handles this automatically once providers are registered.

The bootstrap wires providers (Economy, UpgradeTree, Generators) to SaveService.
On Start, `LoadAsync()` is called. On application quit or `SaveAsync()` call, state is written.

Default save location: `Application.persistentDataPath/save_slot_0.json`

---

## Step 7: Add Optional Modules

Endless Engine modules are opt-in — leave fields unassigned in Bootstrap to disable them.

### Skill Tree

```
BuildingConfigSO + BuildingService → S17 buildings (passive production)
PetConfigSO + PetService           → S17 companions (passive bonuses)
ActiveSkillConfigSO + MinigameService → S15 active skills (tap minigames)
ResearchTreeConfigSO + ResearchService → S14 research queue
ChallengeConfigSO + ChallengeService   → S14 challenge modes
```

See `Documentation~/api-reference.md` for the full API reference per module.

---

## Step 8: Build Your UI

Endless Engine ships no UI — it's a systems toolkit.
Wire your own UI to static events:

```csharp
// Gold display
EconomyService.OnResourcesChanged += amount => goldLabel.text = amount.ToString();

// Wave display
WaveSpawnManager.OnWaveStarted += wave => waveLabel.text = $"Wave {wave}";

// Minigame start
MinigameService.OnMinigameStarted += skill => ShowMinigameOverlay(skill);
```

All services expose static C# events for UI binding.

---

## Common Patterns

### Pattern: Unlock an item and log it

```csharp
void OnEnemyDrop(string itemId)
{
    inventoryService.Add(itemId, 1);
    unlockLogService.Unlock(itemId);          // fires OnEntryUnlocked if first time
}
```

### Pattern: Fire a time-gated event multiplier

```csharp
void OnTick(float dt)
{
    float mult = eventService.GetCombinedIncomeMultiplier();
    passiveIncomeService.SetEventMultiplier(mult);
}
```

### Pattern: Export/import save for sharing

```csharp
string code = exportService.ExportToCode(saveService.CurrentSaveData);
// code is now in clipboard

// On import:
if (exportService.TryImportFromCode(pastedCode, out SaveData data))
    await saveService.ApplyImport(data);
```

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Gold not accumulating | Check PassiveIncomeService is initialized and TickEngine is present |
| Save not loading | Ensure all ISaveStateProviders are registered before LoadAsync |
| Upgrade not applying | UpgradeTreeService must be passed to EconomyService.Initialize() |
| Events not firing | Static events use C# delegates — subscribe before Initialize() is called |

---

## Next Steps

- `api-reference.md` — full API reference for all modules
- `Samples~/MinimalIdle/` — working example with source code
- Sprint 19 (planned): OpenUPM release, full integration test suite
