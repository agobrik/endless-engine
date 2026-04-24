// Tests for Sprint 9 — S9-05: ProgressETAService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - ETA = 0 when current gold already meets target
//   - ETA = -1 when rate is zero
//   - ETA formula: (target - current) / rate
//   - FormatETA: seconds, minutes, hours, days
//   - Currency ETA helper
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.MilestoneSystem

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Milestone;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.MilestoneSystem
{
    [TestFixture]
    public class ProgressETAServiceTests
    {
        private ProgressETAService _service;
        private EconomyService     _economy;
        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 10_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            var go = new GameObject("ETATest");

            _economy = go.AddComponent<EconomyService>();
            _economy.Initialize(upgradeTreeQuery: null, saveNotifier: null);
            var save = new SaveData { CurrentResources = 0L };
            _economy.OnAfterLoad(save);

            _service = go.AddComponent<ProgressETAService>();
            _service.Initialize(_economy, generatorSystem: null);
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ProgressETAService.ClearSubscribersForTesting();

            if (_service != null) Object.DestroyImmediate(_service.gameObject);
            if (_econConfig != null) Object.DestroyImmediate(_econConfig);

            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
#endif
        }

        // ── Already-met target ────────────────────────────────────────────────────

        [Test]
        public void CalculateETA_TargetAlreadyMet_ReturnsZero()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(currentResources: 1000L, hardCap: 10_000_000L, startingGold: 0L);
            float eta = _service.CalculateETA(500L);
            Assert.AreEqual(0f, eta, 0.001f);
#endif
        }

        // ── Zero rate ────────────────────────────────────────────────────────────

        [Test]
        public void CalculateETA_ZeroRate_ReturnsMinusOne()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // No GeneratorSystem → rate = 0
            float eta = _service.CalculateETA(1000L);
            Assert.AreEqual(-1f, eta, 0.001f, "ETA must be -1 when income rate is zero");
#endif
        }

        // ── Currency ETA ──────────────────────────────────────────────────────────

        [Test]
        public void CalculateETACurrency_AlreadyMet_ReturnsZero()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float eta = _service.CalculateETACurrency("gems", target: 100, currentBalance: 150, ratePerSecond: 5);
            Assert.AreEqual(0f, eta, 0.001f);
#endif
        }

        [Test]
        public void CalculateETACurrency_ZeroRate_ReturnsMinusOne()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float eta = _service.CalculateETACurrency("gems", target: 100, currentBalance: 0, ratePerSecond: 0);
            Assert.AreEqual(-1f, eta, 0.001f);
#endif
        }

        [Test]
        public void CalculateETACurrency_Formula_CorrectValue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // (100 - 40) / 3 = 20 seconds
            float eta = _service.CalculateETACurrency("gems", target: 100, currentBalance: 40, ratePerSecond: 3);
            Assert.AreEqual(20f, eta, 0.01f);
#endif
        }

        // ── FormatETA ─────────────────────────────────────────────────────────────

        [Test]
        public void FormatETA_Negative_ReturnsUnknown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual("Unknown", _service.FormatETA(-1f));
#endif
        }

        [Test]
        public void FormatETA_Zero_ReturnsReached()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual("Reached!", _service.FormatETA(0f));
#endif
        }

        [Test]
        public void FormatETA_Seconds()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual("45 sec", _service.FormatETA(45f));
#endif
        }

        [Test]
        public void FormatETA_MinutesAndSeconds()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 150 sec = 2 min 30 sec
            Assert.AreEqual("2 min 30 sec", _service.FormatETA(150f));
#endif
        }

        [Test]
        public void FormatETA_ExactMinutes()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual("5 min", _service.FormatETA(300f));
#endif
        }

        [Test]
        public void FormatETA_HoursAndMinutes()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 3720 sec = 1 hr 2 min
            Assert.AreEqual("1 hr 2 min", _service.FormatETA(3720f));
#endif
        }

        [Test]
        public void FormatETA_ExactHours()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual("2 hr", _service.FormatETA(7200f));
#endif
        }

        [Test]
        public void FormatETA_Days()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 90000 sec = 1d 1 hr
            Assert.AreEqual("1d 1 hr", _service.FormatETA(90000f));
#endif
        }

        [Test]
        public void FormatETA_ExactDays()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual("2d", _service.FormatETA(172800f));
#endif
        }
    }
}
