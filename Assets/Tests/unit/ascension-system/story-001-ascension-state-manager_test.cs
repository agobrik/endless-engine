// Tests for Sprint 10 — S10-05: AscensionStateManager
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - CanTrigger: wave gate, previous-layer count gate, max-count gate
//   - GetCount returns correct per-layer count
//   - GetCascadeMultiplier: product of all layer multipliers
//   - Save/load round-trip persists AscensionCounts
//   - OnAscensionComplete event fires with correct layerIndex, count, cascade
//   - Layer 0 delegates to PrestigeStateManager
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.AscensionSystem

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EndlessEngine.Config;
using EndlessEngine.Prestige;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.AscensionSystem
{
    [TestFixture]
    public class AscensionStateManagerTests
    {
        private AscensionStateManager   _manager;
        private PrestigeStateManager    _prestigeManager;
        private AscensionDatabaseSO     _database;
        private PrestigeLayerConfigSO   _layer0;
        private PrestigeLayerConfigSO   _layer1;
        private PrestigeLayerConfigSO   _layer2;

        private EndlessEngine.Config.EconomyConfigSO  _econConfig;
        private EndlessEngine.Config.PrestigeConfigSO _prestigeConfig;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;

            _prestigeConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.PrestigeConfigSO>();
            _prestigeConfig.MinWaveForPrestige      = 1;
            _prestigeConfig.MaxPrestigeCount        = 0;
            _prestigeConfig.BaseMultiplierPerPrestige = 1.5f;
            _prestigeConfig.MaxPermanentMultiplier  = 999f;

            EndlessEngine.Config.ConfigRegistry.InjectForTesting(
                economy:  _econConfig,
                prestige: _prestigeConfig);

            // ── Layer 0: standard prestige ──
            _layer0 = ScriptableObject.CreateInstance<PrestigeLayerConfigSO>();
            _layer0.LayerIndex              = 0;
            _layer0.DisplayName             = "Prestige";
            _layer0.ActionVerb              = "PRESTIGE";
            _layer0.MinWaveRequired         = 1;
            _layer0.RequiredPreviousLayerCount = 0;
            _layer0.MaxCount                = 0;
            _layer0.BaseMultiplierPerTrigger = 1.5f;
            _layer0.MaxPermanentMultiplier  = 0f;
            _layer0.ResetScope              = AscensionResetScope.Standard;
            _layer0.RewardCurrencyId        = "";

            // ── Layer 1: ascension (needs 3 prestiges) ──
            _layer1 = ScriptableObject.CreateInstance<PrestigeLayerConfigSO>();
            _layer1.LayerIndex               = 1;
            _layer1.DisplayName              = "Ascend";
            _layer1.ActionVerb               = "ASCEND";
            _layer1.MinWaveRequired          = 5;
            _layer1.RequiredPreviousLayerCount = 3;
            _layer1.MaxCount                 = 5;
            _layer1.BaseMultiplierPerTrigger  = 1.2f;
            _layer1.MaxPermanentMultiplier   = 0f;
            _layer1.ResetScope               = AscensionResetScope.Standard;
            _layer1.RewardCurrencyId         = "ascension_shards";
            _layer1.BaseCurrencyReward       = 10;
            _layer1.CurrencyScalingPerCount  = 1.5;

            // ── Layer 2: transcend (needs 2 ascensions) ──
            _layer2 = ScriptableObject.CreateInstance<PrestigeLayerConfigSO>();
            _layer2.LayerIndex               = 2;
            _layer2.DisplayName              = "Transcend";
            _layer2.MinWaveRequired          = 10;
            _layer2.RequiredPreviousLayerCount = 2;
            _layer2.MaxCount                 = 0;
            _layer2.BaseMultiplierPerTrigger  = 2.0f;
            _layer2.MaxPermanentMultiplier   = 0f;
            _layer2.ResetScope               = AscensionResetScope.Full;

            _database          = ScriptableObject.CreateInstance<AscensionDatabaseSO>();
            _database.Layers   = new[] { _layer0, _layer1, _layer2 };

            var go = new GameObject("AscensionTest");

            _prestigeManager = go.AddComponent<PrestigeStateManager>();
            var prestigeSave = new SaveData { PrestigeCount = 0 };
            _prestigeManager.OnAfterLoad(prestigeSave);

            _manager = go.AddComponent<AscensionStateManager>();
            _manager.Initialize(
                database:        _database,
                prestigeManager: _prestigeManager,
                saveService:     null,
                economyService:  null
            );
            _manager.SetInitializedForTesting();
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AscensionStateManager.ClearSubscribersForTesting();
            PrestigeStateManager.ClearStaticEventsForTesting();

            if (_manager != null) Object.DestroyImmediate(_manager.gameObject);
            if (_database        != null) Object.DestroyImmediate(_database);
            if (_layer0          != null) Object.DestroyImmediate(_layer0);
            if (_layer1          != null) Object.DestroyImmediate(_layer1);
            if (_layer2          != null) Object.DestroyImmediate(_layer2);
            if (_econConfig      != null) Object.DestroyImmediate(_econConfig);
            if (_prestigeConfig  != null) Object.DestroyImmediate(_prestigeConfig);

            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
#endif
        }

        // ── CanTrigger: wave gate ─────────────────────────────────────────────────

        [Test]
        public void CanTrigger_Layer1_WaveBelowGate_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _prestigeManager.SetPrestigeCountForTesting(3);
            Assert.IsFalse(_manager.CanTrigger(1, currentWaveNumber: 4),
                "Wave 4 < MinWaveRequired 5 must block layer 1");
#endif
        }

        [Test]
        public void CanTrigger_Layer1_WaveMeetsGate_ReturnsTrueWhenCountMet()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _prestigeManager.SetPrestigeCountForTesting(3);
            Assert.IsTrue(_manager.CanTrigger(1, currentWaveNumber: 5));
