using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Platform;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Milestone
{
    /// <summary>
    /// Platform-agnostic achievement tracker.
    /// Wires MilestoneTracker.OnMilestoneCompleted → IPlatformService.UnlockAchievement().
    ///
    /// Unlike PlatformAchievementBridge (which reads from PlatformServiceLocator),
    /// AchievementTracker accepts an injected IPlatformService — making it fully
    /// testable without a live platform SDK.
    ///
    /// Bootstrap wiring:
    ///   achievementTracker.Initialize(platformService, mappings);
    ///   // mappings: optional MilestoneId → platform API name overrides
    /// </summary>
    public class AchievementTracker : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when an achievement unlock is dispatched to the platform.</summary>
        public static event Action<string> OnAchievementUnlocked;

        // ── State ─────────────────────────────────────────────────────────────────

        private IPlatformService            _platform;
        private Dictionary<string, string>  _mappings = new();
        private bool                        _initialized;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Initialize with a platform service and optional milestone → API name mappings.
        /// Pass null as platform to use NullPlatformService (safe no-op).
        /// </summary>
        public void Initialize(IPlatformService platform,
            IEnumerable<(string milestoneId, string apiName)> mappings = null)
        {
            _platform    = platform ?? NullPlatformService.Instance;
            _mappings    = new Dictionary<string, string>();
            _initialized = true;

            if (mappings != null)
                foreach (var (id, api) in mappings)
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(api))
                        _mappings[id] = api;
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()  => MilestoneTracker.OnMilestoneCompleted += HandleMilestoneCompleted;
        private void OnDisable() => MilestoneTracker.OnMilestoneCompleted -= HandleMilestoneCompleted;

        private void OnDestroy() => ClearSubscribersForTesting();

        // ── Handler ───────────────────────────────────────────────────────────────

        private void HandleMilestoneCompleted(MilestoneConfigSO milestone)
        {
            if (!_initialized || milestone == null) return;
            if (!_platform.IsAvailable) return;

            string apiName = _mappings.TryGetValue(milestone.MilestoneId, out var mapped)
                ? mapped
                : milestone.MilestoneId;

            if (string.IsNullOrEmpty(apiName)) return;

            _platform.UnlockAchievement(apiName);
            OnAchievementUnlocked?.Invoke(apiName);
            Debug.Log($"[AchievementTracker] Unlocked: {apiName} (milestone: {milestone.MilestoneId})");
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting() => OnAchievementUnlocked = null;

        /// <summary>Forces an achievement unlock dispatch without waiting for milestone event.</summary>
        public void ForceUnlockForTesting(string milestoneId)
        {
            var fake = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            fake.MilestoneId = milestoneId;
            HandleMilestoneCompleted(fake);
            UnityEngine.Object.DestroyImmediate(fake);
        }
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }
}
