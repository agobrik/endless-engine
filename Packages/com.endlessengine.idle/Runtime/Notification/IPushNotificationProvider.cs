using System;

namespace EndlessEngine.Notification
{
    /// <summary>
    /// Abstraction for platform push notification scheduling.
    /// Implement per-platform (iOS UNUserNotificationCenter, Android AlarmManager).
    ///
    /// Bootstrap wiring:
    ///   PushNotificationService.SetProvider(new IosPushNotificationProvider());
    ///
    /// Default provider is NullPushNotificationProvider — no notifications sent.
    /// </summary>
    public interface IPushNotificationProvider
    {
        /// <summary>
        /// Schedules a local notification to fire after a delay.
        /// Implementations must be idempotent for the same notificationId.
        /// </summary>
        void Schedule(string notificationId, string title, string body, TimeSpan delay);

        /// <summary>Cancels a previously scheduled notification by ID.</summary>
        void Cancel(string notificationId);

        /// <summary>Cancels all pending notifications scheduled by this provider.</summary>
        void CancelAll();
    }

    /// <summary>Standard notification ID constants.</summary>
    public static class NotificationIds
    {
        public const string OfflineReturned  = "ee_offline_returned";
        public const string DailyQuestReset  = "ee_daily_quest_reset";
        public const string WeeklyQuestReset = "ee_weekly_quest_reset";
        public const string ResearchComplete = "ee_research_complete";
        public const string EnergyFull       = "ee_energy_full";
    }
}
