# Migration Guide — Endless Engine

## Upgrading from v1.0.0 to v1.0.1

No breaking changes. New APIs added:

- `SaveService.GetCurrentSaveData()` — returns current in-memory SaveData
- `SaveService.ApplyImportedSaveData(SaveData)` — replaces save state after import
- `ExportService.ExportCurrentSave()` — convenience wrapper; calls GetCurrentSaveData() internally
- `PrestigeConfigSO.MinGoldToPrestige` — new optional gold gate field (default 0 = no gate)
- `PrestigeStateManager.InjectConfigForTesting(PrestigeConfigSO)` — EditMode test helper (guarded with `#if UNITY_EDITOR || DEVELOPMENT_BUILD`)
- `PrestigeStateManager.TryPrestige(EconomyService)` — synchronous test overload (same guard)

**New UI controllers** (Sprint 21):
`BuildingScreenController`, `PetScreenController`, `UnlockLogScreenController`,
`EventBannerController`, `LeaderboardScreenController`, `ExportDialogController` —
all in `Runtime/UI/`. Wire via Inspector `[SerializeField]` fields on your UI GameObject.

---

## Upgrading from v0.x to v1.0.0

### Current package layout (v1.0.0+)

As of v1.0.0, **all Runtime scripts and Editor tools live inside the UPM package**.
There is nothing to migrate if you install via UPM (`.tgz` or git URL).

```
Packages/com.endlessengine.idle/
  Runtime/    — 160 C# files (25 systems)
  Editor/     — 7 editor windows (EconomyTuning, GeneratorEditor, UpgradeTreeEditor, etc.)
  Samples~/   — MinimalIdle working sample
  Documentation~/
```

### Assembly definition names

| Location | Assembly Name | Purpose |
|----------|--------------|---------|
| `Packages/.../Runtime/` | `EndlessEngine.Runtime` | Package runtime — sole authority |
| `Packages/.../Editor/` | `EndlessEngine.Editor` | Package editor windows |
| `Assets/Scripts/` | `EndlessEngine.GameSample` | Game-layer code (your game) |
| `Assets/Tests/` | `EndlessEngine.Tests` | Unit tests (not in package) |
| `Assets/Tests/integration/` | `EndlessEngine.Tests.Integration` | Integration tests (not in package) |

### ConfigRegistry

`ConfigRegistry` is a static service locator populated at boot by `ConfigLoadingService`
(Addressables-based). For unit tests, use the inject/clear pattern:

```csharp
ConfigRegistry.InjectForTesting(economy: myEconomySO, ...);
// run tests ...
ConfigRegistry.ClearForTesting();
```

### SaveService wiring

Register all `ISaveStateProvider` implementations in `Start()` before `LoadAsync()`.
Provider order constants are in `SaveConstants.SaveProviderOrder`:

```csharp
_saveService.RegisterStateProvider(_economyService);    // order 10
_saveService.RegisterStateProvider(_buildingService);   // order 15
_saveService.RegisterStateProvider(_generatorSystem);   // order 20
_saveService.RegisterStateProvider(_upgradeTreeService);// order 25
// ...
await _saveService.LoadAsync();
```

### Editor tools

All editor windows are in `Packages/com.endlessengine.idle/Editor/` and appear
under the **Tools → Endless Engine** menu after installation.

---

## Publishing as .tgz

```bash
cd Packages/
npm pack com.endlessengine.idle
# → com.endlessengine.idle-1.0.1.tgz
```

Install in another project:
```json
"com.endlessengine.idle": "file:/path/to/com.endlessengine.idle-1.0.1.tgz"
```

Or via OpenUPM after publishing:
```bash
openupm add com.endlessengine.idle
```
