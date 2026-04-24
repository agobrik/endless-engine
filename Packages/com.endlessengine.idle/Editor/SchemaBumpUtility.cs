using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Tools → Endless Engine → Schema Bump
    ///
    /// Increments SchemaVersionSO.CurrentSchemaVersion by 1 and scaffolds
    /// the corresponding IMigration implementation file so the developer only
    /// needs to fill in the field mutations.
    ///
    /// Workflow:
    ///   1. Add or rename a field in SaveData.cs.
    ///   2. Run Schema Bump (or call SchemaBumpUtility.Bump() in a test).
    ///   3. Fill in the generated Migration_N_to_N1.cs Migrate() method.
    ///   4. Wire the migration into SaveService / MigrationPipeline.
    ///
    /// The generated file is placed next to the existing migration files at
    ///   Assets/Scripts/Runtime/SaveAndLoad/Migrations/
    /// or, if that path does not exist, at
    ///   Assets/Migrations/
    /// </summary>
    public static class SchemaBumpUtility
    {
        private const string MigrationsFolder = "Assets/Scripts/Runtime/SaveAndLoad/Migrations";
        private const string FallbackFolder   = "Assets/Migrations";

        // ── Menu item ─────────────────────────────────────────────────────────────

        [MenuItem("Tools/Endless Engine/Schema Bump", priority = 30)]
        public static void BumpFromMenu()
        {
            // Find the first SchemaVersionSO in the project
            string[] guids = AssetDatabase.FindAssets("t:SchemaVersionSO");
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Schema Bump",
                    "No SchemaVersionSO found in the project.\n\n" +
                    "Create one via: Create → Endless Engine → Config → Schema Version",
                    "OK");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var so = AssetDatabase.LoadAssetAtPath<SchemaVersionSO>(path);
            if (so == null)
            {
                Debug.LogError("[SchemaBump] Could not load SchemaVersionSO.");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "Schema Bump",
                $"Current schema version: {so.CurrentSchemaVersion}\n\n" +
                $"This will:\n" +
                $"  • Increment CurrentSchemaVersion to {so.CurrentSchemaVersion + 1}\n" +
                $"  • Scaffold Migration_{so.CurrentSchemaVersion}_to_{so.CurrentSchemaVersion + 1}.cs\n\n" +
                "Continue?",
                "Bump", "Cancel");

            if (!confirm) return;

            Bump(so);
        }

        /// <summary>
        /// Increments the schema version on <paramref name="so"/> and writes a migration scaffold.
        /// Callable from tests or other editor scripts.
        /// </summary>
        public static void Bump(SchemaVersionSO so)
        {
            int fromVersion = so.CurrentSchemaVersion;
            int toVersion   = fromVersion + 1;

            // 1. Increment version
            so.CurrentSchemaVersion = toVersion;
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();

            // 2. Scaffold migration file
            string folder = ResolveMigrationsFolder();
            EnsureDirectory(folder);

            string fileName  = $"Migration_{fromVersion}_to_{toVersion}.cs";
            string filePath  = Path.Combine(Application.dataPath, "..", folder, fileName);

            if (File.Exists(filePath))
            {
                Debug.LogWarning($"[SchemaBump] Migration file already exists: {folder}/{fileName}");
            }
            else
            {
                string src = BuildMigrationSource(fromVersion, toVersion);
                File.WriteAllText(filePath, src, Encoding.UTF8);
                Debug.Log($"[SchemaBump] Created {folder}/{fileName}");
            }

            AssetDatabase.Refresh();

            // 3. Select the SO so the developer sees the change immediately
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = so;

            Debug.Log($"[SchemaBump] Schema bumped {fromVersion} → {toVersion}. " +
                      $"Fill in {fileName}'s Migrate() method, then wire it into MigrationPipeline.");
        }

        // ── Migration scaffold ────────────────────────────────────────────────────

        private static string BuildMigrationSource(int from, int to)
        {
            return
$@"using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.SaveAndLoad.Migrations
{{
    /// <summary>
    /// Migrates SaveData from schema version {from} to {to}.
    ///
    /// Instructions:
    ///   1. Add field mutations below for every field added, removed, or renamed
    ///      in SaveData between versions {from} and {to}.
    ///   2. Always set explicit defaults — never rely on Newtonsoft null-on-missing.
    ///   3. Register this instance in SaveService / MigrationPipeline before shipping.
    ///
    /// ADR-0002: Save Serialization Format and Atomic Write Pattern
    /// </summary>
    public sealed class Migration_{from}_to_{to} : IMigration
    {{
        public int FromVersion => {from};
        public int ToVersion   => {to};

        public void Migrate(SaveData data)
        {{
            // TODO: mutate data fields here.
            // Example:
            //   data.NewField = data.OldField ?? defaultValue;
            //   data.OldField = null;   // if removed
        }}
    }}
}}
";
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string ResolveMigrationsFolder()
        {
            string full = Path.Combine(Application.dataPath, "..",
                MigrationsFolder.Replace('/', Path.DirectorySeparatorChar));
            return Directory.Exists(full) ? MigrationsFolder : FallbackFolder;
        }

        private static void EnsureDirectory(string assetRelativePath)
        {
            string full = Path.Combine(Application.dataPath, "..",
                assetRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full))
                Directory.CreateDirectory(full);
        }
    }
}
