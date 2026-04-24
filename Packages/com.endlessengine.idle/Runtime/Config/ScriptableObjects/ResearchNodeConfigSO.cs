using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// A single node in the research tree.
    /// Research nodes take time (ticks) and resources to complete.
    /// Multiple nodes form tier-based chains (tier 0 unlocks tier 1, etc.).
    ///
    /// Create via: Tools → Endless Engine → Create Research Node Config
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Research/Research Node Config",
        fileName = "ResearchNodeConfig")]
    public class ResearchNodeConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique node ID. Never change after release.")]
        public string NodeId = "";

        [Tooltip("Player-facing display name.")]
        public string DisplayName = "Research";

        [Tooltip("Short description of the unlock effect.")]
        [TextArea(2, 3)]
        public string Description = "";

        [Tooltip("Optional icon.")]
        public Sprite Icon;

        [Header("Tier & Prerequisites")]
        [Tooltip("Tier index (0 = root). Must complete all prior-tier nodes before this tier becomes available.")]
        [Min(0)]
        public int Tier = 0;

        [Tooltip("IDs of nodes that must be completed before this one can be queued.")]
        public List<string> PrerequisiteIds = new List<string>();

        [Header("Cost")]
        [Tooltip("Gold cost to queue this research.")]
        [Min(0)]
        public long GoldCost = 1000;

        [Tooltip("Secondary currency ID (empty = gold only).")]
        public string SecondaryCurrencyId = "";

        [Tooltip("Secondary currency amount required (if SecondaryCurrencyId is set).")]
        [Min(0)]
        public double SecondaryCurrencyCost = 0;

        [Header("Time")]
        [Tooltip("Number of TickEngine ticks (1 s each by default) to complete this research.")]
        [Min(1)]
        public int ResearchTicks = 60;

        [Header("Unlock Effect")]
        [Tooltip("Effects applied when this node completes. Reuses SkillEffect type for consistency.")]
        public List<SkillEffect> Effects = new List<SkillEffect>();
    }
}
