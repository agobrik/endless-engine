using System;
using UnityEngine;
using EndlessEngine.Telemetry;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Modules
{
    /// <summary>
    /// Tracks daily login streaks and session timestamps.
    /// Persists via PlayerPrefs (streak data is meta-game, not run-state).
    ///
    /// A "streak day" increments when the player logs in on a calendar day
    /// different from their last login (UTC). Missing a day resets to 1.
    ///
    /// Usage: Call RecordLogin() on game start, after save is loaded.
    /// Subscribe to OnStreakUpdated to drive UI and reward triggers.
    /// </summary>
    public class SessionService : MonoBehaviour
    {
        private const string KeyLastLogin   = "ee_last_login_utc";
        private const string KeyStreakCount = "ee_streak_count";

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires after RecordLogin() processes the login.
        /// Parameters: (newStreak, isNewDay).
        /// isNewDay=true means this is the first login of a new calendar day.
        /// </summary>
        public static event Action<int, bool> OnStreakUpdated;

        // ── State ─────────────────────────────────────────────────────────────────

        /// <summary>Current streak in days. 1 = first day or streak broken and reset.</summary>
        public int StreakCount { get; private set; }

        /// <summary>UTC date of the most recent recorded login.</summary>
        public DateTime LastLoginDate { get; private set; }

        /// <summary>True if today's login has already been recorded this session.</summary>
        public bool LoginRecordedThisSession { get; private set; }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Records a login. Call once per session after save data is loaded.
        /// Idempotent within a single calendar day.
        /// </summary>
        public void RecordLogin()
        {
            Load();

            DateTime today = DateTime.UtcNow.Date;

            if (today == LastLoginDate)
            {
                LoginRecordedThisSession = true;
                OnStreakUpdated?.Invoke(StreakCount, false);
                return;
            }

            bool isConsecutive = (today - LastLoginDate).TotalDays <= 1.0;
            StreakCount  = isConsecutive ? StreakCount + 1 : 1;
            LastLoginDate = today;
            LoginRecordedThisSession = true;

            Save();
            TelemetryService.Track(TelemetryEvents.SessionStarted,
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { "streak", StreakCount },
                    { "is_new_day", true },
                    { "consecutive", isConsecutive }
                });
            OnStreakUpdated?.Invoke(StreakCount, true);
            Debug.Log($"[SessionService] Login recorded. Streak={StreakCount}, Consecutive={isConsecutive}");
        }

        /// <summary>Resets the streak to 0 and clears persistence. For testing or admin use.</summary>
        public void ResetStreak()
        {
            StreakCount   = 0;
            LastLoginDate = DateTime.MinValue;
            Save();
            OnStreakUpdated?.Invoke(StreakCount, false);
        }

        // ── Persistence ───────────────────────────────────────────────────────────

        private void Load()
        {
            StreakCount = PlayerPrefs.GetInt(KeyStreakCount, 0);

            string raw = PlayerPrefs.GetString(KeyLastLogin, string.Empty);
            LastLoginDate = DateTime.TryParse(raw, out var dt) ? dt.Date : DateTime.MinValue;
        }

        private void Save()
        {
            PlayerPrefs.SetInt(KeyStreakCount, StreakCount);
            PlayerPrefs.SetString(KeyLastLogin, LastLoginDate.ToString("yyyy-MM-dd"));
            PlayerPrefs.Save();
        }

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Inject state for testing without touching PlayerPrefs.</summary>
        public void InjectForTesting(int streak, DateTime lastLogin)
        {
            StreakCount   = streak;
            LastLoginDate = lastLogin.Date;
        }

        public static void ClearSubscribersForTesting() => OnStreakUpdated = null;
#endif
    }
}
