// Unit Tests: AutoBattleController — State Machine
// Type: Unit (EditMode)
//
// AC-ABC-STATE-01: StartCombat transitions Inactive → Active
// AC-ABC-STATE-02: StartCombat is idempotent (double call stays Active)
// AC-ABC-STATE-03: HandlePlayerDied transitions Active → CombatPaused
// AC-ABC-STATE-04: HandlePlayerDied ignores wrong entity IDs
// AC-ABC-STATE-05: HandlePlayerDied is no-op when already CombatPaused
// AC-ABC-STATE-06: SimulateWaveComplete transitions Active → WaveTransition
// AC-ABC-STATE-07: SimulateWaveComplete is no-op when not Active
// AC-ABC-STATE-08: UpgradeSelectionWaveInterval triggers UpgradeSelection state
// AC-ABC-STATE-09: Upgrade selection guard prevents double-trigger (EC-ABC-05)
// AC-ABC-STATE-10: NotifyUpgradeSelected resumes from UpgradeSelection
// AC-ABC-STATE-11: SimulateWaveTransitionComplete restores Active + fires event
// AC-ABC-STATE-12: HandleEnemyKilled is no-op while CombatPaused

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Combat;
using EndlessEngine.Config;
using EndlessEngine.Damage;
using EndlessEngine.Enemy;
using EndlessEngine.Wave;
using EndlessEngine.Health;

namespace EndlessEngine.Tests.Unit.AutoBattle
{
    [TestFixture]
    public class AutoBattleControllerStateMachineTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private GameObject             _go;
        private AutoBattleController   _abc;
        private PlayerBaseStatConfigSO _playerConfig;
        private WaveConfigSO           _waveConfig;
        private FakeStatProvider       _stats;

        [SetUp]
        public void SetUp()
        {
            _playerConfig = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            _playerConfig.BaseAttackDamage           = 10f;
            _playerConfig.BaseMaxHP                  = 100f;
            _playerConfig.BaseAttackInterval         = 1f;
            _playerConfig.BaseCritChance             = 0f;
            _playerConfig.BaseCritMultiplier         = 2f;
            _playerConfig.AttackTargetUpdateInterval = 0.1f;

            _waveConfig = ScriptableObject.CreateInstance<WaveConfigSO>();
            _waveConfig.WaveTransitionDelaySeconds   = 0f;
            _waveConfig.UpgradeSelectionWaveInterval = 5;

            _stats = new FakeStatProvider(
                attackDamage:   10f,
                attackInterval: 1f,
                critChance:     0f,
                critMultiplier: 2f,
                moveSpeed:      5f);

            ConfigRegistry.InjectForTesting(player: _playerConfig);

            _go  = new GameObject("ABC_Unit");
            _abc = _go.AddComponent<AutoBattleController>();

            var enemyMgr = _go.AddComponent<EnemyManager>();
            var wsmGo    = new GameObject("WSM_Unit");
            var wsm      = wsmGo.AddComponent<WaveSpawnManager>();

            _abc.Initialize(enemyMgr, wsm, _stats, _playerConfig, _waveConfig, playerId: 1);
            _abc.SetPlayerQuery(new FakePlayerQuery(Vector2.zero));
        }

        [TearDown]
        public void TearDown()
        {
            ConfigRegistry.ClearForTesting();
            AutoBattleController.ClearStaticSubscribersForTesting();
            DamageSystem.ClearSubscribersForTesting();
            PlayerHealthComponent.ClearStaticSubscribersForTesting();

            if (_go != null) Object.DestroyImmediate(_go);
            var wsmGo = GameObject.Find("WSM_Unit");
            if (wsmGo != null) Object.DestroyImmediate(wsmGo);
            if (_playerConfig != null) Object.DestroyImmediate(_playerConfig);
            if (_waveConfig   != null) Object.DestroyImmediate(_waveConfig);
        }

