using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Prestige;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Full-page upgrade tree. Drag to pan.
    /// tree-viewport clips (overflow:hidden). tree-canvas is position:absolute inside it,
    /// panned by setting left/top directly — avoids ScrollView.contentContainer clipping bug.
    /// Node cards are position:absolute inside tree-canvas.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UpgradeScreenController : MonoBehaviour, ISaveStateProvider
    {
        private const float CellW = 112f;
        private const float CellH = 112f;
        private const float CardW = 100f;
        private const float CardH = 100f;
        private const float PadX  = 56f;
        private const float PadY  = 56f;

        [SerializeField] private UpgradeTreeConfigSO  _upgradeTree;
        [SerializeField] private EconomyService       _economy;
        [SerializeField] private SaveService          _saveService;
        [SerializeField] private PrestigeStateManager _prestigeManager;

        private Font _faFont;

        public int ProviderOrder => SaveConstants.SaveProviderOrder.UpgradeTree;

        private VisualElement _root;
        private VisualElement _viewport;    // overflow:hidden clipping container
        private VisualElement _treeCanvas;  // position:absolute, panned via left/top
        private VisualElement _lineLayer;
        private VisualElement _tooltip;
        private Button        _backButton;
        private Label         _goldLabel;

        private Label _ttName, _ttEffect, _ttRank, _ttCost, _ttPrereq;

        private readonly Dictionary<string, int>           _ranks       = new();
        private readonly Dictionary<string, VisualElement> _cards       = new();
        private readonly List<(string from, string to)>    _connections = new();
        private readonly Dictionary<string, Vector2>       _centres     = new();

        // drag-to-pan + zoom
        private bool    _dragging;
        private Vector2 _dragStart;
        private Vector2 _panAtDrag;
        private Vector2 _panOffset;     // current canvas offset (left/top)
        private float   _zoom = 1f;
        private const float ZoomMin  = 0.20f;
        private const float ZoomMax  = 2.0f;
        private const float ZoomStep = 0.12f;

        public event System.Action OnHide;

        private float _canvasW, _canvasH;

        private void Awake()
        {
            _faFont = Resources.Load<Font>("Fonts/fa-solid-900");

            var doc = GetComponent<UIDocument>().rootVisualElement;
            _root       = doc.Q<VisualElement>("upgrade-root");
            _viewport   = doc.Q<VisualElement>("tree-viewport");
            _treeCanvas = doc.Q<VisualElement>("tree-canvas");
            _backButton = doc.Q<Button>("back-button");
            _goldLabel  = doc.Q<Label>("gold-label");

            // page-title is position:absolute and covers the topbar — ignore picking so back button works
            var pageTitle = doc.Q<Label>("page-title");
            if (pageTitle != null) pageTitle.pickingMode = PickingMode.Ignore;

            if (_backButton != null)
                _backButton.clicked += Hide;

            // Tooltip lives on root so it's never clipped by the viewport
            _tooltip = MakeTooltip();
            _root?.Add(_tooltip);
            HideTooltip();

            // Drag-to-pan + zoom on the viewport
            if (_viewport != null)
            {
                _viewport.RegisterCallback<MouseDownEvent>(OnDragStart);
                _viewport.RegisterCallback<MouseMoveEvent>(OnDragMove);
                _viewport.RegisterCallback<MouseUpEvent>(OnDragEnd);
                _viewport.RegisterCallback<MouseLeaveEvent>(_ => EndDrag());
                _viewport.RegisterCallback<WheelEvent>(OnWheel);
            }

            SetVisible(false);
        }

        // ── Drag-to-pan ───────────────────────────────────────────────────────────

        private void OnDragStart(MouseDownEvent e)
        {
            if (e.button != 0) return;
            // Only start drag if click is directly on viewport/canvas, not on topbar buttons
            _dragging   = true;
            _dragStart  = e.mousePosition;
            _panAtDrag  = _panOffset;
            // Do NOT capture mouse — capturing prevents button clicks elsewhere
            // e.StopPropagation() removed so back button can still receive events
        }

        private void OnDragMove(MouseMoveEvent e)
        {
            if (!_dragging) return;
            Vector2 delta = (Vector2)e.mousePosition - _dragStart;
            SetPan(_panAtDrag + delta);
        }

        private void OnDragEnd(MouseUpEvent e)
        {
            if (e.button != 0) return;
            EndDrag();
        }

        private void EndDrag()
        {
            _dragging = false;
        }

        private void OnWheel(WheelEvent e)
        {
            if (_treeCanvas == null || _viewport == null) return;

            float oldZoom = _zoom;
            float newZoom = Mathf.Clamp(_zoom - e.delta.y * ZoomStep, ZoomMin, ZoomMax);
            if (Mathf.Approximately(oldZoom, newZoom)) { e.StopPropagation(); return; }

            // Zoom towards mouse cursor position in viewport space
            Vector2 mouseVP = e.localMousePosition; // relative to viewport
            // point on canvas (unscaled) that is under the mouse
            Vector2 mouseCanvas = (mouseVP - _panOffset) / oldZoom;
            // new pan so same canvas point stays under mouse
            Vector2 newPan = mouseVP - mouseCanvas * newZoom;

            _zoom = newZoom;
            _treeCanvas.style.scale = new StyleScale(new Scale(new Vector3(_zoom, _zoom, 1f)));
            _treeCanvas.style.transformOrigin = new StyleTransformOrigin(
                new TransformOrigin(0, 0, 0));
            SetPanRaw(newPan);
            e.StopPropagation();
        }

        private void SetPan(Vector2 pan)   => SetPanRaw(pan);

        private void SetPanRaw(Vector2 pan)
        {
            _panOffset = pan;
            if (_treeCanvas != null)
            {
                _treeCanvas.style.left = pan.x;
                _treeCanvas.style.top  = pan.y;
            }
        }

        // ── Tooltip ───────────────────────────────────────────────────────────────

        private VisualElement MakeTooltip()
        {
            var tt = new VisualElement();
            tt.AddToClassList("node-tooltip");
            tt.pickingMode = PickingMode.Ignore;
            _ttName   = ML("tt-name");   tt.Add(_ttName);
            _ttEffect = ML("tt-effect"); tt.Add(_ttEffect);
            _ttRank   = ML("tt-rank");   tt.Add(_ttRank);
            _ttCost   = ML("tt-cost");   tt.Add(_ttCost);
            _ttPrereq = ML("tt-prereq"); tt.Add(_ttPrereq);
            return tt;
        }

        private static Label ML(string cls) { var l = new Label(); l.AddToClassList(cls); return l; }

        private void OnEnable()  => EconomyService.OnResourcesChanged += OnGoldChanged;
        private void OnDisable() => EconomyService.OnResourcesChanged -= OnGoldChanged;

        // ── Public ────────────────────────────────────────────────────────────────

        /// <summary>Called at runtime by GeneratedGameHUD when EconomyService is available.</summary>
        public void InjectEconomy(EconomyService economy) => _economy = economy;

        public void Show()
        {
            SetVisible(true);       // make visible FIRST so layout runs
            BuildTree();
            UpdateGold();
        }

        public void Hide()
        {
            HideTooltip();
            _saveService?.NotifyUpgradePurchased();
            SetVisible(false);
            OnHide?.Invoke();
        }

        // ── Build ─────────────────────────────────────────────────────────────────

        private void BuildTree()
        {
            if (_treeCanvas == null || _upgradeTree == null) return;

            _treeCanvas.Clear();
            _cards.Clear();
            _connections.Clear();
            _centres.Clear();
            _zoom = 1f;
            _treeCanvas.style.scale = new StyleScale(new Scale(Vector3.one));
            _treeCanvas.style.transformOrigin = new StyleTransformOrigin(
                new TransformOrigin(0, 0, 0));

            // Canvas pixel size
            int maxCol = 0, maxRow = 0;
            foreach (var n in _upgradeTree.Nodes)
            {
                if (n.GridX > maxCol) maxCol = n.GridX;
                if (n.GridY > maxRow) maxRow = n.GridY;
            }
            _canvasW = (maxCol + 1) * CellW + PadX * 2f;
            _canvasH = (maxRow + 1) * CellH + PadY * 2f;

            // Canvas size — position:absolute so needs explicit size
            _treeCanvas.style.width    = _canvasW;
            _treeCanvas.style.height   = _canvasH;

            // Start panned to (0,0)
            _panOffset = Vector2.zero;
            _treeCanvas.style.left = 0;
            _treeCanvas.style.top  = 0;

            // Line layer — absolute inside canvas
            _lineLayer = new VisualElement();
            _lineLayer.AddToClassList("line-layer");
            _lineLayer.style.width  = _canvasW;
            _lineLayer.style.height = _canvasH;
            _treeCanvas.Add(_lineLayer);

            // Connections
            var byId = new Dictionary<string, UpgradeNodeDefinition>();
            foreach (var n in _upgradeTree.Nodes) byId[n.NodeId] = n;

            foreach (var n in _upgradeTree.Nodes)
            {
                if (n.PrerequisiteNodeIDs == null) continue;
                foreach (var pre in n.PrerequisiteNodeIDs)
                    if (byId.ContainsKey(pre))
                        _connections.Add((pre, n.NodeId));
            }

            // Cards
            float off = (CellW - CardW) * 0.5f;
            foreach (var n in _upgradeTree.Nodes)
            {
                float px = PadX + n.GridX * CellW + off;
                float py = PadY + n.GridY * CellH + off;
                var card = MakeCard(n, px, py);
                _treeCanvas.Add(card);
                _cards[n.NodeId]   = card;
                _centres[n.NodeId] = new Vector2(px + CardW * 0.5f, py + CardH * 0.5f);
            }

            _lineLayer.generateVisualContent += DrawLines;
            RefreshAll();

            // Centre on Awakening. Viewport size may not be resolved yet on first open,
            // so try immediately and also after next layout pass.
            ApplyCentreOnAwakening();
            _viewport?.RegisterCallbackOnce<GeometryChangedEvent>(_ => ApplyCentreOnAwakening());
        }

        private void ApplyCentreOnAwakening()
        {
            if (_viewport == null) return;
            float vpW = _viewport.resolvedStyle.width;
            float vpH = _viewport.resolvedStyle.height;
            // If viewport hasn't resolved yet (NaN or 0), fall back to root minus topbar
            if (vpW <= 1f && _root != null)
            {
                vpW = _root.resolvedStyle.width;
                vpH = _root.resolvedStyle.height - 56f;
            }
            if (vpW <= 1f) return; // still not ready

            // Awakening: col 10, row 7
            float nodeX = PadX + 10 * CellW + CellW * 0.5f;
            float nodeY = PadY + 7  * CellH + CellH * 0.5f;
            SetPanRaw(new Vector2(vpW * 0.5f - nodeX * _zoom, vpH * 0.5f - nodeY * _zoom));
        }

        // ── Card ─────────────────────────────────────────────────────────────────

        private VisualElement MakeCard(UpgradeNodeDefinition node, float px, float py)
        {
            Color bc = CatColor(node.Category);

            var card = new VisualElement();
            card.style.position                = Position.Absolute;
            card.style.left                    = px;
            card.style.top                     = py;
            card.style.width                   = CardW;
            card.style.height                  = CardH;
            card.style.flexDirection           = FlexDirection.Column;
            card.style.alignItems              = Align.Center;
            card.style.justifyContent          = Justify.FlexStart;
            card.style.paddingTop              = 0;
            card.style.paddingBottom           = 0;
            card.style.paddingLeft             = 3;
            card.style.paddingRight            = 3;
            card.style.borderTopLeftRadius     = 6;
            card.style.borderTopRightRadius    = 6;
            card.style.borderBottomLeftRadius  = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderTopWidth          = 2;
            card.style.borderBottomWidth       = 2;
            card.style.borderLeftWidth         = 2;
            card.style.borderRightWidth        = 2;
            card.style.borderTopColor          = bc;
            card.style.borderBottomColor       = bc;
            card.style.borderLeftColor         = bc;
            card.style.borderRightColor        = bc;
            card.style.backgroundColor         = new Color(0.08f, 0.08f, 0.13f, 1f);
            card.style.overflow                = Overflow.Hidden;

            // ── Icon ──────────────────────────────────────────────────────────────
            Color iconCol = CatColorActive(node.Category);
            iconCol.a = 1f;
            var icon = new Label(Icon(node));
            icon.style.fontSize            = 18;
            icon.style.unityTextAlign      = TextAnchor.MiddleCenter;
            icon.style.color               = iconCol;
            icon.style.width               = CardW - 6;
            icon.style.height              = 22;
            icon.style.flexShrink          = 0;
            icon.style.marginTop           = 5;
            icon.style.marginBottom        = 0;
            icon.style.unityFont           = new StyleFont(_faFont);
            icon.style.unityFontDefinition = new StyleFontDefinition(new FontDefinition { font = _faFont });
            card.Add(icon);

            // ── Name label — takes ALL remaining space ────────────────────────────
            var lbl = new Label(node.DisplayName);
            lbl.style.fontSize                 = 10;
            lbl.style.unityFontStyleAndWeight  = FontStyle.Bold;
            lbl.style.unityTextAlign           = TextAnchor.MiddleCenter;
            lbl.style.whiteSpace               = WhiteSpace.Normal;
            lbl.style.width                    = CardW - 8;
            lbl.style.flexShrink               = 1;
            lbl.style.flexGrow                 = 1;
            lbl.style.color                    = new Color(0.90f, 0.88f, 0.85f, 1f);
            lbl.style.overflow                 = Overflow.Hidden;
            lbl.style.paddingLeft              = 2;
            lbl.style.paddingRight             = 2;
            card.Add(lbl);

            // ── Cost label — 13px fixed at bottom ────────────────────────────────
            var cost = new Label();
            cost.style.fontSize                = 10;
            cost.style.unityFontStyleAndWeight = FontStyle.Bold;
            cost.style.unityTextAlign          = TextAnchor.MiddleCenter;
            cost.style.color                   = new Color(1f, 0.72f, 0.21f, 1f);
            cost.style.width                   = CardW - 6;
            cost.style.height                  = 13;
            cost.style.flexShrink              = 0;
            cost.style.overflow                = Overflow.Hidden;
            cost.style.marginBottom            = 2;
            cost.name = "cost-" + node.NodeId;
            card.Add(cost);

            // ── Pips — 6px strip at very bottom ──────────────────────────────────
            var pips = new VisualElement();
            pips.style.flexDirection  = FlexDirection.Row;
            pips.style.justifyContent = Justify.Center;
            pips.style.alignItems     = Align.Center;
            pips.style.flexShrink     = 0;
            pips.style.height         = 6;
            pips.style.marginBottom   = 3;
            pips.name = "pips-" + node.NodeId;
            card.Add(pips);

            card.RegisterCallback<MouseEnterEvent>(_ => { if (!_dragging) ShowTooltip(node, px, py); });
            card.RegisterCallback<MouseLeaveEvent>(_ => HideTooltip());
            card.RegisterCallback<ClickEvent>(e => { e.StopPropagation(); if (!_dragging) TryPurchase(node.NodeId); });

            return card;
        }

        // ── Tooltip ───────────────────────────────────────────────────────────────

        private void ShowTooltip(UpgradeNodeDefinition node, float px, float py)
        {
            int  rank    = _ranks.TryGetValue(node.NodeId, out int r) ? r : 0;
            bool locked  = IsLocked(node);
            bool maxed   = rank >= node.MaxRank;
            double bal = EconomyService.CurrentResourcesStatic;

            _ttName.text  = node.DisplayName;
            _ttEffect.style.color = CatColorActive(node.Category);
            _ttEffect.text = node.EffectType == UpgradeEffectType.PercentBonus
                ? $"+{node.EffectPerRank * 100f:F0}%  {StatLabel(node)}"
                : $"+{node.EffectPerRank:F1}  {StatLabel(node)}";
            _ttRank.text   = maxed ? $"MAX  {node.MaxRank}/{node.MaxRank}" : $"{rank}/{node.MaxRank} ranks";

            _ttCost.RemoveFromClassList("tt-cost-locked");
            _ttCost.RemoveFromClassList("tt-cost-cant");
            if (maxed)
            {
                _ttCost.text = "Fully upgraded";
            }
            else if (locked)
            {
                _ttCost.text = "Locked";
                _ttCost.AddToClassList("tt-cost-locked");
            }
            else
            {
                long cost = _upgradeTree.CostForNextRank(node.NodeId, rank);
                _ttCost.text = $"Cost: {GoldFormatter.Format(cost)}";
                _ttCost.EnableInClassList("tt-cost-cant", bal < cost);
            }

            if (locked)
            {
                var miss = MissingPrereqs(node);
                _ttPrereq.text = miss.Count > 0 ? "Requires: " + string.Join(", ", miss) : "Requires prestige";
                _ttPrereq.style.display = DisplayStyle.Flex;
            }
            else _ttPrereq.style.display = DisplayStyle.None;

            // Border color matches node category
            Color catCol = CatColorActive(node.Category);
            _tooltip.style.borderTopColor    = catCol;
            _tooltip.style.borderBottomColor = catCol;
            _tooltip.style.borderLeftColor   = catCol;
            _tooltip.style.borderRightColor  = catCol;

            // Tooltip position = card position in canvas space → screen space
            float ttX = px * _zoom + _panOffset.x + CardW * _zoom + 8f;
            float ttY = py * _zoom + _panOffset.y + 56f; // 56 = topbar height
            _tooltip.style.left    = ttX;
            _tooltip.style.top     = ttY;
            _tooltip.style.display = DisplayStyle.Flex;
        }

        private void HideTooltip() => _tooltip.style.display = DisplayStyle.None;

        // ── Purchase ──────────────────────────────────────────────────────────────

        private void TryPurchase(string id)
        {
            if (_upgradeTree == null || _economy == null) return;
            if (!_upgradeTree.TryGetNode(id, out var node)) return;
            int rank = _ranks.TryGetValue(id, out int r) ? r : 0;
            if (rank >= node.MaxRank || IsLocked(node)) return;
            long cost = _upgradeTree.CostForNextRank(id, rank);
            if (_economy.CurrentResources < cost) return;

            _economy.DeductResources(cost);
            _ranks[id] = rank + 1;
            _saveService?.NotifyUpgradePurchased();
            UpdateGold();
            RefreshAll();

            if (_cards.TryGetValue(id, out var card))
                ShowTooltip(node, card.style.left.value.value, card.style.top.value.value);
        }

        // ── Refresh ───────────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (_upgradeTree == null) return;
            double bal = EconomyService.CurrentResourcesStatic;
            foreach (var kv in _cards)
            {
                if (!_upgradeTree.TryGetNode(kv.Key, out var n)) continue;
                int  rank   = _ranks.TryGetValue(kv.Key, out int r) ? r : 0;
                bool locked = IsLocked(n);
                RefreshCard(kv.Value, n, rank, bal, locked);
            }
            _lineLayer?.MarkDirtyRepaint();
        }

        private void RefreshCard(VisualElement card, UpgradeNodeDefinition node,
                                  int rank, double bal, bool locked)
        {
            bool maxed = rank >= node.MaxRank;
            float op = (locked && rank == 0) ? 0.25f : 1f;
            card.style.opacity = op;

            Color bc = maxed             ? new Color(1.00f, 0.73f, 0.21f, 1.0f)
                     : (rank > 0)        ? CatColorActive(node.Category)
                                         : CatColor(node.Category);
            card.style.borderTopColor    = bc;
            card.style.borderBottomColor = bc;
            card.style.borderLeftColor   = bc;
            card.style.borderRightColor  = bc;

            var pipRow = card.Q<VisualElement>("pips-" + node.NodeId);
            if (pipRow != null)
            {
                pipRow.Clear();
                int cnt = Mathf.Min(node.MaxRank, 5);
                for (int i = 0; i < cnt; i++)
                {
                    var pip = new VisualElement();
                    pip.style.width        = 5;
                    pip.style.height       = 5;
                    pip.style.borderTopLeftRadius     = 3;
                    pip.style.borderTopRightRadius    = 3;
                    pip.style.borderBottomLeftRadius  = 3;
                    pip.style.borderBottomRightRadius = 3;
                    pip.style.marginLeft   = 1;
                    pip.style.marginRight  = 1;
                    pip.style.borderTopWidth    = 1;
                    pip.style.borderBottomWidth = 1;
                    pip.style.borderLeftWidth   = 1;
                    pip.style.borderRightWidth  = 1;

                    if (i < rank && maxed)
                    {
                        pip.style.backgroundColor = new Color(1f, 0.73f, 0.21f, 1f);
                        pip.style.borderTopColor = pip.style.borderBottomColor =
                        pip.style.borderLeftColor = pip.style.borderRightColor = new Color(1f, 0.86f, 0.31f, 1f);
                    }
                    else if (i < rank)
                    {
                        pip.style.backgroundColor = new Color(1f, 0.42f, 0.21f, 1f);
                        pip.style.borderTopColor = pip.style.borderBottomColor =
                        pip.style.borderLeftColor = pip.style.borderRightColor = new Color(1f, 0.55f, 0.31f, 1f);
                    }
                    else
                    {
                        pip.style.backgroundColor = new Color(1f, 0.42f, 0.21f, 0.12f);
                        pip.style.borderTopColor = pip.style.borderBottomColor =
                        pip.style.borderLeftColor = pip.style.borderRightColor = new Color(1f, 0.42f, 0.21f, 0.28f);
                    }
                    pipRow.Add(pip);
                }
            }

            var costLbl = card.Q<Label>("cost-" + node.NodeId);
            if (costLbl != null)
            {
                if (maxed)
                {
                    costLbl.text  = "MAX";
                    costLbl.style.color = new Color(1f, 0.73f, 0.21f, 1f);
                }
                else if (locked)
                {
                    costLbl.text  = "";
                }
                else
                {
                    long cost = _upgradeTree.CostForNextRank(node.NodeId, rank);
                    costLbl.text = GoldFormatter.Format(cost);
                    costLbl.style.color = bal >= cost
                        ? new Color(1f, 0.42f, 0.21f, 0.9f)
                        : new Color(0.86f, 0.31f, 0.31f, 0.9f);
                }
            }
        }

        // ── Lines ─────────────────────────────────────────────────────────────────

        private void DrawLines(MeshGenerationContext ctx)
        {
            if (_upgradeTree == null) return;
            var p = ctx.painter2D;

            foreach (var (fromId, toId) in _connections)
            {
                if (!_centres.TryGetValue(fromId, out var fc)) continue;
                if (!_centres.TryGetValue(toId,   out var tc)) continue;

                _upgradeTree.TryGetNode(toId, out var toNode);
                int  toRank  = _ranks.TryGetValue(toId, out int tr) ? tr : 0;
                bool toLock  = IsLocked(toNode);
                bool toMax   = toRank >= toNode.MaxRank;

                Color col; float w;
                if      (toMax)     { col = new Color(1f, 0.73f, 0.21f, 0.95f); w = 2.5f; }
                else if (toRank>0)  { col = new Color(1f, 0.42f, 0.21f, 0.85f); w = 2f;   }
                else if (!toLock)   { col = new Color(0.7f, 0.7f, 0.65f, 0.30f); w = 1.5f; }
                else                { col = new Color(0.25f, 0.25f, 0.25f, 0.20f); w = 1f; }

                p.strokeColor = col;
                p.lineWidth   = w;
                p.lineCap     = LineCap.Round;

                float midX = (fc.x + tc.x) * 0.5f;
                p.BeginPath();
                p.MoveTo(fc);
                p.LineTo(new Vector2(midX, fc.y));
                p.LineTo(new Vector2(midX, tc.y));
                p.LineTo(tc);
                p.Stroke();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private bool IsLocked(UpgradeNodeDefinition node)
        {
            if (node.PrestigeGateRequirement > 0 &&
                (_prestigeManager == null || _prestigeManager.PrestigeCount < node.PrestigeGateRequirement)) return true;
            if (node.PrerequisiteNodeIDs != null)
                foreach (var id in node.PrerequisiteNodeIDs)
                    if ((_ranks.TryGetValue(id, out int pr) ? pr : 0) < 1) return true;
            return false;
        }

        private List<string> MissingPrereqs(UpgradeNodeDefinition node)
        {
            var list = new List<string>();
            if (node.PrerequisiteNodeIDs == null) return list;
            foreach (var id in node.PrerequisiteNodeIDs)
                if ((_ranks.TryGetValue(id, out int pr) ? pr : 0) < 1 &&
                    _upgradeTree.TryGetNode(id, out var pre))
                    list.Add(pre.DisplayName);
            return list;
        }

        private void UpdateGold()
        {
            if (_goldLabel != null)
                _goldLabel.text = GoldFormatter.Format(EconomyService.CurrentResourcesStatic);
        }

        private void OnGoldChanged(double bal, double delta) { UpdateGold(); RefreshAll(); }

        private static Color CatColor(UpgradeCategory c) => c switch
        {
            UpgradeCategory.Combat   => new Color(0.86f, 0.24f, 0.24f, 0.35f),
            UpgradeCategory.Survival => new Color(0.24f, 0.78f, 0.47f, 0.35f),
            UpgradeCategory.Economy  => new Color(0.24f, 0.71f, 1.00f, 0.35f),
            UpgradeCategory.Prestige => new Color(0.71f, 0.31f, 1.00f, 0.35f),
            _                        => new Color(1.00f, 0.42f, 0.21f, 0.35f),
        };

        private static Color CatColorActive(UpgradeCategory c) => c switch
        {
            UpgradeCategory.Combat   => new Color(0.86f, 0.24f, 0.24f, 0.85f),
            UpgradeCategory.Survival => new Color(0.24f, 0.78f, 0.47f, 0.85f),
            UpgradeCategory.Economy  => new Color(0.24f, 0.71f, 1.00f, 0.85f),
            UpgradeCategory.Prestige => new Color(0.71f, 0.31f, 1.00f, 0.85f),
            _                        => new Color(1.00f, 0.42f, 0.21f, 0.85f),
        };

        // SO field first, enum map as fallback — toolset users can override per-node in the SO
        private static string Icon(UpgradeNodeDefinition node)
        {
            if (!string.IsNullOrEmpty(node.IconUnicode)) return node.IconUnicode;
            return node.AffectedStat switch
            {
                StatType.Damage                => "\uf6cf",
                StatType.AttackInterval        => "\uf0e7",
                StatType.AttackRange           => "\uf140",
                StatType.CritChance            => "\uf005",
                StatType.AreaDamage            => "\uf1e2",
                StatType.MaxHP                 => "\uf004",
                StatType.MoveSpeed             => "\uf70c",
                StatType.DamageReduction       => "\uf3ed",
                StatType.HPRegen               => "\uf309",
                StatType.GoldDropMultiplier    => "\uf51e",
                StatType.GoldPickupRange       => "\uf076",
                StatType.BonusRunReward        => "\uf091",
                StatType.ComboMultiplier       => "\uf013",
                StatType.GeneratorSpeed        => "\uf013",
                StatType.OfflineYieldRate      => "\uf186",
                StatType.ActiveRunPassiveBonus => "\uf144",
                StatType.PrestigeMultiplier    => "\uf521",
                StatType.StartingGoldBonus     => "\uf3d1",
                StatType.RunDurationBonus      => "\uf017",
                StatType.DoubleGeneratorChance => "\uf24d",
                _                              => "\uf128",
            };
        }

        private static string StatLabel(UpgradeNodeDefinition node)
        {
            if (!string.IsNullOrEmpty(node.StatDisplayName)) return node.StatDisplayName;
            return node.AffectedStat switch
            {
                StatType.Damage                => "Damage",
                StatType.AttackInterval        => "Attack Speed",
                StatType.AttackRange           => "Attack Range",
                StatType.CritChance            => "Crit Chance",
                StatType.AreaDamage            => "Area Damage",
                StatType.MaxHP                 => "Max HP",
                StatType.MoveSpeed             => "Move Speed",
                StatType.DamageReduction       => "Damage Reduction",
                StatType.HPRegen               => "HP Regen",
                StatType.GoldDropMultiplier    => "Gold Drop",
                StatType.GoldPickupRange       => "Gold Pickup Range",
                StatType.BonusRunReward        => "Run Reward",
                StatType.ComboMultiplier       => "Combo Multiplier",
                StatType.GeneratorSpeed        => "Generator Speed",
                StatType.OfflineYieldRate      => "Offline Yield",
                StatType.ActiveRunPassiveBonus => "Run Passive Bonus",
                StatType.PrestigeMultiplier    => "Prestige Multiplier",
                StatType.StartingGoldBonus     => "Starting Gold",
                StatType.RunDurationBonus      => "Run Duration",
                StatType.DoubleGeneratorChance => "Double Generator",
                _                              => node.AffectedStat.ToString(),
            };
        }

        public void OnBeforeSave(SaveData d)
        {
            d.UpgradeNodeStates ??= new Dictionary<string, int>();
            foreach (var kv in _ranks) d.UpgradeNodeStates[kv.Key] = kv.Value;
        }

        public void OnAfterLoad(SaveData d)
        {
            _ranks.Clear();
            if (d.UpgradeNodeStates == null) return;
            foreach (var kv in d.UpgradeNodeStates) _ranks[kv.Key] = kv.Value;
        }

        private void SetVisible(bool v)
        {
            if (_root != null)
                _root.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

}
