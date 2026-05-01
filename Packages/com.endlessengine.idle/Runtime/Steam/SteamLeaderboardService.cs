using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Leaderboard;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Steam
{
    /// <summary>
    /// Extends the local LeaderboardService with Steam cloud leaderboard sync.
    ///
    /// Design: local leaderboard remains authoritative for UI — Steam is a secondary
    /// sync target. This means the game works offline or without Steam, and Steam
    /// scores are uploaded in the background without blocking gameplay.
    ///
    /// Score mapping: LeaderboardEntry.Score (long) is cast to int for Steam.
    /// For games with scores > int.MaxValue, clamp before submission (configurable).
    ///
    /// Bootstrap wiring (after SteamService.Initialize and LeaderboardService.Initialize):
    ///   steamLeaderboard.Initialize(leaderboardService, steamService, boardMappings);
    ///
    /// Board mappings connect local BoardId → Steam leaderboard API name. Boards
    /// not in the mapping are synced using the BoardId directly as the Steam name.
    /// </summary>
    public class SteamLeaderboardService : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────────────────────────

        private LeaderboardService              _local;
        private ISteamService                   _steam;
        private Dictionary<string, string>      _boardNameMap;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        /// <summary>
        /// <paramref name="boardMappings"/> maps localBoardId → Steam leaderboard name.
        /// Pass null or empty to use localBoardId directly as the Steam name.
        /// </summary>
        public void Initialize(
            LeaderboardService                 local,
            ISteamService                      steam,
            Dictionary<string, string>         boardMappings = null)
        {
            _local        = local;
            _steam        = steam ?? NullSteamService.Instance;
            _boardNameMap = boardMappings ?? new Dictionary<string, string>();
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()  => LeaderboardService.OnScoreSubmitted += HandleScoreSubmitted;
        private void OnDisable() => LeaderboardService.OnScoreSubmitted -= HandleScoreSubmitted;

        // ── Score submission ──────────────────────────────────────────────────────

        /// <summary>
        /// Submits a score to both local and Steam leaderboards.
        /// Local submission is synchronous; Steam upload is fire-and-forget.
        /// </summary>
        public bool SubmitScore(string boardId, string playerName, long score)
        {
            bool localOk = _local != null && _local.SubmitScore(boardId, playerName, score);
            UploadToSteam(boardId, score);
            return localOk;
        }

        /// <summary>
        /// Fetches the Steam global leaderboard and fires <paramref name="onComplete"/>
        /// with the results. Falls back to the local board if Steam is unavailable.
        /// </summary>
        public void FetchGlobalLeaderboard(string boardId, int entryCount, Action<List<SteamLeaderboardEntry>> onComplete)
        {
            if (!_steam.IsAvailable)
            {
                onComplete?.Invoke(new List<SteamLeaderboardEntry>());
                return;
            }

            string steamName = ResolveSteamName(boardId);
            _steam.FetchLeaderboard(steamName, entryCount, onComplete);
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void HandleScoreSubmitted(string boardId, LeaderboardEntry entry)
        {
            // Auto-sync to Steam whenever a score is submitted through LeaderboardService.
            UploadToSteam(boardId, entry.Score);
        }

        private void UploadToSteam(string boardId, long score)
        {
            if (!_steam.IsAvailable) return;

            string steamName  = ResolveSteamName(boardId);
            int    steamScore = score > int.MaxValue ? int.MaxValue : (int)score;

            _steam.SubmitLeaderboardScore(steamName, steamScore, rank =>
            {
                if (rank > 0)
                    Debug.Log($"[SteamLeaderboardService] Uploaded score {steamScore} to '{steamName}' — rank #{rank}.");
            });
        }

        private string ResolveSteamName(string boardId)
        {
            if (_boardNameMap != null && _boardNameMap.TryGetValue(boardId, out var mapped))
                return mapped;
            return boardId;
        }
    }
}
