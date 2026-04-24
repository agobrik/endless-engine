using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Defines a single item type. Used by drop tables and inventory.
    ///
    /// Create via: Tools → Endless Engine → Create Item Config
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Loot/Item Config",
        fileName = "ItemConfig")]
    public class ItemConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique item ID. Never change after release.")]
        public string ItemId = "";

        [Tooltip("Player-facing display name.")]
        public string DisplayName = "Item";

        [Tooltip("Short flavour description shown in inventory tooltip.")]
        [TextArea(2, 3)]
        public string Description = "";

        [Tooltip("Item icon shown in inventory grid and drop popup.")]
        public Sprite Icon;

        [Header("Stack")]
        [Tooltip("Rarity tier affects visual treatment.")]
        public ItemRarity Rarity = ItemRarity.Common;

        [Tooltip("Maximum stack size. 1 = non-stackable. 0 = unlimited.")]
        [Min(1)]
        public int MaxStackSize = 99;

        [Header("Classification")]
        [Tooltip("Optional tag list for filtering/sorting (e.g. 'equipment', 'consumable', 'material').")]
        public string[] Tags = new string[0];

        [Header("Merge")]
        [Tooltip("Group ID used by MergeConfigSO. Leave empty if this item is not mergeable.")]
        public string MergeGroupId = "";

        [Tooltip("Tier within its merge group (0 = lowest). Two items of the same tier and group merge into tier+1.")]
        [Min(0)]
        public int MergeTier = 0;
    }
}
