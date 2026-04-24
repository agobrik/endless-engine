using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Type of effect a skill node applies.
    /// </summary>
    public enum SkillEffectType
    {
        /// <summary>Multiplies the named stat by Value.</summary>
        StatMultiplier,
        /// <summary>Adds Value to the named stat.</summary>
        StatAdditive,
        /// <summary>Unlocks a named feature/flag.</summary>
        UnlockFeature,
        /// <summary>Adds Value to a currency's income rate.</summary>
        IncomeBonus
    }

    /// <summary>
    /// A single effect applied by a skill node when unlocked.
    /// </summary>
    [Serializable]
    public class SkillEffect
    {
        [Tooltip("What kind of effect this is.")]
        public SkillEffectType Type = SkillEffectType.StatMultiplier;

        [Tooltip("ID of the stat, feature, or currency this effect targets.")]
        public string TargetId = "";

        [Tooltip("Numeric value (multiplier, additive bonus, etc.).")]
        public float Value = 1f;
    }

    /// <summary>
    /// A single node in the skill/talent tree.
    /// Nodes can require other nodes (prerequisites) and cost Skill Points.
    ///
    /// Create via: Tools → Endless Engine → Create Skill Node Config
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/SkillTree/Skill Node",
        fileName = "SkillNode")]
    public class SkillNodeConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique node ID. Never change after release.")]
        public string NodeId = "";

        [Tooltip("Player-facing name.")]
        public string DisplayName = "Skill";

        [Tooltip("Short description shown in the skill tree UI.")]
        [TextArea(2, 3)]
        public string Description = "";

        [Tooltip("Icon shown in the tree node.")]
        public Sprite Icon;

        [Header("Prerequisites")]
        [Tooltip("IDs of nodes that must be unlocked before this one.")]
        public List<string> PrerequisiteIds = new List<string>();

        [Header("Cost")]
        [Tooltip("Skill points required to unlock this node.")]
        [Min(0)]
        public int PointCost = 1;

        [Tooltip("If true, this node can be refunded (points returned, effects removed).")]
        public bool Refundable = true;

        [Header("Effects")]
        [Tooltip("Stat changes / unlocks applied when this node is unlocked.")]
        public List<SkillEffect> Effects = new List<SkillEffect>();

        [Header("Visual Position (Editor)")]
        [Tooltip("Position in the tree editor. Set by the Skill Tree Editor Window.")]
        public Vector2 EditorPosition = Vector2.zero;
    }
}
