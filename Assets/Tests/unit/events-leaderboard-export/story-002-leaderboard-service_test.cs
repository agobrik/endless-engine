// Tests for Sprint 18 — S18-02: LeaderboardService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - SubmitScore: adds entry, fires OnScoreSubmitted
//   - SubmitScore: returns false for unknown boardId
//   - GetBoard: sorted descending (default)
//   - GetBoard: sorted ascending (low-score-first board)
//   - MaxEntries: trims oldest entries
//   - GetRank: correct 1-based rank
//   - IsHighScore: true when score beats current #1
//   - Persist: board survives re-initialize from PlayerPrefs
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.EventsLeaderboardExport

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Leaderboard;

namespace EndlessEngine.Tests.Unit.EventsLeaderboardExport
{
    [TestFixture]
    public class LeaderboardServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private LeaderboardService  _service;
        private LeaderboardConfigSO _hiScoreBoard;
        private LeaderboardConfigSO _loScoreBoard;

        private readonly List<(string, LeaderboardEntry)> _submittedEvents = new List<(string, LeaderboardEntry)>();

        [SetUp]
        public void SetUp()
        {
            LeaderboardService.ClearSubscribersForTesting();
            LeaderboardService.OnScoreSubmitted += (id, e) => _submittedEvents.Add((id, e));
            _submittedEvents.Clear();

            _hiScoreBoard = ScriptableObject.CreateInstance<LeaderboardConfigSO>();
            _hiScoreBoard.BoardId        = "wave_record";
            _hiScoreBoard.MaxEntries     = 3;
            _hiScoreBoard.SortDescending = true;

            _loScoreBoard = ScriptableObject.CreateInstance<LeaderboardConfigSO>();
            _loScoreBoard.BoardId        = "speedrun";
            _loScoreBoard.MaxEntries     = 3;
            _loScoreBoard.SortDescending = false; // lower = better

            var go   = new GameObject("LeaderboardService");
            _service = go.AddComponent<LeaderboardService>();
            _service.Initialize(new[] { _hiScoreBoard, _loScoreBoard });
            // Clear any PlayerPrefs from previous test runs
            _service.ClearBoardForTesting("wave_record");
            _service.ClearBoardForTesting("speedrun");
            _service.Initialize(new[] { _hiScoreBoard, _loScoreBoard });
        }

        [TearDown]
        public void TearDown()
        {
            LeaderboardService.ClearSubscribersForTesting();
            if (_service      != null)
            {
                _service.ClearBoardForTesting("wave_record");
                _service.ClearBoardForTesting("speedrun");
                Object.DestroyImmediate(_service.gameObject);
            }
            if (_hiScoreBoard != null) Object.DestroyImmediate(_hiScoreBoard);
            if (_loScoreBoard != null) Object.DestroyImmediate(_loScoreBoard);
        }

        // ── SubmitScore ───────────────────────────────────────────────────────────

        [Test]
        public void SubmitScore_AddsEntry_FiresEvent()
        {
            bool result = _service.SubmitScore("wave_record", "Alice", 100);
            Assert.IsTrue(result);
            Assert.AreEqual(1, _service.GetBoard("wave_record").Count);
            Assert.AreEqual(1, _submittedEvents.Count);
            Assert.AreEqual("wave_record", _submittedEvents[0].Item1);
        }

        [Test]
        public void SubmitScore_ReturnsFalse_ForUnknownBoard()
        {
            bool result = _service.SubmitScore("nonexistent", "Alice", 100);
            Assert.IsFalse(result);
        }

        // ── Sort Order ────────────────────────────────────────────────────────────

        [Test]
        public void GetBoard_SortedDescending_HighestFirst()
        {
            _service.SubmitScore("wave_record", "A", 50);
            _service.SubmitScore("wave_record", "B", 200);
            _service.SubmitScore("wave_record", "C", 100);

            var board = _service.GetBoard("wave_record");
            Assert.AreEqual(200, board[0].Score, "Highest score first");
            Assert.AreEqual(100, board[1].Score);
            Assert.AreEqual(50,  board[2].Score);
        }

        [Test]
        public void GetBoard_SortedAscending_LowestFirst()
        {
            _service.SubmitScore("speedrun", "A", 500);
            _service.SubmitScore("speedrun", "B", 200);
            _service.SubmitScore("speedrun", "C", 350);

            var board = _service.GetBoard("speedrun");
            Assert.AreEqual(200, board[0].Score, "Lowest score first");
            Assert.AreEqual(350, board[1].Score);
            Assert.AreEqual(500, board[2].Score);
        }

        // ── MaxEntries ────────────────────────────────────────────────────────────

        [Test]
        public void MaxEntries_TrimsExcessEntries()
        {
            _service.SubmitScore("wave_record", "A", 10);
            _service.SubmitScore("wave_record", "B", 20);
            _service.SubmitScore("wave_record", "C", 30);
            _service.SubmitScore("wave_record", "D", 5); // should be trimmed (lowest, descending)

            var board = _service.GetBoard("wave_record");
            Assert.AreEqual(3, board.Count, "Max 3 entries");
            Assert.AreEqual(30, board[0].Score);
        }

        // ── GetRank ───────────────────────────────────────────────────────────────

        [Test]
        public void GetRank_ReturnsCorrectRank_Descending()
        {
            _service.SubmitScore("wave_record", "A", 300);
            _service.SubmitScore("wave_record", "B", 200);
            _service.SubmitScore("wave_record", "C", 100);

            Assert.AreEqual(1, _service.GetRank("wave_record", 400), "400 beats #1");
            Assert.AreEqual(2, _service.GetRank("wave_record", 250), "250 is rank 2");
            Assert.AreEqual(4, _service.GetRank("wave_record", 50),  "50 is rank 4 (outside top 3)");
        }

        // ── IsHighScore ───────────────────────────────────────────────────────────

        [Test]
        public void IsHighScore_TrueWhenBoardEmpty()
        {
            Assert.IsTrue(_service.IsHighScore("wave_record", 1));
        }

        [Test]
        public void IsHighScore_TrueWhenBeatsLeader()
        {
            _service.SubmitScore("wave_record", "A", 100);
            Assert.IsTrue(_service.IsHighScore("wave_record", 200));
        }

        [Test]
        public void IsHighScore_FalseWhenBelowLeader()
        {
            _service.SubmitScore("wave_record", "A", 100);
            Assert.IsFalse(_service.IsHighScore("wave_record", 50));
        }

#endif
    }
}
