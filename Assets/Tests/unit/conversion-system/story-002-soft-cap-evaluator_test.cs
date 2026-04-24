// Tests for Sprint 8 — S8-04: SoftCapEvaluator
// Type: Logic (Unit/EditMode)
//
// Covers all three curve types; threshold boundary; AbsoluteCeiling; null config.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.ConversionSystem

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;

namespace EndlessEngine.Tests.Unit.ConversionSystem
{
    [TestFixture]
    public class SoftCapEvaluatorTests
    {
        private SoftCapConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<SoftCapConfigSO>();
            _config.Threshold      = 1000;
            _config.K              = 1.0;
            _config.HardCeiling    = 10_000;
            _config.AbsoluteCeiling = 0; // disabled by default
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null) Object.DestroyImmediate(_config);
        }

        // ── Below threshold: no change ────────────────────────────────────────────

        [TestCase(0)]
        [TestCase(500)]
        [TestCase(999.99)]
        [TestCase(1000)]   // exactly at threshold
        public void Apply_BelowOrAtThreshold_ReturnsRaw(double raw)
        {
            _config.CurveType = SoftCapCurveType.Asymptotic;
            double result = SoftCapEvaluator.Apply(raw, _config);
            Assert.AreEqual(raw, result, 0.001, $"Apply({raw}) at threshold 1000 must return raw unchanged");
        }

        // ── Logarithmic curve ─────────────────────────────────────────────────────

        [Test]
        public void Apply_Logarithmic_AboveThreshold_GrowsSlower()
        {
            _config.CurveType = SoftCapCurveType.Logarithmic;
            double raw    = 5000;
            double result = SoftCapEvaluator.Apply(raw, _config);
            Assert.Less(result, raw, "Logarithmic above threshold must be less than raw");
            Assert.Greater(result, _config.Threshold, "Result must still be above threshold");
        }

        [Test]
        public void Apply_Logarithmic_SmoothJoinAtThreshold()
        {
            _config.CurveType = SoftCapCurveType.Logarithmic;
            // At threshold, ln(1 + 0) = 0, so result = threshold + 0 = threshold
            double result = SoftCapEvaluator.Apply(_config.Threshold, _config);
            Assert.AreEqual(_config.Threshold, result, 0.001);
        }

        [Test]
        public void Apply_Logarithmic_LargerK_MoreGrowth()
        {
            _config.CurveType = SoftCapCurveType.Logarithmic;
            double raw = 2000;

            _config.K = 1.0;
            double result1 = SoftCapEvaluator.Apply(raw, _config);

            _config.K = 2.0;
            double result2 = SoftCapEvaluator.Apply(raw, _config);

            Assert.Greater(result2, result1, "Larger K should produce more effective value");
        }

        // ── SquareRoot curve ──────────────────────────────────────────────────────

        [Test]
        public void Apply_SquareRoot_AboveThreshold_GrowsSlower()
        {
            _config.CurveType = SoftCapCurveType.SquareRoot;
            double raw    = 5000;
            double result = SoftCapEvaluator.Apply(raw, _config);
            Assert.Less(result, raw);
            Assert.Greater(result, _config.Threshold);
        }

        [Test]
        public void Apply_SquareRoot_Formula_CorrectValue()
        {
            _config.CurveType = SoftCapCurveType.SquareRoot;
            _config.K = 1.0;
            // raw=1100, threshold=1000 → result = 1000 + sqrt(100) = 1010
            double result = SoftCapEvaluator.Apply(1100, _config);
            Assert.AreEqual(1010.0, result, 0.001);
        }

        // ── Asymptotic curve ──────────────────────────────────────────────────────

        [Test]
        public void Apply_Asymptotic_NeverReachesCeiling()
        {
            _config.CurveType = SoftCapCurveType.Asymptotic;
            // Even an extremely large raw value must stay below ceiling
            double result = SoftCapEvaluator.Apply(1_000_000_000, _config);
            Assert.Less(result, _config.HardCeiling, "Asymptotic must never reach HardCeiling");
        }

        [Test]
        public void Apply_Asymptotic_ApproachesCeilingForLargeRaw()
        {
            _config.CurveType   = SoftCapCurveType.Asymptotic;
            _config.HardCeiling = 10_000;
            _config.K           = 1.0;
            double result = SoftCapEvaluator.Apply(1_000_000, _config);
            Assert.Greater(result, 9_900, "For very large raw, asymptotic result should be near ceiling");
        }

        [Test]
        public void Apply_Asymptotic_DegenerateCeiling_FallsBackToRaw()
        {
            _config.CurveType   = SoftCapCurveType.Asymptotic;
            _config.HardCeiling = 500; // below threshold → degenerate
            double raw    = 2000;
            double result = SoftCapEvaluator.Apply(raw, _config);
            Assert.AreEqual(raw, result, 0.001, "Degenerate ceiling (< threshold) must fall back to raw");
        }

        // ── AbsoluteCeiling ───────────────────────────────────────────────────────

        [Test]
        public void Apply_AbsoluteCeiling_ClampsResult()
        {
            _config.CurveType       = SoftCapCurveType.Logarithmic;
            _config.K               = 100.0; // high K → high output
            _config.AbsoluteCeiling = 1500;
            double result = SoftCapEvaluator.Apply(10_000, _config);
            Assert.LessOrEqual(result, 1500.0, "AbsoluteCeiling must clamp the result");
        }

        // ── Null config ───────────────────────────────────────────────────────────

        [Test]
        public void Apply_NullConfig_ReturnsRaw()
        {
            double result = SoftCapEvaluator.Apply(5000, null);
            Assert.AreEqual(5000, result, 0.001);
        }

        [Test]
        public void Apply_NegativeInput_ReturnsZero()
        {
            _config.CurveType = SoftCapCurveType.Logarithmic;
            double result = SoftCapEvaluator.Apply(-100, _config);
            Assert.AreEqual(0, result, 0.001);
        }

        // ── Sample ────────────────────────────────────────────────────────────────

        [Test]
        public void Sample_ReturnsCorrectCount()
        {
            _config.CurveType = SoftCapCurveType.Asymptotic;
            var samples = SoftCapEvaluator.Sample(_config, maxRaw: 20_000, sampleCount: 10);
            Assert.AreEqual(10, samples.Length);
        }

        [Test]
        public void Sample_FirstSampleIsZero()
        {
            _config.CurveType = SoftCapCurveType.Asymptotic;
            var samples = SoftCapEvaluator.Sample(_config, maxRaw: 20_000, sampleCount: 5);
            Assert.AreEqual(0, samples[0].raw, 0.001);
        }

        [Test]
        public void Sample_NullConfig_ReturnsEmpty()
        {
            var samples = SoftCapEvaluator.Sample(null, maxRaw: 1000, sampleCount: 10);
            Assert.AreEqual(0, samples.Length);
        }
    }
}
