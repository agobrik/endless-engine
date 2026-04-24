// Performance Tests — Sprint 23 — S23-06
// Scenario 1: TickEngine under subscriber load.
// Verifies that 1000 OnTick subscribers complete within 16ms budget.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Performance

using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace EndlessEngine.Tests.Performance
{
    [TestFixture]
    [Category("Performance")]
    public class TickEngineLoadTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private EndlessEngine.Flow.TickEngine _tickEngine;

        [SetUp]
        public void SetUp()
        {
            var go      = new GameObject("TickEngine");
            _tickEngine = go.AddComponent<EndlessEngine.Flow.TickEngine>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_tickEngine != null) UnityEngine.Object.DestroyImmediate(_tickEngine.gameObject);
            EndlessEngine.Flow.TickEngine.ClearSubscribersForTesting();
        }

        /// <summary>
        /// 1000 tick subscribers should all fire within a single 16.6ms frame budget.
        /// </summary>
        [Test]
        public void FireTick_With1000Subscribers_CompletesUnder16ms()
        {
            const int subscriberCount  = 1000;
            const double budgetMs      = 16.0;

            int callCount = 0;
            Action<float> handler = _ => callCount++;

            for (int i = 0; i < subscriberCount; i++)
                EndlessEngine.Flow.TickEngine.OnTick += handler;

            var sw = Stopwatch.StartNew();
            EndlessEngine.Flow.TickEngine.FireTickForTesting(1f);
            sw.Stop();

            Assert.AreEqual(subscriberCount, callCount,
                "All subscribers must be called");
            Assert.Less(sw.Elapsed.TotalMilliseconds, budgetMs,
                $"Tick dispatch with {subscriberCount} subscribers took {sw.Elapsed.TotalMilliseconds:F2}ms — exceeds {budgetMs}ms frame budget");

            UnityEngine.Debug.Log($"[Perf] TickEngine × {subscriberCount} subscribers: {sw.Elapsed.TotalMilliseconds:F3}ms");
        }

        /// <summary>
        /// 1000 ticks fired in a loop must complete under 50ms.
        /// </summary>
        [Test]
        public void FireTick_1000SuccessiveTicks_CompletesUnder50ms()
        {
            const int tickCount    = 1000;
            const double budgetMs  = 50.0;

            int totalCalls = 0;
            EndlessEngine.Flow.TickEngine.OnTick += _ => totalCalls++;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < tickCount; i++)
                EndlessEngine.Flow.TickEngine.FireTickForTesting(1f);
            sw.Stop();

            Assert.AreEqual(tickCount, totalCalls,
                "Each tick fires the subscriber exactly once");
            Assert.Less(sw.Elapsed.TotalMilliseconds, budgetMs,
                $"{tickCount} successive ticks took {sw.Elapsed.TotalMilliseconds:F2}ms — exceeds {budgetMs}ms budget");

            UnityEngine.Debug.Log($"[Perf] TickEngine × {tickCount} ticks: {sw.Elapsed.TotalMilliseconds:F3}ms");
        }

#endif
    }
}
