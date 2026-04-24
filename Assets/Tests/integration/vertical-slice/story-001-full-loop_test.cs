// Vertical Slice Integration Test — Story 001: Full Core Loop
// Sprint: S4-08
// GDD: design/gdd/auto-battle-combat.md, design/gdd/economy-system.md
// ACs: VS-INT-01 through VS-INT-06
//
// Validates the complete boot → wave → damage → kill → economy → upgrade chain
// using direct system wiring (no Addressables, no scene loading).
// All helpers are guarded by #if UNITY_EDITOR || DEVELOPMENT_BUILD.

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Damage;
using EndlessEngine.Economy;
using EndlessEngine.Enemy;
using EndlessEngine.Health;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Wave;

namespace EndlessEngine.Tests.Integration.VerticalSlice
{
    /// <summary>
    /// Vertical slice integration test — validates the complete core loop chain:
    ///   Boot → ConfigRegistry loaded → EconomyService initialized
    ///   → WaveSpawnManager.SimulateWaveStartForTesting(1) → OnWaveStarted(1)
    ///   → DamageSystem.ResolveDamage → HealthComponent.CurrentHP reduced
    ///   → OnEnemyKilled fired → EconomyService.CurrentResources increased
    ///   → OnWaveComplete(1) fires when wave done
    ///   → OnUpgradeSelectionTriggered fires at wave 3
    ///
    /// Per Sprint S4-08 acceptance criteria (VS-INT-01 through VS-INT-06).
    /// </summary>
    [TestFixture]
    public class Story001FullLoopTests
    {
        // ── Fixtures ──────────────────────────────────────────────────────────────

        private GameObject       _economyGO;
        private EconomyService   _economyService;

        private GameObject       _enemyManagerGO;
        private EnemyManager     _enemyManager;

        private GameObject       _wsmGO;
        private WaveSpawnManager _waveSpawnManager;

        // Config SOs
        private EconomyConfigSO        _economyConfig;
        private WaveConfigSO           _waveConfig;
        private EnemyStatConfigSO      _enemyConfig;
        private PlayerBaseStatConfigSO _playerConfig;

        // Fakes
        private FakeUpgradeTreeQueryVS _upgradeTreeQuery;

        [SetUp]
        public void SetUp()
        {
            // ── Config SOs ────────────────────────────────────────────────────────
            _economyConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            _economyConfig.StartingGold    = 100L;
            _economyConfig.ResourceHardCap = 999_999_999L;

            _waveConfig = ScriptableObject.CreateInstance<WaveConfigSO>();
            _waveConfig.UpgradeSelectionWaveInterval = 3;
            _waveConfig.WaveTransitionDelaySeconds   = 0f;
            _waveConfig.WaveSaveMilestoneInterval    = 10;
            _waveConfig.TotalWavesPerRun             = -1; // infinite mode

            _enemyConfig = ScriptableObject.CreateInstance<EnemyStatConfigSO>();
            _enemyConfig.BaseMaxHP         = 50f;
            _enemyConfig.BaseAttackDamage  = 5f;
            _enemyConfig.BaseContactDamage = 2f;
            _enemyConfig.MoveSpeed         = 0f; // stationary for determinism

            _playerConfig = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            _playerConfig.BaseAttackDamage   = 10f;
            _playerConfig.BaseAttackInterval = 1f;
            _playerConfig.BaseCritChance     = 0f;
            _playerConfig.BaseCritMultiplier = 2f;

            // ── ConfigRegistry: inject all required SOs ───────────────────────────
            ConfigRegistry.InjectForTesting(
                enemy:    _enemyConfig,
                wave:     _waveConfig,
                economy:  _economyConfig,
                player:   _playerConfig,
                upgrades: new UpgradeNodeConfigSO[0]
            );

            // ── Fakes ─────────────────────────────────────────────────────────────
            _upgradeTreeQuery = new FakeUpgradeTreeQueryVS();

            // ── EconomyService ────────────────────────────────────────────────────
            _economyGO      = new GameObject("VS_Economy");
            _economyService = _economyGO.AddComponent<EconomyService>();
            _economyService.Initialize(_upgradeTreeQuery, saveNotifier: null);

            // Simulate save load (new-game path: SchemaVersion=0, CurrentResources=0 → StartingGold)
            var newGameSave = new SaveData { SchemaVersion = 0, CurrentResources = 0 };
            _economyService.OnAfterLoad(newGameSave);

            // ── EnemyManager ──────────────────────────────────────────────────────
            _enemyManagerGO = new GameObject("VS_EnemyManager");
            _enemyManager   = _enemyManagerGO.AddComponent<EnemyManager>();
            _enemyManager.Initialize(
                playerQuery:      new FakePlayerQueryVS(Vector2.zero),
                damageDispatcher: new FakeDamageDispatcherVS()
            );

            // ── WaveSpawnManager ──────────────────────────────────────────────────
            _wsmGO            = new GameObject("VS_WaveSpawnManager");
            _waveSpawnManager = _wsmGO.AddComponent<WaveSpawnManager>();
            _waveSpawnManager.Initialize(_enemyManager, saveNotifier: null);

            var waveSave = new SaveData { WaveNumber = 1 };
            _waveSpawnManager.OnAfterLoad(waveSave);

            // ── Wire economy to enemy kills ───────────────────────────────────────
            EnemyManager.OnEnemyKilled += OnEnemyKilledHandler;
        }

