using EndlessEngine.Config;

namespace EndlessEngine.Building
{
    /// <summary>
    /// Runtime state of a single placed building instance.
    /// Stored by BuildingService; serialized to SaveData.
    /// </summary>
    public class BuildingInstance
    {
        public string InstanceId  { get; }   // unique GUID string
        public string BuildingId  { get; }   // maps to BuildingConfigSO.BuildingId
        public int    GridX       { get; }
        public int    GridY       { get; }
        public int    UpgradeTier { get; private set; }  // 0 = base

        public BuildingInstance(string instanceId, string buildingId, int x, int y, int upgradeTier = 0)
        {
            InstanceId  = instanceId;
            BuildingId  = buildingId;
            GridX       = x;
            GridY       = y;
            UpgradeTier = upgradeTier;
        }

        public void ApplyUpgrade() => UpgradeTier++;

        /// <summary>
        /// Effective production per tick for this instance given its config.
        /// </summary>
        public long GetProductionPerTick(BuildingConfigSO config)
        {
            if (config == null) return 0;
            long   bonus = 0;
            float  mult  = 1f;
            if (UpgradeTier > 0 && config.UpgradeTiers != null)
            {
                int tierIdx = System.Math.Min(UpgradeTier - 1, config.UpgradeTiers.Length - 1);
                var tier    = config.UpgradeTiers[tierIdx];
                bonus       = tier.ProductionBonusPerTick;
                mult        = tier.ProductionMultiplier;
            }
            return (long)((config.ProductionPerTick + bonus) * mult);
        }
    }
}
