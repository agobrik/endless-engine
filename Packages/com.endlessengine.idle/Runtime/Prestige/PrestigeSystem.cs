using System;
using UnityEngine;
using EndlessEngine.Config;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Prestige
{
    /// <summary>
    /// Player-facing coordinator for the prestige ceremony.
    ///
    /// Responsibilities:
    ///   - Expose CanPrestige (delegates to PrestigeStateManager)
    ///   - TryInitiatePrestige(): pause combat, open prestige screen
    ///   - GetPrestigePreview(): multiplier + offline projection (read-only, no mutation)
    ///   - Coordinate ceremony: OnPrestigeComplete → OnPrestigeCeremonyStart → wait → OnPrestigeCeremonyComplete
    ///   - Skip ceremony on any player input (SkipCeremony())
    ///   - Fire OnPrestigeScreenDismissed(confirmed) to resume combat or start fresh run
    ///
    /// This class is the public API for prestige. Internal lifecycle and crash safety
    /// live in PrestigeStateManager. Economy reset and UAS reset live in their own systems.
    ///
    /// ADR: ADR-0010 — Prestige Crash Safety
    /// ADR: ADR-0004 — ISaveStateProvider
    /// GDD: design/gdd/prestige-system.md
    /// </summary>
    public class PrestigeSystem : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when prestige button is pressed and CanPrestige = true. Prestige Screen UI subscribes.</summary>
        public static event Action OnPrestigeScreenRequested;

        /// <summary>Fires when combat should pause (prestige screen open or ceremony in progress).</summary>
        public static event Action OnPauseRequested;

        /// <summary>Fires when prestige ceremony begins. UI subscribes to animate the reveal.</summary>
        public static event Action<int, float, long> OnPrestigeCeremonyStart;

        /// <summary>Fires when ceremony completes (or is skipped). UI dismisses; wave 1 starts.</summary>
        public static event Action OnPrestigeCeremonyComplete;

        /// <summary>
        /// Fires when prestige screen is dismissed.
        /// confirmed=true: prestige happened → fresh run begins.
        /// confirmed=false: player cancelled → combat resumes.
        /// AutoBattleController and WaveSpawnManager subscribe.
        /// </summary>
        public static event Action<bool> OnPrestigeScreenDismissed;

        // ── Dependencies ──────────────────────────────────────────────────────────

        [SerializeField]
        private PrestigeStateManager _prestigeStateManager;

        // ── State ─────────────────────────────────────────────────────────────────

        private PrestigeSystemState _state = PrestigeSystemState.Inactive;
        private Coroutine           _ceremonyCoroutine;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>True when the player may initiate prestige. Delegates to PrestigeStateManager.</summary>
        public bool CanPrestige => _prestigeStateManager != null && _prestigeStateManager.CanPrestige;

        /// <summary>
        /// Call when the player presses the prestige button.
        /// No-op if CanPrestige = false or already in an active prestige state.
        /// </summary>
        public void TryInitiatePrestige()
        {
            if (_state != PrestigeSystemState.Inactive) return;
            if (!CanPrestige) return;

            _state = PrestigeSystemState.ScreenOpen;
            OnPauseRequested?.Invoke();
            OnPrestigeScreenRequested?.Invoke();
        }

        /// <summary>
        /// Call when the player presses Cancel on the prestige screen.
        /// Returns to Inactive and resumes combat.
        /// </summary>
        public void CancelPrestige()
        {
            if (_state != PrestigeSystemState.ScreenOpen) return;
            _state = PrestigeSystemState.Inactive;
            OnPrestigeScreenDismissed?.Invoke(false);
        }

        /// <summary>
        /// Call when the player presses Confirm on the prestige screen.
        /// Delegates to PrestigeStateManager.TryPrestige() which owns the crash-safe sequence.
        /// </summary>
        public void ConfirmPrestige()
        {
            if (_state != PrestigeSystemState.ScreenOpen) return;
            if (_prestigeStateManager == null) return;

            bool started = _prestigeStateManager.TryPrestige();
            if (!started)
            {
                // EC-PRG-02: PSM rejected — keep screen open, show error
                Debug.LogWarning("[PrestigeSystem] TryPrestige() returned false — prestige rejected by PrestigeStateManager.");
            }
            // PrestigeStateManager.OnPrestigeComplete will drive the ceremony from here
        }

        /// <summary>
        /// Skips the remaining ceremony animation. GDD Rule 7.
        /// Any player input during ceremony calls this.
        /// </summary>
        public void SkipCeremony()
        {
            if (_state != PrestigeSystemState.Ceremony) return;
            if (_ceremonyCoroutine != null)
            {
                StopCoroutine(_ceremonyCoroutine);
                _ceremonyCoroutine = null;
            }
            CompleteCeremony();
        }

        /// <summary>
        /// Computes the prestige preview (multiplier + offline projection) without mutating state.
        /// GDD Rule 4 / Formulas F1 + F2.
        /// </summary>
        public PrestigePreviewData GetPrestigePreview()
        {
            if (_prestigeStateManager == null)
                return default;

            var cfg = ConfigRegistry.Prestige;
            int currentCount  = _prestigeStateManager.PrestigeCount;
            float currentMult = _prestigeStateManager.GetPermanentMultiplier();

            // F2: projected new multiplier
            float newMult = Mathf.Min(
                cfg.MaxPermanentMultiplier,
                Mathf.Pow(cfg.BaseMultiplierPerPrestige, currentCount + 1));

            // F1: offline yield projection (6h, informational only)
            float idleBase = ConfigRegistry.Economy.IdleYieldRateBase;
            const float ProjectionHours = 6f;
            long projectedGain = (long)Mathf.Floor(idleBase * newMult * ProjectionHours * 3600f);

            return new PrestigePreviewData
            {
                CurrentPrestigeCount  = currentCount,
                NewPrestigeCount      = currentCount + 1,
                CurrentMultiplier     = currentMult,
                NewMultiplier         = newMult,
                ProjectedOfflineGain6h = projectedGain,
            };
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            PrestigeStateManager.OnPrestigeComplete += HandlePrestigeComplete;
        }

        private void OnDisable()
        {
            PrestigeStateManager.OnPrestigeComplete -= HandlePrestigeComplete;
        }

        // ── Ceremony ─────────────────────────────────────────────────────────────

        private void HandlePrestigeComplete(int count, float multiplier)
        {
            _state = PrestigeSystemState.Ceremony;

            // Offline projection with the new multiplier
            float idleBase = ConfigRegistry.Economy != null ? ConfigRegistry.Economy.IdleYieldRateBase : 0f;
            const float ProjectionHours = 6f;
            long offlineProjection = (long)Mathf.Floor(idleBase * multiplier * ProjectionHours * 3600f);

            OnPrestigeCeremonyStart?.Invoke(count, multiplier, offlineProjection);

            float duration = ConfigRegistry.Prestige != null
                ? 3f   // PrestigeConfigSO doesn't have PrestigeCeremonyDurationSeconds yet — default 3s
                : 3f;

            _ceremonyCoroutine = StartCoroutine(CeremonyCoroutine(duration));
        }

        private System.Collections.IEnumerator CeremonyCoroutine(float duration)
        {
            // EC-PRG-03: duration=0 → immediate completion
            if (duration > 0f)
                yield return new UnityEngine.WaitForSeconds(duration);

            _ceremonyCoroutine = null;
            CompleteCeremony();
        }

        private void CompleteCeremony()
        {
            _state = PrestigeSystemState.Inactive;
            OnPrestigeCeremonyComplete?.Invoke();
            // GDD Rule 8: fresh run start — WaveSpawnManager and AutoBattleController subscribe
            OnPrestigeScreenDismissed?.Invoke(true);
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Injects PrestigeStateManager reference for tests that bypass MonoBehaviour wiring.</summary>
        public void InjectPrestigeStateManagerForTesting(PrestigeStateManager psm) => _prestigeStateManager = psm;

        /// <summary>Forces state for testing.</summary>
        public void SetStateForTesting(PrestigeSystemState state) => _state = state;

        /// <summary>Returns current state for assertion.</summary>
        public PrestigeSystemState GetStateForTesting() => _state;

        /// <summary>Fires HandlePrestigeComplete for testing ceremony flow without PSM.</summary>
        public void SimulatePrestigeCompleteForTesting(int count, float mult) => HandlePrestigeComplete(count, mult);

        /// <summary>Clears static events for test isolation.</summary>
        public static void ClearStaticEventsForTesting()
        {
            OnPrestigeScreenRequested  = null;
            OnPauseRequested           = null;
            OnPrestigeCeremonyStart    = null;
            OnPrestigeCeremonyComplete = null;
            OnPrestigeScreenDismissed  = null;
        }
#endif
    }

    // ── State machine ──────────────────────────────────────────────────────────────

    /// <summary>Internal state machine states for PrestigeSystem.</summary>
    public enum PrestigeSystemState
    {
        Inactive,
        ScreenOpen,
        Ceremony,
    }

    // ── Preview data ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Read-only display data for the Prestige Screen preview.
    /// GDD: design/gdd/prestige-system.md — UI Requirements
    /// </summary>
    public struct PrestigePreviewData
    {
        public int   CurrentPrestigeCount;
        public int   NewPrestigeCount;
        public float CurrentMultiplier;
        public float NewMultiplier;
        public long  ProjectedOfflineGain6h;
    }
}
