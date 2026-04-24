using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;
using EndlessEngine.Prestige;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Controls the multi-layer ascension/prestige overlay.
    /// Shows a tab for each layer defined in AscensionDatabaseSO.
    /// Displays preview (current cascade multiplier → new), currency reward, reset scope.
    ///
    /// Call Show(layerIndex) from HUD prestige button or HUDController.
    /// Attach to a UIDocument whose Source Asset is AscensionOverlay.uxml.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class AscensionOverlayController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private AscensionDatabaseSO  _database;
        [SerializeField] private AscensionStateManager _ascensionManager;

        [Tooltip("Current wave number — used to evaluate CanTrigger gate.")]
        private int _currentWave;

        // ── UI Elements ───────────────────────────────────────────────────────────

        private VisualElement _root;
        private Label         _title;
        private VisualElement _layerTabs;
        private Label         _layerCountLabel;
        private Label         _currentMultLabel;
        private Label         _newMultLabel;
        private Label         _currencyRewardLabel;
        private Label         _gateStatusLabel;
        private Label         _resetScopeLabel;
        private Button        _confirmButton;
        private Button        _cancelButton;

        private int _selectedLayer = 0;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var docRoot = doc.rootVisualElement;

            _root               = docRoot.Q<VisualElement>("ascension-overlay-root");
            _title              = docRoot.Q<Label>("ascension-title");
            _layerTabs          = docRoot.Q<VisualElement>("layer-tabs");
            _layerCountLabel    = docRoot.Q<Label>("layer-count-label");
            _currentMultLabel   = docRoot.Q<Label>("current-mult-label");
            _newMultLabel       = docRoot.Q<Label>("new-mult-label");
            _currencyRewardLabel = docRoot.Q<Label>("currency-reward-label");
            _gateStatusLabel    = docRoot.Q<Label>("gate-status-label");
            _resetScopeLabel    = docRoot.Q<Label>("reset-scope-label");
            _confirmButton      = docRoot.Q<Button>("confirm-button");
            _cancelButton       = docRoot.Q<Button>("cancel-button");

            _confirmButton?.RegisterCallback<ClickEvent>(_ => OnConfirmClicked());
            _cancelButton?.RegisterCallback<ClickEvent>(_ => OnCancelClicked());

            AscensionStateManager.OnAscensionComplete += OnAscensionComplete;
        }

        private void OnDisable()
        {
            AscensionStateManager.OnAscensionComplete -= OnAscensionComplete;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Opens the overlay, pre-selecting the given layer tab.</summary>
        public void Show(int layerIndex = 0, int currentWave = 0)
        {
            _currentWave = currentWave;
            BuildLayerTabs();
            SelectLayer(layerIndex);
            _root.style.display = DisplayStyle.Flex;
        }

        /// <summary>Hides the overlay.</summary>
        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
        }

        // ── Layer Tabs ────────────────────────────────────────────────────────────

        private void BuildLayerTabs()
        {
            _layerTabs.Clear();
            if (_database == null) return;

            for (int i = 0; i < _database.LayerCount; i++)
            {
                var cfg = _database.GetLayer(i);
                if (cfg == null) continue;

                int layerIndex = i;
                var tab = new Button(() => SelectLayer(layerIndex));
                tab.text = cfg.DisplayName;
                tab.AddToClassList("layer-tab");

                bool canTrigger = _ascensionManager != null
                    && _ascensionManager.CanTrigger(layerIndex, _currentWave);
                if (!canTrigger) tab.AddToClassList("locked");

                _layerTabs.Add(tab);
            }
        }

        private void SelectLayer(int layerIndex)
        {
            _selectedLayer = layerIndex;

            // Update tab active state
            var tabs = _layerTabs.Children();
            int idx = 0;
            foreach (var tab in tabs)
            {
                tab.RemoveFromClassList("active");
                if (idx == layerIndex) tab.AddToClassList("active");
                idx++;
            }

            RefreshPreview();
        }

        // ── Preview ───────────────────────────────────────────────────────────────

        private void RefreshPreview()
        {
            if (_database == null || _ascensionManager == null) return;

            var cfg = _database.GetLayer(_selectedLayer);
            if (cfg == null) return;

            bool canTrigger = _ascensionManager.CanTrigger(_selectedLayer, _currentWave);

            // Title
            if (_title != null) _title.text = cfg.ActionVerb;

            // Count
            int count = _selectedLayer == 0
                ? _ascensionManager.GetLayer0Count()
                : _ascensionManager.GetCount(_selectedLayer);
            if (_layerCountLabel != null)
                _layerCountLabel.text = $"{cfg.DisplayName} #{count}";

            // Multipliers
            float currentCascade = _ascensionManager.GetCascadeMultiplier();
            float nextLayerMult  = cfg.GetPermanentMultiplier(count + 1);
            float newCascade     = currentCascade * nextLayerMult;

            if (_currentMultLabel != null)
                _currentMultLabel.text = $"Current cascade: {currentCascade:F2}×";
            if (_newMultLabel != null)
                _newMultLabel.text = $"After {cfg.ActionVerb}: {newCascade:F2}×";

            // Currency reward
            if (_currencyRewardLabel != null)
            {
                if (!string.IsNullOrEmpty(cfg.RewardCurrencyId) && cfg.BaseCurrencyReward > 0)
                {
                    double reward = cfg.GetCurrencyReward(count);
                    _currencyRewardLabel.text = $"+{reward:N0} {cfg.RewardCurrencyId}";
                    _currencyRewardLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _currencyRewardLabel.style.display = DisplayStyle.None;
                }
            }

            // Gate status
            if (_gateStatusLabel != null)
            {
                if (!canTrigger)
                {
                    string reason = BuildGateReason(cfg, count);
                    _gateStatusLabel.text = reason;
                    _gateStatusLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _gateStatusLabel.style.display = DisplayStyle.None;
                }
            }

            // Reset scope
            if (_resetScopeLabel != null)
                _resetScopeLabel.text = "Resets: " + DescribeResetScope(cfg);

            // Confirm button
            if (_confirmButton != null)
                _confirmButton.SetEnabled(canTrigger);
        }

        private string BuildGateReason(PrestigeLayerConfigSO cfg, int currentCount)
        {
            if (cfg.MinWaveRequired > 0 && _currentWave < cfg.MinWaveRequired)
                return $"Requires wave {cfg.MinWaveRequired} (current: {_currentWave})";

            if (cfg.RequiredPreviousLayerCount > 0)
            {
                int prevCount = _selectedLayer == 1
                    ? _ascensionManager.GetLayer0Count()
                    : _ascensionManager.GetCount(_selectedLayer - 1);
                if (prevCount < cfg.RequiredPreviousLayerCount)
                    return $"Requires {cfg.RequiredPreviousLayerCount}× previous layer (have {prevCount})";
            }

            if (cfg.MaxCount > 0 && currentCount >= cfg.MaxCount)
                return $"Max {cfg.MaxCount} times reached";

            return "Not available";
        }

        private string DescribeResetScope(PrestigeLayerConfigSO cfg)
        {
            switch (cfg.ResetScope)
            {
                case AscensionResetScope.Standard: return "Gold, Upgrades, Wave";
                case AscensionResetScope.Deep:
                    var parts = new System.Collections.Generic.List<string> { "Gold", "Upgrades", "Wave" };
                    if (cfg.ResetGenerators)         parts.Add("Generators");
                    if (cfg.ResetSecondaryCurrencies) parts.Add("Currencies");
                    return string.Join(", ", parts);
                case AscensionResetScope.Full: return "Everything";
                default: return "Standard";
            }
        }

        // ── Confirm / Cancel ──────────────────────────────────────────────────────

        private void OnConfirmClicked()
        {
            _ascensionManager?.TryTrigger(_selectedLayer, _currentWave);
            Hide();
        }

        private void OnCancelClicked() => Hide();

        private void OnAscensionComplete(int layerIndex, int newCount, float cascade)
        {
            // If still open for some reason, refresh
            if (_root.style.display == DisplayStyle.Flex)
                RefreshPreview();
        }
    }
}