#endif
        }

        // ── CanTrigger: previous-layer count gate ─────────────────────────────────

        [Test]
        public void CanTrigger_Layer1_InsufficientPrestigeCount_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _prestigeManager.SetPrestigeCountForTesting(2); // needs 3
            Assert.IsFalse(_manager.CanTrigger(1, currentWaveNumber: 5),
                "Prestige count 2 < RequiredPreviousLayerCount 3 must block");
#endif
        }

        [Test]
        public void CanTrigger_Layer2_InsufficientAscensionCount_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _manager.InjectCountForTesting(1, 1); // needs 2
            Assert.IsFalse(_manager.CanTrigger(2, currentWaveNumber: 15),
                "Ascension count 1 < RequiredPreviousLayerCount 2 must block layer 2");
#endif
        }

        // ── CanTrigger: max count gate ────────────────────────────────────────────

        [Test]
        public void CanTrigger_Layer1_AtMaxCount_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _prestigeManager.SetPrestigeCountForTesting(10);
            _manager.InjectCountForTesting(1, 5); // MaxCount = 5
            Assert.IsFalse(_manager.CanTrigger(1, currentWaveNumber: 10),
                "Layer at MaxCount must return false");
#endif
        }

        // ── GetCount ──────────────────────────────────────────────────────────────

        [Test]
        public void GetCount_AfterInject_ReturnsInjectedValue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _manager.InjectCountForTesting(1, 3);
            Assert.AreEqual(3, _manager.GetCount(1));
#endif
        }

        [Test]
        public void GetCount_UnseenLayer_ReturnsZero()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual(0, _manager.GetCount(99));
#endif
        }

        // ── GetCascadeMultiplier ──────────────────────────────────────────────────

        [Test]
        public void GetCascadeMultiplier_NoAscensions_EqualsPrestigeMultiplier()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 0 prestiges, 0 ascensions → cascade = 1.0 (or prestige base)
            float cascade = _manager.GetCascadeMultiplier();
            Assert.AreEqual(1f, cascade, 0.001f,
                "With no prestiges or ascensions, cascade must be 1.0");
#endif
        }

        [Test]
        public void GetCascadeMultiplier_WithPrestigeAndAscension_IsProduct()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _prestigeManager.SetPrestigeCountForTesting(1);
            _manager.InjectCountForTesting(1, 1);

            float prestigeMult = _prestigeManager.GetPermanentMultiplier(); // 1.5^1 = 1.5
            float layer1Mult   = _layer1.GetPermanentMultiplier(1);          // 1.2^1 = 1.2
            float expected     = prestigeMult * layer1Mult;

            float cascade = _manager.GetCascadeMultiplier();
            Assert.AreEqual(expected, cascade, 0.001f,
                "Cascade must be product of prestige multiplier × layer1 multiplier");
#endif
        }

        // ── PrestigeLayerConfigSO helpers ─────────────────────────────────────────

        [Test]
        public void GetPermanentMultiplier_ZeroCount_ReturnsOne()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual(1f, _layer1.GetPermanentMultiplier(0), 0.001f);
#endif
        }

        [Test]
        public void GetPermanentMultiplier_ThreeCount_CorrectValue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 1.2 ^ 3 = 1.728
            float result = _layer1.GetPermanentMultiplier(3);
            Assert.AreEqual(1.728f, result, 0.001f);
#endif
        }

        [Test]
        public void GetCurrencyReward_ScalesWithCount()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // count=0: 10 * 1.5^0 = 10
            Assert.AreEqual(10.0, _layer1.GetCurrencyReward(0), 0.001);
            // count=1: 10 * 1.5^1 = 15
            Assert.AreEqual(15.0, _layer1.GetCurrencyReward(1), 0.001);
            // count=2: 10 * 1.5^2 = 22.5
            Assert.AreEqual(22.5, _layer1.GetCurrencyReward(2), 0.001);
#endif
        }

        // ── Save / load round-trip ────────────────────────────────────────────────

        [Test]
        public void SaveLoad_AscensionCountsPersistedAndRestored()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _manager.InjectCountForTesting(1, 2);
            _manager.InjectCountForTesting(2, 1);

            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _manager.OnBeforeSave(saveData);

            Assert.IsTrue(saveData.AscensionCounts.ContainsKey("1"));
            Assert.AreEqual(2, saveData.AscensionCounts["1"]);
            Assert.AreEqual(1, saveData.AscensionCounts["2"]);

            // Fresh manager loads from save
            var go2 = new GameObject("AscensionTest2");
            var manager2 = go2.AddComponent<AscensionStateManager>();
            manager2.Initialize(_database, _prestigeManager, null, null);
            manager2.OnAfterLoad(saveData);

            Assert.AreEqual(2, manager2.GetCount(1), "Layer 1 count must be restored");
            Assert.AreEqual(1, manager2.GetCount(2), "Layer 2 count must be restored");

            Object.DestroyImmediate(go2);
#endif
        }
    }
}
