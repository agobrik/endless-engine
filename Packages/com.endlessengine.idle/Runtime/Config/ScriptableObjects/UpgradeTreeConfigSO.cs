using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Single ScriptableObject that contains the full upgrade tree as embedded data.
    /// Replaces the 1-node-per-asset approach — a single asset holds all ~85 nodes.
    ///
    /// Nodes are defined inline as UpgradeNodeDefinition structs. The UpgradeTreeService
    /// reads this SO instead of UpgradeNodeConfigSO[].
    ///
    /// Backwards compatibility: ConfigRegistry.Upgrades still returns UpgradeNodeConfigSO[]
    /// for existing systems. Use this SO directly for the idle-game upgrade tree.
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeTreeConfig", menuName = "Endless Engine/Config/Upgrade Tree Config")]
    public class UpgradeTreeConfigSO : ScriptableObject
    {
        [Tooltip("All upgrade nodes. Order determines display order in the UI.")]
        public List<UpgradeNodeDefinition> Nodes = new List<UpgradeNodeDefinition>();

        [Tooltip("If true, nodes with HideUntilUnlockable=true are hidden in the upgrade screen " +
                 "until their prerequisites are all purchased.")]
        public bool ProgressiveReveal;

        // ── Fast lookup (populated at runtime) ───────────────────────────────────

        private Dictionary<string, UpgradeNodeDefinition> _lookup;

        private void OnEnable() => BuildLookup();

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, UpgradeNodeDefinition>(Nodes.Count);
            foreach (var node in Nodes)
            {
                if (string.IsNullOrEmpty(node.NodeId)) continue;
                _lookup[node.NodeId] = node;
            }
        }

        /// <summary>Returns the node definition for the given ID. Null if not found.</summary>
        public bool TryGetNode(string nodeId, out UpgradeNodeDefinition node)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(nodeId, out node);
        }

        /// <summary>Cost to buy rank (currentRank + 1) of a node.</summary>
        public long CostForNextRank(string nodeId, int currentRank)
        {
            if (!TryGetNode(nodeId, out var node)) return -1;
            return (long)(node.BaseCost * Math.Pow(node.CostScalingFactor, currentRank));
        }
    }

    /// <summary>
    /// Inline upgrade node definition. All data lives here — no separate SO file needed.
    /// </summary>
    [Serializable]
    public struct UpgradeNodeDefinition
    {
        [Tooltip("Stable unique key used as save key. Never change after shipping.")]
        public string NodeId;

        [Tooltip("UI display name.")]
        public string DisplayName;

        [TextArea(1, 3)]
        public string Description;

        [Tooltip("Upgrade category for UI grouping.")]
        public UpgradeCategory Category;

        [Tooltip("Which stat this upgrade affects.")]
        public StatType AffectedStat;

        [Tooltip("Effect added per rank (flat or %).")]
        public float EffectPerRank;

        public UpgradeEffectType EffectType;

        [Tooltip("Maximum purchasable rank.")]
        [Min(1)]
        public int MaxRank;

        [Tooltip("Cost at rank 0. Scales by CostScalingFactor per rank.")]
        [Min(1)]
        public float BaseCost;

        [Range(1f, 3f)]
        public float CostScalingFactor;

        [Tooltip("Node IDs that must be purchased before this one is available.")]
        public string[] PrerequisiteNodeIDs;

        [Tooltip("Prestige count required to unlock this node. 0 = always available.")]
        [Min(0)]
        public int PrestigeGateRequirement;

        [Tooltip("Relative weight for card draw pool.")]
        [Range(1f, 100f)]
        public float SelectionWeight;

        [Header("Tree Layout — column/row index on the tree canvas")]
        [Tooltip("Column index (0-based) of this node on the tree canvas.")]
        [Min(0)]
        public int GridX;

        [Tooltip("Row index (0-based) of this node on the tree canvas.")]
        [Min(0)]
        public int GridY;

        [Header("Display — Icon & Stat Label")]
        [Tooltip("Font Awesome 6 Solid unicode codepoint for this node's icon (e.g. \\uf004). " +
                 "Leave empty to fall back to AffectedStat default.")]
        public string IconUnicode;

        [Tooltip("Human-readable stat name shown in tooltip (e.g. 'Gold Drop'). " +
                 "Leave empty to fall back to AffectedStat enum name.")]
        public string StatDisplayName;

        [Header("Custom Data — Optional")]
        [Tooltip("Optional ScriptableObject for custom node behaviour or extra data. " +
                 "Can be any SO type — the toolset reads this at runtime via your own INodeDataProvider.")]
        public ScriptableObject CustomData;

        [Header("Tree Behaviour")]
        [Tooltip("Maximum number of outgoing edges (child nodes) this node can have. 0 = unlimited.")]
        [Min(0)]
        public int MaxOutgoingEdges;

        [Tooltip("If true, this node is hidden in the upgrade screen until it becomes directly unlockable " +
                 "(i.e. all prerequisites are purchased). Only applies when Progressive Reveal is enabled on the tree.")]
        public bool HideUntilUnlockable;
    }

    /// <summary>Upgrade tree categories shown as tabs in the Upgrade Screen.</summary>
    public enum UpgradeCategory
    {
        Production,   // Generators, passive income, offline yield
        Combat,       // Damage, attack speed, crit, range
        Survival,     // Max HP, damage reduction, move speed
        Economy,      // Gold multipliers, combo, bonus rewards
        Prestige,     // Prestige-locked permanent bonuses
    }
}
