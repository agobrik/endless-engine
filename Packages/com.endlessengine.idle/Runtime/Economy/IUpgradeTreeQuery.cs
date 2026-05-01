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

        /// <summary>
        /// Returns the current Gold cost as double.
        /// Default implementation wraps GetNodeCost for backwards compatibility.
        /// Override in implementations that support large costs beyond long.MaxValue.
        /// </summary>
        double GetNodeCostDouble(string nodeId) => (double)GetNodeCost(nodeId);
    }
}
