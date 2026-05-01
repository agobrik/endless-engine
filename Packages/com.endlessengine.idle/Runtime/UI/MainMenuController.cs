using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Economy;
using EndlessEngine.Flow;
using EndlessEngine.Generator;
using EndlessEngine.Prestige;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Controls the Main Menu screen.
    /// Visible in GameFlowState.Menu, hidden during InRun and PostRun.
    ///
    /// Wires: Start Run button → GameFlowStateMachine.StartRun()
    ///        Gold label → EconomyService.OnResourcesChanged
    ///        Income rate → GeneratorSystem.CalculateTotalYield() (polled per second)
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField] private GameFlowStateMachine      _gameFlow;
        [SerializeField] private GeneratorSystem           _generatorSystem;
        [SerializeField] private PrestigeStateManager      _prestigeManager;
        [SerializeField] private PrestigeSystem            _prestigeSystem;
        [SerializeField] private GeneratorScreenController _generatorScreen;
        [SerializeField] private UpgradeScreenController   _upgradeScreen;

        // ── UI References ─────────────────────────────────────────────────────────

        private VisualElement _root;
        private Label         _goldLabel;
        private Label         _incomeRateLabel;
        private Button        _startRunButton;
        private Button        _upgradesButton;
        private Button        _generatorsButton;
        private Button        _prestigeButton;

        // ── State ─────────────────────────────────────────────────────────────────

        private float _incomeRatePollTimer;
        private const float IncomeRatePollInterval = 1f;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _root             = root.Q<VisualElement>("menu-root");
            _goldLabel        = root.Q<Label>("gold-label");
            _incomeRateLabel  = root.Q<Label>("income-rate-label");
            _startRunButton   = root.Q<Button>("start-run-button");
            _upgradesButton   = root.Q<Button>("upgrades-button");
            _generatorsButton = root.Q<Button>("generators-button");
            _prestigeButton   = root.Q<Button>("prestige-button");

            if (_startRunButton   != null) _startRunButton.clicked   += OnStartRun;
            if (_upgradesButton   != null) _upgradesButton.clicked   += OnUpgrades;
            if (_generatorsButton != null) _generatorsButton.clicked += OnGenerators;
            if (_prestigeButton   != null) _prestigeButton.clicked   += OnPrestige;

            if (_upgradeScreen != null) _upgradeScreen.OnHide += ShowMenu;
        }

        private void OnEnable()
        {
            EconomyService.OnResourcesChanged        += OnResourcesChanged;
            GameFlowStateMachine.OnEnteredMenu        += ShowMenu;
            GameFlowStateMachine.OnEnteredRun         += HideMenu;
            GameFlowStateMachine.OnEnteredPostRun     += HideMenu;
        }

        private void OnDisable()
        {
            EconomyService.OnResourcesChanged        -= OnResourcesChanged;
            GameFlowStateMachine.OnEnteredMenu        -= ShowMenu;
            GameFlowStateMachine.OnEnteredRun         -= HideMenu;
            GameFlowStateMachine.OnEnteredPostRun     -= HideMenu;
        }

        private void Start()
        {
            // Sync initial visibility with current state
            bool inMenu = _gameFlow == null || _gameFlow.IsInMenu;
            SetVisible(inMenu);
            UpdatePrestigeButton();
        }

        private void Update()
        {
            _incomeRatePollTimer += Time.deltaTime;
            if (_incomeRatePollTimer >= IncomeRatePollInterval)
            {
                _incomeRatePollTimer = 0f;
                UpdateIncomeRate();
            }
        }

        // ── Visibility ────────────────────────────────────────────────────────────

        private void ShowMenu() => SetVisible(true);
        private void HideMenu() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (_root == null) return;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) UpdatePrestigeButton();
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void OnResourcesChanged(double newBalance, double delta)
        {
            if (_goldLabel != null)
                _goldLabel.text = "◆ " + GoldFormatter.Format(newBalance);
        }

        private void UpdateIncomeRate()
        {
            if (_incomeRateLabel == null) return;
            double rate = _generatorSystem != null ? _generatorSystem.CalculateTotalYield() : 0.0;
            _incomeRateLabel.text = rate > 0.0
                ? $"+{GoldFormatter.Format(rate)}/sec"
                : "+0/sec";
        }

        private void UpdatePrestigeButton()
        {
            if (_prestigeButton == null) return;
            bool canPrestige = false;
            try
            {
                canPrestige = _prestigeSystem != null
                    ? _prestigeSystem.CanPrestige
                    : (_prestigeManager != null && _prestigeManager.CanPrestige);
            }
            catch { }
            _prestigeButton.SetEnabled(canPrestige);
        }

        // ── Button Handlers ───────────────────────────────────────────────────────

        private void OnStartRun()
        {
            _gameFlow?.StartRun();
        }

        private void OnUpgrades()
        {
            HideMenu();
            _upgradeScreen?.Show();
        }

        private void OnGenerators()
        {
            _generatorScreen?.Show();
        }

        private void OnPrestige()
        {
            // PrestigeSystem opens the confirmation overlay first — never bypass to StateManager directly
            _prestigeSystem?.TryInitiatePrestige();
        }
    }
}
