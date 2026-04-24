using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Flow;
using EndlessEngine.Input;

namespace EndlessEngine.Modules
{
    /// <summary>
    /// Module: Cursor Activity
    /// Converts mouse/pointer movement into gold based on <see cref="CursorActivityConfigSO"/>.
    ///
    /// Supports three yield models (Speed / Hover / Distance) — see SO docs.
    /// Subscribes to TickEngine.OnTick for income cadence and reads IInputProvider
    /// each frame for movement data.
    ///
    /// This service is opt-in: only add it to Bootstrap if your game uses cursor activity.
    /// It stacks additively with PassiveIncomeService (generators) and ClickYieldService.
    ///
    /// Bootstrap wiring:
    ///   var cursor = gameObject.AddComponent&lt;CursorYieldService&gt;();
    ///   cursor.Initialize(cursorConfig, economyService, gameFlow, inputProvider);
    /// </summary>
    public class CursorYieldService : MonoBehaviour
    {
        // ── Dependencies ──────────────────────────────────────────────────────────

        private CursorActivityConfigSO _config;
        private EconomyService         _economy;
        private GameFlowStateMachine   _gameFlow;
        private IInputProvider         _input;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private float   _smoothedSpeed;      // pixels/second, smoothed
        private float   _distanceBank;       // accumulated pixels (Distance model)
        private float   _hoverTimer;         // seconds cursor has been hovering
        private Vector2 _lastHoverCenter;    // world pos where hover started
        private bool    _hoverActive;

        /// <summary>Current instantaneous yield/s (before run modifier). Exposed for HUD.</summary>
        public float CurrentYieldPerSecond { get; private set; }

        /// <summary>Total gold earned by this module since last prestige/reset.</summary>
        public long TotalCursorEarned { get; private set; }

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Inject dependencies. Call from Bootstrap after all systems are initialized.
        /// Config may be null — service will no-op safely if uninitialized.
        /// </summary>
        public void Initialize(
            CursorActivityConfigSO config,
            EconomyService         economy,
            GameFlowStateMachine   gameFlow,
            IInputProvider         input)
        {
            _config   = config;
            _economy  = economy;
            _gameFlow = gameFlow;
            _input    = input;
        }

        private void OnEnable()  => TickEngine.OnTick += HandleTick;
        private void OnDisable() => TickEngine.OnTick -= HandleTick;

        // ── Frame Update — track mouse state ─────────────────────────────────────

        private void Update()
        {
            if (_config == null || _input == null) return;

            Vector2 delta = _input.GetMouseScreenDelta();
            float pixelsThisFrame = delta.magnitude;
            float speedThisFrame  = pixelsThisFrame / Mathf.Max(Time.deltaTime, 0.0001f);

            switch (_config.YieldModel)
            {
                case CursorYieldModel.Speed:
                    // Smooth toward current speed
                    float smoothing = _config.SmoothingSpeed > 0
                        ? _config.SmoothingSpeed * Time.deltaTime
                        : 1f;
                    _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speedThisFrame, smoothing);
                    CurrentYieldPerSecond = SpeedToYield(_smoothedSpeed);
                    break;

                case CursorYieldModel.Distance:
                    _distanceBank += pixelsThisFrame;
                    // Yield is calculated per-tick from banked distance
                    CurrentYieldPerSecond = pixelsThisFrame > 0
                        ? _config.MaxYieldPerSecond
                        : _config.IdleYieldPerSecond;
                    break;

                case CursorYieldModel.Hover:
                    UpdateHoverState();
                    break;
            }
        }

        private void UpdateHoverState()
        {
            Vector2 worldPos = _input.GetMouseWorldPosition();
            Vector2 delta    = _input.GetMouseScreenDelta();
            bool    moving   = delta.magnitude > 1f; // >1px = moving

            if (!_hoverActive)
            {
                if (!moving)
                {
                    // Just stopped — start warmup
                    _hoverActive      = true;
                    _hoverTimer       = 0f;
                    _lastHoverCenter  = worldPos;
                }
                CurrentYieldPerSecond = _config.IdleYieldPerSecond;
                return;
            }

            // Check if cursor has drifted outside hover radius
            if (Vector2.Distance(worldPos, _lastHoverCenter) > _config.HoverRadius || moving)
            {
                _hoverActive          = false;
                _hoverTimer           = 0f;
                CurrentYieldPerSecond = _config.IdleYieldPerSecond;
                return;
            }

            _hoverTimer += Time.deltaTime;
            if (_hoverTimer >= _config.HoverWarmupSeconds)
                CurrentYieldPerSecond = _config.MaxYieldPerSecond * _config.GlobalMultiplier;
            else
                CurrentYieldPerSecond = _config.IdleYieldPerSecond;
        }

        // ── Tick Handler — push income to economy ─────────────────────────────────

        private void HandleTick(float effectiveDt)
        {
            if (_config == null || _economy == null) return;

            float yieldPerSec = 0f;

            switch (_config.YieldModel)
            {
                case CursorYieldModel.Speed:
                    yieldPerSec = CurrentYieldPerSecond;
                    break;

                case CursorYieldModel.Distance:
                    // Drain the distance bank
                    if (_config.PixelsPerGold > 0 && _distanceBank > 0)
                    {
                        long goldFromDistance = (long)(_distanceBank / _config.PixelsPerGold);
                        if (goldFromDistance > 0)
                        {
                            _distanceBank -= goldFromDistance * _config.PixelsPerGold;
                            long income = (long)(goldFromDistance * _config.GlobalMultiplier * GetRunModifier());
                            if (income > 0)
                            {
                                _economy.AddResources(income);
                                TotalCursorEarned += income;
                            }
                        }
                    }
                    return; // Distance model handled differently

                case CursorYieldModel.Hover:
                    yieldPerSec = CurrentYieldPerSecond;
                    break;
            }

            float modifier = _config.ApplyRunModifier ? GetRunModifier() : 1f;
            long  earned   = (long)(yieldPerSec * _config.GlobalMultiplier * modifier * effectiveDt);
            if (earned <= 0) return;

            _economy.AddResources(earned);
            TotalCursorEarned += earned;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private float SpeedToYield(float speed)
        {
            float idle = _config.IdleSpeedThreshold;
            float full = _config.FullSpeedThreshold;
            if (full <= idle) return _config.MaxYieldPerSecond;

            float t = Mathf.InverseLerp(idle, full, speed);
            return Mathf.Lerp(_config.IdleYieldPerSecond, _config.MaxYieldPerSecond, t);
        }

        private float GetRunModifier()
        {
            if (_gameFlow == null || !_gameFlow.IsInRun) return 1f;
            try { return ConfigRegistry.Run.ActiveRunPassiveModifier; } catch { return 0.5f; }
        }

        // ── Public controls ───────────────────────────────────────────────────────

        /// <summary>Resets earned total (call on prestige).</summary>
        public void ResetTotals() => TotalCursorEarned = 0;

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Force-set smoothed speed for testing yield calculations.</summary>
        public void SetSmoothedSpeedForTesting(float speed) => _smoothedSpeed = speed;
#endif
    }
}
