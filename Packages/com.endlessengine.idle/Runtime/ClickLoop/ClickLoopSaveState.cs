using System;
using System.Collections.Generic;

namespace EndlessEngine.ClickLoop
{
    /// <summary>
    /// Serializable save slice for the Click Loop system.
    /// Stored as SaveData.ClickLoopState.
    /// </summary>
    [Serializable]
    public class ClickLoopSaveState
    {
        public Dictionary<string, ClickTargetSaveEntry> TargetStates = new();
        public long  TotalGoldEarned;
        public int   TotalTargetsDestroyed;
        public float BestComboMultiplier;
    }

    [Serializable]
    public class ClickTargetSaveEntry
    {
        public bool  IsRespawning;
        public float RespawnSecondsRemaining;
    }
}
