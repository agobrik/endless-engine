using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Building;
using EndlessEngine.Config;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Controls the building screen: building list, place/upgrade/remove actions,
    /// instance count, and production display.
    ///
    /// Attach to a UIDocument whose Source Asset is BuildingGridScreen.uxml.
    /// Wire BuildingService and BuildingConfigs in Inspector.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BuildingScreenController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private BuildingService    _buildingService;
        [SerializeField] private BuildingConfigSO[] _allBuildings;

        // ── UI Elements ──────────────────────────────────────────────────────────

        private VisualElement _root;
        private VisualElement _list;
        private Label         _countLabel;
        private Button        _closeButton;
        private Label         _errorLabel;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            var doc     = GetComponent<UIDocument>();
            var docRoot = doc.rootVisualElement;

            _root        = docRoot.Q<VisualElement>("building-root");
            _list        = docRoot.Q<VisualElement>("building-list");
            _countLabel  = docRoot.Q<Label>("building-count-label");
            _closeButton = docRoot.Q<Button>("close-button");
            _errorLabel  = docRoot.Q<Label>("building-error-label");

            _closeButton?.RegisterCallback<ClickEvent>(_ => Hide());

            BuildingService.OnBuildingPlaced   += OnBuildingChanged;
            BuildingService.OnBuildingUpgraded += OnBuildingChanged;
            BuildingService.OnBuildingRemoved  += OnBuildingRemoved;
            BuildingService.OnPlaceFailed      += OnPlaceFailed;
        }

        private void OnDisable()
        {
            BuildingService.OnBuildingPlaced   -= OnBuildingChanged;
            BuildingService.OnBuildingUpgraded -= OnBuildingChanged;
            BuildingService.OnBuildingRemoved  -= OnBuildingRemoved;
            BuildingService.OnPlaceFailed      -= OnPlaceFailed;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        public void Show()
        {
            RefreshList();
            _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
        }

        // ── Event handlers ───────────────────────────────────────────────────────

        private void OnBuildingChanged(BuildingInstance _)
        {
            if (_root.style.display == DisplayStyle.Flex) RefreshList();
        }

        private void OnBuildingRemoved(string _)
        {
            if (_root.style.display == DisplayStyle.Flex) RefreshList();
        }

        private void OnPlaceFailed(string buildingId, string reason)
        {
            if (_errorLabel == null) return;
            _errorLabel.text = reason switch
            {
                "ConfigNotFound"        => "Unknown building type.",
                "InsufficientFunds"     => "Not enough resources to place.",
                "MaxInstancesReached"   => "Maximum instances already placed.",
                _                       => reason
            };
            _errorLabel.style.display = DisplayStyle.Flex;
            CancelInvoke(nameof(HideError));
            Invoke(nameof(HideError), 3f);
        }

        private void HideError()
        {
            if (_errorLabel != null)
                _errorLabel.style.display = DisplayStyle.None;
        }

        // ── List builder ─────────────────────────────────────────────────────────

        private void RefreshList()
        {
            if (_list == null) return;
            _list.Clear();

            if (_allBuildings == null || _buildingService == null) return;

            int totalPlaced = 0;
            foreach (var cfg in _allBuildings)
            {
                if (cfg == null) continue;
                int count = _buildingService.GetInstanceCount(cfg.BuildingId);
                totalPlaced += count;
                _list.Add(BuildRow(cfg, count));
            }

            if (_countLabel != null)
                _countLabel.text = totalPlaced + " placed";
        }

        private VisualElement BuildRow(BuildingConfigSO cfg, int instanceCount)
        {
            var row = new VisualElement();
            row.AddToClassList("building-row");

            var nameLabel = new Label(cfg.DisplayName);
            nameLabel.AddToClassList("building-row-name");
            row.Add(nameLabel);

            var prodLabel = new Label($"+{cfg.ProductionPerTick:N0}/tick");
            prodLabel.AddToClassList("building-row-production");
            row.Add(prodLabel);

            string maxStr = cfg.MaxInstances > 0 ? cfg.MaxInstances.ToString() : "∞";
            var countLabel = new Label($"{instanceCount}/{maxStr}");
            countLabel.AddToClassList("building-row-count");
            row.Add(countLabel);

            // Place button
            bool canPlace = _buildingService.CanPlace(cfg.BuildingId);
            var placeBtn  = new Button(() => OnPlace(cfg.BuildingId));
            placeBtn.text = $"Place ({cfg.PlacementCost:N0})";
            placeBtn.AddToClassList("building-action-btn");
            placeBtn.AddToClassList("building-place-btn");
            placeBtn.SetEnabled(canPlace);
            row.Add(placeBtn);

            // Upgrade + Remove buttons (only if at least one instance exists)
            if (instanceCount > 0)
            {
                var instances = _buildingService.GetAllInstances();
                BuildingInstance firstOfType = null;
                foreach (var kv in instances)
                    if (kv.Value.BuildingId == cfg.BuildingId) { firstOfType = kv.Value; break; }

                if (firstOfType != null)
                {
                    int nextTierIndex = firstOfType.UpgradeTier;
                    bool canUpgrade   = nextTierIndex < cfg.UpgradeTiers.Length;
                    var upgradeBtn    = new Button(() => OnUpgrade(firstOfType.InstanceId));
                    upgradeBtn.text   = canUpgrade
                        ? $"Upgrade ({cfg.UpgradeTiers[nextTierIndex].UpgradeCost:N0})"
                        : "Max";
                    upgradeBtn.AddToClassList("building-action-btn");
                    upgradeBtn.AddToClassList("building-upgrade-btn");
                    upgradeBtn.SetEnabled(canUpgrade);
                    row.Add(upgradeBtn);

                    var removeBtn = new Button(() => OnRemove(firstOfType.InstanceId));
                    removeBtn.text = "Remove";
                    removeBtn.AddToClassList("building-action-btn");
                    removeBtn.AddToClassList("building-remove-btn");
                    row.Add(removeBtn);
                }
            }

            return row;
        }

        // ── Actions ──────────────────────────────────────────────────────────────

        private void OnPlace(string buildingId)
        {
            _buildingService?.TryPlace(buildingId, 0, 0);
        }

        private void OnUpgrade(string instanceId)
        {
            _buildingService?.TryUpgrade(instanceId);
        }

        private void OnRemove(string instanceId)
        {
            _buildingService?.Remove(instanceId);
        }
    }
}
