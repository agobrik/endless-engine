using System;
using EndlessEngine.Config;

namespace EndlessEngine.Economy.Math
{
    /// <summary>
    /// IBigNumber backed by IEEE 754 double.
    ///
    /// Precision: ~15-17 significant decimal digits.
    /// Range: up to ~1.8e308 (well beyond idle game requirements at this tier).
    /// Safe precision ceiling: values below 1e15 have full integer precision.
    /// Above ~1e15, increments smaller than the value's ULP are silently lost —
    /// this is the expected trade-off for double-backed idle games.
    ///
    /// Struct: zero heap allocation. All arithmetic returns new DoubleNumber.
    /// </summary>
    public readonly struct DoubleNumber : IBigNumber, IComparable<DoubleNumber>, IEquatable<DoubleNumber>
    {
        public static readonly DoubleNumber Zero = new DoubleNumber(0.0);
        public static readonly DoubleNumber One  = new DoubleNumber(1.0);

        private readonly double _value;

        public DoubleNumber(double value)
        {
            _value = double.IsNaN(value) ? 0.0 : value;
        }

        public DoubleNumber(long value) : this((double)value) { }

        // ── IBigNumber: Arithmetic ────────────────────────────────────────────────

        public IBigNumber Add(IBigNumber other)
            => new DoubleNumber(_value + other.ToDouble());

        public IBigNumber Subtract(IBigNumber other)
            => new DoubleNumber(_value - other.ToDouble());

        public IBigNumber Multiply(double scalar)
            => new DoubleNumber(_value * scalar);

        public IBigNumber Divide(double scalar)
        {
            if (scalar == 0.0) return new DoubleNumber(double.PositiveInfinity);
            return new DoubleNumber(_value / scalar);
        }

        // Struct-typed overloads (avoid boxing on hot path)
        public DoubleNumber Add(DoubleNumber other)      => new DoubleNumber(_value + other._value);
        public DoubleNumber Subtract(DoubleNumber other) => new DoubleNumber(_value - other._value);
        public DoubleNumber MultiplyStruct(double scalar) => new DoubleNumber(_value * scalar);
        public DoubleNumber DivideStruct(double scalar)
        {
            if (scalar == 0.0) return new DoubleNumber(double.PositiveInfinity);
            return new DoubleNumber(_value / scalar);
        }

        // ── IBigNumber: Comparison ────────────────────────────────────────────────

        public bool IsGreaterThan(IBigNumber other)          => _value > other.ToDouble();
        public bool IsGreaterThanOrEqual(IBigNumber other)   => _value >= other.ToDouble();
        public bool IsLessThan(IBigNumber other)             => _value < other.ToDouble();
        public bool IsLessThanOrEqual(IBigNumber other)      => _value <= other.ToDouble();

        public bool IsGreaterThan(DoubleNumber other)        => _value > other._value;
        public bool IsGreaterThanOrEqual(DoubleNumber other) => _value >= other._value;
        public bool IsLessThan(DoubleNumber other)           => _value < other._value;
        public bool IsLessThanOrEqual(DoubleNumber other)    => _value <= other._value;

        // ── IBigNumber: Conversion ────────────────────────────────────────────────

        public double ToDouble() => _value;

        public long ToLong()
        {
            if (_value >= (double)long.MaxValue) return long.MaxValue;
            if (_value <= (double)long.MinValue) return long.MinValue;
            return (long)_value;
        }

        // ── IBigNumber: Queries ───────────────────────────────────────────────────

        public bool IsZero     => _value == 0.0;
        public bool IsNegative => _value < 0.0;

        // ── IBigNumber: Display ───────────────────────────────────────────────────

        public string Format(BigNumberNotation notation = BigNumberNotation.Letter, int decimalPlaces = 1)
            => BigNumberFormatter.Format(_value, notation, decimalPlaces);

        public override string ToString() => Format();

        // ── IComparable / IEquatable ──────────────────────────────────────────────

        public int CompareTo(IBigNumber other)   => _value.CompareTo(other.ToDouble());
        public int CompareTo(DoubleNumber other) => _value.CompareTo(other._value);

        public bool Equals(IBigNumber other)    => _value == other.ToDouble();
        public bool Equals(DoubleNumber other)  => _value == other._value;
        public override bool Equals(object obj)
        {
            if (obj is DoubleNumber d) return Equals(d);
            if (obj is IBigNumber b)  return Equals(b);
            return false;
        }

        public override int GetHashCode() => _value.GetHashCode();

        // ── Operators ─────────────────────────────────────────────────────────────

        public static DoubleNumber operator +(DoubleNumber a, DoubleNumber b) => a.Add(b);
        public static DoubleNumber operator -(DoubleNumber a, DoubleNumber b) => a.Subtract(b);
        public static DoubleNumber operator *(DoubleNumber a, double scalar)  => a.MultiplyStruct(scalar);
        public static DoubleNumber operator *(double scalar, DoubleNumber a)  => a.MultiplyStruct(scalar);
        public static DoubleNumber operator /(DoubleNumber a, double scalar)  => a.DivideStruct(scalar);

        public static bool operator >(DoubleNumber a, DoubleNumber b)  => a._value > b._value;
        public static bool operator >=(DoubleNumber a, DoubleNumber b) => a._value >= b._value;
        public static bool operator <(DoubleNumber a, DoubleNumber b)  => a._value < b._value;
        public static bool operator <=(DoubleNumber a, DoubleNumber b) => a._value <= b._value;
        public static bool operator ==(DoubleNumber a, DoubleNumber b) => a._value == b._value;
        public static bool operator !=(DoubleNumber a, DoubleNumber b) => a._value != b._value;

        public static implicit operator DoubleNumber(double v) => new DoubleNumber(v);
        public static implicit operator DoubleNumber(long v)   => new DoubleNumber(v);
        public static implicit operator double(DoubleNumber n) => n._value;
    }
}
