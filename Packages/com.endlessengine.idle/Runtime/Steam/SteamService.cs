using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if STEAMWORKS_ENABLED
using Steamworks;
#endif

namespace EndlessEngine.Steam
{
    /// <summary>
    /// Steamworks.NET backend for ISteamService.
    ///
    /// Activation:
    ///   1. Install com.rlabrecque.steamworks.net via Package Manager
    ///   2. Add "STEAMWORKS_ENABLED" to Player Settings → Scripting Define Symbols
    ///   3. Place a steam_appid.txt with your App ID next to the executable
    ///   4. Call SteamService.Initialize() from Bootstrap before any Steam calls
    ///
    /// Without STEAMWORKS_ENABLED this file compiles to an empty stub so non-Steam
    /// builds continue to work with NullSteamService.
    ///
    /// Thread safety: All Steamworks callbacks must be dispatched on the main thread.
    /// SteamAPI.RunCallbacks() is called from Update() which satisfies this.
    /// </summary>
    public class SteamService : MonoBehaviour, ISteamService
    {
#if STEAMWORKS_ENABLED

        // ── State ─────────────────────────────────────────────────────────────────

        private bool _initialized;
        private uint _appId;

        // Cached leaderboard handles — Steam handle lookups are async; cache after first find.
        private readonly Dictionary<string, SteamLeaderboard_t> _leaderboardHandles
            = new Dictionary<string, SteamLeaderboard_t>();

        // ── ISteamService ─────────────────────────────────────────────────────────

