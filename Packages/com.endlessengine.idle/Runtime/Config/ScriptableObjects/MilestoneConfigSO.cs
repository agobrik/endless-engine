using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Node types in the milestone condition tree.
    /// </summary>
    public enum MilestoneConditionType
    {
        /// <summary>All child conditions must be true.</summary>
        And,
        /// <summary>At least one child condition must be true.</summary>
        Or,
        /// <summary>Leaf node — compare a tracked metric to a threshold.</summary>
        Threshold
    }

    /// <summary>
    /// Tracked metrics that milestones can watch.
    /// </summary>
    public enum MilestoneMetric
    {
        TotalGoldEarned,
        CurrentGold,
        PrestigeCount,
        WaveNumber,
        GeneratorCount,        // total generators owned (any type)
        GeneratorTypeCount,    // generators owned of a specific type (uses MetricId)
        UpgradesPurchased,
        SecondaryCurrencyBalance,  // uses MetricId = currencyId
        TotalConversions,
        TotalClicks,
        RunsCompleted
    }

    /// <summary>
    /// A node in the milestone condition tree. Supports AND/OR composite nodes and
    /// threshold leaf nodes. Serialized inline in MilestoneConfigSO.
    /// </summary>
    [Serializable]
    public class MilestoneConditionNode
    {
        [Tooltip("Node type: And / Or for composites, Threshold for leaf checks.")]
        public MilestoneConditionType Type = MilestoneConditionType.Threshold;

        // ── Threshold leaf fields (used when Type == Threshold) ──────────────────

        [Tooltip("Which metric to compare.")]
        public MilestoneMetric Metric = MilestoneMetric.TotalGoldEarned;

        [Tooltip("Optional string identifier (e.g. generator ID or currency ID) for per-entity metrics.")]
        public string MetricId = "";

        [Tooltip("The value the metric must reach or exceed.")]
        public double Threshold = 1;

        // ── Composite fields (used when Type == And/Or) ──────────────────────────

        [Tooltip("Child conditions evaluated by this composite node.")]
        public List<MilestoneConditionNode> Children = new List<MilestoneConditionNode>();
    }

    /// <summary>
    /// Configures a single milestone/achievement. Milestones are checked by
    /// MilestoneTracker whenever a relevant metric changes.
    ///
    /// Create via: Tools → Endless Engine → Create Milestone Config
    /// (or via MilestoneDatabaseSO's asset list)
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Milestone/Milestone Config",
        fileName = "MilestoneConfig")]
    public class MilestoneConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier used in save data. Never change after release.")]
        public string MilestoneId = "";

        [Tooltip("Player-facing name shown in the achievement log.")]
        public string DisplayName = "Milestone";

        [Tooltip("Short description shown in the achievement popup.")]
        [TextArea(2, 4)]
        public string Description = "";

        [Tooltip("Optional icon shown in the achievement popup.")]
        public Sprite Icon;

        [Header("Condition")]
        [Tooltip("Root node of the condition tree. Supports nested AND/OR logic.")]
        public MilestoneConditionNode Condition = new MilestoneConditionNode();

        [Header("Reward")]
        [Tooltip("Gold added to EconomyService when this milestone is first unlocked. 0 = no gold reward.")]
        public long GoldReward = 0;

        [Tooltip("Secondary currency reward. Leave empty for none.")]
        public string RewardCurrencyId = "";

        [Tooltip("Amount of secondary currency to reward. 0 = no currency reward.")]
        public double RewardCurrencyAmount = 0;

        [Header("Display")]
        [Tooltip("If true, show popup notification when milestone is first completed.")]
        public bool ShowPopup = true;

        [Tooltip("Prestige resets milestone completion state so it can be earned again.")]
        public bool ResetsOnPrestige = false;
    }
}
