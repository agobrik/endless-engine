// Tests for Sprint 13 — S13-05: TimeBoostService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - TryActivate applies multiplier to TickEngine.TimeScale
//   - TryActivate fires OnBoostStarted
//   - TryActivatePaid deducts gold from EconomyService
//   - TryActivatePaid returns false when insufficient gold
//   - Cancel restores TickEngine.TimeScale to 1
//   - Cancel fires OnBoostEnded
//   - IsActive correct before/after activation and cancel
//   - Activating a new boost while one is active replaces it
//   - Null config is silently ignored
//
// NOTE: Timer-based expiry (BoostTimer coroutine) is NOT tested here —
//       coroutines are async and not deterministic in EditMode.
//       InjectRemainingForTesting + cancel covers the stop-boost logic.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.StatisticsSystem

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Flow;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.StatisticsSystem
{
    [TestFixture]
    public class TimeBoostServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private TimeBoostService  _service;
        private EndlessEngine.Flow.TickEngine        _tickEngine;
        private EconomyService    _economyService;
        private TimeBoostConfigSO _config2x;
        private TimeBoostConfigSO _config4x;
        private TimeBoostConfigSO _paidConfig;

        private readonly List<(TimeBoostConfigSO, float)> _startedEvents = new List<(TimeBoostConfigSO, float)>();
        private readonly List<float>                       _tickEvents    = new List<float>();
        private int                                        _endedCount;
        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            TimeBoostService.ClearSubscribersForTesting();
            TimeBoostService.OnBoostStarted += (cfg, remaining) => _startedEvents.Add((cfg, remaining));
            TimeBoostService.OnBoostTick    += r => _tickEvents.Add(r);
            TimeBoostService.OnBoostEnded   += () => _endedCount++;

            _startedEvents.Clear();
            _tickEvents.Clear();
            _endedCount = 0;

            // TickEngine
            var tickGo   = new GameObject("TickEngine");
            _tickEngine  = tickGo.AddComponent<EndlessEngine.Flow.TickEngine>();

            // EconomyService (minimal)
            var ecoGo        = new GameObject("EconomyService");
            _economyService  = ecoGo.AddComponent<EconomyService>();
            var saveGo       = new GameObject("SaveService");
            var saveService  = saveGo.AddComponent<SaveService>();
            _economyService.Initialize(null, saveService);
            // Inject gold
            var sd = new SaveData(); sd.EnsureDefaults(); sd.CurrentResources = 10000;
            _economyService.OnAfterLoad(sd);

            // TimeBoostService
            var svcGo = new GameObject("TimeBoostService");
            _service  = svcGo.AddComponent<TimeBoostService>();
            _service.Initialize(_tickEngine, _economyService);

            // Configs
            _config2x = ScriptableObject.CreateInstance<TimeBoostConfigSO>();
            _config2x.BoostId             = "boost_2x";
            _config2x.TimeScaleMultiplier = 2f;
            _config2x.DurationSeconds     = 60f;
            _config2x.GoldCost            = 0;

            _config4x = ScriptableObject.CreateInstance<TimeBoostConfigSO>();
            _config4x.BoostId             = "boost_4x";
            _config4x.TimeScaleMultiplier = 4f;
            _config4x.DurationSeconds     = 30f;
            _config4x.GoldCost            = 0;

            _paidConfig = ScriptableObject.CreateInstance<TimeBoostConfigSO>();
            _paidConfig.BoostId             = "boost_paid";
            _paidConfig.TimeScaleMultiplier = 2f;
            _paidConfig.DurationSeconds     = 60f;
            _paidConfig.GoldCost            = 500;
        }

        [TearDown]
        public void TearDown()
        {
            TimeBoostService.ClearSubscribersForTesting();
            if (_service       != null) Object.DestroyImmediate(_service.gameObject);
            if (_tickEngine    != null) Object.DestroyImmediate(_tickEngine.gameObject);
            if (_economyService!= null) Object.DestroyImmediate(_economyService.gameObject);
            if (_config2x      != null) Object.DestroyImmediate(_config2x);
            if (_config4x      != null) Object.DestroyImmediate(_config4x);
            if (_paidConfig    != null) Object.DestroyImmediate(_paidConfig);
            if (_econConfig    != null) Object.DestroyImmediate(_econConfig);
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        // ── TryActivate ───────────────────────────────────────────────────────────

        [Test]
        public void TryActivate_AppliesMultiplierToTickEngine()
        {
            _service.TryActivate(_config2x);
            Assert.AreEqual(2f, _tickEngine.TimeScale, 0.001f);
        }

        [Test]
        public void TryActivate_SetsIsActive()
        {
            Assert.IsFalse(_service.IsActive);
            _service.TryActivate(_config2x);
            Assert.IsTrue(_service.IsActive);
        }

        [Test]
        public void TryActivate_FiresOnBoostStarted()
        {
            _service.TryActivate(_config2x);
            Assert.AreEqual(1, _startedEvents.Count);
            Assert.AreEqual(_config2x, _startedEvents[0].Item1);
            Assert.AreEqual(60f, _startedEvents[0].Item2, 0.001f);
        }

        [Test]
        public void TryActivate_NullConfig_Ignored()
        {
            _service.TryActivate(null);
            Assert.IsFalse(_service.IsActive);
            Assert.AreEqual(0, _startedEvents.Count);
        }

        [Test]
        public void TryActivate_ReplacesExistingBoost()
        {
            _service.TryActivate(_config2x);
            _service.TryActivate(_config4x);

            Assert.AreEqual(4f, _tickEngine.TimeScale, 0.001f,
                "4× should replace the previous 2× boost");
            Assert.AreEqual(_config4x, _service.ActiveConfig);
        }

        // ── TryActivatePaid ───────────────────────────────────────────────────────

        [Test]
        public void TryActivatePaid_DeductsGold()
        {
            long goldBefore = _economyService.CurrentResources;
            _service.TryActivatePaid(_paidConfig);
            Assert.AreEqual(goldBefore - 500, _economyService.CurrentResources);
        }

        [Test]
        public void TryActivatePaid_ReturnsTrueOnSuccess()
        {
            bool result = _service.TryActivatePaid(_paidConfig);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryActivatePaid_ReturnsFalseWhenInsufficientGold()
        {
            // Drain gold
            _economyService.DeductResources(9900);
            bool result = _service.TryActivatePaid(_paidConfig);

            Assert.IsFalse(result);
            Assert.IsFalse(_service.IsActive);
        }

        [Test]
        public void TryActivatePaid_ZeroCost_ActivatesWithoutCharge()
        {
            long goldBefore = _economyService.CurrentResources;
            _service.TryActivatePaid(_config2x); // GoldCost = 0
            Assert.AreEqual(goldBefore, _economyService.CurrentResources,
                "Zero-cost boost must not deduct any gold");
        }

        // ── Cancel ────────────────────────────────────────────────────────────────

        [Test]
        public void Cancel_RestoresTimeScaleToOne()
        {
            _service.TryActivate(_config2x);
            _service.Cancel();
            Assert.AreEqual(1f, _tickEngine.TimeScale, 0.001f);
        }

        [Test]
        public void Cancel_FiresOnBoostEnded()
        {
            _service.TryActivate(_config2x);
            _service.Cancel();
            Assert.AreEqual(1, _endedCount);
        }

        [Test]
        public void Cancel_SetsIsActiveFalse()
        {
            _service.TryActivate(_config2x);
            _service.Cancel();
            Assert.IsFalse(_service.IsActive);
        }

        [Test]
        public void Cancel_WhenNotActive_NoEffect()
        {
            _service.Cancel();
            Assert.AreEqual(0, _endedCount);
            Assert.AreEqual(1f, _tickEngine.TimeScale, 0.001f);
        }
#endif
    }
}
