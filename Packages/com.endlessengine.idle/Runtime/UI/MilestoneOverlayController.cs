using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;
using EndlessEngine.Milestone;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Controls the achievement toast popup and milestone log panel.
    /// Listens to MilestoneTracker.OnMilestoneCompleted and drives animations.
    ///
    /// Attach to a UIDocument whose Source Asset is MilestoneOverlay.uxml.
    /// Wire MilestoneDatabaseSO in Inspector so the log can show all milestones.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MilestoneOverlayController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private MilestoneDatabaseSO _database;
        [SerializeField] private MilestoneTracker    _milestoneTracker;

        [Tooltip("Seconds the toast stays visible before sliding out.")]
        [SerializeField] private float _toastDuration = 3.5f;

        // ── UI Elements ───────────────────────────────────────────────────────────

        private VisualElement _root;
        private VisualElement _toast;
        private Label         _toastName;
        private Label         _toastDescription;
        private Label         _toastReward;
        private VisualElement _toastIcon;

        private VisualElement _logPanel;
        private VisualElement _logList;
        private Button        _logCloseBtn;

        private bool _toastBusy;
        private MilestoneConfigSO _queuedMilestone;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            _root = doc.rootVisualElement.Q<VisualElement>("milestone-overlay-root");

            _toast            = _root.Q<VisualElement>("achievement-toast");
            _toastName        = _root.Q<Label>("toast-name");
            _toastDescription = _root.Q<Label>("toast-description");
            _toastReward      = _root.Q<Label>("toast-reward");
            _toastIcon        = _root.Q<VisualElement>("toast-icon");

            _logPanel    = _root.Q<VisualElement>("milestone-log-panel");
            _logList     = _root.Q<VisualElement>("log-list");
            _logCloseBtn = _root.Q<Button>("log-close-button");

            _logCloseBtn?.RegisterCallback<ClickEvent>(_ => HideLog());

            _root.style.display = DisplayStyle.Flex;

            MilestoneTracker.OnMilestoneCompleted += HandleMilestoneCompleted;
        }

        private void OnDisable()
        {
            MilestoneTracker.OnMilestoneCompleted -= HandleMilestoneCompleted;
        }

        // ── Toast ────────────────────────────────────────────────────────────────

        private void HandleMilestoneCompleted(MilestoneConfigSO milestone)
        {
            if (!milestone.ShowPopup) return;

            if (_toastBusy)
            {
                _queuedMilestone = milestone;
                return;
            }

            StartCoroutine(ShowToast(milestone));
        }

        private IEnumerator ShowToast(MilestoneConfigSO milestone)
        {
            _toastBusy = true;

            _toastName.text        = milestone.DisplayName;
            _toastDescription.text = milestone.Description;
            _toastReward.text      = BuildRewardText(milestone);

            if (milestone.Icon != null)
                _toastIcon.style.backgroundImage = new StyleBackground(milestone.Icon);

            _toast.AddToClassList("toast-visible");
            yield return new WaitForSeconds(_toastDuration);
            _toast.RemoveFromClassList("toast-visible");

            // Wait for slide-out animation (0.35s)
            yield return new WaitForSeconds(0.4f);

            _toastBusy = false;

            if (_queuedMilestone != null)
            {
                var queued = _queuedMilestone;
                _queuedMilestone = null;
                StartCoroutine(ShowToast(queued));
            }
        }

        private string BuildRewardText(MilestoneConfigSO milestone)
        {
            if (milestone.GoldReward > 0)
                return $"+{milestone.GoldReward:N0} Gold";

            if (!string.IsNullOrEmpty(milestone.RewardCurrencyId) && milestone.RewardCurrencyAmount > 0)
                return $"+{milestone.RewardCurrencyAmount:N0} {milestone.RewardCurrencyId}";

            return "";
        }

        // ── Milestone Log ────────────────────────────────────────────────────────

        /// <summary>Opens the full achievement log panel. Call from a HUD button.</summary>
        public void ShowLog()
        {
            RefreshLog();
            _logPanel.style.display = DisplayStyle.Flex;
        }

        /// <summary>Hides the achievement log panel.</summary>
        public void HideLog()
        {
            _logPanel.style.display = DisplayStyle.None;
        }

        private void RefreshLog()
        {
            _logList.Clear();
            if (_database == null || _milestoneTracker == null) return;

            foreach (var milestone in _database.Milestones)
            {
                if (milestone == null) continue;
                bool done = _milestoneTracker.IsCompleted(milestone.MilestoneId);
                _logList.Add(BuildLogEntry(milestone, done));
            }
        }

        private VisualElement BuildLogEntry(MilestoneConfigSO milestone, bool completed)
        {
            var entry = new VisualElement();
            entry.AddToClassList("log-entry");
            if (completed) entry.AddToClassList("completed");

            var icon = new VisualElement();
            icon.AddToClassList("log-entry-icon");
            if (completed) icon.AddToClassList("completed");
            if (milestone.Icon != null)
                icon.style.backgroundImage = new StyleBackground(milestone.Icon);

            var textCol = new VisualElement();
            textCol.AddToClassList("log-entry-text");

            var name = new Label(milestone.DisplayName);
            name.AddToClassList("log-entry-name");
            if (completed) name.AddToClassList("completed");

            var desc = new Label(milestone.Description);
            desc.AddToClassList("log-entry-desc");

            textCol.Add(name);
            textCol.Add(desc);

            entry.Add(icon);
            entry.Add(textCol);

            if (completed)
            {
                var check = new Label("✓");
                check.AddToClassList("log-entry-check");
                entry.Add(check);
            }

            return entry;
        }
    }
}
