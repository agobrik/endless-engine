using System.Collections;
using UnityEngine;

namespace EndlessEngine.Flow
{
    /// <summary>
    /// Slow-motion helper that temporarily scales Unity's Time.timeScale and restores it.
    ///
    /// Designed to be driven by FeedbackService — do not call Time.timeScale directly
    /// in gameplay code; use SlowMo() instead so FeedbackService stays the single
    /// authority for juice effects.
    ///
    /// Only one SlowMo effect runs at a time; a second call cancels the first.
    /// FixedDeltaTime is scaled proportionally to keep physics stable.
    /// </summary>
    public class TimeScaleController : MonoBehaviour
    {
        private float     _originalTimeScale     = 1f;
        private float     _originalFixedDeltaTime = 0.02f;
        private Coroutine _activeCoroutine;

        /// <summary>True while a SlowMo effect is active.</summary>
        public bool IsSlowMoActive { get; private set; }

        /// <summary>
        /// Temporarily scales Time.timeScale to <paramref name="factor"/> for
        /// <paramref name="duration"/> real-time seconds, then restores it.
        ///
        /// <paramref name="factor"/> 0.1 = 10 % speed (dramatic slow-mo).
        /// <paramref name="factor"/> 1.0 = no effect (no-op).
        /// Duration is measured in unscaled time so the effect always lasts
        /// the expected wall-clock duration regardless of current time scale.
        /// </summary>
        public void SlowMo(float duration, float factor = 0.3f)
        {
            if (duration <= 0f || Mathf.Approximately(factor, 1f)) return;

            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(SlowMoRoutine(duration, Mathf.Clamp(factor, 0.01f, 1f)));
        }

        /// <summary>Cancels the active slow-mo and immediately restores time scale.</summary>
        public void CancelSlowMo()
        {
            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }
            RestoreTimeScale();
        }

        private IEnumerator SlowMoRoutine(float duration, float factor)
        {
            _originalTimeScale      = Time.timeScale;
            _originalFixedDeltaTime = Time.fixedDeltaTime;

            Time.timeScale      = _originalTimeScale * factor;
            Time.fixedDeltaTime = _originalFixedDeltaTime * factor;
            IsSlowMoActive      = true;

            yield return new WaitForSecondsRealtime(duration);

            RestoreTimeScale();
        }

        private void RestoreTimeScale()
        {
            Time.timeScale      = _originalTimeScale;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            IsSlowMoActive      = false;
            _activeCoroutine    = null;
        }

        private void OnDestroy()
        {
            // Ensure time scale is restored if the GameObject is destroyed mid-effect.
            if (IsSlowMoActive)
                RestoreTimeScale();
        }
    }
}
