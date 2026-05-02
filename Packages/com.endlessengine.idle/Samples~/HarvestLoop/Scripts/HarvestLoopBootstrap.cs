using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;
using EndlessEngine.Harvest;
using EndlessEngine.Statistics;
using EndlessEngine.Input;

namespace EndlessEngine.Samples.HarvestLoop
{
    /// <summary>
    /// Bootstrap for the HarvestLoop sample.
    /// Demonstrates: HarvestLoopService + HarvestCursor + HarvestNode world objects + combo + offline.
    ///
    /// Assign all fields in the Inspector before pressing Play.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class HarvestLoopBootstrap : MonoBehaviour
    {
        [Header("Core Services")]
        [SerializeField] private SaveService        _saveService;
        [SerializeField] private EconomyService     _economyService;
        [SerializeField] private UpgradeTreeService _upgradeTreeService;
        [SerializeField] private StatisticsService  _statisticsService;

        [Header("Harvest Loop")]
        [SerializeField] private HarvestLoopService       _harvestLoopService;
        [SerializeField] private HarvestOfflineCalculator _offlineCalc;
        [SerializeField] private HarvestAreaConfigSO      _harvestAreaConfig;
        [SerializeField] private HarvestNodeConfigSO[]    _nodeConfigs;
        [SerializeField] private HarvestCursor            _harvestCursor;
        [SerializeField] private InputProviderUnity       _inputProvider;

        [Header("Configs")]
        [SerializeField] private EconomyConfigSO       _economyConfig;
        [SerializeField] private SchemaVersionSO       _schemaVersion;
        [SerializeField] private PrestigeConfigSO      _prestigeConfig;
        [SerializeField] private RealmIdentityConfigSO _realmConfig;
        [SerializeField] private StatDefinitionSO[]    _statDefinitions;

        private IEnumerator Start()
        {
            Debug.Log("[HarvestLoop] Wiring systems...");

            if (_economyConfig != null)
                BigNumberFactory.Configure(_economyConfig.NumberBackend);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ConfigRegistry.InjectForTesting(
                economy:  _economyConfig,
                schema:   _schemaVersion,
                prestige: _prestigeConfig,
                realm:    _realmConfig);
#endif

            _upgradeTreeService?.HandleConfigsLoaded();
            _economyService?.Initialize(_upgradeTreeService, _saveService);
            _statisticsService?.Initialize(_statDefinitions ?? System.Array.Empty<StatDefinitionSO>());

            _harvestCursor?.Inject(_inputProvider);

            if (_harvestLoopService != null && _harvestAreaConfig != null)
            {
                _harvestLoopService.Initialize(
                    cursor:     _harvestCursor,
                    config:     _harvestAreaConfig,
                    economy:    _economyService,
                    statistics: _statisticsService,
                    vfx:        null);
            }

            if (_offlineCalc != null)
            {
                _offlineCalc.Initialize(
                    config:      _harvestAreaConfig,
                    economy:     _economyService,
                    nodeConfigs: _nodeConfigs ?? System.Array.Empty<HarvestNodeConfigSO>());
            }

            if (_saveService != null)
            {
                _saveService.RegisterStateProvider(_economyService);
                _saveService.RegisterStateProvider(_upgradeTreeService);
                _saveService.RegisterStateProvider(_statisticsService);
                if (_harvestLoopService != null)
                    _saveService.RegisterStateProvider(_harvestLoopService);
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
                _statisticsService?.OnAfterLoad(mock);
                _harvestLoopService?.OnAfterLoad(mock);
                yield return null;
            }

            Debug.Log("[HarvestLoop] Ready. Drag cursor over nodes to harvest!");
        }
    }
}
