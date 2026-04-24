using System.Collections;
using UnityEngine;
using EndlessEngine.Combat;
using EndlessEngine.Config;
using EndlessEngine.Damage;
using EndlessEngine.Economy;
using EndlessEngine.Enemy;
using EndlessEngine.Flow;
using EndlessEngine.Generator;
using EndlessEngine.Health;
using EndlessEngine.Input;
using EndlessEngine.Offline;
using EndlessEngine.Physics;
using EndlessEngine.Prestige;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Milestone;
using EndlessEngine.Modules;
// AscensionStateManager is in EndlessEngine.Prestige (same namespace as PrestigeStateManager)
using EndlessEngine.Upgrade;
using EndlessEngine.UI;
using EndlessEngine.Wave;
using EndlessEngine.Statistics;
using EndlessEngine.Challenge;
using EndlessEngine.Research;
using EndlessEngine.Minigame;
using EndlessEngine.Building;
using EndlessEngine.Pet;
using EndlessEngine.UnlockLog;
using EndlessEngine.Events;
using EndlessEngine.Leaderboard;
using EndlessEngine.Export;

namespace EndlessEngine.Bootstrap
{
    /// <summary>
    /// Vertical Slice scene bootstrap — wires all gameplay systems together.
    /// Full core loop: Menu → Run (timed arena) → PostRun → Menu.
    ///
    /// THIS IS NOT THE PRODUCTION BOOTSTRAP.
    /// Uses ScriptableObject direct references (InjectForTesting) — no Addressables.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class VerticalSliceBootstrap : MonoBehaviour
    {
        // ── Scene references (assign in Inspector) ────────────────────────────────

        [Header("Core Services")]
        [SerializeField] private SaveService            _saveService;
        [SerializeField] private EconomyService         _economyService;
        [SerializeField] private WaveSpawnManager       _waveSpawnManager;
        [SerializeField] private EnemyManager           _enemyManager;
        [SerializeField] private UpgradeTreeService     _upgradeTreeService;
        [SerializeField] private HealthSystem           _healthSystem;
        [SerializeField] private OfflineTimeCalculator  _offlineCalculator;
        [SerializeField] private DamageDispatchAdapter  _damageDispatchAdapter;
        [SerializeField] private PrestigeStateManager   _prestigeManager;

        [Header("Engine Core — Flow")]
        [SerializeField] private GameFlowStateMachine   _gameFlow;
        [SerializeField] private RunSessionManager      _runSessionManager;
        [SerializeField] private TickEngine             _tickEngine;

        [Header("Engine Core — Generators")]
        [SerializeField] private GeneratorSystem        _generatorSystem;
        [SerializeField] private PassiveIncomeService   _passiveIncomeService;
        [SerializeField] private GeneratorDatabaseSO    _generatorDatabase;

        [Header("Player")]
        [SerializeField] private PlayerHealthComponent  _playerHealth;
        [SerializeField] private PlayerMovementSystem   _playerMovement;
        [SerializeField] private InputProviderUnity     _inputProvider;
        [SerializeField] private AutoBattleController   _autoBattle;

        [Header("Optional Modules (leave unassigned to disable)")]
        [SerializeField] private CursorYieldService      _cursorYieldService;
        [SerializeField] private ClickYieldService       _clickYieldService;
        [SerializeField] private CurrencyService         _currencyService;
        [SerializeField] private CurrencyDatabaseSO      _currencyDatabase;
        [SerializeField] private ConversionService       _conversionService;
        [SerializeField] private ConversionDatabaseSO    _conversionDatabase;
        [SerializeField] private MilestoneTracker        _milestoneTracker;
        [SerializeField] private MilestoneDatabaseSO     _milestoneDatabase;
        [SerializeField] private ProgressETAService      _progressEtaService;
        [SerializeField] private AscensionStateManager   _ascensionManager;
        [SerializeField] private AscensionDatabaseSO     _ascensionDatabase;
        [SerializeField] private InventoryService        _inventoryService;
        [SerializeField] private ItemConfigSO[]          _itemDatabase;
        [SerializeField] private SkillTreeService        _skillTreeService;
        [SerializeField] private SkillTreeConfigSO[]     _skillTreeConfigs;
        [SerializeField] private StatisticsService       _statisticsService;
        [SerializeField] private StatDefinitionSO[]      _statDefinitions;
        [SerializeField] private TimeBoostService        _timeBoostService;
        [SerializeField] private ChallengeService        _challengeService;
        [SerializeField] private ResearchService         _researchService;
        [SerializeField] private ResearchTreeConfigSO[]  _researchTrees;
        [SerializeField] private MergeService            _mergeService;
        [SerializeField] private MergeConfigSO[]         _mergeConfigs;
        [SerializeField] private MinigameService         _minigameService;
        [SerializeField] private ActiveSkillConfigSO[]   _activeSkills;
        [SerializeField] private BuildingService         _buildingService;
        [SerializeField] private BuildingConfigSO[]      _buildingConfigs;
        [SerializeField] private PetService              _petService;
        [SerializeField] private PetConfigSO[]           _petConfigs;
        [SerializeField] private UnlockLogService        _unlockLogService;
        [SerializeField] private UnlockEntryConfigSO[]   _unlockEntries;
        [SerializeField] private EventService            _eventService;
        [SerializeField] private EventScheduleConfigSO[] _eventSchedules;
        [SerializeField] private LeaderboardService      _leaderboardService;
        [SerializeField] private LeaderboardConfigSO[]   _leaderboardConfigs;
        [SerializeField] private ExportService           _exportService;

        [Header("UI")]
        [SerializeField] private UpgradeScreenController   _upgradeScreen;
        [SerializeField] private BuildingScreenController  _buildingScreen;
        [SerializeField] private PetScreenController       _petScreen;
        [SerializeField] private UnlockLogScreenController _unlockLogScreen;
        [SerializeField] private EventBannerController     _eventBanner;
        [SerializeField] private LeaderboardScreenController _leaderboardScreen;
        [SerializeField] private ExportDialogController    _exportDialog;

        [Header("VS Config (direct references — no Addressables)")]
        [SerializeField] private EconomyConfigSO        _economyConfig;
        [SerializeField] private WaveConfigSO            _waveConfig;
        [SerializeField] private EnemyStatConfigSO       _enemyConfig;
        [SerializeField] private PlayerBaseStatConfigSO  _playerConfig;
        [SerializeField] private RunConfigSO             _runConfig;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            Debug.Log("[VS Bootstrap] Wiring systems...");

            // 0. Populate ConfigRegistry (VS uses InjectForTesting — no Addressables)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ConfigRegistry.InjectForTesting(
                enemy:   _enemyConfig,
                wave:    _waveConfig,
                economy: _economyConfig,
                player:  _playerConfig,
                run:     _runConfig);
#endif

