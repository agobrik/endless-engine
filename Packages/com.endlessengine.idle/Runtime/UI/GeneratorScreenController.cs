using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Generator;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Controls the Generator Screen overlay.
    /// Shows all generator types, their counts, yield, and buy buttons.
    /// Opened from MainMenuController's GENERATORS button.
    /// Closed by the ✕ button or by calling Hide().
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GeneratorScreenController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField] private GeneratorSystem _generatorSystem;

        // ── UI References ─────────────────────────────────────────────────────────

        private VisualElement _root;
        private Label         _totalYieldLabel;
        private Button        _closeButton;
        private ScrollView    _list;

        // ── Row tracking ──────────────────────────────────────────────────────────

        private readonly List<GeneratorRowUI> _rows = new List<GeneratorRowUI>();

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _root            = root.Q<VisualElement>("generator-root");
            _totalYieldLabel = root.Q<Label>("total-yield-label");
            _closeButton     = root.Q<Button>("close-button");
            _list            = root.Q<ScrollView>("generator-list");

            if (_closeButton != null) _closeButton.clicked += Hide;

            SetVisible(false);
        }

        private void OnEnable()
        {
            EconomyService.OnResourcesChanged    += OnResourcesChanged;
            GeneratorSystem.OnGeneratorPurchased += OnGeneratorPurchased;
        }

        private void OnDisable()
        {
            EconomyService.OnResourcesChanged    -= OnResourcesChanged;
            GeneratorSystem.OnGeneratorPurchased -= OnGeneratorPurchased;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void Show()
        {
            BuildRows();
            RefreshAll();
            SetVisible(true);
        }

        public void Hide() => SetVisible(false);

        // ── Build ─────────────────────────────────────────────────────────────────

        private void BuildRows()
        {
            if (_list == null || _generatorSystem == null) return;
            _list.Clear();
            _rows.Clear();

            var configs = _generatorSystem.Configs;
            if (configs == null) return;

            foreach (var cfg in configs)
            {
                if (cfg == null) continue;
                var row = new GeneratorRowUI(cfg, _generatorSystem);
                _list.Add(row.Root);
                _rows.Add(row);
            }
        }

        private void RefreshAll()
        {
            double balance = EconomyService.CurrentResourcesStatic;
            float totalYield = 0f;

            foreach (var row in _rows)
            {
                row.Refresh(balance);
                totalYield += row.CurrentYield;
            }

            if (_totalYieldLabel != null)
                _totalYieldLabel.text = totalYield > 0f
                    ? $"+{GoldFormatter.Format((long)totalYield)}/sec"
                    : "+0/sec";
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void OnResourcesChanged(double balance, double delta) => RefreshAll();
        private void OnGeneratorPurchased(string id) => RefreshAll();

        // ── Visibility ────────────────────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            if (_root == null) return;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Single generator row in the list — not a MonoBehaviour, pure UI element.
    /// </summary>
    internal class GeneratorRowUI
    {
        public VisualElement Root { get; }
        public float CurrentYield { get; private set; }

        private readonly GeneratorConfigSO    _cfg;
        private readonly GeneratorSystem      _system;
        private readonly Label                _countLabel;
        private readonly Label                _yieldLabel;
        private readonly Label                _costLabel;
        private readonly Button               _buyBtn;

        public GeneratorRowUI(GeneratorConfigSO cfg, GeneratorSystem system)
        {
            _cfg    = cfg;
            _system = system;

            Root = new VisualElement();
            Root.AddToClassList("generator-row");

            // Info block
            var info = new VisualElement();
            info.AddToClassList("generator-info");

            var nameLabel = new Label(cfg.DisplayName);
            nameLabel.AddToClassList("generator-name");

            var descLabel = new Label(cfg.Description);
            descLabel.AddToClassList("generator-desc");

            _yieldLabel = new Label();
            _yieldLabel.AddToClassList("generator-yield");

            info.Add(nameLabel);
            info.Add(descLabel);
            info.Add(_yieldLabel);

            // Count
            _countLabel = new Label("0");
            _countLabel.AddToClassList("generator-count");

            // Buy button
            _buyBtn = new Button(OnBuy);
            _buyBtn.AddToClassList("generator-buy-btn");

            var buyText = new Label("BUY");
            buyText.AddToClassList("buy-btn-label");

            _costLabel = new Label();
            _costLabel.AddToClassList("buy-btn-cost");

            _buyBtn.Add(buyText);
            _buyBtn.Add(_costLabel);

            Root.Add(info);
            Root.Add(_countLabel);
            Root.Add(_buyBtn);
        }

        public void Refresh(double playerBalance)
        {
            int count = _system.GetCount(_cfg.GeneratorId);
            double cost = _system.GetNextCost(_cfg.GeneratorId);

            _countLabel.text = count.ToString();

            CurrentYield = count * _cfg.BaseYieldPerSecond;
            _yieldLabel.text = count > 0
                ? $"+{CurrentYield:F1}/sec"
                : $"{_cfg.BaseYieldPerSecond:F1}/sec each";

            _costLabel.text = GoldFormatter.Format(cost);
            _buyBtn.SetEnabled(playerBalance >= cost);

            // Locked state
            bool locked = IsLocked();
            if (locked) Root.AddToClassList("locked");
            else        Root.RemoveFromClassList("locked");
            _buyBtn.SetEnabled(!locked && playerBalance >= cost);
        }

        private bool IsLocked()
        {
            if (_cfg.UnlockPrerequisite == null || _cfg.UnlockRequirement <= 0) return false;
            return _system.GetCount(_cfg.UnlockPrerequisite.GeneratorId) < _cfg.UnlockRequirement;
        }

        private void OnBuy() => _system.TryPurchase(_cfg.GeneratorId);
    }
}
