using UnityEngine;
using UnityEngine.UI;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Displays the player's current resource balance (gold) from EconomyService.
    /// Auto-updates on EconomyService.OnResourcesChanged.
    ///
    /// Usage:
    ///   Attach to a GameObject with a Text component.
    ///   Call resourceDisplay.Initialize(economyService) from Bootstrap.
    ///
    /// Optional income-per-second display (set ShowIncomePerSecond = true).
    /// </summary>
    [AddComponentMenu("Endless Engine/UI/Resource Display")]
    public class ResourceDisplay : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField] private Text _balanceLabel;
        [SerializeField] private Text _incomeLabel;

        [Tooltip("Show gold/sec beneath the balance.")]
        [SerializeField] private bool _showIncomePerSecond = false;

        [Tooltip("Format for balance label. {0} = formatted gold value.")]
        [SerializeField] private string _balanceFormat = "{0}";

        [Tooltip("Format for income label. {0} = formatted gold/sec.")]
        [SerializeField] private string _incomeFormat = "+{0}/s";

        // ── State ─────────────────────────────────────────────────────────────────

        private EconomyService _economy;
        private double         _lastBalance;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        public void Initialize(EconomyService economy)
        {
            _economy = economy;
            Refresh(economy?.CurrentResources ?? 0.0, 0.0);
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()  => EconomyService.OnResourcesChanged += HandleResourcesChanged;
        private void OnDisable() => EconomyService.OnResourcesChanged -= HandleResourcesChanged;

        // ── Handler ───────────────────────────────────────────────────────────────

        private void HandleResourcesChanged(double current, double delta)
            => Refresh(current, delta);

        private void Refresh(double current, double delta)
        {
            _lastBalance = current;

            if (_balanceLabel != null)
                _balanceLabel.text = string.Format(_balanceFormat, FormatGold(current));

            if (_incomeLabel != null && _showIncomePerSecond)
                _incomeLabel.text = string.Format(_incomeFormat, FormatGold(delta > 0 ? delta : 0));
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string FormatGold(double v)
        {
            if (v < 0) return "0";
            if (v >= 1e12) return $"{v / 1e12:F2}T";
            if (v >= 1e9)  return $"{v / 1e9:F2}B";
            if (v >= 1e6)  return $"{v / 1e6:F2}M";
            if (v >= 1e3)  return $"{v / 1e3:F1}K";
            return $"{v:F0}";
        }
    }
}
