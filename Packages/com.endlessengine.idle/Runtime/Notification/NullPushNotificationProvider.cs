using System;

namespace EndlessEngine.Notification
{
    /// <summary>No-op push notification provider. Default until a platform provider is wired.</summary>
    public sealed class NullPushNotificationProvider : IPushNotificationProvider
    {
        public static readonly NullPushNotificationProvider Instance = new();
        private NullPushNotificationProvider() { }

        public void Schedule(string notificationId, string title, string body, TimeSpan delay) { }
        public void Cancel(string notificationId) { }
        public void CancelAll() { }
    }
}
