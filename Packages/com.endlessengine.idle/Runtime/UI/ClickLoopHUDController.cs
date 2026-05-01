using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EndlessEngine.ClickLoop;

namespace EndlessEngine.UI
{
    /// <summary>
    /// HUD overlay for the Click active loop.
    ///
    /// Displays:
    ///   - Combo multiplier text (×2.5)
    ///   - Combo fill bar
    ///   - Crit flash (brief colour burst on crit)
    ///   - Target HP bar (tracks the active target's HP)
    ///
    /// All fields optional — null-guarded.
    /// Call Initialize() from bootstrapper after ClickLoopService.Initialize().
    /// </summary>
    public class ClickLoopHUDController : MonoBehaviour
    {
        [Header("Combo")]
        [SerializeField] private TextMeshProUGUI _comboText;
        [SerializeField] private Image           _comboFillBar;
        [SerializeField] private GameObject      _comboPanelRoot;

        [Header("Crit flash")]
        [SerializeField] private Image  _critFlashImage;
        [SerializeField] private float  _critFlashDuration = 0.15f;

        [Header("Target HP bar")]
        [SerializeField] private Image           _targetHPBar;
        [SerializeField] private TextMeshProUGUI _targetHPText;

        [Header("Yield pop")]
        [SerializeField] private TextMeshProUGUI _yieldPopText;
        [SerializeField] private float           _yieldPopDuration = 0.5f;

        // ── Dependencies ──────────────────────────────────────────────────────────

        private ClickLoopService  _service;
        private ClickLoopConfigSO _config;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private float        _critFlashTimer;
        private float        _yieldPopTimer;
        private IClickTarget _trackedTarget;

        // ── Init ──────────────────────────────────────────────────────────────────

        public void Initialize(ClickLoopService service, ClickLoopConfigSO config)
        {
            _service = service;
            _config  = config;
        }

        private void OnEnable()
        {
            if (_service == null) return;
            _service.OnComboChanged    += HandleComboChanged;
            _service.OnYieldAwarded    += HandleYieldAwarded;
            _service.OnCrit            += HandleCrit;
            _service.OnTargetClicked   += HandleTargetClicked;
            _service.OnTargetDestroyed += HandleTargetDestroyed;
        }

        private void OnDisable()
        {
            if (_service == null) return;
            _service.OnComboChanged    -= HandleComboChanged;
            _service.OnYieldAwarded    -= HandleYieldAwarded;
            _service.OnCrit            -= HandleCrit;
            _service.OnTargetClicked   -= HandleTargetClicked;
            _service.OnTargetDestroyed -= HandleTargetDestroyed;
        }

        // ── Update ────────────────────────────────────────────────────────────────

        private void Update()
        {
            TickCritFlash();
            TickYieldPop();
            UpdateTargetHPBar();
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void HandleComboChanged(float mul)
        {
            bool active = mul > 1.001f;
            if (_comboPanelRoot != null) _comboPanelRoot.SetActive(active);
            if (_comboText != null)     _comboText.text = $"×{mul:F1}";

            if (_comboFillBar != null && _config != null)
                _comboFillBar.fillAmount = Mathf.InverseLerp(1f, _config.MaxComboMultiplier, mul);
        }

        private void HandleYieldAwarded(float yield)
        {
            if (_yieldPopText == null) return;
            _yieldPopText.text    = $"+{(long)Mathf.Max(1f, yield)}";
            _yieldPopText.enabled = true;
            _yieldPopTimer        = _yieldPopDuration;
        }

        private void HandleCrit(float critMul)
        {
            if (_critFlashImage == null) return;
            _critFlashImage.enabled = true;
            _critFlashTimer         = _critFlashDuration;
        }

        private void HandleTargetClicked(IClickTarget target, float damage, float yield, bool wasCrit)
        {
            _trackedTarget = target.IsAlive ? target : null;
        }

        private void HandleTargetDestroyed(IClickTarget target)
        {
            if (_trackedTarget == target) _trackedTarget = null;
        }

        // ── Per-frame ─────────────────────────────────────────────────────────────

        private void TickCritFlash()
        {
            if (_critFlashImage == null || !_critFlashImage.enabled) return;
            _critFlashTimer -= Time.deltaTime;
            float a = Mathf.Clamp01(_critFlashTimer / _critFlashDuration);
            var c = _critFlashImage.color;
            c.a = a;
            _critFlashImage.color = c;
            if (_critFlashTimer <= 0f) _critFlashImage.enabled = false;
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

        private void UpdateTargetHPBar()
        {
            if (_targetHPBar == null || _trackedTarget == null) return;
            float fill = _trackedTarget.Config != null
                ? _trackedTarget.CurrentHP / _trackedTarget.Config.MaxHP
                : 0f;
            _targetHPBar.fillAmount = Mathf.Clamp01(fill);

            if (_targetHPText != null)
                _targetHPText.text = $"{Mathf.CeilToInt(_trackedTarget.CurrentHP)}" +
                                     $"/{Mathf.CeilToInt(_trackedTarget.Config?.MaxHP ?? 0f)}";
        }
    }
}
