using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Generator
{
    /// <summary>
    /// Manages all idle generators: purchase, count tracking, yield calculation.
    /// Does NOT produce income directly — PassiveIncomeService reads CalculateTotalYield()
    /// each tick and pushes the result to EconomyService.
    ///
    /// Saves/loads generator state via ISaveStateProvider.
    /// Config is read from GeneratorConfigSO assets registered via Initialize().
    /// </summary>
    public class GeneratorSystem : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.Generator;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires after any generator count changes. Parameter: generatorId.</summary>
        public static event Action<string> OnGeneratorPurchased;

        /// <summary>Fires when a purchase fails (insufficient gold). Parameters: id, cost, balance.</summary>
        public static event Action<string, long, long> OnPurchaseFailed;

        // ── Dependencies ──────────────────────────────────────────────────────────

        private GeneratorConfigSO[]    _configs;
        private EconomyService         _economy;
        private ISaveNotifier          _saveNotifier;

        // ── Runtime state ─────────────────────────────────────────────────────────

        // generatorId → runtime state
        private readonly Dictionary<string, GeneratorState> _states =
            new Dictionary<string, GeneratorState>();

        // generatorId → config (fast lookup)
        private readonly Dictionary<string, GeneratorConfigSO> _configLookup =
            new Dictionary<string, GeneratorConfigSO>();

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Inject dependencies. Call from Bootstrap before SaveService fires OnSaveLoaded.
        /// </summary>
        public void Initialize(
            GeneratorConfigSO[]  configs,
            EconomyService       economy,
            ISaveNotifier        saveNotifier)
        {
            _configs      = configs;
            _economy      = economy;
            _saveNotifier = saveNotifier;

            _configLookup.Clear();
            foreach (var cfg in _configs)
            {
                if (cfg == null) continue;
                _configLookup[cfg.GeneratorId] = cfg;

                if (!_states.ContainsKey(cfg.GeneratorId))
                    _states[cfg.GeneratorId] = new GeneratorState { GeneratorId = cfg.GeneratorId };
            }
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            if (saveData.GeneratorStates == null)
                saveData.GeneratorStates = new Dictionary<string, GeneratorState>();

            foreach (var kv in _states)
                saveData.GeneratorStates[kv.Key] = kv.Value;
        }

        public void OnAfterLoad(SaveData saveData)
        {
            if (saveData.GeneratorStates == null) return;

            foreach (var kv in saveData.GeneratorStates)
            {
                if (_states.ContainsKey(kv.Key))
                    _states[kv.Key] = kv.Value;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to purchase one copy of the given generator.
        /// Deducts cost from EconomyService on success.
        /// </summary>
        public bool TryPurchase(string generatorId)
        {
            if (!_configLookup.TryGetValue(generatorId, out var cfg))
            {
                Debug.LogWarning($"[GeneratorSystem] TryPurchase: unknown id={generatorId}");
                return false;
            }

            if (!_states.TryGetValue(generatorId, out var state))
                return false;

            if (cfg.MaxCount >= 0 && state.Count >= cfg.MaxCount)
            {
                Debug.Log($"[GeneratorSystem] {generatorId} is at max count ({cfg.MaxCount}).");
                return false;
            }

            long cost = cfg.CostForCopy(state.Count);

            if (_economy.CurrentResources < cost)
            {
                OnPurchaseFailed?.Invoke(generatorId, cost, _economy.CurrentResources);
                return false;
            }

            _economy.DeductResources(cost);
            state.Count++;
            _states[generatorId] = state;

            _saveNotifier?.NotifyUpgradePurchased();
            OnGeneratorPurchased?.Invoke(generatorId);

            Debug.Log($"[GeneratorSystem] Purchased {generatorId} #{state.Count}. Next cost: {cfg.CostForCopy(state.Count)}");
            return true;
        }

        /// <summary>
        /// Total yield per second across all owned generators (before run-state modifiers).
        /// Called by PassiveIncomeService every tick.
        /// </summary>
        public float CalculateTotalYield()
        {
            float total = 0f;
            foreach (var kv in _states)
            {
                if (kv.Value.Count <= 0) continue;
                if (!_configLookup.TryGetValue(kv.Key, out var cfg)) continue;
                total += kv.Value.EffectiveYieldPerSecond(cfg.BaseYieldPerSecond);
            }
            return total;
        }

        /// <summary>
        /// Returns the current count for a generator. 0 if not purchased.
        /// </summary>
        public int GetCount(string generatorId) =>
            _states.TryGetValue(generatorId, out var s) ? s.Count : 0;

        /// <summary>
        /// Returns the cost to buy the next copy of a generator.
        /// </summary>
        public long GetNextCost(string generatorId)
        {
            if (!_configLookup.TryGetValue(generatorId, out var cfg)) return -1;
            int count = GetCount(generatorId);
            return cfg.CostForCopy(count);
        }

        /// <summary>
        /// Applies a multiplier to a specific generator's yield (e.g. from an upgrade).
        /// Multiplies with existing multiplier.
        /// </summary>
        public void ApplyUpgradeMultiplier(string generatorId, float multiplier)
        {
            if (!_states.TryGetValue(generatorId, out var state)) return;
            state.UpgradeMultiplier *= multiplier;
            _states[generatorId] = state;
        }

        /// <summary>Resets all generator counts to 0. Used by ascension deep/full reset.</summary>
        public void ResetAllForAscension()
        {
            var keys = new System.Collections.Generic.List<string>(_states.Keys);
            foreach (var key in keys)
            {
                var state = _states[key];
                state.Count            = 0;
                state.UpgradeMultiplier = 1f;
                _states[key] = state;
            }
        }

        /// <summary>Total generators owned across all types.</summary>
        public int TotalGeneratorsOwned()
        {
            int total = 0;
            foreach (var kv in _states) total += kv.Value.Count;
            return total;
        }

        /// <summary>Read-only view of all generator configs (for UI).</summary>
        public IReadOnlyList<GeneratorConfigSO> Configs => _configs;

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Sets generator count directly. For unit tests only.</summary>
        public void SetCountForTesting(string generatorId, int count)
        {
            if (!_states.TryGetValue(generatorId, out var state)) return;
            state.Count = count;
            _states[generatorId] = state;
        }

        public static void ClearSubscribersForTesting()
        {
            OnGeneratorPurchased = null;
            OnPurchaseFailed     = null;
        }
#endif
    }
}
