using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;
using EndlessEngine.Economy;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Controls the inventory screen: item grid, filter bar, slot-count label, tooltip.
    /// Subscribes to InventoryService.OnInventoryChanged to refresh automatically.
    ///
    /// Attach to a UIDocument whose Source Asset is InventoryScreen.uxml.
    /// Wire InventoryService and ItemDatabase in Inspector.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InventoryScreenController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private InventoryService _inventoryService;
        [SerializeField] private ItemConfigSO[]   _allItems;

        // ── UI Elements ───────────────────────────────────────────────────────────

        private VisualElement _root;
        private VisualElement _grid;
        private Label         _slotCountLabel;
        private Button        _closeButton;
        private VisualElement _tooltip;
        private Label         _tooltipName;
        private Label         _tooltipRarity;
        private Label         _tooltipCount;
        private Label         _tooltipDesc;

        private readonly Dictionary<string, Button> _filterButtons = new Dictionary<string, Button>();
        private string _activeFilter = "all";

        // ── Item config lookup ────────────────────────────────────────────────────

        private readonly Dictionary<string, ItemConfigSO> _itemLookup = new Dictionary<string, ItemConfigSO>();

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var docRoot = doc.rootVisualElement;

            _root           = docRoot.Q<VisualElement>("inventory-root");
            _grid           = docRoot.Q<VisualElement>("inventory-grid");
            _slotCountLabel = docRoot.Q<Label>("slot-count-label");
            _closeButton    = docRoot.Q<Button>("close-button");
            _tooltip        = docRoot.Q<VisualElement>("item-tooltip");
            _tooltipName    = docRoot.Q<Label>("tooltip-name");
            _tooltipRarity  = docRoot.Q<Label>("tooltip-rarity");
            _tooltipCount   = docRoot.Q<Label>("tooltip-count");
            _tooltipDesc    = docRoot.Q<Label>("tooltip-desc");

            _closeButton?.RegisterCallback<ClickEvent>(_ => Hide());

            RegisterFilterButton(docRoot, "filter-all",        "all");
            RegisterFilterButton(docRoot, "filter-equipment",  "equipment");
            RegisterFilterButton(docRoot, "filter-consumable", "consumable");
            RegisterFilterButton(docRoot, "filter-material",   "material");

            // Build item lookup
            _itemLookup.Clear();
            if (_allItems != null)
                foreach (var item in _allItems)
                    if (item != null) _itemLookup[item.ItemId] = item;

            InventoryService.OnInventoryChanged += OnInventoryChanged;
        }

        private void OnDisable()
        {
            InventoryService.OnInventoryChanged -= OnInventoryChanged;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void Show()
        {
            RefreshGrid();
            _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            HideTooltip();
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void OnInventoryChanged(string itemId, int newCount, int delta)
        {
            if (_root.style.display == DisplayStyle.Flex)
                RefreshGrid();
        }

        // ── Grid ─────────────────────────────────────────────────────────────────

        private void RefreshGrid()
        {
            _grid.Clear();
            if (_inventoryService == null) return;

            int slotCount = 0;
            foreach (var kv in _inventoryService.Stacks)
            {
                if (kv.Value <= 0) continue;
                if (!PassesFilter(kv.Key)) continue;

                _itemLookup.TryGetValue(kv.Key, out var cfg);
                _grid.Add(BuildSlot(kv.Key, kv.Value, cfg));
                slotCount++;
            }

            if (_slotCountLabel != null)
            {
                int maxSlots = _inventoryService.MaxSlots;
                _slotCountLabel.text = maxSlots > 0
                    ? $"{_inventoryService.SlotCount} / {maxSlots}"
                    : $"{_inventoryService.SlotCount}";
            }
        }

        private VisualElement BuildSlot(string itemId, int count, ItemConfigSO cfg)
        {
            var slot = new VisualElement();
            slot.AddToClassList("item-slot");

            ItemRarity rarity = cfg != null ? cfg.Rarity : ItemRarity.Common;
            slot.AddToClassList("rarity-" + rarity.ToString().ToLower());

            if (cfg?.Icon != null)
            {
                var icon = new VisualElement();
                icon.AddToClassList("item-slot-icon");
                icon.style.backgroundImage = new StyleBackground(cfg.Icon);
                slot.Add(icon);
            }

            if (count > 1)
            {
                var countLabel = new Label("×" + count);
                countLabel.AddToClassList("item-slot-count");
                slot.Add(countLabel);
            }

            slot.RegisterCallback<MouseEnterEvent>(_ => ShowTooltip(itemId, count, cfg));
            slot.RegisterCallback<MouseLeaveEvent>(_ => HideTooltip());

            return slot;
        }

        // ── Tooltip ───────────────────────────────────────────────────────────────

        private void ShowTooltip(string itemId, int count, ItemConfigSO cfg)
        {
            if (_tooltip == null) return;

            _tooltipName.text   = cfg?.DisplayName ?? itemId;
            _tooltipRarity.text = cfg != null ? cfg.Rarity.ToString() : "Unknown";
            _tooltipCount.text  = $"×{count}";
            _tooltipDesc.text   = cfg?.Description ?? "";

            _tooltip.style.display = DisplayStyle.Flex;
        }

        private void HideTooltip()
        {
            if (_tooltip != null)
                _tooltip.style.display = DisplayStyle.None;
        }

        // ── Filter ────────────────────────────────────────────────────────────────

        private void RegisterFilterButton(VisualElement root, string buttonName, string filter)
        {
            var btn = root.Q<Button>(buttonName);
            if (btn == null) return;
            _filterButtons[filter] = btn;
            btn.RegisterCallback<ClickEvent>(_ => SetFilter(filter));
        }

        private void SetFilter(string filter)
        {
            _activeFilter = filter;

            foreach (var kv in _filterButtons)
            {
                kv.Value.RemoveFromClassList("active");
                if (kv.Key == filter) kv.Value.AddToClassList("active");
            }

            RefreshGrid();
        }

        private bool PassesFilter(string itemId)
        {
            if (_activeFilter == "all") return true;
            if (!_itemLookup.TryGetValue(itemId, out var cfg)) return false;
            foreach (var tag in cfg.Tags)
                if (tag.ToLower() == _activeFilter) return true;
            return false;
        }
    }
}
