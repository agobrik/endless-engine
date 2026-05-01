using System;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Static service locator for all runtime config ScriptableObjects.
    /// Populated once at boot by ConfigLoadingService.Populate() — do not access
    /// any property before OnConfigsLoaded fires.
    ///
    /// In UNITY_EDITOR and DEVELOPMENT_BUILD, accessing a property before load
    /// throws <see cref="ConfigNotLoadedException"/>. In production, boot sequencing
    /// prevents early access — the exception guard is a developer safety net only.
    ///
    /// ADR: ADR-0003 — ConfigRegistry Static Service Locator
    /// </summary>
    public static class ConfigRegistry
    {
        // ── State ────────────────────────────────────────────────────────────────

        private static bool _isLoaded;

        private static EnemyStatConfigSO        _enemy;
        private static WaveConfigSO             _wave;
        private static EconomyConfigSO          _economy;
        private static UpgradeNodeConfigSO[]    _upgrades;
        private static PrestigeConfigSO         _prestige;
        private static RealmIdentityConfigSO    _realm;
        private static PlayerBaseStatConfigSO   _player;
        private static SchemaVersionSO          _schema;
        private static RealmRegistrySO          _realmRegistry;
        private static UpgradeSelectionConfigSO _upgradeSelection;
        private static RunConfigSO              _run;

        // ── Events ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires after all 8 SO types have been loaded and validated.
        /// SaveService.LoadAsync() and all gameplay systems subscribe here.
        /// </summary>
        public static event Action OnConfigsLoaded;

        /// <summary>
        /// Fires after an atomic realm swap completes. All subscribers must
        /// invalidate any cached SO references and re-read via ConfigRegistry.
        /// Implemented in Story 005.
        /// </summary>
        public static event Action OnRealmSwapped;

        // ── Accessors ─────────────────────────────────────────────────────────────
        // In DEVELOPMENT_BUILD: throws ConfigNotLoadedException if accessed before load.
        // In production: boot sequence guarantees load before any gameplay access.

        /// <summary>Enemy stat configuration. Non-null after OnConfigsLoaded fires.</summary>
        public static EnemyStatConfigSO Enemy => Get(ref _enemy, nameof(Enemy));

        /// <summary>Wave progression configuration. Non-null after OnConfigsLoaded fires.</summary>
        public static WaveConfigSO Wave => Get(ref _wave, nameof(Wave));

        /// <summary>Economy configuration. Non-null after OnConfigsLoaded fires.</summary>
        public static EconomyConfigSO Economy => Get(ref _economy, nameof(Economy));

        /// <summary>All upgrade node configurations. Non-null and non-empty after OnConfigsLoaded fires.</summary>
        public static UpgradeNodeConfigSO[] Upgrades => GetArray(ref _upgrades, nameof(Upgrades));

        /// <summary>Prestige configuration. Non-null after OnConfigsLoaded fires.</summary>
        public static PrestigeConfigSO Prestige => Get(ref _prestige, nameof(Prestige));

        /// <summary>Realm identity configuration including ArenaBounds. Non-null after OnConfigsLoaded fires.</summary>
        public static RealmIdentityConfigSO Realm => Get(ref _realm, nameof(Realm));

        /// <summary>Player base stat configuration. Non-null after OnConfigsLoaded fires.</summary>
        public static PlayerBaseStatConfigSO Player => Get(ref _player, nameof(Player));

        /// <summary>Schema version configuration. Non-null after OnConfigsLoaded fires.</summary>
        public static SchemaVersionSO Schema => Get(ref _schema, nameof(Schema));

        /// <summary>
        /// Realm registry (all realm packs). Non-null after OnConfigsLoaded fires.
        /// Optional: null if not populated (single-realm MVP builds may omit this).
        /// </summary>
        public static RealmRegistrySO RealmRegistry => _realmRegistry;

        /// <summary>Upgrade card selection configuration. Non-null after OnConfigsLoaded fires.</summary>
        public static UpgradeSelectionConfigSO UpgradeSelection => Get(ref _upgradeSelection, nameof(UpgradeSelection));

        /// <summary>Run session configuration (duration, yield modifiers). Non-null after OnConfigsLoaded fires.</summary>
        public static RunConfigSO Run => Get(ref _run, nameof(Run));

        /// <summary>True after ConfigLoadingService successfully calls Populate().</summary>
        public static bool IsLoaded => _isLoaded;

        // ── Population ───────────────────────────────────────────────────────────

        /// <summary>
        /// Called by ConfigLoadingService only. Internal to the Config assembly.
        /// Assigns all 8 accessors and fires OnConfigsLoaded.
        /// </summary>
        internal static void Populate(ResolvedConfigs resolved)
        {
            _enemy            = resolved.Enemy;
            _wave             = resolved.Wave;
            _economy          = resolved.Economy;
            _upgrades         = resolved.Upgrades;
            _prestige         = resolved.Prestige;
            _realm            = resolved.Realm;
            _player           = resolved.Player;
            _schema           = resolved.Schema;
            _upgradeSelection = resolved.UpgradeSelection;
            _isLoaded         = true;

            OnConfigsLoaded?.Invoke();
        }

        // ── Realm Swap ───────────────────────────────────────────────────────────

        /// <summary>
        /// Atomically swaps all 8 SO references to those in <paramref name="pack"/>
        /// and fires <see cref="OnRealmSwapped"/>.
        /// Full Addressables implementation in Config Story 005.
        /// </summary>
        public static System.Threading.Tasks.Task BeginRealmSwapAsync(RealmPackSO pack)
        {
            if (pack == null)
            {
                UnityEngine.Debug.LogWarning("[ConfigRegistry] BeginRealmSwapAsync: null pack — ignoring.");
                return System.Threading.Tasks.Task.CompletedTask;
            }

            if (pack.EnemyStatConfig        != null) _enemy            = pack.EnemyStatConfig;
            if (pack.WaveConfig             != null) _wave             = pack.WaveConfig;
            if (pack.EconomyConfig          != null) _economy          = pack.EconomyConfig;
            if (pack.UpgradeNodeConfigs     != null) _upgrades         = pack.UpgradeNodeConfigs;
            if (pack.PrestigeConfig         != null) _prestige         = pack.PrestigeConfig;
            if (pack.RealmIdentityConfig    != null) _realm            = pack.RealmIdentityConfig;
            if (pack.PlayerBaseStatConfig   != null) _player           = pack.PlayerBaseStatConfig;
            if (pack.SchemaVersion          != null) _schema           = pack.SchemaVersion;
            if (pack.UpgradeSelectionConfig != null) _upgradeSelection = pack.UpgradeSelectionConfig;

            OnRealmSwapped?.Invoke();
            return System.Threading.Tasks.Task.CompletedTask;
        }

        // ── Guard Helpers ────────────────────────────────────────────────────────

        private static T Get<T>(ref T field, string accessorName) where T : ScriptableObject
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_isLoaded)
                throw new ConfigNotLoadedException(accessorName);
