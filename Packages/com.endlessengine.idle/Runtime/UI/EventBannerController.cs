using System;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Events;
using EndlessEngine.Config;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Displays an event banner at the top of the HUD when calendar events are active.
    /// Shows event name, countdown timer, income multiplier, and research speed multiplier.
    ///
    /// Attach to a UIDocument whose Source Asset is EventBannerOverlay.uxml.
    /// Wire EventService in Inspector. Update() drives the countdown timer.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class EventBannerController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private EventService _eventService;

        // ── UI Elements ──────────────────────────────────────────────────────────

        private VisualElement _root;
        private Label         _eventNameLabel;
        private Label         _timerLabel;
        private Label         _incomeBonusLabel;
        private Label         _researchBonusLabel;

        // ── State ────────────────────────────────────────────────────────────────

        private EventScheduleConfigSO _displayedEvent;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            var doc     = GetComponent<UIDocument>();
            var docRoot = doc.rootVisualElement;

            _root               = docRoot.Q<VisualElement>("event-banner-root");
            _eventNameLabel     = docRoot.Q<Label>("event-name-label");
            _timerLabel         = docRoot.Q<Label>("event-timer-label");
            _incomeBonusLabel   = docRoot.Q<Label>("income-bonus-label");
            _researchBonusLabel = docRoot.Q<Label>("research-bonus-label");

            EventService.OnEventActivated   += OnEventActivated;
            EventService.OnEventDeactivated += OnEventDeactivated;

            RefreshBanner();
        }

        private void OnDisable()
        {
            EventService.OnEventActivated   -= OnEventActivated;
            EventService.OnEventDeactivated -= OnEventDeactivated;
        }

        private void Update()
        {
            if (_displayedEvent == null || _timerLabel == null) return;

            DateTime now    = DateTime.Now;
            int      dayEnd = _displayedEvent.EndDayOfYear;
            DateTime endOfYear = new DateTime(now.Year, 12, 31);
            int      daysLeft  = dayEnd - now.DayOfYear;

            if (daysLeft < 0) daysLeft += 365;

            _timerLabel.text = daysLeft <= 0
                ? "Ends today"
                : $"Ends in: {daysLeft}d";
        }

        // ── Event handlers ───────────────────────────────────────────────────────

        private void OnEventActivated(EventScheduleConfigSO cfg)
        {
            RefreshBanner();
        }

        private void OnEventDeactivated(EventScheduleConfigSO cfg)
        {
            RefreshBanner();
        }

        // ── Banner refresh ───────────────────────────────────────────────────────

        private void RefreshBanner()
        {
            if (_eventService == null || _root == null) return;

            var activeEvents = _eventService.GetActiveEvents();
            if (activeEvents == null || activeEvents.Count == 0)
            {
                _root.style.display = DisplayStyle.None;
                _displayedEvent     = null;
                return;
            }

            _displayedEvent = activeEvents[0];
            _root.style.display = DisplayStyle.Flex;

            if (_eventNameLabel != null)
                _eventNameLabel.text = _displayedEvent.EventId;

            float incomeMulti   = _eventService.GetCombinedIncomeMultiplier();
            float researchMulti = _eventService.GetCombinedResearchMultiplier();

            if (_incomeBonusLabel != null)
            {
                bool hasBonus = incomeMulti > 1f;
                _incomeBonusLabel.text             = hasBonus ? $"+{(incomeMulti - 1f) * 100f:F0}% Income" : "";
                _incomeBonusLabel.style.display    = hasBonus ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_researchBonusLabel != null)
            {
                bool hasBonus = researchMulti > 1f;
                _researchBonusLabel.text          = hasBonus ? $"+{(researchMulti - 1f) * 100f:F0}% Research" : "";
                _researchBonusLabel.style.display = hasBonus ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