        [TearDown]
        public void TearDown()
        {
            EnemyManager.OnEnemyKilled -= OnEnemyKilledHandler;

            WaveSpawnManager.ClearStaticSubscribersForTesting();
            EnemyManager.ClearStaticSubscribersForTesting();
            DamageSystem.ClearSubscribersForTesting();
            ConfigRegistry.ClearForTesting();

            if (_economyGO      != null) Object.DestroyImmediate(_economyGO);
            if (_enemyManagerGO != null) Object.DestroyImmediate(_enemyManagerGO);
            if (_wsmGO          != null) Object.DestroyImmediate(_wsmGO);

            if (_economyConfig != null) Object.DestroyImmediate(_economyConfig);
            if (_waveConfig    != null) Object.DestroyImmediate(_waveConfig);
            if (_enemyConfig   != null) Object.DestroyImmediate(_enemyConfig);
            if (_playerConfig  != null) Object.DestroyImmediate(_playerConfig);
        }

        private void OnEnemyKilledHandler(EnemyAgent agent)
        {
            _economyService.AddResources(agent.GoldDropAmount);
        }

        // ── VS-INT-01: Boot chain ─────────────────────────────────────────────────

        [Test]
        public void VS_INT_01_BootChain_ConfigRegistryAndEconomyInitialized()
        {
            // Assert: ConfigRegistry populated (InjectForTesting sets IsLoaded = true)
            Assert.IsTrue(ConfigRegistry.IsLoaded,
                "ConfigRegistry must be loaded after boot (InjectForTesting sets IsLoaded).");

            // Assert: EconomyService initialized with starting gold
            Assert.AreEqual(
                _economyConfig.StartingGold,
                _economyService.CurrentResources,
                "EconomyService must initialize to StartingGold on new-game save load.");
        }

        // ── VS-INT-02: OnWaveStarted(1) fires when first wave begins ─────────────

        [Test]
        public void VS_INT_02_WaveStart_FiresOnWaveStarted_WithWaveOne()
        {
            // Arrange
            int waveStartedArg = -1;
            WaveSpawnManager.OnWaveStarted += w => waveStartedArg = w;

            // Act: fire wave start synchronously via test helper
            _waveSpawnManager.SimulateWaveStartForTesting(1);

            // Assert
            Assert.AreEqual(1, waveStartedArg,
                "OnWaveStarted must fire with wave number 1 when the first wave begins.");
        }

        // ── VS-INT-03: DamageSystem.ResolveDamage → HealthComponent.HP reduced ────

