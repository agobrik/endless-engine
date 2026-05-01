using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Flow;
using EndlessEngine.Health;
using EndlessEngine.Milestone;
using EndlessEngine.Prestige;
using EndlessEngine.Wave;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Controls the in-game HUD: HP bar, Gold counter, and Wave number.
    /// Subscribes to static game events — never polls gameplay state.
    ///
    /// Attach to a GameObject with a UIDocument set to HUD.uxml (layer 0).
    ///
    /// GDD: design/gdd/hud-system.md
    /// ADR: ADR-0013 — UI Toolkit Runtime
    /// Sprint: S4-02 (core), S4-09 (upgrade pips + prestige button)
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("HP Bar Animation")]
        [Tooltip("Lerp speed for the HP bar fill (units/second). GDD Tuning: HPBarLerpSpeed.")]
        [SerializeField] private float _hpBarLerpSpeed = 5f;

        [Tooltip("Seconds before the delay bar begins to shrink. GDD Tuning: HPDamageDelaySeconds.")]
        [SerializeField] private float _hpDamageDelaySeconds = 0.5f;

        [Header("Gold Counter Animation")]
        [Tooltip("Duration for the Gold roll-up animation. GDD Tuning: GoldCounterAnimDuration.")]
        [SerializeField] private float _goldAnimDuration = 0.3f;

        [Header("Prestige Button")]
        [Tooltip("PrestigeStateManager in this scene — checked on each wave start.")]
        [SerializeField] private PrestigeStateManager _prestigeStateManager;
        [SerializeField] private PrestigeSystem       _prestigeSystem;

        [Header("Progress ETA (optional)")]
        [Tooltip("If assigned, ETA label shows time-to-reach the current target.")]
        [SerializeField] private ProgressETAService _etaService;

        // ── UI References ─────────────────────────────────────────────────────────

        private VisualElement _hudRoot;
        private VisualElement _hpBarFill;
        private VisualElement _hpBarDelay;
        private Label         _waveLabel;
        private Label         _goldLabel;
        private VisualElement _upgradePips;
        private Button        _prestigeButton;

        // ── Upgrade Pip State ─────────────────────────────────────────────────────

        private readonly List<VisualElement> _pipElements = new List<VisualElement>(8);

        // ── HP State ──────────────────────────────────────────────────────────────

        private float _hpFillTarget;      // target fill [0,1]
        private float _hpFillCurrent;     // current animated fill
        private float _hpDelayFill;       // delay bar fill (starts at previous value)
        private float _hpDelayTimer;      // countdown before delay bar shrinks

        // ── Gold State ────────────────────────────────────────────────────────────

        private double _goldDisplayed;     // value currently shown
        private double _goldTarget;        // target value
        private float _goldAnimTimer;     // elapsed animation time

        // ── Wave State ────────────────────────────────────────────────────────────

        private int _currentWave = 1;

        // ── Timer State ───────────────────────────────────────────────────────────

        private Label _timerLabel;
        private bool  _timerUrgent;

        // ── ETA State ─────────────────────────────────────────────────────────────

        private Label _etaLabel;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            var doc  = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;

            _hudRoot        = root.Q<VisualElement>("hud-root");
            _hpBarFill      = root.Q<VisualElement>("hp-bar-fill");
            _hpBarDelay     = root.Q<VisualElement>("hp-bar-delay");
            _waveLabel      = root.Q<Label>("wave-label");
            _goldLabel      = root.Q<Label>("gold-label");
            _upgradePips    = root.Q<VisualElement>("upgrade-pips");
            _prestigeButton = root.Q<Button>("prestige-button");
            _timerLabel     = root.Q<Label>("timer-label");
            _etaLabel       = root.Q<Label>("eta-label");

            if (_prestigeButton != null)
                _prestigeButton.clicked += OnPrestigeButtonClicked;

            // HUD only visible during a run
            SetHudVisible(false);

            // Safe defaults in case UIDocument loads before first events
            _hpFillTarget  = 1f;
            _hpFillCurrent = 1f;
            _hpDelayFill   = 1f;
        }

        private void OnEnable()
        {
            PlayerHealthComponent.OnPlayerHPChanged += OnPlayerHPChanged;
            EconomyService.OnResourcesChanged       += OnResourcesChanged;
            WaveSpawnManager.OnWaveStarted          += OnWaveStarted;
            RunSessionManager.OnRunTimerUpdated     += OnRunTimerUpdated;
            RunSessionManager.OnRunEnded            += OnRunEnded;
            GameFlowStateMachine.OnEnteredRun       += ShowHud;
            GameFlowStateMachine.OnEnteredMenu      += HideHud;
            GameFlowStateMachine.OnEnteredPostRun   += HideHud;
            ProgressETAService.OnETAUpdated         += OnETAUpdated;
        }

        private void OnDisable()
        {
            PlayerHealthComponent.OnPlayerHPChanged -= OnPlayerHPChanged;
            EconomyService.OnResourcesChanged       -= OnResourcesChanged;
            WaveSpawnManager.OnWaveStarted          -= OnWaveStarted;
            RunSessionManager.OnRunTimerUpdated     -= OnRunTimerUpdated;
            RunSessionManager.OnRunEnded            -= OnRunEnded;
            GameFlowStateMachine.OnEnteredRun       -= ShowHud;
            GameFlowStateMachine.OnEnteredMenu      -= HideHud;
            GameFlowStateMachine.OnEnteredPostRun   -= HideHud;
            ProgressETAService.OnETAUpdated         -= OnETAUpdated;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            AnimateHPBar(dt);
            AnimateGoldCounter(dt);
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void OnPlayerHPChanged(float currentHP, float maxHP)
        {
            if (maxHP <= 0f) return;

            float newFill = Mathf.Clamp01(currentHP / maxHP);

            if (newFill < _hpFillCurrent)
            {
                // Damage taken — reset delay bar to current fill, start delay countdown
                _hpDelayFill  = _hpFillCurrent;
                _hpDelayTimer = _hpDamageDelaySeconds;
            }

            _hpFillTarget = newFill;
        }

        private void OnResourcesChanged(double newBalance, double delta)
        {
            _goldTarget = newBalance;

            if (delta > 0L)
            {
                // Gain: animate roll-up from current displayed value to new balance
                _goldAnimTimer = 0f;
            }
            else
            {
                // Deduction or reset: snap immediately (no animation — GDD Rule 2)
                _goldDisplayed = newBalance;
                _goldAnimTimer = _goldAnimDuration; // skip animation
                _goldLabel.text = GoldFormatter.Format(_goldDisplayed);
            }
        }

        private void OnWaveStarted(int waveNumber)
        {
            _currentWave = waveNumber;
            if (_waveLabel != null)
                _waveLabel.text = "WAVE " + waveNumber;

            UpdateUpgradePips(waveNumber);
            UpdatePrestigeButton();
        }

        private void OnPrestigeButtonClicked()
        {
            _prestigeSystem?.TryInitiatePrestige();
        }

        private void ShowHud() => SetHudVisible(true);
        private void HideHud() => SetHudVisible(false);

        private void SetHudVisible(bool visible)
        {
            if (_hudRoot != null)
                _hudRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnRunTimerUpdated(float remaining)
        {
            if (_timerLabel == null) return;
            remaining = Mathf.Max(0f, remaining);
            int minutes = (int)(remaining / 60f);
            int seconds = (int)(remaining % 60f);
            _timerLabel.text = $"{minutes}:{seconds:D2}";

            bool urgent = remaining <= 30f;
            if (urgent != _timerUrgent)
            {
                _timerUrgent = urgent;
                if (urgent) _timerLabel.AddToClassList("urgent");
                else        _timerLabel.RemoveFromClassList("urgent");
            }
        }

        private void OnRunEnded(double goldEarned)
        {
            if (_timerLabel != null)
                _timerLabel.text = "0:00";
        }

        // ── Animation ─────────────────────────────────────────────────────────────

        private void AnimateHPBar(float dt)
        {
            if (_hpBarFill == null || _hpBarDelay == null) return;

            // Lerp fill toward target
            _hpFillCurrent = Mathf.MoveTowards(_hpFillCurrent, _hpFillTarget, _hpBarLerpSpeed * dt);
            ApplyBarWidth(_hpBarFill, _hpFillCurrent);

            // Delay bar: hold then shrink
            if (_hpDelayTimer > 0f)
            {
                _hpDelayTimer -= dt;
            }
            else if (_hpDelayFill > _hpFillCurrent)
            {
                _hpDelayFill = Mathf.MoveTowards(_hpDelayFill, _hpFillCurrent, _hpBarLerpSpeed * dt);
            }

            ApplyBarWidth(_hpBarDelay, _hpDelayFill);
        }

        private void AnimateGoldCounter(float dt)
        {
            if (_goldLabel == null) return;
            if (_goldDisplayed == _goldTarget) return;

            _goldAnimTimer += dt;
            float t = (_goldAnimDuration > 0f)
                ? Mathf.Clamp01(_goldAnimTimer / _goldAnimDuration)
                : 1f;

            _goldDisplayed = _goldDisplayed + (_goldTarget - _goldDisplayed) * t;

            if (t >= 1f)
                _goldDisplayed = _goldTarget;

            _goldLabel.text = GoldFormatter.Format(_goldDisplayed);
        }

        private static void ApplyBarWidth(VisualElement bar, float fill)
        {
            bar.style.width = new StyleLength(new Length(fill * 100f, LengthUnit.Percent));
        }

        // ── Upgrade Pips (S4-09) ──────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds upgrade progress pips per GDD F1:
        /// wavesUntilUpgrade = interval - ((waveNumber - 1) % interval)
        /// </summary>
        private void UpdateUpgradePips(int waveNumber)
        {
            if (_upgradePips == null) return;

            int interval = 3; // fallback default
            if (ConfigRegistry.IsLoaded)
                interval = ConfigRegistry.Wave.UpgradeSelectionWaveInterval;

            int wavesUntil = interval - ((waveNumber - 1) % interval);

            // Rebuild pips if interval changed or first call
            if (_pipElements.Count != interval)
            {
                _upgradePips.Clear();
                _pipElements.Clear();
                for (int i = 0; i < interval; i++)
                {
                    var pip = new VisualElement();
                    pip.AddToClassList("upgrade-pip");
                    _upgradePips.Add(pip);
                    _pipElements.Add(pip);
                }
            }

            // Light up the filled pips (count down from right)
            int filledCount = interval - wavesUntil;
            for (int i = 0; i < _pipElements.Count; i++)
            {
                bool filled = i < filledCount;
                if (filled)
                    _pipElements[i].RemoveFromClassList("empty");
                else
                    _pipElements[i].AddToClassList("empty");
            }
        }

        // ── Prestige Button (S4-09) ───────────────────────────────────────────────

        private void UpdatePrestigeButton()
        {
            if (_prestigeButton == null) return;
            bool canPrestige = false;
            try { canPrestige = _prestigeStateManager != null && _prestigeStateManager.CanPrestige; } catch { }
            _prestigeButton.SetEnabled(canPrestige);
        }

        // ── ETA Label (S9-04) ─────────────────────────────────────────────────────

        private void OnETAUpdated(long targetGold, float etaSeconds)
        {
            if (_etaLabel == null || _etaService == null) return;

            if (etaSeconds < 0f)
            {
                _etaLabel.style.display = DisplayStyle.None;
                return;
            }

            _etaLabel.style.display = DisplayStyle.Flex;
            _etaLabel.text = etaSeconds == 0f
                ? "Goal reached!"
                : $"Goal in {_etaService.FormatETA(etaSeconds)}";
        }
    }
}
