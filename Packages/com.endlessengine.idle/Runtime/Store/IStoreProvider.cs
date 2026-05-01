using System;
using EndlessEngine.Config;

namespace EndlessEngine.Store
{
    /// <summary>
    /// Abstraction over IAP backends (Unity IAP, Steam, custom).
    ///
    /// Implement per-platform. Register via StoreService.SetProvider().
    /// Default is NullStoreProvider — all purchases fail gracefully.
    ///
    /// Implementations must call the success/failure callbacks on the main thread.
    /// </summary>
    public interface IStoreProvider
    {
        /// <summary>
        /// Initialises the store and fetches product metadata.
        /// Calls onReady when the store is ready to accept purchases.
        /// </summary>
        void Initialize(StoreProductSO[] products, Action onReady, Action<string> onFailed);

        /// <summary>
        /// Initiates a purchase flow for the given product.
        /// Calls onSuccess(productId) or onFailed(productId, reason) when complete.
        /// </summary>
        void Purchase(string productId, Action<string> onSuccess, Action<string, string> onFailed);

        /// <summary>Restores previously purchased non-consumable products (iOS requirement).</summary>
        void RestorePurchases(Action<string> onRestored, Action<string> onFailed);

        /// <summary>True when the store is initialised and ready to accept purchases.</summary>
        bool IsReady { get; }
    }
}