            // 1. Player health init (ConfigRegistry now populated)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _playerHealth?.InitialiseFromConfigForTesting();
#endif

            // 1b. Player movement
            if (_playerMovement != null && _inputProvider != null)
                _playerMovement.Initialize(_inputProvider);

            // 2. AutoBattleController
            if (_autoBattle != null && _playerConfig != null)
            {
                var statProvider = new BaseStatUpgradeProvider(_playerConfig);
                WaveConfigSO waveConfig;
                try { waveConfig = ConfigRegistry.Wave; } catch { waveConfig = null; }
                _autoBattle.Initialize(
                    enemyManager:     _enemyManager,
                    waveSpawnManager: _waveSpawnManager,
                    statProvider:     statProvider,
                    playerConfig:     _playerConfig,
                    waveConfig:       waveConfig,
                    playerId:         _playerHealth != null ? _playerHealth.GetEntityIdForTesting() : 0
                );
                _autoBattle.SetPlayerQuery(_playerHealth);
                EnemyManager.OnEnemyKilled += _autoBattle.HandleEnemyKilledByManager;
                EnemyManager.OnEnemyKilled += OnEnemyKilledAddGold;
                EnemyManager.OnEnemyKilled += OnEnemyKilledDestroyGO;
                PlayerHealthComponent.OnEntityDied += _autoBattle.HandlePlayerDied;
                PlayerHealthComponent.OnPlayerEnteredIdleRecovery += OnPlayerEnteredIdleRecovery;
            }

