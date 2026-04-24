namespace EndlessEngine.Config
{
    /// <summary>
    /// Container holding references to all 9 canonical SO types resolved from a RealmPackSO.
    /// Passed from ConfigLoadingService to ConfigRegistry.Populate().
    /// All fields except UpgradeSelection are non-null after successful resolution —
    /// ConfigValidator guarantees this. UpgradeSelection may be null for builds that
    /// have not yet wired the new SO (backwards-compatible until all realm packs updated).
    /// </summary>
    public class ResolvedConfigs
    {
        public EnemyStatConfigSO        Enemy            { get; }
        public WaveConfigSO             Wave             { get; }
        public EconomyConfigSO          Economy          { get; }
        public UpgradeNodeConfigSO[]    Upgrades         { get; }
        public PrestigeConfigSO         Prestige         { get; }
        public RealmIdentityConfigSO    Realm            { get; }
        public PlayerBaseStatConfigSO   Player           { get; }
        public SchemaVersionSO          Schema           { get; }
        public UpgradeSelectionConfigSO UpgradeSelection { get; }

        /// <summary>The slug of the realm pack these configs came from (for error reporting).</summary>
        public string RealmSlug { get; }

        public ResolvedConfigs(
            EnemyStatConfigSO        enemy,
            WaveConfigSO             wave,
            EconomyConfigSO          economy,
            UpgradeNodeConfigSO[]    upgrades,
            PrestigeConfigSO         prestige,
            RealmIdentityConfigSO    realm,
            PlayerBaseStatConfigSO   player,
            SchemaVersionSO          schema,
            string                   realmSlug,
            UpgradeSelectionConfigSO upgradeSelection = null)
        {
            Enemy            = enemy;
            Wave             = wave;
            Economy          = economy;
            Upgrades         = upgrades;
            Prestige         = prestige;
            Realm            = realm;
            Player           = player;
            Schema           = schema;
            RealmSlug        = realmSlug;
            UpgradeSelection = upgradeSelection;
        }
    }
}
