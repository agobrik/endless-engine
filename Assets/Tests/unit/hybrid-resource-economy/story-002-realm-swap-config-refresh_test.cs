// Tests for Sprint 6 — S6-05: EconomyService.RefreshConfigCache on realm swap
// Type: Logic (Unit/EditMode)
//
// Verifies that EconomyService subscribes to ConfigRegistry.OnRealmSwapped and
// refreshes _resourceHardCap and _startingGold when the realm changes.
// Also verifies that CurrentResources is clamped if the new realm has a lower cap.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.HybridResourceEconomy

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.HybridResourceEconomy
{
    /// <summary>
    /// Unit tests for EconomyService realm-swap config refresh — Sprint 6 S6-05.
    /// </summary>
    [TestFixture]
    public class RealmSwapConfigRefreshTests
    {
        private EconomyService _economy;
        private EconomyConfigSO _initialConfig;
        private EconomyConfigSO _newRealmConfig;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _initialConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            _initialConfig.ResourceHardCap = 1_000_000L;
            _initialConfig.StartingGold    = 10L;

            ConfigRegistry.InjectForTesting(economy: _initialConfig);

            var go = new GameObject("EconomyRealmSwapTest");
            _economy = go.AddComponent<EconomyService>();
            _economy.Initialize(upgradeTreeQuery: null, saveNotifier: null);
            _economy.SubscribeForTesting(); // OnEnable doesn't fire in EditMode tests

            // Load save so economy is initialized
            var saveData = new SaveData { CurrentResources = 50_000L };
            _economy.OnAfterLoad(saveData);

            // Prepare new realm config
            _newRealmConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_economy != null)
            {
                _economy.UnsubscribeForTesting();
                Object.DestroyImmediate(_economy.gameObject);
            }

            if (_initialConfig != null)
                Object.DestroyImmediate(_initialConfig);
            if (_newRealmConfig != null)
                Object.DestroyImmediate(_newRealmConfig);

            ConfigRegistry.ClearForTesting();
#endif
        }

        // ── Config refresh on swap ────────────────────────────────────────────────

        [Test]
        [Description("After realm swap, new hard cap is applied — AddResources respects new cap.")]
        public void RealmSwap_NewHardCap_IsRespected()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _newRealmConfig.ResourceHardCap = 100_000L; // lower than current balance (50_000 < 1_000_000)
            _newRealmConfig.StartingGold    = 20L;
            ConfigRegistry.InjectForTesting(economy: _newRealmConfig);

            // Simulate realm swap firing OnRealmSwapped
            ConfigRegistry.FireRealmSwappedForTesting();

            // AddResources above new cap should be truncated
            _economy.AddResources(60_000L); // would exceed 100_000 cap from 50_000 baseline
            Assert.AreEqual(100_000L, _economy.CurrentResources,
                "Resources must be capped at new realm hard cap after swap");
#endif
        }

        [Test]
        [Description("After realm swap, if current resources exceed new cap, they are clamped.")]
        public void RealmSwap_CurrentResourcesExceedNewCap_AresClamped()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // First give the player a lot of gold
            _economy.InjectStateForTesting(currentResources: 900_000L, hardCap: 1_000_000L, startingGold: 10L);

            // New realm has much lower cap
            _newRealmConfig.ResourceHardCap = 500_000L;
            _newRealmConfig.StartingGold    = 10L;
            ConfigRegistry.InjectForTesting(economy: _newRealmConfig);

            ConfigRegistry.FireRealmSwappedForTesting();

            Assert.AreEqual(500_000L, _economy.CurrentResources,
                "Resources must be clamped to new realm hard cap if they exceed it");
#endif
        }

        [Test]
        [Description("After realm swap, if current resources are below new cap, they are unchanged.")]
        public void RealmSwap_CurrentResourcesBelowNewCap_Unchanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Balance is 50_000 (from SetUp)
            _newRealmConfig.ResourceHardCap = 2_000_000L; // much higher cap
            _newRealmConfig.StartingGold    = 10L;
            ConfigRegistry.InjectForTesting(economy: _newRealmConfig);

            ConfigRegistry.FireRealmSwappedForTesting();

            Assert.AreEqual(50_000L, _economy.CurrentResources,
                "Resources below new cap must not be changed");
#endif
        }

        [Test]
        [Description("OnResourcesChanged fires when realm swap clamps resources.")]
        public void RealmSwap_ClampsResources_FiresOnResourcesChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(currentResources: 900_000L, hardCap: 1_000_000L, startingGold: 10L);

            bool eventFired = false;
            long reportedCurrent = 0;
            System.Action<long, long> handler = (current, delta) =>
            {
                eventFired      = true;
                reportedCurrent = current;
            };
            EconomyService.OnResourcesChanged += handler;

            _newRealmConfig.ResourceHardCap = 500_000L;
            _newRealmConfig.StartingGold    = 10L;
            ConfigRegistry.InjectForTesting(economy: _newRealmConfig);
            ConfigRegistry.FireRealmSwappedForTesting();

            EconomyService.OnResourcesChanged -= handler;

            Assert.IsTrue(eventFired, "OnResourcesChanged must fire when cap clamps resources");
            Assert.AreEqual(500_000L, reportedCurrent,
                "OnResourcesChanged must report the clamped balance");
#endif
        }
    }
}
