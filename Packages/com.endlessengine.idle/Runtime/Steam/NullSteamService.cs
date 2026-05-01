using System;
using System.Collections.Generic;

namespace EndlessEngine.Steam
{
    /// <summary>
    /// No-op ISteamService. Default when Steamworks.NET is unavailable or Steam is not running.
    /// All calls silently succeed or return safe defaults — no exceptions, no log spam.
    /// </summary>
    public sealed class NullSteamService : ISteamService
    {
        public static readonly NullSteamService Instance = new NullSteamService();

        public bool IsAvailable => false;

        public void  UnlockAchievement(string apiName)                                  { }
        public bool  IsAchievementUnlocked(string apiName)                              => false;
        public void  SetStatInt(string name, int value)                                 { }
        public void  SetStatFloat(string name, float value)                             { }
        public int   GetStatInt(string name)                                            => 0;
        public void  StoreStats()                                                        { }
        public void  CloudSaveWrite(string filename, byte[] data, Action<bool> onComplete = null) => onComplete?.Invoke(false);
        public void  CloudSaveRead(string filename, Action<byte[]> onComplete)          => onComplete?.Invoke(null);
        public bool  CloudSaveExists(string filename)                                   => false;
        public void  SubmitLeaderboardScore(string leaderboardName, int score, Action<int> onComplete = null) => onComplete?.Invoke(-1);
        public void  FetchLeaderboard(string leaderboardName, int entryCount, Action<List<SteamLeaderboardEntry>> onComplete) => onComplete?.Invoke(new List<SteamLeaderboardEntry>());
    }
}
