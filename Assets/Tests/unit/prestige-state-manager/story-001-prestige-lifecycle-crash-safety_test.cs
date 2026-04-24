// Tests for Story S2-04: Prestige State Manager — Lifecycle and Crash Safety
// Type: Logic (Unit/EditMode)
// Story: production/epics/prestige-state-manager/story-001-prestige-lifecycle-and-crash-safety.md
//
// Acceptance Criteria: AC-PSM-01 through AC-PSM-06
// (AC-PSM-07 is an Integration test covered by Save & Load Story 002)
//
// Async pattern: SaveService is not injected in these unit tests.
// BeginPrestigeForTesting() is used, which calls the same async path.
// SaveAsync is a no-op when _saveService is null.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.PrestigeStateManager

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Prestige;

namespace EndlessEngine.Tests.Unit.PrestigeStateManagerTests
{
    /// <summary>
    /// Unit tests for PrestigeStateManager lifecycle and crash safety (S2-04).
    /// Validates AC-PSM-01 through AC-PSM-06.
    /// </summary>
    [TestFixture]
    public class PrestigeLifecycleCrashSafetyTests
    {
        private global::EndlessEngine.Prestige.PrestigeStateManager _psm;
        private PrestigeConfigSO _prestigeConfig;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _psm = new GameObject("PSM_Test").AddComponent<global::EndlessEngine.Prestige.PrestigeStateManager>();
            global::EndlessEngine.Prestige.PrestigeStateManager.ClearStaticEventsForTesting();

            _prestigeConfig = ScriptableObject.CreateInstance<PrestigeConfigSO>();
            _prestigeConfig.MinWaveForPrestige        = 10;
            _prestigeConfig.MaxPrestigeCount          = 0;   // unlimited by default
            _prestigeConfig.BaseMultiplierPerPrestige = 1.5f;
            _prestigeConfig.MaxPermanentMultiplier    = 1000f;

            var playerConfig  = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            var economyConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            ConfigRegistry.InjectForTesting(prestige: _prestigeConfig, player: playerConfig, economy: economyConfig);
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            global::EndlessEngine.Prestige.PrestigeStateManager.ClearStaticEventsForTesting();
            if (_psm != null)
                UnityEngine.Object.DestroyImmediate(_psm.gameObject);
            ConfigRegistry.ClearForTesting();
#endif
        }

        // ── AC-PSM-01: Wave gate ──────────────────────────────────────────────────

        [Test]
        [Description("AC-PSM-01: MinWaveToPrestige=10, CurrentWave=9 → TryPrestige returns false.")]
        public void TryPrestige_WaveGateNotMet_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            _prestigeConfig.MinWaveForPrestige = 10;
            _psm.SetWaveNumberForTesting(9);

            bool onPrestigeStartedFired = false;
            global::EndlessEngine.Prestige.PrestigeStateManager.OnPrestigeStarted += () => onPrestigeStartedFired = true;

            // Act
            bool result = _psm.TryPrestige();

            // Assert
            Assert.IsFalse(result, "AC-PSM-01: TryPrestige must return false when wave < MinWaveForPrestige");
            Assert.AreEqual(0, _psm.PrestigeCount, "AC-PSM-01: PrestigeCount must remain 0");
            Assert.IsFalse(onPrestigeStartedFired, "AC-PSM-01: OnPrestigeStarted must not fire");
#endif
        }

        [Test]
        [Description("AC-PSM-01 (boundary): CurrentWave=10 exactly meets minimum → TryPrestige returns true.")]
        public void TryPrestige_WaveExactlyAtMinimum_ReturnsTrue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            _prestigeConfig.MinWaveForPrestige = 10;
            _psm.SetWaveNumberForTesting(10);

            // Act
            bool result = _psm.TryPrestige();

            // Assert
            Assert.IsTrue(result,
                "TryPrestige must return true when wave equals MinWaveForPrestige");
#endif
        }

        // ── AC-PSM-02: Count increments after prestige ────────────────────────────

        [Test]
        [Description("AC-PSM-02: Gates pass → PrestigeCount increments; OnPrestigeComplete fires with new count.")]
        public async Task TryPrestige_GatesPass_IncrementsCountAndFiresComplete()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            _psm.SetPrestigeCountForTesting(2);
            _psm.SetWaveNumberForTesting(15);

            int  completedCount = 0;
            float completedMult = 0f;
            global::EndlessEngine.Prestige.PrestigeStateManager.OnPrestigeComplete += (c, m) =>
            {
                completedCount = c;
                completedMult  = m;
            };

            // Act: fire-and-forget returns, but we await via BeginPrestigeForTesting
            await _psm.BeginPrestigeForTesting();

            // Assert
            Assert.AreEqual(3, _psm.PrestigeCount, "AC-PSM-02: PrestigeCount must be 3 after prestige");
            Assert.AreEqual(3, completedCount, "AC-PSM-02: OnPrestigeComplete must fire with new count=3");
            Assert.Greater(completedMult, 1f, "AC-PSM-02: multiplier must be > 1 after first prestige");
