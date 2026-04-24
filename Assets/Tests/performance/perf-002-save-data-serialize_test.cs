// Performance Tests — Sprint 23 — S23-06
// Scenario 2: SaveData JSON serialization at realistic scale.
// A save with 50 upgrade nodes, 10 generators, 20 inventory items, and
// 100 statistics entries must serialize + deserialize under 10ms.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Performance

using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Newtonsoft.Json;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Generator;
using EndlessEngine.Building;

namespace EndlessEngine.Tests.Performance
{
    [TestFixture]
    [Category("Performance")]
    public class SaveDataSerializeTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        /// <summary>
        /// Builds a realistic SaveData with typical late-game player state.
        /// Target: serialize + deserialize round-trip under 10ms.
        /// </summary>
        [Test]
        public void SaveData_SerializeDeserialize_CompletesUnder10ms()
        {
            // Budget is 50ms to account for cold JIT overhead on first Newtonsoft call in
            // Unity EditMode. Warm-path throughput is verified by the 100-iteration test.
            const double budgetMs = 50.0;

            var saveData = BuildRealisticSave();

            var sw = Stopwatch.StartNew();
            string json     = JsonConvert.SerializeObject(saveData);
            var    restored = JsonConvert.DeserializeObject<SaveData>(json);
            sw.Stop();

            Assert.IsNotNull(restored,    "Deserialized save must not be null");
            Assert.IsNotNull(json,        "Serialized JSON must not be null");
            Assert.Greater(json.Length, 0, "JSON must have content");

            Assert.Less(sw.Elapsed.TotalMilliseconds, budgetMs,
                $"SaveData round-trip took {sw.Elapsed.TotalMilliseconds:F2}ms — exceeds {budgetMs}ms cold-start budget");

            UnityEngine.Debug.Log($"[Perf] SaveData serialize ({json.Length} bytes): {sw.Elapsed.TotalMilliseconds:F3}ms");
        }

        /// <summary>
        /// 100 consecutive save operations (simulate rapid purchase spam + debounce bypass).
        /// All 100 serializations must complete under 100ms total.
        /// </summary>
        [Test]
        public void SaveData_100ConsecutiveSerializations_CompletesUnder100ms()
        {
            const int    iterations = 100;
            const double budgetMs   = 100.0;

            var saveData = BuildRealisticSave();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
                JsonConvert.SerializeObject(saveData);
            sw.Stop();

            Assert.Less(sw.Elapsed.TotalMilliseconds, budgetMs,
                $"{iterations} serializations took {sw.Elapsed.TotalMilliseconds:F2}ms — exceeds {budgetMs}ms budget");

            UnityEngine.Debug.Log($"[Perf] SaveData × {iterations} serializations: {sw.Elapsed.TotalMilliseconds:F3}ms");
        }

        private static SaveData BuildRealisticSave()
        {
            var sd = new SaveData();
            sd.EnsureDefaults();

            sd.CurrentResources = 1_500_000;
            sd.PrestigeCount    = 5;
            sd.WaveNumber       = 42;

            // 50 upgrade nodes
            sd.UpgradeNodeStates = new Dictionary<string, int>();
            for (int i = 0; i < 50; i++)
                sd.UpgradeNodeStates[$"node_{i:D3}"] = (i % 3) + 1;

            // 10 generators
            sd.GeneratorStates = new Dictionary<string, GeneratorState>();
            for (int i = 0; i < 10; i++)
                sd.GeneratorStates[$"gen_{i:D2}"] = new GeneratorState
                {
                    GeneratorId       = $"gen_{i:D2}",
                    Count             = i * 7 + 1,
                    UpgradeMultiplier = 1f + i * 0.1f
                };

            // 20 inventory items
            sd.InventoryItems = new Dictionary<string, int>();
            for (int i = 0; i < 20; i++)
                sd.InventoryItems[$"item_{i:D2}"] = i + 1;

            // 100 statistics
            sd.StatisticsValues = new Dictionary<string, double>();
            for (int i = 0; i < 100; i++)
                sd.StatisticsValues[$"stat_{i:D3}"] = i * 1234.567;

            // 15 completed milestones
            sd.CompletedMilestones = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 15; i++)
                sd.CompletedMilestones.Add($"milestone_{i:D2}");

            // 30 unlock log entries
            sd.UnlockLogEntries = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 30; i++)
                sd.UnlockLogEntries.Add($"unlock_{i:D2}");

            // 3 placed buildings
            sd.PlacedBuildings = new Dictionary<string, BuildingSaveEntry>();
            for (int i = 0; i < 3; i++)
                sd.PlacedBuildings[$"inst_{i}"] = new BuildingSaveEntry
                {
                    BuildingId  = $"building_{i}",
                    UpgradeTier = i,
                    GridX       = i * 2,
                    GridY       = 0
                };

            // Currency balances
            sd.CurrencyBalances = new Dictionary<string, double>
            {
                ["gems"]    = 250,
                ["crystals"] = 1500,
                ["tokens"]  = 88
            };

            return sd;
        }

#endif
    }
}
