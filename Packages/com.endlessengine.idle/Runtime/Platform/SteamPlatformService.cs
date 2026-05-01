using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Steam;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Platform
{
    /// <summary>
    /// IPlatformService backed by ISteamService (Steamworks.NET).
    ///
    /// Bootstrap wiring:
    ///   var steam = GetComponent<SteamService>();
    ///   steam.Initialize(appId);
    ///   PlatformServiceLocator.Set(new SteamPlatformService(steam));
    ///
    /// Falls back to NullPlatformService behaviour when Steam is unavailable.
    /// </summary>
    public class SteamPlatformService : IPlatformService
    {
        private readonly ISteamService _steam;

        public SteamPlatformService(ISteamService steam)
        {
            _steam = steam ?? NullSteamService.Instance;
        }

        public string PlatformName         => "Steam";
        public bool   IsAvailable          => _steam.IsAvailable;
        public bool   SupportsCloudSave    => _steam.IsAvailable;
        public bool   SupportsLeaderboards => _steam.IsAvailable;

        // ── Achievements ──────────────────────────────────────────────────────────

        public void UnlockAchievement(string apiName)           => _steam.UnlockAchievement(apiName);
        public bool IsAchievementUnlocked(string apiName)       => _steam.IsAchievementUnlocked(apiName);

        // ── Cloud Saves ───────────────────────────────────────────────────────────

        public void CloudSaveWrite(string filename, byte[] data, Action<bool> onComplete = null)
            => _steam.CloudSaveWrite(filename, data, onComplete);

        public void CloudSaveRead(string filename, Action<byte[]> onComplete)
            => _steam.CloudSaveRead(filename, onComplete);

        // ── Leaderboards ──────────────────────────────────────────────────────────

        public void SubmitScore(string boardName, int score, Action<int> onComplete = null)
            => _steam.SubmitLeaderboardScore(boardName, score, onComplete);

        public void FetchLeaderboard(string boardName, int count, Action<List<PlatformLeaderboardEntry>> onComplete)
        {
            _steam.FetchLeaderboard(boardName, count, steamEntries =>
            {
                var result = new List<PlatformLeaderboardEntry>(steamEntries.Count);
                foreach (var e in steamEntries)
                    result.Add(new PlatformLeaderboardEntry
                    {
                        DisplayName = e.DisplayName,
                        Score       = e.Score,
                        Rank        = e.Rank,
                    });
                onComplete?.Invoke(result);
            });
        }

        // ── Stats ─────────────────────────────────────────────────────────────────

        public void SetStatInt(string name, int value)   => _steam.SetStatInt(name, value);
        public void SetStatFloat(string name, float value) => _steam.SetStatFloat(name, value);
        public int  GetStatInt(string name)              => _steam.GetStatInt(name);
        public void StoreStats()                         => _steam.StoreStats();

        // ── Overlay ───────────────────────────────────────────────────────────────

        public void OpenOverlayURL(string url)
        {
#if STEAMWORKS_ENABLED
            if (_steam.IsAvailable)
                Steamworks.SteamFriends.ActivateGameOverlayToWebPage(url);
#else
            Debug.Log($"[SteamPlatformService] OpenOverlayURL: {url} (STEAMWORKS_ENABLED not set)");
#endif
        }
    }
}
