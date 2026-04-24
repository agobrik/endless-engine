using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Challenge;
using EndlessEngine.Config;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Displays the challenge selection overlay.
    ///
    /// Shows all available ChallengeConfigSO entries in a list.
    /// Clicking an entry populates the preview panel (modifiers, victory condition, reward).
    /// "ACTIVATE" sets the challenge in ChallengeService.
    /// "NO CHALLENGE" cancels any active challenge.
    ///
    /// Wiring:
    ///   Assign _challengeService and _availableChallenges in Inspector.
    ///   Call Show() from the main menu or pre-run screen.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ChallengeScreenController : MonoBehaviour
    {
        [SerializeField] private ChallengeService      _challengeService;
        [SerializeField] private ChallengeConfigSO[]   _availableChallenges;

        public static event Action OnClosed;

        private UIDocument    _doc;
        private VisualElement _root;

        private VisualElement _list;
        private Label _previewTitle, _previewDesc, _previewCondition, _previewReward;
        private VisualElement _modifierList;
        private Button _activateBtn, _cancelBtn;

        private ChallengeConfigSO _selected;
        private readonly List<VisualElement> _entryElements = new List<VisualElement>();

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _doc  = GetComponent<UIDocument>();
            _root = _doc.rootVisualElement.Q("challenge-root");

            _list           = _root.Q("challenge-list");
            _previewTitle   = _root.Q<Label>("preview-title");
            _previewDesc    = _root.Q<Label>("preview-description");
            _previewCondition = _root.Q<Label>("preview-condition");
            _previewReward  = _root.Q<Label>("preview-reward");
            _modifierList   = _root.Q("modifier-list");

            _activateBtn = _root.Q<Button>("activate-button");
            _cancelBtn   = _root.Q<Button>("cancel-button");

            _activateBtn.clicked += OnActivateClicked;
            _cancelBtn.clicked   += OnCancelClicked;
            _root.Q<Button>("close-button").clicked += Hide;

            Hide();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void Show()
        {
            _root.style.display = DisplayStyle.Flex;
            BuildList();
            ClearPreview();
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            OnClosed?.Invoke();
        }

        // ── List building ─────────────────────────────────────────────────────────

        private void BuildList()
        {
            _list.Clear();
            _entryElements.Clear();

            if (_availableChallenges == null) return;

            foreach (var cfg in _availableChallenges)
            {
                if (cfg == null) continue;
                var entry = BuildEntry(cfg);
                _list.Add(entry);
                _entryElements.Add(entry);
            }
        }

        private VisualElement BuildEntry(ChallengeConfigSO cfg)
        {
            var entry = new VisualElement();
            entry.AddToClassList("challenge-entry");

            var name = new Label(cfg.DisplayName); name.AddToClassList("challenge-entry-name");
            var reward = new Label($"×{cfg.RewardMultiplier:F1} reward"); reward.AddToClassList("challenge-entry-reward");
            entry.Add(name); entry.Add(reward);

            entry.RegisterCallback<ClickEvent>(_ => SelectChallenge(cfg, entry));
            return entry;
        }

        private void SelectChallenge(ChallengeConfigSO cfg, VisualElement entryEl)
        {
            _selected = cfg;

            // Update selection highlight
            foreach (var e in _entryElements) e.RemoveFromClassList("challenge-entry-selected");
            entryEl.AddToClassList("challenge-entry-selected");

            PopulatePreview(cfg);
            _activateBtn.SetEnabled(true);
        }

        // ── Preview panel ─────────────────────────────────────────────────────────

        private void PopulatePreview(ChallengeConfigSO cfg)
        {
            _previewTitle.text = cfg.DisplayName;
            _previewDesc.text  = cfg.Description;

            // Modifiers
            _modifierList.Clear();
            if (cfg.Modifiers != null)
            {
                foreach (var m in cfg.Modifiers)
                {
                    var lbl = new Label(FormatModifier(m));
                    lbl.AddToClassList("modifier-entry");
                    _modifierList.Add(lbl);
                }
            }

            // Victory condition
            if (cfg.RequiredWave > 0 && cfg.TimeLimitSeconds > 0)
                _previewCondition.text = $"Reach wave {cfg.RequiredWave} within {(int)cfg.TimeLimitSeconds}s";
            else if (cfg.RequiredWave > 0)
                _previewCondition.text = $"Reach wave {cfg.RequiredWave}";
            else if (cfg.TimeLimitSeconds > 0)
                _previewCondition.text = $"Survive for {(int)cfg.TimeLimitSeconds}s";
            else
                _previewCondition.text = "Survive as long as possible";

            _previewReward.text = $"×{cfg.RewardMultiplier:F1} gold"
                + (cfg.BonusSkillPoints > 0 ? $" + {cfg.BonusSkillPoints} skill pts" : "");
        }

        private void ClearPreview()
        {
            _selected = null;
            _previewTitle.text     = "Select a challenge";
            _previewDesc.text      = "";
            _previewCondition.text = "—";
            _previewReward.text    = "—";
            _modifierList.Clear();
            _activateBtn.SetEnabled(false);
        }

        // ── Button handlers ───────────────────────────────────────────────────────

        private void OnActivateClicked()
        {
            if (_selected == null || _challengeService == null) return;
            _challengeService.ActivateChallenge(_selected);
            Hide();
        }

        private void OnCancelClicked()
        {
            _challengeService?.CancelChallenge();
            Hide();
        }

        // ── Formatting ────────────────────────────────────────────────────────────

        private static string FormatModifier(ChallengeModifier m) => m.Type switch
        {
            ChallengeModifierType.StatOverride       => $"• {m.TargetId} = {m.Value:F1}",
            ChallengeModifierType.StatMultiplier     => $"• {m.TargetId} ×{m.Value:F2}",
            ChallengeModifierType.DisableSystem      => $"• {m.TargetId} DISABLED",
            ChallengeModifierType.TimeLimit          => $"• Time limit: {(int)m.Value}s",
            ChallengeModifierType.EnemyDifficultyScale => $"• Enemy difficulty ×{m.Value:F2}",
            _                                        => $"• {m.TargetId}: {m.Value}"
        };
    }
}
