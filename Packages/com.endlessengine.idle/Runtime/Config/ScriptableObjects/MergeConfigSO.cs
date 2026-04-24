using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// A single merge rule: two items of tier N → one item of tier N+1.
    /// </summary>
    [Serializable]
    public class MergeRule
    {
        [Tooltip("Tier of the two input items.")]
        [Min(0)]
        public int InputTier = 0;

        [Tooltip("The ItemConfigSO produced when two items at InputTier are merged.")]
        public ItemConfigSO ResultItem;

        [Tooltip("Gold bonus awarded on merge. 0 = no bonus.")]
        [Min(0)]
        public long GoldBonus = 0;
    }

    /// <summary>
    /// Configuration for the merge mechanic.
    /// Lists which item groups can be merged and what they produce.
    ///
    /// Items are grouped by their MergeGroupId (a string tag on ItemConfigSO).
    /// Two items in the same group at the same tier merge into the rule's ResultItem.
    ///
    /// Create via: Tools → Endless Engine → Create Merge Config
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Merge/Merge Config",
        fileName = "MergeConfig")]
    public class MergeConfigSO : ScriptableObject
    {
        [Tooltip("Unique config ID.")]
        public string ConfigId = "";

        [Tooltip("The merge group this config applies to (must match ItemConfigSO.MergeGroupId).")]
        public string MergeGroupId = "";

        [Tooltip("Ordered list of merge rules from tier 0 upward.")]
        public List<MergeRule> Rules = new List<MergeRule>();

        /// <summary>Returns the rule for the given input tier, or null if no rule exists.</summary>
        public MergeRule GetRule(int tier)
        {
            if (Rules == null) return null;
            foreach (var r in Rules)
                if (r.InputTier == tier) return r;
            return null;
        }
    }
}
