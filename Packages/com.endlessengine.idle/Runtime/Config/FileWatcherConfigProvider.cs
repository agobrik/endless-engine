#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Config
{
    /// <summary>
    /// IConfigProvider that watches JSON override files on disk and hot-reloads
    /// ScriptableObject fields at runtime in the Editor.
    ///
    /// Usage pattern (Editor-only):
    ///   1. Export a SO's current values to a JSON override file:
    ///        FileWatcherConfigProvider.ExportOverride(myEconomyConfig, "Overrides/economy.json");
    ///   2. Edit the JSON file externally (spreadsheet export, balance tool output, etc.)
    ///   3. FileWatcherConfigProvider detects the change and applies it to the SO.
    ///   4. OnConfigReloaded fires so live UI updates without an editor restart.
    ///
    /// Security / build note: This class is Editor-only (#if UNITY_EDITOR).
    /// FileSystemWatcher is not available in WebGL/iOS builds and is unnecessary in
    /// shipping builds — use AddressablesConfigProvider in production.
    ///
    /// JSON format: flat key-value matching SO public field names.
    ///   { "IdleYieldRateBase": 25.0, "ResourceHardCap": 5000000000 }
    /// Unknown keys are silently ignored. Only public value-type fields are patched.
    ///
    /// Bootstrap (Editor Playmode only):
    ///   var provider = new FileWatcherConfigProvider("Assets/Overrides");
    ///   provider.Register(myEconomyConfig, "economy.json");
    ///   provider.Load();
    /// </summary>
    public class FileWatcherConfigProvider : DirectConfigProvider, IDisposable
    {
        private readonly string                             _watchDirectory;
        private readonly Dictionary<string, ScriptableObject> _fileToSO = new Dictionary<string, ScriptableObject>();
        private readonly List<FileSystemWatcher>            _watchers  = new List<FileSystemWatcher>();
        private readonly Queue<(string path, ScriptableObject so)> _pendingReloads
            = new Queue<(string, ScriptableObject)>();

        private bool _disposed;

        public FileWatcherConfigProvider(string watchDirectory)
        {
            _watchDirectory = watchDirectory;
        }

        /// <summary>
        /// Registers a SO paired with a JSON override filename (relative to watchDirectory).
        /// If the file exists at Load() time its values are applied immediately.
        /// </summary>
        public void Register<T>(T config, string jsonFilename) where T : ScriptableObject
        {
            base.Register(config);
            _fileToSO[jsonFilename] = config;
        }

        // ── IConfigProvider.Load ──────────────────────────────────────────────────

        public new void Load()
        {
            // Apply existing override files before marking loaded
            foreach (var (filename, so) in _fileToSO)
            {
                string fullPath = Path.Combine(_watchDirectory, filename);
                if (File.Exists(fullPath))
                    ApplyOverride(fullPath, so);
            }

            // Start watching
            StartWatchers();

            base.Load();
        }

        // ── Hot-reload tick (call from EditorApplication.update or MonoBehaviour.Update) ──

        /// <summary>
        /// Processes pending file change events on the main thread.
        /// Call this from EditorApplication.update or a MonoBehaviour.Update.
        /// FileSystemWatcher callbacks fire on a background thread — this drains the queue safely.
        /// </summary>
        public void ProcessPendingReloads()
        {
            while (_pendingReloads.Count > 0)
            {
                var (path, so) = _pendingReloads.Dequeue();
                ApplyOverride(path, so);
                FireConfigReloaded(so);
                Debug.Log($"[FileWatcherConfigProvider] Hot-reloaded: {Path.GetFileName(path)} → {so.name}");
            }
        }

        // ── IDisposable ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var w in _watchers)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            _watchers.Clear();
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void StartWatchers()
        {
            if (!Directory.Exists(_watchDirectory))
            {
                Debug.LogWarning($"[FileWatcherConfigProvider] Watch directory not found: '{_watchDirectory}'. Creating.");
                Directory.CreateDirectory(_watchDirectory);
            }

            foreach (var (filename, so) in _fileToSO)
            {
                var watcher = new FileSystemWatcher(_watchDirectory, filename)
                {
                    NotifyFilter            = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents     = true,
                };

                // Capture loop variables for closure
                string capturedFilename = filename;
                ScriptableObject capturedSO = so;

                watcher.Changed += (_, e) =>
                {
                    // FileSystemWatcher fires on background thread — enqueue for main-thread processing
                    lock (_pendingReloads)
                        _pendingReloads.Enqueue((e.FullPath, capturedSO));
                };

                _watchers.Add(watcher);
            }
        }

        private static void ApplyOverride(string jsonPath, ScriptableObject so)
        {
            try
            {
                string json = File.ReadAllText(jsonPath);
                var overrides = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (overrides == null) return;

                var soType = so.GetType();
                foreach (var kv in overrides)
                {
                    var field = soType.GetField(kv.Key,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (field == null) continue;

                    try
                    {
                        object converted = System.Convert.ChangeType(
                            kv.Value, field.FieldType,
                            System.Globalization.CultureInfo.InvariantCulture);
                        field.SetValue(so, converted);
                    }
                    catch
                    {
                        // Type conversion failed — skip this field silently
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FileWatcherConfigProvider] Failed to apply override '{jsonPath}': {ex.Message}");
            }
        }

        // ── Export helper ─────────────────────────────────────────────────────────

        /// <summary>
        /// Exports a ScriptableObject's public fields to a JSON override file.
        /// Call once to create the initial override template, then edit the file.
        /// </summary>
        public static void ExportOverride(ScriptableObject so, string outputPath)
        {
            var fields = so.GetType().GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            var dict = new Dictionary<string, object>();
            foreach (var f in fields)
            {
                var val = f.GetValue(so);
                if (val == null || val is UnityEngine.Object) continue; // skip Unity refs
                dict[f.Name] = val;
            }

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(outputPath, JsonConvert.SerializeObject(dict, Formatting.Indented));
            Debug.Log($"[FileWatcherConfigProvider] Exported override template: {outputPath}");
        }
    }
}
#endif
