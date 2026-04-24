using System;
using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.Flow
{
    /// <summary>
    /// Manages a single timed run session.
    /// Starts the countdown when GameFlowStateMachine enters InRun,
    /// ends the run when timer reaches zero.
    ///
    /// Also tracks resources earned during the run for the PostRun summary.
    /// </summary>
    public class RunSessionManager : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires every frame during a run with remaining seconds.</summary>
        public static event Action<float> OnRunTimerUpdated;

        /// <summary>Fires when run ends (timer → 0 or forced end). Parameter: gold earned this run.</summary>
        public static event Action<long> OnRunEnded;

        // ── Dependencies ──────────────────────────────────────────────────────────

        private GameFlowStateMachine _gameFlow;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private bool  _runActive;
        private float _remainingSeconds;
        private long  _goldAtRunStart;
        private long  _goldEarnedThisRun;

        // ── Public accessors ──────────────────────────────────────────────────────

        public float RemainingSeconds  => _remainingSeconds;
        public float TotalRunSeconds   { get; private set; }
        public long  GoldEarnedThisRun => _goldEarnedThisRun;
        public bool  IsRunActive       => _runActive;

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(GameFlowStateMachine gameFlow)
        {
            _gameFlow = gameFlow;
            GameFlowStateMachine.OnEnteredRun    += HandleRunStarted;
            GameFlowStateMachine.OnEnteredPostRun += HandleRunEnded;
        }

        private void OnDestroy()
        {
            GameFlowStateMachine.OnEnteredRun    -= HandleRunStarted;
            GameFlowStateMachine.OnEnteredPostRun -= HandleRunEnded;
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Update()
        {
            if (!_runActive) return;

            _remainingSeconds -= Time.deltaTime;
            OnRunTimerUpdated?.Invoke(_remainingSeconds);

            if (_remainingSeconds <= 0f)
            {
                _remainingSeconds = 0f;
                _runActive = false;
                _gameFlow?.EndRun();
            }
        }

        // ── Handlers ──────────────────────────────────────────────────────────────

        private void HandleRunStarted()
        {
            RunConfigSO cfg = null;
            try { cfg = ConfigRegistry.Run; } catch { }

            TotalRunSeconds   = cfg != null ? cfg.RunDurationSeconds : 120f;
            _remainingSeconds = TotalRunSeconds;
            _goldAtRunStart   = Economy.EconomyService.CurrentResourcesStatic;
            _goldEarnedThisRun = 0L;
            _runActive        = true;

            Debug.Log($"[RunSession] Run started. Duration={TotalRunSeconds}s");
        }

        private void HandleRunEnded()
        {
            _runActive = false;
            _goldEarnedThisRun = Economy.EconomyService.CurrentResourcesStatic - _goldAtRunStart;
            if (_goldEarnedThisRun < 0) _goldEarnedThisRun = 0;

            Debug.Log($"[RunSession] Run ended. Gold earned={_goldEarnedThisRun}");
            OnRunEnded?.Invoke(_goldEarnedThisRun);
        }

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void ForceEndRunForTesting() => _gameFlow?.EndRun();

        public static void ClearSubscribersForTesting()
        {
            OnRunTimerUpdated = null;
            OnRunEnded        = null;
        }
#endif
    }
}
