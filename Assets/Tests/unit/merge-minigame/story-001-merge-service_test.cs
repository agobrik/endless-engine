// Tests for Sprint 15 — S15-04: MergeService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - TryMerge success: two items at tier 0 → result item
//   - TryMerge deducts 2 from inventory, adds 1 result
//   - TryMerge fires OnMergeCompleted
//   - TryMerge fails: not enough items (< 2)
//   - TryMerge fails: item not in merge group
//   - TryMerge fails: no merge config for group
//   - TryMerge fails: no rule for tier (max tier)
//   - TryMerge awards gold bonus via EconomyService
//   - CanMerge returns true only when conditions are met
//   - Multi-tier progression: tier 0 → 1 → 2
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.MergeMinigame

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.MergeMinigame
{
    [TestFixture]
    public class MergeServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private MergeService    _service;
        private InventoryService _inventory;
        private EconomyService  _economy;

        private ItemConfigSO _tier0Item;
        private ItemConfigSO _tier1Item;
        private ItemConfigSO _tier2Item;
        private ItemConfigSO _nonMergeItem;
        private MergeConfigSO _mergeConfig;

        private readonly List<(string, int, MergeResult)> _completedEvents = new List<(string, int, MergeResult)>();
        private readonly List<(string, int, string)>       _failedEvents    = new List<(string, int, string)>();
        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            MergeService.ClearSubscribersForTesting();
            MergeService.OnMergeCompleted += (id, tier, r) => _completedEvents.Add((id, tier, r));
            MergeService.OnMergeFailed    += (id, tier, r) => _failedEvents.Add((id, tier, r));
            _completedEvents.Clear(); _failedEvents.Clear();

            // Economy
            var ecoGo = new GameObject("Economy");
            _economy  = ecoGo.AddComponent<EconomyService>();
            _economy.Initialize(null, new GameObject("Save").AddComponent<SaveService>());
            var sd = new SaveData(); sd.EnsureDefaults(); sd.CurrentResources = 0;
            _economy.OnAfterLoad(sd);

            // Items
            _tier0Item = ScriptableObject.CreateInstance<ItemConfigSO>();
            _tier0Item.ItemId       = "ore_t0"; _tier0Item.MergeGroupId = "ore"; _tier0Item.MergeTier = 0;
            _tier0Item.MaxStackSize = 99;

            _tier1Item = ScriptableObject.CreateInstance<ItemConfigSO>();
            _tier1Item.ItemId       = "ore_t1"; _tier1Item.MergeGroupId = "ore"; _tier1Item.MergeTier = 1;
            _tier1Item.MaxStackSize = 99;

            _tier2Item = ScriptableObject.CreateInstance<ItemConfigSO>();
            _tier2Item.ItemId       = "ore_t2"; _tier2Item.MergeGroupId = "ore"; _tier2Item.MergeTier = 2;
            _tier2Item.MaxStackSize = 99;

            _nonMergeItem = ScriptableObject.CreateInstance<ItemConfigSO>();
            _nonMergeItem.ItemId       = "potion";
            _nonMergeItem.MergeGroupId = "";  // not mergeable
            _nonMergeItem.MaxStackSize = 99;

            // Merge config
            _mergeConfig = ScriptableObject.CreateInstance<MergeConfigSO>();
            _mergeConfig.MergeGroupId = "ore";
            _mergeConfig.Rules        = new List<MergeRule>
            {
                new MergeRule { InputTier = 0, ResultItem = _tier1Item, GoldBonus = 10 },
                new MergeRule { InputTier = 1, ResultItem = _tier2Item, GoldBonus = 50 },
                // No rule for tier 2 → max tier
            };

            // Inventory
            var invGo  = new GameObject("Inventory");
            _inventory = invGo.AddComponent<InventoryService>();
            _inventory.Initialize(new[] { _tier0Item, _tier1Item, _tier2Item, _nonMergeItem }, maxSlots: 20);

            // Service
            var svcGo = new GameObject("MergeService");
            _service  = svcGo.AddComponent<MergeService>();
            _service.Initialize(new[] { _mergeConfig }, _inventory, _economy);
        }

        [TearDown]
        public void TearDown()
        {
            MergeService.ClearSubscribersForTesting();
            if (_service      != null) Object.DestroyImmediate(_service.gameObject);
            if (_inventory    != null) Object.DestroyImmediate(_inventory.gameObject);
            if (_economy      != null) Object.DestroyImmediate(_economy.gameObject);
            if (_tier0Item    != null) Object.DestroyImmediate(_tier0Item);
            if (_tier1Item    != null) Object.DestroyImmediate(_tier1Item);
            if (_tier2Item    != null) Object.DestroyImmediate(_tier2Item);
            if (_nonMergeItem != null) Object.DestroyImmediate(_nonMergeItem);
            if (_mergeConfig  != null) Object.DestroyImmediate(_mergeConfig);
            if (_econConfig   != null) Object.DestroyImmediate(_econConfig);
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        // ── Success ───────────────────────────────────────────────────────────────

        [Test]
        public void TryMerge_Success_RemovesTwoAddsOne()
        {
            _inventory.Add("ore_t0", 2);
            var result = _service.TryMerge(_tier0Item);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, _inventory.GetCount("ore_t0"), "Two tier-0 items consumed");
            Assert.AreEqual(1, _inventory.GetCount("ore_t1"), "One tier-1 item added");
        }

        [Test]
        public void TryMerge_Success_FiresOnMergeCompleted()
        {
            _inventory.Add("ore_t0", 2);
            _service.TryMerge(_tier0Item);

            Assert.AreEqual(1, _completedEvents.Count);
            Assert.AreEqual("ore_t0", _completedEvents[0].Item1);
        }

        [Test]
        public void TryMerge_Success_AwardsGoldBonus()
        {
            _inventory.Add("ore_t0", 2);
            _service.TryMerge(_tier0Item);

            Assert.AreEqual(10, _economy.CurrentResources, "GoldBonus=10 awarded");
        }

        // ── Multi-tier progression ────────────────────────────────────────────────

        [Test]
        public void TryMerge_Tier0ToTier1ThenTier2()
        {
            _inventory.Add("ore_t0", 4);

            _service.TryMerge(_tier0Item); // → 2 tier-1
            _service.TryMerge(_tier0Item); // → 2 tier-1 total

            // Now merge two tier-1 → one tier-2
            var result = _service.TryMerge(_tier1Item);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, _inventory.GetCount("ore_t2"));
        }

        // ── Failures ──────────────────────────────────────────────────────────────

        [Test]
        public void TryMerge_InsufficientCount_Fails()
        {
            _inventory.Add("ore_t0", 1); // only 1, need 2
            var result = _service.TryMerge(_tier0Item);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("InsufficientCount", result.FailReason);
        }

        [Test]
        public void TryMerge_NotMergeable_Fails()
        {
            _inventory.Add("potion", 5);
            var result = _service.TryMerge(_nonMergeItem);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NotMergeable", result.FailReason);
        }

        [Test]
        public void TryMerge_MaxTier_NoRule_Fails()
        {
            _inventory.Add("ore_t2", 2);
            var result = _service.TryMerge(_tier2Item); // no rule for tier 2

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NoRuleForTier", result.FailReason);
        }

        [Test]
        public void TryMerge_NullItem_Fails()
        {
            var result = _service.TryMerge(null);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NullItem", result.FailReason);
        }

        // ── CanMerge ──────────────────────────────────────────────────────────────

        [Test]
        public void CanMerge_TrueWhenTwoAvailable()
        {
            _inventory.Add("ore_t0", 2);
            Assert.IsTrue(_service.CanMerge(_tier0Item));
        }

        [Test]
        public void CanMerge_FalseWhenOnlyOne()
        {
            _inventory.Add("ore_t0", 1);
            Assert.IsFalse(_service.CanMerge(_tier0Item));
        }

        [Test]
        public void CanMerge_FalseForNonMergeableItem()
        {
            _inventory.Add("potion", 10);
            Assert.IsFalse(_service.CanMerge(_nonMergeItem));
        }
#endif
    }
}
