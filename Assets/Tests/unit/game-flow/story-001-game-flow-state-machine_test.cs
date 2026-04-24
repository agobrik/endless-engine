// Tests for GameFlowStateMachine
// Type: Logic (Unit/EditMode)
//
// Verifies:
//   (1) Initial state is Menu
//   (2) StartRun transitions to InRun and fires OnEnteredRun
//   (3) EndRun transitions to PostRun and fires OnEnteredPostRun
//   (4) ReturnToMenu transitions to Menu and fires OnEnteredMenu
//   (5) StartRun from non-Menu state is ignored
//   (6) EndRun from non-InRun state is ignored
//   (7) ReturnToMenu from non-PostRun state is ignored
//   (8) OnStateChanged fires with correct (from, to) parameters

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Flow;

namespace EndlessEngine.Tests.Unit.GameFlow
{
    public class GameFlowStateMachineTests
    {
        private GameFlowStateMachine _fsm;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("GameFlow");
            _fsm = go.AddComponent<GameFlowStateMachine>();
        }

        [TearDown]
        public void TearDown()
        {
            GameFlowStateMachine.ClearSubscribersForTesting();
            Object.DestroyImmediate(_fsm.gameObject);
        }

        [Test]
        public void InitialState_IsMenu()
        {
            Assert.AreEqual(GameFlowState.Menu, _fsm.CurrentState);
            Assert.IsTrue(_fsm.IsInMenu);
        }

        [Test]
        public void StartRun_TransitionsToInRun()
        {
            _fsm.StartRun();
            Assert.AreEqual(GameFlowState.InRun, _fsm.CurrentState);
            Assert.IsTrue(_fsm.IsInRun);
        }

        [Test]
        public void StartRun_FiresOnEnteredRun()
        {
            bool fired = false;
            GameFlowStateMachine.OnEnteredRun += () => fired = true;
            _fsm.StartRun();
            Assert.IsTrue(fired);
        }

        [Test]
        public void EndRun_TransitionsToPostRun()
        {
            _fsm.StartRun();
            _fsm.EndRun();
            Assert.AreEqual(GameFlowState.PostRun, _fsm.CurrentState);
            Assert.IsTrue(_fsm.IsPostRun);
        }

        [Test]
        public void EndRun_FiresOnEnteredPostRun()
        {
            bool fired = false;
            GameFlowStateMachine.OnEnteredPostRun += () => fired = true;
            _fsm.StartRun();
            _fsm.EndRun();
            Assert.IsTrue(fired);
        }

        [Test]
        public void ReturnToMenu_TransitionsToMenu()
        {
            _fsm.StartRun();
            _fsm.EndRun();
            _fsm.ReturnToMenu();
            Assert.AreEqual(GameFlowState.Menu, _fsm.CurrentState);
        }

        [Test]
        public void ReturnToMenu_FiresOnEnteredMenu()
        {
            bool fired = false;
            _fsm.StartRun();
            _fsm.EndRun();
            GameFlowStateMachine.OnEnteredMenu += () => fired = true;
            _fsm.ReturnToMenu();
            Assert.IsTrue(fired);
        }

        [Test]
        public void StartRun_FromInRun_IsIgnored()
        {
            _fsm.StartRun(); // Menu → InRun
            _fsm.StartRun(); // should be ignored
            Assert.AreEqual(GameFlowState.InRun, _fsm.CurrentState);
        }

        [Test]
        public void EndRun_FromMenu_IsIgnored()
        {
            _fsm.EndRun(); // should be ignored — we're in Menu
            Assert.AreEqual(GameFlowState.Menu, _fsm.CurrentState);
        }

        [Test]
        public void ReturnToMenu_FromMenu_IsIgnored()
        {
            _fsm.ReturnToMenu(); // should be ignored
            Assert.AreEqual(GameFlowState.Menu, _fsm.CurrentState);
        }

        [Test]
        public void OnStateChanged_FiresWithCorrectFromTo()
        {
            GameFlowState from = GameFlowState.PostRun;
            GameFlowState to   = GameFlowState.PostRun;
            GameFlowStateMachine.OnStateChanged += (f, t) => { from = f; to = t; };

            _fsm.StartRun();

            Assert.AreEqual(GameFlowState.Menu,  from);
            Assert.AreEqual(GameFlowState.InRun, to);
        }

        [Test]
        public void FullLoop_MenuInRunPostRunMenu_Works()
        {
            Assert.AreEqual(GameFlowState.Menu,    _fsm.CurrentState);
            _fsm.StartRun();
            Assert.AreEqual(GameFlowState.InRun,   _fsm.CurrentState);
            _fsm.EndRun();
            Assert.AreEqual(GameFlowState.PostRun, _fsm.CurrentState);
            _fsm.ReturnToMenu();
            Assert.AreEqual(GameFlowState.Menu,    _fsm.CurrentState);
        }
    }
}