            // 3. Economy dependencies
            _economyService?.Initialize(
                upgradeTreeQuery: _upgradeTreeService,
                saveNotifier:     _saveService
            );

            // 4. Generator system
            if (_generatorSystem != null && _economyService != null)
            {
                var configs = _generatorDatabase != null
                    ? _generatorDatabase.Generators
                    : new GeneratorConfigSO[0];
                _generatorSystem.Initialize(
                    configs:      configs,
                    economy:      _economyService,
                    saveNotifier: _saveService
                );
            }

            // 5. Passive income
            if (_passiveIncomeService != null)
            {
                _passiveIncomeService.Initialize(
                    generators: _generatorSystem,
                    economy:    _economyService,
                    gameFlow:   _gameFlow
                );
            }

            // 5a. Currency service (optional secondary currencies)
            if (_currencyService != null)
                _currencyService.Initialize(_currencyDatabase);

            // 5aa. Conversion service (optional)
            if (_conversionService != null)
                _conversionService.Initialize(_conversionDatabase, _economyService, _currencyService);

            // 5ab00. Inventory service (optional)
            if (_inventoryService != null)
                _inventoryService.Initialize(_itemDatabase);

            // 5ab000. Skill tree service (optional)
            if (_skillTreeService != null)
                _skillTreeService.Initialize(_skillTreeConfigs);

            // 5ab001. Statistics service (optional)
            if (_statisticsService != null)
                _statisticsService.Initialize(_statDefinitions);

            // 5ab002. Time boost service (optional)
            if (_timeBoostService != null)
                _timeBoostService.Initialize(_tickEngine, _economyService);

            // 5ab003. Challenge service (optional)
            if (_challengeService != null)
                _challengeService.Initialize(_economyService, _upgradeTreeService);

            // 5ab005. Merge service (optional)
            if (_mergeService != null)
                _mergeService.Initialize(_mergeConfigs, _inventoryService, _economyService);

            // 5ab006. Minigame service (optional)
            if (_minigameService != null)
                _minigameService.Initialize(_activeSkills, _economyService);

            // 5ab007. Building service (optional)
            if (_buildingService != null)
                _buildingService.Initialize(_buildingConfigs, _economyService);

            // 5ab008. Pet service (optional)
            if (_petService != null)
                _petService.Initialize(_petConfigs, _economyService);

            // 5ab009. Unlock log service (optional)
            if (_unlockLogService != null)
                _unlockLogService.Initialize(_unlockEntries);

            // 5ab010. Event service (optional)
            if (_eventService != null)
                _eventService.Initialize(_eventSchedules);

            // 5ab011. Leaderboard service (optional)
            if (_leaderboardService != null)
                _leaderboardService.Initialize(_leaderboardConfigs);

            // 5ab012. Export service (optional)
            if (_exportService != null)
                _exportService.Initialize(_saveService);

            // 5ab013. Leaderboard screen — wire CurrentScore from StatisticsService
            if (_leaderboardScreen != null && _statisticsService != null)
                _leaderboardScreen.CurrentScore = (long)_statisticsService.Get("total_gold_earned");

            // 5ab004. Research service (optional)
            if (_researchService != null)
                _researchService.Initialize(_researchTrees, _economyService);

            // 5ab0. Ascension state manager (optional — extends PrestigeStateManager)
            if (_ascensionManager != null && _prestigeManager != null)
                _ascensionManager.Initialize(
                    database:        _ascensionDatabase,
                    prestigeManager: _prestigeManager,
                    saveService:     _saveService,
                    economyService:  _economyService,
                    generatorSystem: _generatorSystem,
                    currencyService: _currencyService
                );

            // 5ab. Milestone tracker (optional)
            if (_milestoneTracker != null && _economyService != null)
                _milestoneTracker.Initialize(
                    database:        _milestoneDatabase,
                    economyService:  _economyService,
                    prestigeManager: _prestigeManager,
                    currencyService: _currencyService,
                    generatorSystem: _generatorSystem
                );

