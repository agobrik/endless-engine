using System;
using EndlessEngine.Config;

namespace EndlessEngine.Economy.Math
{
    /// <summary>
    /// Abstraction over numeric backends for large idle-game values.
    ///
    /// v1.1 ships DoubleNumber (IEEE 754 double, ~15-17 significant digits, up to ~1.8e308).
    /// v1.3+ will introduce BigDouble (mantissa+exponent) for games that exceed 1e17 precision.
    ///
    /// EconomyConfigSO.NumberBackend selects the backend per-game so game code never changes.
    ///
    /// All implementations must be structs (value type, zero heap alloc on hot path).
    /// </summary>
    public interface IBigNumber : IComparable<IBigNumber>, IEquatable<IBigNumber>
    {
        // ── Arithmetic ────────────────────────────────────────────────────────────

        IBigNumber Add(IBigNumber other);
        IBigNumber Subtract(IBigNumber other);
        IBigNumber Multiply(double scalar);
        IBigNumber Divide(double scalar);

        // ── Comparison ────────────────────────────────────────────────────────────

        bool IsGreaterThan(IBigNumber other);
        bool IsGreaterThanOrEqual(IBigNumber other);
        bool IsLessThan(IBigNumber other);
        bool IsLessThanOrEqual(IBigNumber other);

        // ── Conversion ────────────────────────────────────────────────────────────

        double ToDouble();

        /// <summary>
        /// Converts to long. Values above long.MaxValue are clamped.
        /// Use only when you know the value fits (e.g. legacy save data).
        /// </summary>
        long ToLong();

        // ── Queries ───────────────────────────────────────────────────────────────

        bool IsZero { get; }
        bool IsNegative { get; }

        // ── Display ───────────────────────────────────────────────────────────────

        /// <summary>Formats the value for UI display using BigNumberFormatter.</summary>
        string Format(BigNumberNotation notation = BigNumberNotation.Letter, int decimalPlaces = 1);
    }

    /// <summary>Selects the numeric backend per-game in EconomyConfigSO.</summary>
    public enum NumberBackend
    {
        /// <summary>IEEE 754 double. Precise up to ~1e15. Suitable for games that stay below 1e17.</summary>
        DoubleNumber = 0,

        /// <summary>Mantissa + exponent BigDouble. Precise at any scale. (v1.3+, not yet implemented)</summary>
        BigDouble = 1,
    }
}
