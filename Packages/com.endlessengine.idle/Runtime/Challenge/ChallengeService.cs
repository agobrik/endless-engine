using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Stats;
using EndlessEngine.Upgrade;

namespace EndlessEngine.Challenge
{
    /// <summary>
    /// Manages the active challenge run:
    ///   - Activates / deactivates a ChallengeConfigSO for the upcoming run.
    ///   - Applies stat modifiers to the upgrade/stat system.
    ///   - Tracks victory condition (wave threshold, time limit).
    ///   - Awards reward multiplier on success, fires failure event on defeat.
    ///
    /// Bootstrap wiring:
    ///   challengeService.Initialize(economyService, upgradeTreeService);
    ///   // Then call ActivateChallenge(config) before a run starts.
    ///
    /// ChallengeService does NOT persist between sessions — challenges are
    /// per-run modifiers and do not need save/load.
    /// </summary>
    public class ChallengeService : MonoBehaviour, IModifierSource
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when a challenge becomes active (before run starts).</summary>
        public static event Action<ChallengeConfigSO> OnChallengeActivated;

        /// <summary>Fires when the active challenge is cancelled or deselected.</summary>
        public static event Action OnChallengeCancelled;

        /// <summary>
        /// Fires when the player completes the challenge successfully.
        /// Parameter: the gold reward (base × RewardMultiplier).
        /// </summary>
        public static event Action<long> OnChallengeCompleted;

        /// <summary>Fires when the player fails the challenge (died before reaching wave threshold).</summary>
        public static event Action OnChallengeFailed;

        // ── State ─────────────────────────────────────────────────────────────────

        private EconomyService    _economyService;
        private UpgradeTreeService _upgradeTree;

        private ChallengeConfigSO _active;
        private bool              _runActive;

        // Track which systems have been disabled so we can re-enable them
        private readonly HashSet<string> _disabledSystems = new HashSet<string>();

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(EconomyService economyService, UpgradeTreeService upgradeTree = null)
        {
            _economyService = economyService;
            _upgradeTree    = upgradeTree;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>The currently selected/active challenge, or null.</summary>
        public ChallengeConfigSO ActiveChallenge => _active;

        /// <summary>Whether a challenge run is currently in progress.</summary>
        public bool IsRunActive => _runActive;

        /// <summary>
        /// Selects a challenge for the next run. Call before GameFlowStateMachine.StartRun().
        /// If another challenge is active it is replaced.
        /// </summary>
        public void ActivateChallenge(ChallengeConfigSO config)
        {
            _active = config;
            OnChallengeActivated?.Invoke(config);
        }

        /// <summary>Deselects the current challenge (player opts out).</summary>
        public void CancelChallenge()
        {
            _active = null;
            OnChallengeCancelled?.Invoke();
        }

        /// <summary>
        /// Called at run-start (e.g. from VerticalSliceBootstrap OnEnteredRun handler).
        /// Applies all modifiers in the active challenge.
        /// </summary>
        public void OnRunStarted()
        {
            if (_active == null) return;
            _runActive = true;
            ApplyModifiers(_active);
        }

        /// <summary>
        /// Called each time a new wave starts. Checks the victory condition.
        /// If RequiredWave is reached: awards reward and fires OnChallengeCompleted.
        /// </summary>
        public void NotifyWaveReached(int waveNumber)
        {
            if (!_runActive || _active == null) return;
            if (_active.RequiredWave <= 0) return;

            if (waveNumber >= _active.RequiredWave)
                CompleteChallenge();
        }

        /// <summary>
        /// Called when the player dies or the run ends in defeat.
        /// Removes modifiers and fires OnChallengeFailed.
        /// </summary>
        public void NotifyRunFailed()
        {
            if (!_runActive || _active == null) return;
            RemoveModifiers(_active);
            _runActive = false;
            _active    = null;
            OnChallengeFailed?.Invoke();
        }

        /// <summary>
        /// Returns all modifier values of a given type for the active challenge.
        /// Callers (e.g. EnemyManager) query this to apply difficulty scaling.
        /// Returns empty list when no challenge is active.
        /// </summary>
        public List<ChallengeModifier> GetModifiers(ChallengeModifierType type)
        {
            var result = new List<ChallengeModifier>();
            if (_active?.Modifiers == null) return result;
            foreach (var m in _active.Modifiers)
                if (m.Type == type) result.Add(m);
            return result;
        }

        /// <summary>Returns true if the named system has been disabled by the active challenge.</summary>
        public bool IsSystemDisabled(string systemId) => _disabledSystems.Contains(systemId);

        /// <summary>
        /// Returns the effective gold reward for the current run.
        /// Applies RewardMultiplier to the provided base gold amount.
        /// Returns baseGold unchanged if no challenge is active.
        /// </summary>
        public long CalculateReward(double baseGold)
        {
            if (_active == null) return (long)baseGold;
            return (long)(baseGold * _active.RewardMultiplier);
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private void CompleteChallenge()
        {
            double baseGold = _economyService?.CurrentResources ?? 0.0;
            long reward    = CalculateReward(baseGold);

            // Award bonus points
            // (SkillTreeService.AddPoints would be called here if wired, via event)
            RemoveModifiers(_active);
            _runActive = false;
            var completed = _active;
            _active = null;

            OnChallengeCompleted?.Invoke(reward);
        }

        private void ApplyModifiers(ChallengeConfigSO config)
        {
            if (config?.Modifiers == null) return;
            _disabledSystems.Clear();
            foreach (var m in config.Modifiers)
            {
                if (m.Type == ChallengeModifierType.DisableSystem)
                    _disabledSystems.Add(m.TargetId);
                // Stat override/multiplier: queried externally via GetModifiers()
                // — we do not mutate UpgradeTreeService directly to stay data-driven
            }
        }

        private void RemoveModifiers(ChallengeConfigSO config)
        {
            _disabledSystems.Clear();
        }

        // ── IModifierSource ───────────────────────────────────────────────────────

        public string SourceId => "challenge";

        public Modifier GetModifier(StatType stat)
        {
            if (_active?.Modifiers == null || !_runActive) return Modifier.None;
            double mult = 1.0;
            foreach (var m in _active.Modifiers)
            {
                if (m.Type != ChallengeModifierType.StatMultiplier) continue;
                if (!System.Enum.TryParse<StatType>(m.TargetId, ignoreCase: true, out var targetStat)) continue;
                if (targetStat == stat) mult *= m.Value;
            }
            return mult == 1.0 ? Modifier.None : Modifier.FromMultiplier(mult);
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnChallengeActivated  = null;
            OnChallengeCancelled  = null;
            OnChallengeCompleted  = null;
            OnChallengeFailed     = null;
        }

        public void ForceRunActiveForTesting(bool value) => _runActive = value;
#endif
    }
}
