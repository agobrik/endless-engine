namespace EndlessEngine.UI
{
    /// <summary>
    /// Display-only Gold value abbreviation per GDD Rule 6 (HUD System).
    /// Below 10,000: integer. 10K–999K: "10K". 1M–999.9M: "1.0M". 1B+: "1.0B"+
    /// Not configurable — thresholds are display convention, not balance data.
    /// </summary>
    public static class GoldFormatter
    {
        public static string Format(long value)
        {
            if (value < 0L)
                return "0";

            if (value < 10_000L)
                return value.ToString();

            if (value < 1_000_000L)
                return (value / 1_000L) + "K";

            if (value < 1_000_000_000L)
                return (value / 1_000_000f).ToString("0.0") + "M";

            return (value / 1_000_000_000f).ToString("0.0") + "B";
        }
    }
}
