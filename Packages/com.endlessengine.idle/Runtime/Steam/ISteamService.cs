using System;
using System.Collections.Generic;

namespace EndlessEngine.Steam
{
    /// <summary>
    /// Engine-level Steam facade. Decouples gameplay code from Steamworks.NET so that
    /// non-Steam builds (itch.io, review builds) compile without the SDK.
    ///
    /// Bootstrap wiring:
    ///   SteamService.Initialize(appId);   // in Bootstrap, before any Steam calls
    ///
    /// All methods are null-safe. When Steam is unavailable (editor, non-Steam build,
    /// Steam not running), calls silently no-op — callers need no guard checks.
    ///
    /// Implementation: SteamService (Steamworks.NET backend)
    /// Stub: NullSteamService (no-op, used in tests and non-Steam builds)
    /// </summary>
    public interface ISteamService
    {
        /// <summary>True when Steam is initialised and the overlay is available.</summary>
        bool IsAvailable { get; }

        // ── Achievements ──────────────────────────────────────────────────────────

        /// <summary>Unlocks a Steam achievement by API name. No-op if already unlocked.</summary>
        void UnlockAchievement(string apiName);

        /// <summary>Returns true if the achievement has been unlocked.</summary>
        bool IsAchievementUnlocked(string apiName);

        // ── Stats ─────────────────────────────────────────────────────────────────

        /// <summary>Sets an integer stat on Steam Stats backend.</summary>
        void SetStatInt(string name, int value);

        /// <summary>Sets a float stat on Steam Stats backend.</summary>
        void SetStatFloat(string name, float value);

        /// <summary>Gets a previously set integer stat. Returns 0 if unavailable.</summary>
        int GetStatInt(string name);

        /// <summary>Flushes pending achievement/stat changes to Steam. Call after bulk updates.</summary>
        void StoreStats();

        // ── Cloud Saves ───────────────────────────────────────────────────────────

        /// <summary>
        /// Writes <paramref name="data"/> to Steam Remote Storage under <paramref name="filename"/>.
        /// Fires <paramref name="onComplete"/> with success flag when done.
        /// </summary>
        void CloudSaveWrite(string filename, byte[] data, Action<bool> onComplete = null);

        /// <summary>
        /// Reads a file from Steam Remote Storage.
        /// Fires <paramref name="onComplete"/> with the raw bytes (null on failure).
        /// </summary>
        void CloudSaveRead(string filename, Action<byte[]> onComplete);

        /// <summary>Returns true if the given filename exists in Steam Remote Storage.</summary>
        bool CloudSaveExists(string filename);

        // ── Leaderboards ──────────────────────────────────────────────────────────

        /// <summary>
        /// Submits a score to the named Steam leaderboard.
        /// <paramref name="onComplete"/> receives the new rank (1-based, -1 on failure).
        /// </summary>
        void SubmitLeaderboardScore(string leaderboardName, int score, Action<int> onComplete = null);

        /// <summary>
        /// Downloads global leaderboard entries around the player's rank.
        /// <paramref name="onComplete"/> receives ordered entries (empty on failure).
        /// </summary>
        void FetchLeaderboard(string leaderboardName, int entryCount, Action<List<SteamLeaderboardEntry>> onComplete);
    }

    /// <summary>One entry from a Steam leaderboard download.</summary>
    public class SteamLeaderboardEntry
    {
        public string DisplayName;
        public int    Score;
        public int    Rank;
    }
}
