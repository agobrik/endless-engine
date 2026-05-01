using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Milestone;
using EndlessEngine.Steam;

namespace EndlessEngine.Platform
{
    /// <summary>
    /// Platform-agnostic achievement bridge.
    /// Wires MilestoneTracker.OnMilestoneCompleted → IPlatformService.UnlockAchievement().
    ///
    /// Replaces SteamAchievementBridge when using the platform abstraction layer.
    /// Uses PlatformServiceLocator.Current — no direct Steam dependency.
    ///
    /// Mapping strategy: MilestoneConfigSO.MilestoneId → platform achievement API name.
    /// The inspector override table handles name mismatches (length limits, casing, etc.).
    ///
    /// Bootstrap wiring:
    ///   // 1. Set platform service before bridge activates
    ///   PlatformServiceLocator.Set(new SteamPlatformService(steamService));
    ///   // 2. Add PlatformAchievementBridge to Bootstrap GameObject — OnEnable auto-subscribes.
    /// </summary>
    public class PlatformAchievementBridge : MonoBehaviour, IAchievementBridge
    {
        [Tooltip("Maps engine MilestoneId → platform achievement API name. " +
                 "Leave empty to use MilestoneId directly.")]
        [SerializeField] private List<AchievementMapping> _mappings = new List<AchievementMapping>();

        private Dictionary<string, string> _mappingLookup;

        private void Awake()
        {
            _mappingLookup = new Dictionary<string, string>(_mappings.Count);
            foreach (var m in _mappings)
                if (!string.IsNullOrEmpty(m.MilestoneId) && !string.IsNullOrEmpty(m.PlatformApiName))
                    _mappingLookup[m.MilestoneId] = m.PlatformApiName;
        }

        private void OnEnable()  => MilestoneTracker.OnMilestoneCompleted += OnMilestoneCompleted;
        private void OnDisable() => MilestoneTracker.OnMilestoneCompleted -= OnMilestoneCompleted;

        public void OnMilestoneCompleted(MilestoneConfigSO milestone)
        {
            if (milestone == null) return;

            string apiName = _mappingLookup != null && _mappingLookup.TryGetValue(milestone.MilestoneId, out var mapped)
                ? mapped
                : milestone.MilestoneId;

            if (string.IsNullOrEmpty(apiName)) return;

            PlatformServiceLocator.Current.UnlockAchievement(apiName);
        }

        [System.Serializable]
        public class AchievementMapping
        {
            public string MilestoneId;
            public string PlatformApiName;
        }
    }
}
