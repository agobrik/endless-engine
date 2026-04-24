// Integration Tests for Sprint 19 — S19-01
// Test chain: Loot → Inventory → Merge → Tier progression
// Type: Integration (EditMode — multi-service interactions)
//
// Covers:
//   - InventoryService.Add distributes items correctly
//   - MergeService.TryMerge consumes inventory items and produces tier+1
//   - Multi-tier chain: tier 0 × 4 → 2 tier-1 → 1 tier-2
//   - MergeService awards gold bonus via EconomyService
//   - Inventory state persists through merge chain
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Integration.FullSystem

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Integration.FullSystem
{
    [TestFixture]
    public class LootInventoryMergeChainTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private InventoryService  _inventory;
        private MergeService      _mergeService;
        private EconomyService    _economy;

        private ItemConfigSO    _tier0;
        private ItemConfigSO    _tier1;
        private ItemConfigSO    _tier2;
        private MergeConfigSO   _mergeConfig;

        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            // Economy
            var ecoGo = new GameObject("Economy");
            _economy  = ecoGo.AddComponent<EconomyService>();
            _economy.Initialize(null, new GameObject("Save").AddComponent<SaveService>());
            var sd = new SaveData(); sd.EnsureDefaults(); sd.CurrentResources = 0;
            _economy.OnAfterLoad(sd);

            // Items
            _tier0 = ScriptableObject.CreateInstance<ItemConfigSO>();
            _tier0.ItemId = "ore_t0"; _tier0.MergeGroupId = "ore"; _tier0.MergeTier = 0; _tier0.MaxStackSize = 99;
            _tier1 = ScriptableObject.CreateInstance<ItemConfigSO>();
            _tier1.ItemId = "ore_t1"; _tier1.MergeGroupId = "ore"; _tier1.MergeTier = 1; _tier1.MaxStackSize = 99;
            _tier2 = ScriptableObject.CreateInstance<ItemConfigSO>();
            _tier2.ItemId = "ore_t2"; _tier2.MergeGroupId = "ore"; _tier2.MergeTier = 2; _tier2.MaxStackSize = 99;

            _mergeConfig = ScriptableObject.CreateInstance<MergeConfigSO>();
            _mergeConfig.MergeGroupId = "ore";
            _mergeConfig.Rules = new List<MergeRule>
            {
                new MergeRule { InputTier = 0, ResultItem = _tier1, GoldBonus = 5 },
                new MergeRule { InputTier = 1, ResultItem = _tier2, GoldBonus = 20 },
            };

            var invGo   = new GameObject("Inventory");
            _inventory  = invGo.AddComponent<InventoryService>();
            _inventory.Initialize(new[] { _tier0, _tier1, _tier2 }, maxSlots: 30);

            var svcGo      = new GameObject("MergeService");
            _mergeService  = svcGo.AddComponent<MergeService>();
            _mergeService.Initialize(new[] { _mergeConfig }, _inventory, _economy);
        }

        [TearDown]
        public void TearDown()
        {
            MergeService.ClearSubscribersForTesting();
            if (_inventory    != null) Object.DestroyImmediate(_inventory.gameObject);
            if (_mergeService != null) Object.DestroyImmediate(_mergeService.gameObject);
            if (_economy      != null) Object.DestroyImmediate(_economy.gameObject);
            Object.DestroyImmediate(_tier0);
            Object.DestroyImmediate(_tier1);
            Object.DestroyImmediate(_tier2);
            Object.DestroyImmediate(_mergeConfig);
            if (_econConfig   != null) Object.DestroyImmediate(_econConfig);
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        [Test]
        public void LootDropToInventory_AndMergeToTier2_FullChain()
        {
            // Simulate loot drops: 4× tier-0
            _inventory.Add("ore_t0", 4);
            Assert.AreEqual(4, _inventory.GetCount("ore_t0"));

            // Merge 1: 2× tier-0 → 1× tier-1 (+5 gold)
            var r1 = _mergeService.TryMerge(_tier0);
            Assert.IsTrue(r1.Success);
            Assert.AreEqual(2, _inventory.GetCount("ore_t0"), "2 tier-0 remaining");
            Assert.AreEqual(1, _inventory.GetCount("ore_t1"));
            Assert.AreEqual(5, _economy.CurrentResources);

            // Merge 2: 2× tier-0 → 1× tier-1 (+5 gold more)
            var r2 = _mergeService.TryMerge(_tier0);
            Assert.IsTrue(r2.Success);
            Assert.AreEqual(0, _inventory.GetCount("ore_t0"), "All tier-0 consumed");
            Assert.AreEqual(2, _inventory.GetCount("ore_t1"), "2 tier-1 accumulated");
            Assert.AreEqual(10, _economy.CurrentResources);

            // Merge 3: 2× tier-1 → 1× tier-2 (+20 gold)
            var r3 = _mergeService.TryMerge(_tier1);
            Assert.IsTrue(r3.Success);
            Assert.AreEqual(0, _inventory.GetCount("ore_t1"), "All tier-1 consumed");
            Assert.AreEqual(1, _inventory.GetCount("ore_t2"), "One tier-2 produced");
            Assert.AreEqual(30, _economy.CurrentResources, "Total gold bonus: 5+5+20 = 30");
        }

        [Test]
        public void MergeAtMaxTier_Fails_InventoryUnchanged()
        {
            _inventory.Add("ore_t2", 2);
            var result = _mergeService.TryMerge(_tier2);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NoRuleForTier", result.FailReason);
            Assert.AreEqual(2, _inventory.GetCount("ore_t2"), "Inventory unchanged on failure");
        }

#endif
    }
}
