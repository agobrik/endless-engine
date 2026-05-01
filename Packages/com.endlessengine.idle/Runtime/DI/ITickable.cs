namespace EndlessEngine.DI
{
    /// <summary>
    /// Marks a service that should receive per-tick updates from ITickSource.
    ///
    /// Prefer ITickable over MonoBehaviour.Update() for services that operate on
    /// the game clock (passive income, cooldowns, timers). ITickable services
    /// are driven by TickEngine — they pause/resume with the game clock, not with
    /// Unity's frame rate.
    ///
    /// The container registers all ITickable services with TickEngine automatically.
    /// </summary>
    public interface ITickable
    {
        /// <summary>
        /// Called once per game tick.
        /// <paramref name="deltaTime"/> is the effective tick duration
        /// (TickIntervalSeconds × TimeScale).
        /// </summary>
        void Tick(float deltaTime);
    }
}
