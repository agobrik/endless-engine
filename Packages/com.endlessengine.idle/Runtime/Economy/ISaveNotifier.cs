namespace EndlessEngine.Economy
{
    /// <summary>
    /// Narrow interface used by EconomyService to trigger save debounce after purchase.
    /// EconomyService does not call SaveService.SaveAsync() directly — only notifies.
    ///
    /// ADR: ADR-0004 — ISaveStateProvider Pull-Based Save Collection
    /// </summary>
    public interface ISaveNotifier
    {
        /// <summary>Notifies the save system that an upgrade was purchased. Activates the 5-second debounce save.</summary>
        void NotifyUpgradePurchased();
    }
}
