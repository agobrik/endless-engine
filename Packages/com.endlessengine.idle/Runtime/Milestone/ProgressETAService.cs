using System;
using UnityEngine;
using EndlessEngine.Economy;
using EndlessEngine.Generator;

namespace EndlessEngine.Milestone
{
    /// <summary>
    /// Calculates estimated time-to-reach for arbitrary gold targets based on
    /// current passive income rate.
    ///
    /// ETA = (target - currentGold) / incomeRatePerSecond
    ///
    /// Rate is sampled from PassiveIncomeService (via GeneratorSystem.CalculateTotalYield)
    /// plus ClickYield contributions when available.
    ///
    /// Wire-up: VerticalSliceBootstrap → ProgressETAService.Initialize().
    /// </summary>
    public class ProgressETAService : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires each time the ETA for the tracked target changes significantly.
        /// Parameters: targetGold, etaSeconds (-1 if unreachable with current rate).</summary>
        public static event Action<long, float> OnETAUpdated;

        // ── Dependencies ──────────────────────────────────────────────────────────

        private EconomyService  _economyService;
        private GeneratorSystem _generatorSystem; // optional

        // ── Tracked target ────────────────────────────────────────────────────────

        private long  _targetGold;
        private float _lastETA = -1f;

        // ── Config ────────────────────────────────────────────────────────────────

        [Tooltip("How often (seconds) to recalculate the ETA. Lower = more responsive but more CPU.")]
        [SerializeField] private float _updateIntervalSeconds = 5f;

        private float _timeSinceUpdate;

        private bool _initialized;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Initialize the service. GeneratorSystem is the primary income rate source.
        /// </summary>
        public void Initialize(EconomyService economyService, GeneratorSystem generatorSystem = null)
        {
            _economyService  = economyService;
            _generatorSystem = generatorSystem;
            _initialized     = true;
        }

        private void Update()
        {
            if (!_initialized || _targetGold <= 0) return;

            _timeSinceUpdate += Time.deltaTime;
            if (_timeSinceUpdate < _updateIntervalSeconds) return;
            _timeSinceUpdate = 0f;

            float eta = CalculateETA(_targetGold);
            if (Math.Abs(eta - _lastETA) > 1f) // only fire if changed by >1 second
            {
                _lastETA = eta;
                OnETAUpdated?.Invoke(_targetGold, eta);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Set the gold target to track. Pass 0 to disable tracking.
        /// Fires OnETAUpdated immediately with the current estimate.
        /// </summary>
        public void SetTarget(long targetGold)
        {
            _targetGold      = targetGold;
            _timeSinceUpdate = _updateIntervalSeconds; // force update next frame
        }

        /// <summary>
        /// Calculate ETA in seconds for an arbitrary gold target.
        /// Returns -1 if the target is already met or income rate is zero/negative.
        /// </summary>
        public float CalculateETA(double targetGold)
        {
            if (_economyService == null) return -1f;

            double current = _economyService.CurrentResources;
            if (current >= targetGold) return 0f;

            float ratePerSecond = GetCurrentIncomeRate();
            if (ratePerSecond <= 0f) return -1f;

            return (float)((targetGold - current) / ratePerSecond);
        }

        /// <summary>
        /// Calculate ETA for a secondary currency target.
        /// Returns -1 if the currency rate is unknown (no generator produces it directly).
        /// </summary>
        public float CalculateETACurrency(string currencyId, double target, double currentBalance, double ratePerSecond)
        {
            if (currentBalance >= target) return 0f;
            if (ratePerSecond <= 0) return -1f;
            return (float)((target - currentBalance) / ratePerSecond);
        }

        /// <summary>
        /// Returns a human-readable ETA string.
        /// Examples: "2 min 30 sec", "1 hr 15 min", "Reached!", "Unknown"
        /// </summary>
        public string FormatETA(float etaSeconds)
        {
            if (etaSeconds < 0f) return "Unknown";
            if (etaSeconds == 0f) return "Reached!";

            int total = Mathf.CeilToInt(etaSeconds);
            if (total < 60) return $"{total} sec";

            int minutes = total / 60;
            int seconds = total % 60;
            if (minutes < 60) return seconds > 0 ? $"{minutes} min {seconds} sec" : $"{minutes} min";

            int hours = minutes / 60;
            int mins  = minutes % 60;
            if (hours < 24) return mins > 0 ? $"{hours} hr {mins} min" : $"{hours} hr";

            int days = hours / 24;
            int hrs  = hours % 24;
            return hrs > 0 ? $"{days}d {hrs} hr" : $"{days}d";
        }

        /// <summary>
        /// Returns the current passive income rate in gold/second.
        /// Sources: GeneratorSystem.CalculateTotalYield (primary).
        /// </summary>
        public float GetCurrentIncomeRate()
        {
            if (_generatorSystem != null)
                return (float)_generatorSystem.CalculateTotalYield();
            return 0f;
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting() => OnETAUpdated = null;

        public void SetUpdateIntervalForTesting(float seconds) => _updateIntervalSeconds = seconds;
#endif
    }
}
