using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Configuration for a single upgrade tree node. One asset per node.
    /// The full upgrade tree is the array <c>ConfigRegistry.Upgrades</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeNode_", menuName = "Endless Engine/Config/Upgrade Node")]
    public class UpgradeNodeConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique key used as the save key. Never change after shipping.")]
        public string NodeId;

        public string DisplayName;

        [TextArea]
        public string Description;

        [Header("Progression")]
        public int MaxRank = 5;

        [Tooltip("Base cost at rank 0. Formula: Floor(BaseCost × CostScalingFactor ^ CurrentRank).")]
        public float BaseCost = 100f;

        public float CostScalingFactor = 1.5f;

        [Header("Effect")]
        public StatType AffectedStat;
        public float EffectPerRank = 0.1f;
        public UpgradeEffectType EffectType = UpgradeEffectType.PercentBonus;

        [Header("Unlock")]
        public string[] PrerequisiteNodeIDs;
        public int MinWaveRequirement = 0;
        public int PrestigeGateRequirement = 0;

        [Header("Selection Pool")]
        [Tooltip("Relative draw weight for upgrade card selection. Higher = appears more often. GDD F1 / Rule 3.")]
        [Range(1f, 100f)]
        public float SelectionWeight = 10f;
    }

    public enum StatType
    {
        // Combat
        Damage,
        AttackInterval,
        AttackRange,
        CritChance,
        CritMultiplier,
        AreaDamage,

        // Survival
        MaxHP,
        MoveSpeed,
        DamageReduction,
        HPRegen,

        // Economy
        GoldDropMultiplier,
        GoldPickupRange,
        BonusRunReward,
        ComboMultiplier,

        // Production
        IdleYieldRate,
        GeneratorSpeed,
        OfflineYieldRate,
        ActiveRunPassiveBonus,

        // Prestige
        PrestigeMultiplier,
        StartingGoldBonus,
        RunDurationBonus,
        DoubleGeneratorChance,

        // Harvest (Active Loop)
        HarvestRadius,
        HarvestTickRate,
        HarvestYieldMultiplier,
        HarvestNodeMaxHP,
        HarvestNodeRespawnRate,
        HarvestComboMultiplier,
        HarvestComboDecayRate,
        HarvestMultiNodeBonus,

        // Click Loop (Active — tap/click a target object)
        ClickDamage,
        ClickTargetMaxHP,
        ClickTargetRespawnRate,
        ClickYieldMultiplier,
        ClickComboMultiplier,
        ClickComboDecayRate,
        ClickCritChance,
        ClickCritMultiplier,
        ClickAutoRate,
    }

    public enum UpgradeEffectType
    {
        FlatBonus,
        PercentBonus,
    }
}
