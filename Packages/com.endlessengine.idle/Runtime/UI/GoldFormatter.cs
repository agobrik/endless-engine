namespace EndlessEngine.UI
{
    /// <summary>
    /// Display-only Gold value abbreviation per GDD Rule 6 (HUD System).
    /// Below 10,000: integer. 10K–999K: "10K". 1M–999.9M: "1.0M". 1B+: "1.0B"+
    /// Not configurable — thresholds are display convention, not balance data.
    /// </summary>
    public static class GoldFormatter
    {
        public static string Format(double value)
        {
            if (value < 0.0) return "0";

            if (value < 10_000.0)
                return ((long)value).ToString();

            if (value < 1_000_000.0)
                return ((long)(value / 1_000.0)) + "K";

            if (value < 1_000_000_000.0)
                return (value / 1_000_000.0).ToString("0.0") + "M";

            if (value < 1_000_000_000_000.0)
                return (value / 1_000_000_000.0).ToString("0.0") + "B";

            if (value < 1_000_000_000_000_000.0)
                return (value / 1_000_000_000_000.0).ToString("0.0") + "T";

            return (value / 1e15).ToString("0.0e+0");
        }

        public static string Format(long value) => Format((double)value);
    }
}
