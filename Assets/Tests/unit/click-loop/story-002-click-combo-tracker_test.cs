// Tests for Story CLK-02: Click Loop — Combo Tracker
// Type: Unit (EditMode)
//
// AC-CLK-05: ComboMultiplier = 1 at zero combo points
// AC-CLK-06: Combo multiplier grows with points
// AC-CLK-07: Combo clamps at MaxComboMultiplier
// AC-CLK-08: Combo decays after ComboDecayDelay
// AC-CLK-09: RecordClick resets decay timer

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;
using EndlessEngine.ClickLoop;

namespace EndlessEngine.Tests.Unit.ClickLoopSystem
{
    [TestFixture]
    public class ClickComboTrackerTests
    {
        private ClickLoopConfigSO  _config;
        private ClickComboTracker  _tracker;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpgradeApplicationSystem.ResetForTesting();
#endif
            _config = ScriptableObject.CreateInstance<ClickLoopConfigSO>();
            _config.ComboDecayDelay     = 1.5f;
            _config.ComboDecayRate      = 8f;
            _config.MaxComboMultiplier  = 8f;
            _config.ComboPointsPerStep  = 5f;
            _config.BaseCritChance      = 0.05f;
            _config.BaseCritMultiplier  = 3f;
            _config.BaseAutoClickRate   = 0f;
            _config.OfflineCapHours     = 8f;
            _config.OfflineEfficiency   = 0.25f;

            _tracker = new ClickComboTracker(_config);
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null) Object.DestroyImmediate(_config);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpgradeApplicationSystem.ResetForTesting();
#endif
        }

        [Test]
        [Description("AC-CLK-05: ComboMultiplier is 1 with no clicks")]
        public void ComboMultiplier_NoClicks_ReturnsOne()
            => Assert.AreEqual(1f, _tracker.ComboMultiplier, 0.001f);

        [Test]
        [Description("AC-CLK-06: 5 points → multiplier = 2 (1 + 5/5)")]
        public void ComboMultiplier_FivePoints_ReturnsTwo()
        {
            _tracker.RecordClick(5f);
            Assert.AreEqual(2f, _tracker.ComboMultiplier, 0.001f);
        }

        [Test]
        [Description("AC-CLK-07: Excess points clamp at MaxComboMultiplier")]
        public void ComboMultiplier_ExcessPoints_ClampsAtMax()
        {
            _tracker.RecordClick(1000f);
            Assert.AreEqual(_config.MaxComboMultiplier, _tracker.ComboMultiplier, 0.001f);
        }

        [Test]
        [Description("AC-CLK-08: Combo decays to 0 after enough idle time")]
        public void Tick_AfterDecayDelay_ComboDecaysToZero()
        {
            _tracker.RecordClick(5f);
            float elapsed = 0f;
            while (elapsed < _config.ComboDecayDelay + 5f)
            {
                _tracker.Tick(0.1f);
                elapsed += 0.1f;
            }
            Assert.AreEqual(0f, _tracker.ComboPoints, 0.001f);
        }

        [Test]
        [Description("AC-CLK-08: Combo does not decay before ComboDecayDelay")]
        public void Tick_BeforeDecayDelay_ComboUnchanged()
        {
            _tracker.RecordClick(5f);
            _tracker.Tick(_config.ComboDecayDelay * 0.5f);
            Assert.AreEqual(5f, _tracker.ComboPoints, 0.001f);
        }

        [Test]
        [Description("AC-CLK-09: RecordClick resets decay timer")]
        public void RecordClick_DuringDecayWindow_ResetsTimer()
        {
            _tracker.RecordClick(5f);
            _tracker.Tick(_config.ComboDecayDelay * 0.75f);
            _tracker.RecordClick(2f);
            _tracker.Tick(_config.ComboDecayDelay * 0.75f);
            Assert.Greater(_tracker.ComboPoints, 0f);
        }

        [Test]
        [Description("Reset clears combo")]
        public void Reset_ClearsCombo()
        {
            _tracker.RecordClick(10f);
            _tracker.Reset();
            Assert.AreEqual(0f, _tracker.ComboPoints,     0.001f);
            Assert.AreEqual(1f, _tracker.ComboMultiplier, 0.001f);
        }
    }
}
