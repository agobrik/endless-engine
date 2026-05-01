using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Feedback
{
    /// <summary>
    /// Designer-facing configuration that maps event IDs to audio + VFX + slow-mo parameters.
    ///
    /// Add entries in the Inspector: assign an event ID string (use FeedbackEvent constants),
    /// then assign the SFX clip, VFX prefab, and optional slow-mo parameters.
    ///
    /// FeedbackService.Trigger() looks up entries by ID each call — O(n) on list count.
    /// Typical idle games have ≤ 20 entries, so a Dictionary is unnecessary overhead.
    /// </summary>
    [CreateAssetMenu(fileName = "FeedbackConfig", menuName = "Endless Engine/Config/Feedback")]
    public class FeedbackConfigSO : ScriptableObject
    {
        [SerializeField] private List<FeedbackEntry> _entries = new List<FeedbackEntry>();

        /// <summary>
        /// Returns the first entry matching <paramref name="eventId"/>, or null if not found.
        /// </summary>
        public FeedbackEntry FindEntry(string eventId)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].EventId == eventId) return _entries[i];
            return null;
        }

#if UNITY_EDITOR
        /// <summary>All entries (Inspector / test access).</summary>
        public IReadOnlyList<FeedbackEntry> Entries => _entries;
#endif
    }

    /// <summary>One feedback event binding: ID → SFX + VFX + slow-mo.</summary>
    [Serializable]
    public class FeedbackEntry
    {
        [Tooltip("Must match a FeedbackEvent constant. Case-sensitive.")]
        public string EventId;

        [Header("SFX")]
        public AudioClip SFXClip;
        [Range(0f, 1f)] public float SFXVolume = 0.8f;
        [Range(0.5f, 2f)] public float SFXPitch = 1f;

        [Header("VFX")]
        [Tooltip("Instantiated at the trigger position. Leave null to skip VFX.")]
        public GameObject VFXPrefab;

        [Tooltip("Auto-destroy VFX after this many seconds. 0 = never (manage manually).")]
        [Min(0f)] public float VFXDuration = 2f;

        [Header("Slow-Mo")]
        [Tooltip("Duration in real seconds. 0 = disabled.")]
        [Min(0f)] public float SlowMoDuration = 0f;

        [Tooltip("Time scale factor during slow-mo (0.01 – 1.0). 1 = no slowdown.")]
        [Range(0.01f, 1f)] public float SlowMoFactor = 0.3f;
    }
}
