// Tests for Story S3-04: Enemy AI — EnemyManager Core Behavior
// Type: Logic (Unit/EditMode)
// Story: production/epics/enemy-ai-behavior/story-001-basic-enemy-state-machine.md
//
// These tests verify:
//   (1) AC-AI-01: Enemy moves toward player each frame (position changes toward target)
//   (2) AC-AI-02: Enemy attacks on AttackInterval timer (DamageDispatcher called at correct interval)
//   (3) AC-AI-03: Enemy does not attack outside AttackRange (AttackTimer not reset, no dispatch)
//   (4) AC-AI-04: Enemies pause during IdleRecovery (no movement, no attack)
//   (5) AC-AI-06: Enemy initialized from EnemyRuntimeData (MoveSpeed, AttackTimer set correctly)
//   (6) EC-AI-01: Zero-vector case (enemy at player position) — no exception
//   (7) Dead enemy removal — MarkDead triggers removal + OnEnemyKilled event
//   (8) Multiple enemies update independently (flat list, no cross-contamination)
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.EnemyAIBehavior

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Enemy;

namespace EndlessEngine.Tests.Unit.EnemyAIBehavior
{
    [TestFixture]
    public class BasicEnemyStateMachineTests
    {
        // ── Fakes ──────────────────────────────────────────────────────────────────

        private class FakePlayerQuery : IPlayerQuery
        {
            public Vector2 Position       { get; set; }
            public bool    IsInIdleRecovery { get; set; }
        }

        private class FakeDamageDispatcher : IDamageDispatcher
        {
            public int AttackCount;
            public EnemyAgent LastAttacker;

            public void DispatchEnemyAttack(EnemyAgent agent, Vector2 playerPosition)
            {
                AttackCount++;
                LastAttacker = agent;
            }
        }

        // ── State ──────────────────────────────────────────────────────────────────

        private EnemyManager        _manager;
        private FakePlayerQuery     _playerQuery;
        private FakeDamageDispatcher _damageDispatcher;

        private EnemyAgent _killedAgent;
        private int        _killedCount;

        // ── Helpers ────────────────────────────────────────────────────────────────

        private EnemyAgent MakeAgent(Vector2 pos, float moveSpeed = 2f,
            float attackInterval = 2f, float attackRange = 1f)
        {
            return new EnemyAgent
            {
                InstanceId     = UnityEngine.Random.Range(1, 100000),
                Position       = pos,
                MoveSpeed      = moveSpeed,
                AttackInterval = attackInterval,
                AttackTimer    = attackInterval,
                AttackRange    = attackRange,
                State          = EnemyState.Moving,
                GoldDropAmount = 10L,
            };
        }

        // ── SetUp / TearDown ──────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _playerQuery      = new FakePlayerQuery { Position = Vector2.zero, IsInIdleRecovery = false };
            _damageDispatcher = new FakeDamageDispatcher();

            var go = new GameObject("EnemyManager");
            _manager = go.AddComponent<EnemyManager>();
            _manager.Initialize(_playerQuery, _damageDispatcher);

            _killedAgent = null;
            _killedCount = 0;

            EnemyManager.OnEnemyKilled += CaptureKilled;
        }

        [TearDown]
        public void TearDown()
        {
            EnemyManager.OnEnemyKilled -= CaptureKilled;
            if (_manager != null)
                UnityEngine.Object.DestroyImmediate(_manager.gameObject);
        }

        private void CaptureKilled(EnemyAgent agent)
        {
            _killedAgent = agent;
            _killedCount++;
        }

        // ── Tests ─────────────────────────────────────────────────────────────────

        /// <summary>AC-AI-01: Enemy moves toward player each frame.</summary>
        [Test]
        public void Test_EnemyManager_Update_EnemyMovesTowardPlayer()
        {
            // Arrange
            // Enemy at (5, 0), Player at (0, 0), MoveSpeed = 2.0, dt = 0.016
            // Expected new X ≈ 5 - (2.0 × 0.016) = 4.968
            var agent = MakeAgent(new Vector2(5f, 0f), moveSpeed: 2f, attackRange: 0.1f);
            _playerQuery.Position = Vector2.zero;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _manager.SpawnEnemy(agent);
            _manager.TickForTesting(Vector2.zero, false, 0.016f);
#endif

            // Assert — position moved toward (0,0)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnemyAgent updated = _manager.ActiveEnemiesForTesting[0];
            Assert.Less(updated.Position.x, 5f, "Enemy X should decrease toward player.");
            Assert.AreEqual(0f, updated.Position.y, 0.0001f, "Enemy Y should remain 0 (horizontal movement).");
            Assert.AreEqual(4.968f, updated.Position.x, 0.001f,
                "Enemy should move 2.0 × 0.016 = 0.032 units toward player.");
#endif
        }

