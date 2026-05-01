// Tests for GeneratorSystem — purchase, yield calculation, save/load
// Type: Logic (Unit/EditMode)
//
// Verifies:
//   (1) New generator starts at count 0
//   (2) TryPurchase deducts cost from EconomyService
//   (3) TryPurchase increments count
//   (4) CalculateTotalYield returns count * baseYield
//   (5) TryPurchase fails if insufficient gold
//   (6) CostForCopy scales by CostScalingFactor
//   (7) OnBeforeSave writes state to SaveData
//   (8) OnAfterLoad restores count from SaveData
//   (9) MaxCount cap prevents over-purchase
//   (10) UpgradeMultiplier applies to yield

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Generator;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.GeneratorSystem
{
    public class GeneratorSystemTests
    {
        private EndlessEngine.Generator.GeneratorSystem _system;
        private EconomyService _economy;
        private GeneratorConfigSO _cfg;

        [SetUp]
        public void SetUp()
        {
            // ConfigRegistry mock
            var econConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            econConfig.ResourceHardCap = 1_000_000L;
            econConfig.StartingGold    = 0L;

            ConfigRegistry.InjectForTesting(economy: econConfig);

            // EconomyService with known balance
            var econGO = new GameObject("Economy");
            _economy = econGO.AddComponent<EconomyService>();
            _economy.Initialize(upgradeTreeQuery: null, saveNotifier: null);
            // SchemaVersion=1 prevents new-game branch, CurrentResources=1000 is restored
            var loadData = new SaveData { CurrentResources = 1000L, SchemaVersion = 1 };
            _economy.OnAfterLoad(loadData);

            // Generator config
            _cfg = ScriptableObject.CreateInstance<GeneratorConfigSO>();
            _cfg.GeneratorId       = "test_gen";
            _cfg.DisplayName       = "Test Generator";
            _cfg.BaseYieldPerSecond = 2f;
            _cfg.BaseCost          = 100;
            _cfg.CostScalingFactor = 1.5f;
            _cfg.MaxCount          = -1;

            // GeneratorSystem
            var sysGO = new GameObject("GeneratorSystem");
            _system = sysGO.AddComponent<EndlessEngine.Generator.GeneratorSystem>();
            _system.Initialize(
                configs:      new GeneratorConfigSO[] { _cfg },
                economy:      _economy,
                saveNotifier: null
            );
        }

        [TearDown]
        public void TearDown()
        {
            ConfigRegistry.ClearForTesting();
            EndlessEngine.Generator.GeneratorSystem.ClearSubscribersForTesting();
            Object.DestroyImmediate(_economy.gameObject);
            Object.DestroyImmediate(_system.gameObject);
            Object.DestroyImmediate(_cfg);
        }

        [Test]
        public void NewGenerator_StartsAtZeroCount()
        {
            Assert.AreEqual(0, _system.GetCount("test_gen"));
        }

        [Test]
        public void TryPurchase_Success_IncrementsCount()
        {
            _system.TryPurchase("test_gen");
            Assert.AreEqual(1, _system.GetCount("test_gen"));
        }

        [Test]
        public void TryPurchase_Success_DeductsCostFromEconomy()
        {
            double before = _economy.CurrentResources;
            long expectedCost = _cfg.CostForCopy(0);

            _system.TryPurchase("test_gen");

            Assert.AreEqual(before - expectedCost, _economy.CurrentResources);
        }

        [Test]
        public void TryPurchase_InsufficientGold_ReturnsFalse()
        {
            _cfg.BaseCost = 9999;
            var result = _system.TryPurchase("test_gen");
            Assert.IsFalse(result);
        }

        [Test]
        public void TryPurchase_InsufficientGold_CountUnchanged()
        {
            _cfg.BaseCost = 9999;
            _system.TryPurchase("test_gen");
            Assert.AreEqual(0, _system.GetCount("test_gen"));
        }

        [Test]
        public void CalculateTotalYield_ZeroGenerators_ReturnsZero()
        {
            Assert.AreEqual(0f, _system.CalculateTotalYield(), 0.001f);
        }

        [Test]
        public void CalculateTotalYield_AfterPurchase_ReturnsBaseYield()
        {
            _system.TryPurchase("test_gen");
            // 1 copy × 2f/sec × 1.0 multiplier = 2f
            Assert.AreEqual(2f, _system.CalculateTotalYield(), 0.001f);
        }

        [Test]
        public void CalculateTotalYield_TwoCopies_ReturnsDoubleYield()
        {
            _system.TryPurchase("test_gen");
            _system.TryPurchase("test_gen");
            Assert.AreEqual(4f, _system.CalculateTotalYield(), 0.001f);
        }

        [Test]
        public void CostScaling_SecondCopy_CostsMore()
        {
            long first  = _cfg.CostForCopy(0);
            long second = _cfg.CostForCopy(1);
            Assert.Greater(second, first);
        }

        [Test]
        public void OnBeforeSave_WritesCountToSaveData()
        {
            _system.SetCountForTesting("test_gen", 3);
            var saveData = new SaveData { GeneratorStates = new Dictionary<string, EndlessEngine.Generator.GeneratorState>() };
            _system.OnBeforeSave(saveData);

            Assert.IsTrue(saveData.GeneratorStates.ContainsKey("test_gen"));
            Assert.AreEqual(3, saveData.GeneratorStates["test_gen"].Count);
        }

        [Test]
        public void OnAfterLoad_RestoresCountFromSaveData()
        {
            var saveData = new SaveData
            {
                GeneratorStates = new Dictionary<string, EndlessEngine.Generator.GeneratorState>
                {
                    ["test_gen"] = new EndlessEngine.Generator.GeneratorState { GeneratorId = "test_gen", Count = 7 }
                }
            };
            _system.OnAfterLoad(saveData);
            Assert.AreEqual(7, _system.GetCount("test_gen"));
        }

        [Test]
        public void MaxCount_PreventsOverPurchase()
        {
            _cfg.MaxCount = 2;
            _system.TryPurchase("test_gen"); // count → 1
            _system.TryPurchase("test_gen"); // count → 2
            bool result = _system.TryPurchase("test_gen"); // should fail
            Assert.IsFalse(result);
            Assert.AreEqual(2, _system.GetCount("test_gen"));
        }

        [Test]
        public void ApplyUpgradeMultiplier_ScalesYield()
        {
            _system.SetCountForTesting("test_gen", 1);
            _system.ApplyUpgradeMultiplier("test_gen", 2f); // 2x multiplier
            // 1 copy × 2f/sec × 2.0 multiplier = 4f
            Assert.AreEqual(4f, _system.CalculateTotalYield(), 0.001f);
        }
    }
}
