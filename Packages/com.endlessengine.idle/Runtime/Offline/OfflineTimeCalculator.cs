using System;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Offline
{
    /// <summary>
    /// Computes offline resource gain once per session on SaveService.OnSaveLoaded.
    ///
    /// Formula:
    ///   EffectiveDelta    = Min(offlineDeltaSeconds, OfflineAccumulationCap × 3600)
    ///   PermanentMult     = Min(IdleYieldMultiplierCap, BaseMultiplierPerPrestige ^ PrestigeCount)
    ///   StateModifier     = CurrentRunState == "IdleRecovery" ? 1.0 : ActiveRunStateOfflineModifier
    ///   OfflineGain       = Floor(IdleYieldRateBase × PermanentMult × StateModifier × EffectiveDelta)
    ///
    /// PermanentMultiplier is sourced from SaveData (not ConfigRegistry.Prestige) for crash-safety.
    /// OnOfflineGainCalculated fires exactly once per session, even if OnSaveLoaded fires multiple times.
    ///
    /// ADR: ADR-0004 — ISaveStateProvider (consumer, not provider)
    /// </summary>
    public class OfflineTimeCalculator : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires once per session after offline gain is computed.
        /// Parameters: (gain, effectiveDeltaSeconds)
        /// </summary>
        public static event Action<long, float> OnOfflineGainCalculated;

        // ── State ─────────────────────────────────────────────────────────────────

        private bool _hasRun;

        [SerializeField]
        private SaveService _saveService;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_saveService != null)
                _saveService.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (_saveService != null)
                _saveService.OnSaveLoaded -= HandleSaveLoaded;
        }

        // ── Core logic ────────────────────────────────────────────────────────────

        private void HandleSaveLoaded(SaveData saveData, bool isNewGame)
        {
            if (_hasRun) return;     // exactly once per session
            _hasRun = true;

            if (isNewGame)
            {
                OnOfflineGainCalculated?.Invoke(0L, 0f);
                return;
            }

            // Delta is already clock-skew-clamped by SaveService (load guard 1)
            float deltaSeconds = Mathf.Max(0f,
                (float)(DateTime.UtcNow - saveData.LastSessionTimestamp).TotalSeconds);

            float capSeconds     = ConfigRegistry.Economy.OfflineCapHours * 3600f;
            float effectiveDelta = Mathf.Min(deltaSeconds, capSeconds);

            float multCap      = ConfigRegistry.Economy.IdleYieldMultiplierCap;
            float permanentMult = Mathf.Min(multCap,
                Mathf.Pow(saveData.BaseMultiplierPerPrestige, saveData.PrestigeCount));

            // "IdleRecovery" = full yield; any other state = partial yield
            float stateModifier = saveData.CurrentRunState == "IdleRecovery"
                ? 1.0f
                : ConfigRegistry.Economy.ActiveRunStateOfflineModifier;

            double rawGain = (double)ConfigRegistry.Economy.IdleYieldRateBase
                           * permanentMult
                           * stateModifier
                           * effectiveDelta;

            long gain = (long)Math.Floor(rawGain);
            OnOfflineGainCalculated?.Invoke(gain, effectiveDelta);
        }

        // ── Test injection ────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Directly invokes the calculation logic with injected parameters.
        /// Bypasses the MonoBehaviour event subscription — allows EditMode testing.
        /// Resets the _hasRun flag before invoking so repeated calls work in tests.
        /// </summary>
        public void InvokeForTesting(SaveData saveData, bool isNewGame)
        {
            _hasRun = false;
            HandleSaveLoaded(saveData, isNewGame);
        }

        /// <summary>Resets the single-fire guard for test isolation.</summary>
        public void ResetForTesting() => _hasRun = false;
#endif
    }
}
