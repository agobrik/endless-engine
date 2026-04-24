// Tests for Story S3-09: Prestige System — Prestige Reward and Reset Coordinator
// Type: Logic (Unit/EditMode)
// Story: production/epics/prestige-system/story-001-prestige-reward-and-reset.md
//
// These tests verify:
//   (1) AC-PRG-01: TryInitiatePrestige() when CanPrestige=true → OnPrestigeScreenRequested + OnPauseRequested fire
//   (2) AC-PRG-01b: TryInitiatePrestige() when CanPrestige=false → no events fire
//   (3) AC-PRG-02: CancelPrestige() → OnPrestigeScreenDismissed(false) fires; prestige count unchanged
//   (4) AC-PRG-04: GetPrestigePreview() returns correct projected multiplier
//   (5) AC-PRG-05: SkipCeremony() → OnPrestigeCeremonyComplete fires immediately
//   (6) AC-PRG-06: TryInitiatePrestige when already in ScreenOpen state is ignored (guard)
//   (7) OnPrestigeComplete received → OnPrestigeCeremonyStart fires with correct data
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.PrestigeSystemTests

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Prestige;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.PrestigeSystemTests
{
    [TestFixture]
    public class PrestigeRewardAndResetTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        private static PrestigeConfigSO MakePrestigeCfg(
            int   minWave            = 5,
            float baseMultiplier     = 1.5f,
            float maxMultiplier      = 1000f)
        {
            var so = ScriptableObject.CreateInstance<PrestigeConfigSO>();
            so.MinWaveForPrestige      = minWave;
            so.BaseMultiplierPerPrestige = baseMultiplier;
            so.MaxPermanentMultiplier  = maxMultiplier;
            so.MaxPrestigeCount        = 0;
            return so;
        }

        private static EconomyConfigSO MakeEconomyCfg(float idleYieldBase = 10f)
        {
            var so = ScriptableObject.CreateInstance<EconomyConfigSO>();
            so.IdleYieldRateBase = idleYieldBase;
            return so;
        }

        // ── Setup / Teardown ──────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            ConfigRegistry.InjectForTesting(
                prestige: MakePrestigeCfg(),
                economy:  MakeEconomyCfg());
        }

        [TearDown]
        public void TearDown()
        {
            PrestigeSystem.ClearStaticEventsForTesting();
            PrestigeStateManager.ClearStaticEventsForTesting();
            ConfigRegistry.ClearForTesting();

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                Object.DestroyImmediate(go);
        }

        // ── AC-PRG-01: TryInitiatePrestige when CanPrestige=true ─────────────────

        [Test]
        public void test_prestigeSystem_initiatePrestige_whenCanPrestige_firesScreenRequestedAndPause()
        {
            // Arrange
            var psmGo = new GameObject();
            var psm   = psmGo.AddComponent<PrestigeStateManager>();
            psm.SetWaveNumberForTesting(10); // above MinWave=5

            var sysGo = new GameObject();
            var sys   = sysGo.AddComponent<PrestigeSystem>();
            sys.InjectPrestigeStateManagerForTesting(psm);

            bool screenRequested = false;
            bool pauseRequested  = false;
            PrestigeSystem.OnPrestigeScreenRequested += () => screenRequested = true;
            PrestigeSystem.OnPauseRequested          += () => pauseRequested  = true;

            // Act
            sys.TryInitiatePrestige();

            // Assert
            Assert.IsTrue(screenRequested, "OnPrestigeScreenRequested should fire");
            Assert.IsTrue(pauseRequested,  "OnPauseRequested should fire");
            Assert.AreEqual(PrestigeSystemState.ScreenOpen, sys.GetStateForTesting());
        }

        // ── AC-PRG-01b: TryInitiatePrestige when CanPrestige=false ───────────────

        [Test]
        public void test_prestigeSystem_initiatePrestige_whenCannotPrestige_firesNoEvents()
        {
            // Arrange: wave=0, MinWave=5 → CanPrestige=false
            var psmGo = new GameObject();
            var psm   = psmGo.AddComponent<PrestigeStateManager>();
            psm.SetWaveNumberForTesting(0);

            var sysGo = new GameObject();
            var sys   = sysGo.AddComponent<PrestigeSystem>();
            sys.InjectPrestigeStateManagerForTesting(psm);

            bool anyEventFired = false;
            PrestigeSystem.OnPrestigeScreenRequested += () => anyEventFired = true;
            PrestigeSystem.OnPauseRequested          += () => anyEventFired = true;

            // Act
            sys.TryInitiatePrestige();

            // Assert
            Assert.IsFalse(anyEventFired, "No events should fire when CanPrestige=false");
            Assert.AreEqual(PrestigeSystemState.Inactive, sys.GetStateForTesting());
        }

        // ── AC-PRG-02: CancelPrestige returns to combat ───────────────────────────

        [Test]
        public void test_prestigeSystem_cancelPrestige_firesScreenDismissedWithFalse()
        {
            // Arrange
            var psmGo = new GameObject();
            var psm   = psmGo.AddComponent<PrestigeStateManager>();
            psm.SetWaveNumberForTesting(10);

            var sysGo = new GameObject();
            var sys   = sysGo.AddComponent<PrestigeSystem>();
            sys.InjectPrestigeStateManagerForTesting(psm);
            sys.TryInitiatePrestige(); // open screen first

            bool? dismissedConfirmed = null;
            PrestigeSystem.OnPrestigeScreenDismissed += confirmed => dismissedConfirmed = confirmed;

            // Act
            sys.CancelPrestige();

            // Assert
            Assert.IsNotNull(dismissedConfirmed, "OnPrestigeScreenDismissed should fire");
            Assert.IsFalse(dismissedConfirmed.Value, "Cancel should fire with confirmed=false");
            Assert.AreEqual(PrestigeSystemState.Inactive, sys.GetStateForTesting());
            Assert.AreEqual(0, psm.PrestigeCount, "PrestigeCount should be unchanged");
        }

        // ── AC-PRG-04: GetPrestigePreview returns correct projected multiplier ────

        [Test]
        public void test_prestigeSystem_getPrestigePreview_returnsCorrectProjectedMultiplier()
        {
            // Arrange: BaseMultiplierPerPrestige=1.5, PrestigeCount=2 → NewMultiplier=1.5^3=3.375
            var psmGo = new GameObject();
            var psm   = psmGo.AddComponent<PrestigeStateManager>();
            psm.SetPrestigeCountForTesting(2);
            psm.SetWaveNumberForTesting(10);

            var sysGo = new GameObject();
            var sys   = sysGo.AddComponent<PrestigeSystem>();
            sys.InjectPrestigeStateManagerForTesting(psm);

            // Act
            var preview = sys.GetPrestigePreview();

            // Assert
            Assert.AreEqual(2, preview.CurrentPrestigeCount);
            Assert.AreEqual(3, preview.NewPrestigeCount);
            Assert.AreEqual(1, preview.CurrentPrestigeCount + 1 - 2, "offset check"); // just clarity
            Assert.AreEqual(3, preview.NewPrestigeCount);
            float expected = Mathf.Pow(1.5f, 3); // 3.375
            Assert.AreEqual(expected, preview.NewMultiplier, 0.001f,
                $"NewMultiplier should be 1.5^3=3.375, got {preview.NewMultiplier}");
        }

        // ── AC-PRG-05: SkipCeremony fires OnPrestigeCeremonyComplete immediately ──

        [Test]
        public void test_prestigeSystem_skipCeremony_firesPrestigeCeremonyCompleteImmediately()
        {
            // Arrange
            var psmGo = new GameObject();
            var psm   = psmGo.AddComponent<PrestigeStateManager>();
            psm.SetWaveNumberForTesting(10);

            var sysGo = new GameObject();
            var sys   = sysGo.AddComponent<PrestigeSystem>();
            sys.InjectPrestigeStateManagerForTesting(psm);

            // Force into Ceremony state
            sys.SetStateForTesting(PrestigeSystemState.Ceremony);

            bool ceremonyCalled = false;
            bool dismissedFired = false;
            PrestigeSystem.OnPrestigeCeremonyComplete += () => ceremonyCalled = true;
            PrestigeSystem.OnPrestigeScreenDismissed  += _ => dismissedFired = true;

            // Act
            sys.SkipCeremony();

            // Assert
            Assert.IsTrue(ceremonyCalled, "OnPrestigeCeremonyComplete should fire on skip");
            Assert.IsTrue(dismissedFired, "OnPrestigeScreenDismissed should fire after ceremony completes");
            Assert.AreEqual(PrestigeSystemState.Inactive, sys.GetStateForTesting());
        }

        // ── AC-PRG-06: Double-initiate guard ─────────────────────────────────────

        [Test]
        public void test_prestigeSystem_doubleInitiate_secondCallIgnored()
        {
            // Arrange
            var psmGo = new GameObject();
            var psm   = psmGo.AddComponent<PrestigeStateManager>();
            psm.SetWaveNumberForTesting(10);

            var sysGo = new GameObject();
            var sys   = sysGo.AddComponent<PrestigeSystem>();
            sys.InjectPrestigeStateManagerForTesting(psm);

            int screenRequestedCount = 0;
            PrestigeSystem.OnPrestigeScreenRequested += () => screenRequestedCount++;

            // Act
            sys.TryInitiatePrestige();
            sys.TryInitiatePrestige(); // second call while ScreenOpen — should be ignored

            // Assert
            Assert.AreEqual(1, screenRequestedCount,
                "OnPrestigeScreenRequested should fire exactly once");
        }

        // ── OnPrestigeComplete → ceremony start ───────────────────────────────────

        [Test]
        public void test_prestigeSystem_onPrestigeComplete_firesOnPrestigeCeremonyStartWithCorrectData()
        {
            // Arrange
            var psmGo = new GameObject();
            var psm   = psmGo.AddComponent<PrestigeStateManager>();
            psm.SetWaveNumberForTesting(10);

            var sysGo = new GameObject();
            var sys   = sysGo.AddComponent<PrestigeSystem>();
            sys.InjectPrestigeStateManagerForTesting(psm);

            int   receivedCount      = -1;
            float receivedMultiplier = -1f;
            long  receivedProjection = -1L;
            PrestigeSystem.OnPrestigeCeremonyStart += (count, mult, proj) =>
            {
                receivedCount      = count;
                receivedMultiplier = mult;
                receivedProjection = proj;
            };

            // Act: simulate PSM firing OnPrestigeComplete(1, 1.5)
            sys.SimulatePrestigeCompleteForTesting(1, 1.5f);

            // Assert
            Assert.AreEqual(1, receivedCount,     "count should match");
            Assert.AreEqual(1.5f, receivedMultiplier, 0.001f, "multiplier should match");
            // Projection = 10 (idle base) × 1.5 × 6 × 3600 = 324,000
            Assert.AreEqual(324_000L, receivedProjection,
                $"Projection should be 10×1.5×6×3600=324000, got {receivedProjection}");
            Assert.AreEqual(PrestigeSystemState.Ceremony, sys.GetStateForTesting());
        }
    }
}
