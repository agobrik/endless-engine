#if UNITY_PURCHASING
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using EndlessEngine.Config;

namespace EndlessEngine.Store
{
    /// <summary>
    /// Unity IAP concrete IStoreProvider.
    /// Requires com.unity.purchasing package and UNITY_PURCHASING scripting define.
    ///
    /// Activate:
    ///   1. Install com.unity.purchasing via Package Manager
    ///   2. Add "UNITY_PURCHASING" to Player Settings → Scripting Define Symbols
    ///   3. In Bootstrap: storeService.Initialize(products, economy, unlocks, currency,
    ///                        new UnityIapStoreProvider());
    /// </summary>
    public class UnityIapStoreProvider : IStoreProvider,
        IStoreListener
    {
        private IStoreController   _controller;
        private IExtensionProvider _extensions;

        private Action           _onReady;
        private Action<string>   _onInitFailed;

        private readonly Dictionary<string, (Action<string> onSuccess, Action<string, string> onFailed)>
            _pendingPurchases = new();

        public bool IsReady => _controller != null;

        public void Initialize(StoreProductSO[] products, Action onReady, Action<string> onFailed)
        {
            _onReady      = onReady;
            _onInitFailed = onFailed;

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            if (products != null)
                foreach (var p in products)
                    if (p != null && !string.IsNullOrEmpty(p.Sku))
                        builder.AddProduct(p.Sku, MapProductType(p.ProductType));

            UnityPurchasing.Initialize(this, builder);
        }

        public void Purchase(string productId, Action<string> onSuccess, Action<string, string> onFailed)
        {
            if (_controller == null) { onFailed?.Invoke(productId, "Store not initialised."); return; }

            var product = _controller.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
            {
                onFailed?.Invoke(productId, "Product not available.");
                return;
            }

            _pendingPurchases[productId] = (onSuccess, onFailed);
            _controller.InitiatePurchase(product);
        }

        public void RestorePurchases(Action<string> onRestored, Action<string> onFailed)
        {
#if UNITY_IOS
            var apple = _extensions?.GetExtension<IAppleExtensions>();
            apple?.RestoreTransactions(result =>
            {
                if (result) Debug.Log("[UnityIap] Restore complete.");
                else        onFailed?.Invoke("Restore failed.");
            });
#else
            Debug.Log("[UnityIap] RestorePurchases not required on this platform.");
#endif
        }

        // ── IStoreListener ────────────────────────────────────────────────────────

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _controller = controller;
            _extensions = extensions;
            Debug.Log("[UnityIap] Initialized.");
            _onReady?.Invoke();
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogError($"[UnityIap] Init failed: {error}");
            _onInitFailed?.Invoke(error.ToString());
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError($"[UnityIap] Init failed: {error} — {message}");
            _onInitFailed?.Invoke(message ?? error.ToString());
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            string id = args.purchasedProduct.definition.id;
            if (_pendingPurchases.TryGetValue(id, out var callbacks))
            {
                _pendingPurchases.Remove(id);
                callbacks.onSuccess?.Invoke(id);
            }
            else
            {
                // Restored purchase — fire as success with no stored callback
                Debug.Log($"[UnityIap] Restored purchase: {id}");
            }
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
        {
            string id = product.definition.id;
            Debug.LogWarning($"[UnityIap] Purchase failed: {id} — {reason}");
            if (_pendingPurchases.TryGetValue(id, out var callbacks))
            {
                _pendingPurchases.Remove(id);
                callbacks.onFailed?.Invoke(id, reason.ToString());
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static ProductType MapProductType(StoreProductType t) => t switch
        {
            StoreProductType.NonConsumable => ProductType.NonConsumable,
            StoreProductType.Subscription  => ProductType.Subscription,
            _                              => ProductType.Consumable,
        };
    }
}
#endif
