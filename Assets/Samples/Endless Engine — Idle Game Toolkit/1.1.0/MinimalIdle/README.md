# MinimalIdle — Endless Engine Sample

A minimal working idle game demonstrating the core Endless Engine systems:

- 1 Generator (gold mine, 5 gold/tick)
- 1 Upgrade (mine efficiency — doubles gold rate)
- Gold economy with save/load
- TickEngine driving passive income

## What It Shows

| System | Class | Purpose |
|--------|-------|---------|
| Economy | `EconomyService` | Tracks gold, fires `OnResourcesChanged` |
| Generator | `GeneratorSystem` + `GeneratorConfigSO` | Produces gold per tick |
| Tick | `TickEngine` | 1 Hz heartbeat wired to `PassiveIncomeService` |
| Upgrade | `UpgradeTreeService` + `UpgradeNodeConfigSO` | One efficiency node |
| Save | `SaveService` | Persists economy + generator state across play sessions |
| Bootstrap | `MinimalIdleBootstrap` | Wires all systems (no scene refs needed beyond configs) |

## How to Use

1. Import this sample via **Package Manager → Endless Engine → Samples → MinimalIdle**.
2. Open `Assets/Samples/MinimalIdle/Scenes/MinimalIdle.unity`.
3. Press **Play** — gold accumulates, click **Buy Upgrade** to double the rate.
4. Stop and Play again — gold total persists via SaveService.

## Key Files

```
Scripts/
  MinimalIdleBootstrap.cs   — wires all services, drives the main loop
  MinimalIdleUI.cs          — displays gold count + upgrade button
Configs/
  EconomyConfig.asset       — EconomyConfigSO (HardCap=1B, StartingGold=0)
  SchemaVersion.asset       — SchemaVersionSO (CurrentSchemaVersion=1)
  PrestigeConfig.asset      — PrestigeConfigSO (BaseMultiplierPerPrestige=1.5)
  RealmIdentityConfig.asset — RealmIdentityConfigSO (slug=base)
  GoldMine.asset            — GeneratorConfigSO (GoldPerTick=5, BaseCost=0)
  MineEfficiency.asset      — UpgradeNodeConfigSO (Cost=50, StatMultiplier=2)
  GeneratorDatabase.asset   — GeneratorDatabaseSO (references GoldMine)
```

## Inspector Wiring

After importing the sample, assign the following in the Bootstrap GameObject Inspector:

| Field | Asset |
|-------|-------|
| Economy Config | `EconomyConfig.asset` |
| Schema Version | `SchemaVersion.asset` |
| Prestige Config | `PrestigeConfig.asset` |
| Realm Config | `RealmIdentityConfig.asset` |
| Generator Database | `GeneratorDatabase.asset` |

## Extending This Sample

- Add more generators: duplicate `GoldMine.asset`, change `BaseYieldPerSecond`, add to `GeneratorDatabase`.
- Add prestige: wire `PrestigeStateManager` following the same optional-module pattern in `VerticalSliceBootstrap`.
- Add a minigame: add `MinigameService` + `ActiveSkillConfigSO` — same optional pattern.

See `Packages/com.endlessengine.idle/Runtime/Bootstrap/VerticalSliceBootstrap.cs` for a full
wiring example with every optional module enabled.
