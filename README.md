# Endless Engine — Idle Game Toolkit

[![Unity](https://img.shields.io/badge/Unity-6.3%20LTS-blue)](https://unity.com/releases/lts)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.md)
[![Version](https://img.shields.io/badge/version-1.3.4-brightgreen)](Packages/com.endlessengine.idle/CHANGELOG.md)

**Modular idle/incremental game engine for Unity 6.3 LTS.**  
Zero-config bootstrap, one-click New Game Wizard, 35+ opt-in systems — build any idle game without writing code.

---

## Quick Start

### 1. Install

In Unity: **Window → Package Manager → + → Add package from git URL…**

```
https://github.com/agobrik/endless-engine.git?path=Packages/com.endlessengine.idle
```

Or clone and open this folder directly as a Unity project — the package is already embedded under `Packages/`.

### 2. Create Your Game

**Tools → Endless Engine → New Game Wizard**  
→ Enter a name → Select game type → **Generate Skeleton**  
→ Open the generated scene → Press **Play**

That's it. No Inspector wiring, no code required.

### 3. Create Package Prefabs (optional)

**Tools → Endless Engine → Create Package Prefabs**  
→ Generates ready-to-use gameplay prefabs for all game types

---

## Full Documentation

📖 **[GUIDE_00_Baslangic.md](GUIDE_00_Baslangic.md)** — Tek kaynak rehber (Türkçe)  
Kurulum, Wizard, sahne yapısı, config düzenleme, upgrade tree, UI, prefablar, prestige, save, build, Steam, troubleshooting.

---

## What's in this repo?

```
endless-engine/
├── Packages/com.endlessengine.idle/    ← UPM package (Runtime, Editor, Samples)
│   ├── Runtime/                        ← 35+ game systems + 29 UI controllers
│   ├── Editor/                         ← New Game Wizard, Prefab Factory, tuning tools
│   ├── Samples~/                       ← MinimalIdle, ClickerIdle, HarvestLoop, WaveIdle...
│   └── Runtime/Prefabs/                ← Per-game-type prefab sets (after Create Package Prefabs)
├── Assets/                             ← Unity test project (UI UXML, configs, scenes)
└── GUIDE_00_Baslangic.md               ← Full user guide
```

---

## Systems (35+, all opt-in)

| Category | Systems |
|----------|---------|
| Core | ConfigRegistry, SaveService, TickEngine, EconomyService |
| Active Gameplay | ClickLoopService, HarvestLoopService, ClickYieldService, CursorYieldService |
| Economy | GeneratorSystem, MergeService, ZoneSystem, InventoryService, OfflineTimeCalculator |
| Progression | UpgradeTreeService, ResearchService, PrestigeStateManager, AscensionStateManager |
| Engagement | QuestService, EventService, ChallengeService, MilestoneTracker |
| World | BuildingService, PetService, RealmSystem |
| Combat | WaveSpawnManager, AutoBattleController, DamageSystem, HealthSystem |
| Utility | AudioService, BigDouble, NotificationService |

---

## Editor Tools

All under **Tools → Endless Engine**:

| Tool | Purpose |
|------|---------|
| **New Game Wizard** | Scaffold any game type — scene + configs in one click |
| **Create Package Prefabs** | Generate prefab sets for all game types |
| **Economy Simulator** | Preview income curves without Play mode |
| **Economy Tuning** | Live parameter sliders with real-time feedback |
| **Upgrade Tree Editor** | Visual node editor for upgrade trees |
| **Config Validator** | Catch missing ScriptableObject references |
| **Generator Window** | Visual generator config editor |
| **Schema Bump** | Increment save schema version |

---

## Requirements

- Unity 6000.0+ (6.3 LTS)
- Input System 1.14.2+
- Addressables 2.7.6+
- Newtonsoft Json 3.2.2+

---

## License

MIT — see [LICENSE.md](LICENSE.md).
