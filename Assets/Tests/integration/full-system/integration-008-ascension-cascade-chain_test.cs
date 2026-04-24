// Integration Tests — Sprint 23 — S23-08
// Test chain: AscensionStateManager with multi-layer database → GetCascadeMultiplier
// returns product of layer multipliers; InjectCountForTesting drives layer counts;
// save/load round-trip preserves AscensionCounts dictionary.
//
// Design note: TryTrigger(layer, wave) is async (two-save crash-safety pattern —
// ADR-0010). Tests use InjectCountForTesting to set counts synchronously and verify
// GetCascadeMultiplier in isolation. The save/load round-trip tests OnBeforeSave /
// OnAfterLoad directly without triggering the async prestige sequence.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Integration.FullSystem

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Prestige;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Integration.FullSystem
{
    [TestFixture]
    public class AscensionCascadeChainTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private AscensionStateManager  _ascension;
        private AscensionDatabaseSO    _database;
        private PrestigeLayerConfigSO  _layer1;
        private PrestigeLayerConfigSO  _layer2;

        [SetUp]
        public void SetUp()
        {
            // Layer 1: 1.5× per trigger (no cap)
            _layer1 = ScriptableObject.CreateInstance<PrestigeLayerConfigSO>();
            _layer1.LayerIndex               = 1;
            _layer1.DisplayName              = "Ascend";
            _layer1.MinWaveRequired          = 1;
            _layer1.RequiredPreviousLayerCount = 0;
            _layer1.MaxCount                 = 0;
            _layer1.BaseMultiplierPerTrigger = 1.5f;
            _layer1.MaxPermanentMultiplier   = 0f;  // unlimited

            // Layer 2: 2.0× per trigger (no cap)
            _layer2 = ScriptableObject.CreateInstance<PrestigeLayerConfigSO>();
            _layer2.LayerIndex               = 2;
            _layer2.DisplayName              = "Transcend";
            _layer2.MinWaveRequired          = 1;
            _layer2.RequiredPreviousLayerCount = 0;
            _layer2.MaxCount                 = 0;
            _layer2.BaseMultiplierPerTrigger = 2.0f;
            _layer2.MaxPermanentMultiplier   = 0f;

            _database = ScriptableObject.CreateInstance<AscensionDatabaseSO>();
            _database.Layers = new[] { _layer1, _layer2 };

            var go     = new GameObject("Ascension");
            _ascension = go.AddComponent<AscensionStateManager>();

            // Initialize with null PrestigeStateManager — GetCascadeMultiplier
            // uses _prestigeManager?.GetPermanentMultiplier() ?? 1f when null.
            _ascension.Initialize(
                database:         _database,
                prestigeManager:  null,
                saveService:      null,
                economyService:   null);

            _ascension.SetInitializedForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            if (_ascension != null) Object.DestroyImmediate(_ascension.gameObject);
            if (_database  != null) Object.DestroyImmediate(_database);
            if (_layer1    != null) Object.DestroyImmediate(_layer1);
            if (_layer2    != null) Object.DestroyImmediate(_layer2);

            AscensionStateManager.ClearSubscribersForTesting();
        }

        [Test]
        public void GetCascadeMultiplier_NoTriggers_ReturnsOne()
        {
            float mult = _ascension.GetCascadeMultiplier();
            Assert.AreEqual(1f, mult, 0.001f,
                "Cascade multiplier with zero layer counts should be 1");
        }

        [Test]
        public void GetCascadeMultiplier_Layer1Count2_ReturnsCorrectProduct()
        {
            // Layer 1: 1.5^2 = 2.25; Layer 2: 2.0^0 = 1.0 → total = 2.25
            _ascension.InjectCountForTesting(1, 2);

            float expected = Mathf.Pow(1.5f, 2); // 2.25
            float actual   = _ascension.GetCascadeMultiplier();

            Assert.AreEqual(expected, actual, 0.001f,
                "Layer 1 × 2 at 1.5× per trigger should give 2.25");
        }

        [Test]
        public void GetCascadeMultiplier_BothLayersTriggered_ReturnsProductOfBothLayers()
        {
            // Layer 1: 1.5^2 = 2.25; Layer 2: 2.0^1 = 2.0 → total = 4.5
            _ascension.InjectCountForTesting(1, 2);
            _ascension.InjectCountForTesting(2, 1);

            float layer1Mult = Mathf.Pow(1.5f, 2);  // 2.25
            float layer2Mult = Mathf.Pow(2.0f, 1);  // 2.0
            float expected   = layer1Mult * layer2Mult; // 4.5

            float actual = _ascension.GetCascadeMultiplier();
            Assert.AreEqual(expected, actual, 0.001f,
                "Cascade multiplier should be product of all layer multipliers");
        }

        [Test]
        public void SaveLoad_RoundTrip_PreservesAscensionCounts()
        {
            _ascension.InjectCountForTesting(1, 3);
            _ascension.InjectCountForTesting(2, 1);

            // Save
            var sd = new SaveData();
            sd.EnsureDefaults();
            _ascension.OnBeforeSave(sd);

            Assert.IsNotNull(sd.AscensionCounts, "OnBeforeSave must populate AscensionCounts");
            Assert.AreEqual(3, sd.AscensionCounts["1"], "Layer 1 count must be serialized");
            Assert.AreEqual(1, sd.AscensionCounts["2"], "Layer 2 count must be serialized");

            // Fresh instance loads from the same SaveData
            var go2  = new GameObject("Ascension2");
            var asc2 = go2.AddComponent<AscensionStateManager>();
            asc2.Initialize(_database, null, null, null);
            asc2.SetInitializedForTesting();
            asc2.OnAfterLoad(sd);

            Assert.AreEqual(3, asc2.GetCount(1), "Layer 1 count must persist after save/load");
            Assert.AreEqual(1, asc2.GetCount(2), "Layer 2 count must persist after save/load");

            // Cascade multiplier is also correct on the restored instance
            float expected = Mathf.Pow(1.5f, 3) * Mathf.Pow(2.0f, 1);
            Assert.AreEqual(expected, asc2.GetCascadeMultiplier(), 0.001f,
                "Cascade multiplier computed from restored counts must match expectation");

            Object.DestroyImmediate(go2);
        }

#endif
    }
}
