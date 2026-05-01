using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EndlessEngine.Economy;
using EndlessEngine.Upgrade;

namespace EndlessEngine.Samples.MinimalIdle
{
    /// <summary>
    /// Minimal HUD for the MinimalIdle sample.
    /// Displays current gold and a single upgrade button.
    ///
    /// Assign in Inspector: GoldLabel (TMP), BuyButton (Button),
    ///                       EconomyService, UpgradeTreeService,
    ///                       UpgradeNodeId (the efficiency node id).
    /// </summary>
    public class MinimalIdleUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text          _goldLabel;
        [SerializeField] private Button            _buyButton;
        [SerializeField] private TMP_Text          _buyButtonLabel;
        [SerializeField] private EconomyService    _economyService;
        [SerializeField] private UpgradeTreeService _upgradeTreeService;

        [Header("Config")]
        [SerializeField] private string            _upgradeNodeId = "mine_efficiency";
        [SerializeField] private long              _upgradeCost   = 50; // fallback if UpgradeTreeService is null

        private void OnEnable()
        {
            EconomyService.OnResourcesChanged += HandleResourcesChanged;
        }

        private void OnDisable()
        {
            EconomyService.OnResourcesChanged -= HandleResourcesChanged;
        }

        private void Start()
        {
            _buyButton?.onClick.AddListener(OnBuyUpgrade);
            RefreshUI();
        }

        private void HandleResourcesChanged(double current, double previous)
        {
            if (_goldLabel != null)
                _goldLabel.text = $"Gold: {FormatGold(current)}";
            RefreshBuyButton();
        }

        private void OnBuyUpgrade()
        {
            if (_economyService == null) return;
            // TryPurchase handles cost check, deduction, and node rank-up internally
            _economyService.TryPurchase(_upgradeNodeId);
            RefreshBuyButton();
        }

        private void RefreshUI()
        {
            if (_economyService != null)
                HandleResourcesChanged(_economyService.CurrentResources, 0);
        }

        private void RefreshBuyButton()
        {
            if (_buyButton == null) return;
            double cost      = _upgradeTreeService != null
                ? _upgradeTreeService.GetNodeCost(_upgradeNodeId)
                : _upgradeCost;
            bool canAfford   = _economyService != null && _economyService.CurrentResources >= cost;
            bool nodeAvail   = _upgradeTreeService == null || _upgradeTreeService.IsNodeAvailable(_upgradeNodeId);
            _buyButton.interactable = canAfford && nodeAvail;
            if (_buyButtonLabel != null)
                _buyButtonLabel.text = $"Buy Efficiency ×2 ({FormatGold(cost)} gold)";
        }

        private static string FormatGold(double value)
        {
            if (value >= 1_000_000_000) return $"{value / 1_000_000_000:0.#}B";
            if (value >= 1_000_000)     return $"{value / 1_000_000:0.#}M";
            if (value >= 1_000)         return $"{value / 1_000:0.#}K";
            return ((long)value).ToString();
        }
    }
}
