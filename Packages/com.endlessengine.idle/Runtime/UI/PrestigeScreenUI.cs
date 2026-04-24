using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Prestige;
using EndlessEngine.UI;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Prestige confirmation overlay.
    ///
    /// Opens on <see cref="PrestigeSystem.OnPrestigeScreenRequested"/>.
    /// Displays preview data from <see cref="PrestigeSystem.GetPrestigePreview()"/>.
    /// Confirm → <see cref="PrestigeSystem.ConfirmPrestige()"/>.
    /// Cancel  → <see cref="PrestigeSystem.CancelPrestige()"/>.
    /// Hides on <see cref="PrestigeSystem.OnPrestigeScreenDismissed"/>.
    ///
    /// GDD: design/gdd/prestige-system.md
    /// ADR: ADR-0013 (UI Toolkit), ADR-0010 (prestige crash safety)
    /// Sprint: S4-11
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PrestigeScreenUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Tooltip("PrestigeSystem instance in this scene — used for preview data and action routing.")]
        [SerializeField] private PrestigeSystem _prestigeSystem;

        // ── UI References ─────────────────────────────────────────────────────────

        private VisualElement _overlayRoot;
        private Label         _currentMultLabel;
        private Label         _newMultLabel;
        private Label         _offlineLabel;
        private Button        _confirmButton;
        private Button        _cancelButton;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            var doc  = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;

            _overlayRoot      = root.Q<VisualElement>("prestige-overlay-root");
            _currentMultLabel = root.Q<Label>("current-mult-label");
            _newMultLabel     = root.Q<Label>("new-mult-label");
            _offlineLabel     = root.Q<Label>("offline-label");
            _confirmButton    = root.Q<Button>("confirm-button");
            _cancelButton     = root.Q<Button>("cancel-button");

            _confirmButton?.RegisterCallback<ClickEvent>(_ => OnConfirmClicked());
            _cancelButton?.RegisterCallback<ClickEvent>(_ => OnCancelClicked());

            SetVisible(false);
        }

        private void OnEnable()
        {
            PrestigeSystem.OnPrestigeScreenRequested  += OnPrestigeScreenRequested;
            PrestigeSystem.OnPrestigeScreenDismissed  += OnPrestigeScreenDismissed;
        }

        private void OnDisable()
        {
            PrestigeSystem.OnPrestigeScreenRequested  -= OnPrestigeScreenRequested;
            PrestigeSystem.OnPrestigeScreenDismissed  -= OnPrestigeScreenDismissed;
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void OnPrestigeScreenRequested()
        {
            PopulatePreview();
            SetVisible(true);
        }

        private void OnPrestigeScreenDismissed(bool confirmed)
        {
            SetVisible(false);
        }

        private void OnConfirmClicked()
        {
            _prestigeSystem?.ConfirmPrestige();
        }

        private void OnCancelClicked()
        {
            _prestigeSystem?.CancelPrestige();
        }

        // ── Preview Data ──────────────────────────────────────────────────────────

        private void PopulatePreview()
        {
            if (_prestigeSystem == null) return;

            var preview = _prestigeSystem.GetPrestigePreview();

            if (_currentMultLabel != null)
                _currentMultLabel.text = $"Current Multiplier: {preview.CurrentMultiplier:F1}×";

            if (_newMultLabel != null)
                _newMultLabel.text = $"New Multiplier: {preview.NewMultiplier:F1}×";

            if (_offlineLabel != null)
                _offlineLabel.text = $"Projected offline (6h): +{GoldFormatter.Format(preview.ProjectedOfflineGain6h)}";
        }

        // ── Visibility ────────────────────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            if (_overlayRoot == null) return;

            if (visible)
                _overlayRoot.AddToClassList("visible");
            else
                _overlayRoot.RemoveFromClassList("visible");
        }
    }
}
