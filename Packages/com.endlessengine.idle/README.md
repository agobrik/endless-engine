# Endless Engine — Idle Game Toolkit

[![Tests](https://github.com/your-org/endless-engine/actions/workflows/tests.yml/badge.svg)](https://github.com/your-org/endless-engine/actions/workflows/tests.yml)
[![openupm](https://img.shields.io/npm/v/com.endlessengine.idle?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.endlessengine.idle/)

Modular idle game engine for Unity 6.3 LTS. All 25 systems are config-driven, modular,
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
"com.endlessengine.idle": "https://github.com/your-org/endless-engine.git?path=Packages/com.endlessengine.idle"
```

## Systems (25 total — all opt-in)

| System | Category | Description |
|--------|----------|-------------|
| `EconomyService` | Core | Gold ledger — AddResources, DeductResources |
| `SaveService` | Core | Atomic save, ISaveStateProvider chain |
| `TickEngine` | Core | 1 Hz heartbeat, TimeScale |
| `ConfigRegistry` | Core | SO config access (Addressables) |
| `GeneratorSystem` | Economy | Passive income generators |
| `PassiveIncomeService` | Economy | Wires generators → economy per tick |
| `CurrencyService` | Economy | Secondary currencies |
| `ConversionService` | Economy | Cross-currency recipes |
| `InventoryService` | Economy | Slot-based item stacks |
| `MergeService` | Economy | Tier-progression merge (2×N → 1×N+1) |
| `UpgradeTreeService` | Progression | Branching upgrade nodes |
| `SkillTreeService` | Progression | Free-form talent tree |
| `ResearchService` | Progression | Queue-based research |
| `PrestigeStateManager` | Progression | Reset loop + multiplier |
| `AscensionStateManager` | Progression | Multi-layer prestige |
| `ChallengeService` | Engagement | Restriction modifier modes |
| `MinigameService` | Engagement | Cooldown-gated active skills |
| `TimeBoostService` | Engagement | Temporary speed multiplier |
| `MilestoneTracker` | Engagement | Achievement conditions |
| `StatisticsService` | Analytics | Lifetime counters + peaks |
| `EventService` | Live | Calendar + rotation time-gated events |
| `LeaderboardService` | Social | Local PlayerPrefs leaderboard |
| `ExportService` | Utility | Build code export/import |
| `BuildingService` | World | Placeable passive-income buildings |
| `PetService` | Companions | Equippable companions with passive bonuses |

## Quick Start

1. Open `Tools → Endless Engine → New Game Wizard`
2. Follow the wizard to create your project skeleton
3. Import the **MinimalIdle** sample for a working reference

**Or** follow the step-by-step tutorial in `Documentation~/getting-started.md`.

## Editor Tools

- `Tools → Endless Engine → Generator Editor`
- `Tools → Endless Engine → Upgrade Tree Editor`
- `Tools → Endless Engine → Skill Tree Editor`
- `Tools → Endless Engine → Economy Tuning` (6 tabs)
- `Tools → Endless Engine → New Game Wizard`

## Documentation

- `Documentation~/getting-started.md` — step-by-step setup guide
- `Documentation~/api-reference.md` — full API reference for all 25 systems
- `Samples~/MinimalIdle/` — working minimal idle game

## Contributing

See `CONTRIBUTING.md`.

## License

MIT — see `LICENSE.md`.
