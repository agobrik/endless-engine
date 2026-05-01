using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Economy
{
    /// <summary>
    /// Manages all secondary currency balances (gems, tokens, shards, etc.).
    /// Gold (primary currency) is still owned by EconomyService for full backwards compat.
    ///
    /// Secondary currencies are defined in <see cref="CurrencyDatabaseSO"/> and are
    /// persisted as <c>SaveData.CurrencyBalances</c> (Dictionary&lt;string, double&gt;).
    ///
    /// Hot path: Add/Spend are O(1) dictionary lookups — zero allocation.
    ///
    /// Bootstrap wiring:
    ///   var cs = gameObject.AddComponent&lt;CurrencyService&gt;();
    ///   cs.Initialize(currencyDatabase);
    ///   saveService.RegisterStateProvider(cs);
    /// </summary>
    public class CurrencyService : MonoBehaviour, ISaveStateProvider
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires after any balance change.
        /// Parameters: (currencyId, newBalance, delta).
        /// </summary>
        public static event Action<string, double, double> OnCurrencyChanged;

        /// <summary>Fires when a spend attempt fails due to insufficient balance.</summary>
        public static event Action<string, double, double> OnSpendFailed;

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public int ProviderOrder => SaveConstants.SaveProviderOrder.Currency;

        // ── State ─────────────────────────────────────────────────────────────────

        private CurrencyDatabaseSO              _database;
        private Dictionary<string, double>      _balances   = new();
        private Dictionary<string, CurrencyConfigSO> _configs = new();
        private bool _initialized;

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Inject dependency. Call before SaveService fires OnSaveLoaded.
        /// </summary>
        public void Initialize(CurrencyDatabaseSO database)
        {
            _database = database;
            if (database == null) return;

            _configs.Clear();
            foreach (var config in database.Currencies)
            {
                if (config == null || string.IsNullOrEmpty(config.CurrencyId)) continue;
                _configs[config.CurrencyId] = config;
            }
        }

        private void OnEnable()
        {
            Prestige.PrestigeStateManager.OnPrestigeStarted += HandlePrestigeStarted;
        }

        private void OnDisable()
        {
            Prestige.PrestigeStateManager.OnPrestigeStarted -= HandlePrestigeStarted;
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void OnBeforeSave(SaveData saveData)
        {
            saveData.CurrencyBalances ??= new Dictionary<string, double>();
            saveData.CurrencyBalances.Clear();
            foreach (var kv in _balances)
                saveData.CurrencyBalances[kv.Key] = kv.Value;
        }

        /// <inheritdoc/>
        public void OnAfterLoad(SaveData saveData)
        {
            _balances.Clear();

            // Initialize all registered currencies to StartingAmount
            foreach (var kv in _configs)
                _balances[kv.Key] = kv.Value.StartingAmount;

            // Restore saved balances (overwrite defaults)
            if (saveData.CurrencyBalances != null)
            {
                foreach (var kv in saveData.CurrencyBalances)
                {
                    if (!_configs.ContainsKey(kv.Key)) continue; // unknown currency — skip
                    var cfg = _configs[kv.Key];
                    double clamped = cfg.HardCap > 0 ? System.Math.Min(kv.Value, cfg.HardCap) : kv.Value;
                    _balances[kv.Key] = System.Math.Max(0, clamped);
                }
            }

            _initialized = true;

            // Notify UI of initial balances
            foreach (var kv in _balances)
                OnCurrencyChanged?.Invoke(kv.Key, kv.Value, 0);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns current balance for the given currency.
        /// Returns 0 if currency is unknown (no throw).
        /// </summary>
        public double GetBalance(string currencyId)
        {
            if (!_initialized || string.IsNullOrEmpty(currencyId)) return 0;
            return _balances.TryGetValue(currencyId, out double v) ? v : 0;
        }

        /// <summary>
        /// Adds <paramref name="amount"/> to the given currency.
        /// Clamps to HardCap if configured. No-ops for unknown currencies.
        /// </summary>
        public void Add(string currencyId, double amount)
        {
            if (!_initialized || string.IsNullOrEmpty(currencyId) || amount <= 0) return;
            if (!_configs.TryGetValue(currencyId, out var cfg)) return;

            double current   = _balances.TryGetValue(currencyId, out double b) ? b : 0;
            double newBalance = cfg.HardCap > 0
                ? System.Math.Min(current + amount, cfg.HardCap)
                : current + amount;

            double actualDelta = newBalance - current;
            _balances[currencyId] = newBalance;

            if (actualDelta > 0)
                OnCurrencyChanged?.Invoke(currencyId, newBalance, actualDelta);
        }

        /// <summary>
        /// Attempts to spend <paramref name="amount"/> from the given currency.
        /// Returns true on success; fires <see cref="OnSpendFailed"/> and returns false if insufficient.
        /// </summary>
        public bool TrySpend(string currencyId, double amount)
        {
            if (!_initialized || string.IsNullOrEmpty(currencyId) || amount <= 0) return false;
            if (!_configs.ContainsKey(currencyId)) return false;

            double current = _balances.TryGetValue(currencyId, out double b) ? b : 0;
            if (current < amount)
            {
                OnSpendFailed?.Invoke(currencyId, amount, current);
                return false;
            }

            _balances[currencyId] = current - amount;
            OnCurrencyChanged?.Invoke(currencyId, _balances[currencyId], -amount);
            return true;
        }

        /// <summary>Returns true if the player has at least <paramref name="amount"/> of the currency.</summary>
        public bool CanAfford(string currencyId, double amount)
        {
            if (!_initialized) return false;
            return GetBalance(currencyId) >= amount;
        }

        /// <summary>
        /// Returns the formatted display string for the given currency balance.
        /// Uses the currency's configured notation and decimal places.
        /// </summary>
        public string GetFormatted(string currencyId)
        {
            double balance = GetBalance(currencyId);
            if (!_configs.TryGetValue(currencyId, out var cfg))
                return BigNumberFormatter.Format(balance);
            return BigNumberFormatter.Format(balance, cfg);
        }

        /// <summary>Returns the CurrencyConfigSO for the given id, or null if not found.</summary>
        public CurrencyConfigSO GetConfig(string currencyId)
            => _configs.TryGetValue(currencyId, out var c) ? c : null;

        /// <summary>Returns all known currency ids.</summary>
        public IReadOnlyCollection<string> GetAllCurrencyIds() => _balances.Keys;

        // ── Prestige Handler ──────────────────────────────────────────────────────

        private void HandlePrestigeStarted()
        {
            foreach (var kv in _configs)
            {
                if (!kv.Value.ResetsOnPrestige) continue;
                double startingAmount = kv.Value.StartingAmount;
                _balances[kv.Key]    = startingAmount;
                OnCurrencyChanged?.Invoke(kv.Key, startingAmount, 0);
            }
        }

        /// <summary>
        /// Resets all currencies with ResetsOnPrestige=true to their StartingAmount.
        /// Called by AscensionStateManager on Deep/Full reset scope.
        /// </summary>
        public void ResetForAscension() => HandlePrestigeStarted();

        // ── Test injection ────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Directly sets a balance for testing without triggering events.</summary>
        public void InjectBalanceForTesting(string currencyId, double amount)
        {
            _balances[currencyId] = amount;
            if (!_configs.ContainsKey(currencyId))
            {
                // Register a minimal config so the currency is recognized
                var cfg = ScriptableObject.CreateInstance<CurrencyConfigSO>();
                cfg.CurrencyId = currencyId;
                cfg.HardCap    = 0;
                _configs[currencyId] = cfg;
            }
            _initialized = true;
        }

        /// <summary>Clears all subscribers for test isolation.</summary>
        public static void ClearSubscribersForTesting()
        {
            OnCurrencyChanged = null;
            OnSpendFailed     = null;
        }

        /// <summary>Resets all balances and config for test isolation.</summary>
        public void ResetForTesting()
        {
            _balances.Clear();
            _configs.Clear();
            _initialized = false;
        }

        /// <summary>
        /// Subscribes to runtime events for testing.
        /// Call after Initialize() in EditMode tests where OnEnable does not fire.
        /// </summary>
        public void SubscribeForTesting()
        {
            Prestige.PrestigeStateManager.OnPrestigeStarted += HandlePrestigeStarted;
        }

        /// <summary>Unsubscribes from runtime events. Call in test TearDown.</summary>
        public void UnsubscribeForTesting()
        {
            Prestige.PrestigeStateManager.OnPrestigeStarted -= HandlePrestigeStarted;
        }
#endif
    }
}
