using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Configuration for a time boost entry (e.g. 2× for 60 s, or 4× for 30 s).
    /// Multiple presets can be offered (ad reward, paid, etc.).
    ///
    /// Create via: Tools → Endless Engine → Create Time Boost Config
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/TimeBoost/Time Boost Config",
        fileName = "TimeBoostConfig")]
    public class TimeBoostConfigSO : ScriptableObject
    {
        [Tooltip("Unique ID for this boost preset.")]
        public string BoostId = "";

        [Tooltip("Player-facing name shown in the UI.")]
        public string DisplayName = "2× Speed";

        [Tooltip("Time scale multiplier applied to TickEngine. Typically 2.0 or 4.0.")]
        [Min(1f)]
        public float TimeScaleMultiplier = 2f;

        [Tooltip("Duration of the boost in seconds.")]
        [Min(1f)]
        public float DurationSeconds = 60f;

        [Tooltip("Gold cost to activate. 0 = free / ad-based.")]
        [Min(0)]
        public long GoldCost = 0;
    }
}
