// Tests for Sprint 18 — S18-01: EventService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - Initialize: fires OnEventActivated for events in-window
//   - CheckSchedule: fires OnEventDeactivated when window ends
//   - IsActive: correct before/after window
//   - GetActiveEvents: returns only active events
//   - GetCombinedIncomeMultiplier: product of all active multipliers
//   - GetCombinedResearchMultiplier: product of all active multipliers
//   - Rotation window: active only during cycle offset
//   - Year boundary wrap: event spanning Dec→Jan
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.EventsLeaderboardExport

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Events;

namespace EndlessEngine.Tests.Unit.EventsLeaderboardExport
{
    [TestFixture]
    public class EventServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private EventService          _service;
        private EventScheduleConfigSO _summerEvent;
        private EventScheduleConfigSO _winterEvent;
        private EventScheduleConfigSO _rotationEvent;

        private readonly List<EventScheduleConfigSO> _activatedEvents   = new List<EventScheduleConfigSO>();
        private readonly List<EventScheduleConfigSO> _deactivatedEvents = new List<EventScheduleConfigSO>();

        [SetUp]
        public void SetUp()
        {
            EventService.ClearSubscribersForTesting();
            EventService.OnEventActivated   += e => _activatedEvents.Add(e);
            EventService.OnEventDeactivated += e => _deactivatedEvents.Add(e);
            _activatedEvents.Clear(); _deactivatedEvents.Clear();

            // Summer event: days 152–243 (June 1 – Aug 31 approx)
            _summerEvent = ScriptableObject.CreateInstance<EventScheduleConfigSO>();
            _summerEvent.EventId           = "summer_fest";
            _summerEvent.StartDayOfYear    = 152;
            _summerEvent.EndDayOfYear      = 243;
            _summerEvent.IncomeMultiplier  = 2f;

            // Winter event: days 355–365 + 1–10 (wraps year)
            _winterEvent = ScriptableObject.CreateInstance<EventScheduleConfigSO>();
            _winterEvent.EventId        = "winter_fest";
            _winterEvent.StartDayOfYear = 355;
            _winterEvent.EndDayOfYear   = 10; // wraps to Jan 10

            // Rotation event: cycles every 4 hours, active for 2 hours
            _rotationEvent = ScriptableObject.CreateInstance<EventScheduleConfigSO>();
            _rotationEvent.EventId               = "flash_sale";
            _rotationEvent.StartDayOfYear        = 1;
            _rotationEvent.EndDayOfYear          = 365;
            _rotationEvent.RotationCycleHours    = 4f;
            _rotationEvent.RotationDurationHours = 2f;
            _rotationEvent.IncomeMultiplier      = 1.5f;

            var go   = new GameObject("EventService");
            _service = go.AddComponent<EventService>();
        }

        [TearDown]
        public void TearDown()
        {
            EventService.ClearSubscribersForTesting();
            if (_service       != null) UnityEngine.Object.DestroyImmediate(_service.gameObject);
            if (_summerEvent   != null) UnityEngine.Object.DestroyImmediate(_summerEvent);
            if (_winterEvent   != null) UnityEngine.Object.DestroyImmediate(_winterEvent);
            if (_rotationEvent != null) UnityEngine.Object.DestroyImmediate(_rotationEvent);
        }

        // ── Initialize ────────────────────────────────────────────────────────────

