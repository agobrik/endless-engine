using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.Events
{
    /// <summary>
    /// Manages time-gated event scheduling. Checks current UTC date against
    /// EventScheduleConfigSO windows and fires state change events.
    ///
    /// EventService does not distribute rewards directly — it fires
    /// OnEventActivated and OnEventDeactivated so other systems can react.
    ///
    /// Integrate with TickEngine.OnTick or a per-minute coroutine for live checks.
    /// In-editor, call CheckSchedule() manually for testing.
    ///
    /// Time source is overrideable for testing via InjectDateTimeForTesting.
    /// </summary>
    public class EventService : MonoBehaviour
    {
        // ── Static events ─────────────────────────────────────────────────────────

        public static event Action<EventScheduleConfigSO> OnEventActivated;
        public static event Action<EventScheduleConfigSO> OnEventDeactivated;

        // ── State ─────────────────────────────────────────────────────────────────

        private readonly Dictionary<string, EventScheduleConfigSO> _configs = new Dictionary<string, EventScheduleConfigSO>();
        private readonly HashSet<string>                            _active  = new HashSet<string>();
        private          Func<DateTime>                             _nowProvider = () => DateTime.UtcNow;

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(EventScheduleConfigSO[] events)
        {
            _configs.Clear();
            _active.Clear();

            if (events != null)
                foreach (var e in events)
                    if (e != null && !string.IsNullOrEmpty(e.EventId))
                        _configs[e.EventId] = e;

            CheckSchedule();
        }

        // ── Schedule Check ────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluate all event schedules against current time.
        /// Call from TickEngine.OnTick or a timer.
        /// </summary>
        public void CheckSchedule()
        {
            var now = _nowProvider();

            foreach (var kv in _configs)
            {
                bool shouldBeActive = IsEventActive(kv.Value, now);
                bool wasActive      = _active.Contains(kv.Key);

                if (shouldBeActive && !wasActive)
                {
                    _active.Add(kv.Key);
                    OnEventActivated?.Invoke(kv.Value);
                }
                else if (!shouldBeActive && wasActive)
                {
                    _active.Remove(kv.Key);
                    OnEventDeactivated?.Invoke(kv.Value);
                }
            }
        }

        // ── Active Event Queries ──────────────────────────────────────────────────

        public bool IsActive(string eventId) => _active.Contains(eventId);

        public IReadOnlyList<EventScheduleConfigSO> GetActiveEvents()
            => _configs.Values.Where(e => _active.Contains(e.EventId)).ToList();

        /// <summary>
        /// Combined income multiplier from all currently active events.
        /// </summary>
        public float GetCombinedIncomeMultiplier()
        {
            float mult = 1f;
            foreach (var eventId in _active)
                if (_configs.TryGetValue(eventId, out var cfg))
                    mult *= cfg.IncomeMultiplier;
            return mult;
        }

        /// <summary>
        /// Combined research speed multiplier from all currently active events.
        /// </summary>
        public float GetCombinedResearchMultiplier()
        {
            float mult = 1f;
            foreach (var eventId in _active)
                if (_configs.TryGetValue(eventId, out var cfg))
                    mult *= cfg.ResearchSpeedMultiplier;
            return mult;
        }

        // ── Internal Schedule Logic ───────────────────────────────────────────────

        private static bool IsEventActive(EventScheduleConfigSO config, DateTime now)
        {
            int dayOfYear = now.DayOfYear;

            // Calendar window check
            bool inCalendarWindow = config.StartDayOfYear <= config.EndDayOfYear
                ? dayOfYear >= config.StartDayOfYear && dayOfYear <= config.EndDayOfYear
                : dayOfYear >= config.StartDayOfYear || dayOfYear <= config.EndDayOfYear; // wraps year boundary

            if (!inCalendarWindow) return false;

            // If no rotation, active whenever in calendar window
            if (config.RotationCycleHours <= 0) return true;

            // Rotation check: active only during the window within each cycle
            double hourOfYear  = (dayOfYear - 1) * 24.0 + now.Hour + now.Minute / 60.0;
            double cycleOffset = hourOfYear % config.RotationCycleHours;
            return config.RotationDurationHours <= 0 || cycleOffset < config.RotationDurationHours;
        }

        private void OnDestroy() => ClearSubscribersForTesting();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnEventActivated   = null;
            OnEventDeactivated = null;
        }

        public void InjectDateTimeForTesting(Func<DateTime> provider)
            => _nowProvider = provider;

        public HashSet<string> GetActiveIdsForTesting() => _active;
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }
}
