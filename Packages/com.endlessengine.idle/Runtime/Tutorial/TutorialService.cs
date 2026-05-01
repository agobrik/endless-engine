using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Telemetry;

namespace EndlessEngine.Tutorial
{
    /// <summary>
    /// Engine-level tutorial/onboarding service.
    ///
    /// Manages a linear sequence of TutorialStepConfigSO entries.
    /// Persists completed step IDs so that returning players skip already-seen steps.
    /// Fires events so UI code can show/hide tutorial overlays without coupling to
    /// game logic.
    ///
    /// Bootstrap wiring:
    ///   tutorialService.Initialize(steps, saveService);
    ///   tutorialService.Begin(); // starts from first incomplete step
    ///
    /// Game events → TutorialService:
    ///   tutorialService.NotifyEvent("generator_purchased");
    ///   // If current step completion type is EventFired with that name, advances.
    ///
    /// UI integration:
    ///   TutorialService.OnStepStarted  → show overlay with step data
    ///   TutorialService.OnStepCompleted → hide/animate overlay
    ///   TutorialService.OnTutorialFinished → show "tutorial done" or nothing
    /// </summary>
    public class TutorialService : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.Milestone + 10;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when a tutorial step becomes active.</summary>
        public static event Action<TutorialStepConfigSO> OnStepStarted;

        /// <summary>Fires when the active step is completed.</summary>
        public static event Action<TutorialStepConfigSO> OnStepCompleted;

        /// <summary>Fires when all steps have been completed or tutorial is skipped.</summary>
        public static event Action OnTutorialFinished;

        // ── State ─────────────────────────────────────────────────────────────────

        private List<TutorialStepConfigSO> _steps;
        private readonly HashSet<string>   _completedStepIds = new HashSet<string>();
        private TutorialStepConfigSO       _currentStep;
        private bool                       _active;
        private bool                       _initialized;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        public void Initialize(TutorialStepConfigSO[] steps, SaveService saveService)
        {
            _steps = new List<TutorialStepConfigSO>(steps ?? Array.Empty<TutorialStepConfigSO>());
            _steps.Sort((a, b) => a.SequenceIndex.CompareTo(b.SequenceIndex));
            saveService?.RegisterStateProvider(this);
            _initialized = true;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Starts the tutorial from the first incomplete step.</summary>
        public void Begin()
        {
            if (!_initialized || _active) return;
            _active = true;
            AdvanceToNextStep();
        }

        /// <summary>Skips the entire tutorial.</summary>
        public void Skip()
        {
            if (!_active) return;
            _active      = false;
            _currentStep = null;
            OnTutorialFinished?.Invoke();
        }

        /// <summary>
        /// Programmatically completes the current step.
        /// Use for Manual completion type, or when game code detects the relevant action.
        /// </summary>
        public void CompleteCurrentStep()
        {
            if (_currentStep == null) return;
            MarkStepCompleted(_currentStep);
        }

        /// <summary>
        /// Notifies the service that a named game event occurred.
        /// If the current step uses EventFired completion and the names match, advances.
        /// </summary>
        public void NotifyEvent(string eventName)
        {
            if (_currentStep == null) return;
            if (_currentStep.CompletionType == TutorialCompletionType.EventFired &&
                _currentStep.CompletionEventName == eventName)
                MarkStepCompleted(_currentStep);
        }

        /// <summary>Handles a tap/click anywhere — advances TapAnywhere steps.</summary>
        public void NotifyTap()
        {
            if (_currentStep == null) return;
            if (_currentStep.CompletionType == TutorialCompletionType.TapAnywhere)
                MarkStepCompleted(_currentStep);
        }

        /// <summary>Handles a tap on the highlighted element — advances TapHighlight steps.</summary>
        public void NotifyHighlightTapped()
        {
            if (_currentStep == null) return;
            if (_currentStep.CompletionType == TutorialCompletionType.TapHighlight ||
                _currentStep.CompletionType == TutorialCompletionType.TapAnywhere)
                MarkStepCompleted(_currentStep);
        }

        /// <summary>Currently active tutorial step, or null if not running.</summary>
        public TutorialStepConfigSO CurrentStep => _currentStep;

        /// <summary>True while a tutorial sequence is in progress.</summary>
        public bool IsActive => _active;

        /// <summary>True if all steps have been completed.</summary>
        public bool IsFinished => _completedStepIds.Count >= (_steps?.Count ?? 0);

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.CompletedMilestones ??= new HashSet<string>();
            foreach (var id in _completedStepIds)
                saveData.CompletedMilestones.Add($"tutorial:{id}");
        }

        public void OnAfterLoad(SaveData saveData)
        {
            _completedStepIds.Clear();
            if (saveData.CompletedMilestones == null) return;
            const string prefix = "tutorial:";
            foreach (var entry in saveData.CompletedMilestones)
                if (entry.StartsWith(prefix))
                    _completedStepIds.Add(entry.Substring(prefix.Length));
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void MarkStepCompleted(TutorialStepConfigSO step)
        {
            _completedStepIds.Add(step.StepId);
            OnStepCompleted?.Invoke(step);
            AdvanceToNextStep();
        }

        private void AdvanceToNextStep()
        {
            _currentStep = null;

            if (_steps == null) return;

            foreach (var step in _steps)
            {
                if (step == null) continue;
                if (_completedStepIds.Contains(step.StepId)) continue;
                _currentStep = step;
                OnStepStarted?.Invoke(step);
                return;
            }

            // All steps done
            _active = false;
            TelemetryService.Track("tutorial.finished");
            OnTutorialFinished?.Invoke();
        }

        private void OnDestroy() => ClearSubscribersForTesting();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnStepStarted      = null;
            OnStepCompleted    = null;
            OnTutorialFinished = null;
        }

        public void ResetForTesting()
        {
            _completedStepIds.Clear();
            _currentStep = null;
            _active      = false;
        }

        public bool IsStepCompletedForTesting(string stepId) => _completedStepIds.Contains(stepId);
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }
}
