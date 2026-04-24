using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using EndlessEngine.Config;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.SaveAndLoad
{
    /// <summary>
    /// Orchestrates save and load for all gameplay state.
    /// Lifecycle: boot → ConfigRegistry.OnConfigsLoaded → SaveService.LoadAsync()
    ///   → OnSaveLoaded fires → all gameplay systems initialize.
    ///
    /// Registration pattern: gameplay systems call <c>RegisterStateProvider(this)</c>
    /// in their <c>Start()</c> before the first save can occur.
    ///
    /// ADR: ADR-0002 — Save Serialization Format and Atomic Write Pattern
    /// ADR: ADR-0004 — ISaveStateProvider Pull-Based Save Collection
    /// </summary>
    public class SaveService : MonoBehaviour, EndlessEngine.Economy.ISaveNotifier
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires after save data is loaded (or new-game defaults are created).</summary>
        public event Action<SaveData, bool> OnSaveLoaded;

        /// <summary>Fires when a save operation begins.</summary>
        public event Action OnSaveStarted;

        /// <summary>Fires when a save operation completes. Parameter: success.</summary>
        public event Action<bool> OnSaveCompleted;

        /// <summary>
        /// Fires once per session when 3 consecutive save write failures are detected.
        /// Game is in a degraded state — player should be warned that progress may not persist.
        /// </summary>
        public event Action OnPersistentWriteFailure;

        // ── Public State ──────────────────────────────────────────────────────────

        /// <summary>True if the backup save was used because the primary was corrupted. HUD reads this flag.</summary>
        public bool PendingBackupNotice { get; private set; }

        // ── Constants ─────────────────────────────────────────────────────────────

        private const float AutoSaveIntervalSeconds = 60f;
        private const float DebounceSeconds         = 5f;
        private const int   MaxConsecutiveFailures  = 3;

        // ── State ─────────────────────────────────────────────────────────────────

        private enum SaveServiceState { Uninitialized, Loading, Ready, Saving }
        private SaveServiceState _state = SaveServiceState.Uninitialized;

        private SaveData _currentSave;
        private readonly List<ISaveStateProvider> _providers = new List<ISaveStateProvider>();

        // Auto-save timer
        private float _autoSaveTimer = AutoSaveIntervalSeconds;

        // Debounce for rapid purchase/event triggers
        private bool  _debouncePending;
        private float _debounceTimer;

        // Consecutive failure tracking
        private int  _consecutiveFailures;
        private bool _failureEventRaised;

        // ── Registration ──────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a system as a save state provider. Call in Start(), before the
        /// first save. Providers are sorted by <see cref="ISaveStateProvider.ProviderOrder"/>.
        /// </summary>
        public void RegisterStateProvider(ISaveStateProvider provider)
        {
            _providers.Add(provider);
            _providers.Sort((a, b) => a.ProviderOrder.CompareTo(b.ProviderOrder));
        }

        /// <summary>Returns the current in-memory SaveData (null if not yet loaded).</summary>
        public SaveData GetCurrentSaveData() => _currentSave;

        /// <summary>
        /// Replaces the current save with imported data and notifies all providers via OnAfterLoad.
        /// Use after a successful <see cref="EndlessEngine.Export.ExportService.TryImportFromCode"/>.
        /// </summary>
        public void ApplyImportedSaveData(SaveData imported)
        {
            if (imported == null) return;
            imported.EnsureDefaults();
            _currentSave = imported;
            foreach (var p in _providers)
                p.OnAfterLoad(_currentSave);
            OnSaveLoaded?.Invoke(_currentSave, false);
        }

        // ── Load ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads existing save data or creates a new game. Called by boot sequence
        /// after <see cref="ConfigRegistry.OnConfigsLoaded"/> fires.
        /// Raises <see cref="OnSaveLoaded"/> on completion.
        /// </summary>
        public async Task LoadAsync()
        {
            if (_state != SaveServiceState.Uninitialized)
            {
                Debug.LogWarning("[SaveService] LoadAsync called when not in Uninitialized state — ignoring.");
                return;
            }

            _state = SaveServiceState.Loading;

            string primaryPath = Path.Combine(Application.persistentDataPath, SaveConstants.PrimaryFile);
            string bakPath     = Path.Combine(Application.persistentDataPath, SaveConstants.BackupFile);
            bool isNewGame = false;
            SaveData saveData;

            if (!File.Exists(primaryPath))
            {
                // ── New game ──────────────────────────────────────────────────────
                saveData  = SaveDataFactory.CreateNewGame();
                isNewGame = true;
                Debug.Log("[SaveService] No save file found — starting new game.");
            }
            else
            {
                // ── Try primary save ──────────────────────────────────────────────
                saveData = await LoadFromFileAsync(primaryPath);

                if (saveData == null)
                {
                    // Primary corrupted — try backup
                    Debug.LogWarning("[SaveService] Primary save corrupted — attempting backup.");
                    saveData = await LoadFromFileAsync(bakPath);

                    if (saveData != null)
                    {
                        // Backup loaded successfully
                        PendingBackupNotice = true;
                        WriteDiagnosticsLog("Primary save corrupted — loaded from backup save.");
                        Debug.LogWarning("[SaveService] Loaded from backup save.");
                    }
                    else
                    {
                        // Both files failed — new game
                        saveData  = SaveDataFactory.CreateNewGame();
                        isNewGame = true;
                        WriteDiagnosticsLog("Both primary and backup saves failed — starting new game. Save data lost.");
                        Debug.LogError("[SaveService] Both primary and backup saves failed — starting new game.");
                    }
                }
            }

            ApplyLoadGuards(saveData);

            _currentSave = saveData;
            _state       = SaveServiceState.Ready;

            // Notify all registered providers
            foreach (var provider in _providers)
                provider.OnAfterLoad(saveData);

            OnSaveLoaded?.Invoke(saveData, isNewGame);
        }

        // ── Save ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Persists all registered provider state atomically.
        /// Serializes on main thread; file I/O on background thread.
        /// </summary>
        public async Task SaveAsync()
        {
            if (_state != SaveServiceState.Ready)
            {
                Debug.LogWarning("[SaveService] SaveAsync called in non-Ready state — ignoring.");
                return;
            }

            _state = SaveServiceState.Saving;
            OnSaveStarted?.Invoke();

            // Collect state from all providers (main thread)
            foreach (var provider in _providers)
                provider.OnBeforeSave(_currentSave);

            _currentSave.LastSessionTimestamp = DateTime.UtcNow;
            SchemaVersionSO schema;
            try { schema = ConfigRegistry.Schema; } catch { schema = null; }
            _currentSave.SchemaVersion = schema != null ? schema.CurrentSchemaVersion : 0;

            // Serialize on main thread (object graph is main-thread-owned)
#if DEVELOPMENT_BUILD
            string json = JsonConvert.SerializeObject(_currentSave, Formatting.Indented);
#else
            string json = JsonConvert.SerializeObject(_currentSave, Formatting.None);
#endif

            string dir     = Application.persistentDataPath;
            string tmpPath = Path.Combine(dir, SaveConstants.TempFile);
            string bakPath = Path.Combine(dir, SaveConstants.BackupFile);
            string jsonPath = Path.Combine(dir, SaveConstants.PrimaryFile);

            bool success = await Task.Run(() => AtomicWrite(json, tmpPath, bakPath, jsonPath));

            _state = SaveServiceState.Ready;

            if (success)
            {
                _consecutiveFailures = 0;
                _autoSaveTimer       = AutoSaveIntervalSeconds; // reset auto-save after successful write
            }
            else
            {
                _consecutiveFailures++;
                Debug.LogError("[SaveService] Atomic write failed — previous save preserved.");

                if (_consecutiveFailures >= MaxConsecutiveFailures && !_failureEventRaised)
                {
                    _failureEventRaised = true;
                    OnPersistentWriteFailure?.Invoke();
                }
            }

            OnSaveCompleted?.Invoke(success);
        }

        /// <summary>
        /// Notifies SaveService that an upgrade was purchased, triggering a debounced save.
        /// Multiple calls within <see cref="DebounceSeconds"/> collapse into a single write.
        /// </summary>
        public void NotifyUpgradePurchased()
        {
            if (_state != SaveServiceState.Ready)
                return;

            // Reset or start the debounce countdown.
            _debouncePending = true;
            _debounceTimer   = DebounceSeconds;
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Update()
        {
            if (_state != SaveServiceState.Ready)
                return;

            float dt = Time.unscaledDeltaTime;

            // Auto-save timer
            _autoSaveTimer -= dt;
            if (_autoSaveTimer <= 0f)
            {
                _autoSaveTimer = AutoSaveIntervalSeconds;
                _ = SaveAsync();
                return; // debounce reset handled inside SaveAsync completion
            }

            // Debounce countdown
            if (_debouncePending)
            {
                _debounceTimer -= dt;
                if (_debounceTimer <= 0f)
                {
                    _debouncePending = false;
                    _ = SaveAsync();
                }
            }
        }

        private void OnApplicationQuit()
        {
#if UNITY_EDITOR
            // Skip blocking save in Editor — synchronous GetAwaiter().GetResult() deadlocks
            // the Editor's main thread when stopping Play Mode. Production builds save normally.
            return;
#endif
            // Synchronous save on quit so progress is never lost.
            // Blocking is intentional and acceptable on the quit path.
            if (_state == SaveServiceState.Ready && _currentSave != null)
                SaveAsync().GetAwaiter().GetResult();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // Save when app moves to background (Alt-Tab, suspend on desktop).
            if (pauseStatus && _state == SaveServiceState.Ready && _currentSave != null)
                _ = SaveAsync();
        }

        // ── Test Injection ────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Directly injects a <see cref="SaveData"/> for testing, bypassing file I/O.
        /// Applies the same load guards (clock-skew clamp, prestige rollback) as LoadAsync().
        /// Sets state to Ready and fires <see cref="OnSaveLoaded"/>.
        /// </summary>
        public void InjectForTesting(SaveData data, bool isNewGame = false)
        {
            ApplyLoadGuards(data);

            _currentSave = data;
            _state       = SaveServiceState.Ready;

            foreach (var provider in _providers)
                provider.OnAfterLoad(data);

            OnSaveLoaded?.Invoke(data, isNewGame);
        }

        /// <summary>Resets SaveService to Uninitialized state for test TearDown.</summary>
        public void ResetForTesting()
        {
            _currentSave        = null;
            _state              = SaveServiceState.Uninitialized;
            PendingBackupNotice = false;
            _providers.Clear();
        }

        /// <summary>
        /// Simulates the post-save outcome (success or failure) to test failure-tracking
        /// logic without real file I/O. Applies the same counter/event logic as SaveAsync().
        /// </summary>
        public void SimulateSaveResultForTesting(bool success)
        {
            if (success)
            {
                _consecutiveFailures = 0;
                _autoSaveTimer       = AutoSaveIntervalSeconds;
            }
            else
            {
                _consecutiveFailures++;
                if (_consecutiveFailures >= MaxConsecutiveFailures && !_failureEventRaised)
                {
                    _failureEventRaised = true;
                    OnPersistentWriteFailure?.Invoke();
                }
            }

            OnSaveCompleted?.Invoke(success);
        }

        /// <summary>Manually expires the auto-save timer for testing.</summary>
        public void ExpireAutoSaveTimerForTesting() => _autoSaveTimer = -1f;

        /// <summary>Manually expires the debounce timer for testing.</summary>
        public void ExpireDebounceTimerForTesting() => _debounceTimer = -1f;

        /// <summary>
        /// Simulates one Update tick without using SendMessage (which triggers
        /// ShouldRunBehaviour assertions in EditMode). When a save would fire,
        /// it calls SimulateSaveResultForTesting(true) synchronously so that
        /// OnSaveStarted and OnSaveCompleted are observable in the same frame.
        /// </summary>
        public void TickUpdateForTesting()
        {
            if (_state != SaveServiceState.Ready) return;

            if (_autoSaveTimer <= 0f)
            {
                _autoSaveTimer = AutoSaveIntervalSeconds;
                OnSaveStarted?.Invoke();
                SimulateSaveResultForTesting(true);
                return;
            }

            if (_debouncePending && _debounceTimer <= 0f)
            {
                _debouncePending = false;
                OnSaveStarted?.Invoke();
                SimulateSaveResultForTesting(true);
            }
        }
#endif

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies both load guards in-place to <paramref name="saveData"/> before
        /// providers are notified and <see cref="OnSaveLoaded"/> fires.
        /// Guards: (1) clock-skew clamp, (2) prestige crash-safety rollback.
        /// ADR-0002 Rule 17, ADR-0010.
        /// </summary>
        private static void ApplyLoadGuards(SaveData saveData)
        {
            // Guard 0: Null-safety — initialize any collection that was absent in the saved JSON
            saveData.EnsureDefaults();

            // Guard 1: Clock-skew — clamp future LastSessionTimestamp to UtcNow
            var skewThreshold = DateTime.UtcNow.AddMinutes(5);
            if (saveData.LastSessionTimestamp > skewThreshold)
            {
                Debug.LogWarning($"[SaveService] Clock skew detected: timestamp {saveData.LastSessionTimestamp:O} is in the future. Clamping to UtcNow. Offline delta set to 0.");
                saveData.LastSessionTimestamp = DateTime.UtcNow;
            }

            // Guard 2: Prestige crash-safety rollback — restore snapshot if prestige was interrupted
            if (saveData.PrestigeInProgress)
            {
                Debug.LogWarning("[SaveService] PrestigeInProgress=true detected — applying rollback from pre-prestige snapshot.");
                saveData.CurrentResources  = saveData.PrePrestigeResources;
                saveData.UpgradeNodeStates = saveData.PrePrestigeUpgradeNodeStates
                                             ?? new Dictionary<string, int>();
                saveData.WaveNumber        = saveData.PrePrestigeWaveNumber;
                saveData.PrestigeInProgress           = false;
                saveData.PrePrestigeResources          = 0L;
                saveData.PrePrestigeUpgradeNodeStates  = null;
                saveData.PrePrestigeWaveNumber         = 0;
            }
        }

        private static async Task<SaveData> LoadFromFileAsync(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                string json = await Task.Run(() => File.ReadAllText(path));
                return JsonConvert.DeserializeObject<SaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveService] Failed to deserialize save at '{path}': {ex.Message}");
                return null;
            }
        }

        private static void WriteDiagnosticsLog(string message)
        {
            try
            {
                string logPath = Path.Combine(Application.persistentDataPath, SaveConstants.DiagnosticsFile);
                string entry   = $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}";
                File.AppendAllText(logPath, entry);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveService] Could not write diagnostics log: {ex.Message}");
            }
        }

        /// <summary>
        /// Atomic write: write → .tmp, copy → .bak, rename .tmp → .json, delete .bak.
        /// Runs on background thread. Returns true on success.
        /// </summary>
        private static bool AtomicWrite(string json, string tmpPath, string bakPath, string jsonPath)
        {
            try
            {
                // Step 1: write to .tmp
                File.WriteAllText(tmpPath, json);

                // Step 2: copy current .json → .bak (if .json exists)
                if (File.Exists(jsonPath))
                    File.Copy(jsonPath, bakPath, overwrite: true);

                // Step 3: rename .tmp → .json (atomic on NTFS same volume)
                if (File.Exists(jsonPath)) File.Delete(jsonPath);
                File.Move(tmpPath, jsonPath);

                // Step 4: delete .bak
                if (File.Exists(bakPath))
                    File.Delete(bakPath);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveService] Atomic write exception: {ex.Message}");
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { /* ignore cleanup failure */ }
                return false;
            }
        }
    }
}
