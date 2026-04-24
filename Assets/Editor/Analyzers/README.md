# Endless Engine Roslyn Analyzers

This directory is the deployment target for the Roslyn analyzer DLL.

Place `EndlessEngine.Analyzers.dll` here and Unity 6 will automatically load it
as a compile-time analyzer in the Editor.

## Build and Install

**Prerequisites**: .NET SDK 6+ installed locally.

```bash
# From the repository root:
dotnet build tools/analyzers/EndlessEngine.Analyzers/EndlessEngine.Analyzers/EndlessEngine.Analyzers.csproj \
    -c Release \
    -o Assets/Editor/Analyzers/
```

After the build, `Assets/Editor/Analyzers/EndlessEngine.Analyzers.dll` will be present.
Unity will detect the new file and recompile — check the Console for ENDLESSENG001/002 diagnostics
if any violations exist in the codebase.

## Verify Activation

1. Open any runtime `.cs` file
2. Add a temporary line: `someScriptableObject.AnyField = 0;` (replace with a real SO field)
3. Save — Unity should show a compile error: `ENDLESSENG001: ScriptableObject field assigned in non-Editor assembly`
4. Remove the test line

If no error appears, check:
- The DLL is present in this directory (not just the README)
- Unity reimported the DLL (right-click → Reimport)
- The DLL targets `netstandard2.0` (confirmed in the .csproj)

## Analyzers Defined

| Diagnostic ID | Rule | Severity |
|---|---|---|
| ENDLESSENG001 | SO field assignment in non-Editor assembly | Error |
| ENDLESSENG002 | [NonOverridable] member redeclared in SO subclass | Error |

## Source

`tools/analyzers/EndlessEngine.Analyzers/`

The DLL is gitignored (binary artifact). Build it from source before working
on the project in a fresh clone.
