using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;

namespace EndlessEngine.Economy
{
    /// <summary>
    /// Result of a merge operation.
    /// </summary>
    public class MergeResult
    {
        public ItemConfigSO ResultItem;
        public long         GoldBonus;
        public bool         Success;
        public string       FailReason;
    }

    /// <summary>
    /// Handles the merge mechanic: two items of the same tier and group → one item of tier+1.
    ///
    /// The merge service does NOT own the inventory — it delegates add/remove
    /// operations to InventoryService. The caller confirms availability before calling TryMerge.
    ///
    /// Supported merge types:
    ///   - Item merge: two ItemConfigSO entries with matching MergeGroupId and MergeTier.
    ///
    /// Bootstrap wiring:
    ///   mergeService.Initialize(mergeConfigs, inventoryService, economyService);
    /// </summary>
    public class MergeService : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires on successful merge. Parameters: inputItemId, tier, result.</summary>
        public static event Action<string, int, MergeResult> OnMergeCompleted;

        /// <summary>Fires when a merge is attempted but fails.</summary>
        public static event Action<string, int, string> OnMergeFailed;

        // ── State ─────────────────────────────────────────────────────────────────

        private readonly Dictionary<string, MergeConfigSO> _configs = new Dictionary<string, MergeConfigSO>();
        private InventoryService _inventory;
        private EconomyService   _economy;

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(MergeConfigSO[] configs, InventoryService inventory, EconomyService economy = null)
        {
            _configs.Clear();
            if (configs != null)
                foreach (var c in configs)
                    if (c != null && !string.IsNullOrEmpty(c.MergeGroupId))
                        _configs[c.MergeGroupId] = c;
            _inventory = inventory;
            _economy   = economy;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to merge two items of the given itemId (must be stackable ≥ 2 or two individual items).
        /// Removes 2 from inventory, adds the result item, awards gold bonus.
        /// Returns a MergeResult with Success=true on success.
        /// </summary>
        public MergeResult TryMerge(ItemConfigSO item)
        {
            if (item == null)
                return Fail(null, 0, "NullItem");

            if (string.IsNullOrEmpty(item.MergeGroupId))
                return Fail(item.ItemId, item.MergeTier, "NotMergeable");

            if (_inventory == null)
                return Fail(item.ItemId, item.MergeTier, "NoInventory");

            // Need at least 2 in inventory
            if (!_inventory.Has(item.ItemId, 2))
                return Fail(item.ItemId, item.MergeTier, "InsufficientCount");

            if (!_configs.TryGetValue(item.MergeGroupId, out var config))
                return Fail(item.ItemId, item.MergeTier, "NoMergeConfig");

            var rule = config.GetRule(item.MergeTier);
            if (rule == null || rule.ResultItem == null)
                return Fail(item.ItemId, item.MergeTier, "NoRuleForTier");

            // Execute merge
            _inventory.Remove(item.ItemId, 2);
            _inventory.Add(rule.ResultItem.ItemId, 1);
            if (rule.GoldBonus > 0) _economy?.AddResources(rule.GoldBonus);

            var result = new MergeResult
            {
                Success    = true,
                ResultItem = rule.ResultItem,
                GoldBonus  = rule.GoldBonus
            };
            OnMergeCompleted?.Invoke(item.ItemId, item.MergeTier, result);
            return result;
        }

        /// <summary>
        /// Returns true if the player can currently merge two of the given item.
        /// </summary>
        public bool CanMerge(ItemConfigSO item)
        {
            if (item == null || string.IsNullOrEmpty(item.MergeGroupId)) return false;
            if (_inventory == null || !_inventory.Has(item.ItemId, 2)) return false;
            if (!_configs.TryGetValue(item.MergeGroupId, out var config)) return false;
            return config.GetRule(item.MergeTier)?.ResultItem != null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private MergeResult Fail(string itemId, int tier, string reason)
        {
            if (itemId != null) OnMergeFailed?.Invoke(itemId, tier, reason);
            return new MergeResult { Success = false, FailReason = reason };
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnMergeCompleted = null;
            OnMergeFailed    = null;
        }
#endif
    }
}
