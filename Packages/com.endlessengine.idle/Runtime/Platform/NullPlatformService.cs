using System;
using System.Collections.Generic;

namespace EndlessEngine.Platform
{
    /// <summary>
    /// No-op IPlatformService. Default for itch.io, review builds, and tests.
    /// All calls silently succeed or return safe defaults.
    /// </summary>
    public sealed class NullPlatformService : IPlatformService
    {
        public static readonly NullPlatformService Instance = new NullPlatformService();

        public string PlatformName   => "Null";
        public bool   IsAvailable    => false;
        public bool   SupportsCloudSave    => false;
        public bool   SupportsLeaderboards => false;

        public void UnlockAchievement(string apiName)                                                           { }
        public bool IsAchievementUnlocked(string apiName)                                                       => false;
        public void CloudSaveWrite(string filename, byte[] data, Action<bool> onComplete = null)                => onComplete?.Invoke(false);
        public void CloudSaveRead(string filename, Action<byte[]> onComplete)                                   => onComplete?.Invoke(null);
        public void SubmitScore(string boardName, int score, Action<int> onComplete = null)                     => onComplete?.Invoke(-1);
        public void FetchLeaderboard(string boardName, int count, Action<List<PlatformLeaderboardEntry>> cb)    => cb?.Invoke(new List<PlatformLeaderboardEntry>());
        public void SetStatInt(string name, int value)                                                          { }
        public void SetStatFloat(string name, float value)                                                      { }
        public int  GetStatInt(string name)                                                                     => 0;
        public void StoreStats()                                                                                 { }
        public void OpenOverlayURL(string url)                                                                  { }
    }
}
