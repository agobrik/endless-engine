using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Milestone;
using EndlessEngine.Prestige;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Auto-triggers notifications in response to game events.
    /// Attach to same GO as NotificationService.
    ///
    /// Wires:
    ///   MilestoneTracker.OnMilestoneCompleted → "Achievement unlocked!" notification
    ///   PrestigeStateManager.OnPrestigeComplete → "Prestige!" notification
    ///   AscensionStateManager.OnAscensionComplete → "Ascension!" notification
    /// </summary>
    public class NotificationAutoTrigger : MonoBehaviour
    {
        [Header("Notification Configs")]
        [SerializeField] private NotificationConfigSO _milestoneNotification;
        [SerializeField] private NotificationConfigSO _prestigeNotification;
        [SerializeField] private NotificationConfigSO _ascensionNotification;

        private NotificationService _service;

        private void OnEnable()
        {
            _service = GetComponent<NotificationService>();

            MilestoneTracker.OnMilestoneCompleted       += HandleMilestone;
            PrestigeStateManager.OnPrestigeComplete      += HandlePrestige;
            AscensionStateManager.OnAscensionComplete    += HandleAscension;
        }

        private void OnDisable()
        {
            MilestoneTracker.OnMilestoneCompleted       -= HandleMilestone;
            PrestigeStateManager.OnPrestigeComplete      -= HandlePrestige;
            AscensionStateManager.OnAscensionComplete    -= HandleAscension;
        }

        private void HandleMilestone(MilestoneConfigSO milestone)
        {
            if (_milestoneNotification == null || _service == null) return;
            _service.Enqueue(_milestoneNotification, $"Achievement: {milestone.DisplayName}");
        }

        private void HandlePrestige(int count, float mult)
        {
            if (_prestigeNotification == null || _service == null) return;
            _service.Enqueue(_prestigeNotification, $"Prestige ×{count}! Multiplier: {mult:F2}×");
        }

        private void HandleAscension(int layer, int count, float cascade)
        {
            if (_ascensionNotification == null || _service == null) return;
            _service.Enqueue(_ascensionNotification, $"Ascension L{layer} ×{count}! Cascade: {cascade:F2}×");
        }
    }
}
