using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Generator;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Prestige
{
    /// <summary>
    /// Manages the multi-layer prestige / ascension system.
    /// Works alongside PrestigeStateManager — layer 0 continues to use the
    /// existing PrestigeStateManager path; layer 1+ uses this manager.
    ///
    /// Per-layer counts are stored in SaveData.AscensionCounts.
    /// The cascade multiplier = ∏(layer[i].GetPermanentMultiplier(count[i])) for all layers.
    ///
    /// ISaveStateProvider (order=25): writes AscensionCounts between Economy(10) and UpgradeTree(20+).
    ///
    /// ADR: ADR-0010 — Prestige Crash Safety (same two-save pattern applies per layer)
    /// </summary>
    public class AscensionStateManager : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.Ascension;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when any layer (1+) ascension begins. Parameter: layerIndex.</summary>
        public static event Action<int> OnAscensionStarted;

        /// <summary>Fires after a layer ascension completes. Parameters: layerIndex, newCount, cascadeMultiplier.</summary>
        public static event Action<int, int, float> OnAscensionComplete;

        /// <summary>
        /// Fires when an ascension layer triggers a reset. Systems that reset on
        /// PrestigeStateManager.OnPrestigeStarted should also subscribe here so
        /// ascension resets work identically.
        /// </summary>
        public static event Action OnAscensionResetRequested;

        // ── Dependencies ──────────────────────────────────────────────────────────

        [SerializeField] private AscensionDatabaseSO  _database;
        [SerializeField] private SaveService          _saveService;
        [SerializeField] private PrestigeStateManager _prestigeManager;  // layer 0
        [SerializeField] private EconomyService       _economyService;
        [SerializeField] private GeneratorSystem      _generatorSystem;  // optional, for ResetGenerators
        [SerializeField] private CurrencyService      _currencyService;  // optional, for currency rewards

        // ── Runtime state ─────────────────────────────────────────────────────────

        // layerIndex → count (layers 1+; layer 0 is in PrestigeStateManager.PrestigeCount)
        private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();
        private bool _ascensionInProgress;
        private bool _initialized;

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>Wire dependencies at runtime. Called from Bootstrap or Inspector references.</summary>
        public void Initialize(
            AscensionDatabaseSO  database,
            PrestigeStateManager prestigeManager,
            SaveService          saveService,
            EconomyService       economyService,
            GeneratorSystem      generatorSystem = null,
            CurrencyService      currencyService = null)
        {
            _database        = database;
            _prestigeManager = prestigeManager;
            _saveService     = saveService;
            _economyService  = economyService;
            _generatorSystem = generatorSystem;
            _currencyService = currencyService;
            _initialized     = true;
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.AscensionCounts ??= new Dictionary<string, int>();
            saveData.AscensionCounts.Clear();
            foreach (var kv in _counts)
                saveData.AscensionCounts[kv.Key.ToString()] = kv.Value;
        }

        public void OnAfterLoad(SaveData saveData)
        {
            _counts.Clear();
            if (saveData.AscensionCounts == null) return;
            foreach (var kv in saveData.AscensionCounts)
            {
                if (int.TryParse(kv.Key, out int idx))
                    _counts[idx] = kv.Value;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Returns the number of times the given layer has been triggered (1+).</summary>
        public int GetCount(int layerIndex) =>
            _counts.TryGetValue(layerIndex, out int c) ? c : 0;

        /// <summary>Returns the total number of times layer 0 has been triggered (from PrestigeStateManager).</summary>
        public int GetLayer0Count() =>
            _prestigeManager != null ? _prestigeManager.PrestigeCount : 0;

        /// <summary>
        /// Returns the cascade multiplier: product of permanent multipliers from all layers.
        /// Layer 0 uses PrestigeStateManager.GetPermanentMultiplier().
        /// </summary>
        public float GetCascadeMultiplier()
        {
            float total = _prestigeManager != null ? _prestigeManager.GetPermanentMultiplier() : 1f;

            if (_database == null) return total;

            for (int i = 0; i < _database.LayerCount; i++)
            {
                var cfg = _database.GetLayer(i);
                if (cfg == null || cfg.LayerIndex == 0) continue; // LayerIndex 0 = standard prestige, handled by _prestigeManager
                total *= cfg.GetPermanentMultiplier(GetCount(cfg.LayerIndex));
            }

            return total;
        }

        /// <summary>
        /// Returns true if the given layer can be triggered right now.
        /// Checks MinWaveRequired and RequiredPreviousLayerCount.
        /// Layer 0 delegates to PrestigeStateManager.CanPrestige.
        /// </summary>
        public bool CanTrigger(int layerIndex, int currentWaveNumber)
        {
            if (_ascensionInProgress) return false;

            if (layerIndex == 0)
                return _prestigeManager != null && _prestigeManager.CanPrestige;

            var cfg = _database?.GetLayer(layerIndex);
            if (cfg == null) return false;

            if (currentWaveNumber < cfg.MinWaveRequired) return false;

            if (cfg.MaxCount > 0 && GetCount(layerIndex) >= cfg.MaxCount) return false;

            // Must have enough triggers on the previous layer
            if (cfg.RequiredPreviousLayerCount > 0)
            {
                int prevCount = layerIndex == 1
                    ? GetLayer0Count()
                    : GetCount(layerIndex - 1);

                if (prevCount < cfg.RequiredPreviousLayerCount) return false;
            }

            return true;
        }

        /// <summary>
        /// Triggers the given ascension layer (1+). Returns false if CanTrigger is false.
        /// Layer 0 must be triggered via PrestigeStateManager.TryPrestige().
        /// </summary>
        public bool TryTrigger(int layerIndex, int currentWaveNumber)
        {
            if (layerIndex == 0) return _prestigeManager?.TryPrestige() ?? false;
            if (!CanTrigger(layerIndex, currentWaveNumber)) return false;
            _ = BeginAscensionAsync(layerIndex);
            return true;
        }

        // ── Ascension sequence (two-save crash-safety) ────────────────────────────

        private async Task BeginAscensionAsync(int layerIndex)
        {
            _ascensionInProgress = true;

            // ── SAVE 1: guard save ────────────────────────────────────────────────
            if (_saveService != null)
                await _saveService.SaveAsync();

            // ── FIRE RESET CHAIN ──────────────────────────────────────────────────
            OnAscensionStarted?.Invoke(layerIndex);

            // Apply reset scope
            var cfg = _database?.GetLayer(layerIndex);
            if (cfg != null)
                ApplyResetScope(cfg);

            // ── INCREMENT ─────────────────────────────────────────────────────────
            _counts.TryGetValue(layerIndex, out int current);
            _counts[layerIndex] = current + 1;
            _ascensionInProgress = false;

            // ── SAVE 2: completed state ───────────────────────────────────────────
            if (_saveService != null)
                await _saveService.SaveAsync();

            // ── CURRENCY REWARD ───────────────────────────────────────────────────
            if (cfg != null && !string.IsNullOrEmpty(cfg.RewardCurrencyId) && _currencyService != null)
            {
                double reward = cfg.GetCurrencyReward(current); // before increment
                if (reward > 0)
                    _currencyService.Add(cfg.RewardCurrencyId, reward);
            }

            // ── NOTIFY COMPLETE ───────────────────────────────────────────────────
            float cascade = GetCascadeMultiplier();
            OnAscensionComplete?.Invoke(layerIndex, _counts[layerIndex], cascade);
        }

        private void ApplyResetScope(PrestigeLayerConfigSO cfg)
        {
            // Fire the prestige-started reset chain (Economy, UpgradeTree, etc. subscribe to this)
            // Uses the same static event so all existing reset subscribers react correctly.
            OnAscensionResetRequested?.Invoke();

            switch (cfg.ResetScope)
            {
                case AscensionResetScope.Deep:
                    if (cfg.ResetGenerators)         _generatorSystem?.ResetAllForAscension();
                    if (cfg.ResetSecondaryCurrencies) _currencyService?.ResetForAscension();
                    break;

                case AscensionResetScope.Full:
                    _generatorSystem?.ResetAllForAscension();
                    _currencyService?.ResetForAscension();
                    // Reset all lower layer counts (full wipe)
                    _counts.Clear();
                    break;
            }
        }


        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnAscensionStarted        = null;
            OnAscensionComplete       = null;
            OnAscensionResetRequested = null;
        }

        public void InjectCountForTesting(int layerIndex, int count) => _counts[layerIndex] = count;

        public void SetInitializedForTesting() => _initialized = true;
#endif
    }
}
