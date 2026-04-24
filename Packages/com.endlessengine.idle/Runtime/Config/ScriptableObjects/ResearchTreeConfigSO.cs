using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Container for all nodes in a research / technology tree.
    ///
    /// Create via: Tools → Endless Engine → Create Research Tree Config
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Research/Research Tree Config",
        fileName = "ResearchTreeConfig")]
    public class ResearchTreeConfigSO : ScriptableObject
    {
        [Tooltip("Unique tree ID.")]
        public string TreeId = "";

        [Tooltip("Player-facing tree name.")]
        public string DisplayName = "Research";

        [Tooltip("All nodes in this research tree.")]
        public ResearchNodeConfigSO[] Nodes = new ResearchNodeConfigSO[0];

        /// <summary>Returns the node with the given ID, or null.</summary>
        public ResearchNodeConfigSO GetNode(string nodeId)
        {
            if (Nodes == null) return null;
            foreach (var n in Nodes)
                if (n != null && n.NodeId == nodeId) return n;
            return null;
        }
    }
}
