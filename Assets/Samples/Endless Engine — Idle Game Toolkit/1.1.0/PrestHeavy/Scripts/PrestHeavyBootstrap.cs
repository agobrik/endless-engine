using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.Flow;
using EndlessEngine.Generator;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;
using EndlessEngine.Wave;
using EndlessEngine.Combat;
using EndlessEngine.Enemy;
using EndlessEngine.Prestige;
using EndlessEngine.Research;
using EndlessEngine.Statistics;
using EndlessEngine.Input;

namespace EndlessEngine.Samples.PrestHeavy
{
    /// <summary>
    /// Bootstrap for the PrestHeavy sample.
    /// Demonstrates: full prestige + ascension + skill tree + research queue chain.
    ///
    /// Assign all fields in the Inspector before pressing Play.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class PrestHeavyBootstrap : MonoBehaviour
    {
        [Header("Core Services")]
        [SerializeField] private SaveService          _saveService;
        [SerializeField] private EconomyService       _economyService;
        [SerializeField] private UpgradeTreeService   _upgradeTreeService;
        [SerializeField] private GeneratorSystem      _generatorSystem;
        [SerializeField] private PassiveIncomeService _passiveIncomeService;
        [SerializeField] private StatisticsService    _statisticsService;

        [Header("Prestige / Ascension")]
        [SerializeField] private PrestigeStateManager   _prestigeManager;
        [SerializeField] private AscensionStateManager  _ascensionManager;
        [SerializeField] private AscensionDatabaseSO    _ascensionDatabase;

        [Header("Wave / Combat")]
        [SerializeField] private WaveSpawnManager     _waveSpawnManager;
        [SerializeField] private AutoBattleController _autoBattle;
        [SerializeField] private EnemyManager         _enemyManager;
        [SerializeField] private InputProviderUnity   _inputProvider;

        [Header("Research")]
        [SerializeField] private ResearchService          _researchService;
        [SerializeField] private ResearchTreeConfigSO[]   _researchTrees;

        [Header("Configs")]
        [SerializeField] private EconomyConfigSO        _economyConfig;
        [SerializeField] private WaveConfigSO           _waveConfig;
        [SerializeField] private PlayerBaseStatConfigSO _playerConfig;
        [SerializeField] private PrestigeConfigSO       _prestigeConfig;
        [SerializeField] private GeneratorDatabaseSO    _generatorDatabase;
        [SerializeField] private SchemaVersionSO        _schemaVersion;
        [SerializeField] private RealmIdentityConfigSO  _realmConfig;
        [SerializeField] private StatDefinitionSO[]     _statDefinitions;

        private IEnumerator Start()
        {
            Debug.Log("[PrestHeavy] Wiring systems...");

            if (_economyConfig != null)
                BigNumberFactory.Configure(_economyConfig.NumberBackend);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ConfigRegistry.InjectForTesting(
                economy:  _economyConfig,
                wave:     _waveConfig,
                player:   _playerConfig,
                prestige: _prestigeConfig,
                schema:   _schemaVersion,
                realm:    _realmConfig);
#endif

            _upgradeTreeService?.HandleConfigsLoaded();
            _economyService?.Initialize(_upgradeTreeService, _saveService);
            _statisticsService?.Initialize(_statDefinitions ?? System.Array.Empty<StatDefinitionSO>());

            if (_generatorSystem != null && _generatorDatabase != null)
                _generatorSystem.Initialize(_generatorDatabase.Generators, _economyService, _saveService);

            _passiveIncomeService?.Initialize(_generatorSystem, _economyService, null);

            // WaveSpawnManager reads WaveConfig from ConfigRegistry after InjectForTesting
            if (_waveSpawnManager != null && _enemyManager != null)
                _waveSpawnManager.Initialize(_enemyManager, saveNotifier: null);

            if (_autoBattle != null && _playerConfig != null)
            {
                var statProvider = new BaseStatUpgradeProvider(_playerConfig);
                _autoBattle.Initialize(
                    _enemyManager, _waveSpawnManager, statProvider,
                    _playerConfig, _waveConfig, playerId: 1);
            }

            // PrestigeStateManager has no Initialize — reads config via ConfigRegistry on load
            _ascensionManager?.Initialize(
                database:        _ascensionDatabase,
                prestigeManager: _prestigeManager,
                saveService:     _saveService,
                economyService:  _economyService,
                generatorSystem: _generatorSystem);

            if (_researchService != null && _researchTrees != null)
            {
                _researchService.Initialize(_researchTrees, _economyService, null);
                TickEngine.OnTick += _researchService.OnTick;
            }

            if (_saveService != null)
            {
                _saveService.RegisterStateProvider(_economyService);
                _saveService.RegisterStateProvider(_upgradeTreeService);
                _saveService.RegisterStateProvider(_statisticsService);
                if (_generatorSystem != null)  _saveService.RegisterStateProvider(_generatorSystem);
                if (_waveSpawnManager != null) _saveService.RegisterStateProvider(_waveSpawnManager);
                if (_prestigeManager != null)  _saveService.RegisterStateProvider(_prestigeManager);
                if (_ascensionManager != null) _saveService.RegisterStateProvider(_ascensionManager);
                if (_researchService != null)  _saveService.RegisterStateProvider(_researchService);
            }

            if (_saveService != null)
            {
                bool done = false;
                _ = _saveService.LoadAsync().ContinueWith(_ => done = true,
                    System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
                yield return new WaitUntil(() => done);
            }
            else
            {
                var mock = new SaveData();
                mock.EnsureDefaults();
                _economyService?.OnAfterLoad(mock);
                _upgradeTreeService?.OnAfterLoad(mock);
                yield return null;
            }

            _autoBattle?.StartCombat();
            Debug.Log("[PrestHeavy] Ready. Prestige at wave 10!");
        }
    }
}
