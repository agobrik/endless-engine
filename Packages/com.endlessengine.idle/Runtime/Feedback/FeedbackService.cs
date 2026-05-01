using UnityEngine;
using EndlessEngine.Audio;
using EndlessEngine.Flow;

namespace EndlessEngine.Feedback
{
    /// <summary>
    /// Unified trigger point for audio, VFX, and slow-motion "juice" effects.
    ///
    /// Gameplay code fires FeedbackService.Trigger(FeedbackEvent) — never calls
    /// AudioService, VFXController, or TimeScaleController directly. This keeps
    /// effect configuration centralised and lets designers tweak what each event
    /// looks/sounds like without touching call sites.
    ///
    /// Bootstrap wiring:
    ///   feedbackService.Initialize(audioService, timeScaleController, feedbackConfig);
    ///
    /// Each FeedbackEvent entry in FeedbackConfigSO binds a clip, VFX prefab,
    /// volume, and optional slow-mo parameters. FeedbackService reads the config
    /// and delegates to the relevant services.
    /// </summary>
    public class FeedbackService : MonoBehaviour
    {
        // ── Dependencies ──────────────────────────────────────────────────────────

        private AudioService        _audio;
        private TimeScaleController _timeScale;
        private FeedbackConfigSO    _config;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        public void Initialize(AudioService audio, TimeScaleController timeScale, FeedbackConfigSO config)
        {
            _audio     = audio;
            _timeScale = timeScale;
            _config    = config;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Triggers the configured audio + VFX + slow-mo response for a named event.
        /// No-op if the event is not found in the active FeedbackConfigSO.
        /// </summary>
        public void Trigger(string eventId)
        {
            if (_config == null) return;
            var entry = _config.FindEntry(eventId);
            if (entry == null) return;

            TriggerEntry(entry, Vector3.zero, null);
        }

        /// <summary>
        /// Triggers feedback at a world position (for positional VFX).
        /// </summary>
        public void Trigger(string eventId, Vector3 worldPosition)
        {
            if (_config == null) return;
            var entry = _config.FindEntry(eventId);
            if (entry == null) return;

            TriggerEntry(entry, worldPosition, null);
        }

        /// <summary>
        /// Triggers feedback parented to a Transform (follows a moving target).
        /// </summary>
        public void Trigger(string eventId, Transform parent)
        {
            if (_config == null) return;
            var entry = _config.FindEntry(eventId);
            if (entry == null) return;

            TriggerEntry(entry, parent ? parent.position : Vector3.zero, parent);
        }

        // ── Trigger Execution ─────────────────────────────────────────────────────

        private void TriggerEntry(FeedbackEntry entry, Vector3 position, Transform parent)
        {
            // SFX
            if (_audio != null && entry.SFXClip != null)
                _audio.PlaySFX(entry.SFXClip, entry.SFXVolume, entry.SFXPitch);

            // VFX
            if (entry.VFXPrefab != null)
            {
                GameObject vfx = parent != null
                    ? Instantiate(entry.VFXPrefab, parent.position, Quaternion.identity, parent)
                    : Instantiate(entry.VFXPrefab, position, Quaternion.identity);

                if (entry.VFXDuration > 0f)
                    Destroy(vfx, entry.VFXDuration);
            }

            // Slow-mo
            if (_timeScale != null && entry.SlowMoDuration > 0f)
                _timeScale.SlowMo(entry.SlowMoDuration, entry.SlowMoFactor);
        }
    }

    // ── FeedbackEvent constants ───────────────────────────────────────────────────

    /// <summary>Well-known feedback event IDs. Use these to avoid magic strings.</summary>
    public static class FeedbackEvent
    {
        public const string PrestigeTriggered    = "prestige_triggered";
        public const string WaveComplete         = "wave_complete";
        public const string EnemyKilledBoss      = "enemy_killed_boss";
        public const string UpgradePurchased     = "upgrade_purchased";
        public const string MilestoneReached     = "milestone_reached";
        public const string GeneratorUnlocked    = "generator_unlocked";
        public const string StreakBonus          = "streak_bonus";
    }
}
