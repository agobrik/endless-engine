using System;

namespace EndlessEngine.Leaderboard
{
    [Serializable]
    public class LeaderboardEntry
    {
        public string PlayerName;
        public long   Score;
        public string Timestamp; // ISO-8601 UTC string

        public LeaderboardEntry(string name, long score, DateTime timestamp)
        {
            PlayerName = name;
            Score      = score;
            Timestamp  = timestamp.ToString("o");
        }
    }
}
