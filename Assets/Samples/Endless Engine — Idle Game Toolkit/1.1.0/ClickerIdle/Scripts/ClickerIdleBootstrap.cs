using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.Flow;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;
using EndlessEngine.ClickLoop;
using EndlessEngine.Statistics;
using EndlessEngine.Input;

namespace EndlessEngine.Samples.ClickerIdle
{
    /// <summary>
    /// Bootstrap for the ClickerIdle sample.
    /// Demonstrates: ClickLoopService + ClickTarget world objects + combo + crit + offline.
    ///
    /// Assign all fields in the Inspector before pressing Play.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class ClickerIdleBootstrap : MonoBehaviour
    {
        [Header("Core Services")]
        [SerializeField] private SaveService        _saveService;
        [SerializeField] private EconomyService     _economyService;
        [SerializeField] private UpgradeTreeService _upgradeTreeService;
        [SerializeField] private StatisticsService  _statisticsService;

        [Header("Click Loop")]
        [SerializeField] private ClickLoopService           _clickLoopService;
        [SerializeField] private ClickLoopOfflineCalculator _offlineCalc;
        [SerializeField] private ClickLoopConfigSO          _clickLoopConfig;
        [SerializeField] private ClickTargetConfigSO[]      _targetConfigs;
        [SerializeField] private InputProviderUnity         _inputProvider;
        [SerializeField] private LayerMask                  _clickTargetLayer;

        [Header("Configs")]
        [SerializeField] private EconomyConfigSO       _economyConfig;
        [SerializeField] private SchemaVersionSO       _schemaVersion;
        [SerializeField] private PrestigeConfigSO      _prestigeConfig;
        [SerializeField] private RealmIdentityConfigSO _realmConfig;
        [SerializeField] private StatDefinitionSO[]    _statDefinitions;

        private IEnumerator Start()
        {
            Debug.Log("[ClickerIdle] Wiring systems...");

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

            if (_clickLoopService != null && _clickLoopConfig != null)
            {
                _clickLoopService.Initialize(
                    config:      _clickLoopConfig,
                    economy:     _economyService,
                    input:       _inputProvider,
                    targetLayer: _clickTargetLayer,
                    statistics:  _statisticsService,
                    vfx:         null);
            }

            if (_offlineCalc != null)
            {
                _offlineCalc.Initialize(
                    config:        _clickLoopConfig,
                    economy:       _economyService,
                    targetConfigs: _targetConfigs ?? System.Array.Empty<ClickTargetConfigSO>());
            }

            if (_saveService != null)
            {
                _saveService.RegisterStateProvider(_economyService);
                _saveService.RegisterStateProvider(_upgradeTreeService);
                _saveService.RegisterStateProvider(_statisticsService);
                if (_clickLoopService != null)
                    _saveService.RegisterStateProvider(_clickLoopService);
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
                _clickLoopService?.OnAfterLoad(mock);
                yield return null;
            }

            Debug.Log("[ClickerIdle] Ready. Click the targets!");
        }
    }
}
