namespace EndlessEngine.Flow
{
    /// <summary>
    /// Top-level game flow states.
    /// Menu → InRun → PostRun → Menu (loop)
    /// </summary>
    public enum GameFlowState
    {
        /// <summary>Main menu, upgrade screen, generator screen. Passive income ticks.</summary>
        Menu,

        /// <summary>Active run in progress. Arena active, run timer counting down.</summary>
        InRun,

        /// <summary>Run ended. Summary screen shown before returning to Menu.</summary>
        PostRun,
    }
}
