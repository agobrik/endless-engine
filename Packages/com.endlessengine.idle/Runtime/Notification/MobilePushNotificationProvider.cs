#if UNITY_IOS || UNITY_ANDROID
using System;
using UnityEngine;

#if UNITY_MOBILE_NOTIFICATIONS
using Unity.Notifications;
#if UNITY_IOS
using Unity.Notifications.iOS;
#elif UNITY_ANDROID
using Unity.Notifications.Android;
#endif
#endif

namespace EndlessEngine.Notification
{
    /// <summary>
    /// Mobile platform push notification provider.
    /// Bridges PushNotificationService to Unity Mobile Notifications package.
    ///
    /// Requires com.unity.mobile.notifications (Package Manager).
    /// Add "UNITY_MOBILE_NOTIFICATIONS" to Scripting Define Symbols to activate.
    ///
    /// iOS: Uses iOSNotificationCenter.ScheduleLocalNotification().
    /// Android: Uses AndroidNotificationCenter.SendNotificationWithExplicitID().
    ///          Creates a default notification channel on first use.
    ///
    /// Without UNITY_MOBILE_NOTIFICATIONS defined, logs warnings so calls are visible
    /// during development without requiring the package.
    ///
    /// Activate in Bootstrap:
    ///   PushNotificationService.SetProvider(new MobilePushNotificationProvider());
    /// </summary>
    public class MobilePushNotificationProvider : IPushNotificationProvider
    {
#if UNITY_ANDROID && UNITY_MOBILE_NOTIFICATIONS
        private const string ChannelId   = "endless_engine_default";
        private const string ChannelName = "Game Notifications";
        private bool _channelCreated;
#endif

        public void Schedule(string notificationId, string title, string body, TimeSpan delay)
        {
#if UNITY_MOBILE_NOTIFICATIONS
#if UNITY_IOS
            var n = new iOSNotification
            {
                Identifier    = notificationId,
                Title         = title,
                Body          = body,
                ShowInForeground = false,
                Trigger       = new iOSNotificationTimeIntervalTrigger
                {
                    TimeInterval = delay,
                    Repeats      = false
                }
            };
            iOSNotificationCenter.ScheduleLocalNotification(n);
            Debug.Log($"[MobilePush] Scheduled iOS notification '{notificationId}' in {delay.TotalSeconds:F0}s.");

#elif UNITY_ANDROID
            EnsureChannel();

            // Android notification IDs must be int. We hash the string ID for stability.
            int intId = System.Math.Abs(notificationId.GetHashCode());

            var n = new AndroidNotification
            {
                Title          = title,
                Text           = body,
                FireTime       = DateTime.Now + delay,
                SmallIcon      = "icon_0",
                LargeIcon      = "icon_1"
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(n, ChannelId, intId);
            Debug.Log($"[MobilePush] Scheduled Android notification '{notificationId}' (id={intId}) in {delay.TotalSeconds:F0}s.");
#endif
#else
            Debug.LogWarning($"[MobilePush] Schedule called but UNITY_MOBILE_NOTIFICATIONS is not defined. " +
                             $"Notification '{notificationId}' would fire in {delay.TotalSeconds:F0}s.");
#endif
        }

        public void Cancel(string notificationId)
        {
#if UNITY_MOBILE_NOTIFICATIONS
#if UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(notificationId);
            Debug.Log($"[MobilePush] Cancelled iOS notification '{notificationId}'.");

#elif UNITY_ANDROID
            int intId = System.Math.Abs(notificationId.GetHashCode());
            AndroidNotificationCenter.CancelScheduledNotification(intId);
            Debug.Log($"[MobilePush] Cancelled Android notification id={intId} ('{notificationId}').");
#endif
#else
            Debug.LogWarning($"[MobilePush] Cancel called but UNITY_MOBILE_NOTIFICATIONS is not defined. id='{notificationId}'");
#endif
        }

        public void CancelAll()
        {
#if UNITY_MOBILE_NOTIFICATIONS
#if UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            Debug.Log("[MobilePush] Cancelled all iOS notifications.");

#elif UNITY_ANDROID
            AndroidNotificationCenter.CancelAllScheduledNotifications();
            Debug.Log("[MobilePush] Cancelled all Android notifications.");
#endif
#else
            Debug.LogWarning("[MobilePush] CancelAll called but UNITY_MOBILE_NOTIFICATIONS is not defined.");
#endif
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

#if UNITY_ANDROID && UNITY_MOBILE_NOTIFICATIONS
        private void EnsureChannel()
        {
            if (_channelCreated) return;

            var channel = new AndroidNotificationChannel
            {
                Id          = ChannelId,
                Name        = ChannelName,
                Importance  = Importance.Default,
                Description = "Endless Engine offline progress and event reminders."
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);
            _channelCreated = true;
        }
#endif
    }
}
#endif
