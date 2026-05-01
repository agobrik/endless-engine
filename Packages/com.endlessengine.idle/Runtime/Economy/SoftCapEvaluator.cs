using System;
using EndlessEngine.Config;

namespace EndlessEngine.Economy
{
    /// <summary>
    /// Applies a soft-cap / diminishing-returns curve to a raw value.
    /// All methods are static and allocation-free — safe for hot-path use.
    ///
    /// Curve contracts:
    ///   - raw &lt;= threshold  → returns raw unchanged
    ///   - raw == threshold  → returns threshold (smooth join guaranteed)
    ///   - raw &gt; threshold   → returns a value that grows slower than raw
    ///   - AbsoluteCeiling   → if config.AbsoluteCeiling &gt; 0, result is clamped
    ///
    /// Usage:
    ///   double effective = SoftCapEvaluator.Apply(rawYield, softCapConfig);
    /// </summary>
    public static class SoftCapEvaluator
    {
        /// <summary>
        /// Applies the soft-cap curve defined in <paramref name="config"/> to <paramref name="raw"/>.
        /// Returns <paramref name="raw"/> unchanged if config is null or raw is below threshold.
        /// </summary>
        public static double Apply(double raw, SoftCapConfigSO config)
        {
            if (config == null) return raw;
            if (raw <= 0) return 0;

            double t = config.Threshold;
            if (t <= 0) t = 1; // guard: invalid threshold treated as 1

            if (raw <= t) return raw;

            double result = config.CurveType switch
            {
                SoftCapCurveType.Logarithmic => ApplyLogarithmic(raw, t, config.K),
                SoftCapCurveType.SquareRoot  => ApplySquareRoot(raw, t, config.K),
                SoftCapCurveType.Asymptotic  => ApplyAsymptotic(raw, t, config.K, config.HardCeiling),
                _                            => raw,
            };

            if (config.AbsoluteCeiling > 0)
                result = System.Math.Min(result, config.AbsoluteCeiling);

            return result;
        }

        /// <summary>
        /// Applies the logarithmic curve directly (no config object).
        /// effectiveValue = threshold + k * ln(1 + raw - threshold)
        /// </summary>
        public static double ApplyLogarithmic(double raw, double threshold, double k = 1.0)
        {
            if (raw <= threshold) return raw;
            double excess = raw - threshold;
            return threshold + k * System.Math.Log(1.0 + excess);
        }

        /// <summary>
        /// Applies the square-root curve directly.
        /// effectiveValue = threshold + k * sqrt(raw - threshold)
        /// </summary>
        public static double ApplySquareRoot(double raw, double threshold, double k = 1.0)
        {
            if (raw <= threshold) return raw;
            double excess = raw - threshold;
            return threshold + k * System.Math.Sqrt(excess);
        }

        /// <summary>
        /// Applies the asymptotic curve directly.
        /// effectiveValue = ceiling - (ceiling - threshold) * exp(-k * excess / (ceiling - threshold))
        /// Guarantees result &lt; ceiling for all finite raw values.
        /// </summary>
        public static double ApplyAsymptotic(double raw, double threshold, double k, double ceiling)
        {
            if (raw <= threshold) return raw;
            // Ceiling must be above threshold to be meaningful
            if (ceiling <= threshold) return raw; // degenerate: fall back to raw

            double range  = ceiling - threshold;
            double excess = raw - threshold;
            double result = ceiling - range * System.Math.Exp(-k * excess / range);
            // Guard against floating-point underflow causing result == ceiling.
            // double.Epsilon is too small relative to large ceiling values (10000.0 - double.Epsilon == 10000.0).
            // Subtract one ULP by nudging with a relative epsilon instead.
            if (result >= ceiling) result = ceiling - ceiling * 1e-15;
            return result;
        }

        /// <summary>
        /// Returns a sequence of (raw, effective) samples across [0, maxRaw] for chart rendering.
        /// Useful for EconomyTuningWindow soft-cap preview.
        /// </summary>
        public static (double raw, double effective)[] Sample(
            SoftCapConfigSO config,
            double maxRaw,
            int sampleCount = 50)
        {
            if (config == null || sampleCount <= 0) return Array.Empty<(double, double)>();
            var result = new (double, double)[sampleCount];
            double step = maxRaw / System.Math.Max(1, sampleCount - 1);
            for (int i = 0; i < sampleCount; i++)
            {
                double raw = step * i;
                result[i]  = (raw, Apply(raw, config));
            }
            return result;
        }
    }
}