#endif
            return field;
        }

        private static UpgradeNodeConfigSO[] GetArray(ref UpgradeNodeConfigSO[] field, string accessorName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_isLoaded)
                throw new ConfigNotLoadedException(accessorName);
#endif
            return field;
        }

        // ── Test Injection ───────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Injects mock SO references for unit testing. Bypasses Addressables.
        /// Any null parameter retains the current value.
        /// Partial injection is supported — only non-null parameters are set.
        /// </summary>
        public static void InjectForTesting(
            EnemyStatConfigSO      enemy         = null,
            WaveConfigSO           wave          = null,
            EconomyConfigSO        economy       = null,
            UpgradeNodeConfigSO[]  upgrades      = null,
            PrestigeConfigSO       prestige      = null,
            RealmIdentityConfigSO  realm         = null,
            PlayerBaseStatConfigSO player        = null,
            SchemaVersionSO        schema        = null,
            RealmRegistrySO        realmRegistry = null,
            RunConfigSO            run           = null)
        {
            if (enemy         != null) _enemy         = enemy;
            if (wave          != null) _wave          = wave;
            if (economy       != null) _economy       = economy;
            if (upgrades      != null) _upgrades      = upgrades;
            if (prestige      != null) _prestige      = prestige;
            if (realm         != null) _realm         = realm;
            if (player        != null) _player        = player;
            if (schema        != null) _schema        = schema;
            if (realmRegistry != null) _realmRegistry = realmRegistry;
            if (run           != null) _run           = run;
            _isLoaded = true;
            OnConfigsLoaded?.Invoke();
        }

        /// <summary>
        /// Clears all injected references and resets _isLoaded. Call in test TearDown.
        /// </summary>
        public static void ClearForTesting()
        {
            _enemy            = null;
            _wave             = null;
            _economy          = null;
            _upgrades         = null;
            _prestige         = null;
            _realm            = null;
            _player           = null;
            _schema           = null;
            _realmRegistry    = null;
            _upgradeSelection = null;
            _run              = null;
            _isLoaded         = false;
        }

        /// <summary>
        /// Fires OnRealmSwapped without a real realm pack, for testing subscribers
        /// that react to config changes after a realm swap.
        /// </summary>
        public static void FireRealmSwappedForTesting() => OnRealmSwapped?.Invoke();
#endif
    }
}
