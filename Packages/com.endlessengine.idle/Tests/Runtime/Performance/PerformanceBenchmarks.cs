using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.Generator;
using EndlessEngine.Config;
using EndlessEngine.Stats;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.SaveAndLoad.Migrations;
using EndlessEngine.DI;

namespace EndlessEngine.Tests.Performance
{
    /// <summary>
    /// Performance benchmarks for hot-path engine systems.
    ///
    /// Run via: Unity Test Runner → PlayMode → Performance group.
    /// Requires com.unity.test-framework.performance package.
    ///
    /// Baselines (recorded at v1.2 on reference machine):
    ///   DoubleNumber arithmetic      : &lt; 5 ns per op
    ///   ModifierRegistry.GetMultiplier: &lt; 50 µs for 10 sources
    ///   MigrationPipeline.Apply      : &lt; 1 ms for V1→V2
    ///   SaveSigner.Sign (1 KB JSON)  : &lt; 200 µs
    ///   BulkPurchase.CalculateBulk   : &lt; 10 µs for n=1000
    ///
    /// If a benchmark exceeds 2× its baseline, investigate before merging.
    /// </summary>
    [TestFixture]
    public class PerformanceBenchmarks
    {
        private const int WarmupCount     = 3;
        private const int MeasurementCount = 20;

        // ── DoubleNumber arithmetic ───────────────────────────────────────────────

        [Test, Performance]
        public void DoubleNumber_Addition_HotPath()
        {
            DoubleNumber a = 1_000_000.0;
            DoubleNumber b = 9_999.0;

            Measure.Method(() =>
            {
                for (int i = 0; i < 1000; i++)
                    _ = a + b;
            })
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .GC()
            .Run();
        }

        [Test, Performance]
        public void DoubleNumber_Multiply_HotPath()
        {
            DoubleNumber a = 1e15;

            Measure.Method(() =>
            {
                for (int i = 0; i < 1000; i++)
                    _ = a.Multiply(1.05);
            })
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .GC()
            .Run();
        }

        [Test, Performance]
        public void DoubleNumber_Format_Letter_HotPath()
        {
            DoubleNumber v = 1.234e12;

            Measure.Method(() =>
            {
                for (int i = 0; i < 100; i++)
                    _ = v.Format(BigNumberNotation.Letter, 2);
            })
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .GC()
            .Run();
        }

        // ── ModifierRegistry ──────────────────────────────────────────────────────

        [Test, Performance]
        public void ModifierRegistry_GetMultiplier_TenSources()
        {
            var registry = new ModifierRegistry();
            for (int i = 0; i < 10; i++)
                registry.Register(new StubModifierSource($"src{i}", 1.1));

            Measure.Method(() =>
            {
                for (int i = 0; i < 100; i++)
                    _ = registry.GetMultiplier(StatType.IdleYieldRate);
            })
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .GC()
            .Run();
        }

        [Test, Performance]
        public void ModifierRegistry_GetBreakdown_TenSources()
        {
            var registry = new ModifierRegistry();
            for (int i = 0; i < 10; i++)
                registry.Register(new StubModifierSource($"src{i}", 1.1));

            Measure.Method(() =>
            {
                for (int i = 0; i < 100; i++)
                    _ = registry.GetTotal(StatType.IdleYieldRate);
            })
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .GC()
            .Run();
        }

        // ── MigrationPipeline ─────────────────────────────────────────────────────

        [Test, Performance]
        public void MigrationPipeline_Apply_V1toV3()
        {
            var pipeline = new MigrationPipeline(new IMigration[]
            {
                new SaveMigration_V1_V2(),
                new SaveMigration_V2_V3(),
            });

            Measure.Method(() =>
            {
                var data = new SaveData { SchemaVersion = 1 };
#pragma warning disable CS0618
                data.LegacyCurrentResources = 999_999L;
#pragma warning restore CS0618
                pipeline.Apply(data, 3);
            })
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .GC()
            .Run();
        }

        // ── SaveSigner ────────────────────────────────────────────────────────────

        [Test, Performance]
        public void SaveSigner_Sign_OneKilobyteJson()
        {
            // Build a ~1 KB JSON string representative of a real save
            var json = new System.Text.StringBuilder();
            json.Append("{\"SchemaVersion\":2,\"CurrentResources\":1234567.89,\"PrestigeCount\":5,");
            json.Append("\"WaveNumber\":42,\"UpgradeNodeStates\":{");
            for (int i = 0; i < 30; i++)
                json.Append($"\"node_{i:D3}\":{i},");
            json.Append("\"node_final\":99}}");
            string testJson = json.ToString();

            Measure.Method(() =>
            {
                _ = SaveSigner.Sign(testJson);
            })
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .GC()
            .Run();
        }

        [Test, Performance]
        public void SaveSigner_Verify_OneKilobyteJson()
        {
            var jb = new System.Text.StringBuilder();
            jb.Append("{\"SchemaVersion\":2,\"CurrentResources\":1234567.89}");
            string testJson = jb.ToString();
            string sig = SaveSigner.Sign(testJson);

            Measure.Method(() =>
            {
                _ = SaveSigner.Verify(testJson, sig);
            })
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .GC()
            .Run();
        }

        // ── BulkPurchase ──────────────────────────────────────────────────────────

        [Test, Performance]
        public void BulkPurchase_CalculateAffordableCount_N1000()
        {
            var config = ScriptableObject.CreateInstance<GeneratorConfigSO>();
            config.BaseCost         = 10L;
            config.CostScalingFactor = 1.07f;

            Measure.Method(() =>
            {
                _ = GeneratorSystem.CalculateAffordableCount(config, 0, 1e12);
            })
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .GC()
            .Run();

            Object.DestroyImmediate(config);
        }

        // ── MockTickSource ────────────────────────────────────────────────────────

        [Test, Performance]
        public void MockTickSource_FireTick_100Subscribers()
        {
            var tick = new MockTickSource();
            for (int i = 0; i < 100; i++)
                tick.Subscribe(_ => { });

            Measure.Method(() =>
            {
                for (int i = 0; i < 60; i++) // simulate 60 ticks
                    tick.FireTick(1f);
            })
            .WarmupCount(WarmupCount)
            .MeasurementCount(MeasurementCount)
            .GC()
            .Run();

            tick.ClearSubscribers();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private class StubModifierSource : IModifierSource
        {
            private readonly double _mult;
            public string SourceId { get; }

            public StubModifierSource(string id, double mult)
            {
                SourceId = id;
                _mult    = mult;
            }

            public Modifier GetModifier(StatType stat)
                => Modifier.FromMultiplier(_mult);
        }
    }
}
