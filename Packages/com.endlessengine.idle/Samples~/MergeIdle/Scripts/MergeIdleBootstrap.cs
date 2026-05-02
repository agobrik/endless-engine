using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;

namespace EndlessEngine.Samples.MergeIdle
{
    /// <summary>
    /// Bootstrap for the MergeIdle sample.
    /// Demonstrates: MergeService + InventoryService + EconomyService + UpgradeTreeService.
    ///
    /// Assign all fields in the Inspector before pressing Play.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class MergeIdleBootstrap : MonoBehaviour
    {
        [Header("Core Services")]
        [SerializeField] private SaveService        _saveService;
        [SerializeField] private EconomyService     _economyService;
        [SerializeField] private UpgradeTreeService _upgradeTreeService;
        [SerializeField] private InventoryService   _inventoryService;

        [Header("Merge")]
        [SerializeField] private MergeService    _mergeService;
        [SerializeField] private MergeConfigSO[] _mergeConfigs;
        [SerializeField] private ItemConfigSO[]  _allItems;
        [SerializeField] private int             _inventorySlots = 20;

        [Header("Configs")]
        [SerializeField] private EconomyConfigSO       _economyConfig;
        [SerializeField] private SchemaVersionSO       _schemaVersion;
        [SerializeField] private PrestigeConfigSO      _prestigeConfig;
        [SerializeField] private RealmIdentityConfigSO _realmConfig;

        private IEnumerator Start()
        {
            Debug.Log("[MergeIdle] Wiring systems...");

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

            if (_inventoryService != null)
                _inventoryService.Initialize(_allItems ?? System.Array.Empty<ItemConfigSO>(), _inventorySlots);

            if (_mergeService != null)
                _mergeService.Initialize(_mergeConfigs, _inventoryService, _economyService);

            if (_saveService != null)
            {
                _saveService.RegisterStateProvider(_economyService);
                _saveService.RegisterStateProvider(_upgradeTreeService);
                if (_inventoryService != null)
                    _saveService.RegisterStateProvider(_inventoryService);
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
                _inventoryService?.OnAfterLoad(mock);
                yield return null;
            }

            Debug.Log("[MergeIdle] Ready. Merge items to earn gold!");
        }
    }
}
