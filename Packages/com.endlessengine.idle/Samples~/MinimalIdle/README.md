# MinimalIdle — Endless Engine Sample

**Play immediately after import — no Inspector wiring needed.**

A complete working idle game:
- 1 Generator (gold mine, 5 gold/s)
- 1 Upgrade (mine efficiency — doubles gold rate, 5 ranks)
- Gold display with income rate, mine count, upgrade status
- Auto-save / load on close/open

## How to Play

1. **Window → Package Manager → Endless Engine → Samples → MinimalIdle → Import**
2. Open `Assets/Samples/MinimalIdle/Scenes/MinimalIdle.unity`
3. Press **Play**

That's it. Gold accumulates instantly. Buy mines to increase income. Buy upgrades to multiply it.
Stop and Play again — progress is saved automatically.

## What This Demonstrates

| System | Class | What it does |
|--------|-------|-------------|
| Economy | `EconomyService` | Gold ledger, events on every change |
| Generator | `GeneratorSystem` | Per-second income from purchased mines |
| Tick | `TickEngine` | 1 Hz heartbeat drives all passive income |
| Upgrade | `UpgradeTreeService` | Rank-based efficiency multiplier |
| Save | `SaveService` | Persists all state to disk automatically |
| Bootstrap | `MinimalIdleBootstrap` | Wires everything, no Addressables required |
| UI | `MinimalIdleUI` | Auto-finds services, no Inspector slots needed |

## Key Files

```
Scripts/
  MinimalIdleBootstrap.cs   — wires all services in the correct order
  MinimalIdleUI.cs          — auto-finds services, handles all button/label updates
Configs/
  EconomyConfig.asset       — HardCap=1B, StartingGold=0
  GoldMine.asset            — GeneratorConfigSO (5 gold/s, cost 50)
  MineEfficiency.asset      — UpgradeNodeConfigSO (×2/rank, cost 50, 5 ranks)
  GeneratorDatabase.asset   — holds the GoldMine reference
  SchemaVersion.asset       — save schema versioning
  PrestigeConfig.asset      — prestige multiplier settings
  RealmIdentityConfig.asset — arena/world identity
```

## Extending This Sample

**Add more generators**: duplicate `GoldMine.asset`, change `GeneratorId`, `DisplayName`, `BaseYieldPerSecond`, `BaseCost`, then add it to `GeneratorDatabase`.

**Add prestige**: wire `PrestigeStateManager` as an optional module — see `VerticalSliceBootstrap` for the pattern.

**Add more upgrades**: create additional `UpgradeNodeConfigSO` assets, add to `ConfigRegistry.Upgrades` (or use `InjectForTesting` with an array).

**Go to full game**: see `Tools → Endless Engine → New Game Wizard` to generate a complete skeleton with the modules you need.
