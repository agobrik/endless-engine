using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.Flow;
using EndlessEngine.Generator;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;

namespace EndlessEngine.Bootstrap
{
    /// <summary>
    /// Zero-configuration bootstrap for idle games.
    ///
    /// Drop this component on any GameObject and assign your ScriptableObject configs.
    /// All MonoBehaviour services are auto-found or created on the same GameObject —
    /// no Inspector wiring required.
    ///
    /// Supports: Economy · Generator · PassiveIncome · UpgradeTree · Save/Load
    ///
    /// NOTE: Uses ConfigRegistry.InjectForTesting (no Addressables) — suitable for
    /// rapid prototyping and Editor/Development builds. For production shipping, replace
    /// with VerticalSliceBootstrap + ConfigLoadingService + Addressables.
    ///
    /// For full vertical-slice features see VerticalSliceBootstrap.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [AddComponentMenu("Endless Engine/Auto Setup Bootstrap")]
    public class AutoSetupBootstrap : MonoBehaviour
    {
        [Header("Configs — assign your ScriptableObject assets here")]
        [Tooltip("Economy settings (hard cap, starting gold, etc.)")]
        [SerializeField] private EconomyConfigSO _economyConfig;

        [Tooltip("Database of all generator types (mines, farms, etc.)")]
        [SerializeField] private GeneratorDatabaseSO _generatorDatabase;

        [Tooltip("Schema version asset (create via Endless Engine → Config → Schema Version)")]
        [SerializeField] private SchemaVersionSO _schemaVersion;

        [Tooltip("Prestige config — leave null to disable prestige")]
        [SerializeField] private PrestigeConfigSO _prestigeConfig;

        [Tooltip("Realm identity config — leave null to use defaults")]
        [SerializeField] private RealmIdentityConfigSO _realmConfig;

        [Tooltip("Upgrade node configs — auto-populated by the New Game Wizard")]
        [SerializeField] private UpgradeNodeConfigSO[] _upgradeNodeConfigs;

        [Header("Options")]
        [Tooltip("Enable auto-save every 60 seconds")]
        [SerializeField] private bool _enableSave = true;

        // ── Resolved services (accessible after Start) ───────────────────────────

        /// <summary>The economy service resolved at boot. Use to read gold balance.</summary>
        public EconomyService Economy { get; private set; }

        /// <summary>The generator system resolved at boot.</summary>
        public GeneratorSystem Generators { get; private set; }

        /// <summary>The upgrade tree service resolved at boot.</summary>
        public UpgradeTreeService UpgradeTree { get; private set; }

        /// <summary>The save service resolved at boot (null if _enableSave is false).</summary>
        public SaveService Save { get; private set; }

        /// <summary>The tick engine resolved at boot.</summary>
        public TickEngine Tick { get; private set; }

        /// <summary>True after Start() coroutine completes and all systems are ready.</summary>
        public bool IsReady { get; private set; }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            // 1. Auto-resolve or create MonoBehaviour services on this GameObject
            Economy     = GetOrAdd<EconomyService>();
            Generators  = GetOrAdd<GeneratorSystem>();
            UpgradeTree = GetOrAdd<UpgradeTreeService>();
            Tick        = GetOrAdd<TickEngine>();
            Save        = _enableSave ? GetOrAdd<SaveService>() : null;

            var passiveIncome = GetOrAdd<PassiveIncomeService>();

            // 2. Configure numeric backend
            if (_economyConfig != null)
                BigNumberFactory.Configure(_economyConfig.NumberBackend);

            // 3. Populate ConfigRegistry (no Addressables required)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ConfigRegistry.InjectForTesting(
                economy:  _economyConfig,
                schema:   _schemaVersion,
                prestige: _prestigeConfig,
                realm:    _realmConfig,
                upgrades: _upgradeNodeConfigs != null && _upgradeNodeConfigs.Length > 0
                              ? _upgradeNodeConfigs : null);
#endif

            // 4. Initialize services
            // UpgradeTreeService subscribes to ConfigRegistry.OnConfigsLoaded in Start(),
            // but Start() hasn't run yet when InjectForTesting fires the event above.
            // Call HandleConfigsLoaded manually so the tree builds before LoadAsync.
            UpgradeTree.HandleConfigsLoaded();

            Economy.Initialize(upgradeTreeQuery: UpgradeTree, saveNotifier: Save);

            var generatorConfigs = _generatorDatabase != null
                ? _generatorDatabase.Generators
                : new GeneratorConfigSO[0];
            Generators.Initialize(configs: generatorConfigs, economy: Economy, saveNotifier: Save);

            passiveIncome.Initialize(generators: Generators, economy: Economy, gameFlow: null);

            // 5. Register save providers
            if (Save != null)
            {
                Save.RegisterStateProvider(Economy);
                Save.RegisterStateProvider(UpgradeTree);
                Save.RegisterStateProvider(Generators);
            }

            // 6. Load
            if (Save != null)
            {
                bool loaded = false;
                _ = Save.LoadAsync().ContinueWith(
                    _ => loaded = true,
                    System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
                yield return new WaitUntil(() => loaded);
            }
            else
            {
                var mock = new SaveData();
                mock.EnsureDefaults();
                Economy.OnAfterLoad(mock);
                UpgradeTree.OnAfterLoad(mock);
                Generators.OnAfterLoad(mock);
                yield return null;
            }

            IsReady = true;
            Debug.Log("[AutoSetupBootstrap] Ready.");
        }

        private T GetOrAdd<T>() where T : MonoBehaviour
        {
            var c = GetComponent<T>();
            if (c == null) c = gameObject.AddComponent<T>();
            return c;
        }
    }
}