        // ── State transitions ─────────────────────────────────────────────────────

        [Test]
        [Description("AC-ABC-STATE-01: StartCombat transitions Inactive → Active")]
        public void StartCombat_FromInactive_BecomesActive()
        {
            Assert.AreEqual(CombatState.Inactive, _abc.State);
            _abc.StartCombat();
            Assert.AreEqual(CombatState.Active, _abc.State);
        }

        [Test]
        [Description("AC-ABC-STATE-02: StartCombat is idempotent")]
        public void StartCombat_CalledTwice_StaysActive()
        {
            _abc.StartCombat();
            _abc.StartCombat();
            Assert.AreEqual(CombatState.Active, _abc.State);
        }

        [Test]
        [Description("AC-ABC-STATE-03: HandlePlayerDied with correct ID → CombatPaused")]
        public void HandlePlayerDied_CorrectId_PausesCombat()
        {
            _abc.StartCombat();
            _abc.SimulatePlayerDiedForTesting(entityId: 1);
            Assert.AreEqual(CombatState.CombatPaused, _abc.State);
        }

        [Test]
        [Description("AC-ABC-STATE-04: HandlePlayerDied with wrong ID is no-op")]
        public void HandlePlayerDied_WrongId_NoStateChange()
        {
            _abc.StartCombat();
            _abc.SimulatePlayerDiedForTesting(entityId: 999);
            Assert.AreEqual(CombatState.Active, _abc.State);
        }

        [Test]
        [Description("AC-ABC-STATE-05: HandlePlayerDied is no-op when already CombatPaused")]
        public void HandlePlayerDied_AlreadyPaused_NoEvent()
        {
            _abc.StartCombat();
            _abc.SimulatePlayerDiedForTesting(entityId: 1);

            int firedCount = 0;
            AutoBattleController.OnPlayerDied += () => firedCount++;
            _abc.SimulatePlayerDiedForTesting(entityId: 1);

            Assert.AreEqual(0, firedCount);
        }

        [Test]
        [Description("AC-ABC-STATE-06: WaveComplete transitions Active → WaveTransition")]
        public void WaveComplete_FromActive_EntersWaveTransition()
        {
            _abc.StartCombat();
            _abc.SimulateWaveCompleteForTesting(waveNumber: 1);
            // Wave 1 is not an upgrade milestone (interval=5), so WaveTransition
            Assert.AreEqual(CombatState.WaveTransition, _abc.State);
        }

        [Test]
        [Description("AC-ABC-STATE-07: WaveComplete is no-op when not Active")]
        public void WaveComplete_FromInactive_NoStateChange()
        {
            _abc.SimulateWaveCompleteForTesting(waveNumber: 1);
            Assert.AreEqual(CombatState.Inactive, _abc.State);
        }

        [Test]
        [Description("AC-ABC-STATE-08: Wave at upgrade interval triggers UpgradeSelection state")]
        public void WaveComplete_UpgradeMilestoneWave_EntersUpgradeSelection()
        {
            _abc.StartCombat();
            _abc.SimulateWaveCompleteForTesting(waveNumber: _waveConfig.UpgradeSelectionWaveInterval);
            Assert.AreEqual(CombatState.UpgradeSelection, _abc.State);
        }

        [Test]
        [Description("AC-ABC-STATE-08: OnUpgradeSelectionTriggered fires at milestone wave")]
        public void WaveComplete_UpgradeMilestoneWave_FiresUpgradeSelectionEvent()
        {
            _abc.StartCombat();
            int fired = 0;
            AutoBattleController.OnUpgradeSelectionTriggered += () => fired++;
            _abc.SimulateWaveCompleteForTesting(waveNumber: _waveConfig.UpgradeSelectionWaveInterval);
            Assert.AreEqual(1, fired);
        }

