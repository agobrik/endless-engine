using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Unlock;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Store
{
    /// <summary>
    /// Engine-level IAP/store service.
    /// Routes purchase requests to the active IStoreProvider and applies rewards.
    ///
    /// Bootstrap wiring:
    ///   storeService.Initialize(products, economy, unlockService, provider);
    ///   // provider = new UnityIapStoreProvider() or similar
    ///
    /// Default provider is NullStoreProvider — all purchases decline gracefully.
    /// </summary>
    public class StoreService : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when the store finishes initialising. Parameter: success.</summary>
        public static event Action<bool> OnStoreReady;

        /// <summary>Fires when a purchase succeeds. Parameter: productId.</summary>
        public static event Action<string> OnPurchaseSucceeded;

        /// <summary>Fires when a purchase fails. Parameters: (productId, reason).</summary>
        public static event Action<string, string> OnPurchaseFailed;

        // ── State ─────────────────────────────────────────────────────────────────

        private IStoreProvider                             _provider  = NullStoreProvider.Instance;
        private readonly Dictionary<string, StoreProductSO> _products = new();

        private EconomyService          _economy;
        private CurrencyService         _currency;
        private ConditionalUnlockService _unlocks;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        public void Initialize(
            StoreProductSO[]         products,
            EconomyService           economy,
            ConditionalUnlockService unlocks   = null,
            CurrencyService          currency  = null,
            IStoreProvider           provider  = null)
        {
            _economy  = economy;
            _unlocks  = unlocks;
            _currency = currency;
            _provider = provider ?? NullStoreProvider.Instance;

            _products.Clear();
            if (products != null)
                foreach (var p in products)
                    if (p != null && !string.IsNullOrEmpty(p.ProductId))
                        _products[p.ProductId] = p;

            _provider.Initialize(
                products ?? Array.Empty<StoreProductSO>(),
                () => OnStoreReady?.Invoke(true),
                reason =>
                {
                    Debug.LogError($"[StoreService] Init failed: {reason}");
                    OnStoreReady?.Invoke(false);
                });
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>True when the underlying provider is ready.</summary>
        public bool IsReady => _provider.IsReady;

        /// <summary>Initiates a purchase. Fires OnPurchaseSucceeded or OnPurchaseFailed.</summary>
        public void Purchase(string productId)
        {
            if (!_provider.IsReady)
            {
                OnPurchaseFailed?.Invoke(productId, "Store not ready.");
                return;
            }
            _provider.Purchase(
                productId,
                id => { ApplyReward(id); OnPurchaseSucceeded?.Invoke(id); },
                (id, reason) => OnPurchaseFailed?.Invoke(id, reason));
        }

        /// <summary>Restores non-consumable purchases (required on iOS).</summary>
        public void RestorePurchases()
        {
            _provider.RestorePurchases(
                id => { ApplyReward(id); OnPurchaseSucceeded?.Invoke(id); },
                reason => Debug.LogWarning($"[StoreService] Restore failed: {reason}"));
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private void ApplyReward(string productId)
        {
            if (!_products.TryGetValue(productId, out var product)) return;

            if (product.CurrencyAmount > 0)
            {
                if (!string.IsNullOrEmpty(product.CurrencyId))
                    _currency?.Add(product.CurrencyId, product.CurrencyAmount);
                else
                    _economy?.AddResources(product.CurrencyAmount);
            }

            if (!string.IsNullOrEmpty(product.UnlockEntryId))
                _unlocks?.ForceUnlockForTesting(product.UnlockEntryId);
        }

        private void OnDestroy() => ClearSubscribersForTesting();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnStoreReady         = null;
            OnPurchaseSucceeded  = null;
            OnPurchaseFailed     = null;
        }
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }
}