            // 5ac. Progress ETA service (optional)
            if (_progressEtaService != null && _economyService != null)
                _progressEtaService.Initialize(_economyService, _generatorSystem);

            // 5b. Cursor yield module (optional)
            if (_cursorYieldService != null && _economyService != null)
            {
                _cursorYieldService.Initialize(
                    config:    null,   // assign CursorActivityConfigSO in Inspector
                    economy:   _economyService,
                    gameFlow:  _gameFlow,
                    input:     _inputProvider
                );
            }

            // 5c. Click yield module (optional)
            if (_clickYieldService != null && _economyService != null)
            {
                _clickYieldService.Initialize(
                    config:              null,  // assign ClickSourceConfigSO in Inspector
                    economy:             _economyService,
                    passiveYieldGetter:  null   // wire to a yield/s getter if YieldRateClickFraction > 0
                );
                if (_inputProvider != null)
                    _clickYieldService.SetInputProvider(_inputProvider);
            }

            // 6. Run session manager
            if (_runSessionManager != null && _gameFlow != null)
                _runSessionManager.Initialize(_gameFlow);

            // 7. GameFlow → WaveSpawnManager integration
            if (_gameFlow != null && _waveSpawnManager != null)
            {
                GameFlowStateMachine.OnEnteredRun     += OnEnteredRun;
                GameFlowStateMachine.OnEnteredPostRun += OnEnteredPostRun;
            }

            // 7b. Wave number → MilestoneTracker
            if (_milestoneTracker != null)
                WaveSpawnManager.OnWaveStarted += OnWaveStartedForMilestone;

            // 7c. TickEngine → ResearchService
            if (_researchService != null)
                TickEngine.OnTick += _researchService.OnTick;

            // 7d. TickEngine → BuildingService
            if (_buildingService != null)
                TickEngine.OnTick += _buildingService.OnTick;

            // 8. Register ISaveStateProviders
            if (_saveService != null)
            {
                _saveService.RegisterStateProvider(_economyService);
                _saveService.RegisterStateProvider(_upgradeTreeService);
                _saveService.RegisterStateProvider(_prestigeManager);
                _saveService.RegisterStateProvider(_waveSpawnManager);
                if (_generatorSystem != null)
                    _saveService.RegisterStateProvider(_generatorSystem);
                if (_upgradeScreen != null)
                    _saveService.RegisterStateProvider(_upgradeScreen);
                if (_clickYieldService != null)
                    _saveService.RegisterStateProvider(_clickYieldService);
                if (_currencyService != null)
                    _saveService.RegisterStateProvider(_currencyService);
                if (_milestoneTracker != null)
                    _saveService.RegisterStateProvider(_milestoneTracker);
                if (_ascensionManager != null)
                    _saveService.RegisterStateProvider(_ascensionManager);
                if (_inventoryService != null)
                    _saveService.RegisterStateProvider(_inventoryService);
                if (_skillTreeService != null)
                    _saveService.RegisterStateProvider(_skillTreeService);
                if (_statisticsService != null)
                    _saveService.RegisterStateProvider(_statisticsService);
                if (_researchService != null)
                    _saveService.RegisterStateProvider(_researchService);
                if (_buildingService != null)
                    _saveService.RegisterStateProvider(_buildingService);
                if (_petService != null)
                    _saveService.RegisterStateProvider(_petService);
                if (_unlockLogService != null)
                    _saveService.RegisterStateProvider(_unlockLogService);
            }

            // 9. Wave spawning dependencies
            _waveSpawnManager?.Initialize(_enemyManager, null, _healthSystem);

            // 10. Enemy manager dependencies
            _enemyManager?.Initialize(
                playerQuery:      _playerHealth,
                damageDispatcher: _damageDispatchAdapter
            );

            // 11. Wire HealthSystem.OnEntityDied → EnemyManager.MarkDead
            if (_healthSystem != null && _enemyManager != null)
                HealthSystem.OnEntityDied += OnEntityDied;

