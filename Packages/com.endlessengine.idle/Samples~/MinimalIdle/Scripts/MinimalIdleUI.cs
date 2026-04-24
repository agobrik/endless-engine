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
        [SerializeField] private long              _upgradeCost   = 50;

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

        private void HandleResourcesChanged(long newAmount)
        {
            if (_goldLabel != null)
                _goldLabel.text = $"Gold: {FormatGold(newAmount)}";
            RefreshBuyButton();
        }

        private void OnBuyUpgrade()
        {
            if (_upgradeTreeService == null || _economyService == null) return;
            if (_economyService.CurrentResources < _upgradeCost)        return;

            _economyService.DeductResources(_upgradeCost);
            // Unlock the upgrade node — triggers stat recalculation in GeneratorSystem
            _upgradeTreeService.UnlockNode(_upgradeNodeId);
            RefreshBuyButton();
        }

        private void RefreshUI()
        {
            if (_economyService != null)
                HandleResourcesChanged(_economyService.CurrentResources);
        }

        private void RefreshBuyButton()
        {
            if (_buyButton == null) return;
            bool canAfford = _economyService != null && _economyService.CurrentResources >= _upgradeCost;
            _buyButton.interactable = canAfford;
            if (_buyButtonLabel != null)
                _buyButtonLabel.text = $"Buy Efficiency ×2 ({FormatGold(_upgradeCost)} gold)";
        }

        private static string FormatGold(long value)
        {
            if (value >= 1_000_000_000) return $"{value / 1_000_000_000f:0.#}B";
            if (value >= 1_000_000)     return $"{value / 1_000_000f:0.#}M";
            if (value >= 1_000)         return $"{value / 1_000f:0.#}K";
            return value.ToString();
        }
    }
}
