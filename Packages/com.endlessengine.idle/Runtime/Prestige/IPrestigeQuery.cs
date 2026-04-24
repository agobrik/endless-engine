namespace EndlessEngine.Prestige
{
    /// <summary>
    /// Read-only prestige state query — decouples UI and gameplay systems
    /// from PrestigeStateManager's concrete implementation.
    ///
    /// Implement on PrestigeStateManager. Inject wherever prestige count
    /// or multiplier is needed without taking a hard dependency on the manager.
    /// </summary>
    public interface IPrestigeQuery
    {
        int   PrestigeCount          { get; }
        bool  CanPrestige            { get; }
        float GetPermanentMultiplier();
    }
}
