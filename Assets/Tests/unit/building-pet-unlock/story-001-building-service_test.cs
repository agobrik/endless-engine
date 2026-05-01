// Tests for Sprint 17 — S17-01: BuildingService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - TryPlace: success, deducts cost, fires OnBuildingPlaced
//   - TryPlace: ConfigNotFound, InsufficientFunds, MaxInstancesReached
//   - TryUpgrade: upgrades tier, deducts cost, fires OnBuildingUpgraded
//   - TryUpgrade: rejects unknown instance, rejects when already max tier
//   - Remove: removes instance, fires OnBuildingRemoved
//   - OnTick: accumulates gold production into EconomyService
//   - CanPlace: returns false when insufficient funds
//   - GetInstanceCount: accurate count per buildingId
//   - Save/Load round-trip: instances persist across OnBeforeSave → OnAfterLoad
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.BuildingPetUnlock

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Building;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.BuildingPetUnlock
{
    [TestFixture]
    public class BuildingServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private BuildingService   _service;
        private EconomyService    _economy;
        private BuildingConfigSO  _mineConfig;
        private BuildingConfigSO  _limitedConfig;

        private readonly List<BuildingInstance>  _placedEvents   = new List<BuildingInstance>();
        private readonly List<BuildingInstance>  _upgradedEvents = new List<BuildingInstance>();
        private readonly List<string>            _removedEvents  = new List<string>();
        private readonly List<(string, string)>  _failedEvents   = new List<(string, string)>();
        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            BuildingService.ClearSubscribersForTesting();
            BuildingService.OnBuildingPlaced   += b => _placedEvents.Add(b);
            BuildingService.OnBuildingUpgraded += b => _upgradedEvents.Add(b);
            BuildingService.OnBuildingRemoved  += id => _removedEvents.Add(id);
            BuildingService.OnPlaceFailed      += (id, r) => _failedEvents.Add((id, r));
            _placedEvents.Clear(); _upgradedEvents.Clear(); _removedEvents.Clear(); _failedEvents.Clear();

            // Economy
            var ecoGo = new GameObject("Economy");
            _economy  = ecoGo.AddComponent<EconomyService>();
            _economy.Initialize(null, new GameObject("Save").AddComponent<SaveService>());
            var sd = new SaveData(); sd.EnsureDefaults(); sd.CurrentResources = 200;
            _economy.OnAfterLoad(sd);

            // Building configs
            _mineConfig = ScriptableObject.CreateInstance<BuildingConfigSO>();
            _mineConfig.BuildingId        = "gold_mine";
            _mineConfig.PlacementCost     = 50;
            _mineConfig.ProductionPerTick = 10;
            _mineConfig.UpgradeTiers = new[]
            {
                new BuildingUpgradeTier { UpgradeCost = 100, ProductionBonusPerTick = 10, ProductionMultiplier = 1f }
            };

            _limitedConfig = ScriptableObject.CreateInstance<BuildingConfigSO>();
            _limitedConfig.BuildingId    = "rare_mine";
            _limitedConfig.PlacementCost = 10;
            _limitedConfig.MaxInstances  = 1;

            var go = new GameObject("BuildingService");
            _service = go.AddComponent<BuildingService>();
            _service.Initialize(new[] { _mineConfig, _limitedConfig }, _economy);
        }

        [TearDown]
        public void TearDown()
        {
            BuildingService.ClearSubscribersForTesting();
            if (_service      != null) Object.DestroyImmediate(_service.gameObject);
            if (_economy      != null) Object.DestroyImmediate(_economy.gameObject);
            if (_mineConfig   != null) Object.DestroyImmediate(_mineConfig);
            if (_limitedConfig != null) Object.DestroyImmediate(_limitedConfig);
            if (_econConfig   != null) Object.DestroyImmediate(_econConfig);
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        // ── TryPlace ──────────────────────────────────────────────────────────────

        [Test]
        public void TryPlace_Success_DeductsCostAndFiresEvent()
        {
            var result = _service.TryPlace("gold_mine", 0, 0);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(150, _economy.CurrentResources, "50 deducted");
            Assert.AreEqual(1, _placedEvents.Count);
        }

        [Test]
        public void TryPlace_ConfigNotFound_Fails()
        {
            var result = _service.TryPlace("unknown", 0, 0);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("ConfigNotFound", result.FailReason);
        }

        [Test]
        public void TryPlace_InsufficientFunds_Fails()
        {
            // Set resources too low
            var sd = new SaveData(); sd.EnsureDefaults(); sd.CurrentResources = 10;
            _economy.OnAfterLoad(sd);

            var result = _service.TryPlace("gold_mine", 0, 0);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("InsufficientFunds", result.FailReason);
        }

        [Test]
        public void TryPlace_MaxInstancesReached_Fails()
        {
            _service.TryPlace("rare_mine", 0, 0); // 1st — OK
            var result = _service.TryPlace("rare_mine", 1, 0); // 2nd — should fail
            Assert.IsFalse(result.Success);
            Assert.AreEqual("MaxInstancesReached", result.FailReason);
        }

        // ── TryUpgrade ────────────────────────────────────────────────────────────

        [Test]
        public void TryUpgrade_IncreasesTier_DeductsCost()
        {
            var placed = _service.TryPlace("gold_mine", 0, 0);
            string instanceId = placed.Instance.InstanceId;

            bool upgraded = _service.TryUpgrade(instanceId);

            Assert.IsTrue(upgraded);
            Assert.AreEqual(1, placed.Instance.UpgradeTier);
            Assert.AreEqual(1, _upgradedEvents.Count);
            Assert.AreEqual(50, _economy.CurrentResources, "200 - 50 (place) - 100 (upgrade) = 50");
        }

        [Test]
        public void TryUpgrade_ReturnsFalse_WhenAlreadyMaxTier()
        {
            var placed = _service.TryPlace("gold_mine", 0, 0);
            _service.TryUpgrade(placed.Instance.InstanceId); // tier 0 → 1 (only 1 tier exists)
            bool result = _service.TryUpgrade(placed.Instance.InstanceId); // already at max
            Assert.IsFalse(result);
        }

        [Test]
        public void TryUpgrade_ReturnsFalse_ForUnknownInstance()
        {
            Assert.IsFalse(_service.TryUpgrade("fake-instance-id"));
        }

        // ── Remove ────────────────────────────────────────────────────────────────

        [Test]
        public void Remove_RemovesInstance_FiresEvent()
        {
            var placed = _service.TryPlace("gold_mine", 0, 0);
            bool removed = _service.Remove(placed.Instance.InstanceId);
            Assert.IsTrue(removed);
            Assert.AreEqual(1, _removedEvents.Count);
            Assert.AreEqual(0, _service.GetInstanceCount("gold_mine"));
        }

        [Test]
        public void Remove_ReturnsFalse_ForUnknownInstance()
        {
            Assert.IsFalse(_service.Remove("nonexistent"));
        }

        // ── OnTick Production ─────────────────────────────────────────────────────

        [Test]
        public void OnTick_AccumulatesGoldProduction()
        {
            _service.TryPlace("gold_mine", 0, 0); // 10 gold/tick
            _service.TryPlace("gold_mine", 1, 0); // 10 gold/tick
            double before = _economy.CurrentResources; // 200 - 100 = 100

            _service.OnTick(1f);

            Assert.AreEqual(before + 20, _economy.CurrentResources, "2 mines × 10 = 20 per tick");
        }

        // ── CanPlace ─────────────────────────────────────────────────────────────

        [Test]
        public void CanPlace_FalseWhenInsufficientFunds()
        {
            var sd = new SaveData(); sd.EnsureDefaults(); sd.CurrentResources = 10;
            _economy.OnAfterLoad(sd);
            Assert.IsFalse(_service.CanPlace("gold_mine"));
        }

        // ── GetInstanceCount ──────────────────────────────────────────────────────

        [Test]
        public void GetInstanceCount_ReturnsCorrectCount()
        {
            _service.TryPlace("gold_mine", 0, 0);
            _service.TryPlace("gold_mine", 1, 0);
            Assert.AreEqual(2, _service.GetInstanceCount("gold_mine"));
        }

        // ── Save/Load round-trip ──────────────────────────────────────────────────

        [Test]
        public void SaveLoad_RoundTrip_RestoresInstances()
        {
            _service.TryPlace("gold_mine", 3, 5);

            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _service.OnBeforeSave(saveData);

            // New service instance (simulates game restart)
            var go2      = new GameObject("BuildingService2");
            var service2 = go2.AddComponent<BuildingService>();
            service2.Initialize(new[] { _mineConfig, _limitedConfig }, _economy);
            service2.OnAfterLoad(saveData);

            Assert.AreEqual(1, service2.GetInstanceCount("gold_mine"));

            Object.DestroyImmediate(go2);
        }

#endif
    }
}
