using System;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Notification
{
    /// <summary>
    /// Engine-level push notification facade.
    /// Routes Schedule/Cancel calls to the active IPushNotificationProvider.
    ///
    /// Bootstrap wiring:
    ///   PushNotificationService.SetProvider(new IosPushNotificationProvider());
    ///
    /// Defaults to NullPushNotificationProvider — no notifications until provider is set.
    /// All methods are null-safe and never throw.
    /// </summary>
    public static class PushNotificationService
    {
        private static IPushNotificationProvider _provider = NullPushNotificationProvider.Instance;

        /// <summary>Replaces the active provider. Call from Bootstrap after platform init.</summary>
        public static void SetProvider(IPushNotificationProvider provider)
        {
            _provider = provider ?? NullPushNotificationProvider.Instance;
        }

        /// <summary>Schedules a local notification with the given delay.</summary>
        public static void Schedule(string notificationId, string title, string body, TimeSpan delay)
        {
            try { _provider.Schedule(notificationId, title, body, delay); }
            catch (Exception ex)
            { Debug.LogError($"[PushNotificationService] Schedule '{notificationId}' failed: {ex.Message}"); }
        }

        /// <summary>Cancels a previously scheduled notification.</summary>
        public static void Cancel(string notificationId)
        {
            try { _provider.Cancel(notificationId); }
            catch (Exception ex)
            { Debug.LogError($"[PushNotificationService] Cancel '{notificationId}' failed: {ex.Message}"); }
        }

        /// <summary>Cancels all pending notifications.</summary>
        public static void CancelAll()
        {
            try { _provider.CancelAll(); }
            catch (Exception ex)
            { Debug.LogError($"[PushNotificationService] CancelAll failed: {ex.Message}"); }
        }

        /// <summary>Returns true if the active provider is the no-op null provider.</summary>
        public static bool IsNullProvider => _provider is NullPushNotificationProvider;
    }
}
