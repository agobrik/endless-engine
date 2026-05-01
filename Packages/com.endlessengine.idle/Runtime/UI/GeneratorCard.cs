using UnityEngine;
using UnityEngine.UI;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Generator;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Self-contained HUD card for a single generator type.
    ///
    /// Displays: generator name, owned count, yield/s, and buy cost.
    /// Buy buttons: ×1, ×10, Max — each calls TryPurchaseBulk on GeneratorSystem.
    ///
    /// Usage:
    ///   Attach to a panel GameObject.
    ///   Call Initialize(generatorId, generatorSystem) from Bootstrap or
    ///   the parent GeneratorScreenController.
    ///
    /// Reacts to:
    ///   - GeneratorSystem.OnGeneratorPurchased (updates count and cost display)
    ///   - EconomyService.OnResourcesChanged    (updates button interactability)
    /// </summary>
    [AddComponentMenu("Endless Engine/UI/Generator Card")]
    public class GeneratorCard : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Labels")]
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Text _countLabel;
        [SerializeField] private Text _yieldLabel;

        [Header("Buy ×1")]
        [SerializeField] private Button _buy1Button;
        [SerializeField] private Text   _buy1CostLabel;

        [Header("Buy ×10")]
        [SerializeField] private Button _buy10Button;
        [SerializeField] private Text   _buy10CostLabel;

        [Header("Buy Max")]
        [SerializeField] private Button _buyMaxButton;
        [SerializeField] private Text   _buyMaxCostLabel;

        [Tooltip("Format for yield label. {0} = yield/s value.")]
        [SerializeField] private string _yieldFormat = "+{0}/s";

        [Tooltip("Format for count label. {0} = owned count.")]
        [SerializeField] private string _countFormat = "×{0}";

        // ── State ─────────────────────────────────────────────────────────────────

        private string          _generatorId;
        private GeneratorSystem _generatorSystem;
        private GeneratorConfigSO _config;
        private bool            _initialized;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        public void Initialize(string generatorId, GeneratorSystem generatorSystem)
        {
            _generatorId     = generatorId;
            _generatorSystem = generatorSystem;
            _initialized     = true;

            // Find config for static display (name)
            if (generatorSystem != null)
            {
                var configs = generatorSystem.Configs;
                if (configs != null)
                    foreach (var cfg in configs)
                        if (cfg != null && cfg.GeneratorId == generatorId)
                        { _config = cfg; break; }
            }

            if (_nameLabel != null && _config != null)
                _nameLabel.text = _config.DisplayName;

            WireButtons();
            Refresh();
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            GeneratorSystem.OnGeneratorPurchased += HandleGeneratorPurchased;
            EconomyService.OnResourcesChanged    += HandleResourcesChanged;
        }

        private void OnDisable()
        {
            GeneratorSystem.OnGeneratorPurchased -= HandleGeneratorPurchased;
            EconomyService.OnResourcesChanged    -= HandleResourcesChanged;
        }

        // ── Handlers ─────────────────────────────────────────────────────────────

        private void HandleGeneratorPurchased(string id) => Refresh();

        private void HandleResourcesChanged(double _, double __) => Refresh();

        // ── Refresh ───────────────────────────────────────────────────────────────

        private void Refresh()
        {
            if (!_initialized || _generatorSystem == null) return;

            double balance   = EconomyService.CurrentResourcesStatic;
            int    count     = _generatorSystem.GetCount(_generatorId);
            double cost1     = _generatorSystem.GetNextCostBig(_generatorId).ToDouble();
            double cost10    = _generatorSystem.GetBulkCostDisplay(_generatorId, BulkPurchaseMode.Ten);
            double costMax   = _generatorSystem.GetBulkCostDisplay(_generatorId, BulkPurchaseMode.Max);
            float  yield     = _config != null ? count * _config.BaseYieldPerSecond : 0f;

            if (_countLabel != null)
                _countLabel.text = string.Format(_countFormat, count);

            if (_yieldLabel != null)
                _yieldLabel.text = count > 0
                    ? string.Format(_yieldFormat, FormatGold(yield))
                    : string.Format(_yieldFormat, "0");

            SetBuyButton(_buy1Button,   _buy1CostLabel,   cost1,   balance >= cost1);
            SetBuyButton(_buy10Button,  _buy10CostLabel,  cost10,  balance >= cost10 && cost10 > 0);
            SetBuyButton(_buyMaxButton, _buyMaxCostLabel, costMax, balance >= costMax && costMax > 0);
        }

        private void SetBuyButton(Button btn, Text costLabel, double cost, bool canAfford)
        {
            if (btn == null) return;
            btn.interactable = canAfford;
            if (costLabel != null)
                costLabel.text = FormatGold(cost);
        }

        // ── Button wiring ─────────────────────────────────────────────────────────

        private void WireButtons()
        {
            if (_buy1Button   != null) _buy1Button.onClick.AddListener(Buy1);
            if (_buy10Button  != null) _buy10Button.onClick.AddListener(Buy10);
            if (_buyMaxButton != null) _buyMaxButton.onClick.AddListener(BuyMax);
        }

        private void Buy1()
        {
            if (_initialized && _generatorSystem != null)
                _generatorSystem.TryPurchase(_generatorId);
        }

        private void Buy10()
        {
            if (_initialized && _generatorSystem != null)
                _generatorSystem.TryPurchaseBulk(_generatorId, BulkPurchaseMode.Ten);
        }

        private void BuyMax()
        {
            if (_initialized && _generatorSystem != null)
                _generatorSystem.TryPurchaseBulk(_generatorId, BulkPurchaseMode.Max);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string FormatGold(double v)
        {
            if (v <= 0)    return "0";
            if (v >= 1e12) return $"{v / 1e12:F2}T";
            if (v >= 1e9)  return $"{v / 1e9:F2}B";
            if (v >= 1e6)  return $"{v / 1e6:F2}M";
            if (v >= 1e3)  return $"{v / 1e3:F1}K";
            return $"{v:F0}";
        }
    }
}