        /// <summary>AC-AI-02: Enemy attacks on AttackInterval timer when in range.</summary>
        [Test]
        public void Test_EnemyManager_Update_EnemyAttacksAfterIntervalWhenInRange()
        {
            // Arrange
            // Enemy at (0.5, 0) — within AttackRange=1.0 of player at (0, 0)
            // AttackInterval = 2.0 — simulate 2.0s elapsed as two 1.0s ticks
            var agent = MakeAgent(new Vector2(0.5f, 0f), moveSpeed: 0f, attackInterval: 2f, attackRange: 1f);
            _playerQuery.Position = Vector2.zero;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _manager.SpawnEnemy(agent);

            // Tick 1: 1.0s — timer goes from 2.0 → 1.0, no attack yet
            _manager.TickForTesting(Vector2.zero, false, 1.0f);
            Assert.AreEqual(0, _damageDispatcher.AttackCount, "No attack should fire before interval elapses.");

            // Tick 2: 1.0s more — timer goes from 1.0 → 0, attack fires
            _manager.TickForTesting(Vector2.zero, false, 1.0f);
            Assert.AreEqual(1, _damageDispatcher.AttackCount, "Attack should fire when timer reaches 0 within range.");

            // Timer should reset to AttackInterval=2.0
            float timer = _manager.ActiveEnemiesForTesting[0].AttackTimer;
            Assert.AreEqual(2.0f, timer, 0.001f, "AttackTimer should reset to AttackInterval after attack.");
#endif
        }

        /// <summary>AC-AI-03: Enemy does not attack when outside AttackRange.</summary>
        [Test]
        public void Test_EnemyManager_Update_EnemyDoesNotAttackOutsideRange()
        {
            // Arrange — enemy at (5, 0), player at (0, 0), AttackRange = 0.5
            // Timer = 0 immediately (already expired)
            var agent = MakeAgent(new Vector2(5f, 0f), moveSpeed: 0f, attackInterval: 1f, attackRange: 0.5f);
            agent.AttackTimer = 0f; // already expired
            _playerQuery.Position = Vector2.zero;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _manager.SpawnEnemy(agent);
            _manager.TickForTesting(Vector2.zero, false, 0.016f);

            Assert.AreEqual(0, _damageDispatcher.AttackCount,
                "No attack should fire when enemy is outside AttackRange, even with expired timer.");
#endif
        }

        /// <summary>AC-AI-04: All enemies pause during IdleRecovery — no movement, no attack.</summary>
        [Test]
        public void Test_EnemyManager_Update_PausesDuringIdleRecovery()
        {
            // Arrange
            var agent = MakeAgent(new Vector2(0.5f, 0f), moveSpeed: 5f, attackInterval: 0.1f, attackRange: 2f);
            agent.AttackTimer = 0f; // would attack immediately
            _playerQuery.Position = Vector2.zero;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _manager.SpawnEnemy(agent);
            Vector2 positionBefore = agent.Position;

            // Act — tick with IdleRecovery = true
            _manager.TickForTesting(Vector2.zero, true, 1.0f);

            EnemyAgent updated = _manager.ActiveEnemiesForTesting[0];
            Assert.AreEqual(EnemyState.Idle, updated.State,   "Enemy state should be Idle during IdleRecovery.");
            Assert.AreEqual(0, _damageDispatcher.AttackCount, "No attack should fire during IdleRecovery.");
            Assert.AreEqual(positionBefore, updated.Position, "Enemy position should not change during IdleRecovery.");
#endif
        }

