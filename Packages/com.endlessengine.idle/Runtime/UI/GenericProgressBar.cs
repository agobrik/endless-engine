using UnityEngine;
using UnityEngine.UI;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Reusable progress bar component.
    /// Drives a UI Image (Filled) or a RectTransform scale bar.
    ///
    /// Usage:
    ///   progressBar.SetProgress(current, max);        // absolute values
    ///   progressBar.SetNormalized(0.75f);             // 0–1 directly
    ///
    /// Supports animated fill via SmoothSpeed (0 = instant).
    /// </summary>
    [AddComponentMenu("Endless Engine/UI/Generic Progress Bar")]
    public class GenericProgressBar : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Tooltip("Filled Image — if set, drives fillAmount. Takes priority over FillRect.")]
        [SerializeField] private Image _fillImage;

        [Tooltip("RectTransform used as a scale bar (anchorMax.x driven). Used if FillImage is null.")]
        [SerializeField] private RectTransform _fillRect;

        [Tooltip("Optional label showing 'current / max' or percentage.")]
        [SerializeField] private Text _label;

        [Tooltip("Label format. Use {0} for current, {1} for max, {2} for percent (0–100).")]
        [SerializeField] private string _labelFormat = "{0} / {1}";

        [Tooltip("Lerp speed toward target fill. 0 = instant.")]
        [SerializeField] [Range(0f, 20f)] private float _smoothSpeed = 8f;

        // ── State ─────────────────────────────────────────────────────────────────

        private float _targetNormalized;
        private float _currentNormalized;
        private double _currentValue;
        private double _maxValue;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Sets the bar using absolute current/max values.</summary>
        public void SetProgress(double current, double max)
        {
            _currentValue = current;
            _maxValue     = max;
            _targetNormalized = max > 0 ? Mathf.Clamp01((float)(current / max)) : 0f;
            UpdateLabel();
            if (_smoothSpeed <= 0f) ApplyFill(_targetNormalized);
        }

        /// <summary>Sets the bar using a normalized 0–1 value directly.</summary>
        public void SetNormalized(float normalized)
        {
            _targetNormalized = Mathf.Clamp01(normalized);
            if (_smoothSpeed <= 0f) ApplyFill(_targetNormalized);
        }

        /// <summary>Immediately snaps to the target fill with no interpolation.</summary>
        public void SnapToTarget()
        {
            _currentNormalized = _targetNormalized;
            ApplyFill(_currentNormalized);
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Update()
        {
            if (_smoothSpeed <= 0f) return;
            if (Mathf.Approximately(_currentNormalized, _targetNormalized)) return;

            _currentNormalized = Mathf.Lerp(_currentNormalized, _targetNormalized,
                                             _smoothSpeed * Time.deltaTime);
            ApplyFill(_currentNormalized);
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private void ApplyFill(float t)
        {
            if (_fillImage != null)
            {
                _fillImage.fillAmount = t;
            }
            else if (_fillRect != null)
            {
                var anchor = _fillRect.anchorMax;
                anchor.x = t;
                _fillRect.anchorMax = anchor;
            }
        }

        private void UpdateLabel()
        {
            if (_label == null) return;
            _label.text = string.Format(_labelFormat,
                FormatValue(_currentValue),
                FormatValue(_maxValue),
                (int)(_targetNormalized * 100f));
        }

        private static string FormatValue(double v)
        {
            if (v >= 1e9)  return $"{v / 1e9:F1}B";
            if (v >= 1e6)  return $"{v / 1e6:F1}M";
            if (v >= 1e3)  return $"{v / 1e3:F1}K";
            return $"{v:F0}";
        }
    }
}