        [Test]
        [Description("AC-ABC-STATE-09: Double wave complete during upgrade selection does not double-trigger (EC-ABC-05)")]
        public void WaveComplete_DuringUpgradeSelection_DoesNotDoubleTrigger()
        {
            _abc.StartCombat();
            _abc.SimulateWaveCompleteForTesting(waveNumber: _waveConfig.UpgradeSelectionWaveInterval);
            Assert.AreEqual(CombatState.UpgradeSelection, _abc.State);

            int fired = 0;
            AutoBattleController.OnUpgradeSelectionTriggered += () => fired++;
            _abc.SimulateWaveCompleteForTesting(waveNumber: _waveConfig.UpgradeSelectionWaveInterval);
            Assert.AreEqual(0, fired, "Guard must prevent re-triggering upgrade selection.");
        }

        [Test]
        [Description("AC-ABC-STATE-10: NotifyUpgradeSelected from UpgradeSelection resumes to WaveTransition")]
        public void NotifyUpgradeSelected_FromUpgradeSelection_ResumesTransition()
        {
            _abc.StartCombat();
            _abc.SimulateWaveCompleteForTesting(waveNumber: _waveConfig.UpgradeSelectionWaveInterval);
            Assert.AreEqual(CombatState.UpgradeSelection, _abc.State);

            _abc.NotifyUpgradeSelected();
            // After NotifyUpgradeSelected, BeginWaveTransition runs synchronously
            // (delay=0 in test config → SimulateWaveTransitionComplete is needed to fully clear)
            Assert.AreNotEqual(CombatState.UpgradeSelection, _abc.State,
                "State must leave UpgradeSelection after upgrade is selected.");
        }

        [Test]
        [Description("AC-ABC-STATE-11: SimulateWaveTransitionComplete restores Active and fires event")]
        public void WaveTransitionComplete_RestoresActiveAndFiresEvent()
        {
            _abc.StartCombat();
            _abc.SimulateWaveCompleteForTesting(waveNumber: 1);

            int nextWave = -1;
            AutoBattleController.OnWaveTransitionComplete += w => nextWave = w;
            _abc.SimulateWaveTransitionCompleteForTesting(completedWave: 1);

            Assert.AreEqual(CombatState.Active, _abc.State);
            Assert.AreEqual(2, nextWave);
        }

        [Test]
        [Description("AC-ABC-STATE-12: Enemy kill events during CombatPaused do not fire OnEnemyKilled")]
        public void EnemyKilled_WhilePaused_DoesNotFireEvent()
        {
            _abc.StartCombat();
            _abc.SimulatePlayerDiedForTesting(entityId: 1);

            int fired = 0;
            AutoBattleController.OnEnemyKilled += _ => fired++;

            var dummy = new EnemyAgent { InstanceId = 99, State = EnemyState.Dead };
            _abc.SimulateEnemyKilledForTesting(dummy);

            Assert.AreEqual(0, fired, "Enemy kill events must be silent while combat is paused.");
        }

#endif
    }

    // ── Test doubles ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal class FakeStatProvider : IUpgradeStatProvider
    {
        private readonly float _dmg, _interval, _crit, _critMult, _speed;
        public FakeStatProvider(float attackDamage, float attackInterval,
            float critChance, float critMultiplier, float moveSpeed)
        {
            _dmg = attackDamage; _interval = attackInterval;
            _crit = critChance; _critMult = critMultiplier; _speed = moveSpeed;
        }
        public float GetAttackDamage()    => _dmg;
        public float GetAttackInterval()  => _interval;
        public float GetCritChance()      => _crit;
        public float GetCritMultiplier()  => _critMult;
        public float GetMoveSpeed()       => _speed;
    }

    internal class FakePlayerQuery : IPlayerQuery
    {
        private Vector2 _pos;
        public FakePlayerQuery(Vector2 pos) => _pos = pos;
        public Vector2 Position => _pos;
        public bool IsInIdleRecovery => false;
        public void SetPosition(Vector2 p) => _pos = p;
    }
#endif
}
