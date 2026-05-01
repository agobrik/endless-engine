using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Milestone;

namespace EndlessEngine.Steam
{
    /// <summary>
    /// Wires MilestoneTracker.OnMilestoneCompleted → Steam achievements via ISteamService.
    ///
    /// Mapping strategy: MilestoneConfigSO.MilestoneId is used as the Steam API name
    /// by default. Override specific IDs using the inspector mapping table when Steam
    /// API names differ from engine milestone IDs (e.g. Steam has a length limit).
    ///
    /// Bootstrap wiring:
    ///   bridge.Initialize(steamService);
    ///   // Call after MilestoneTracker is initialized — OnEnable subscribes the event.
    ///
    /// The bridge is a MonoBehaviour so OnEnable / OnDisable manage subscription lifetime
    /// correctly when the parent GameObject is activated/deactivated.
    /// </summary>
    public class SteamAchievementBridge : MonoBehaviour, IAchievementBridge
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Tooltip("Override table: maps engine MilestoneId → Steam API achievement name. " +
                 "Leave empty to use MilestoneId directly as the Steam API name.")]
        [SerializeField] private List<AchievementMapping> _mappings = new List<AchievementMapping>();

        // ── State ─────────────────────────────────────────────────────────────────

        private ISteamService                   _steam;
        private Dictionary<string, string>      _mappingLookup;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        public void Initialize(ISteamService steam)
        {
            _steam = steam ?? NullSteamService.Instance;

            _mappingLookup = new Dictionary<string, string>(_mappings.Count);
            foreach (var m in _mappings)
                if (!string.IsNullOrEmpty(m.MilestoneId) && !string.IsNullOrEmpty(m.SteamApiName))
                    _mappingLookup[m.MilestoneId] = m.SteamApiName;
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()  => MilestoneTracker.OnMilestoneCompleted += OnMilestoneCompleted;
        private void OnDisable() => MilestoneTracker.OnMilestoneCompleted -= OnMilestoneCompleted;

        // ── IAchievementBridge ────────────────────────────────────────────────────

        public void OnMilestoneCompleted(MilestoneConfigSO milestone)
        {
            if (milestone == null || _steam == null) return;

            string apiName = ResolveSteamApiName(milestone.MilestoneId);
            if (string.IsNullOrEmpty(apiName)) return;

            _steam.UnlockAchievement(apiName);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string ResolveSteamApiName(string milestoneId)
        {
            if (_mappingLookup != null && _mappingLookup.TryGetValue(milestoneId, out var mapped))
                return mapped;

            // Fall back to using MilestoneId directly as the Steam API name.
            return milestoneId;
        }

        // ── Inspector type ────────────────────────────────────────────────────────

        [System.Serializable]
        public class AchievementMapping
        {
            [Tooltip("Engine MilestoneId (from MilestoneConfigSO).")]
            public string MilestoneId;

            [Tooltip("Steam achievement API name (from Steamworks partner dashboard).")]
            public string SteamApiName;
        }
    }
}
