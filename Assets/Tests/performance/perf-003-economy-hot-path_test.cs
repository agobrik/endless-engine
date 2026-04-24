// Performance Tests — Sprint 23 — S23-06
// Scenario 3: EconomyService.AddResources hot path under rapid-fire load.
// Simulates click-spam and passive income accumulation at 60fps.
// 10,000 AddResources calls must complete under 5ms.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Performance

using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Performance
{
    [TestFixture]
    [Category("Performance")]
    public class EconomyHotPathTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private EconomyService _economy;
        private SaveService    _save;
        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = UnityEngine.ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            var ecoGo = new GameObject("Economy");
            _economy  = ecoGo.AddComponent<EconomyService>();
            var savGo = new GameObject("Save");
            _save     = savGo.AddComponent<SaveService>();
            _economy.Initialize(null, _save);

            var sd = new SaveData();
            sd.EnsureDefaults();
            _economy.OnAfterLoad(sd);
        }

        [TearDown]
        public void TearDown()
        {
            if (_economy    != null) UnityEngine.Object.DestroyImmediate(_economy.gameObject);
            if (_save       != null) UnityEngine.Object.DestroyImmediate(_save.gameObject);
            if (_econConfig != null) UnityEngine.Object.DestroyImmediate(_econConfig);
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        /// <summary>
        /// AddResources is called every tick for passive income AND every click.
        /// 10,000 calls represent ~167 seconds of 60fps passive income ticks,
        /// or a heavy click-spam session. Must complete under 5ms (zero-allocation hot path).
        /// </summary>
        [Test]
        public void AddResources_10000Calls_CompletesUnder5ms()
        {
            const int    iterations = 10_000;
            const double budgetMs   = 5.0;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
                _economy.AddResources(1);
            sw.Stop();

            Assert.AreEqual(iterations, _economy.CurrentResources,
                "All resources must be accumulated correctly");
            Assert.Less(sw.Elapsed.TotalMilliseconds, budgetMs,
                $"AddResources × {iterations} took {sw.Elapsed.TotalMilliseconds:F2}ms — exceeds {budgetMs}ms budget");

            UnityEngine.Debug.Log($"[Perf] EconomyService.AddResources × {iterations}: {sw.Elapsed.TotalMilliseconds:F3}ms");
        }

        /// <summary>
        /// Large single-batch additions (offline income catch-up) should also be instant.
        /// Offline income for 8 hours at 1M gold/sec = 28.8 billion in one call.
        /// </summary>
        [Test]
        public void AddResources_LargeBatch_CompletesUnder1ms()
        {
            const long   offlineAmount = 28_800_000_000L;
            const double budgetMs      = 1.0;

            // Pre-seed enough capacity (hard cap check)
            var sw = Stopwatch.StartNew();
            _economy.AddResources(offlineAmount);
            sw.Stop();

            // Result will be capped at ResourceHardCap — that's expected behavior
            Assert.GreaterOrEqual(_economy.CurrentResources, 0,
                "Resources must not go negative");
            Assert.Less(sw.Elapsed.TotalMilliseconds, budgetMs,
                $"Large AddResources took {sw.Elapsed.TotalMilliseconds:F2}ms — exceeds {budgetMs}ms budget");

            UnityEngine.Debug.Log($"[Perf] EconomyService.AddResources (offline batch {offlineAmount:N0}): {sw.Elapsed.TotalMilliseconds:F3}ms → balance {_economy.CurrentResources:N0}");
        }

        /// <summary>
        /// DeductResources under upgrade-purchase spam.
        /// 1000 purchases in sequence (e.g. auto-buy script) must complete under 2ms.
        /// </summary>
        [Test]
        public void DeductResources_1000Calls_CompletesUnder2ms()
        {
            const int    purchases = 1000;
            const double budgetMs  = 2.0;

            _economy.AddResources(purchases * 10); // fund all purchases

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < purchases; i++)
                _economy.DeductResources(10);
            sw.Stop();

            Assert.AreEqual(0, _economy.CurrentResources, "Balance should reach exactly 0");
            Assert.Less(sw.Elapsed.TotalMilliseconds, budgetMs,
                $"DeductResources × {purchases} took {sw.Elapsed.TotalMilliseconds:F2}ms — exceeds {budgetMs}ms budget");

            UnityEngine.Debug.Log($"[Perf] EconomyService.DeductResources × {purchases}: {sw.Elapsed.TotalMilliseconds:F3}ms");
        }

#endif
    }
}
