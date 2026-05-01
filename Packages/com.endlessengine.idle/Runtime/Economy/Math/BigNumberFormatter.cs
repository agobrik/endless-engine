using System;
using EndlessEngine.Config;

namespace EndlessEngine.Economy.Math
{
    /// <summary>
    /// Formats a double value for IBigNumber.Format() — used by DoubleNumber and BigDouble.
    /// Delegates to EndlessEngine.Economy.BigNumberFormatter for the full implementation.
    /// BigNumberNotation enum lives in EndlessEngine.Config (shared with CurrencyConfigSO).
    /// </summary>
    internal static class BigNumberFormatter
    {
        public static string Format(double value, BigNumberNotation notation = BigNumberNotation.Letter, int decimalPlaces = 1)
            => Economy.BigNumberFormatter.Format(value, notation, decimalPlaces);
    }
}
