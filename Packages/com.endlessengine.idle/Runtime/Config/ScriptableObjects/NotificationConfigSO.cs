using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>Priority level for notification queue ordering.</summary>
    public enum NotificationPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Template for a reusable notification type.
    /// NotificationService.Enqueue(config, overrideText) creates instances from this.
    ///
    /// Create via: Tools → Endless Engine → Create Notification Config
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Notification/Notification Config",
        fileName = "NotificationConfig")]
    public class NotificationConfigSO : ScriptableObject
    {
        [Tooltip("Unique notification type ID.")]
        public string NotificationId = "";

        [Tooltip("Default display text (may be overridden at runtime).")]
        public string DefaultText = "";

        [Tooltip("Optional icon shown in the notification toast.")]
        public Sprite Icon;

        [Tooltip("Notification priority in the queue.")]
        public NotificationPriority Priority = NotificationPriority.Normal;

        [Tooltip("Seconds before this notification auto-dismisses. 0 = use NotificationService default.")]
        public float Duration = 0f;
    }
}
