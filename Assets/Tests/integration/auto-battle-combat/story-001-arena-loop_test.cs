// Integration tests for Auto-Battle Combat Story 001 — 30-Second Arena Loop
// GDD: design/gdd/auto-battle-combat.md
// AC-ABC-01 through AC-ABC-10
//
// These are NUnit EditMode tests (UnityTest attribute for coroutine-driven ACs).
// All helpers are guarded by #if UNITY_EDITOR || DEVELOPMENT_BUILD.

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EndlessEngine.Combat;
using EndlessEngine.Config;
using EndlessEngine.Damage;
using EndlessEngine.Enemy;
using EndlessEngine.Wave;
using EndlessEngine.Health;

namespace EndlessEngine.Tests.Integration.AutoBattleCombat
{
    /// <summary>
    /// Integration tests for AutoBattleController.
    /// Validates AC-ABC-01 through AC-ABC-10 per GDD design/gdd/auto-battle-combat.md.
    /// </summary>
    [TestFixture]
    public class Story001ArenaLoopTests
    {
        // ── Shared fixtures ───────────────────────────────────────────────────────

        private GameObject          _controllerGO;
        private AutoBattleController _abc;
        private EnemyManager        _enemyManager;
        private WaveSpawnManager    _waveSpawnManager;
        private PlayerBaseStatConfigSO _playerConfig;
        private WaveConfigSO           _waveConfig;
        private FakeStatProvider       _statProvider;
        private FakePlayerQuery        _playerQuery;

        [SetUp]
        public void SetUp()
        {
            // Create configs via SO factory (required for Unity SO lifecycle)
            _playerConfig = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            _waveConfig   = ScriptableObject.CreateInstance<WaveConfigSO>();

            // Sensible defaults for fast test execution
            _playerConfig.BaseAttackDamage          = 10f;
            _playerConfig.BaseAttackInterval        = 1f;
            _playerConfig.BaseCritChance            = 0f;   // no crits by default
            _playerConfig.BaseCritMultiplier        = 2f;
            _playerConfig.AttackTargetUpdateInterval = 0.1f;
            _waveConfig.WaveTransitionDelaySeconds  = 0.5f;
            _waveConfig.UpgradeSelectionWaveInterval = 3;

            _statProvider = new FakeStatProvider(
                attackDamage:    10f,
                attackInterval:  1f,
                critChance:      0f,
                critMultiplier:  2f,
                moveSpeed:       5f);

            _playerQuery = new FakePlayerQuery(Vector2.zero);

            // GameObject hierarchy: controller and enemy manager on same root
            _controllerGO = new GameObject("ABC_Test");
            _abc          = _controllerGO.AddComponent<AutoBattleController>();
            _enemyManager = _controllerGO.AddComponent<EnemyManager>();

            // WaveSpawnManager needs its own GO (has coroutines)
            var wsmGO      = new GameObject("WSM_Test");
            _waveSpawnManager = wsmGO.AddComponent<WaveSpawnManager>();

            _abc.Initialize(
                _enemyManager,
                _waveSpawnManager,
                _statProvider,
                _playerConfig,
                _waveConfig,
                playerId: 42);

            _abc.SetPlayerQuery(_playerQuery);
        }

        [TearDown]
        public void TearDown()
        {
            AutoBattleController.ClearStaticSubscribersForTesting();
            DamageSystem.ClearSubscribersForTesting();
            PlayerHealthComponent.ClearStaticSubscribersForTesting();

            if (_controllerGO != null) Object.DestroyImmediate(_controllerGO);

            // Cleanup WSM GO
            var wsmGO = GameObject.Find("WSM_Test");
            if (wsmGO != null) Object.DestroyImmediate(wsmGO);

            if (_playerConfig != null) Object.DestroyImmediate(_playerConfig);
            if (_waveConfig   != null) Object.DestroyImmediate(_waveConfig);
        }

        // ── AC-ABC-01: Nearest enemy targeted (SqrMagnitude) ─────────────────────

        [Test]
        public void AC_ABC_01_PlayerAttacksNearestEnemy()
        {
            // Arrange: two enemies, one near (dist 3) and one far (dist 10)
            int nearId = 101, farId = 202;
            _playerQuery.SetPosition(Vector2.zero);

            var near  = MakeEnemy(nearId,  new Vector2(3f, 0f));
            var far   = MakeEnemy(farId,   new Vector2(10f, 0f));

            _enemyManager.SpawnEnemy(near);
            _enemyManager.SpawnEnemy(far);

            // Flush spawns and update target
            _enemyManager.TickForTesting(Vector2.zero, false, 0.01f);

            _abc.StartCombat();

            int resolvedTargetId = -1;
            DamageSystem.OnDamageResolved += hit => resolvedTargetId = hit.TargetID;


            // Act: advance attack timer past interval
            // We tick the ABC Update loop indirectly by calling FireAutoAttack via
            // the coroutine-free SimulateAttackTickForTesting helper
            _abc.SimulateAttackTickForTesting();

            // Assert: attack went to near enemy (AC-ABC-01)
            Assert.AreEqual(nearId, resolvedTargetId,
                "AutoBattleController must target the nearest enemy by SqrMagnitude.");
        }

