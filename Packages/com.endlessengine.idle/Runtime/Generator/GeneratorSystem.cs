using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.SaveAndLoad;
using Debug = UnityEngine.Debug;

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
        public static event Action<string, double, double> OnPurchaseFailed;

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

            IBigNumber cost = cfg.CostForCopyBig(state.Count);

            if (_economy.CurrentResources < cost.ToDouble())
            {
                OnPurchaseFailed?.Invoke(generatorId, cost.ToDouble(), _economy.CurrentResources);
                return false;
            }

            _economy.DeductResources(cost.ToDouble());
            state.Count++;
            _states[generatorId] = state;

            _saveNotifier?.NotifyUpgradePurchased();
            OnGeneratorPurchased?.Invoke(generatorId);

            Debug.Log($"[GeneratorSystem] Purchased {generatorId} #{state.Count}. Next cost: {cfg.CostForCopy(state.Count)}");
            return true;
        }

        /// <summary>
        /// Attempts to purchase multiple copies of a generator in one operation.
        /// Uses the geometric series sum to calculate total cost without looping.
        ///
        /// Formula: cost = BaseCost × (scale^currentCount × (scale^n − 1)) / (scale − 1)
        /// where n = number of copies to buy (clamped to affordable count and MaxCount).
        ///
        /// BulkMode.Ten   → buy exactly 10 (or fewer if cap/balance reached)
        /// BulkMode.Max   → buy as many as affordable in one transaction
        /// BulkMode.Until → buy until total count reaches the target threshold
        /// </summary>
        public BulkPurchaseResult TryPurchaseBulk(string generatorId, BulkPurchaseMode mode, int untilCount = 0)
        {
            if (!_configLookup.TryGetValue(generatorId, out var cfg))
            {
                Debug.LogWarning($"[GeneratorSystem] TryPurchaseBulk: unknown id={generatorId}");
                return new BulkPurchaseResult(0, 0);
            }

            if (!_states.TryGetValue(generatorId, out var state))
                return new BulkPurchaseResult(0, 0);

            int currentCount = state.Count;
            int maxAffordable = CalculateAffordableCount(cfg, currentCount, _economy.CurrentResources);

            if (maxAffordable <= 0)
            {
                OnPurchaseFailed?.Invoke(generatorId, (double)cfg.CostForCopy(currentCount), _economy.CurrentResources);
                return new BulkPurchaseResult(0, 0);
            }

            int toBuy = mode switch
            {
                BulkPurchaseMode.Ten   => System.Math.Min(10, maxAffordable),
                BulkPurchaseMode.Max   => maxAffordable,
                BulkPurchaseMode.Until => System.Math.Min(System.Math.Max(0, untilCount - currentCount), maxAffordable),
                _                      => 1
            };

            if (cfg.MaxCount >= 0)
                toBuy = System.Math.Min(toBuy, cfg.MaxCount - currentCount);

            if (toBuy <= 0) return new BulkPurchaseResult(0, 0);

            double totalCost = CalculateBulkCost(cfg, currentCount, toBuy);

            _economy.DeductResources(totalCost);
            state.Count += toBuy;
            _states[generatorId] = state;

            _saveNotifier?.NotifyUpgradePurchased();
            OnGeneratorPurchased?.Invoke(generatorId);

            Debug.Log($"[GeneratorSystem] Bulk purchased {toBuy}× {generatorId}. Total cost: {totalCost}. New count: {state.Count}");
            return new BulkPurchaseResult(toBuy, totalCost);
        }

        /// <summary>
        /// Geometric series cost for buying n copies starting at currentCount.
        /// Formula: BaseCost × scale^currentCount × (scale^n − 1) / (scale − 1)
        /// Falls back to per-copy sum when scale == 1 (linear cost).
        /// </summary>
        public static double CalculateBulkCost(GeneratorConfigSO cfg, int currentCount, int count)
        {
            if (count <= 0) return 0.0;
            double scale = cfg.CostScalingFactor;
            double base_ = cfg.BaseCost;

            if (System.Math.Abs(scale - 1.0) < 1e-9)
                return base_ * count;

            return base_ * System.Math.Pow(scale, currentCount) * (System.Math.Pow(scale, count) - 1.0) / (scale - 1.0);
        }

        /// <summary>
        /// Returns how many copies can be bought given the current balance.
        /// Uses binary search on the geometric series — O(log n), no per-copy iteration.
        /// </summary>
        public static int CalculateAffordableCount(GeneratorConfigSO cfg, int currentCount, double balance)
        {
            if (balance <= 0) return 0;
            double firstCost = cfg.CostForCopy(currentCount);
            if (balance < firstCost) return 0;

            // Binary search: find largest n such that BulkCost(n) <= balance
            int lo = 1, hi = 10_000, result = 0;
            while (lo <= hi)
            {
                int mid  = (lo + hi) / 2;
                double cost = CalculateBulkCost(cfg, currentCount, mid);
                if (cost <= balance) { result = mid; lo = mid + 1; }
                else                  hi = mid - 1;
            }
            return result;
        }

        /// <summary>
        /// Total yield per second across all owned generators (before run-state modifiers).
        /// Returns double to avoid precision loss when PassiveIncomeService multiplies
        /// yield × modifier × dt and accumulates into a double-backed balance.
        /// Called by PassiveIncomeService every tick.
        /// </summary>
        public double CalculateTotalYield()
        {
            double total = 0.0;
            foreach (var kv in _states)
            {
                if (kv.Value.Count <= 0) continue;
                if (!_configLookup.TryGetValue(kv.Key, out var cfg)) continue;
                total += kv.Value.EffectiveYieldPerSecond(cfg.BaseYieldPerSecond);
            }
            return total;
        }

        /// <summary>
        /// IBigNumber version of CalculateTotalYield — backend-aware.
        /// PassiveIncomeService uses this to keep yield in IBigNumber space.
        /// </summary>
        public IBigNumber CalculateTotalYieldBig()
        {
            IBigNumber total = BigNumberFactory.Zero;
            foreach (var kv in _states)
            {
                if (kv.Value.Count <= 0) continue;
                if (!_configLookup.TryGetValue(kv.Key, out var cfg)) continue;
                total = total.Add(BigNumberFactory.Create(kv.Value.EffectiveYieldPerSecond(cfg.BaseYieldPerSecond)));
            }
            return total;
        }

        /// <summary>
        /// Returns the current count for a generator. 0 if not purchased.
        /// </summary>
        public int GetCount(string generatorId) =>
            _states.TryGetValue(generatorId, out var s) ? s.Count : 0;

        /// <summary>Returns the cost to buy the next copy as IBigNumber (backend-aware).</summary>
        public IBigNumber GetNextCostBig(string generatorId)
        {
            if (!_configLookup.TryGetValue(generatorId, out var cfg)) return BigNumberFactory.Zero;
            return cfg.CostForCopyBig(GetCount(generatorId));
        }

        /// <summary>Returns the cost to buy the next copy as long (legacy callers).</summary>
        public long GetNextCost(string generatorId)
        {
            if (!_configLookup.TryGetValue(generatorId, out var cfg)) return -1;
            return cfg.CostForCopy(GetCount(generatorId));
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

        /// <summary>
        /// Returns the display cost for buying n copies in a bulk mode (for UI labels).
        /// Does NOT execute the purchase. Returns 0 if the generator is unknown.
        /// </summary>
        public double GetBulkCostDisplay(string generatorId, BulkPurchaseMode mode)
        {
            if (!_configLookup.TryGetValue(generatorId, out var cfg)) return 0.0;
            if (!_states.TryGetValue(generatorId, out var state)) return 0.0;

            int currentCount = state.Count;
            double balance = _economy != null ? _economy.CurrentResources : Economy.EconomyService.CurrentResourcesStatic;
            int maxAffordable = CalculateAffordableCount(cfg, currentCount, balance);

            int n = mode switch
            {
                BulkPurchaseMode.Ten  => System.Math.Min(10, maxAffordable),
                BulkPurchaseMode.Max  => maxAffordable,
                _                     => 1
            };

            return n > 0 ? CalculateBulkCost(cfg, currentCount, n) : 0.0;
        }

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
