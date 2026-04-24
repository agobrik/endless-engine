using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Leaderboard;
using EndlessEngine.Config;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Controls the leaderboard screen: board selector tabs, score table with rank
    /// highlight, and submit score form.
    ///
    /// Attach to a UIDocument whose Source Asset is LeaderboardScreen.uxml.
    /// Wire LeaderboardService and LeaderboardConfigs in Inspector.
    /// Caller must set CurrentScore before showing (used for submit).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LeaderboardScreenController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private LeaderboardService    _leaderboardService;
        [SerializeField] private LeaderboardConfigSO[] _allBoards;

        /// <summary>Score that will be submitted when the player clicks Submit.</summary>
        public long CurrentScore { get; set; }

        // ── UI Elements ──────────────────────────────────────────────────────────

        private VisualElement _root;
        private VisualElement _boardSelector;
        private VisualElement _rowContainer;
        private Button        _closeButton;
        private Button        _submitButton;
        private TextField     _nameField;

        private readonly Dictionary<string, Button> _boardTabs = new Dictionary<string, Button>();
        private string _activeBoardId;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            var doc     = GetComponent<UIDocument>();
            var docRoot = doc.rootVisualElement;

            _root          = docRoot.Q<VisualElement>("lb-root");
            _boardSelector = docRoot.Q<VisualElement>("board-selector");
            _rowContainer  = docRoot.Q<VisualElement>("lb-rows");
            _closeButton   = docRoot.Q<Button>("close-button");
            _submitButton  = docRoot.Q<Button>("submit-btn");
            _nameField     = docRoot.Q<TextField>("player-name-field");

            _closeButton?.RegisterCallback<ClickEvent>(_ => Hide());
            _submitButton?.RegisterCallback<ClickEvent>(_ => OnSubmit());

            BuildBoardTabs();

            if (_allBoards != null && _allBoards.Length > 0 && _allBoards[0] != null)
                _activeBoardId = _allBoards[0].BoardId;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        public void Show()
        {
            RefreshTable();
            _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
        }

        // ── Board tabs ───────────────────────────────────────────────────────────

        private void BuildBoardTabs()
        {
            if (_boardSelector == null || _allBoards == null) return;
            _boardSelector.Clear();
            _boardTabs.Clear();

            foreach (var cfg in _allBoards)
            {
                if (cfg == null) continue;
                string id  = cfg.BoardId;
                var tab    = new Button(() => SelectBoard(id));
                tab.text   = cfg.DisplayName;
                tab.AddToClassList("board-tab");
                _boardSelector.Add(tab);
                _boardTabs[id] = tab;
            }
        }

        private void SelectBoard(string boardId)
        {
            _activeBoardId = boardId;
            foreach (var kv in _boardTabs)
            {
                kv.Value.RemoveFromClassList("active");
                if (kv.Key == boardId) kv.Value.AddToClassList("active");
            }
            RefreshTable();
        }

        // ── Table refresh ────────────────────────────────────────────────────────

        private void RefreshTable()
        {
            if (_rowContainer == null || _leaderboardService == null || _activeBoardId == null) return;
            _rowContainer.Clear();

            // Highlight active tab
            foreach (var kv in _boardTabs)
            {
                kv.Value.RemoveFromClassList("active");
                if (kv.Key == _activeBoardId) kv.Value.AddToClassList("active");
            }

            var entries = _leaderboardService.GetBoard(_activeBoardId);
            if (entries == null) return;

            int playerRank = _leaderboardService.GetRank(_activeBoardId, CurrentScore);

            for (int i = 0; i < entries.Count; i++)
            {
                var e   = entries[i];
                int rank = i + 1;
                bool isPlayer = rank == playerRank;
                _rowContainer.Add(BuildRow(rank, e, isPlayer));
            }
        }

        private VisualElement BuildRow(int rank, LeaderboardEntry entry, bool isPlayerRow)
        {
            var row = new VisualElement();
            row.AddToClassList("lb-row");
            if (isPlayerRow) row.AddToClassList("highlight");

            var rankLabel = new Label(rank.ToString());
            rankLabel.AddToClassList("lb-row-rank");
            if (rank <= 3) rankLabel.AddToClassList("top3");
            row.Add(rankLabel);

            var nameLabel = new Label(entry.PlayerName);
            nameLabel.AddToClassList("lb-row-name");
            row.Add(nameLabel);

            var scoreLabel = new Label(entry.Score.ToString("N0"));
            scoreLabel.AddToClassList("lb-row-score");
            row.Add(scoreLabel);

            string dateStr = entry.Timestamp?.Length >= 10 ? entry.Timestamp.Substring(0, 10) : "--";
            var dateLabel  = new Label(dateStr);
            dateLabel.AddToClassList("lb-row-date");
            row.Add(dateLabel);

            return row;
        }

        // ── Submit ───────────────────────────────────────────────────────────────

        private void OnSubmit()
        {
            if (_leaderboardService == null || _activeBoardId == null) return;
            string playerName = _nameField?.value;
            if (string.IsNullOrWhiteSpace(playerName)) playerName = "Player";
            _leaderboardService.SubmitScore(_activeBoardId, playerName, CurrentScore);
            RefreshTable();
        }
    }
}
