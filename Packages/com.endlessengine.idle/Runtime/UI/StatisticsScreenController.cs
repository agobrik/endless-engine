using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Statistics;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Displays the statistics overlay (all-time counters + last-run summary).
    ///
    /// Wiring:
    ///   Assign _statisticsService in Inspector (or set via code after Initialize).
    ///   Call Show() to open, Hide() to close.
    ///   SetLastRunSummary() populates the "LAST RUN" tab.
    ///
    /// Subscribe to OnClosed to return to the previous screen.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class StatisticsScreenController : MonoBehaviour
    {
        [SerializeField] private StatisticsService _statisticsService;

        public static event Action OnClosed;

        private UIDocument  _doc;
        private VisualElement _root;

        // All-time tab
        private VisualElement _allTimeList;
        private VisualElement _allTimeScroll;

        // Last run tab
        private VisualElement _lastRunPanel;
        private Label _lblDuration, _lblGold, _lblKills, _lblMaxWave;
        private Label _lblPrestige, _lblUpgrades, _lblCascade, _lblIncome;

        // Filter tabs
        private Button _tabAll, _tabLastRun;

        private RunSummaryData _lastRun;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _doc  = GetComponent<UIDocument>();
            _root = _doc.rootVisualElement.Q("stats-root");

            _allTimeList   = _root.Q("all-time-list");
            _allTimeScroll = _root.Q("all-time-scroll");
            _lastRunPanel  = _root.Q("last-run-panel");

            _lblDuration = _root.Q<Label>("run-duration");
            _lblGold     = _root.Q<Label>("run-gold");
            _lblKills    = _root.Q<Label>("run-kills");
            _lblMaxWave  = _root.Q<Label>("run-max-wave");
            _lblPrestige = _root.Q<Label>("run-prestige");
            _lblUpgrades = _root.Q<Label>("run-upgrades");
            _lblCascade  = _root.Q<Label>("run-cascade");
            _lblIncome   = _root.Q<Label>("run-income");

            _tabAll     = _root.Q<Button>("tab-all");
            _tabLastRun = _root.Q<Button>("tab-lastrun");

            _tabAll.clicked     += () => ShowTab(allTime: true);
            _tabLastRun.clicked += () => ShowTab(allTime: false);
            _root.Q<Button>("close-button").clicked += Hide;

            Hide();
        }

        private void OnEnable()
        {
            StatisticsService.OnStatChanged += HandleStatChanged;
        }

        private void OnDisable()
        {
            StatisticsService.OnStatChanged -= HandleStatChanged;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void Show()
        {
            _root.style.display = DisplayStyle.Flex;
            ShowTab(allTime: true);
            RefreshAllTime();
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            OnClosed?.Invoke();
        }

        /// <summary>Stores the last-run summary for display in the "LAST RUN" tab.</summary>
        public void SetLastRunSummary(RunSummaryData summary)
        {
            _lastRun = summary;
            if (summary == null) return;

            _lblDuration.text = FormatDuration(summary.DurationSeconds);
            _lblGold.text     = FormatLong(summary.GoldEarned);
            _lblKills.text    = summary.KillCount.ToString("N0");
            _lblMaxWave.text  = summary.MaxWave.ToString();
            _lblPrestige.text = summary.PrestigePerformed ? $"Yes (total {summary.PrestigeCountAtStart + 1})" : "No";
            _lblUpgrades.text = summary.UpgradesAccepted.ToString();
            _lblCascade.text  = $"{summary.CascadeMultiplier:F2}×";
            _lblIncome.text   = $"{FormatDouble(summary.FinalIncomeRate)}/s";
        }

        // ── Tab display ───────────────────────────────────────────────────────────

        private void ShowTab(bool allTime)
        {
            _allTimeScroll.style.display = allTime ? DisplayStyle.Flex : DisplayStyle.None;
            _lastRunPanel.style.display  = allTime ? DisplayStyle.None : DisplayStyle.Flex;

            SetTabActive(_tabAll,     allTime);
            SetTabActive(_tabLastRun, !allTime);
        }

        private static void SetTabActive(Button tab, bool active)
        {
            if (active)
            {
                if (!tab.ClassListContains("filter-tab-active"))
                    tab.AddToClassList("filter-tab-active");
            }
            else
            {
                tab.RemoveFromClassList("filter-tab-active");
            }
        }

        // ── All-time list ─────────────────────────────────────────────────────────

        private void RefreshAllTime()
        {
            _allTimeList.Clear();
            if (_statisticsService == null) return;

            foreach (var kv in _statisticsService.GetAll())
                _allTimeList.Add(BuildRow(
                    _statisticsService.GetDisplayName(kv.Key),
                    FormatDouble(kv.Value)));
        }

        private static VisualElement BuildRow(string name, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("stat-entry");
            var lbl = new Label(name); lbl.AddToClassList("stat-entry-name");
            var val = new Label(value); val.AddToClassList("stat-entry-value");
            row.Add(lbl); row.Add(val);
            return row;
        }

        private void HandleStatChanged(string statId, double newValue)
        {
            if (_root.style.display == DisplayStyle.None) return;
            RefreshAllTime();
        }

        // ── Formatting ────────────────────────────────────────────────────────────

        private static string FormatLong(long v)
        {
            if (v >= 1_000_000_000) return $"{v / 1_000_000_000.0:F2}B";
            if (v >= 1_000_000)     return $"{v / 1_000_000.0:F2}M";
            if (v >= 1_000)         return $"{v / 1_000.0:F1}K";
            return v.ToString();
        }

        private static string FormatDouble(double v)
        {
            if (v >= 1_000_000_000) return $"{v / 1_000_000_000.0:F2}B";
            if (v >= 1_000_000)     return $"{v / 1_000_000.0:F2}M";
            if (v >= 1_000)         return $"{v / 1_000.0:F1}K";
            return $"{v:F0}";
        }

        private static string FormatDuration(float seconds)
        {
            if (seconds < 0) return "—";
            int s = (int)seconds;
            int h = s / 3600; s %= 3600;
            int m = s / 60;   s %= 60;
            if (h > 0) return $"{h}h {m}m {s}s";
            if (m > 0) return $"{m}m {s}s";
            return $"{s}s";
        }
    }
}
