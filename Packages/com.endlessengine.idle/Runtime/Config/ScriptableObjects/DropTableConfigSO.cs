using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Rarity tier for drop table entries.
    /// Drives visual treatment and pity counter tracking.
    /// </summary>
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// A single entry in a drop table. Higher Weight = more likely to roll.
    /// Weight is relative — a weight of 10 among total 100 = 10% chance.
    /// </summary>
    [Serializable]
    public class DropEntry
    {
        [Tooltip("The item config to drop.")]
        public ItemConfigSO Item;

        [Tooltip("Relative weight. Higher = more frequent.")]
        [Min(0)]
        public float Weight = 1f;

        [Tooltip("Override rarity for this entry. If None, uses Item.Rarity.")]
        public ItemRarity Rarity = ItemRarity.Common;

        [Tooltip("Number of this item dropped per roll. Randomized between Min and Max inclusive.")]
        public int MinCount = 1;
        public int MaxCount = 1;
    }

    /// <summary>
    /// A weighted random drop table. Used by DropResolver.Roll() to select items.
    /// Includes pity configuration (guaranteed rare+ after N common rolls).
    ///
    /// Create via: Tools → Endless Engine → Create Drop Table
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Loot/Drop Table",
        fileName = "DropTable")]
    public class DropTableConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID for this drop table.")]
        public string TableId = "";

        [Header("Entries")]
        [Tooltip("All possible drops and their relative weights.")]
        public List<DropEntry> Entries = new List<DropEntry>();

        [Header("Pity System")]
        [Tooltip("Enable pity counter: after N rolls without a rare+, the next roll guarantees a rare+.")]
        public bool EnablePity = false;

        [Tooltip("Number of common-or-below rolls before a pity-guaranteed rare+.")]
        [Min(1)]
        public int PityThreshold = 10;

        [Tooltip("Minimum rarity guaranteed by pity.")]
        public ItemRarity PityMinRarity = ItemRarity.Rare;

        [Header("Rolls Per Use")]
        [Tooltip("How many items to roll per use of this table. 1 = single drop.")]
        [Min(1)]
        public int RollsPerUse = 1;

        /// <summary>Total weight of all entries. Used by DropResolver for normalization.</summary>
        public float TotalWeight()
        {
            float total = 0f;
            if (Entries == null) return total;
            foreach (var e in Entries)
                if (e?.Item != null) total += Mathf.Max(0f, e.Weight);
            return total;
        }
    }
}