            // 12. Load save data and start
            yield return StartCoroutine(LoadSaveAndStart());
        }

        private IEnumerator LoadSaveAndStart()
        {
            if (_saveService == null)
            {
                Debug.LogWarning("[VS Bootstrap] No SaveService — using mock save data.");
                var mockSave = new SaveData
                {
                    SchemaVersion    = 0,
                    WaveNumber       = 1,
                    GeneratorStates  = new System.Collections.Generic.Dictionary<string, GeneratorState>()
                };
                _economyService?.OnAfterLoad(mockSave);
                _upgradeTreeService?.OnAfterLoad(mockSave);
                _waveSpawnManager?.OnAfterLoad(mockSave);
                _generatorSystem?.OnAfterLoad(mockSave);
                yield return null;
            }
            else
            {
                bool done = false;
                _ = _saveService.LoadAsync().ContinueWith(_ => done = true,
                    System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
                yield return new WaitUntil(() => done);
            }


            // If no GameFlow assigned, fall back to direct start (VS sandbox mode)
            if (_gameFlow == null)
            {
                Debug.Log("[VS Bootstrap] No GameFlowStateMachine — starting arena directly (sandbox mode).");
                _autoBattle?.StartCombat();
                _waveSpawnManager?.StartFirstWave();
            }
            else
            {
                Debug.Log("[VS Bootstrap] All systems initialized. GameFlow in Menu — press START RUN.");
                // MainMenuController's Start Run button calls _gameFlow.StartRun().
            }
        }

        private void OnDestroy()
        {
            GameFlowStateMachine.OnEnteredRun     -= OnEnteredRun;
            GameFlowStateMachine.OnEnteredPostRun -= OnEnteredPostRun;
            HealthSystem.OnEntityDied             -= OnEntityDied;
            EnemyManager.OnEnemyKilled            -= OnEnemyKilledAddGold;
            EnemyManager.OnEnemyKilled            -= OnEnemyKilledDestroyGO;
            PlayerHealthComponent.OnPlayerEnteredIdleRecovery -= OnPlayerEnteredIdleRecovery;
            WaveSpawnManager.OnWaveStarted        -= OnWaveStartedForMilestone;
            if (_researchService != null)
                TickEngine.OnTick -= _researchService.OnTick;
            if (_buildingService != null)
                TickEngine.OnTick -= _buildingService.OnTick;

            if (_autoBattle != null)
            {
                EnemyManager.OnEnemyKilled         -= _autoBattle.HandleEnemyKilledByManager;
                PlayerHealthComponent.OnEntityDied -= _autoBattle.HandlePlayerDied;
            }

            // Clear any remaining statics (test safety)
            GameFlowStateMachine.ClearSubscribersForTesting();
            RunSessionManager.ClearSubscribersForTesting();
        }

        // ── Named event handlers (no lambda leaks) ────────────────────────────────

        private void OnEnteredRun()
        {
            _waveSpawnManager?.ResetForNewRun();
            _waveSpawnManager?.StartFirstWave();
            _autoBattle?.StartCombat();
        }

        private void OnEnteredPostRun()
        {
            _waveSpawnManager?.StopWaves();
            _milestoneTracker?.NotifyRunCompleted();
        }

        private void OnEntityDied(int entityId, string vfxTag, UnityEngine.Vector2 pos)
        {
            _enemyManager?.MarkDead(entityId);
            _healthSystem?.Unregister(entityId);
        }

        private void OnWaveStartedForMilestone(int waveNumber)
            => _milestoneTracker?.NotifyWaveChanged(waveNumber);

        private void OnEnemyKilledAddGold(EndlessEngine.Enemy.EnemyAgent agent)
            => _economyService?.AddResources(agent.GoldDropAmount);

        private void OnEnemyKilledDestroyGO(EndlessEngine.Enemy.EnemyAgent agent)
        {
            if (agent?.Rigidbody != null) Destroy(agent.Rigidbody.gameObject);
        }

        private void OnPlayerEnteredIdleRecovery()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _playerHealth?.InitialiseFromConfigForTesting();
#endif
        }
    }
}
