using System;
using System.Collections.Generic;

namespace EndlessEngine.Harvest
{
    /// <summary>
    /// Serializable slice of SaveData owned by the Harvest system.
    /// Stored as SaveData.HarvestState (added by schema migration).
    ///
    /// NodeStates: keyed by NodeId. Stores whether the node is currently
    /// respawning and how many seconds remain on the respawn timer.
    /// Nodes not present in the dictionary are assumed alive with full HP.
    /// </summary>
    [Serializable]
    public class HarvestSaveState
    {
        /// <summary>Key = HarvestNodeConfigSO.NodeId + "_" + instanceIndex</summary>
        public Dictionary<string, HarvestNodeSaveEntry> NodeStates = new();

        public long   TotalGoldEarned;
        public int    TotalNodesHarvested;
        public float  BestComboMultiplier;
    }

    [Serializable]
    public class HarvestNodeSaveEntry
    {
        public bool  IsRespawning;
        public float RespawnSecondsRemaining;
    }
}
