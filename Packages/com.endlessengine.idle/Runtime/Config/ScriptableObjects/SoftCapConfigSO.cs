using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Configures a soft-cap / diminishing-returns curve applied to a yield value.
    /// Pass the raw (uncapped) yield to <see cref="SoftCapEvaluator.Apply"/> to get
    /// the capped effective yield.
    ///
    /// Three curve types:
    ///   Logarithmic  — gentle floor; good for prestige multipliers
    ///   SquareRoot   — moderate taper; good for generator counts
    ///   Asymptotic   — hard ceiling approach; good for gold/sec rate caps
    ///
    /// All curves guarantee:
    ///   - Below threshold → no change (raw value returned unchanged)
    ///   - At threshold    → value equals threshold (smooth join)
    ///   - Above threshold → value grows slower than raw
    ///   - HardCeiling     → if set, value never exceeds HardCeiling
    /// </summary>
    [CreateAssetMenu(fileName = "SoftCapConfig", menuName = "Endless Engine/Config/Soft Cap")]
    public class SoftCapConfigSO : ScriptableObject
    {
        [Header("Curve Type")]
        [Tooltip("Which diminishing-returns formula to apply above the threshold.")]
        public SoftCapCurveType CurveType = SoftCapCurveType.Asymptotic;

        [Header("Threshold")]
        [Tooltip("Raw value below which no soft cap is applied. Must be > 0.")]
        public double Threshold = 1000;

        [Header("Curve Parameters")]
        [Tooltip("Logarithmic / SquareRoot: scale factor k. Larger k = more aggressive tapering. Default 1.")]
        public double K = 1.0;

        [Tooltip("Asymptotic: the value the curve approaches but never reaches (ceiling). Must be > Threshold.")]
        public double HardCeiling = 10_000;

        [Header("Absolute ceiling (optional)")]
        [Tooltip("If > 0, the output is additionally clamped to this absolute maximum regardless of curve. 0 = no clamp.")]
        public double AbsoluteCeiling = 0;
    }

    /// <summary>Curve type used by <see cref="SoftCapConfigSO"/>.</summary>
    public enum SoftCapCurveType
    {
        /// <summary>
        /// Above threshold: effectiveValue = threshold + k * ln(1 + raw - threshold)
        /// Grows without bound but very slowly.
        /// </summary>
        Logarithmic,

        /// <summary>
        /// Above threshold: effectiveValue = threshold + k * sqrt(raw - threshold)
        /// Grows without bound but moderately slowly.
        /// </summary>
        SquareRoot,

        /// <summary>
        /// Above threshold: effectiveValue = ceiling - (ceiling - threshold) * exp(-k * (raw - threshold) / (ceiling - threshold))
        /// Asymptotically approaches HardCeiling.
        /// </summary>
        Asymptotic,
    }
}
