using System;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Economy
{
    /// <summary>
    /// Sole authority on the player's Gold balance (CurrentResources).
    /// Receives gains from OnEnemyKilled and OnOfflineGainCalculated.
    /// Validates and executes upgrade purchases via TryPurchase.
    /// Resets to StartingGold on OnPrestigeStarted.
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

        /// <summary>Fires after every mutation (gain, deduction, cap truncation, prestige reset).
        /// Delta is signed: positive for gains, negative for deductions and resets.</summary>
        public static event Action<long, long> OnResourcesChanged;

        /// <summary>Fires after a successful upgrade purchase.</summary>
        public static event Action<string, long> OnUpgradePurchased;

        /// <summary>Fires when a purchase attempt fails due to insufficient balance.</summary>
        public static event Action<string, long, long> OnPurchaseFailed;

        // ── Dependencies (injected via Initialize or set in tests) ─────────────────

        private IUpgradeTreeQuery _upgradeTreeQuery;
        private ISaveNotifier     _saveNotifier;

        // ── Config cache (populated on OnAfterLoad) ────────────────────────────────

        private long _resourceHardCap;
        private long _startingGold;

        // ── Runtime state ──────────────────────────────────────────────────────────

        private long _currentResources;
        private int  _lastPurchaseFrame = int.MinValue;
        private string _lastPurchaseNodeId;
        private bool _initialized;

        // ── Public accessors ───────────────────────────────────────────────────────

        /// <summary>Current Gold balance. Read-only external access.</summary>
        public long CurrentResources => _currentResources;

        /// <summary>Static accessor for current Gold balance. Used by RunSessionManager to snapshot gold at run boundaries.</summary>
        public static long CurrentResourcesStatic { get; private set; }

        // ── Initialization ─────────────────────────────────────────────────────────

        /// <summary>
        /// Inject dependencies. Call before SaveService fires OnSaveLoaded.
        /// </summary>
        public void Initialize(IUpgradeTreeQuery upgradeTreeQuery, ISaveNotifier saveNotifier)
        {
            _upgradeTreeQuery = upgradeTreeQuery;
            _saveNotifier     = saveNotifier;
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

        private static void SyncStaticAccessor(long current, long delta) => CurrentResourcesStatic = current;

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        /// <inheritdoc />
        public void OnBeforeSave(SaveData saveData)
        {
            saveData.CurrentResources = _currentResources;
        }

        /// <inheritdoc />
        public void OnAfterLoad(SaveData saveData)
        {
            // Cache config values once — not re-read per event (per GDD Rule 5, Config section)
            var economyConfig = ConfigRegistry.Economy;
            _resourceHardCap  = economyConfig.ResourceHardCap;
            _startingGold     = economyConfig.StartingGold;

            bool isNewGame = saveData.CurrentResources == 0 && saveData.SchemaVersion == 0;
            if (isNewGame)
            {
                _currentResources = _startingGold;
            }
            else
            {
                // Clamp to current cap in case config was patched since last save (EC-ECO-08)
                _currentResources = Math.Min(saveData.CurrentResources, _resourceHardCap);
                // Clamp to zero — defensive against corrupted saves
                if (_currentResources < 0) _currentResources = 0;
            }

            _initialized = true;
            CurrentResourcesStatic = _currentResources;
            OnResourcesChanged?.Invoke(_currentResources, 0L);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Add Gold to the balance. Applies F2 hard-cap formula.
        /// Zero-allocation hot path — safe to call from OnEnemyKilled handler.
        /// </summary>
        public void AddResources(long amount)
        {
            if (!_initialized) return;
            if (amount < 0)
            {
                Debug.LogError($"[EconomyService] AddResources called with negative amount: {amount}. Clamping to 0.");
                amount = 0;
            }

            // Overflow-safe cap check (EC-ECO-07)
            long headroom = _resourceHardCap - _currentResources;
            long actualAdded = amount <= headroom ? amount : headroom;

            if (actualAdded == 0 && amount > 0)
            {
                // Silently truncated — at cap (GDD Rule 3)
                Debug.Log($"[EconomyService] ResourceHardCap reached. Gain of {amount} truncated to 0.");
            }

            _currentResources += actualAdded;
            OnResourcesChanged?.Invoke(_currentResources, actualAdded);
        }

        /// <summary>
        /// Deduct Gold from the balance. Precondition: CurrentResources >= amount.
        /// Raises OnResourcesChanged with negative delta.
        /// </summary>
        public void DeductResources(long amount)
        {
            if (!_initialized) return;
            if (amount < 0 || _currentResources < amount)
            {
                Debug.LogError($"[EconomyService] DeductResources precondition failed: balance={_currentResources}, amount={amount}");
                return;
            }

            _currentResources -= amount;
            OnResourcesChanged?.Invoke(_currentResources, -amount);
        }

        /// <summary>
        /// Validate and execute an upgrade purchase.
        /// Raises OnUpgradePurchased on success, OnPurchaseFailed on insufficient balance.
        /// Double-purchase guard: same nodeId twice in the same frame is ignored.
        /// </summary>
        public void TryPurchase(string nodeId)
        {
            if (!_initialized) return;

            // Double-purchase guard (GDD Rule 13)
            int currentFrame = Time.frameCount;
            if (_lastPurchaseFrame == currentFrame && _lastPurchaseNodeId == nodeId)
            {
                Debug.Log($"[EconomyService] TryPurchase: duplicate call for '{nodeId}' in same frame — ignored.");
                return;
            }

            long cost = _upgradeTreeQuery.GetNodeCost(nodeId);

            if (_currentResources >= cost)
            {
                _lastPurchaseFrame  = currentFrame;
                _lastPurchaseNodeId = nodeId;

                DeductResources(cost);
                OnUpgradePurchased?.Invoke(nodeId, cost);
                _saveNotifier?.NotifyUpgradePurchased();
            }
            else
            {
                OnPurchaseFailed?.Invoke(nodeId, cost, _currentResources);
            }
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        /// <summary>
        /// Re-reads economy config after a realm swap.
        /// Clamps current balance to the new hard cap if it has decreased.
        /// ADR-0003 — Config Registry: downstream services must listen to OnRealmSwapped.
        /// </summary>
        private void RefreshConfigCache()
        {
            var economyConfig = Config.ConfigRegistry.Economy;
            _resourceHardCap  = economyConfig.ResourceHardCap;
            _startingGold     = economyConfig.StartingGold;

            // Clamp balance to new cap if the new realm has a lower cap
            if (_currentResources > _resourceHardCap)
            {
                long delta = _resourceHardCap - _currentResources;
                _currentResources = _resourceHardCap;
                OnResourcesChanged?.Invoke(_currentResources, delta);
            }

            Debug.Log($"[EconomyService] Config refreshed after realm swap. HardCap={_resourceHardCap}, StartingGold={_startingGold}");
        }

        private void HandlePrestigeStarted()
        {
            long delta = _startingGold - _currentResources; // negative delta (reset)
            _currentResources = _startingGold;
            OnResourcesChanged?.Invoke(_currentResources, delta);
        }

        // ── Test injection ────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Direct state injection for tests. Do not use in production code.</summary>
        public void InjectStateForTesting(long currentResources, long hardCap, long startingGold)
        {
            _currentResources = currentResources;
            _resourceHardCap  = hardCap;
            _startingGold     = startingGold;
            _initialized      = true;
        }

        /// <summary>
        /// Subscribes to ConfigRegistry events for testing.
        /// Call after Initialize() in EditMode tests where OnEnable does not fire.
        /// </summary>
        public void SubscribeForTesting()
        {
            Config.ConfigRegistry.OnRealmSwapped += RefreshConfigCache;
        }

        /// <summary>Unsubscribes from ConfigRegistry events. Call in test TearDown.</summary>
        public void UnsubscribeForTesting()
        {
            Config.ConfigRegistry.OnRealmSwapped -= RefreshConfigCache;
        }

        /// <summary>
        /// Directly invokes the prestige reset handler for unit tests.
        /// Simulates OnPrestigeStarted firing without requiring PrestigeStateManager.
        /// </summary>
        public void InjectPrestigeResetForTesting() => HandlePrestigeStarted();
#endif
    }
}
