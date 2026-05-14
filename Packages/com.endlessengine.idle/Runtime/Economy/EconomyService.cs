using System;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy.Math;
using EndlessEngine.SaveAndLoad;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Economy
{
    /// <summary>
    /// Sole authority on the player's Gold balance.
    /// Receives gains from OnEnemyKilled and OnOfflineGainCalculated.
    /// Validates and executes upgrade purchases via TryPurchase.
    /// Resets to StartingGold on OnPrestigeStarted.
    ///
    /// Internal balance is stored as IBigNumber (backend selected by BigNumberFactory at bootstrap).
    /// Public API exposes both double and long accessors for backwards compatibility.
    /// long accessors clamp at long.MaxValue — use double accessors for display/comparison.
    ///
    /// Hot path: AddResources is zero-allocation — no LINQ, no delegate captures per call.
    ///
    /// ADR: ADR-0004 — ISaveStateProvider Pull-Based Save Collection
    /// ADR: ADR-0005 — Damage Event Bus (same event-bus pattern for economy events)
    /// </summary>
    public class EconomyService : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        /// <inheritdoc />
        public int ProviderOrder => SaveConstants.SaveProviderOrder.Economy;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires after every mutation (gain, deduction, cap truncation, prestige reset).
        /// current = new balance as double. delta signed: positive gain, negative deduction.
        /// </summary>
        public static event Action<double, double> OnResourcesChanged;

        /// <summary>Fires after a successful upgrade purchase.</summary>
        public static event Action<string, double> OnUpgradePurchased;

        /// <summary>Fires when a purchase attempt fails due to insufficient balance.</summary>
        public static event Action<string, double, double> OnPurchaseFailed;

        // ── Dependencies ──────────────────────────────────────────────────────────

        private IUpgradeTreeQuery _upgradeTreeQuery;
        private ISaveNotifier     _saveNotifier;
        private EconomyConfigSO   _config;

        // ── Config cache ──────────────────────────────────────────────────────────

        private IBigNumber _resourceHardCap;
        private IBigNumber _startingGold;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private IBigNumber _currentResources;
        private int          _lastPurchaseFrame = int.MinValue;
        private string       _lastPurchaseNodeId;
        private bool         _initialized;

        // ── Public accessors ──────────────────────────────────────────────────────

        /// <summary>Current Gold balance as double. Use for display and comparison.</summary>
        public double CurrentResources => _currentResources.ToDouble();

        /// <summary>
        /// Current Gold balance as long (clamped at long.MaxValue).
        /// Kept for backwards compatibility with systems that haven't migrated yet.
        /// </summary>
        public long CurrentResourcesLong => _currentResources.ToLong();

        /// <summary>Static double accessor. Used by RunSessionManager and legacy callers.</summary>
        public static double CurrentResourcesStatic { get; private set; }

        /// <summary>Static long accessor for legacy callers. Clamped at long.MaxValue.</summary>
        public static long CurrentResourcesStaticLong => (long)System.Math.Min(CurrentResourcesStatic, (double)long.MaxValue);

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Inject dependencies. Call before SaveService fires OnSaveLoaded.
        /// </summary>
        public void Initialize(IUpgradeTreeQuery upgradeTreeQuery, ISaveNotifier saveNotifier, EconomyConfigSO config = null)
        {
            _upgradeTreeQuery = upgradeTreeQuery;
            _saveNotifier     = saveNotifier;
            _config           = config;
        }

        private void OnEnable()
        {
            Prestige.PrestigeStateManager.OnPrestigeStarted += HandlePrestigeStarted;
            Config.ConfigRegistry.OnRealmSwapped            += RefreshConfigCache;
            OnResourcesChanged += SyncStaticAccessor;
        }

        private void OnDisable()
        {
            Prestige.PrestigeStateManager.OnPrestigeStarted -= HandlePrestigeStarted;
            Config.ConfigRegistry.OnRealmSwapped            -= RefreshConfigCache;
            OnResourcesChanged -= SyncStaticAccessor;
        }

        private static void SyncStaticAccessor(double current, double delta)
            => CurrentResourcesStatic = current;

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        /// <inheritdoc />
        public void OnBeforeSave(SaveData saveData)
        {
            saveData.NumberBackendName = BigNumberFactory.Backend.ToString();

            if (_currentResources is BigDouble bd)
            {
                // Store full mantissa+exponent for lossless BigDouble persistence
                saveData.CurrentResourcesMantissa  = bd.Mantissa;
                saveData.CurrentResourcesExponent  = bd.Exponent;
                // Also write double for backwards compat (within range) or as Infinity marker
                saveData.CurrentResources = bd.ToDouble();
            }
            else
            {
                saveData.CurrentResources          = _currentResources.ToDouble();
                saveData.CurrentResourcesMantissa  = 0.0;
                saveData.CurrentResourcesExponent  = 0;
            }
        }

        /// <inheritdoc />
        public void OnAfterLoad(SaveData saveData)
        {
            EconomyConfigSO economyConfig = _config;
            if (economyConfig == null) try { economyConfig = ConfigRegistry.Economy; } catch { }
            if (economyConfig == null) { Debug.LogError("[EconomyService] No EconomyConfigSO — inject via Initialize(config:)."); return; }
            _resourceHardCap  = BigNumberFactory.Create((double)economyConfig.ResourceHardCap);
            _startingGold     = BigNumberFactory.Create((double)economyConfig.StartingGold);

            bool isNewGame = saveData.CurrentResources == 0
                          && saveData.CurrentResourcesExponent == 0
                          && saveData.SchemaVersion == 0;
            if (isNewGame)
            {
                _currentResources = _startingGold;
            }
            else
            {
                IBigNumber loaded;
                bool hasBigDoubleFields = saveData.CurrentResourcesExponent != 0
                                       || saveData.CurrentResourcesMantissa != 0.0;
                if (hasBigDoubleFields && BigNumberFactory.Backend == NumberBackend.BigDouble)
                    loaded = new BigDouble(saveData.CurrentResourcesMantissa, saveData.CurrentResourcesExponent);
                else
                    loaded = BigNumberFactory.Create(saveData.CurrentResources);

                _currentResources = loaded.IsGreaterThan(_resourceHardCap) ? _resourceHardCap : loaded;
                if (_currentResources.IsNegative) _currentResources = BigNumberFactory.Zero;
            }

            _initialized = true;
            CurrentResourcesStatic = _currentResources.ToDouble();
            OnResourcesChanged?.Invoke(_currentResources.ToDouble(), 0.0);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Add Gold to the balance. Applies hard-cap.
        /// Zero-allocation hot path — safe to call from OnEnemyKilled handler.
        /// </summary>
        public void AddResources(double amount)
        {
            if (!_initialized) return;
            if (amount < 0)
            {
                Debug.LogWarning($"[EconomyService] AddResources called with negative amount: {amount}. Clamping to 0.");
                amount = 0;
            }

            double headroom    = _resourceHardCap.ToDouble() - _currentResources.ToDouble();
            double actualAdded = amount <= headroom ? amount : headroom;

            if (actualAdded == 0 && amount > 0)
                Debug.Log($"[EconomyService] ResourceHardCap reached. Gain of {amount} truncated to 0.");

            _currentResources      = _currentResources.Add(BigNumberFactory.Create(actualAdded));
            CurrentResourcesStatic = _currentResources.ToDouble();
            OnResourcesChanged?.Invoke(_currentResources.ToDouble(), actualAdded);
        }

        /// <summary>Legacy long overload — converts to double internally.</summary>
        public void AddResources(long amount) => AddResources((double)amount);

        /// <summary>
        /// Deduct Gold from the balance. Precondition: CurrentResources >= amount.
        /// </summary>
        public void DeductResources(double amount)
        {
            if (!_initialized) return;
            if (amount < 0 || _currentResources.ToDouble() < amount)
            {
                Debug.LogWarning($"[EconomyService] DeductResources precondition failed: balance={_currentResources.ToDouble()}, amount={amount}");
                return;
            }

            _currentResources      = _currentResources.Subtract(BigNumberFactory.Create(amount));
            CurrentResourcesStatic = _currentResources.ToDouble();
            OnResourcesChanged?.Invoke(_currentResources.ToDouble(), -amount);
        }

        /// <summary>Legacy long overload.</summary>
        public void DeductResources(long amount) => DeductResources((double)amount);

        /// <summary>
        /// Validate and execute an upgrade purchase.
        /// Double-purchase guard: same nodeId twice in the same frame is ignored.
        /// </summary>
        public void TryPurchase(string nodeId)
        {
            if (!_initialized) return;

            int currentFrame = Time.frameCount;
            if (_lastPurchaseFrame == currentFrame && _lastPurchaseNodeId == nodeId)
            {
                Debug.Log($"[EconomyService] TryPurchase: duplicate call for '{nodeId}' in same frame — ignored.");
                return;
            }

            double cost = _upgradeTreeQuery.GetNodeCostDouble(nodeId);

            if (_currentResources.ToDouble() >= cost)
            {
                _lastPurchaseFrame  = currentFrame;
                _lastPurchaseNodeId = nodeId;

                DeductResources(cost);
                OnUpgradePurchased?.Invoke(nodeId, cost);
                _saveNotifier?.NotifyUpgradePurchased();
            }
            else
            {
                OnPurchaseFailed?.Invoke(nodeId, cost, _currentResources.ToDouble());
            }
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void RefreshConfigCache()
        {
            EconomyConfigSO economyConfig = _config;
            if (economyConfig == null) try { economyConfig = Config.ConfigRegistry.Economy; } catch { }
            if (economyConfig == null) return;
            _resourceHardCap  = BigNumberFactory.Create((double)economyConfig.ResourceHardCap);
            _startingGold     = BigNumberFactory.Create((double)economyConfig.StartingGold);

            if (_currentResources.IsGreaterThan(_resourceHardCap))
            {
                double delta      = _resourceHardCap.ToDouble() - _currentResources.ToDouble();
                _currentResources = _resourceHardCap;
                OnResourcesChanged?.Invoke(_currentResources.ToDouble(), delta);
            }

            Debug.Log($"[EconomyService] Config refreshed. HardCap={_resourceHardCap.ToDouble()}, StartingGold={_startingGold.ToDouble()}");
        }

        private void HandlePrestigeStarted()
        {
            double delta      = _startingGold.ToDouble() - _currentResources.ToDouble();
            _currentResources = _startingGold;
            OnResourcesChanged?.Invoke(_currentResources.ToDouble(), delta);
        }

        // ── Test injection ────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void InjectStateForTesting(double currentResources, double hardCap, double startingGold)
        {
            _currentResources = BigNumberFactory.Create(currentResources);
            _resourceHardCap  = BigNumberFactory.Create(hardCap);
            _startingGold     = BigNumberFactory.Create(startingGold);
            _initialized      = true;
        }

        /// <summary>Legacy long overload for existing tests.</summary>
        public void InjectStateForTesting(long currentResources, long hardCap, long startingGold)
            => InjectStateForTesting((double)currentResources, (double)hardCap, (double)startingGold);

        public void SubscribeForTesting()
        {
            Config.ConfigRegistry.OnRealmSwapped += RefreshConfigCache;
        }

        public void UnsubscribeForTesting()
        {
            Config.ConfigRegistry.OnRealmSwapped -= RefreshConfigCache;
        }

        public void InjectPrestigeResetForTesting() => HandlePrestigeStarted();

        public static void ClearSubscribersForTesting()
        {
            OnResourcesChanged    = null;
            OnUpgradePurchased    = null;
            OnPurchaseFailed      = null;
            CurrentResourcesStatic = 0;
        }
#endif
    }
}
