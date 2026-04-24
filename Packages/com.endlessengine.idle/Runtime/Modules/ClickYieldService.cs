using System;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Input;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Modules
{
    /// <summary>
    /// Module: Click-to-Produce
    /// Converts pointer clicks into immediate gold yield with optional combo and crit systems.
    ///
    /// This is a frame-driven service (Update), NOT tick-driven —
    /// clicks are instant, not deferred to the next tick.
    ///
    /// Auto-click: if BaseAutoClicksPerSecond > 0 (or raised via SetAutoClickRate),
    /// the service fires synthetic clicks on a timer, respecting the same combo/crit logic.
    ///
    /// Bootstrap wiring:
    ///   var click = gameObject.AddComponent&lt;ClickYieldService&gt;();
    ///   click.Initialize(clickConfig, economyService, passiveIncomeGetter);
    ///   saveService.RegisterStateProvider(click);
    /// </summary>
    public class ClickYieldService : MonoBehaviour, ISaveStateProvider
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires on each processed click. Parameters: (goldEarned, comboMultiplier, wasCrit).</summary>
        public static event Action<long, float, bool> OnClick;

        /// <summary>Fires when combo resets to 1 (gap exceeded).</summary>
        public static event Action OnComboReset;

        // ── Dependencies ──────────────────────────────────────────────────────────

        private ClickSourceConfigSO   _config;
        private EconomyService        _economy;
        private Func<float>           _getPassiveYieldPerSecond; // optional — for YieldRateClickFraction

        // ── Runtime state ─────────────────────────────────────────────────────────

        private float _comboMultiplier   = 1f;
        private float _timeSinceLastClick = float.MaxValue;
        private float _autoClickAccumulator;
        private float _autoClickRate;        // clicks/sec (runtime, overridable by upgrades)
        private float _clicksThisSecond;
        private float _clickRateSampleTimer;

        /// <summary>Current combo multiplier. 1 when no combo is active.</summary>
        public float ComboMultiplier => _comboMultiplier;

        /// <summary>Total gold earned by this module.</summary>
        public long TotalClickEarned { get; private set; }

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Inject dependencies. passiveYieldGetter is optional — used only when
        /// <see cref="ClickSourceConfigSO.YieldRateClickFraction"/> is > 0.
        /// </summary>
        public void Initialize(
            ClickSourceConfigSO config,
            EconomyService      economy,
            Func<float>         passiveYieldGetter = null)
        {
            _config                   = config;
            _economy                  = economy;
            _getPassiveYieldPerSecond = passiveYieldGetter;
            _autoClickRate            = config?.BaseAutoClicksPerSecond ?? 0f;
            _comboMultiplier          = 1f;
        }

        // ── Unity Update ──────────────────────────────────────────────────────────

        private void Update()
        {
            if (_config == null || _economy == null) return;

            float dt = Time.deltaTime;

            // Combo timeout
            _timeSinceLastClick += dt;
            if (_config.EnableCombo && _timeSinceLastClick > _config.ComboWindowSeconds
                && _comboMultiplier > 1f)
            {
                _comboMultiplier = 1f;
                OnComboReset?.Invoke();
            }

            // Click rate cap tracking (reset per second)
            _clickRateSampleTimer += dt;
            if (_clickRateSampleTimer >= 1f)
            {
                _clicksThisSecond    = 0;
                _clickRateSampleTimer = 0f;
            }

            // Manual click
            if (TryConsumeClick())
                ProcessClick();

            // Auto-click (upgrade-driven)
            if (_autoClickRate > 0f)
            {
                _autoClickAccumulator += _autoClickRate * dt;
                while (_autoClickAccumulator >= 1f)
                {
                    _autoClickAccumulator -= 1f;
                    ProcessClick();
                }
            }
        }

        // ── Click Processing ──────────────────────────────────────────────────────

        private bool TryConsumeClick()
        {
            if (_inputProvider == null) return false;
            return _inputProvider.GetPointerClickedThisFrame();
        }

        private void ProcessClick()
        {
            // Rate cap
            if (_config.MaxClicksPerSecondCap > 0 && _clicksThisSecond >= _config.MaxClicksPerSecondCap)
                return;

            _clicksThisSecond++;
            _timeSinceLastClick = 0f;

            // Combo step
            if (_config.EnableCombo)
            {
                _comboMultiplier = Mathf.Min(
                    _config.MaxComboMultiplier,
                    _comboMultiplier + _config.ComboMultiplierStep);
            }

            // Base yield
            float baseYield = _config.GoldPerClick;

            // Fraction of passive yield/s
            if (_config.YieldRateClickFraction > 0f && _getPassiveYieldPerSecond != null)
                baseYield += _getPassiveYieldPerSecond() * _config.YieldRateClickFraction;

            // Crit check
            bool wasCrit = _config.CritChance > 0f && UnityEngine.Random.value < _config.CritChance;
            float critMult = wasCrit ? _config.CritMultiplier : 1f;

            long earned = (long)(baseYield * _comboMultiplier * critMult * _config.GlobalMultiplier);
            if (earned <= 0) earned = 1;

            _economy.AddResources(earned);
            TotalClickEarned += earned;

            OnClick?.Invoke(earned, _comboMultiplier, wasCrit);
        }

        // ── IInputProvider plumbing (optional — use when injected) ────────────────

        private IInputProvider _inputProvider;

        /// <summary>
        /// Optional: provide an IInputProvider so ClickYieldService uses the same
        /// abstracted input as the rest of the engine. Must be set before clicking
        /// is functional — without an IInputProvider, manual clicks are ignored.
        /// </summary>
        public void SetInputProvider(IInputProvider input) => _inputProvider = input;

        // ── Public upgrade API ────────────────────────────────────────────────────

        /// <summary>
        /// Set the auto-click rate from an upgrade. Additive with BaseAutoClicksPerSecond.
        /// E.g. after buying "Auto Tapper Lv2": SetAutoClickRate(baseRate + 2).
        /// </summary>
        public void SetAutoClickRate(float clicksPerSecond)
            => _autoClickRate = Mathf.Max(0f, clicksPerSecond);

        /// <summary>Current auto-click rate in clicks/second.</summary>
        public float AutoClickRate => _autoClickRate;

        /// <summary>Resets earned total and combo (call on prestige).</summary>
        public void ResetTotals()
        {
            TotalClickEarned = 0;
            _comboMultiplier = 1f;
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public int ProviderOrder => SaveConstants.SaveProviderOrder.Click;

        /// <inheritdoc/>
        public void OnBeforeSave(SaveData saveData)
        {
            saveData.ClickState ??= new ClickModuleSaveState();
            saveData.ClickState.TotalClickEarned      = TotalClickEarned;
            saveData.ClickState.AutoClickRateOverride  = _autoClickRate;
        }

        /// <inheritdoc/>
        public void OnAfterLoad(SaveData saveData)
        {
            if (saveData.ClickState == null) return;
            TotalClickEarned = saveData.ClickState.TotalClickEarned;
            if (saveData.ClickState.AutoClickRateOverride > 0f)
                _autoClickRate = saveData.ClickState.AutoClickRateOverride;
        }

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Fires a synthetic click bypassing IInputProvider. For tests.</summary>
        public void SimulateClickForTesting() => ProcessClick();

        /// <summary>Clears all static subscribers. Call in test TearDown.</summary>
        public static void ClearSubscribersForTesting()
        {
            OnClick      = null;
            OnComboReset = null;
        }
#endif
    }
}
