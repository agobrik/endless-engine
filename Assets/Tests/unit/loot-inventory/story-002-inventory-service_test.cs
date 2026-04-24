// Tests for Sprint 11 — S11-05: InventoryService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - Add: basic add, stack merge
//   - Add: MaxStackSize enforcement, OnInventoryFull event
//   - Add: slot limit enforcement
//   - Remove: success, insufficient count
//   - Has / GetCount
//   - Save/load round-trip
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.LootInventory

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.LootInventory
{
    [TestFixture]
    public class InventoryServiceTests
    {
        private InventoryService _inventory;
        private ItemConfigSO     _ore;
        private ItemConfigSO     _gem;
        private ItemConfigSO     _sword; // non-stackable (MaxStackSize=1)

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _ore = ScriptableObject.CreateInstance<ItemConfigSO>();
            _ore.ItemId       = "ore";
            _ore.MaxStackSize = 99;

            _gem = ScriptableObject.CreateInstance<ItemConfigSO>();
            _gem.ItemId       = "gem";
            _gem.MaxStackSize = 10;

            _sword = ScriptableObject.CreateInstance<ItemConfigSO>();
            _sword.ItemId       = "sword";
            _sword.MaxStackSize = 1;

            var go = new GameObject("InventoryTest");
            _inventory = go.AddComponent<InventoryService>();
            _inventory.Initialize(new[] { _ore, _gem, _sword }, maxSlots: 3);
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            InventoryService.ClearSubscribersForTesting();

            if (_inventory != null) Object.DestroyImmediate(_inventory.gameObject);
            if (_ore   != null) Object.DestroyImmediate(_ore);
            if (_gem   != null) Object.DestroyImmediate(_gem);
            if (_sword != null) Object.DestroyImmediate(_sword);
#endif
        }

        // ── Add ───────────────────────────────────────────────────────────────────

        [Test]
        public void Add_NewItem_CountIsCorrect()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _inventory.Add("ore", 5);
            Assert.AreEqual(5, _inventory.GetCount("ore"));
#endif
        }

        [Test]
        public void Add_ExistingItem_StacksMerge()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _inventory.Add("ore", 10);
            _inventory.Add("ore", 20);
            Assert.AreEqual(30, _inventory.GetCount("ore"));
#endif
        }

        [Test]
        public void Add_ExceedsMaxStack_ClampedToMax()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _inventory.Add("gem", 8);
            int added = _inventory.Add("gem", 5); // would overflow to 13, max is 10
            Assert.AreEqual(2, added, "Should only add 2 (up to max stack 10)");
            Assert.AreEqual(10, _inventory.GetCount("gem"));
#endif
        }

        [Test]
        public void Add_AtStackMax_FiresOnInventoryFull()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _inventory.Add("gem", 10);
            string capturedId = null;
            InventoryService.OnInventoryFull += (id, _) => capturedId = id;

            int added = _inventory.Add("gem", 1);
            Assert.AreEqual(0, added);
            Assert.AreEqual("gem", capturedId);
#endif
        }

        // ── Slot limit ────────────────────────────────────────────────────────────

        [Test]
        public void Add_SlotLimitReached_NewItemNotAdded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Fill all 3 slots
            _inventory.Add("ore",   1);
            _inventory.Add("gem",   1);
            _inventory.Add("sword", 1);

            // Adding a 4th item type must fail
            var extraItem = ScriptableObject.CreateInstance<ItemConfigSO>();
            extraItem.ItemId       = "shield";
            extraItem.MaxStackSize = 99;

            int added = _inventory.Add("shield", 1);
            Assert.AreEqual(0, added, "Slot limit reached — no new item type should be added");

            Object.DestroyImmediate(extraItem);
#endif
        }

        // ── Remove ────────────────────────────────────────────────────────────────

        [Test]
        public void Remove_SufficientCount_ReturnsTrueAndDeducts()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _inventory.Add("ore", 10);
            bool result = _inventory.Remove("ore", 3);
            Assert.IsTrue(result);
            Assert.AreEqual(7, _inventory.GetCount("ore"));
#endif
        }

        [Test]
        public void Remove_ExactCount_SlotFreed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _inventory.Add("gem", 5);
            _inventory.Remove("gem", 5);
            Assert.AreEqual(0, _inventory.GetCount("gem"));
            Assert.AreEqual(0, _inventory.SlotCount, "Slot must be freed when count reaches 0");
#endif
        }

        [Test]
        public void Remove_InsufficientCount_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _inventory.Add("ore", 2);
            bool result = _inventory.Remove("ore", 5);
            Assert.IsFalse(result);
            Assert.AreEqual(2, _inventory.GetCount("ore"), "Count must be unchanged on failed remove");
#endif
        }

        [Test]
        public void Remove_AbsentItem_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool result = _inventory.Remove("ore", 1);
            Assert.IsFalse(result);
#endif
        }

        // ── Has ───────────────────────────────────────────────────────────────────

        [Test]
        public void Has_SufficientCount_ReturnsTrue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _inventory.Add("ore", 10);
            Assert.IsTrue(_inventory.Has("ore", 5));
            Assert.IsTrue(_inventory.Has("ore", 10));
            Assert.IsFalse(_inventory.Has("ore", 11));
#endif
        }

        // ── OnInventoryChanged event ──────────────────────────────────────────────

        [Test]
        public void Add_FiresOnInventoryChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string capturedId = null;
            int capturedCount  = 0;
            InventoryService.OnInventoryChanged += (id, count, _) => { capturedId = id; capturedCount = count; };

            _inventory.Add("ore", 7);
            Assert.AreEqual("ore", capturedId);
            Assert.AreEqual(7, capturedCount);
#endif
        }

        // ── Save / load ───────────────────────────────────────────────────────────

        [Test]
        public void SaveLoad_InventoryItemsPersistedAndRestored()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _inventory.Add("ore",  15);
            _inventory.Add("gem",  3);

            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _inventory.OnBeforeSave(saveData);

            Assert.AreEqual(15, saveData.InventoryItems["ore"]);
            Assert.AreEqual(3,  saveData.InventoryItems["gem"]);

            // Fresh inventory loads
            var go2 = new GameObject("InventoryTest2");
            var inv2 = go2.AddComponent<InventoryService>();
            inv2.Initialize(new[] { _ore, _gem, _sword }, maxSlots: 3);
            inv2.OnAfterLoad(saveData);

            Assert.AreEqual(15, inv2.GetCount("ore"));
            Assert.AreEqual(3,  inv2.GetCount("gem"));

            Object.DestroyImmediate(go2);
#endif
        }
    }
}
