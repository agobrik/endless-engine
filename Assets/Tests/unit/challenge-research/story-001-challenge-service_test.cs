// Tests for Sprint 14 — S14-05: ChallengeService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - ActivateChallenge sets active challenge, fires event
//   - CancelChallenge clears active challenge, fires event
//   - GetModifiers returns modifiers of requested type only
//   - IsSystemDisabled reflects active DisableSystem modifiers
//   - CalculateReward applies RewardMultiplier correctly
//   - CalculateReward returns base gold when no challenge active
//   - NotifyWaveReached completes challenge at required wave
//   - NotifyWaveReached below threshold does not complete
//   - NotifyRunFailed fires OnChallengeFailed, clears state
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.ChallengeResearch

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Challenge;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.ChallengeResearch
{
    [TestFixture]
    public class ChallengeServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private ChallengeService  _service;
        private EconomyService    _economy;
        private ChallengeConfigSO _basicChallenge;
        private ChallengeConfigSO _timedChallenge;

        private readonly List<ChallengeConfigSO> _activatedEvents = new List<ChallengeConfigSO>();
        private int _cancelledCount;
        private readonly List<long> _completedRewards = new List<long>();
        private int _failedCount;
        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            ChallengeService.ClearSubscribersForTesting();
            ChallengeService.OnChallengeActivated  += cfg => _activatedEvents.Add(cfg);
            ChallengeService.OnChallengeCancelled  += () => _cancelledCount++;
            ChallengeService.OnChallengeCompleted  += r  => _completedRewards.Add(r);
            ChallengeService.OnChallengeFailed     += () => _failedCount++;
            _activatedEvents.Clear(); _completedRewards.Clear();
            _cancelledCount = 0; _failedCount = 0;

            // EconomyService
            var ecoGo  = new GameObject("Economy");
            _economy   = ecoGo.AddComponent<EconomyService>();
            var saveGo = new GameObject("Save");
            _economy.Initialize(null, saveGo.AddComponent<SaveService>());
            var sd = new SaveData(); sd.EnsureDefaults(); sd.CurrentResources = 10000;
            _economy.OnAfterLoad(sd);

            // Service
            var go    = new GameObject("ChallengeService");
            _service  = go.AddComponent<ChallengeService>();
            _service.Initialize(_economy);

            // Basic challenge: disable generators, ×2 reward, wave 10 target
            _basicChallenge = ScriptableObject.CreateInstance<ChallengeConfigSO>();
            _basicChallenge.ChallengeId      = "no_generators";
            _basicChallenge.DisplayName      = "No Generators";
            _basicChallenge.RequiredWave     = 10;
            _basicChallenge.RewardMultiplier = 2f;
            _basicChallenge.Modifiers        = new List<ChallengeModifier>
            {
                new ChallengeModifier { Type = ChallengeModifierType.DisableSystem, TargetId = "generators" },
                new ChallengeModifier { Type = ChallengeModifierType.EnemyDifficultyScale, TargetId = "", Value = 1.5f }
            };

            _timedChallenge = ScriptableObject.CreateInstance<ChallengeConfigSO>();
            _timedChallenge.ChallengeId      = "timed";
            _timedChallenge.DisplayName      = "Timed Challenge";
            _timedChallenge.RequiredWave     = 5;
            _timedChallenge.TimeLimitSeconds = 120f;
            _timedChallenge.RewardMultiplier = 1.5f;
            _timedChallenge.Modifiers        = new List<ChallengeModifier>();
        }

        [TearDown]
        public void TearDown()
        {
            ChallengeService.ClearSubscribersForTesting();
            if (_service        != null) Object.DestroyImmediate(_service.gameObject);
            if (_economy        != null) Object.DestroyImmediate(_economy.gameObject);
            if (_basicChallenge != null) Object.DestroyImmediate(_basicChallenge);
            if (_timedChallenge != null) Object.DestroyImmediate(_timedChallenge);
            if (_econConfig     != null) Object.DestroyImmediate(_econConfig);
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        // ── Activate / cancel ─────────────────────────────────────────────────────

        [Test]
        public void ActivateChallenge_SetsActiveChallenge()
        {
            _service.ActivateChallenge(_basicChallenge);
            Assert.AreEqual(_basicChallenge, _service.ActiveChallenge);
        }

        [Test]
        public void ActivateChallenge_FiresEvent()
        {
            _service.ActivateChallenge(_basicChallenge);
            Assert.AreEqual(1, _activatedEvents.Count);
            Assert.AreEqual(_basicChallenge, _activatedEvents[0]);
        }

        [Test]
        public void CancelChallenge_ClearsActiveChallenge()
        {
            _service.ActivateChallenge(_basicChallenge);
            _service.CancelChallenge();
            Assert.IsNull(_service.ActiveChallenge);
        }

        [Test]
        public void CancelChallenge_FiresEvent()
        {
            _service.ActivateChallenge(_basicChallenge);
            _service.CancelChallenge();
            Assert.AreEqual(1, _cancelledCount);
        }

        // ── Modifiers ─────────────────────────────────────────────────────────────

        [Test]
        public void GetModifiers_ReturnsOnlyRequestedType()
        {
            _service.ActivateChallenge(_basicChallenge);
            _service.OnRunStarted();

            var disabledMods = _service.GetModifiers(ChallengeModifierType.DisableSystem);
            var diffMods     = _service.GetModifiers(ChallengeModifierType.EnemyDifficultyScale);

            Assert.AreEqual(1, disabledMods.Count);
            Assert.AreEqual(1, diffMods.Count);
            Assert.AreEqual("generators", disabledMods[0].TargetId);
        }

        [Test]
        public void IsSystemDisabled_TrueForDisabledSystems()
        {
            _service.ActivateChallenge(_basicChallenge);
            _service.OnRunStarted();

            Assert.IsTrue(_service.IsSystemDisabled("generators"));
            Assert.IsFalse(_service.IsSystemDisabled("upgrades"));
        }

        [Test]
        public void GetModifiers_EmptyWhenNoChallengeActive()
        {
            var mods = _service.GetModifiers(ChallengeModifierType.DisableSystem);
            Assert.IsEmpty(mods);
        }

        // ── Reward ────────────────────────────────────────────────────────────────

        [Test]
        public void CalculateReward_AppliesMultiplier()
        {
            _service.ActivateChallenge(_basicChallenge); // ×2 reward
            long reward = _service.CalculateReward(1000L);
            Assert.AreEqual(2000L, reward);
        }

        [Test]
        public void CalculateReward_ReturnsBaseGoldWhenNoChallenge()
        {
            long reward = _service.CalculateReward(500L);
            Assert.AreEqual(500L, reward);
        }

        // ── Wave / run completion ─────────────────────────────────────────────────

        [Test]
        public void NotifyWaveReached_AtRequiredWave_CompletesChallenge()
        {
            _service.ActivateChallenge(_basicChallenge);
            _service.OnRunStarted();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.ForceRunActiveForTesting(true);
#endif
            _service.NotifyWaveReached(10); // == RequiredWave

            Assert.AreEqual(1, _completedRewards.Count);
        }

        [Test]
        public void NotifyWaveReached_BelowThreshold_NoCompletion()
        {
            _service.ActivateChallenge(_basicChallenge);
            _service.OnRunStarted();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.ForceRunActiveForTesting(true);
#endif
            _service.NotifyWaveReached(9);

            Assert.AreEqual(0, _completedRewards.Count);
        }

        [Test]
        public void NotifyRunFailed_FiresFailedEvent()
        {
            _service.ActivateChallenge(_basicChallenge);
            _service.OnRunStarted();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.ForceRunActiveForTesting(true);
#endif
            _service.NotifyRunFailed();

            Assert.AreEqual(1, _failedCount);
        }

        [Test]
        public void NotifyRunFailed_ClearsActiveChallenge()
        {
            _service.ActivateChallenge(_basicChallenge);
            _service.OnRunStarted();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.ForceRunActiveForTesting(true);
#endif
            _service.NotifyRunFailed();

            Assert.IsNull(_service.ActiveChallenge);
        }
#endif
    }
}
