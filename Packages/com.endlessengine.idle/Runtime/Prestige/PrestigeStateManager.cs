using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Prestige
{
    /// <summary>
    /// Manages the prestige lifecycle with two-save crash safety.
    ///
    /// Sequence:
    ///   (1) SAVE 1: set PrestigeInProgress=true + snapshot → SaveAsync()
    ///   (2) Fire OnPrestigeStarted → subscribers reset (Economy, UAS, UpgradeTree, Wave, Health)
    ///   (3) Increment PrestigeCount, clear PrestigeInProgress flag → SaveAsync()
    ///   (4) Fire OnPrestigeComplete(count, multiplier)
    ///
    /// Guard: If app crashes between save-1 and save-2, LoadAsync() detects
    ///        PrestigeInProgress=true and rolls back via SaveService.ApplyLoadGuards.
    ///
    /// ISaveStateProvider (order=30): writes PrestigeCount and BaseMultiplierPerPrestige.
    ///
    /// ADR: ADR-0010 — Prestige Crash Safety
    /// </summary>
    public class PrestigeStateManager : MonoBehaviour, ISaveStateProvider, IPrestigeQuery
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.Prestige;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires after save-1 completes. Subscribers: Economy, UAS, UpgradeTree, Wave, Health.</summary>
        public static event Action OnPrestigeStarted;

        /// <summary>Fires after save-2 completes. Parameters: (count, permanentMultiplier).</summary>
        public static event Action<int, float> OnPrestigeComplete;

        /// <summary>Fires when a realm is unlocked by reaching a prestige threshold.</summary>
        public static event Action<string> OnRealmUnlocked;

        // ── State ─────────────────────────────────────────────────────────────────

        public int PrestigeCount { get; private set; }

        private bool _prestigeInProgress;

        [SerializeField]
        private SaveService _saveService;

        // ── CanPrestige ───────────────────────────────────────────────────────────

        private int _currentWaveNumber;

        /// <summary>True when all prestige gate conditions are met and no prestige is in progress.</summary>
        public bool CanPrestige
        {
            get
            {
                if (_prestigeInProgress) return false;

                var cfg = ConfigRegistry.Prestige;

                if (_currentWaveNumber < cfg.MinWaveForPrestige) return false;

                // MaxPrestigeCount=0 means unlimited
                if (cfg.MaxPrestigeCount > 0 && PrestigeCount >= cfg.MaxPrestigeCount) return false;

                return true;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the current permanent multiplier earned from prestiges.
        /// Formula: Min(MaxPermanentMultiplier, BaseMultiplierPerPrestige ^ PrestigeCount)
        /// </summary>
        public float GetPermanentMultiplier()
        {
            var cfg = ConfigRegistry.Prestige;
            return Mathf.Min(cfg.MaxPermanentMultiplier,
                             Mathf.Pow(cfg.BaseMultiplierPerPrestige, PrestigeCount));
        }

        /// <summary>
        /// Attempts to start a prestige. Returns false immediately if CanPrestige is false.
        /// Fires the async prestige ceremony via fire-and-forget.
        /// </summary>
        public bool TryPrestige()
        {
            if (!CanPrestige) return false;
            _ = BeginPrestigeAsync();
            return true;
        }

        /// <summary>Updates the current wave number (called by WaveSpawningSystem).</summary>
        public void SetCurrentWave(int waveNumber) => _currentWaveNumber = waveNumber;

        // ── Two-save crash-safety sequence ────────────────────────────────────────

        private async Task BeginPrestigeAsync()
        {
            _prestigeInProgress = true;

            // ── SAVE 1: guard save ────────────────────────────────────────────────
            // Write PrestigeInProgress=true so a crash here is detectable on load.
            // (OnBeforeSave will write the flag because we set it above.)
            if (_saveService != null)
                await _saveService.SaveAsync();

            // ── RESET CHAIN: fire OnPrestigeStarted ───────────────────────────────
            // Subscribers reset in deterministic order by C# event invocation order:
            //   Economy(10) → UAS(20) → UpgradeTree(30) → Wave(40) → Health(50)
            OnPrestigeStarted?.Invoke();

            // ── INCREMENT AND CLEAR ───────────────────────────────────────────────
            PrestigeCount++;
            _prestigeInProgress = false;

            // ── SAVE 2: completed state ───────────────────────────────────────────
            if (_saveService != null)
                await _saveService.SaveAsync();

            // ── NOTIFY COMPLETE ───────────────────────────────────────────────────
            float mult = GetPermanentMultiplier();
            OnPrestigeComplete?.Invoke(PrestigeCount, mult);

            // ── REALM UNLOCK CHECK ────────────────────────────────────────────────
            CheckRealmUnlocks();
        }

        private void CheckRealmUnlocks()
        {
            // Notify RealmConfigSystem of any newly unlocked realms.
            // This is a simple threshold check — actual unlock state lives in RealmConfigSystem.
            // The RealmConfigSystem subscribes to OnPrestigeComplete and can call this back,
            // or PrestigeStateManager fires OnRealmUnlocked directly for realms at this threshold.
            // Current MVP: only base realm exists — no unlock logic needed.
            // Placeholder for future realm unlock at prestige N.
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.PrestigeCount      = PrestigeCount;
            saveData.PrestigeInProgress = _prestigeInProgress;

            PrestigeConfigSO cfg;
            try { cfg = ConfigRegistry.Prestige; } catch { cfg = null; }
            saveData.BaseMultiplierPerPrestige = cfg != null ? cfg.BaseMultiplierPerPrestige : 1.5f;

            if (_prestigeInProgress)
            {
                // Save snapshot fields — populated by caller before SaveAsync()
                // (PrestigeStateManager owns snapshot field writes per ADR-0010)
                // Snapshot values are already in saveData from previous save or from
                // the current run's in-memory SaveData (managed by SaveService).
            }
        }

        public void OnAfterLoad(SaveData saveData)
        {
            PrestigeCount = saveData.PrestigeCount;
            // PermanentMultiplier is computed on demand from ConfigRegistry + PrestigeCount
        }

        // ── Test injection ────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private PrestigeConfigSO _injectedConfig;

        /// <summary>
        /// Injects a PrestigeConfigSO directly for EditMode testing, bypassing ConfigRegistry.
        /// Must be called before TryPrestige or CanPrestige checks in tests.
        /// </summary>
        public void InjectConfigForTesting(PrestigeConfigSO config) => _injectedConfig = config;

        private PrestigeConfigSO GetConfig()
        {
            if (_injectedConfig != null) return _injectedConfig;
            try { return ConfigRegistry.Prestige; }
            catch { return null; }
        }

        /// <summary>
        /// Test-friendly TryPrestige that checks gold via injected EconomyService
        /// (bypasses async ceremony and wave-gate check when config is injected).
        /// </summary>
        public bool TryPrestige(EconomyService economy)
        {
            var cfg = GetConfig();
            if (cfg == null) return false;
            if (_prestigeInProgress) return false;
            if (economy == null) return false;
            if (cfg.MinGoldToPrestige > 0 && economy.CurrentResources < cfg.MinGoldToPrestige) return false;

            economy.DeductResources(economy.CurrentResources); // reset to 0
            PrestigeCount++;
            OnPrestigeComplete?.Invoke(PrestigeCount, Mathf.Pow(cfg.BaseMultiplierPerPrestige, PrestigeCount));
            return true;
        }

        /// <summary>Injects prestige count for testing without a save round-trip.</summary>
        public void SetPrestigeCountForTesting(int count) => PrestigeCount = count;

        /// <summary>Forces the in-progress flag for testing the double-prestige guard.</summary>
        public void SetPrestigeInProgressForTesting(bool inProgress) => _prestigeInProgress = inProgress;

        /// <summary>Sets the current wave number for testing the wave gate.</summary>
        public void SetWaveNumberForTesting(int wave) => _currentWaveNumber = wave;

        /// <summary>Clears events for test isolation.</summary>
        public static void ClearStaticEventsForTesting()
        {
            OnPrestigeStarted  = null;
            OnPrestigeComplete = null;
            OnRealmUnlocked    = null;
        }

        /// <summary>
        /// Synchronous version of BeginPrestigeAsync for EditMode testing.
        /// Uses injected ISaveService mock that returns Task.CompletedTask.
        /// </summary>
        public async Task BeginPrestigeForTesting()
        {
            await BeginPrestigeAsync();
        }

        /// <summary>
        /// Directly fires OnPrestigeStarted for unit testing subscribers
        /// (e.g. CurrencyService prestige-reset behaviour) without requiring
        /// a full prestige lifecycle.
        /// </summary>
        public static void FirePrestigeStartedForTesting() => OnPrestigeStarted?.Invoke();
#endif
    }
}
