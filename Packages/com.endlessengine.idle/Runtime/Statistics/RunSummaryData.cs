using System;

namespace EndlessEngine.Statistics
{
    /// <summary>
    /// Immutable snapshot of a single run's results.
    /// Populated by RunSessionManager at run-end and passed to PostRunScreen.
    ///
    /// All fields are value types — no ScriptableObject refs so it can be
    /// serialized cheaply (e.g. for history logging).
    /// </summary>
    [Serializable]
    public class RunSummaryData
    {
        /// <summary>UTC timestamp when the run started.</summary>
        public DateTime StartTime;

        /// <summary>UTC timestamp when the run ended.</summary>
        public DateTime EndTime;

        /// <summary>Run duration in seconds.</summary>
        public float DurationSeconds => (float)(EndTime - StartTime).TotalSeconds;

        /// <summary>Gold earned during this run (excludes idle/offline).</summary>
        public long GoldEarned;

        /// <summary>Enemies killed during this run.</summary>
        public int KillCount;

        /// <summary>Highest wave number reached this run.</summary>
        public int MaxWave;

        /// <summary>Prestige count at run-start (baseline reference).</summary>
        public int PrestigeCountAtStart;

        /// <summary>True if a prestige was triggered during this run.</summary>
        public bool PrestigePerformed;

        /// <summary>Number of upgrade cards accepted during this run.</summary>
        public int UpgradesAccepted;

        /// <summary>Cascade multiplier at run-end (product of all ascension layers).</summary>
        public float CascadeMultiplier;

        /// <summary>Run-end gold income rate in gold/sec.</summary>
        public float FinalIncomeRate;

        // ── Factory ───────────────────────────────────────────────────────────────

        public static RunSummaryData Create(
            DateTime  startTime,
            DateTime  endTime,
            long      goldEarned,
            int       killCount,
            int       maxWave,
            int       prestigeCountAtStart,
            bool      prestigePerformed,
            int       upgradesAccepted,
            float     cascadeMultiplier,
            float     finalIncomeRate)
        {
            return new RunSummaryData
            {
                StartTime             = startTime,
                EndTime               = endTime,
                GoldEarned            = goldEarned,
                KillCount             = killCount,
                MaxWave               = maxWave,
                PrestigeCountAtStart  = prestigeCountAtStart,
                PrestigePerformed     = prestigePerformed,
                UpgradesAccepted      = upgradesAccepted,
                CascadeMultiplier     = cascadeMultiplier,
                FinalIncomeRate       = finalIncomeRate
            };
        }
    }
}
