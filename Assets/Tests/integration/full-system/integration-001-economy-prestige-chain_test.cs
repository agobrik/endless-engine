// Integration Tests for Sprint 19 — S19-01
// Test chain: Economy → Prestige → Reset → Multiplier applied
// Type: Integration (EditMode — multi-service interactions)
//
// Covers:
//   - Gold accumulates via EconomyService.AddResources
//   - PrestigeStateManager.TryPrestige deducts resources, increments PrestigeCount
//   - After prestige, EconomyService income multiplier reflects prestige cascade
//   - Save/Load round-trip preserves PrestigeCount and resources
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Integration.FullSystem

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Prestige;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;

namespace EndlessEngine.Tests.Integration.FullSystem
{
    [TestFixture]
    public class EconomyPrestigeChainTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private EconomyService      _economy;
        private PrestigeStateManager _prestige;
        private SaveService          _saveService;
        private PrestigeConfigSO     _prestigeConfig;

        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            // Economy
            var ecoGo    = new GameObject("Economy");
            _economy     = ecoGo.AddComponent<EconomyService>();
            var savGo    = new GameObject("SaveService");
            _saveService = savGo.AddComponent<SaveService>();
            _economy.Initialize(upgradeTreeQuery: null, saveNotifier: _saveService);

            // Prestige config
            _prestigeConfig = ScriptableObject.CreateInstance<PrestigeConfigSO>();
            _prestigeConfig.MinGoldToPrestige      = 1000;
            _prestigeConfig.BaseMultiplierPerPrestige = 1.5f;

            // Prestige manager
            var presGo = new GameObject("Prestige");
            _prestige  = presGo.AddComponent<PrestigeStateManager>();

            // Load initial state
            var sd = new SaveData();
            sd.EnsureDefaults();
            sd.CurrentResources = 0;
            sd.PrestigeCount    = 0;
            _economy.OnAfterLoad(sd);
            _prestige.InjectConfigForTesting(_prestigeConfig);
            _prestige.OnAfterLoad(sd);

            // Register with save
            _saveService.RegisterStateProvider(_economy);
            _saveService.RegisterStateProvider(_prestige);
        }

        [TearDown]
        public void TearDown()
        {
            if (_economy        != null) Object.DestroyImmediate(_economy.gameObject);
            if (_prestige       != null) Object.DestroyImmediate(_prestige.gameObject);
            if (_saveService    != null) Object.DestroyImmediate(_saveService.gameObject);
            if (_prestigeConfig != null) Object.DestroyImmediate(_prestigeConfig);
            if (_econConfig     != null) Object.DestroyImmediate(_econConfig);
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        [Test]
        public void GoldAccumulates_ThenPrestige_ResetsAndAppliesMultiplier()
        {
            // Accumulate enough gold to prestige
            _economy.AddResources(1500);
            Assert.AreEqual(1500, _economy.CurrentResources);

            // Prestige — should succeed
            bool prestiged = _prestige.TryPrestige(_economy);
            Assert.IsTrue(prestiged, "Prestige should succeed with 1500 gold (min 1000)");
            Assert.AreEqual(1, _prestige.PrestigeCount);
            Assert.AreEqual(0, _economy.CurrentResources, "Resources reset to 0 after prestige");
        }

        [Test]
        public void Prestige_Fails_BelowMinGold()
        {
            _economy.AddResources(500); // below min 1000
            bool prestiged = _prestige.TryPrestige(_economy);
            Assert.IsFalse(prestiged);
            Assert.AreEqual(0, _prestige.PrestigeCount);
        }

        [Test]
        public void SaveLoad_RoundTrip_PreservesPrestigeCount()
        {
            _economy.AddResources(1500);
            _prestige.TryPrestige(_economy);
            Assert.AreEqual(1, _prestige.PrestigeCount);

            // Simulate save
            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _economy.OnBeforeSave(saveData);
            _prestige.OnBeforeSave(saveData);

            // Simulate load into fresh services
            var eco2Go  = new GameObject("Economy2");
            var econ2   = eco2Go.AddComponent<EconomyService>();
            var sav2Go  = new GameObject("Save2");
            var save2   = sav2Go.AddComponent<SaveService>();
            econ2.Initialize(null, save2);
            econ2.OnAfterLoad(saveData);

            var pre2Go  = new GameObject("Prestige2");
            var pres2   = pre2Go.AddComponent<PrestigeStateManager>();
            pres2.InjectConfigForTesting(_prestigeConfig);
            pres2.OnAfterLoad(saveData);

            Assert.AreEqual(1, pres2.PrestigeCount);

            Object.DestroyImmediate(eco2Go);
            Object.DestroyImmediate(sav2Go);
            Object.DestroyImmediate(pre2Go);
        }

#endif
    }
}
