// Tests for Story HAR-02: Harvest System — Combo Tracker
// Type: Unit (EditMode)
//
// Acceptance Criteria:
//   AC-HAR-06: Combo multiplier = 1 at zero combo points
//   AC-HAR-07: Combo multiplier increases with combo points (1 + points/step)
//   AC-HAR-08: Combo multiplier clamps at MaxComboMultiplier
//   AC-HAR-09: Combo decays to 0 after ComboDecayDelay with no hits
//   AC-HAR-10: RecordHit resets decay timer
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.HarvestSystem

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;
using EndlessEngine.Harvest;

namespace EndlessEngine.Tests.Unit.HarvestSystem
{
    [TestFixture]
    public class HarvestComboTrackerTests
    {
        private HarvestAreaConfigSO  _config;
        private HarvestComboTracker  _tracker;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpgradeApplicationSystem.ResetForTesting();
#endif
            _config = ScriptableObject.CreateInstance<HarvestAreaConfigSO>();
            _config.ComboDecayDelay              = 2f;
            _config.ComboDecayRate               = 5f;
            _config.MaxComboMultiplier           = 5f;
            _config.ComboPointsPerMultiplierStep = 10f;
            _config.BaseRadius                   = 1.5f;
            _config.BaseTickInterval             = 0.25f;

            _tracker = new HarvestComboTracker(_config);
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
                Object.DestroyImmediate(_config);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpgradeApplicationSystem.ResetForTesting();
#endif
        }

        // ── AC-HAR-06: Baseline multiplier ───────────────────────────────────────

        [Test]
        [Description("AC-HAR-06: ComboMultiplier is 1 when no hits recorded")]
        public void ComboMultiplier_NoHits_ReturnsOne()
        {
            Assert.AreEqual(1f, _tracker.ComboMultiplier, 0.001f,
                "ComboMultiplier must be 1 with zero combo points");
        }

        // ── AC-HAR-07: Multiplier growth ──────────────────────────────────────────

        [Test]
        [Description("AC-HAR-07: 10 combo points → multiplier = 2 (1 + 10/10)")]
        public void ComboMultiplier_TenPoints_ReturnsTwo()
        {
            _tracker.RecordHit(10f);

            Assert.AreEqual(2f, _tracker.ComboMultiplier, 0.001f,
                "10 combo points with step=10 must give multiplier of 2");
        }

        [Test]
        [Description("AC-HAR-07: 5 combo points → multiplier = 1.5")]
        public void ComboMultiplier_FivePoints_ReturnsOnePointFive()
        {
            _tracker.RecordHit(5f);

            Assert.AreEqual(1.5f, _tracker.ComboMultiplier, 0.001f,
                "5 combo points with step=10 must give multiplier of 1.5");
        }

        // ── AC-HAR-08: Max clamp ──────────────────────────────────────────────────

        [Test]
        [Description("AC-HAR-08: Very high combo points clamp at MaxComboMultiplier")]
        public void ComboMultiplier_ExcessivePoints_ClampsAtMax()
        {
            _tracker.RecordHit(1000f);

            Assert.AreEqual(_config.MaxComboMultiplier, _tracker.ComboMultiplier, 0.001f,
                "ComboMultiplier must not exceed MaxComboMultiplier regardless of points");
        }

        // ── AC-HAR-09: Decay ──────────────────────────────────────────────────────

        [Test]
        [Description("AC-HAR-09: After ComboDecayDelay + enough time, combo decays to 0")]
        public void Tick_AfterDecayDelay_ComboDecaysToZero()
        {
            _tracker.RecordHit(10f);

            // Advance past the decay delay
            float elapsed = 0f;
            while (elapsed < _config.ComboDecayDelay + 5f) // more than enough
            {
                _tracker.Tick(0.1f);
                elapsed += 0.1f;
            }

            Assert.AreEqual(0f, _tracker.ComboPoints, 0.001f,
                "Combo points must decay to 0 after enough idle time");
        }

        [Test]
        [Description("AC-HAR-09: Combo does NOT decay before ComboDecayDelay elapses")]
        public void Tick_BeforeDecayDelay_ComboDoesNotDecay()
        {
            _tracker.RecordHit(10f);

            // Advance but stay within the decay window
            _tracker.Tick(_config.ComboDecayDelay * 0.5f);

            Assert.AreEqual(10f, _tracker.ComboPoints, 0.001f,
                "Combo points must not decay before the decay delay window elapses");
        }

        // ── AC-HAR-10: RecordHit resets decay timer ───────────────────────────────

        [Test]
        [Description("AC-HAR-10: RecordHit within decay window resets timer; combo does not decay")]
        public void RecordHit_DuringDecayWindow_ResetsDecayTimer()
        {
            _tracker.RecordHit(10f);

            // Advance past half the decay delay (no decay yet)
            _tracker.Tick(_config.ComboDecayDelay * 0.75f);

            // Hit again — should reset the decay timer
            _tracker.RecordHit(5f);

            // Advance another 0.75× decay delay (would have triggered decay without reset)
            _tracker.Tick(_config.ComboDecayDelay * 0.75f);

            // Should still have at least the points from second hit
            Assert.Greater(_tracker.ComboPoints, 0f,
                "RecordHit must reset the decay timer so combo is not lost prematurely");
        }

        // ── Reset ─────────────────────────────────────────────────────────────────

        [Test]
        [Description("Reset clears combo points and multiplier returns to 1")]
        public void Reset_AfterHits_ClearsCombo()
        {
            _tracker.RecordHit(20f);
            _tracker.Reset();

            Assert.AreEqual(0f, _tracker.ComboPoints,    0.001f, "ComboPoints must be 0 after Reset");
            Assert.AreEqual(1f, _tracker.ComboMultiplier, 0.001f, "ComboMultiplier must be 1 after Reset");
        }
    }
}