        /// <summary>AC-AI-06: Enemy initialized from spawn — MoveSpeed and AttackTimer set correctly.</summary>
        [Test]
        public void Test_EnemyManager_SpawnEnemy_InitializesFieldsFromRuntimeData()
        {
            // Arrange
            var agent = MakeAgent(new Vector2(3f, 1f), moveSpeed: 5f, attackInterval: 1.5f, attackRange: 0.5f);
            agent.GoldDropAmount = 42L;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _manager.SpawnEnemy(agent);
            _manager.TickForTesting(Vector2.zero, false, 0f); // flush queue only

            EnemyAgent spawned = _manager.ActiveEnemiesForTesting[0];
            Assert.AreEqual(5f,   spawned.MoveSpeed,      "MoveSpeed should be 5.0 from spawn data.");
            Assert.AreEqual(1.5f, spawned.AttackInterval, "AttackInterval should be 1.5 from spawn data.");
            Assert.AreEqual(1.5f, spawned.AttackTimer,    "AttackTimer should initialize to AttackInterval.");
            Assert.AreEqual(42L,  spawned.GoldDropAmount, "GoldDropAmount should be preserved from spawn data.");
            Assert.AreEqual(EnemyState.Moving, spawned.State, "Enemy should start in Moving state.");
#endif
        }

        /// <summary>EC-AI-01: Enemy at exact player position — no exception, no movement.</summary>
        [Test]
        public void Test_EnemyManager_Update_ZeroVectorCase_NoException()
        {
            // Arrange — enemy at same position as player
            var agent = MakeAgent(Vector2.zero, moveSpeed: 5f, attackRange: 2f);
            agent.AttackTimer = 100f; // won't attack
            _playerQuery.Position = Vector2.zero;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _manager.SpawnEnemy(agent);

            // Act — should not throw
            Assert.DoesNotThrow(() => _manager.TickForTesting(Vector2.zero, false, 0.016f),
                "EnemyManager should not throw when enemy is at player position (zero move vector).");

            EnemyAgent updated = _manager.ActiveEnemiesForTesting[0];
            Assert.AreEqual(Vector2.zero, updated.Position, "Enemy at player position should stay at (0,0).");
#endif
        }

        /// <summary>Dead enemy fires OnEnemyKilled and is removed from active list.</summary>
        [Test]
        public void Test_EnemyManager_MarkDead_RemovesEnemyAndFiresOnEnemyKilled()
        {
            // Arrange
            var agent = MakeAgent(new Vector2(1f, 0f));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _manager.SpawnEnemy(agent);
            _manager.TickForTesting(Vector2.zero, false, 0f); // flush

            Assert.AreEqual(1, _manager.ActiveEnemiesForTesting.Count, "One enemy should be active.");

            // Act — mark dead and tick
            _manager.MarkDead(_manager.ActiveEnemiesForTesting[0].InstanceId);
            _manager.TickForTesting(Vector2.zero, false, 0.016f);

            // Assert
            Assert.AreEqual(0, _manager.ActiveEnemiesForTesting.Count, "Dead enemy should be removed from active list.");
            Assert.AreEqual(1, _killedCount,    "OnEnemyKilled should fire once.");
            Assert.IsNotNull(_killedAgent,       "OnEnemyKilled should pass the agent reference.");
#endif
        }

        /// <summary>Multiple enemies update independently — no state cross-contamination.</summary>
        [Test]
        public void Test_EnemyManager_Update_MultipleEnemiesUpdateIndependently()
        {
            // Arrange — two enemies, different speeds and positions
            var agent1 = MakeAgent(new Vector2(5f,  0f), moveSpeed: 2f, attackRange: 0.1f);
            var agent2 = MakeAgent(new Vector2(-5f, 0f), moveSpeed: 4f, attackRange: 0.1f);
            _playerQuery.Position = Vector2.zero;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _manager.SpawnEnemy(agent1);
            _manager.SpawnEnemy(agent2);
            _manager.TickForTesting(Vector2.zero, false, 0.016f);

            EnemyAgent a1 = _manager.ActiveEnemiesForTesting[0];
            EnemyAgent a2 = _manager.ActiveEnemiesForTesting[1];

            // Agent1 moved from (5,0) toward (0,0): new X < 5
            Assert.Less(a1.Position.x, 5f, "Agent1 should have moved toward player.");
            // Agent2 moved from (-5,0) toward (0,0): new X > -5
            Assert.Greater(a2.Position.x, -5f, "Agent2 should have moved toward player.");
            // They should not have swapped positions
            Assert.Greater(a1.Position.x, 0f,  "Agent1 should still be on positive X side.");
            Assert.Less(a2.Position.x,    0f,  "Agent2 should still be on negative X side.");
#endif
        }
    }
}
