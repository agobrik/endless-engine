using System;
using EndlessEngine.Config;

namespace EndlessEngine.Economy.Math
{
    /// <summary>
    /// IBigNumber backed by mantissa × 10^exponent (base-10 floating point).
    ///
    /// Precision: ~15–17 significant digits (same as double for the mantissa).
    /// Range: effectively unlimited — exponent is a 64-bit integer, so the
    ///        maximum representable value is ~1.8e308 × 10^(long.MaxValue).
    ///
    /// Use case: deep-prestige idle games where values routinely exceed 1e300.
    ///           Below 1e15, DoubleNumber is faster and equally precise.
    ///
    /// Normalisation invariant: mantissa is always in [1.0, 10.0) unless the
    /// value is exactly zero. Zero is represented as (0.0, 0).
    ///
    /// Struct: zero heap allocation on the hot path.
    /// All arithmetic returns a new normalised BigDouble.
    ///
    /// Activation: Set EconomyConfigSO.NumberBackend = BigDouble,
    ///             then use BigNumberFactory.Create() instead of new DoubleNumber().
    /// </summary>
    public readonly struct BigDouble : IBigNumber, IComparable<BigDouble>, IEquatable<BigDouble>
    {
        // ── Constants ─────────────────────────────────────────────────────────────

        public static readonly BigDouble Zero = new BigDouble(0.0, 0);
        public static readonly BigDouble One  = new BigDouble(1.0, 0);
        public static readonly BigDouble Ten  = new BigDouble(1.0, 1);

        private const double Log10E    = 0.4342944819032518; // log10(e), for Ln→Log10
        private const double Ln10      = 2.302585092994046;  // ln(10)

        // ── Fields ────────────────────────────────────────────────────────────────

        /// <summary>Normalised mantissa in [1, 10) or 0 for zero value.</summary>
        public readonly double Mantissa;

        /// <summary>Base-10 exponent. Value = Mantissa × 10^Exponent.</summary>
        public readonly long   Exponent;

        // ── Construction ──────────────────────────────────────────────────────────

        public BigDouble(double mantissa, long exponent)
        {
            if (double.IsNaN(mantissa) || mantissa == 0.0)
            {
                Mantissa = 0.0;
                Exponent = 0;
                return;
            }
            if (double.IsInfinity(mantissa))
            {
                Mantissa = mantissa;
                Exponent = 0;
                return;
            }

            // Normalise: mantissa into [1, 10)
            bool negative = mantissa < 0;
            double abs    = System.Math.Abs(mantissa);

            int adjust    = (int)System.Math.Floor(System.Math.Log10(abs));
            double m      = abs / System.Math.Pow(10.0, adjust);

            // Clamp floating-point edge cases (e.g. 9.9999999... → 10.0)
            if (m >= 10.0) { m /= 10.0; adjust++; }
            if (m < 1.0)   { m *= 10.0; adjust--; }

            Mantissa = negative ? -m : m;
            Exponent = exponent + adjust;
        }

        /// <summary>Construct from a plain double (convenience — normalises automatically).</summary>
        public BigDouble(double value) : this(value, 0) { }

        /// <summary>Construct from a long (exact).</summary>
        public BigDouble(long value) : this((double)value, 0) { }

        // ── IBigNumber: Arithmetic ────────────────────────────────────────────────

        public IBigNumber Add(IBigNumber other)      => Add(FromIBigNumber(other));
        public IBigNumber Subtract(IBigNumber other) => Subtract(FromIBigNumber(other));
        IBigNumber IBigNumber.Multiply(double scalar) => Multiply(scalar);
        IBigNumber IBigNumber.Divide(double scalar)   => Divide(scalar);

        public BigDouble Add(BigDouble other)
        {
            if (other.IsZero) return this;
            if (IsZero)       return other;

            // Align exponents — shift smaller to match larger
            long expDiff = Exponent - other.Exponent;

            if (expDiff > 17)  return this;           // other is negligible
            if (expDiff < -17) return other;          // this is negligible

            double m1 = Mantissa;
            double m2 = other.Mantissa;

            if (expDiff >= 0)
                m2 /= System.Math.Pow(10.0, expDiff);
            else
                m1 /= System.Math.Pow(10.0, -expDiff);

            long baseExp = expDiff >= 0 ? Exponent : other.Exponent;
            return new BigDouble(m1 + m2, baseExp);
        }

        public BigDouble Subtract(BigDouble other)
        {
            if (other.IsZero) return this;
            return Add(new BigDouble(-other.Mantissa, other.Exponent));
        }

        public BigDouble Multiply(double scalar)
        {
            if (scalar == 0.0 || IsZero) return Zero;
            return new BigDouble(Mantissa * scalar, Exponent);
        }

        public BigDouble Multiply(BigDouble other)
        {
            if (IsZero || other.IsZero) return Zero;
            return new BigDouble(Mantissa * other.Mantissa, Exponent + other.Exponent);
        }

        public BigDouble Divide(double scalar)
        {
            if (scalar == 0.0) return new BigDouble(double.PositiveInfinity, 0);
            if (IsZero)        return Zero;
            return new BigDouble(Mantissa / scalar, Exponent);
        }

        public BigDouble Divide(BigDouble other)
        {
            if (other.IsZero)  return new BigDouble(double.PositiveInfinity, 0);
            if (IsZero)        return Zero;
            return new BigDouble(Mantissa / other.Mantissa, Exponent - other.Exponent);
        }

        // ── Power / Log ───────────────────────────────────────────────────────────

        /// <summary>Returns this^exponent. Uses logarithms for large exponents.</summary>
        public BigDouble Pow(double exp)
        {
            if (IsZero) return Zero;
            // log10(result) = exp × log10(this) = exp × (Exponent + log10(Mantissa))
            double log10 = exp * (Exponent + System.Math.Log10(System.Math.Abs(Mantissa)));
            long   resExp = (long)System.Math.Floor(log10);
            double resMant = System.Math.Pow(10.0, log10 - resExp);
            return new BigDouble(resMant, resExp);
        }

        /// <summary>Returns log10(this). Returns NaN for non-positive values.</summary>
        public double Log10()
        {
            if (IsZero || IsNegative) return double.NaN;
            return Exponent + System.Math.Log10(Mantissa);
        }

        /// <summary>Returns the natural log of this. Returns NaN for non-positive values.</summary>
        public double Ln() => Log10() * Ln10;

        // ── IBigNumber: Comparison ────────────────────────────────────────────────

        public bool IsGreaterThan(IBigNumber other)        => CompareTo(FromIBigNumber(other)) > 0;
        public bool IsGreaterThanOrEqual(IBigNumber other) => CompareTo(FromIBigNumber(other)) >= 0;
        public bool IsLessThan(IBigNumber other)           => CompareTo(FromIBigNumber(other)) < 0;
        public bool IsLessThanOrEqual(IBigNumber other)    => CompareTo(FromIBigNumber(other)) <= 0;

        public bool IsGreaterThan(BigDouble other)        => CompareTo(other) > 0;
        public bool IsGreaterThanOrEqual(BigDouble other) => CompareTo(other) >= 0;
        public bool IsLessThan(BigDouble other)           => CompareTo(other) < 0;
        public bool IsLessThanOrEqual(BigDouble other)    => CompareTo(other) <= 0;

        // ── IBigNumber: Conversion ────────────────────────────────────────────────

        public double ToDouble()
        {
            if (IsZero) return 0.0;
            // Clamp to double range
            if (Exponent > 308)  return Mantissa > 0 ? double.PositiveInfinity : double.NegativeInfinity;
            if (Exponent < -323) return 0.0;
            return Mantissa * System.Math.Pow(10.0, Exponent);
        }

        public long ToLong()
        {
            double d = ToDouble();
            if (d >= (double)long.MaxValue) return long.MaxValue;
            if (d <= (double)long.MinValue) return long.MinValue;
            return (long)d;
        }

        // ── IBigNumber: Queries ───────────────────────────────────────────────────

        public bool IsZero     => Mantissa == 0.0;
        public bool IsNegative => Mantissa < 0.0;
        public bool IsInfinite => double.IsInfinity(Mantissa);

        // ── IBigNumber: Display ───────────────────────────────────────────────────

        public string Format(BigNumberNotation notation = BigNumberNotation.Letter, int decimalPlaces = 1)
        {
            // For values within double range, delegate to BigNumberFormatter
            if (Exponent <= 308 && Exponent >= -308)
                return BigNumberFormatter.Format(ToDouble(), notation, decimalPlaces);

            // Beyond double range: always use scientific / extended notation
            double m = Mantissa;
            decimalPlaces = System.Math.Max(0, System.Math.Min(3, decimalPlaces));
            string mantissaStr = m.ToString("F" + decimalPlaces,
                System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
            return $"{mantissaStr}e{Exponent}";
        }

        public override string ToString() => Format();

        // ── IComparable / IEquatable ──────────────────────────────────────────────

        public int CompareTo(IBigNumber other)  => CompareTo(FromIBigNumber(other));
        public int CompareTo(BigDouble other)
        {
            // Handle zero / sign cases
            int signA = IsZero ? 0 : (IsNegative ? -1 : 1);
            int signB = other.IsZero ? 0 : (other.IsNegative ? -1 : 1);
            if (signA != signB) return signA.CompareTo(signB);
            if (signA == 0)     return 0;

            // Same sign: compare exponent then mantissa
            int expCmp = Exponent.CompareTo(other.Exponent);
            if (expCmp != 0) return signA > 0 ? expCmp : -expCmp;
            return Mantissa.CompareTo(other.Mantissa);
        }

        public bool Equals(IBigNumber other)  => CompareTo(other) == 0;
        public bool Equals(BigDouble other)   => Mantissa == other.Mantissa && Exponent == other.Exponent;
        public override bool Equals(object obj)
        {
            if (obj is BigDouble b) return Equals(b);
            if (obj is IBigNumber n) return Equals(n);
            return false;
        }
        public override int GetHashCode() => HashCode.Combine(Mantissa, Exponent);

        // ── Operators ─────────────────────────────────────────────────────────────

        public static BigDouble operator +(BigDouble a, BigDouble b) => a.Add(b);
        public static BigDouble operator -(BigDouble a, BigDouble b) => a.Subtract(b);
        public static BigDouble operator *(BigDouble a, BigDouble b) => a.Multiply(b);
        public static BigDouble operator *(BigDouble a, double s)    => a.Multiply(s);
        public static BigDouble operator *(double s, BigDouble a)    => a.Multiply(s);
        public static BigDouble operator /(BigDouble a, BigDouble b) => a.Divide(b);
        public static BigDouble operator /(BigDouble a, double s)    => a.Divide(s);

        public static bool operator >(BigDouble a, BigDouble b)  => a.CompareTo(b) > 0;
        public static bool operator >=(BigDouble a, BigDouble b) => a.CompareTo(b) >= 0;
        public static bool operator <(BigDouble a, BigDouble b)  => a.CompareTo(b) < 0;
        public static bool operator <=(BigDouble a, BigDouble b) => a.CompareTo(b) <= 0;
        public static bool operator ==(BigDouble a, BigDouble b) => a.Equals(b);
        public static bool operator !=(BigDouble a, BigDouble b) => !a.Equals(b);

        public static implicit operator BigDouble(double v) => new BigDouble(v);
        public static implicit operator BigDouble(long v)   => new BigDouble(v);
        public static explicit operator double(BigDouble n) => n.ToDouble();

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static BigDouble FromIBigNumber(IBigNumber n)
        {
            if (n is BigDouble bd) return bd;
            return new BigDouble(n.ToDouble());
        }
    }
}
