using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EndlessEngine.Harvest;

namespace EndlessEngine.UI
{
    /// <summary>
    /// HUD overlay for the Harvest active loop.
    ///
    /// Displays:
    ///   - Combo multiplier value (text)
    ///   - Combo fill bar (Image fillAmount, 0-1 mapped to 1× → MaxComboMultiplier)
    ///   - Cursor radius visual (circle Image that scales with EffectiveRadius)
    ///
    /// Wire up in the Inspector and call Initialize() from the bootstrapper.
    /// All fields are optional — the controller null-guards each one.
    /// </summary>
    public class HarvestHUDController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Combo")]
        [Tooltip("Text showing '×2.5' style combo multiplier. Leave null to skip.")]
        [SerializeField] private TextMeshProUGUI _comboText;

        [Tooltip("Fill image (0→1) showing combo progress. Leave null to skip.")]
        [SerializeField] private Image _comboFillBar;

        [Tooltip("Root object shown only when combo > 1. Leave null to skip.")]
        [SerializeField] private GameObject _comboPanelRoot;

        [Header("Yield feedback")]
        [Tooltip("Text briefly shown on yield award (e.g. '+12'). Leave null to skip.")]
        [SerializeField] private TextMeshProUGUI _yieldPopText;

        [Tooltip("Seconds the yield pop text remains visible before clearing.")]
        [SerializeField] private float _yieldPopDuration = 0.6f;

        [Header("Cursor radius indicator")]
        [Tooltip("RectTransform of a circle image centred on the cursor. Leave null to skip.")]
        [SerializeField] private RectTransform _cursorRadiusIndicator;

        [Tooltip("Pixels-per-world-unit for radius scaling. Match your camera setup.")]
        [SerializeField] private float _pxPerWorldUnit = 100f;

        // ── Dependencies ──────────────────────────────────────────────────────────

        private HarvestLoopService  _loopService;
        private HarvestCursor       _cursor;
        private HarvestAreaConfigSO _config;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private float _yieldPopTimer;

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Call from bootstrapper after HarvestLoopService.Initialize().
        /// </summary>
        public void Initialize(HarvestLoopService loopService, HarvestCursor cursor, HarvestAreaConfigSO config)
        {
            _loopService = loopService;
            _cursor      = cursor;
            _config      = config;
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_loopService == null) return;
            _loopService.OnComboChanged  += HandleComboChanged;
            _loopService.OnYieldAwarded  += HandleYieldAwarded;
        }

        private void OnDisable()
        {
            if (_loopService == null) return;
            _loopService.OnComboChanged  -= HandleComboChanged;
            _loopService.OnYieldAwarded  -= HandleYieldAwarded;
        }

        private void Update()
        {
            UpdateCursorIndicator();
            TickYieldPop();
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void HandleComboChanged(float multiplier)
        {
            bool active = multiplier > 1.001f;

            if (_comboPanelRoot != null)
                _comboPanelRoot.SetActive(active);

            if (_comboText != null)
                _comboText.text = $"×{multiplier:F1}";

            if (_comboFillBar != null && _config != null)
            {
                float max  = _config.MaxComboMultiplier;
                float fill = Mathf.InverseLerp(1f, max, multiplier);
                _comboFillBar.fillAmount = fill;
            }
        }

        private void HandleYieldAwarded(float yield)
        {
            if (_yieldPopText == null) return;
            long display = (long)Mathf.Max(1f, yield);
            _yieldPopText.text    = $"+{display}";
            _yieldPopText.enabled = true;
            _yieldPopTimer        = _yieldPopDuration;
        }

        // ── Per-frame updates ─────────────────────────────────────────────────────

        private void UpdateCursorIndicator()
        {
            if (_cursorRadiusIndicator == null || _cursor == null) return;

            // Position follows mouse in screen space
            Vector2 screenPos = Camera.main != null
                ? (Vector2)Camera.main.WorldToScreenPoint(_cursor.WorldPosition)
                : Vector2.zero;
            _cursorRadiusIndicator.position = screenPos;

            // Scale: diameter in pixels = radius × 2 × pxPerWorldUnit
            float diameter = _cursor.EffectiveRadius * 2f * _pxPerWorldUnit;
            _cursorRadiusIndicator.sizeDelta = new Vector2(diameter, diameter);
        }

        private void TickYieldPop()
        {
            if (_yieldPopText == null || !_yieldPopText.enabled) return;

            _yieldPopTimer -= Time.deltaTime;
            if (_yieldPopTimer <= 0f)
            {
                _yieldPopText.enabled = false;
                _yieldPopText.text    = string.Empty;
            }
        }
    }
}
