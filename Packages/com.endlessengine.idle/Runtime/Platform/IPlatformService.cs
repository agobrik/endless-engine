using System;
using System.Collections.Generic;

namespace EndlessEngine.Platform
{
    /// <summary>
    /// Platform abstraction layer for features that differ across distribution platforms.
    ///
    /// Implementations:
    ///   SteamPlatformService  — wraps ISteamService (Steam / PC)
    ///   NullPlatformService   — no-op (itch.io, review builds, tests)
    ///   (v1.4+) EpicPlatformService, XboxPlatformService, etc.
    ///
    /// Bootstrap wiring:
    ///   PlatformServiceLocator.Set(new SteamPlatformService(steamService));
    ///   // Then access via PlatformServiceLocator.Current anywhere.
    ///
    /// All methods are null-safe. Unavailable features return false/empty/null.
    /// </summary>
    public interface IPlatformService
    {
        /// <summary>Human-readable platform name for logging ("Steam", "Epic", "Null").</summary>
        string PlatformName { get; }

        /// <summary>True when the platform SDK is initialised and available.</summary>
        bool IsAvailable { get; }

        // ── Achievements ──────────────────────────────────────────────────────────

        /// <summary>Unlocks a platform achievement by its platform-specific API name.</summary>
        void UnlockAchievement(string apiName);

        /// <summary>Returns true if the achievement is already unlocked.</summary>
        bool IsAchievementUnlocked(string apiName);

        // ── Cloud Saves ───────────────────────────────────────────────────────────

        /// <summary>True if the platform supports cloud save sync.</summary>
        bool SupportsCloudSave { get; }

        /// <summary>
        /// Writes bytes to platform cloud storage under <paramref name="filename"/>.
        /// Fires <paramref name="onComplete"/> with success flag.
        /// </summary>
        void CloudSaveWrite(string filename, byte[] data, Action<bool> onComplete = null);

        /// <summary>
        /// Reads bytes from platform cloud storage.
        /// Fires <paramref name="onComplete"/> with bytes (null on failure or missing).
        /// </summary>
        void CloudSaveRead(string filename, Action<byte[]> onComplete);

        // ── Leaderboards ──────────────────────────────────────────────────────────

        /// <summary>True if the platform supports leaderboards.</summary>
        bool SupportsLeaderboards { get; }

        /// <summary>Submits a score. Fires <paramref name="onComplete"/> with new rank (1-based, -1 on failure).</summary>
        void SubmitScore(string boardName, int score, Action<int> onComplete = null);

        /// <summary>Fetches global leaderboard entries.</summary>
        void FetchLeaderboard(string boardName, int count, Action<List<PlatformLeaderboardEntry>> onComplete);

        // ── Stats ─────────────────────────────────────────────────────────────────

        void SetStatInt(string name, int value);
        void SetStatFloat(string name, float value);
        int  GetStatInt(string name);
        void StoreStats();

        // ── Overlay / UI ──────────────────────────────────────────────────────────

        /// <summary>Opens the platform overlay for the given URL (store page, friend invite, etc.).</summary>
        void OpenOverlayURL(string url);
    }

    public class PlatformLeaderboardEntry
    {
        public string DisplayName;
        public int    Score;
        public int    Rank;
    }
}
