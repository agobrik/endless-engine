using EndlessEngine.Config;

namespace EndlessEngine.Steam
{
    /// <summary>
    /// Decouples the Milestone system from the Steam achievement backend.
    ///
    /// MilestoneTracker fires OnMilestoneCompleted(MilestoneConfigSO).
    /// An IAchievementBridge subscriber translates MilestoneId → Steam API name
    /// and calls the appropriate backend (Steam, GameCenter, or nothing in tests).
    ///
    /// Implementation: SteamAchievementBridge
    /// Test stub: NullAchievementBridge (no-op)
    /// </summary>
    public interface IAchievementBridge
    {
        /// <summary>
        /// Called when a milestone is completed.
        /// Implementations should map <paramref name="milestone"/>.MilestoneId
        /// to the platform achievement identifier and unlock it.
        /// </summary>
        void OnMilestoneCompleted(MilestoneConfigSO milestone);
    }
}
