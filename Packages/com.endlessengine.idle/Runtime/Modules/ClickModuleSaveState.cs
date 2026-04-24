using System;

namespace EndlessEngine.Modules
{
    /// <summary>
    /// Save state for the Click module.
    /// Persisted in <see cref="EndlessEngine.SaveAndLoad.SaveData.ClickState"/>.
    /// </summary>
    [Serializable]
    public class ClickModuleSaveState
    {
        /// <summary>Total gold earned via clicks across all sessions (lifetime stat).</summary>
        public long TotalClickEarned;

        /// <summary>Current auto-click rate override set by upgrades. 0 = use config default.</summary>
        public float AutoClickRateOverride;
    }
}
