using UnityEngine;
using UnityEngine.Events;

namespace EndlessEngine.Tutorial
{
    /// <summary>
    /// Configures one step in a tutorial sequence.
    ///
    /// A step has:
    ///   - A trigger condition: when to show this step
    ///   - Display data: what to show (text, highlight target, arrow direction)
    ///   - A completion condition: when the player has "done" the step
    ///   - Optional: block input outside the highlighted element while step is active
    ///
    /// Steps are processed by TutorialService in sequence index order.
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialStep", menuName = "Endless Engine/Tutorial/Tutorial Step")]
    public class TutorialStepConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID. Never change after release — used in save data.")]
        public string StepId;

        [Tooltip("Zero-based display order within the tutorial sequence.")]
        public int SequenceIndex;

        [Header("Display")]
        [Tooltip("Main instructional text shown to the player.")]
        [TextArea(2, 5)] public string InstructionText;

        [Tooltip("Optional secondary text (smaller, below instruction).")]
        [TextArea(1, 3)] public string SubText;

        [Tooltip("Optional image to display alongside instruction.")]
        public Sprite IllustrationSprite;

        [Header("Highlight")]
        [Tooltip("UI element name to highlight. Leave empty for no highlight.")]
        public string HighlightTargetName;

        [Tooltip("If true, all UI input outside the highlighted element is blocked.")]
        public bool BlockInputOutsideHighlight;

        [Header("Completion")]
        [Tooltip("How the player completes this step.")]
        public TutorialCompletionType CompletionType = TutorialCompletionType.TapAnywhere;

        [Tooltip("Event name that completes this step (for EventFired completion type).")]
        public string CompletionEventName;

        [Header("Skip")]
        [Tooltip("If true, this step can be skipped by the player.")]
        public bool Skippable = true;
    }

    public enum TutorialCompletionType
    {
        /// <summary>Any tap/click anywhere advances the step.</summary>
        TapAnywhere,
        /// <summary>Player must tap the highlighted element.</summary>
        TapHighlight,
        /// <summary>Completed when a named game event fires (registered via TutorialService).</summary>
        EventFired,
        /// <summary>Completed programmatically by calling TutorialService.CompleteCurrentStep().</summary>
        Manual,
    }
}
