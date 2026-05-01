using UnityEngine;

namespace EndlessEngine.Harvest
{
    /// <summary>
    /// Global config for the harvest cursor area. One asset per game.
    /// Controls base radius, tick interval, and combo decay parameters.
    /// All values are the stat-independent baselines; runtime values are
    /// scaled by UpgradeApplicationSystem stat multipliers.
    /// </summary>
    [CreateAssetMenu(fileName = "HarvestAreaConfig", menuName = "Endless Engine/Harvest/Area Config")]
    public class HarvestAreaConfigSO : ScriptableObject
    {
        [Header("Cursor")]
        [Tooltip("World-space radius of the harvest cursor. Scaled by HarvestRadius stat.")]
        public float BaseRadius = 1.5f;

        [Header("Tick")]
        [Tooltip("Seconds between harvest ticks across all overlapping nodes. Scaled by HarvestTickRate stat.")]
        public float BaseTickInterval = 0.25f;

        [Header("Combo")]
        [Tooltip("Seconds of inactivity before the combo meter resets to 0.")]
        public float ComboDecayDelay = 2f;

        [Tooltip("Decay rate (combo points / second) once decay starts. Scaled by HarvestComboDecayRate stat.")]
        public float ComboDecayRate = 5f;

        [Tooltip("Maximum combo multiplier achievable.")]
        public float MaxComboMultiplier = 5f;

        [Tooltip("Combo points needed for each ×1 multiplier step.")]
        public float ComboPointsPerMultiplierStep = 10f;

        [Header("Offline Harvest")]
        [Tooltip("Maximum hours of offline harvest that will be credited. Beyond this cap is ignored.")]
        public float OfflineCapHours = 8f;

        [Tooltip("Fraction of active harvest rate applied while offline (0–1). Keep below 1 to preserve active-play incentive.")]
        [Range(0f, 1f)]
        public float OfflineEfficiency = 0.3f;
    }
}
