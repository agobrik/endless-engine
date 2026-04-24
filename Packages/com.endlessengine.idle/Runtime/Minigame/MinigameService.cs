using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;

namespace EndlessEngine.Minigame
{
    /// <summary>
    /// Manages active skill cooldowns and minigame sessions.
    ///
    /// Architecture:
    ///   - ActiveSkillConfigSO defines each skill + its minigame type + reward.
    ///   - TryTrigger() checks cooldown, starts the minigame session.
    ///   - RecordAction() is called by the UI for each player action (tap, etc.).
    ///   - Sessions expire after MinigameDurationSeconds; reward is computed and awarded.
    ///   - Multiple skills can have independent cooldowns; only one minigame runs at a time.
    ///
    /// Bootstrap wiring:
    ///   minigameService.Initialize(skills, economyService);
    /// </summary>
    public class MinigameService : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when a minigame session starts. Parameter: skill config.</summary>
        public static event Action<ActiveSkillConfigSO> OnMinigameStarted;

        /// <summary>Fires each RecordAction call. Parameters: skill, total actions so far.</summary>
        public static event Action<ActiveSkillConfigSO, int> OnActionRecorded;

        /// <summary>Fires when a session ends (timeout or manual close). Parameters: skill, total reward.</summary>
        public static event Action<ActiveSkillConfigSO, long> OnMinigameEnded;

        /// <summary>Fires when a skill's cooldown expires (ready to use again). Parameter: skillId.</summary>
        public static event Action<string> OnSkillReady;

        // ── State ─────────────────────────────────────────────────────────────────

        private readonly Dictionary<string, ActiveSkillConfigSO> _skills     = new Dictionary<string, ActiveSkillConfigSO>();
        private readonly Dictionary<string, float>                _cooldowns  = new Dictionary<string, float>(); // remaining seconds
        private EconomyService _economy;

        private ActiveSkillConfigSO _activeSession;
        private int   _sessionActions;
        private Coroutine _sessionTimer;
        private Coroutine _cooldownTicker;

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(ActiveSkillConfigSO[] skills, EconomyService economy)
        {
            _skills.Clear();
            _cooldowns.Clear();
            _economy = economy;
            if (skills == null) return;
            foreach (var s in skills)
                if (s != null && !string.IsNullOrEmpty(s.SkillId))
                {
                    _skills[s.SkillId]    = s;
                    _cooldowns[s.SkillId] = 0f;
                }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>True if a minigame session is currently running.</summary>
        public bool IsSessionActive => _activeSession != null;

        /// <summary>Number of actions recorded in the current session (0 if idle).</summary>
        public int SessionActions => _sessionActions;

        /// <summary>Returns the remaining cooldown in seconds for the given skill (0 = ready).</summary>
        public float GetCooldown(string skillId) =>
            _cooldowns.TryGetValue(skillId, out var cd) ? cd : 0f;

        /// <summary>Returns true if the given skill is off-cooldown and no session is running.</summary>
        public bool CanTrigger(string skillId) =>
            _skills.ContainsKey(skillId) && GetCooldown(skillId) <= 0f && !IsSessionActive;

        /// <summary>
        /// Attempts to trigger a skill's minigame.
        /// Returns false if on cooldown or a session is already running.
        /// </summary>
        public bool TryTrigger(string skillId)
        {
            if (!CanTrigger(skillId)) return false;
            if (!_skills.TryGetValue(skillId, out var skill)) return false;

            _activeSession  = skill;
            _sessionActions = 0;
            _cooldowns[skillId] = skill.CooldownSeconds;

            OnMinigameStarted?.Invoke(skill);
            _sessionTimer = StartCoroutine(SessionTimeout(skill));
            _cooldownTicker ??= StartCoroutine(CooldownTick());
            return true;
        }

        /// <summary>
        /// Called by the UI for each player action during a session (e.g. tap, button press).
        /// Ignored if no session is active.
        /// </summary>
        public void RecordAction()
        {
            if (_activeSession == null) return;
            _sessionActions++;
            OnActionRecorded?.Invoke(_activeSession, _sessionActions);
        }

        /// <summary>Manually ends the current session early (used by test helpers or UI close).</summary>
        public void EndSession()
        {
            if (_activeSession == null) return;
            if (_sessionTimer != null) { StopCoroutine(_sessionTimer); _sessionTimer = null; }
            FinalizeSession();
        }

        // ── Internals ─────────────────────────────────────────────────────────────

        private IEnumerator SessionTimeout(ActiveSkillConfigSO skill)
        {
            yield return new WaitForSeconds(skill.MinigameDurationSeconds);
            _sessionTimer = null;
            FinalizeSession();
        }

        private void FinalizeSession()
        {
            var skill   = _activeSession;
            int actions = _sessionActions;
            _activeSession  = null;
            _sessionActions = 0;

            long reward = CalculateReward(skill, actions);
            _economy?.AddResources(reward);
            OnMinigameEnded?.Invoke(skill, reward);
        }

        private static long CalculateReward(ActiveSkillConfigSO skill, int actions)
        {
            float mult = 1f + (actions * skill.PerActionBonus);
            mult = Mathf.Min(mult, skill.MaxRewardMultiplier);
            return (long)Math.Round(skill.BaseGoldReward * mult);
        }

        private IEnumerator CooldownTick()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                bool allZero = true;
                var keys = new List<string>(_cooldowns.Keys);
                foreach (var k in keys)
                {
                    if (_cooldowns[k] > 0f)
                    {
                        _cooldowns[k] = Mathf.Max(0f, _cooldowns[k] - 1f);
                        if (_cooldowns[k] == 0f) OnSkillReady?.Invoke(k);
                        else allZero = false;
                    }
                }
                if (allZero) { _cooldownTicker = null; yield break; }
            }
        }

        private void OnDestroy() => EndSession();

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnMinigameStarted  = null;
            OnActionRecorded   = null;
            OnMinigameEnded    = null;
            OnSkillReady       = null;
        }

        public void InjectCooldownForTesting(string skillId, float seconds) =>
            _cooldowns[skillId] = seconds;

        public long CalculateRewardForTesting(ActiveSkillConfigSO skill, int actions) =>
            CalculateReward(skill, actions);
#endif
    }
}
