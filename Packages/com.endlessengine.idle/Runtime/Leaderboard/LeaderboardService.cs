using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.Leaderboard
{
    /// <summary>
    /// Local-only leaderboard service. Scores are stored in PlayerPrefs as JSON.
    /// No cloud sync — local device only.
    ///
    /// Supports multiple boards (one per LeaderboardConfigSO).
    /// Sorted descending by default (high score first).
    /// </summary>
    public class LeaderboardService : MonoBehaviour
    {
        public static event Action<string, LeaderboardEntry> OnScoreSubmitted; // boardId, entry

        private readonly Dictionary<string, LeaderboardConfigSO>  _configs = new Dictionary<string, LeaderboardConfigSO>();
        private readonly Dictionary<string, List<LeaderboardEntry>> _boards  = new Dictionary<string, List<LeaderboardEntry>>();

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(LeaderboardConfigSO[] configs)
        {
            _configs.Clear();
            _boards.Clear();

            if (configs == null) return;
            foreach (var cfg in configs)
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.BoardId)) continue;
                _configs[cfg.BoardId] = cfg;
                _boards[cfg.BoardId]  = LoadBoard(cfg.BoardId);
            }
        }

        // ── Submit ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Submit a score. Adds to the board, re-sorts, trims to MaxEntries.
        /// Fires OnScoreSubmitted.
        /// </summary>
        public bool SubmitScore(string boardId, string playerName, long score)
        {
            if (!_configs.TryGetValue(boardId, out var config)) return false;
            if (!_boards.TryGetValue(boardId, out var board))   return false;

            var entry = new LeaderboardEntry(playerName, score, DateTime.UtcNow);
            board.Add(entry);
            SortBoard(board, config.SortDescending);

            // Trim to max
            while (board.Count > config.MaxEntries)
                board.RemoveAt(board.Count - 1);

            SaveBoard(boardId, board);
            OnScoreSubmitted?.Invoke(boardId, entry);
            return true;
        }

        // ── Query ─────────────────────────────────────────────────────────────────

        public IReadOnlyList<LeaderboardEntry> GetBoard(string boardId)
        {
            _boards.TryGetValue(boardId, out var board);
            return board ?? new List<LeaderboardEntry>();
        }

        public int GetRank(string boardId, long score)
        {
            if (!_boards.TryGetValue(boardId, out var board)) return -1;
            if (!_configs.TryGetValue(boardId, out var config)) return -1;

            for (int i = 0; i < board.Count; i++)
            {
                bool betterOrEqual = config.SortDescending
                    ? score >= board[i].Score
                    : score <= board[i].Score;
                if (betterOrEqual) return i + 1; // 1-based rank
            }
            return board.Count + 1;
        }

        public bool IsHighScore(string boardId, long score)
        {
            if (!_boards.TryGetValue(boardId, out var board)) return false;
            if (board.Count == 0) return true;
            if (!_configs.TryGetValue(boardId, out var config)) return false;
            return config.SortDescending ? score > board[0].Score : score < board[0].Score;
        }

        // ── Persistence (PlayerPrefs) ─────────────────────────────────────────────

        private static string PrefsKey(string boardId) => $"leaderboard_{boardId}";

        private static void SaveBoard(string boardId, List<LeaderboardEntry> board)
        {
            var wrapper = new BoardWrapper { Entries = board };
            PlayerPrefs.SetString(PrefsKey(boardId), JsonUtility.ToJson(wrapper));
            PlayerPrefs.Save();
        }

        private static List<LeaderboardEntry> LoadBoard(string boardId)
        {
            string json = PlayerPrefs.GetString(PrefsKey(boardId), null);
            if (string.IsNullOrEmpty(json)) return new List<LeaderboardEntry>();
            try
            {
                var wrapper = JsonUtility.FromJson<BoardWrapper>(json);
                return wrapper?.Entries ?? new List<LeaderboardEntry>();
            }
            catch
            {
                return new List<LeaderboardEntry>();
            }
        }

        private static void SortBoard(List<LeaderboardEntry> board, bool descending)
        {
            if (descending)
                board.Sort((a, b) => b.Score.CompareTo(a.Score));
            else
                board.Sort((a, b) => a.Score.CompareTo(b.Score));
        }

        [Serializable]
        private class BoardWrapper { public List<LeaderboardEntry> Entries; }

        private void OnDestroy() => ClearSubscribersForTesting();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnScoreSubmitted = null;
        }

        public void ClearBoardForTesting(string boardId)
        {
            if (_boards.ContainsKey(boardId)) _boards[boardId].Clear();
            PlayerPrefs.DeleteKey(PrefsKey(boardId));
        }
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }
}
