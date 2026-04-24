using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Defines a local leaderboard. Scores are stored in PlayerPrefs (local only).
    ///
    /// SortOrder = Descending means higher scores rank better.
    /// MaxEntries caps the stored history (oldest removed when full).
    /// </summary>
    [CreateAssetMenu(menuName = "Endless Engine/Leaderboard Config", fileName = "LeaderboardConfig")]
    public class LeaderboardConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public string  BoardId;
        public string  DisplayName;
        public string  ScoreLabel = "Score";

        [Header("Settings")]
        [Min(1)] public int  MaxEntries  = 10;
        public          bool SortDescending = true; // true = high score first
    }
}
