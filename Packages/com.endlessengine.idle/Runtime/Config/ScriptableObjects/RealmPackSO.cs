using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Assembles all 8 canonical SO types for a realm. Swapping this reference
    /// (via ConfigRegistry.BeginRealmSwap) atomically changes all config values.
    /// All fields are direct serialized Unity object references — no Addressable
    /// keys needed per SO; they load as part of loading this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "RealmPack_Base", menuName = "Endless Engine/Config/Realm Pack")]
    public class RealmPackSO : ScriptableObject
    {
        [Tooltip("Slug used in error messages and Addressable labels (e.g. 'base', 'fire-realm').")]
        public string RealmSlug = "base";

        [Header("9 Canonical Config SOs")]
        public EnemyStatConfigSO       EnemyStatConfig;
        public WaveConfigSO            WaveConfig;
        public EconomyConfigSO         EconomyConfig;
        public UpgradeNodeConfigSO[]   UpgradeNodeConfigs;
        public PrestigeConfigSO        PrestigeConfig;
        public RealmIdentityConfigSO   RealmIdentityConfig;
        public PlayerBaseStatConfigSO  PlayerBaseStatConfig;
        public SchemaVersionSO         SchemaVersion;
        public UpgradeSelectionConfigSO UpgradeSelectionConfig;
    }
}
