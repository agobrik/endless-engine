using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Defines a placeable building. Buildings produce resources passively
    /// and can be upgraded through a tier progression.
    ///
    /// ProductionType = "gold" integrates with EconomyService.
    /// ProductionType = any currency id integrates with CurrencyService.
    /// </summary>
    [CreateAssetMenu(menuName = "Endless Engine/Building Config", fileName = "BuildingConfig")]
    public class BuildingConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public string BuildingId;
        public string DisplayName;
        [TextArea(2, 4)]
        public string Description;

        [Header("Placement")]
        /// <summary>Width in grid cells (1 = 1x1).</summary>
        [Min(1)] public int GridWidth  = 1;
        /// <summary>Height in grid cells (1 = 1x1).</summary>
        [Min(1)] public int GridHeight = 1;

        [Header("Cost")]
        public long PlacementCost;
        public string PlacementCurrencyId = "gold"; // "gold" or secondary currency id

        [Header("Production")]
        public string ProductionCurrencyId = "gold";
        /// <summary>Amount produced per tick.</summary>
        public long   ProductionPerTick    = 0;

        [Header("Upgrades")]
        /// <summary>
        /// Upgrade tiers. Tier 0 = base (this config). Each entry defines the delta for that upgrade level.
        /// </summary>
        public BuildingUpgradeTier[] UpgradeTiers = new BuildingUpgradeTier[0];

        /// <summary>Max concurrent instances of this building (0 = unlimited).</summary>
        [Min(0)] public int MaxInstances = 0;
    }

    [System.Serializable]
    public class BuildingUpgradeTier
    {
        public string DisplayLabel;          // e.g. "Level 2", "Tier II"
        public long   UpgradeCost;
        public string UpgradeCurrencyId = "gold";
        public long   ProductionBonusPerTick; // added to base production
        [Range(1f, 10f)]
        public float  ProductionMultiplier = 1f; // multiplied on top of bonus
    }
}