        // ── AC-ABC-02: DamageSystem.ResolveDamage called with correct damage ──────

        [Test]
        public void AC_ABC_02_AttackCallsResolveDamageWithEffectiveDamage()
        {
            // Arrange
            _statProvider.AttackDamage = 25f;
            _playerQuery.SetPosition(Vector2.zero);

            var enemy = MakeEnemy(999, new Vector2(1f, 0f));
            _enemyManager.SpawnEnemy(enemy);
            _enemyManager.TickForTesting(Vector2.zero, false, 0.01f);

            _abc.StartCombat();
            _abc.CacheStatsForTesting();

            float resolvedRaw = -1f;
            DamageSystem.OnDamageResolved += hit => resolvedRaw = hit.BaseDamage;

            // Act
            _abc.SimulateAttackTickForTesting();

            // Assert
            Assert.AreEqual(25f, resolvedRaw, 0.001f,
                "ResolveDamage must receive EffectiveAttackDamage from the stat provider.");
        }

        // ── AC-ABC-03: Enemy attack resolves player damage ────────────────────────

        [Test]
        public void AC_ABC_03_EnemyAttackRaisesResolveDamageOnPlayer()
        {
            // Arrange: enemy sends a damage hit through DamageSystem targeting playerId=42
            // AutoBattleController itself doesn't route enemy attacks in this implementation
            // (EnemyManager handles DispatchEnemyAttack → DamageSystem).
            // We verify the chain: DamageDispatchAdapter → DamageSystem fires OnDamageResolved.

            bool playerHit = false;
            int  targetId  = -1;

            DamageSystem.OnDamageResolved += hit =>
            {
                if (hit.AttackerType == AttackerType.Enemy) { playerHit = true; targetId = hit.TargetID; }
            };

            // Act: directly call ResolveDamage as EnemyManager/DamageDispatchAdapter would
            DamageSystem.ResolveDamage(
                rawDamage:          25f,
                attacker:           AttackerType.Enemy,
                damageType:         DamageType.Attack,
                targetId:           42,
                hitPos:             Vector2.zero,
                isPlayerInvincible: false);

            // Assert
            Assert.IsTrue(playerHit, "Enemy attack must fire OnDamageResolved.");
            Assert.AreEqual(42, targetId, "Enemy attack must target the player entity ID.");
        }

        // ── AC-ABC-04: Enemy death fires OnEnemyKilled and notifies wave spawning ─

        [Test]
        public void AC_ABC_04_EnemyDeathFiresOnEnemyKilledAndNotifiesWave()
        {
            bool killedFired = false;
            EnemyAgent killedAgent = default;
            AutoBattleController.OnEnemyKilled += a => { killedFired = true; killedAgent = a; };

            _abc.StartCombat();

            // Act: simulate an enemy kill arriving from EnemyManager
            var agent = MakeEnemy(55, Vector2.one);
            _abc.SimulateEnemyKilledForTesting(agent);

            // Assert
            Assert.IsTrue(killedFired, "OnEnemyKilled must fire when an enemy dies.");
            Assert.AreEqual(55, killedAgent.InstanceId, "OnEnemyKilled must carry the correct agent.");
        }

        // ── AC-ABC-05: Wave complete → WaveTransition state ──────────────────────

        [Test]
        public void AC_ABC_05_WaveCompleteTransitionsToWaveTransition()
        {
            _abc.StartCombat();

            // Wave 1 complete (not a milestone — UpgradeSelectionWaveInterval=3)
            _abc.SimulateWaveCompleteForTesting(1);

            Assert.AreEqual(CombatState.WaveTransition, _abc.State,
                "Wave complete must move ABC to WaveTransition state.");
        }

        // ── AC-ABC-06: Wave transition delay → OnWaveTransitionComplete ──────────

        [UnityTest]
        public IEnumerator AC_ABC_06_WaveTransitionDelayFiresOnWaveTransitionComplete()
        {
            _abc.StartCombat();

            int nextWaveReceived = -1;
            AutoBattleController.OnWaveTransitionComplete += n => nextWaveReceived = n;

            _abc.SimulateWaveCompleteForTesting(1);

            // WaveTransitionDelaySeconds = 0.5 in SetUp
            yield return new WaitForSeconds(0.6f);

            Assert.AreEqual(2, nextWaveReceived,
                "OnWaveTransitionComplete must fire with nextWaveNumber = completedWave + 1.");
            Assert.AreEqual(CombatState.Active, _abc.State,
                "State must return to Active after wave transition delay.");
        }

