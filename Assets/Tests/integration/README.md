# Integration Tests

Integration tests for multi-system interactions in Endless Engine.

## Framework

- **Engine**: Unity Test Framework (NUnit) — `com.unity.test-framework`
- **Runner**: `game-ci/unity-test-runner@v4` (GitHub Actions)  
- **Assembly**: `EndlessEngine.Tests.Integration` (PlayMode)

## Running Tests

In Unity Editor: **Window → General → Test Runner → PlayMode → Run All**

Via CI: `.github/workflows/tests.yml`

## Scope

Integration tests verify multi-system contracts. Key scenarios:

- `DamageSystem → HealthSystem → VFXFeedbackSystem` event chain
- `WaveSpawnManager → EnemyPoolService → EnemyManager` lifecycle
- `PrestigeStateManager` two-save crash-safety sequence
- `SaveService → ISaveStateProvider` load/save round-trip
- `UpgradeTreeService → UAS → GetEffectiveStat` after purchase
- Enemy death during `EnemyManager.Update()` iteration (pending-recycle)

## Naming Convention

- File: `[SystemA]_[SystemB]_IntegrationTests.cs`
- Method: `[Scenario]_[ExpectedOutcome]()`
