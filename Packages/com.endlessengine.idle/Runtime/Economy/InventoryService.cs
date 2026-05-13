#pragma warning disable CS0414
using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Economy
{
    /// <summary>
    /// Manages the player's item inventory.
    /// Items are identified by ItemConfigSO.ItemId and stored as stacks (itemId → count).
    ///
    /// Slot-based: maximum MaxSlots distinct item types. Stack counts are per-item.
    /// Persisted via ISaveStateProvider → SaveData.InventoryItems.
    ///
    /// Bootstrap wiring:
    ///   inventoryService.Initialize(itemDatabase, maxSlots);
    ///   saveService.RegisterStateProvider(inventoryService);
    /// </summary>
    public class InventoryService : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.Inventory;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires after any inventory change. Parameters: itemId, newCount, delta.</summary>
        public static event Action<string, int, int> OnInventoryChanged;

        /// <summary>Fires when an add fails due to slot limit or stack cap.</summary>
        public static event Action<string, int> OnInventoryFull;

        // ── Config ────────────────────────────────────────────────────────────────

        [Tooltip("Maximum distinct item types the inventory can hold. 0 = unlimited.")]
        [SerializeField] private int _maxSlots = 20;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private readonly Dictionary<string, int>           _stacks  = new Dictionary<string, int>();
        private readonly Dictionary<string, ItemConfigSO>  _configs = new Dictionary<string, ItemConfigSO>();
        private bool _initialized;

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(ItemConfigSO[] allItems, int maxSlots = 20)
        {
            _maxSlots = maxSlots;
            _configs.Clear();
            if (allItems != null)
                foreach (var item in allItems)
                    if (item != null && !string.IsNullOrEmpty(item.ItemId))
                        _configs[item.ItemId] = item;
            _initialized = true;
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.InventoryItems ??= new Dictionary<string, int>();
            saveData.InventoryItems.Clear();
            foreach (var kv in _stacks)
                saveData.InventoryItems[kv.Key] = kv.Value;
        }

        public void OnAfterLoad(SaveData saveData)
        {
            _stacks.Clear();
            if (saveData.InventoryItems == null) return;
            foreach (var kv in saveData.InventoryItems)
                if (kv.Value > 0) _stacks[kv.Key] = kv.Value;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds count of the given item to inventory.
        /// Respects per-item MaxStackSize and MaxSlots.
        /// Returns the amount actually added (may be less than requested if capped).
        /// </summary>
        public int Add(string itemId, int count)
        {
            if (count <= 0 || string.IsNullOrEmpty(itemId)) return 0;

            int maxStack = GetMaxStack(itemId);
            _stacks.TryGetValue(itemId, out int current);

            // Check slot limit
            if (current == 0 && _maxSlots > 0 && _stacks.Count >= _maxSlots)
            {
                OnInventoryFull?.Invoke(itemId, count);
                return 0;
            }

            int space    = maxStack > 0 ? maxStack - current : int.MaxValue;
            int toAdd    = Mathf.Min(count, space);

            if (toAdd <= 0)
            {
                OnInventoryFull?.Invoke(itemId, count);
                return 0;
            }

            _stacks[itemId] = current + toAdd;
            OnInventoryChanged?.Invoke(itemId, _stacks[itemId], toAdd);
            return toAdd;
        }

        /// <summary>
        /// Removes count of the given item from inventory.
        /// Returns true if count was available; false if insufficient.
        /// </summary>
        public bool Remove(string itemId, int count)
        {
            if (count <= 0 || string.IsNullOrEmpty(itemId)) return false;
            if (!_stacks.TryGetValue(itemId, out int current)) return false;
            if (current < count) return false;

            int newCount = current - count;
            if (newCount == 0)
                _stacks.Remove(itemId);
            else
                _stacks[itemId] = newCount;

            OnInventoryChanged?.Invoke(itemId, newCount, -count);
            return true;
        }

        /// <summary>Returns the count of the given item in inventory. 0 if not present.</summary>
        public int GetCount(string itemId) =>
            _stacks.TryGetValue(itemId, out int c) ? c : 0;

        /// <summary>Returns true if at least count of the item is in inventory.</summary>
        public bool Has(string itemId, int count = 1) => GetCount(itemId) >= count;

        /// <summary>Number of distinct item types in inventory.</summary>
        public int SlotCount => _stacks.Count;

        /// <summary>Maximum slots. 0 = unlimited.</summary>
        public int MaxSlots => _maxSlots;

        /// <summary>Read-only view of all item stacks.</summary>
        public IReadOnlyDictionary<string, int> Stacks => _stacks;

        // ── Helpers ───────────────────────────────────────────────────────────────

        private int GetMaxStack(string itemId)
        {
            if (_configs.TryGetValue(itemId, out var cfg))
                return cfg.MaxStackSize > 0 ? cfg.MaxStackSize : int.MaxValue;
            return 99; // sensible default for unregistered items
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnInventoryChanged = null;
            OnInventoryFull    = null;
        }

        public void ClearForTesting() => _stacks.Clear();
#endif
    }
}
