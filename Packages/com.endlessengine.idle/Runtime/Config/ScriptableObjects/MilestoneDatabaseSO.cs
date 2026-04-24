using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Container for all milestones in the game.
    /// Assign all MilestoneConfigSO assets here; MilestoneTracker reads this at startup.
    ///
    /// Create via: Tools → Endless Engine → Create Milestone Database
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Milestone/Milestone Database",
        fileName = "MilestoneDatabase")]
    public class MilestoneDatabaseSO : ScriptableObject
    {
        [Tooltip("All milestones in the game. MilestoneTracker evaluates these in order.")]
        public MilestoneConfigSO[] Milestones = new MilestoneConfigSO[0];

        /// <summary>Returns the milestone with the given ID, or null if not found.</summary>
        public MilestoneConfigSO GetById(string milestoneId)
        {
            if (Milestones == null) return null;
            foreach (var m in Milestones)
                if (m != null && m.MilestoneId == milestoneId) return m;
            return null;
        }
    }
}
