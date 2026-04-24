# UPM Migration Status

## Sprint 15 — First Batch (S15-03)

### Migrated to package (copies — Assets/ still authoritative)

| Script | Package Path | Assets/ Original |
|--------|-------------|-----------------|
| ConfigRegistry | Runtime/Config/ConfigRegistry.cs | Assets/Scripts/Runtime/Config/ConfigRegistry.cs |
| SaveService | Runtime/SaveAndLoad/SaveService.cs | Assets/Scripts/Runtime/SaveAndLoad/SaveService.cs |
| EconomyService | Runtime/Economy/EconomyService.cs | Assets/Scripts/Runtime/Economy/EconomyService.cs |
| TickEngine | Runtime/Flow/TickEngine.cs | Assets/Scripts/Runtime/Flow/TickEngine.cs |

### Status: PENDING FULL MIGRATION (Sprint 16)

Sprint 16 will:
1. Remove Assets/ copies of migrated scripts
2. Update scene/inspector references to point to package versions
3. Migrate remaining 88 scripts in batches
4. Replace Assets asmdef with package asmdef reference

### Note

Both `Assets/Scripts/EndlessEngine.Runtime.asmdef` and
`Packages/com.endlessengine.idle/Runtime/EndlessEngine.Runtime.asmdef` exist.
During migration, Unity will flag duplicate type names. Sprint 16 resolves this
by removing the Assets/ asmdef after all scripts are migrated.

Do NOT delete Assets/ versions until all scene `.unity` files are re-serialized
with the package GUID. Use Unity's "Find References In Scene" before removing each file.
