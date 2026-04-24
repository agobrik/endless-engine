using System;
using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;

namespace EndlessEngine.Flow
{
    /// <summary>
    /// Applies a temporary time-scale multiplier to TickEngine.
    /// Supports multiple presets (2×, 4×, etc.) defined in TimeBoostConfigSO.
    ///
    /// Activation:
    ///   - Free / ad-based: TryActivate(config) — no cost check.
    ///   - Gold-cost:       TryActivatePaid(config) — deducts from EconomyService.
    ///
    /// Only one boost is active at a time; activating a new one replaces the existing one.
    ///
    /// Bootstrap wiring:
    ///   timeBoostService.Initialize(tickEngine, economyService);
    /// </summary>
    public class TimeBoostService : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when a boost becomes active. Parameter: config, remaining seconds.</summary>
        public static event Action<TimeBoostConfigSO, float> OnBoostStarted;

        /// <summary>Fires every second while a boost is active. Parameter: remaining seconds.</summary>
        public static event Action<float> OnBoostTick;

        /// <summary>Fires when the boost expires or is cancelled.</summary>
        public static event Action OnBoostEnded;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private TickEngine      _tickEngine;
        private EconomyService  _economyService;
        private float           _baseTimeScale = 1f;

        private bool   _active;
        private float  _remainingSeconds;
        private TimeBoostConfigSO _activeConfig;

        private Coroutine _timerCoroutine;

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(TickEngine tickEngine, EconomyService economyService = null)
        {
            _tickEngine     = tickEngine;
            _economyService = economyService;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Whether a boost is currently active.</summary>
        public bool IsActive => _active;

        /// <summary>Seconds remaining on the current boost (0 if inactive).</summary>
        public float RemainingSeconds => _remainingSeconds;

        /// <summary>The currently active boost config, or null.</summary>
        public TimeBoostConfigSO ActiveConfig => _activeConfig;

        /// <summary>
        /// Activates a boost without any gold cost (ad reward, debug, etc.).
        /// Replaces any existing active boost.
        /// </summary>
        public void TryActivate(TimeBoostConfigSO config)
        {
            if (config == null) return;
            BeginBoost(config);
        }

        /// <summary>
        /// Activates a boost after deducting the gold cost from EconomyService.
        /// Returns false if insufficient gold or no EconomyService wired.
        /// </summary>
        public bool TryActivatePaid(TimeBoostConfigSO config)
        {
            if (config == null) return false;
            if (config.GoldCost <= 0) { BeginBoost(config); return true; }
            if (_economyService == null) return false;
            if (_economyService.CurrentResources < config.GoldCost) return false;

            _economyService.DeductResources(config.GoldCost);
            BeginBoost(config);
            return true;
        }

        /// <summary>Cancels the active boost immediately, restoring the base time scale.</summary>
        public void Cancel()
        {
            if (!_active) return;
            StopBoost();
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private void BeginBoost(TimeBoostConfigSO config)
        {
            // Cancel any running boost first
            if (_timerCoroutine != null) { StopCoroutine(_timerCoroutine); _timerCoroutine = null; }

            _baseTimeScale = 1f; // always restore to default before applying new multiplier
            if (_tickEngine != null) _tickEngine.TimeScale = config.TimeScaleMultiplier;

            _active           = true;
            _remainingSeconds = config.DurationSeconds;
            _activeConfig     = config;

            OnBoostStarted?.Invoke(config, _remainingSeconds);
            _timerCoroutine = StartCoroutine(BoostTimer());
        }

        private IEnumerator BoostTimer()
        {
            while (_remainingSeconds > 0f)
            {
                yield return new WaitForSeconds(1f);
                _remainingSeconds = Mathf.Max(0f, _remainingSeconds - 1f);
                OnBoostTick?.Invoke(_remainingSeconds);
            }
            StopBoost();
        }

        private void StopBoost()
        {
            if (_timerCoroutine != null)
            {
                StopCoroutine(_timerCoroutine);
                _timerCoroutine = null;
            }

            if (_tickEngine != null) _tickEngine.TimeScale = _baseTimeScale;

            _active           = false;
            _remainingSeconds = 0f;
            _activeConfig     = null;

            OnBoostEnded?.Invoke();
        }

        private void OnDestroy() => StopBoost();

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnBoostStarted = null;
            OnBoostTick    = null;
            OnBoostEnded   = null;
        }

        /// <summary>Directly sets remaining time for deterministic expiry tests.</summary>
        public void InjectRemainingForTesting(float seconds) => _remainingSeconds = seconds;

        /// <summary>Expose internal active state for assertions.</summary>
        public bool IsActiveForTesting => _active;
#endif
    }
}
