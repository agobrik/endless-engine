using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Defines a store product available for purchase.
    ///
    /// SKU is the platform product ID (App Store / Google Play / Steam SKU).
    /// ProductType controls whether the purchase is consumable, non-consumable, or a subscription.
    ///
    /// Create via: Tools → Endless Engine → Create Store Product
    /// </summary>
    [CreateAssetMenu(menuName = "Endless Engine/Store/Store Product", fileName = "StoreProduct")]
    public class StoreProductSO : ScriptableObject
    {
        [Header("Identity")]
        public string ProductId = "";
        public string DisplayName = "Product";

        [TextArea(1, 3)]
        public string Description = "";
        public Sprite Icon;

        [Header("Platform SKU")]
        [Tooltip("App Store / Google Play product SKU. Must match exactly.")]
        public string Sku = "";

        public StoreProductType ProductType = StoreProductType.Consumable;

        [Header("Reward")]
        [Tooltip("Currency amount granted on purchase (consumable only).")]
        public double CurrencyAmount;
        public string CurrencyId = "";

        [Tooltip("If set, unlocks this entry in the unlock log on purchase.")]
        public string UnlockEntryId = "";
    }

    public enum StoreProductType
    {
        Consumable,
        NonConsumable,
        Subscription,
    }
}
