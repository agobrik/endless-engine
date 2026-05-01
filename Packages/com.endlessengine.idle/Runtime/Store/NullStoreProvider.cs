using System;
using EndlessEngine.Config;

namespace EndlessEngine.Store
{
    /// <summary>No-op store provider. Default until a platform provider is wired.</summary>
    public sealed class NullStoreProvider : IStoreProvider
    {
        public static readonly NullStoreProvider Instance = new();
        private NullStoreProvider() { }

        public bool IsReady => false;

        public void Initialize(StoreProductSO[] products, Action onReady, Action<string> onFailed)
            => onFailed?.Invoke("NullStoreProvider: no IAP backend configured.");

        public void Purchase(string productId, Action<string> onSuccess, Action<string, string> onFailed)
            => onFailed?.Invoke(productId, "NullStoreProvider: no IAP backend configured.");

        public void RestorePurchases(Action<string> onRestored, Action<string> onFailed)
            => onFailed?.Invoke("NullStoreProvider: no IAP backend configured.");
    }
}
