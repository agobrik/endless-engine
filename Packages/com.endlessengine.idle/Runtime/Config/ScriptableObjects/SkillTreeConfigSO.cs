using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Container for all nodes in a skill/talent tree.
    /// Multiple trees are supported (one per SkillTreeConfigSO instance).
    ///
    /// Create via: Tools → Endless Engine → Create Skill Tree Config
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/SkillTree/Skill Tree Config",
        fileName = "SkillTreeConfig")]
    public class SkillTreeConfigSO : ScriptableObject
    {
        [Tooltip("Unique tree ID (e.g. 'combat', 'economy', 'prestige').")]
        public string TreeId = "";

        [Tooltip("Player-facing tree name.")]
        public string DisplayName = "Skill Tree";

        [Tooltip("All nodes in this tree.")]
        public SkillNodeConfigSO[] Nodes = new SkillNodeConfigSO[0];

        /// <summary>Returns the node with the given ID, or null.</summary>
        public SkillNodeConfigSO GetNode(string nodeId)
        {
            if (Nodes == null) return null;
            foreach (var n in Nodes)
                if (n != null && n.NodeId == nodeId) return n;
            return null;
        }
    }
}