#endif
        }

        // ── AC-PSM-03: Multiplier formula ─────────────────────────────────────────

        [Test]
        [Description("AC-PSM-03: BaseMultiplier=1.5, PrestigeCount=4, Cap=1000 → mult=5.0625.")]
        public void GetPermanentMultiplier_FourPrestiges_ReturnsCorrectValue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            _prestigeConfig.BaseMultiplierPerPrestige = 1.5f;
            _prestigeConfig.MaxPermanentMultiplier    = 1000f;
            _psm.SetPrestigeCountForTesting(4);

            // Act
            float mult = _psm.GetPermanentMultiplier();

            // Assert: 1.5^4 = 5.0625
            Assert.AreEqual(5.0625f, mult, 0.0001f,
                "AC-PSM-03: GetPermanentMultiplier must return 1.5^4 = 5.0625");
#endif
        }

        [Test]
        [Description("AC-PSM-03 (cap): Multiplier capped at MaxPermanentMultiplier.")]
        public void GetPermanentMultiplier_HighPrestigeCount_CappedAtMax()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: very high prestige count, cap=1000
            _prestigeConfig.BaseMultiplierPerPrestige = 2.0f;
            _prestigeConfig.MaxPermanentMultiplier    = 1000f;
            _psm.SetPrestigeCountForTesting(60);

            // Act
            float mult = _psm.GetPermanentMultiplier();

            // Assert
            Assert.AreEqual(1000f, mult, 0.001f,
                "GetPermanentMultiplier must be capped at MaxPermanentMultiplier");
#endif
        }

        // ── AC-PSM-04: Subscriber reset chain fires ───────────────────────────────

        [Test]
        [Description("AC-PSM-04: OnPrestigeStarted fires to all registered subscribers.")]
        public async Task TryPrestige_OnPrestigeStarted_FiresAllSubscribers()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            _psm.SetWaveNumberForTesting(15);

            int subscriberAFired = 0;
            int subscriberBFired = 0;
            global::EndlessEngine.Prestige.PrestigeStateManager.OnPrestigeStarted += () => subscriberAFired++;
            global::EndlessEngine.Prestige.PrestigeStateManager.OnPrestigeStarted += () => subscriberBFired++;

            // Act
            await _psm.BeginPrestigeForTesting();

            // Assert
            Assert.AreEqual(1, subscriberAFired, "AC-PSM-04: Subscriber A must be called once");
            Assert.AreEqual(1, subscriberBFired, "AC-PSM-04: Subscriber B must be called once");
#endif
        }

        // ── AC-PSM-05: Double-prestige guard ──────────────────────────────────────

        [Test]
        [Description("AC-PSM-05: _prestigeInProgress=true → TryPrestige returns false, no state change.")]
        public void TryPrestige_AlreadyInProgress_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: simulate mid-prestige state
            _psm.SetWaveNumberForTesting(15);
            _psm.SetPrestigeInProgressForTesting(true);

            bool startedFired = false;
            global::EndlessEngine.Prestige.PrestigeStateManager.OnPrestigeStarted += () => startedFired = true;

            // Act
            bool result = _psm.TryPrestige();

            // Assert
            Assert.IsFalse(result, "AC-PSM-05: TryPrestige must return false when _prestigeInProgress=true");
            Assert.IsFalse(_psm.CanPrestige, "AC-PSM-05: CanPrestige must be false when in progress");
            Assert.IsFalse(startedFired, "AC-PSM-05: OnPrestigeStarted must not fire");
#endif
        }

        // ── AC-PSM-06: Max prestige cap ───────────────────────────────────────────

        [Test]
        [Description("AC-PSM-06: MaxPrestigeCount=3, PrestigeCount=3 → CanPrestige=false.")]
        public void TryPrestige_AtMaxPrestigeCount_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            _prestigeConfig.MaxPrestigeCount = 3;
            _psm.SetPrestigeCountForTesting(3);
            _psm.SetWaveNumberForTesting(15);

            // Act
            bool result = _psm.TryPrestige();

            // Assert
            Assert.IsFalse(_psm.CanPrestige, "AC-PSM-06: CanPrestige must be false at max prestige");
            Assert.IsFalse(result, "AC-PSM-06: TryPrestige must return false at max prestige");
#endif
        }

        [Test]
        [Description("AC-PSM-06: MaxPrestigeCount=0 (unlimited) → does not block at any count.")]
        public void TryPrestige_UnlimitedPrestiges_DoesNotBlockAtAnyCount()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: MaxPrestigeCount=0 means unlimited
            _prestigeConfig.MaxPrestigeCount = 0;
            _psm.SetPrestigeCountForTesting(100);
            _psm.SetWaveNumberForTesting(15);

            // Act
            bool canPrestige = _psm.CanPrestige;

            // Assert
            Assert.IsTrue(canPrestige,
                "MaxPrestigeCount=0 must allow unlimited prestiges — CanPrestige must be true");
#endif
        }

        // ── PrestigeCount=0: baseline multiplier ──────────────────────────────────

        [Test]
        [Description("PrestigeCount=0 → GetPermanentMultiplier returns 1.0 (baseMulti^0 = 1).")]
        public void GetPermanentMultiplier_NoPrestige_ReturnsOne()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            _psm.SetPrestigeCountForTesting(0);

            // Act
            float mult = _psm.GetPermanentMultiplier();

            // Assert: any base^0 = 1
            Assert.AreEqual(1.0f, mult, 0.0001f,
                "With PrestigeCount=0, GetPermanentMultiplier must return 1.0 (no amplification)");
#endif
        }
    }
}
