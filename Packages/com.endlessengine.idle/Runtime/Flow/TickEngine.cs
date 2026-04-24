using System;
using UnityEngine;

namespace EndlessEngine.Flow
{
    /// <summary>
    /// Game clock for passive income. Fires OnTick at a configurable interval,
    /// independent of frame rate. Supports pause/resume and time scale.
    ///
    /// All idle income systems (PassiveIncomeService, OfflineTimeCalculator)
    /// subscribe to OnTick rather than using Update() directly — this gives a
    /// single choke point for time control (pause menus, 2x speed, etc.).
    ///
    /// Usage: Place on the Bootstrap GameObject. Call Pause()/Resume() from
    /// GameFlowStateMachine state transitions as needed.
    /// </summary>
    public class TickEngine : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires every tick. Parameter is the effective delta-time for this tick
        /// (TickIntervalSeconds * TimeScale), so subscribers can multiply yield
        /// without needing to know the tick rate.
        /// </summary>
        public static event Action<float> OnTick;

        // ── Configuration ─────────────────────────────────────────────────────────

        /// <summary>
        /// How often the tick fires in real seconds (before TimeScale).
        /// Default 1.0 = once per second. Reduce for smoother income on fast speeds.
        /// </summary>
        [Tooltip("Tick interval in real seconds. Default 1.0.")]
        public float TickIntervalSeconds = 1.0f;

        /// <summary>
        /// Multiplier applied to tick delta time. 1.0 = normal, 2.0 = 2x speed.
        /// Does NOT affect how often the tick fires — only the effective dt passed
        /// to subscribers. This means at 2x, income doubles but ticks don't stutter.
        /// </summary>
        [Tooltip("Time scale multiplier. 1 = normal, 2 = double speed.")]
        [Range(0f, 10f)]
        public float TimeScale = 1.0f;

        // ── State ─────────────────────────────────────────────────────────────────

        private float _accumulator;
        private bool  _paused;

        /// <summary>True when the tick engine is paused.</summary>
        public bool IsPaused => _paused;

        /// <summary>Total effective time elapsed (sum of all tick dt values).</summary>
        public float TotalEffectiveTime { get; private set; }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Pauses tick. OnTick will not fire until Resume() is called.</summary>
        public void Pause()
        {
            _paused = true;
        }

        /// <summary>Resumes tick after Pause().</summary>
        public void Resume()
        {
            _paused = false;
        }

        /// <summary>
        /// Resets the accumulator. Call when resuming from offline to avoid a
        /// burst of ticks from accumulated real time.
        /// </summary>
        public void ResetAccumulator()
        {
            _accumulator = 0f;
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Update()
        {
            if (_paused) return;
            if (TickIntervalSeconds <= 0f) return;

            _accumulator += Time.deltaTime;

            while (_accumulator >= TickIntervalSeconds)
            {
                _accumulator -= TickIntervalSeconds;
                float effectiveDt = TickIntervalSeconds * TimeScale;
                TotalEffectiveTime += effectiveDt;
                OnTick?.Invoke(effectiveDt);
            }
        }

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Manually fires one tick with the given delta time. For unit tests only.</summary>
        public static void FireTickForTesting(float dt) => OnTick?.Invoke(dt);

        /// <summary>Clears all OnTick subscribers. Call in test TearDown.</summary>
        public static void ClearSubscribersForTesting() => OnTick = null;
#endif
    }
}
