# Endless Engine — Idle Game Toolkit

[![openupm](https://img.shields.io/npm/v/com.endlessengine.idle?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.endlessengine.idle/)
[![Unity](https://img.shields.io/badge/Unity-6.3%20LTS-blue)](https://unity.com/releases/lts)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.md)
[![Version](https://img.shields.io/badge/version-1.3.4-brightgreen)](Packages/com.endlessengine.idle/CHANGELOG.md)

**Modular idle/incremental game engine for Unity 6.3 LTS.**  
Zero-config bootstrap, one-click New Game Wizard, 35+ opt-in systems — build any idle game in hours, not weeks.

---

## What's in this repo?

This repository is the **development project** for the Endless Engine UPM package.

```
endless-engine/
├── Packages/com.endlessengine.idle/   ← The actual UPM package (Runtime, Editor, Samples, Docs)
├── Assets/                            ← Unity test/demo project (not part of the package)
├── .github/                           ← Issue templates, PR template
├── ENDLESS_ENGINE_SYSTEM_REFERENCE.md ← Full system reference (TR/EN)
└── ENDLESS_ENGINE_OYUN_URETIM_REHBERI.md ← Step-by-step game production guide (TR)
```

The package lives at `Packages/com.endlessengine.idle/`.  
See [`Packages/com.endlessengine.idle/README.md`](Packages/com.endlessengine.idle/README.md) for full system list, API overview, and editor tools.

---

## Installation (3 ways)

### Option 1 — OpenUPM (recommended)

```bash
openupm add com.endlessengine.idle
```

### Option 2 — Package Manager (Git URL)

In Unity: **Window → Package Manager → + → Add package from git URL…**

```
https://github.com/agobrik/endless-engine.git?path=Packages/com.endlessengine.idle
```

### Option 3 — Clone & open locally

```bash
git clone https://github.com/agobrik/endless-engine.git
```

Open the cloned folder as a Unity project. The package is already embedded under `Packages/`.

---

## Quick Start

### New Game Wizard (recommended for beginners)

1. **Tools → Endless Engine → New Game Wizard**
2. Enter a game name, select a game type (Pure Idle, Clicker, Wave RPG, Merge, Research, Building, Prestige…)
3. Click **Generate Skeleton**
4. Open the generated scene — press **Play**

No Inspector wiring, no code required to get started.

### Import a Sample

1. **Window → Package Manager → Endless Engine → Samples**
2. Click **Import** next to any sample
3. Open the imported scene — press **Play**

| Sample | What it shows |
|--------|--------------|
| **MinimalIdle** | Generator + upgrade + save/load — start here |
| **ClickerIdle** | Tap targets, combo, crit, offline gains |
| **HarvestLoop** | Drag-cursor harvest nodes, area damage, offline |
| **WaveIdle** | Auto-battle, wave scaling, upgrade cards, prestige |
| **MergeIdle** | Merge board, inventory, economy |
| **PrestHeavy** | Prestige + ascension + skill tree + research chain |

---

## Systems (35+, all opt-in)

| Category | Systems |
|----------|---------|
| Core | ConfigRegistry, SaveService, TickEngine, EconomyService |
| Active Gameplay | ClickLoopService, HarvestLoopService, ClickYieldService, CursorYieldService |
| Economy | GeneratorSystem, MergeService, ZoneSystem, InventoryService, OfflineTimeCalculator |
| Progression | UpgradeTreeService, SkillTreeService, ResearchService, PrestigeStateManager, AscensionStateManager |
| Engagement | QuestService, EventService, ChallengeService, MinigameService, MilestoneTracker |
| World | BuildingService, PetService, RealmSystem |
| Combat | WaveSpawnManager, AutoBattleController, DamageSystem, HealthSystem |
| Utility | AudioService, TutorialService, BigDouble, NotificationService |

---

## Editor Tools

All under **Tools → Endless Engine**:

| Tool | Purpose |
|------|---------|
| New Game Wizard | Scaffold any game type in one click |
| Generator Editor | Visual generator config (income, cost, unlock curves) |
| Upgrade Tree Editor | Drag-and-drop DAG node editor |
| Skill Tree Editor | Permanent talent tree layout tool |
| Economy Simulator | Mathematical curve preview without Play mode |
| Economy Tuning | Live parameter sliders with real-time feedback |
| Config Validator | Catch missing/broken ScriptableObject references |
| Content Pack Wizard | Bundle systems into a named content pack |

---

## Documentation

| File | Description |
|------|-------------|
| [`Packages/.../getting-started.md`](Packages/com.endlessengine.idle/Documentation~/getting-started.md) | Step-by-step setup guide |
| [`Packages/.../api-reference.md`](Packages/com.endlessengine.idle/Documentation~/api-reference.md) | Full API reference |
| [`Packages/.../cookbook.md`](Packages/com.endlessengine.idle/Documentation~/cookbook.md) | 30+ code recipes |
| [`Packages/.../kullanim-kilavuzu-tr.md`](Packages/com.endlessengine.idle/Documentation~/kullanim-kilavuzu-tr.md) | Turkish user guide (58 sections) |
| [`ENDLESS_ENGINE_SYSTEM_REFERENCE.md`](ENDLESS_ENGINE_SYSTEM_REFERENCE.md) | Complete system reference |
| [`ENDLESS_ENGINE_OYUN_URETIM_REHBERI.md`](ENDLESS_ENGINE_OYUN_URETIM_REHBERI.md) | Game production guide (TR) |

---

## Requirements

- Unity 6000.0+ (6.3 LTS)
- Input System 1.14.2+
- Addressables 2.7.6+
- Newtonsoft Json 3.2.2+

---

## License

MIT — see [LICENSE.md](LICENSE.md).

## Contributing

See [`Packages/com.endlessengine.idle/CONTRIBUTING.md`](Packages/com.endlessengine.idle/CONTRIBUTING.md).
