using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EndlessEngine.Economy;
using EndlessEngine.Generator;
using EndlessEngine.Upgrade;

namespace EndlessEngine.Samples.MinimalIdle
{
    /// <summary>
    /// HUD for the MinimalIdle sample scene.
    ///
    /// Wires automatically to EconomyService and GeneratorSystem events —
    /// displays gold balance, income rate, and buy buttons.
    ///
    /// Assign in Inspector: GoldLabel, IncomeLabel, BuyMineButton, BuyUpgradeButton,
    /// GeneratorCountLabel, EconomyService, GeneratorSystem, UpgradeTreeService.
    /// </summary>
    public class MinimalIdleUI : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text _goldLabel;
        [SerializeField] private TMP_Text _incomeLabel;
        [SerializeField] private TMP_Text _generatorCountLabel;
        [SerializeField] private TMP_Text _upgradeStatusLabel;

        [Header("Buttons")]
        [SerializeField] private Button   _buyMineButton;
        [SerializeField] private TMP_Text _buyMineButtonLabel;
        [SerializeField] private Button   _buyUpgradeButton;
        [SerializeField] private TMP_Text _buyUpgradeButtonLabel;

        [Header("Services")]
        [SerializeField] private EconomyService     _economyService;
        [SerializeField] private GeneratorSystem    _generatorSystem;
        [SerializeField] private UpgradeTreeService _upgradeTreeService;

        [Header("Config")]
        [SerializeField] private string _upgradeNodeId  = "mine_efficiency";
        [SerializeField] private string _generatorId    = "gold_mine";

        // Income tracking
        private double _goldLastFrame;
        private double _incomePerSecond;
        private float  _incomeTimer;

        private void OnEnable()
        {
            EconomyService.OnResourcesChanged   += HandleResourcesChanged;
            GeneratorSystem.OnGeneratorPurchased += HandleGeneratorPurchased;
        }

        private void OnDisable()
        {
            EconomyService.OnResourcesChanged   -= HandleResourcesChanged;
            GeneratorSystem.OnGeneratorPurchased -= HandleGeneratorPurchased;
        }

        private void Start()
        {
            // Auto-find services if not assigned in Inspector
            if (_economyService    == null) _economyService    = FindFirstObjectByType<EconomyService>();
            if (_generatorSystem   == null) _generatorSystem   = FindFirstObjectByType<GeneratorSystem>();
            if (_upgradeTreeService == null) _upgradeTreeService = FindFirstObjectByType<UpgradeTreeService>();

            _buyMineButton?.onClick.AddListener(OnBuyMine);
            _buyUpgradeButton?.onClick.AddListener(OnBuyUpgrade);
            _goldLastFrame = _economyService != null ? _economyService.CurrentResources : 0;
            RefreshAll();
        }

        private void Update()
        {
            // Rolling income estimate (1s window)
            _incomeTimer += Time.deltaTime;
            if (_incomeTimer >= 1f)
            {
                double current     = _economyService != null ? _economyService.CurrentResources : 0;
                _incomePerSecond   = current - _goldLastFrame;
                _goldLastFrame     = current;
                _incomeTimer       = 0f;

                if (_incomeLabel != null)
                    _incomeLabel.text = $"Income: {FormatGold(_incomePerSecond)}/s";
            }

            RefreshButtons();
        }

        private void HandleResourcesChanged(double current, double delta)
        {
            if (_goldLabel != null)
                _goldLabel.text = $"Gold: {FormatGold(current)}";
        }

        private void HandleGeneratorPurchased(string id)
        {
            RefreshGeneratorCount();
        }

        private void OnBuyMine()
        {
            if (_generatorSystem == null) return;
            _generatorSystem.TryPurchase(_generatorId);
            RefreshGeneratorCount();
        }

        private void OnBuyUpgrade()
        {
            if (_economyService == null) return;
            _economyService.TryPurchase(_upgradeNodeId);
            RefreshUpgradeStatus();
        }

        private void RefreshAll()
        {
            if (_economyService != null && _goldLabel != null)
                _goldLabel.text = $"Gold: {FormatGold(_economyService.CurrentResources)}";
            RefreshGeneratorCount();
            RefreshButtons();
            RefreshUpgradeStatus();
        }

        private void RefreshGeneratorCount()
        {
            if (_generatorSystem == null || _generatorCountLabel == null) return;
            int count = _generatorSystem.GetCount(_generatorId);
            double yield = _generatorSystem.CalculateTotalYield();
            _generatorCountLabel.text = $"Mines: {count}  ({FormatGold(yield)}/s)";
        }

        private void RefreshButtons()
        {
            // Mine button
            if (_buyMineButton != null && _generatorSystem != null && _economyService != null)
            {
                long   cost     = _generatorSystem.GetNextCost(_generatorId);
                bool   canBuy   = _economyService.CurrentResources >= cost;
                _buyMineButton.interactable = canBuy;
                if (_buyMineButtonLabel != null)
                    _buyMineButtonLabel.text = $"Buy Mine  {FormatGold(cost)}";
            }

            // Upgrade button
            if (_buyUpgradeButton != null && _upgradeTreeService != null && _economyService != null)
            {
                long   cost    = _upgradeTreeService.GetNodeCost(_upgradeNodeId);
                bool   avail   = _upgradeTreeService.IsNodeAvailable(_upgradeNodeId);
                bool   canBuy  = avail && _economyService.CurrentResources >= cost;
                _buyUpgradeButton.interactable = canBuy;
                if (_buyUpgradeButtonLabel != null)
                {
                    var node = _upgradeTreeService.GetNode(_upgradeNodeId);
                    if (node != null)
                    {
                        int rank = node.CurrentRank;
                        int max  = node.Config.MaxRank;
                        string rankStr = max > 0 && rank >= max ? "MAX" : $"Rank {rank + 1}{(max > 0 ? $"/{max}" : "")}";
                        _buyUpgradeButtonLabel.text = avail
                            ? $"Mine Efficiency ×2  {FormatGold(cost)}  [{rankStr}]"
                            : "Mine Efficiency (locked)";
                    }
                    else
                    {
                        _buyUpgradeButtonLabel.text = "Mine Efficiency";
                    }
                }
            }
        }

        private void RefreshUpgradeStatus()
        {
            if (_upgradeStatusLabel == null || _upgradeTreeService == null) return;
            var node = _upgradeTreeService.GetNode(_upgradeNodeId);
            if (node == null) { _upgradeStatusLabel.text = ""; return; }
            int rank = node.CurrentRank;
            int max  = node.Config.MaxRank;
            int bonus = (int)System.Math.Pow(2, rank);
            _upgradeStatusLabel.text = max > 0 && rank >= max
                ? $"Mine Efficiency: MAX (x{bonus} bonus)"
                : $"Mine Efficiency: Rank {rank}{(max > 0 ? $"/{max}" : "")}  (x{bonus} bonus)";
        }

        private static string FormatGold(double v)
        {
            if (v >= 1e15) return $"{v / 1e15:0.##}Qa";
            if (v >= 1e12) return $"{v / 1e12:0.##}T";
            if (v >= 1e9)  return $"{v / 1e9:0.##}B";
            if (v >= 1e6)  return $"{v / 1e6:0.##}M";
            if (v >= 1_000) return $"{v / 1_000:0.##}K";
            return ((long)v).ToString();
        }
    }
}
