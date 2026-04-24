// Integration Tests — Sprint 22 — S22-01
// Test chain: BuildingService.TryPlace → Economy debit, OnTick → Economy credit,
// Save/Load round-trip preserves placed buildings.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Integration.FullSystem

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Building;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Integration.FullSystem
{
    [TestFixture]
    public class BuildingEconomyChainTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private BuildingService _buildingService;
        private EconomyService  _economy;
        private SaveService     _saveService;
        private BuildingConfigSO _buildingConfig;

        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            var ecoGo    = new GameObject("Economy");
            _economy     = ecoGo.AddComponent<EconomyService>();
            var savGo    = new GameObject("SaveService");
            _saveService = savGo.AddComponent<SaveService>();
            _economy.Initialize(null, _saveService);

            _buildingConfig = ScriptableObject.CreateInstance<BuildingConfigSO>();
            _buildingConfig.BuildingId        = "test_farm";
            _buildingConfig.DisplayName       = "Test Farm";
            _buildingConfig.PlacementCost     = 100;
            _buildingConfig.PlacementCurrencyId = "gold";
            _buildingConfig.ProductionPerTick = 10;
            _buildingConfig.MaxInstances      = 3;
            _buildingConfig.UpgradeTiers      = new BuildingUpgradeTier[0];

            var bldGo        = new GameObject("BuildingService");
            _buildingService = bldGo.AddComponent<BuildingService>();
            _buildingService.Initialize(new[] { _buildingConfig }, _economy);

            var sd = new SaveData();
            sd.EnsureDefaults();
            _economy.OnAfterLoad(sd);
            _buildingService.OnAfterLoad(sd);
        }

        [TearDown]
        public void TearDown()
        {
            if (_economy         != null) Object.DestroyImmediate(_economy.gameObject);
            if (_buildingService != null) Object.DestroyImmediate(_buildingService.gameObject);
            if (_saveService     != null) Object.DestroyImmediate(_saveService.gameObject);
            if (_buildingConfig  != null) Object.DestroyImmediate(_buildingConfig);
            if (_econConfig      != null) Object.DestroyImmediate(_econConfig);

            BuildingService.ClearSubscribersForTesting();
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        [Test]
        public void TryPlace_DeductsGold_WhenSufficient()
        {
            _economy.AddResources(500);
            var result = _buildingService.TryPlace("test_farm", 0, 0);

            Assert.IsTrue(result.Success, "TryPlace should succeed with sufficient funds");
            Assert.AreEqual(400, _economy.CurrentResources, "100 gold deducted for placement");
            Assert.AreEqual(1, _buildingService.GetInstanceCount("test_farm"));
        }

        [Test]
        public void TryPlace_Fails_InsufficientFunds()
        {
            _economy.AddResources(50); // less than 100 cost
            var result = _buildingService.TryPlace("test_farm", 0, 0);

            Assert.IsFalse(result.Success, "TryPlace should fail with insufficient funds");
            Assert.AreEqual("InsufficientFunds", result.FailReason);
            Assert.AreEqual(0, _buildingService.GetInstanceCount("test_farm"));
            Assert.AreEqual(50, _economy.CurrentResources, "Gold unchanged on failure");
        }

        [Test]
        public void OnTick_AddsProduction_ToEconomy()
        {
            _economy.AddResources(500);
            _buildingService.TryPlace("test_farm", 0, 0);
            long goldAfterPlace = _economy.CurrentResources; // 400

            _buildingService.OnTick(1f); // 1 second tick

            Assert.AreEqual(goldAfterPlace + 10, _economy.CurrentResources,
                "Building produces 10 gold per tick");
        }

        [Test]
        public void SaveLoad_RoundTrip_PreservesPlacedBuildings()
        {
            _economy.AddResources(500);
            _buildingService.TryPlace("test_farm", 0, 0);
            Assert.AreEqual(1, _buildingService.GetInstanceCount("test_farm"));

            // Save
            var sd = new SaveData();
            sd.EnsureDefaults();
            _buildingService.OnBeforeSave(sd);

            // Fresh service load
            var bld2Go    = new GameObject("BuildingService2");
            var bld2      = bld2Go.AddComponent<BuildingService>();
            var eco2Go    = new GameObject("Economy2");
            var eco2      = eco2Go.AddComponent<EconomyService>();
            var sav2Go    = new GameObject("Save2");
            var sav2      = sav2Go.AddComponent<SaveService>();
            eco2.Initialize(null, sav2);
            bld2.Initialize(new[] { _buildingConfig }, eco2);
            bld2.OnAfterLoad(sd);

            Assert.AreEqual(1, bld2.GetInstanceCount("test_farm"),
                "Placed building persisted after save/load");

            Object.DestroyImmediate(bld2Go);
            Object.DestroyImmediate(eco2Go);
            Object.DestroyImmediate(sav2Go);
        }

#endif
    }
}
