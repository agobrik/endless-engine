using UnityEngine;
using UnityEngine.UI;
using EndlessEngine.Economy;
using EndlessEngine.Prestige;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Reactive prestige button component.
    ///
    /// Shows/hides and enables/disables itself based on PrestigeStateManager.CanPrestige.
    /// Displays the next permanent multiplier as a preview label.
    ///
    /// Usage:
    ///   Attach to a Button GameObject.
    ///   Call Initialize(prestigeStateManager) from Bootstrap.
    ///   Wire the Button's onClick to TryPrestige() (or let this component handle it).
    ///
    /// Reacts to:
    ///   - EconomyService.OnResourcesChanged (balance may affect CanPrestige)
    ///   - PrestigeStateManager.OnPrestigeComplete (updates multiplier preview)
    ///   - PrestigeStateManager.OnPrestigeStarted  (disables during ceremony)
    /// </summary>
    [AddComponentMenu("Endless Engine/UI/Prestige Button")]
    [RequireComponent(typeof(Button))]
    public class PrestigeButton : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Tooltip("Label that shows 'PRESTIGE' or similar call-to-action text.")]
        [SerializeField] private Text _buttonLabel;

        [Tooltip("Label that previews the next permanent multiplier, e.g. '× 2.25'.")]
        [SerializeField] private Text _multiplierPreviewLabel;

        [Tooltip("Format for multiplier preview. {0} = next multiplier value.")]
        [SerializeField] private string _multiplierFormat = "× {0:F2}";

        [Tooltip("Text shown on button when prestige is available.")]
        [SerializeField] private string _availableText = "PRESTIGE";

        [Tooltip("Text shown on button when prestige requirement is not met yet.")]
        [SerializeField] private string _lockedText = "NOT READY";

        // ── State ─────────────────────────────────────────────────────────────────

        private Button                _button;
        private PrestigeStateManager  _prestige;
        private bool                  _initialized;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        public void Initialize(PrestigeStateManager prestige)
        {
            _prestige    = prestige;
            _initialized = true;
            Refresh();
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        private void OnEnable()
        {
            EconomyService.OnResourcesChanged           += HandleResourcesChanged;
            PrestigeStateManager.OnPrestigeStarted      += HandlePrestigeStarted;
            PrestigeStateManager.OnPrestigeComplete     += HandlePrestigeComplete;
        }

        private void OnDisable()
        {
            EconomyService.OnResourcesChanged           -= HandleResourcesChanged;
            PrestigeStateManager.OnPrestigeStarted      -= HandlePrestigeStarted;
            PrestigeStateManager.OnPrestigeComplete     -= HandlePrestigeComplete;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Manually trigger prestige attempt. Safe to call from UI event.</summary>
        public void TryPrestige()
        {
            if (_prestige != null && _prestige.CanPrestige)
                _prestige.TryPrestige();
        }

        // ── Handlers ─────────────────────────────────────────────────────────────

        private void HandleResourcesChanged(double _, double __) => Refresh();

        private void HandlePrestigeStarted() => SetInteractable(false);

        private void HandlePrestigeComplete(int _, float __) => Refresh();

        private void OnClick() => TryPrestige();

        // ── Refresh ───────────────────────────────────────────────────────────────

        private void Refresh()
        {
            if (!_initialized || _prestige == null) return;

            bool canPrestige = _prestige.CanPrestige;
            SetInteractable(canPrestige);

            if (_buttonLabel != null)
                _buttonLabel.text = canPrestige ? _availableText : _lockedText;

            if (_multiplierPreviewLabel != null)
            {
                float nextMult = _prestige.GetPermanentMultiplier();
                _multiplierPreviewLabel.text = string.Format(_multiplierFormat, nextMult);
                _multiplierPreviewLabel.gameObject.SetActive(canPrestige);
            }
        }

        private void SetInteractable(bool interactable)
        {
            if (_button != null)
                _button.interactable = interactable;
        }
    }
}
