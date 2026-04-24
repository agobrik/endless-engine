// Tests for Sprint 15 — S15-04: MinigameService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - CanTrigger returns true when ready, false when on cooldown
//   - TryTrigger returns false when already in session
//   - TryTrigger fires OnMinigameStarted, sets IsSessionActive
//   - RecordAction increments SessionActions, fires OnActionRecorded
//   - RecordAction ignored when no session active
//   - EndSession calculates reward (base + per-action bonus)
//   - EndSession caps reward at MaxRewardMultiplier
//   - EndSession awards gold via EconomyService
//   - EndSession fires OnMinigameEnded
//   - Reward calculation formula
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.MergeMinigame

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Minigame;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.MergeMinigame
{
    [TestFixture]
    public class MinigameServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private MinigameService     _service;
        private EconomyService      _economy;
        private ActiveSkillConfigSO _tapSkill;
        private ActiveSkillConfigSO _timedSkill;

        private readonly List<ActiveSkillConfigSO>       _startedEvents  = new List<ActiveSkillConfigSO>();
        private readonly List<(ActiveSkillConfigSO, int)> _actionEvents   = new List<(ActiveSkillConfigSO, int)>();
        private readonly List<(ActiveSkillConfigSO, long)>_endedEvents    = new List<(ActiveSkillConfigSO, long)>();
        private readonly List<string>                     _readyEvents    = new List<string>();
        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            MinigameService.ClearSubscribersForTesting();
            MinigameService.OnMinigameStarted += s => _startedEvents.Add(s);
            MinigameService.OnActionRecorded  += (s, n) => _actionEvents.Add((s, n));
            MinigameService.OnMinigameEnded   += (s, r) => _endedEvents.Add((s, r));
            MinigameService.OnSkillReady      += id => _readyEvents.Add(id);
            _startedEvents.Clear(); _actionEvents.Clear(); _endedEvents.Clear(); _readyEvents.Clear();

            // Economy
            var ecoGo = new GameObject("Economy");
            _economy  = ecoGo.AddComponent<EconomyService>();
            _economy.Initialize(null, new GameObject("Save").AddComponent<SaveService>());
            var sd = new SaveData(); sd.EnsureDefaults(); sd.CurrentResources = 0;
            _economy.OnAfterLoad(sd);

            // Skills
            _tapSkill = ScriptableObject.CreateInstance<ActiveSkillConfigSO>();
            _tapSkill.SkillId               = "tap_frenzy";
            _tapSkill.MinigameType          = MinigameType.TapFrenzy;
            _tapSkill.CooldownSeconds       = 30f;
            _tapSkill.MinigameDurationSeconds = 10f;
            _tapSkill.BaseGoldReward        = 1000;
            _tapSkill.PerActionBonus        = 0.1f;   // +10% per tap
            _tapSkill.MaxRewardMultiplier   = 3f;     // cap at 3×

            _timedSkill = ScriptableObject.CreateInstance<ActiveSkillConfigSO>();
            _timedSkill.SkillId               = "timed_press";
            _timedSkill.MinigameType          = MinigameType.TimedPress;
            _timedSkill.CooldownSeconds       = 60f;
            _timedSkill.MinigameDurationSeconds = 5f;
            _timedSkill.BaseGoldReward        = 500;
            _timedSkill.PerActionBonus        = 0.5f;
            _timedSkill.MaxRewardMultiplier   = 2f;

            var go    = new GameObject("MinigameService");
            _service  = go.AddComponent<MinigameService>();
            _service.Initialize(new[] { _tapSkill, _timedSkill }, _economy);
        }

        [TearDown]
        public void TearDown()
        {
            MinigameService.ClearSubscribersForTesting();
            if (_service    != null) Object.DestroyImmediate(_service.gameObject);
            if (_economy    != null) Object.DestroyImmediate(_economy.gameObject);
            if (_tapSkill   != null) Object.DestroyImmediate(_tapSkill);
            if (_timedSkill != null) Object.DestroyImmediate(_timedSkill);
            if (_econConfig != null) Object.DestroyImmediate(_econConfig);
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        // ── CanTrigger ────────────────────────────────────────────────────────────

        [Test]
        public void CanTrigger_TrueWhenReady()
        {
            Assert.IsTrue(_service.CanTrigger("tap_frenzy"));
        }

        [Test]
        public void CanTrigger_FalseWhenOnCooldown()
        {
            _service.InjectCooldownForTesting("tap_frenzy", 25f);
            Assert.IsFalse(_service.CanTrigger("tap_frenzy"));
        }

        [Test]
        public void CanTrigger_FalseWhenSessionActive()
        {
            _service.TryTrigger("tap_frenzy");
            Assert.IsFalse(_service.CanTrigger("timed_press"),
                "Cannot trigger a second skill while a session is running");
        }

        // ── TryTrigger ────────────────────────────────────────────────────────────

        [Test]
        public void TryTrigger_ReturnsTrue_AndSetsSessionActive()
        {
            bool result = _service.TryTrigger("tap_frenzy");
            Assert.IsTrue(result);
            Assert.IsTrue(_service.IsSessionActive);
        }

        [Test]
        public void TryTrigger_FiresOnMinigameStarted()
        {
            _service.TryTrigger("tap_frenzy");
            Assert.AreEqual(1, _startedEvents.Count);
            Assert.AreEqual(_tapSkill, _startedEvents[0]);
        }

        [Test]
        public void TryTrigger_ReturnsFalseWhenOnCooldown()
        {
            _service.InjectCooldownForTesting("tap_frenzy", 15f);
            bool result = _service.TryTrigger("tap_frenzy");
            Assert.IsFalse(result);
            Assert.IsFalse(_service.IsSessionActive);
        }

        // ── RecordAction ──────────────────────────────────────────────────────────

        [Test]
        public void RecordAction_IncrementsSessionActions()
        {
            _service.TryTrigger("tap_frenzy");
            _service.RecordAction();
            _service.RecordAction();
            Assert.AreEqual(2, _service.SessionActions);
        }

        [Test]
        public void RecordAction_FiresOnActionRecorded()
        {
            _service.TryTrigger("tap_frenzy");
            _service.RecordAction();
            Assert.AreEqual(1, _actionEvents.Count);
            Assert.AreEqual(1, _actionEvents[0].Item2, "Action count = 1");
        }

        [Test]
        public void RecordAction_IgnoredWhenNoSession()
        {
            _service.RecordAction();
            Assert.AreEqual(0, _actionEvents.Count);
            Assert.AreEqual(0, _service.SessionActions);
        }

        // ── EndSession / reward ───────────────────────────────────────────────────

        [Test]
        public void EndSession_AwardsBaseRewardWithZeroActions()
        {
            _service.TryTrigger("tap_frenzy");
            _service.EndSession();

            Assert.AreEqual(1000, _economy.CurrentResources,
                "Base reward = 1000 when no actions");
        }

        [Test]
        public void EndSession_AwardsPerActionBonus()
        {
            _service.TryTrigger("tap_frenzy");
            _service.RecordAction(); // +10% → 1100
            _service.RecordAction(); // +10% → 1200
            _service.RecordAction(); // +10% → 1300
            _service.EndSession();

            Assert.AreEqual(1300, _economy.CurrentResources);
        }

        [Test]
        public void EndSession_CapsAtMaxMultiplier()
        {
            _service.TryTrigger("tap_frenzy");
            for (int i = 0; i < 100; i++) _service.RecordAction(); // would be ×11 without cap
            _service.EndSession();

            Assert.AreEqual(3000, _economy.CurrentResources,
                "Cap = 3× → 3000 gold max");
        }

        [Test]
        public void EndSession_FiresOnMinigameEnded()
        {
            _service.TryTrigger("tap_frenzy");
            _service.EndSession();

            Assert.AreEqual(1, _endedEvents.Count);
            Assert.AreEqual(_tapSkill, _endedEvents[0].Item1);
        }

        [Test]
        public void EndSession_SetsSessionActiveFalse()
        {
            _service.TryTrigger("tap_frenzy");
            _service.EndSession();
            Assert.IsFalse(_service.IsSessionActive);
        }

        // ── Reward formula ────────────────────────────────────────────────────────

        [Test]
        public void CalculateReward_Formula()
        {
            // PerActionBonus=0.1, MaxMult=3. actions=5 → mult=1.5 → reward=1500
            long reward = _service.CalculateRewardForTesting(_tapSkill, 5);
            Assert.AreEqual(1500, reward);
        }

        [Test]
        public void CalculateReward_CapsCorrectly()
        {
            // actions=30 → raw mult=4.0, capped at 3 → 3000
            long reward = _service.CalculateRewardForTesting(_tapSkill, 30);
            Assert.AreEqual(3000, reward);
        }
#endif
    }
}
