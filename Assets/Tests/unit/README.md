# Unit Tests

Unit tests for Endless Engine gameplay systems.

## Framework

- **Engine**: Unity Test Framework (NUnit) — `com.unity.test-framework`
- **Runner**: `game-ci/unity-test-runner@v4` (GitHub Actions)
- **Assembly**: `EndlessEngine.Tests.Unit` (EditMode)

## Running Tests

In Unity Editor: **Window → General → Test Runner → EditMode → Run All**

Via CI: `.github/workflows/tests.yml`

## Coverage Requirements (from coding-standards.md)

All Logic stories require unit tests before marking Done (BLOCKING gate).

Minimum coverage:
- Economy balance formulas (`EconomyService`, `UpgradeApplicationSystem`)
- Prestige state transitions (`PrestigeStateManager`)
- Offline time calculation (`OfflineTimeCalculator`)
- Config loading (`ConfigRegistry`, `ConfigLoadingService`)
- Wave scaling formulas (`WaveScalingCalculator`)
- Damage resolution (`DamageSystem`)
- Save serialization / migration (`SaveService`, `IMigration` chain)

## Naming Convention

- File: `[System]_[Feature]Tests.cs`
- Method: `[Method]_[Scenario]_[ExpectedResult]`
- Example: `DamageSystem_EnemyAttack_NeverCrits()`

## Test Isolation

- Each test sets up and tears down its own state
- Call `ConfigRegistry.ClearForTesting()` in `[TearDown]`
- Call `UpgradeApplicationSystem.ClearRunEffects()` in `[TearDown]`
- No Unity runtime required for pure C# system tests (DamageSystem, UAS, etc.)
- Use `MockInputProvider` for tests requiring input
