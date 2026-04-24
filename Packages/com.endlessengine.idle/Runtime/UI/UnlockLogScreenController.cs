using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.UnlockLog;
using EndlessEngine.Config;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Controls the unlock log / discoveries screen: category filter, entry list,
    /// hidden entries shown as locked slots.
    ///
    /// Attach to a UIDocument whose Source Asset is UnlockLogScreen.uxml.
    /// Wire UnlockLogService and all UnlockEntryConfigSOs in Inspector.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UnlockLogScreenController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private UnlockLogService       _unlockLogService;
        [SerializeField] private UnlockEntryConfigSO[]  _allEntries;

        // ── UI Elements ──────────────────────────────────────────────────────────

        private VisualElement _root;
        private VisualElement _list;
        private Button        _closeButton;
        private Label         _countLabel;

        private readonly Dictionary<string, Button> _catButtons = new Dictionary<string, Button>();
        private string _activeCategory = "all";

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            var doc     = GetComponent<UIDocument>();
            var docRoot = doc.rootVisualElement;

            _root        = docRoot.Q<VisualElement>("unlock-root");
            _list        = docRoot.Q<VisualElement>("unlock-list");
            _closeButton = docRoot.Q<Button>("close-button");
            _countLabel  = docRoot.Q<Label>("unlock-count-label");

            _closeButton?.RegisterCallback<ClickEvent>(_ => Hide());

            RegisterCatButton(docRoot, "filter-all",         "all");
            RegisterCatButton(docRoot, "filter-item",        "item");
            RegisterCatButton(docRoot, "filter-building",    "building");
            RegisterCatButton(docRoot, "filter-pet",         "pet");
            RegisterCatButton(docRoot, "filter-milestone",   "milestone");
            RegisterCatButton(docRoot, "filter-achievement", "achievement");

            UnlockLogService.OnEntryUnlocked += OnEntryUnlocked;
        }

        private void OnDisable()
        {
            UnlockLogService.OnEntryUnlocked -= OnEntryUnlocked;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        public void Show()
        {
            RefreshList();
            _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
        }

        // ── Event handlers ───────────────────────────────────────────────────────

        private void OnEntryUnlocked(UnlockEntryConfigSO _)
        {
            if (_root.style.display == DisplayStyle.Flex) RefreshList();
        }

        // ── List ─────────────────────────────────────────────────────────────────

        private void RefreshList()
        {
            if (_list == null || _unlockLogService == null || _allEntries == null) return;
            _list.Clear();

            int discovered = 0;
            foreach (var cfg in _allEntries)
            {
                if (cfg == null) continue;
                if (!PassesCategoryFilter(cfg)) continue;

                bool isUnlocked = _unlockLogService.IsUnlocked(cfg.EntryId);
                bool isVisible  = isUnlocked || !cfg.IsHiddenUntilUnlocked;

                if (!isVisible) continue;

                if (isUnlocked) discovered++;
                _list.Add(BuildEntry(cfg, isUnlocked));
            }

            if (_countLabel != null)
                _countLabel.text = $"{_unlockLogService.TotalUnlocked} discovered";
        }

        private VisualElement BuildEntry(UnlockEntryConfigSO cfg, bool isUnlocked)
        {
            var entry = new VisualElement();
            entry.AddToClassList("unlock-entry");
            if (!isUnlocked) entry.AddToClassList("locked");

            var icon = new Label(isUnlocked ? "◆" : "◇");
            icon.AddToClassList("unlock-entry-icon");
            if (!isUnlocked) icon.AddToClassList("locked");
            entry.Add(icon);

            var nameLabel = new Label(isUnlocked ? cfg.DisplayName : "???");
            nameLabel.AddToClassList("unlock-entry-name");
            if (!isUnlocked) nameLabel.AddToClassList("locked");
            entry.Add(nameLabel);

            var catLabel = new Label(cfg.Category.ToString());
            catLabel.AddToClassList("unlock-entry-category");
            if (!isUnlocked) catLabel.AddToClassList("locked");
            entry.Add(catLabel);

            return entry;
        }

        // ── Category filter ──────────────────────────────────────────────────────

        private void RegisterCatButton(VisualElement root, string buttonName, string category)
        {
            var btn = root.Q<Button>(buttonName);
            if (btn == null) return;
            _catButtons[category] = btn;
            btn.RegisterCallback<ClickEvent>(_ => SetCategory(category));
        }

        private void SetCategory(string category)
        {
            _activeCategory = category;
            foreach (var kv in _catButtons)
            {
                kv.Value.RemoveFromClassList("active");
                if (kv.Key == category) kv.Value.AddToClassList("active");
            }
            RefreshList();
        }

        private bool PassesCategoryFilter(UnlockEntryConfigSO cfg)
        {
            if (_activeCategory == "all") return true;
            return cfg.Category.ToString().ToLower() == _activeCategory;
        }
    }
}