        public bool IsAvailable => _initialized && SteamAPI.IsSteamRunning();

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Initialises the Steamworks API. Call once from Bootstrap before any Steam use.
        /// <paramref name="appId"/> must match the value in steam_appid.txt.
        /// Returns false and falls back to NullSteamService if Steam is not running.
        /// </summary>
        public bool Initialize(uint appId)
        {
            _appId = appId;

            if (!SteamAPI.Init())
            {
                Debug.LogWarning($"[SteamService] SteamAPI.Init() failed — Steam may not be running. appId={appId}");
                _initialized = false;
                return false;
            }

            _initialized = true;
            SteamAPI.RequestCurrentStats(); // required before any Get/SetStat calls
            Debug.Log($"[SteamService] Initialised. AppId={appId}. Player: {SteamFriends.GetPersonaName()}");
            return true;
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Update()
        {
            if (_initialized)
                SteamAPI.RunCallbacks();
        }

        private void OnDestroy()
        {
            if (_initialized)
            {
                SteamAPI.Shutdown();
                _initialized = false;
                Debug.Log("[SteamService] SteamAPI shut down.");
            }
        }

        // ── Achievements ──────────────────────────────────────────────────────────

        public void UnlockAchievement(string apiName)
        {
            if (!IsAvailable) return;
            SteamUserStats.SetAchievement(apiName);
            SteamUserStats.StoreStats();
            Debug.Log($"[SteamService] Achievement unlocked: {apiName}");
        }

        public bool IsAchievementUnlocked(string apiName)
        {
            if (!IsAvailable) return false;
            SteamUserStats.GetAchievement(apiName, out bool achieved);
            return achieved;
        }

        // ── Stats ─────────────────────────────────────────────────────────────────

        public void SetStatInt(string name, int value)
        {
            if (!IsAvailable) return;
            SteamUserStats.SetStat(name, value);
        }

        public void SetStatFloat(string name, float value)
        {
            if (!IsAvailable) return;
            SteamUserStats.SetStat(name, value);
        }

        public int GetStatInt(string name)
        {
            if (!IsAvailable) return 0;
            SteamUserStats.GetStat(name, out int value);
            return value;
        }

        public void StoreStats()
        {
            if (!IsAvailable) return;
            SteamUserStats.StoreStats();
        }

        // ── Cloud Saves ───────────────────────────────────────────────────────────

        public void CloudSaveWrite(string filename, byte[] data, Action<bool> onComplete = null)
        {
            if (!IsAvailable) { onComplete?.Invoke(false); return; }

            bool ok = SteamRemoteStorage.FileWrite(filename, data, data.Length);
            if (!ok)
                Debug.LogWarning($"[SteamService] CloudSaveWrite failed for '{filename}'.");
            onComplete?.Invoke(ok);
        }

        public void CloudSaveRead(string filename, Action<byte[]> onComplete)
        {
            if (!IsAvailable) { onComplete?.Invoke(null); return; }

            if (!SteamRemoteStorage.FileExists(filename))
            {
                onComplete?.Invoke(null);
                return;
            }

            int size = SteamRemoteStorage.GetFileSize(filename);
            if (size <= 0) { onComplete?.Invoke(null); return; }

            byte[] buffer = new byte[size];
            int    read   = SteamRemoteStorage.FileRead(filename, buffer, size);
            onComplete?.Invoke(read == size ? buffer : null);
        }

        public bool CloudSaveExists(string filename)
        {
            if (!IsAvailable) return false;
            return SteamRemoteStorage.FileExists(filename);
        }

        // ── Leaderboards ──────────────────────────────────────────────────────────

        public void SubmitLeaderboardScore(string leaderboardName, int score, Action<int> onComplete = null)
        {
            if (!IsAvailable) { onComplete?.Invoke(-1); return; }
            StartCoroutine(SubmitScoreRoutine(leaderboardName, score, onComplete));
        }

        public void FetchLeaderboard(string leaderboardName, int entryCount, Action<List<SteamLeaderboardEntry>> onComplete)
        {
            if (!IsAvailable) { onComplete?.Invoke(new List<SteamLeaderboardEntry>()); return; }
            StartCoroutine(FetchLeaderboardRoutine(leaderboardName, entryCount, onComplete));
        }

        // ── Leaderboard coroutines ────────────────────────────────────────────────

        private IEnumerator SubmitScoreRoutine(string name, int score, Action<int> onComplete)
        {
            yield return StartCoroutine(EnsureLeaderboardHandle(name));

            if (!_leaderboardHandles.TryGetValue(name, out var handle) || !handle.IsValid())
            {
                Debug.LogWarning($"[SteamService] Leaderboard '{name}' handle invalid — score not submitted.");
                onComplete?.Invoke(-1);
                yield break;
            }

            bool done    = false;
            int  newRank = -1;

            var call = SteamUserStats.UploadLeaderboardScore(
                handle,
                ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
                score,
                null, 0);

            var callResult = CallResult<LeaderboardScoreUploaded_t>.Create((result, ioFailure) =>
            {
                if (!ioFailure && result.m_bSuccess == 1)
                    newRank = (int)result.m_nGlobalRankNew;
                else
                    Debug.LogWarning($"[SteamService] Score upload failed for '{name}'.");
                done = true;
            });
            callResult.Set(call);

            yield return new WaitUntil(() => done);
            onComplete?.Invoke(newRank);
        }

        private IEnumerator FetchLeaderboardRoutine(string name, int count, Action<List<SteamLeaderboardEntry>> onComplete)
        {
            yield return StartCoroutine(EnsureLeaderboardHandle(name));

            if (!_leaderboardHandles.TryGetValue(name, out var handle) || !handle.IsValid())
            {
                onComplete?.Invoke(new List<SteamLeaderboardEntry>());
                yield break;
            }

            bool                     done    = false;
            List<SteamLeaderboardEntry> result = new List<SteamLeaderboardEntry>();

            var call = SteamUserStats.DownloadLeaderboardEntries(
                handle,
                ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,
                1, count);

            var callResult = CallResult<LeaderboardScoresDownloaded_t>.Create((data, ioFailure) =>
            {
                if (!ioFailure)
                {
                    for (int i = 0; i < data.m_cEntryCount; i++)
                    {
                        SteamUserStats.GetDownloadedLeaderboardEntry(
                            data.m_hSteamLeaderboardEntries, i,
                            out LeaderboardEntry_t entry, null, 0);

                        result.Add(new SteamLeaderboardEntry
                        {
                            DisplayName = SteamFriends.GetFriendPersonaName(entry.m_steamIDUser),
                            Score       = entry.m_nScore,
                            Rank        = entry.m_nGlobalRank,
                        });
                    }
                }
                done = true;
            });
            callResult.Set(call);

            yield return new WaitUntil(() => done);
            onComplete?.Invoke(result);
        }

        private IEnumerator EnsureLeaderboardHandle(string name)
        {
            if (_leaderboardHandles.TryGetValue(name, out var existing) && existing.IsValid())
                yield break;

            bool done = false;
            var call = SteamUserStats.FindLeaderboard(name);

            var callResult = CallResult<LeaderboardFindResult_t>.Create((data, ioFailure) =>
            {
                if (!ioFailure && data.m_bLeaderboardFound == 1)
                    _leaderboardHandles[name] = data.m_hSteamLeaderboard;
                else
                    Debug.LogWarning($"[SteamService] Leaderboard '{name}' not found on Steam backend.");
                done = true;
            });
            callResult.Set(call);

            yield return new WaitUntil(() => done);
        }

#else
        // ── Stub when STEAMWORKS_ENABLED is not defined ───────────────────────────
        // The class still compiles but acts as NullSteamService.
        // Bootstrap should use NullSteamService.Instance instead of this MonoBehaviour.

        public bool IsAvailable => false;

        public void UnlockAchievement(string apiName)                                               { }
        public bool IsAchievementUnlocked(string apiName)                                           => false;
        public void SetStatInt(string name, int value)                                              { }
        public void SetStatFloat(string name, float value)                                          { }
        public int  GetStatInt(string name)                                                         => 0;
        public void StoreStats()                                                                     { }
        public void CloudSaveWrite(string filename, byte[] data, Action<bool> onComplete = null)    => onComplete?.Invoke(false);
        public void CloudSaveRead(string filename, Action<byte[]> onComplete)                       => onComplete?.Invoke(null);
        public bool CloudSaveExists(string filename)                                                => false;
        public void SubmitLeaderboardScore(string name, int score, Action<int> onComplete = null)   => onComplete?.Invoke(-1);
        public void FetchLeaderboard(string name, int count, Action<List<SteamLeaderboardEntry>> cb) => cb?.Invoke(new List<SteamLeaderboardEntry>());
#endif
    }
}
