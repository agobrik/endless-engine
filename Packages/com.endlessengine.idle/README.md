# Endless Engine — Idle Game Toolkit

[![openupm](https://img.shields.io/npm/v/com.endlessengine.idle?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.endlessengine.idle/)

Modular idle game engine for Unity 6.3 LTS. All systems are config-driven, modular,
and opt-in — add only what your game needs.

## Requirements

- Unity 6000.0+ (6.3 LTS recommended)
- Input System 1.14.2+
- Addressables 2.7.6+
- Newtonsoft Json 3.2.2+

## Installation

### Via OpenUPM

```bash
openupm add com.endlessengine.idle
```

### Via Package Manager (Git URL)

```json
"com.endlessengine.idle": "https://github.com/agobrik/endless-engine.git?path=Packages/com.endlessengine.idle"
```

### Via Package Manager (Local)

In Unity: **Window → Package Manager → + → Add package from disk…** → select `Packages/com.endlessengine.idle/package.json`

## Systems (all opt-in)

### Core
| System | Description |
|--------|-------------|
| `ConfigRegistry` | ScriptableObject config hub — Addressables + InjectForTesting |
| `SaveService` | Atomic write, auto-save, schema migration, ISaveStateProvider chain |
| `TickEngine` | Game clock — 1 Hz heartbeat, TimeScale, MaxTicksPerFrame |
| `EconomyService` | Gold ledger — AddResources, DeductResources, hard cap |

### Active Gameplay
| System | Description |
|--------|-------------|
| `ClickYieldService` | Instant gold on click/tap |
| `ClickLoopService` | Click targets with HP, combo, crit, auto-click, offline |
| `HarvestLoopService` | Cursor-drag harvest nodes, area damage, combo, offline |
| `CursorYieldService` | Mouse movement → gold (Speed / Distance / Hover models) |

### Economy
| System | Description |
|--------|-------------|
| `GeneratorSystem` | Passive income generators with bulk purchase |
| `PassiveIncomeService` | Wires generators → economy per tick |
| `ZoneSystem` | World-space income zones — passive or active (cursor-in) |
| `CurrencyService` | Secondary currencies |
| `ConversionService` | Cross-currency conversion with cooldown |
| `InventoryService` | Slot-based item stacks |
| `DropResolver` | Weighted-random drops with pity system |
| `MergeService` | Tier-progression merge (2×N → 1×N+1) |
| `OfflineTimeCalculator` | Offline gain formula, auto-activates on load |

### Progression
| System | Description |
|--------|-------------|
| `UpgradeTreeService` | DAG upgrade graph, prereqs, weighted card draw |
| `UpgradeApplicationSystem` | Stat calculator — cache, dirty flag, run vs permanent |
| `SkillTreeService` | Permanent talent tree, refund support |
| `ResearchService` | Queue-based tick-driven research tree |
| `PrestigeStateManager` | Soft reset + permanent multiplier |
| `AscensionStateManager` | Multi-layer deep reset + cascade multiplier |

### Engagement
| System | Description |
|--------|-------------|
| `ChallengeService` | Restriction modifier modes |
| `MinigameService` | Cooldown-gated active skills |
| `TimeBoostService` | Temporary TickEngine speed multiplier |
| `MilestoneTracker` | Threshold-based achievement conditions |
| `EventService` | Calendar time-gated events with income multipliers |
| `QuestService` | Daily/weekly goals with condition system |
| `TraitService` | Permanent trait selection at prestige/ascension |

### World & Companions
| System | Description |
|--------|-------------|
| `BuildingService` | Placeable grid buildings with passive production |
| `PetService` | Equippable companions — level, evolve, passive bonuses |

### Social & Analytics
| System | Description |
|--------|-------------|
| `StatisticsService` | Lifetime counters + peaks |
| `LeaderboardService` | Local leaderboard (synchronous API) |
| `NotificationService` | In-game notification queue (Singleton) |
| `UnlockLogService` | Discovery log with category filtering |

### Combat
| System | Description |
|--------|-------------|
| `WaveSpawnManager` | Wave scaling, upgrade card triggers |
| `AutoBattleController` | Automatic combat loop, nearest-enemy targeting |
| `DamageSystem` | Static event bus — crit, attacker type, block |
| `HealthSystem` | Entity HP registry, bridges DamageSystem |
| `PlayerHealthComponent` | Player HP, i-frames, IdleRecovery state |

### Utility
| System | Description |
|--------|-------------|
| `TutorialService` | Step-by-step tutorial with event-based progression |
| `RealmSystem` | Hot-swap game world configs at runtime |
| `AudioService` | Pooled SFX, music, AudioMixer snapshots |
| `BigDouble` | Large number backend for economies beyond 1e308 |

## Quick Start

1. Open **Tools → Endless Engine → New Game Wizard**
2. Select your game type and let the wizard create configs and scene skeleton
3. Import a **Sample** from Package Manager for a working reference

**Or** read `Documentation~/getting-started.md` for a step-by-step walkthrough.

## Samples

Import via **Window → Package Manager → Endless Engine → Samples**:

| Sample | What it demonstrates |
|--------|---------------------|
| **MinimalIdle** | Generator + passive income + upgrade + save/load |
| **ClickerIdle** | ClickLoopService — tap targets, combo, crit, offline |
| **HarvestLoop** | HarvestLoopService — drag cursor, area harvest, offline |
| **WaveIdle** | AutoBattle + wave system + upgrade cards + prestige |
| **MergeIdle** | MergeService + inventory + economy |
| **PrestHeavy** | Prestige + ascension + skill tree + research chain |

## Editor Tools

| Tool | Location |
|------|----------|
| New Game Wizard | Tools → Endless Engine → New Game Wizard |
| Content Pack Wizard | Tools → Endless Engine → Content Pack Wizard |
| Upgrade Tree Editor | Tools → Endless Engine → Upgrade Tree Editor |
| Skill Tree Editor | Tools → Endless Engine → Skill Tree Editor |
| Trait Tree Editor | Tools → Endless Engine → Trait Tree Editor |
| Generator Editor | Tools → Endless Engine → Generator Editor |
| Economy Tuning | Tools → Endless Engine → Economy Tuning |
| Economy Simulator | Tools → Endless Engine → Economy Simulator |
| Config Validator | Tools → Endless Engine → Config Validator |
| ID Registry | Tools → Endless Engine → ID Registry |
| Schema Bump | Tools → Endless Engine → Schema Bump |

## Documentation

- `Documentation~/getting-started.md` — step-by-step setup
- `Documentation~/kullanim-kilavuzu-tr.md` — full Turkish user guide (58 sections)
- `Documentation~/api-reference.md` — API reference index
- `Documentation~/migration-guide.md` — upgrading from previous versions

## License

MIT — see `LICENSE.md`.

## Contributing

See `CONTRIBUTING.md`.
