// Tests for RunSessionManager
// Type: Logic (Unit/EditMode)
//
// Verifies:
//   (1) IsRunActive is false before run starts
//   (2) IsRunActive is true after OnEnteredRun fires
//   (3) TotalRunSeconds reads from RunConfigSO
//   (4) RemainingSeconds starts at TotalRunSeconds
//   (5) OnRunEnded fires when run ends
//   (6) IsRunActive is false after run ends

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Flow;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.RunSession
{
    public class RunSessionManagerTests
    {
        private GameFlowStateMachine _fsm;
        private RunSessionManager    _rsm;

        [SetUp]
        public void SetUp()
        {
            var econConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            econConfig.ResourceHardCap = 1_000_000L;
            econConfig.StartingGold    = 0L;

            var runConfig = ScriptableObject.CreateInstance<RunConfigSO>();
            runConfig.RunDurationSeconds = 90f;

            ConfigRegistry.InjectForTesting(economy: econConfig, run: runConfig);

            var fsmGO = new GameObject("GameFlow");
            _fsm = fsmGO.AddComponent<GameFlowStateMachine>();

            var rsmGO = new GameObject("RunSession");
            _rsm = rsmGO.AddComponent<RunSessionManager>();
            _rsm.Initialize(_fsm);
        }

        [TearDown]
        public void TearDown()
        {
            ConfigRegistry.ClearForTesting();
            GameFlowStateMachine.ClearSubscribersForTesting();
            RunSessionManager.ClearSubscribersForTesting();
            Object.DestroyImmediate(_fsm.gameObject);
            Object.DestroyImmediate(_rsm.gameObject);
        }

        [Test]
        public void BeforeRunStarts_IsRunActive_IsFalse()
        {
            Assert.IsFalse(_rsm.IsRunActive);
        }

        [Test]
        public void AfterStartRun_IsRunActive_IsTrue()
        {
            _fsm.StartRun();
            Assert.IsTrue(_rsm.IsRunActive);
        }

        [Test]
        public void AfterStartRun_TotalRunSeconds_ReadsFromConfig()
        {
            _fsm.StartRun();
            Assert.AreEqual(90f, _rsm.TotalRunSeconds, 0.001f);
        }

        [Test]
        public void AfterStartRun_RemainingSeconds_EqualsTotalRunSeconds()
        {
            _fsm.StartRun();
            Assert.AreEqual(_rsm.TotalRunSeconds, _rsm.RemainingSeconds, 0.001f);
        }

        [Test]
        public void AfterEndRun_IsRunActive_IsFalse()
        {
            _fsm.StartRun();
            _fsm.EndRun();
            Assert.IsFalse(_rsm.IsRunActive);
        }

        [Test]
        public void OnRunEnded_FiresWhenRunEnds()
        {
            bool fired = false;
            RunSessionManager.OnRunEnded += _ => fired = true;
            _fsm.StartRun();
            _fsm.EndRun();
            Assert.IsTrue(fired);
        }

        [Test]
        public void ConfigNotLoaded_FallsBackTo120Seconds()
        {
            // Clear run config — should use fallback
            ConfigRegistry.ClearForTesting();
            var econConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            econConfig.ResourceHardCap = 1_000_000L;
            ConfigRegistry.InjectForTesting(economy: econConfig); // no run config

            _fsm.StartRun();
            Assert.AreEqual(120f, _rsm.TotalRunSeconds, 0.001f);
        }
    }
}
