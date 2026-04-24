namespace EndlessEngine.Economy
{
    /// <summary>
    /// Read-only query interface that EconomyService uses to get upgrade node costs.
    /// Decouples Economy from the concrete UpgradeTree implementation.
    ///
    /// ADR: ADR-0009 — Upgrade Stat Model (cost queries)
    /// </summary>
    public interface IUpgradeTreeQuery
    {
        /// <summary>Returns the current Gold cost for the given upgrade node ID.</summary>
        long GetNodeCost(string nodeId);
    }
}