        [Test]
        public void VS_INT_03_ResolveDamage_ReducesEnemyHP()
        {
            // Arrange: enemy health component with known HP
            var health = new HealthComponent();
            health.Initialize(entityId: 1001, maxHP: 50f, deathVFXTag: "enemy_death");

            DamageSystem.OnDamageResolved += hit =>
            {
                if (hit.TargetID == 1001)
                    health.ApplyDamage(hit.FinalDamage, hit.HitPosition);
            };

            float hpBefore = health.CurrentHP;

            // Act
            DamageSystem.ResolveDamage(
                rawDamage:          20f,
                attacker:           AttackerType.Player,
                damageType:         DamageType.Attack,
                targetId:           1001,
                hitPos:             Vector2.zero,
                isPlayerInvincible: false);

            // Assert
            Assert.Less(health.CurrentHP, hpBefore,
                "DamageSystem.ResolveDamage must reduce the enemy's HP.");
            Assert.AreEqual(30f, health.CurrentHP, 0.01f,
                "50 HP enemy taking 20 damage should have 30 HP remaining.");
        }

        // ── VS-INT-04: Enemy HP=0 → OnEnemyKilled → EconomyService.CurrentResources++

        [Test]
        public void VS_INT_04_EnemyKilled_IncreasesEconomyResources()
        {
            // Arrange: enemy with known gold drop
            var agent = new EnemyAgent
            {
                InstanceId     = 2001,
                GoldDropAmount = 25L,
                State          = EnemyState.Dead,
            };

            long resourcesBefore = _economyService.CurrentResources;

            // Act: fire OnEnemyKilled directly (simulates enemy HP reaching 0)
            EnemyManager.FireEnemyKilledForTesting(agent);

            // Assert
            Assert.AreEqual(resourcesBefore + 25L, _economyService.CurrentResources,
                "OnEnemyKilled must cause EconomyService to add the enemy's gold drop to CurrentResources.");
        }

        // ── VS-INT-05: All enemies dead → OnWaveComplete(1) fires ────────────────

        [Test]
        public void VS_INT_05_AllEnemiesDead_FiresOnWaveComplete()
        {
            // Arrange
            int waveCompleteArg = -1;
            WaveSpawnManager.OnWaveComplete += w => waveCompleteArg = w;

            // Act: simulate wave 1 clear (all enemies dead)
            _waveSpawnManager.SimulateWaveClearForTesting(1);

            // Assert
            Assert.AreEqual(1, waveCompleteArg,
                "OnWaveComplete must fire with wave number 1 when all wave-1 enemies are dead.");
        }

        // ── VS-INT-06: Wave 3 complete → OnUpgradeSelectionTriggered fires ────────

        [Test]
        public void VS_INT_06_Wave3Complete_FiresOnUpgradeSelectionTriggered()
        {
            // Arrange
            bool upgradeTriggered = false;
            WaveSpawnManager.OnUpgradeSelectionTriggered += () => upgradeTriggered = true;

            // Act: simulate wave 3 completion (UpgradeSelectionWaveInterval = 3)
            _waveSpawnManager.SimulateWaveClearForTesting(3);

            // Assert
            Assert.IsTrue(upgradeTriggered,
                "OnUpgradeSelectionTriggered must fire after wave 3 completes (UpgradeSelectionWaveInterval = 3).");
        }

        // ── Fakes ─────────────────────────────────────────────────────────────────

        private class FakeUpgradeTreeQueryVS : IUpgradeTreeQuery
        {
            public long GetNodeCost(string nodeId) => 100L;
        }

        private class FakePlayerQueryVS : IPlayerQuery
        {
            private readonly Vector2 _pos;
            public FakePlayerQueryVS(Vector2 pos) => _pos = pos;
            public Vector2 Position        => _pos;
            public bool    IsInIdleRecovery => false;
        }

        private class FakeDamageDispatcherVS : IDamageDispatcher
        {
            public void DispatchEnemyAttack(EnemyAgent agent, Vector2 playerPosition) { }
        }
    }
}
#endif
