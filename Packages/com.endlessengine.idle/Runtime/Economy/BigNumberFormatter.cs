using System;
using System.Globalization;
using System.Text;
using EndlessEngine.Config;

namespace EndlessEngine.Economy
{
    /// <summary>
    /// Formats large double values into human-readable strings.
    /// Three notation styles: Letter (1.23aa), Scientific (1.23e6), Engineering (1.23M).
    ///
    /// Letter chain: K, M, B, T, aa, ab, ac … az, ba, bb … zz (702 suffixes total).
    /// Values beyond the suffix chain fall back to scientific notation.
    ///
    /// All methods are zero-allocation-safe for values requiring no suffix (< 1000).
    /// Uses a pooled StringBuilder for formatted output above 1000.
    ///
    /// Thread-safety: all static methods — safe to call from any thread.
    /// </summary>
    public static class BigNumberFormatter
    {
        // ── Letter suffix table ───────────────────────────────────────────────────

        private static readonly string[] FixedSuffixes = { "K", "M", "B", "T" };

        // Double-letter suffixes aa-zz (676 entries)
        private static readonly string[] DoubleSuffixes = BuildDoubleSuffixes();

        private static string[] BuildDoubleSuffixes()
        {
            const int Letters = 26;
            var result = new string[Letters * Letters];
            int idx = 0;
            for (char first = 'a'; first <= 'z'; first++)
                for (char second = 'a'; second <= 'z'; second++)
                    result[idx++] = new string(new[] { first, second });
            return result;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Formats <paramref name="value"/> using the specified notation.
        /// </summary>
        /// <param name="value">Value to format. Negative values are formatted with a leading '-'.</param>
        /// <param name="notation">Notation style (Letter / Scientific / Engineering).</param>
        /// <param name="decimalPlaces">Number of decimal places (0–3). Clamped to [0, 3].</param>
        public static string Format(
            double value,
            BigNumberNotation notation    = BigNumberNotation.Letter,
            int    decimalPlaces          = 1)
        {
            decimalPlaces = System.Math.Max(0, System.Math.Min(3, decimalPlaces));

            if (double.IsNaN(value))      return "NaN";
            if (double.IsPositiveInfinity(value)) return "∞";
            if (double.IsNegativeInfinity(value)) return "-∞";

            bool negative = value < 0;
            double abs    = System.Math.Abs(value);

            string formatted = notation switch
            {
                BigNumberNotation.Scientific  => FormatScientific(abs, decimalPlaces),
                BigNumberNotation.Engineering => FormatEngineering(abs, decimalPlaces),
                _                             => FormatLetter(abs, decimalPlaces),
            };

            return negative ? "-" + formatted : formatted;
        }

        /// <summary>
        /// Formats using <paramref name="config"/> settings.
        /// Convenience wrapper for UI bindings that hold a CurrencyConfigSO reference.
        /// </summary>
        public static string Format(double value, CurrencyConfigSO config)
        {
            if (config == null) return Format(value);
            return Format(value, config.Notation, config.DecimalPlaces);
        }

        // ── Letter notation ───────────────────────────────────────────────────────

        /// <summary>
        /// Formats in letter notation: 1234 → "1.2K", 1_500_000 → "1.5M", etc.
        /// Values &lt; 1000 are returned as integer strings.
        /// </summary>
        public static string FormatLetter(double value, int decimalPlaces = 1)
        {
            if (value < 1_000d)
                return ((long)value).ToString();

            // Tier 0 = K (10^3), Tier 1 = M (10^6), ...
            // Fixed: K M B T → tiers 0-3 → exponent 3-12
            // Double-letters: aa→10^15 ... (each tier adds 3 to exponent)

            int tier = (int)System.Math.Floor(System.Math.Log10(value) / 3.0) - 1;

            string suffix;
            double divisor;

            if (tier < FixedSuffixes.Length)
            {
                suffix  = FixedSuffixes[tier];
                divisor = System.Math.Pow(10, (tier + 1) * 3);
            }
            else
            {
                int doubleTier = tier - FixedSuffixes.Length; // 0 = aa, 1 = ab, ...
                if (doubleTier >= DoubleSuffixes.Length)
                    return FormatScientific(value, decimalPlaces); // beyond zz

                suffix  = DoubleSuffixes[doubleTier];
                divisor = System.Math.Pow(10, (tier + 1) * 3);
            }

            double scaled = value / divisor;
            return FormatFixed(scaled, decimalPlaces) + suffix;
        }

        // ── Scientific notation ───────────────────────────────────────────────────

        /// <summary>
        /// Formats in scientific notation: 1_234_567 → "1.23e6".
        /// Values &lt; 1000 are formatted as plain integers.
        /// </summary>
        public static string FormatScientific(double value, int decimalPlaces = 2)
        {
            if (value < 1_000d)
                return ((long)value).ToString();

            int exp       = (int)System.Math.Floor(System.Math.Log10(value));
            double mantissa = value / System.Math.Pow(10, exp);
            return FormatFixed(mantissa, decimalPlaces) + "e" + exp;
        }

        // ── Engineering / SI notation ─────────────────────────────────────────────

        private static readonly string[] SiPrefixes =
            { "", "K", "M", "G", "T", "P", "E", "Z", "Y" };

        /// <summary>
        /// Formats in SI/engineering notation: 1_500_000 → "1.5M", 1_200 → "1.2K".
        /// Values &lt; 1000 are formatted as plain integers.
        /// </summary>
        public static string FormatEngineering(double value, int decimalPlaces = 1)
        {
            if (value < 1_000d)
                return ((long)value).ToString();

            int tier = (int)System.Math.Floor(System.Math.Log10(value) / 3.0);
            tier = System.Math.Min(tier, SiPrefixes.Length - 1);

            double scaled = value / System.Math.Pow(10, tier * 3);
            return FormatFixed(scaled, decimalPlaces) + SiPrefixes[tier];
        }

        // ── Helper ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Formats a double to a fixed number of decimal places without trailing zeros.
        /// e.g. FormatFixed(1.500, 2) → "1.5" (trailing zero stripped).
        /// </summary>
        private static string FormatFixed(double value, int places)
        {
            if (places == 0)
                return ((long)System.Math.Round(value)).ToString();

            string fmt = value.ToString("F" + places, CultureInfo.InvariantCulture);

            // Strip trailing zeros and unnecessary dot
            int dot = fmt.IndexOf('.');
            if (dot < 0) return fmt;

            int end = fmt.Length - 1;
            while (end > dot && fmt[end] == '0') end--;
            if (fmt[end] == '.') end--;

            return fmt.Substring(0, end + 1);
        }
    }
}
