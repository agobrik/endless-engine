# Contributing to Endless Engine

## Development Setup

1. Clone the repo
2. Open the `endless-engine` Unity project in Unity 6.3 LTS
3. All scripts live under `Assets/Scripts/Runtime/` (authoritative) and are mirrored to `Packages/com.endlessengine.idle/Runtime/` (UPM target)
4. Tests live under `Assets/Tests/`

## Coding Standards

- All public APIs must have XML doc comments
- Balance values must live in ScriptableObjects — no magic numbers
- New services must implement `ISaveStateProvider` if they have persistent state
- Static events must have `ClearSubscribersForTesting()` guarded by `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
- All test injection helpers (`InjectXxxForTesting`) must be guarded likewise

## Testing

- Run tests via **Window → General → Test Runner → EditMode**
- All new systems require at least: success path, failure path, save/load roundtrip
- Integration tests for cross-system chains go in `Assets/Tests/integration/`

## Pull Request Guidelines

1. One feature or fix per PR
2. Include unit tests for any new logic
3. Update `CHANGELOG.md` under `[Unreleased]`
4. Ensure CI (GitHub Actions) passes before requesting review

## Adding a New Module

1. Create the ScriptableObject config in `Assets/Scripts/Runtime/Config/ScriptableObjects/`
2. Create the service MonoBehaviour in `Assets/Scripts/Runtime/[ModuleName]/`
3. Implement `ISaveStateProvider` (add save fields to `SaveData.cs`, add to `EnsureDefaults()`, add order constant to `SaveConstants.SaveProviderOrder`)
4. Wire to `VerticalSliceBootstrap.cs` as an optional module (null-checked SerializeField)
5. Write unit tests in `Assets/Tests/unit/[module-name]/`
6. Mirror files to `Packages/com.endlessengine.idle/Runtime/[ModuleName]/`
7. Document in `Documentation~/api-reference.md`
