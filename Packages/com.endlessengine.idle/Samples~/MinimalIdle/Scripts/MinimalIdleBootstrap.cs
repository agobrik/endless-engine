using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Flow;
using EndlessEngine.Generator;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;

namespace EndlessEngine.Samples.MinimalIdle
{
    /// <summary>
    /// Minimal bootstrap for the MinimalIdle sample scene.
    /// Wires: SaveService → EconomyService → GeneratorSystem → PassiveIncomeService
    ///        UpgradeTreeService (one efficiency node)
    ///        TickEngine (1 Hz heartbeat)
    ///
    /// Assign config assets in the Inspector. No Addressables required.
    /// This is a sample — see VerticalSliceBootstrap for the full production pattern.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class MinimalIdleBootstrap : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField] private SaveService          _saveService;
        [SerializeField] private EconomyService       _economyService;
        [SerializeField] private UpgradeTreeService   _upgradeTreeService;
        [SerializeField] private GeneratorSystem      _generatorSystem;
        [SerializeField] private PassiveIncomeService _passiveIncomeService;
        [SerializeField] private TickEngine           _tickEngine;

        [Header("Configs")]
        [SerializeField] private EconomyConfigSO       _economyConfig;
        [SerializeField] private GeneratorDatabaseSO   _generatorDatabase;
        [SerializeField] private SchemaVersionSO       _schemaVersion;
        [SerializeField] private PrestigeConfigSO      _prestigeConfig;
        [SerializeField] private RealmIdentityConfigSO _realmConfig;

        private IEnumerator Start()
        {
            Debug.Log("[MinimalIdle] Wiring systems...");

            // Config registry (testing path — no Addressables in sample)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ConfigRegistry.InjectForTesting(
                economy:  _economyConfig,
                schema:   _schemaVersion,
                prestige: _prestigeConfig,
                realm:    _realmConfig);
#endif

            // Economy
            _economyService?.Initialize(
                upgradeTreeQuery: _upgradeTreeService,
                saveNotifier:     _saveService);

            // Generators
            if (_generatorSystem != null && _economyService != null)
            {
                var configs = _generatorDatabase != null
                    ? _generatorDatabase.Generators
                    : new GeneratorConfigSO[0];
                _generatorSystem.Initialize(
                    configs:      configs,
                    economy:      _economyService,
                    saveNotifier: _saveService);
            }

            // Passive income (wired to tick engine via GameFlow=null path)
            _passiveIncomeService?.Initialize(
                generators: _generatorSystem,
                economy:    _economyService,
                gameFlow:   null);

            // Save providers
            if (_saveService != null)
            {
                _saveService.RegisterStateProvider(_economyService);
                _saveService.RegisterStateProvider(_upgradeTreeService);
                if (_generatorSystem != null)
                    _saveService.RegisterStateProvider(_generatorSystem);
            }

            // Load
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
                _generatorSystem?.OnAfterLoad(mock);
                yield return null;
            }

            Debug.Log("[MinimalIdle] Ready. Gold accumulating...");
        }
    }
}
