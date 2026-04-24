using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.UI
{
    /// <summary>
    /// A queued notification item.
    /// </summary>
    public class NotificationItem
    {
        public NotificationConfigSO Config;
        public string               Text;
        public float                Duration;
        public NotificationPriority Priority;
    }

    /// <summary>
    /// Queue-based notification service. Enqueued notifications are displayed
    /// one at a time in priority order. Each notification auto-dismisses after
    /// its duration.
    ///
    /// Consumers subscribe to OnNotificationShown / OnNotificationDismissed.
    /// UI controllers (NotificationToastController) render the active notification.
    ///
    /// Usage:
    ///   NotificationService.Instance.Enqueue(config);         // uses config defaults
    ///   NotificationService.Instance.Enqueue(config, "text"); // override text
    /// </summary>
    public class NotificationService : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────────

        public static NotificationService Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when a notification becomes the active (visible) one.</summary>
        public static event Action<NotificationItem> OnNotificationShown;

        /// <summary>Fires when the active notification is dismissed (expired or dismissed manually).</summary>
        public static event Action<NotificationItem> OnNotificationDismissed;

        // ── Config ────────────────────────────────────────────────────────────────

        [Tooltip("Default display duration in seconds when not specified by the config.")]
        [SerializeField] private float _defaultDuration = 3f;

        [Tooltip("Maximum items in the queue. Oldest low-priority items are dropped if exceeded.")]
        [SerializeField] private int _maxQueueSize = 20;

        // ── State ─────────────────────────────────────────────────────────────────

        private readonly List<NotificationItem> _queue = new List<NotificationItem>();
        private NotificationItem _active;
        private bool _processing;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Enqueues a notification. Immediately shows if nothing is currently active.
        /// </summary>
        public void Enqueue(NotificationConfigSO config, string overrideText = null)
        {
            if (config == null) return;

            var item = new NotificationItem
            {
                Config   = config,
                Text     = !string.IsNullOrEmpty(overrideText) ? overrideText : config.DefaultText,
                Duration = config.Duration > 0f ? config.Duration : _defaultDuration,
                Priority = config.Priority
            };

            // Drop low-priority items if queue is full
            if (_queue.Count >= _maxQueueSize)
            {
                int dropIdx = FindLowestPriorityIndex();
                if (dropIdx >= 0 && _queue[dropIdx].Priority < item.Priority)
                    _queue.RemoveAt(dropIdx);
                else
                    return; // can't fit
            }

            // Insert in priority order (highest first)
            int insertAt = _queue.Count;
            for (int i = 0; i < _queue.Count; i++)
            {
                if (item.Priority > _queue[i].Priority)
                {
                    insertAt = i;
                    break;
                }
            }
            _queue.Insert(insertAt, item);

            if (!_processing)
                StartCoroutine(ProcessQueue());
        }

        /// <summary>Clears the entire queue and dismisses the active notification.</summary>
        public void Clear()
        {
            _queue.Clear();
            StopAllCoroutines();
            if (_active != null)
            {
                var prev = _active;
                _active = null;
                _processing = false;
                OnNotificationDismissed?.Invoke(prev);
            }
        }

        /// <summary>Number of notifications currently queued (not including active).</summary>
        public int QueueCount => _queue.Count;

        /// <summary>The currently displayed notification, or null.</summary>
        public NotificationItem Active => _active;

        // ── Queue processing ──────────────────────────────────────────────────────

        private IEnumerator ProcessQueue()
        {
            _processing = true;

            while (_queue.Count > 0)
            {
                _active = _queue[0];
                _queue.RemoveAt(0);

                OnNotificationShown?.Invoke(_active);
                yield return new WaitForSeconds(_active.Duration);

                var prev = _active;
                _active = null;
                OnNotificationDismissed?.Invoke(prev);
            }

            _processing = false;
        }

        private int FindLowestPriorityIndex()
        {
            if (_queue.Count == 0) return -1;
            int lowest = 0;
            for (int i = 1; i < _queue.Count; i++)
                if (_queue[i].Priority < _queue[lowest].Priority) lowest = i;
            return lowest;
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnNotificationShown     = null;
            OnNotificationDismissed = null;
        }

        public void SetDefaultDurationForTesting(float dur) => _defaultDuration = dur;
#endif
    }
}