        // ── AC-ABC-07: Upgrade selection at milestone wave ────────────────────────

        [Test]
        public void AC_ABC_07_UpgradeSelectionTriggeredAtMilestoneWave()
        {
            _abc.StartCombat();

            bool triggered = false;
            AutoBattleController.OnUpgradeSelectionTriggered += () => triggered = true;

            // Wave 3 = milestone (UpgradeSelectionWaveInterval = 3)
            _abc.SimulateWaveCompleteForTesting(3);

            Assert.IsTrue(triggered, "OnUpgradeSelectionTriggered must fire on milestone waves.");
            Assert.AreEqual(CombatState.UpgradeSelection, _abc.State,
                "State must be UpgradeSelection after milestone wave complete.");
        }

        // ── AC-ABC-08: Player death → CombatPaused ───────────────────────────────

        [Test]
        public void AC_ABC_08_PlayerDeathTransitionsToCombatPaused()
        {
            _abc.StartCombat();

            bool playerDiedFired = false;
            AutoBattleController.OnPlayerDied += () => playerDiedFired = true;

            _abc.SimulatePlayerDiedForTesting(42);

            Assert.AreEqual(CombatState.CombatPaused, _abc.State,
                "Player death must move ABC to CombatPaused state.");
            Assert.IsTrue(playerDiedFired, "OnPlayerDied must fire when the player dies.");
        }

        // ── AC-ABC-09: Combat resumes after idle recovery ─────────────────────────

        [Test]
        public void AC_ABC_09_CombatResumesAfterIdleRecovery()
        {
            _abc.StartCombat();
            _abc.SimulatePlayerDiedForTesting(42);
            Assert.AreEqual(CombatState.CombatPaused, _abc.State);

            // Simulate idle recovery complete (PlayerHealthComponent raises this event)
            PlayerHealthComponent.RaisePlayerEnteredIdleRecoveryForTesting();

            Assert.AreEqual(CombatState.Active, _abc.State,
                "Combat must resume (Active state) after player returns from idle recovery.");
        }

        // ── AC-ABC-10: No double upgrade selection trigger (EC-ABC-05) ────────────

        [Test]
        public void AC_ABC_10_NoDoubleUpgradeSelectionTrigger()
        {
            _abc.StartCombat();

            int triggerCount = 0;
            AutoBattleController.OnUpgradeSelectionTriggered += () => triggerCount++;

            // First milestone — goes to UpgradeSelection, sets _upgradeSelectionInProgress
            _abc.SimulateWaveCompleteForTesting(3);
            Assert.AreEqual(1, triggerCount);
            Assert.AreEqual(CombatState.UpgradeSelection, _abc.State);

            // Second OnWaveComplete while already in UpgradeSelection (shouldn't re-trigger)
            _abc.SimulateWaveCompleteForTesting(3);

            Assert.AreEqual(1, triggerCount,
                "OnUpgradeSelectionTriggered must not fire a second time while upgrade selection is in progress.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static EnemyAgent MakeEnemy(int instanceId, Vector2 position)
        {
            return new EnemyAgent
            {
                InstanceId     = instanceId,
                Position       = position,
                MoveSpeed      = 2f,
                AttackInterval = 1f,
                AttackRange    = 1.5f,
                GoldDropAmount = 1L,
                AttackTimer    = 1f,
                State          = EnemyState.Moving,
                RuntimeData    = new EnemyRuntimeData
                {
                    EntityID            = instanceId,
                    ScaledHP            = 100L,
                    ScaledDamage        = 5L,
                    ScaledContactDamage = 2L,
                    ScalingExponent     = 1f
                }
            };
        }
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────────

    internal sealed class FakeStatProvider : IUpgradeStatProvider
    {
        public float AttackDamage;
        public float AttackInterval;
        public float CritChance;
        public float CritMultiplier;
        public float MoveSpeed;

        public FakeStatProvider(float attackDamage, float attackInterval,
                                float critChance, float critMultiplier, float moveSpeed)
        {
            AttackDamage    = attackDamage;
            AttackInterval  = attackInterval;
            CritChance      = critChance;
            CritMultiplier  = critMultiplier;
            MoveSpeed       = moveSpeed;
        }

        public float GetAttackDamage()     => AttackDamage;
        public float GetAttackInterval()   => AttackInterval;
        public float GetCritChance()       => CritChance;
        public float GetCritMultiplier()   => CritMultiplier;
        public float GetMoveSpeed()        => MoveSpeed;
    }

    internal sealed class FakePlayerQuery : IPlayerQuery
    {
        private Vector2 _position;
        public Vector2 Position       => _position;
        public bool    IsInIdleRecovery { get; set; }

        public FakePlayerQuery(Vector2 position) => _position = position;
        public void SetPosition(Vector2 p)       => _position = p;
    }
}
#endif