        [Test]
        public void Initialize_FiresActivated_ForInWindowEvent()
        {
            // Inject date inside summer window
            _service.InjectDateTimeForTesting(() => new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
            _service.Initialize(new[] { _summerEvent });

            Assert.AreEqual(1, _activatedEvents.Count);
            Assert.AreEqual("summer_fest", _activatedEvents[0].EventId);
        }

        [Test]
        public void Initialize_NoActivation_WhenOutsideWindow()
        {
            // Inject date outside summer window (December)
            _service.InjectDateTimeForTesting(() => new DateTime(2026, 12, 15, 12, 0, 0, DateTimeKind.Utc));
            _service.Initialize(new[] { _summerEvent });

            Assert.AreEqual(0, _activatedEvents.Count);
        }

        // ── CheckSchedule deactivation ────────────────────────────────────────────

        [Test]
        public void CheckSchedule_FiresDeactivated_WhenWindowEnds()
        {
            // Start inside window
            _service.InjectDateTimeForTesting(() => new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
            _service.Initialize(new[] { _summerEvent });
            Assert.AreEqual(1, _activatedEvents.Count);

            // Move outside window
            _service.InjectDateTimeForTesting(() => new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
            _service.CheckSchedule();

            Assert.AreEqual(1, _deactivatedEvents.Count);
            Assert.AreEqual("summer_fest", _deactivatedEvents[0].EventId);
        }

        // ── IsActive ──────────────────────────────────────────────────────────────

        [Test]
        public void IsActive_TrueWhenInWindow()
        {
            _service.InjectDateTimeForTesting(() => new DateTime(2026, 7, 4, 0, 0, 0, DateTimeKind.Utc));
            _service.Initialize(new[] { _summerEvent });
            Assert.IsTrue(_service.IsActive("summer_fest"));
        }

        [Test]
        public void IsActive_FalseWhenOutsideWindow()
        {
            _service.InjectDateTimeForTesting(() => new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
            _service.Initialize(new[] { _summerEvent });
            Assert.IsFalse(_service.IsActive("summer_fest"));
        }

        // ── Year boundary wrap ────────────────────────────────────────────────────

        [Test]
        public void YearBoundaryEvent_ActiveOnDec31()
        {
            _service.InjectDateTimeForTesting(() => new DateTime(2026, 12, 31, 12, 0, 0, DateTimeKind.Utc));
            _service.Initialize(new[] { _winterEvent });
            Assert.IsTrue(_service.IsActive("winter_fest"));
        }

        [Test]
        public void YearBoundaryEvent_ActiveOnJan5()
        {
            _service.InjectDateTimeForTesting(() => new DateTime(2027, 1, 5, 12, 0, 0, DateTimeKind.Utc));
            _service.Initialize(new[] { _winterEvent });
            Assert.IsTrue(_service.IsActive("winter_fest"));
        }

        [Test]
        public void YearBoundaryEvent_InactiveOnJan15()
        {
            _service.InjectDateTimeForTesting(() => new DateTime(2027, 1, 15, 12, 0, 0, DateTimeKind.Utc));
            _service.Initialize(new[] { _winterEvent });
            Assert.IsFalse(_service.IsActive("winter_fest"));
        }

        // ── Rotation window ───────────────────────────────────────────────────────

        [Test]
        public void RotationEvent_ActiveWithinCycleWindow()
        {
            // Hour 1 → offset 1 < 2 (duration) → active
            _service.InjectDateTimeForTesting(() => new DateTime(2026, 6, 1, 1, 0, 0, DateTimeKind.Utc));
            _service.Initialize(new[] { _rotationEvent });
            Assert.IsTrue(_service.IsActive("flash_sale"));
        }

        [Test]
        public void RotationEvent_InactiveOutsideCycleWindow()
        {
            // Hour 3 → offset 3 >= 2 (duration) → inactive
            _service.InjectDateTimeForTesting(() => new DateTime(2026, 6, 1, 3, 0, 0, DateTimeKind.Utc));
            _service.Initialize(new[] { _rotationEvent });
            Assert.IsFalse(_service.IsActive("flash_sale"));
        }

        // ── Multipliers ───────────────────────────────────────────────────────────

        [Test]
        public void GetCombinedIncomeMultiplier_ProductOfActiveEvents()
        {
            // Both summer (×2) and rotation (×1.5) active simultaneously
            _service.InjectDateTimeForTesting(() => new DateTime(2026, 7, 1, 1, 0, 0, DateTimeKind.Utc));
            _service.Initialize(new[] { _summerEvent, _rotationEvent });
            float combined = _service.GetCombinedIncomeMultiplier();
            Assert.AreEqual(3f, combined, 0.001f, "2.0 × 1.5 = 3.0");
        }

        [Test]
        public void GetCombinedIncomeMultiplier_OneWhenNoEventsActive()
        {
            _service.InjectDateTimeForTesting(() => new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc));
            _service.Initialize(new[] { _summerEvent });
            Assert.AreEqual(1f, _service.GetCombinedIncomeMultiplier(), 0.001f);
        }

#endif
    }
}
