using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Economy;
using EndlessEngine.Flow;
using EndlessEngine.Wave;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Controls the Post-Run summary screen.
    /// Shown when GameFlowStateMachine enters PostRun state.
    /// Displays gold earned this run, waves cleared, total gold.
    ///
    /// "Play Again" → GameFlowStateMachine.ReturnToMenu() then StartRun()
    /// "Main Menu"  → GameFlowStateMachine.ReturnToMenu()
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PostRunController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField] private GameFlowStateMachine _gameFlow;
        [SerializeField] private RunSessionManager    _runSessionManager;

        // ── UI References ─────────────────────────────────────────────────────────

        private VisualElement _root;
        private Label         _goldEarnedLabel;
        private Label         _wavesClearedLabel;
        private Label         _totalGoldLabel;
        private Button        _playAgainButton;
        private Button        _returnMenuButton;

        // ── State ─────────────────────────────────────────────────────────────────

        private int  _wavesCleared;
        private double _goldEarned;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _root              = root.Q<VisualElement>("postrun-root");
            _goldEarnedLabel   = root.Q<Label>("gold-earned-label");
            _wavesClearedLabel = root.Q<Label>("waves-cleared-label");
            _totalGoldLabel    = root.Q<Label>("total-gold-label");
            _playAgainButton   = root.Q<Button>("play-again-button");
            _returnMenuButton  = root.Q<Button>("return-menu-button");

            if (_playAgainButton  != null) _playAgainButton.clicked  += OnPlayAgain;
            if (_returnMenuButton != null) _returnMenuButton.clicked += OnReturnMenu;

            // Hidden by default — shown when PostRun fires
            SetVisible(false);
        }

        private void OnEnable()
        {
            GameFlowStateMachine.OnEnteredPostRun += ShowPostRun;
            GameFlowStateMachine.OnEnteredMenu    += HidePostRun;
            GameFlowStateMachine.OnEnteredRun     += HidePostRun;
            WaveSpawnManager.OnWaveComplete       += OnWaveComplete;
            RunSessionManager.OnRunEnded          += OnRunEnded;
        }

        private void OnDisable()
        {
            GameFlowStateMachine.OnEnteredPostRun -= ShowPostRun;
            GameFlowStateMachine.OnEnteredMenu    -= HidePostRun;
            GameFlowStateMachine.OnEnteredRun     -= HidePostRun;
            WaveSpawnManager.OnWaveComplete       -= OnWaveComplete;
            RunSessionManager.OnRunEnded          -= OnRunEnded;
        }

        // ── Visibility ────────────────────────────────────────────────────────────

        private void ShowPostRun()
        {
            // Stats populated by OnRunEnded — just show the panel
            SetVisible(true);
        }

        private void HidePostRun() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (_root == null) return;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void OnWaveComplete(int waveNumber)
        {
            _wavesCleared = waveNumber;
        }

        private void OnRunEnded(double goldEarned)
        {
            _goldEarned = goldEarned;
            PopulateStats();
        }

        private void PopulateStats()
        {
            double totalGold = EconomyService.CurrentResourcesStatic;

            if (_goldEarnedLabel   != null) _goldEarnedLabel.text   = GoldFormatter.Format(_goldEarned);
            if (_wavesClearedLabel != null) _wavesClearedLabel.text = _wavesCleared.ToString();
            if (_totalGoldLabel    != null) _totalGoldLabel.text    = GoldFormatter.Format(totalGold);
        }

        // ── Button Handlers ───────────────────────────────────────────────────────

        private void OnPlayAgain()
        {
            _wavesCleared = 0;
            _goldEarned   = 0;
            _gameFlow?.ReturnToMenu();
            _gameFlow?.StartRun();
        }

        private void OnReturnMenu()
        {
            _wavesCleared = 0;
            _goldEarned   = 0;
            _gameFlow?.ReturnToMenu();
        }
    }
}
