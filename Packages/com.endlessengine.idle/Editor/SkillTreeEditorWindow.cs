using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;

namespace EndlessEngine.Editor
{
    public class SkillTreeEditorWindow : EditorWindow
    {
        private SkillTreeGraphView  _graphView;
        private SkillNodeInspector  _inspector;
        private SkillTreeConfigSO   _target;
        private bool                _dirty;
        private Label               _assetLabel;

        [MenuItem("Tools/Endless Engine/Skill Tree Editor")]
        public static void Open()
        {
            var win = GetWindow<SkillTreeEditorWindow>("Skill Tree Editor");
            win.minSize = new Vector2(1100, 600);
        }

        private void OnEnable() => BuildUI();

        private void Update()
        {
            if (_graphView == null) return;
            var node = _graphView.selection.OfType<SkillNodeView>().FirstOrDefault();
            if (node != null) _inspector?.ShowNode(node);
        }

        private void OnDisable()
        {
            if (_dirty && _target != null &&
                EditorUtility.DisplayDialog("Unsaved Changes", "Save before closing?", "Save", "Discard"))
                SaveAsset();
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            // ── Toolbar ───────────────────────────────────────────────────────────
            var toolbar = new VisualElement();
            toolbar.style.flexDirection     = FlexDirection.Row;
            toolbar.style.alignItems        = Align.Center;
            toolbar.style.height            = 32;
            toolbar.style.flexShrink        = 0;
            toolbar.style.backgroundColor   = new Color(0.18f, 0.18f, 0.18f);
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            toolbar.style.paddingLeft       = 8;
            toolbar.style.paddingRight      = 8;

            toolbar.Add(Btn("Load Asset", null,                           OnLoadClicked));
            toolbar.Add(Btn("New Tree",   null,                           OnNewClicked));
            toolbar.Add(Btn("+ Add Node", new Color(0.2f, 0.35f, 0.55f), OnAddClicked));
            toolbar.Add(Btn("Frame All",  null,                           () => _graphView?.FrameAll()));

            _assetLabel = new Label("No asset loaded");
            _assetLabel.style.color      = new Color(0.6f, 0.6f, 0.6f);
            _assetLabel.style.marginLeft = 12;
            _assetLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            toolbar.Add(_assetLabel);

            var spacer = new VisualElement(); spacer.style.flexGrow = 1;
            toolbar.Add(spacer);
            toolbar.Add(Btn("Save", new Color(0.2f, 0.5f, 0.2f), OnSaveClicked));
            rootVisualElement.Add(toolbar);

            // ── Body ──────────────────────────────────────────────────────────────
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexGrow      = 1;
            rootVisualElement.Add(row);

            _graphView = new SkillTreeGraphView(this);
            _graphView.style.flexGrow = 1;
            row.Add(_graphView);

            _inspector = new SkillNodeInspector(this);
            _inspector.style.width           = 290;
            _inspector.style.flexShrink      = 0;
            _inspector.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
            _inspector.style.borderLeftWidth = 1;
            _inspector.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);
            row.Add(_inspector);

            // Auto-load if exactly one SkillTreeConfigSO exists
            var guids = AssetDatabase.FindAssets("t:SkillTreeConfigSO");
            if (guids.Length == 1)
                LoadAsset(AssetDatabase.LoadAssetAtPath<SkillTreeConfigSO>(
                    AssetDatabase.GUIDToAssetPath(guids[0])));
        }

        private static Button Btn(string label, Color? bg, System.Action action)
        {
            var b = new Button(action) { text = label };
            b.style.marginRight             = 4;
            b.style.paddingLeft             = 10;
            b.style.paddingRight            = 10;
            b.style.height                  = 22;
            b.style.borderTopLeftRadius     = 3;
            b.style.borderTopRightRadius    = 3;
            b.style.borderBottomLeftRadius  = 3;
            b.style.borderBottomRightRadius = 3;
            if (bg.HasValue) b.style.backgroundColor = bg.Value;
            return b;
        }

        // ── Toolbar actions ───────────────────────────────────────────────────────

        private void OnLoadClicked()
        {
            var path = EditorUtility.OpenFilePanel("Select SkillTreeConfigSO", "Assets/Configs", "asset");
            if (string.IsNullOrEmpty(path)) return;
            path = "Assets" + path.Substring(Application.dataPath.Length);
            var so = AssetDatabase.LoadAssetAtPath<SkillTreeConfigSO>(path);
            if (so != null) LoadAsset(so);
        }

        private void OnNewClicked()
        {
            var path = EditorUtility.SaveFilePanelInProject("New Skill Tree", "SkillTreeConfig", "asset", "");
            if (string.IsNullOrEmpty(path)) return;
            var so = ScriptableObject.CreateInstance<SkillTreeConfigSO>();
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();
            LoadAsset(so);
        }

        private void OnSaveClicked() => SaveAsset();

        private void OnAddClicked()
        {
            if (_target == null) { EditorUtility.DisplayDialog("No Asset", "Load an asset first.", "OK"); return; }
            _graphView.AddNewNode();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void LoadAsset(SkillTreeConfigSO so)
        {
            _target = so;
            _dirty  = false;
            _assetLabel.text = so.name;
            _graphView.Populate(so);
            _inspector.Clear();
        }

        public void SaveAsset()
        {
            if (_target == null) return;
            _graphView.FlushToSO(_target);
            EditorUtility.SetDirty(_target);
            AssetDatabase.SaveAssets();
            _dirty = false;
            _assetLabel.text = _target.name;
        }

        public void MarkDirty()
        {
            _dirty = true;
            _assetLabel.text = (_target?.name ?? "?") + " *";
        }

        public void AddNodeAtCenter() => _graphView?.AddNewNode();

        public SkillTreeConfigSO  Target    => _target;
        public SkillNodeInspector Inspector => _inspector;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Graph View
    // ══════════════════════════════════════════════════════════════════════════════

    public class SkillTreeGraphView : GraphView
    {
        private const float GridSize = 130f;

        private readonly SkillTreeEditorWindow            _window;
        private readonly Dictionary<string, SkillNodeView> _views = new();

        public SkillTreeGraphView(SkillTreeEditorWindow window)
        {
            _window = window;

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            this.AddManipulator(new ContextualMenuManipulator(ctx =>
            {
                ctx.menu.AppendAction("Add Node", _ => _window.AddNodeAtCenter());
                ctx.menu.AppendAction("Frame All", _ => FrameAll());
            }));

            style.flexGrow = 1;

            // Reuse upgrade tree stylesheet if present
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/UpgradeTreeEditor.uss");
            if (uss != null) styleSheets.Add(uss);

            deleteSelection   = OnDeleteSelection;
            graphViewChanged += OnGraphViewChanged;
        }

        public void Populate(SkillTreeConfigSO so)
        {
            graphElements.ForEach(RemoveElement);
            _views.Clear();

            if (so.Nodes == null) return;

            foreach (var node in so.Nodes)
                if (node != null) CreateNodeView(node);

            schedule.Execute(() =>
            {
                if (so.Nodes == null) return;
                foreach (var node in so.Nodes)
                {
                    if (node?.PrerequisiteIds == null) continue;
                    if (!_views.TryGetValue(node.NodeId, out var toView)) continue;
                    foreach (var pid in node.PrerequisiteIds)
                    {
                        if (!_views.TryGetValue(pid, out var fromView)) continue;
                        var edge = new Edge { output = fromView.OutPort, input = toView.InPort };
                        edge.output.Connect(edge);
                        edge.input.Connect(edge);
                        AddElement(edge);
                    }
                }
                schedule.Execute(_ => FrameAll()).ExecuteLater(50);
            }).ExecuteLater(100);
        }

        public void AddNewNode()
        {
            if (_window?.Target == null) return;

            // Generate unique ID
            int idx = (_window.Target.Nodes?.Length ?? 0) + 1;
            string id = $"node_{idx:D3}";
            var existing = _window.Target.Nodes ?? new SkillNodeConfigSO[0];
            while (existing.Any(n => n != null && n.NodeId == id)) id = $"node_{++idx:D3}";

            var path = EditorUtility.SaveFilePanelInProject(
                "New Skill Node", id, "asset",
                "Save the new SkillNodeConfigSO asset", "Assets/Configs");
            if (string.IsNullOrEmpty(path)) return;

            var nodeSO = ScriptableObject.CreateInstance<SkillNodeConfigSO>();
            nodeSO.NodeId      = id;
            nodeSO.DisplayName = "New Node";
            nodeSO.PointCost   = 1;
            nodeSO.Refundable  = true;
            nodeSO.EditorPosition = new Vector2(200, 200);
            AssetDatabase.CreateAsset(nodeSO, path);
            AssetDatabase.SaveAssets();

            // Append to tree's node array
            var list = (_window.Target.Nodes ?? new SkillNodeConfigSO[0]).ToList();
            list.Add(nodeSO);
            _window.Target.Nodes = list.ToArray();
            _window.MarkDirty();

            var view = CreateNodeView(nodeSO);
            ClearSelection();
            AddToSelection(view);
        }

        private SkillNodeView CreateNodeView(SkillNodeConfigSO node)
        {
            var view = new SkillNodeView(node, _window);
            view.SetPosition(new Rect(node.EditorPosition.x, node.EditorPosition.y, 170, 110));
            AddElement(view);
            _views[node.NodeId] = view;
            return view;
        }

        /// <summary>
        /// Writes current graph state (positions, prerequisite edges) back to the SO.
        /// Does NOT save sub-assets — that is done by SaveAsset().
        /// </summary>
        public void FlushToSO(SkillTreeConfigSO so)
        {
            foreach (var kv in _views)
            {
                var node = kv.Value.Node;
                var rect = kv.Value.GetPosition();
                node.EditorPosition = new Vector2(rect.x, rect.y);

                // Rebuild prerequisite list from incoming edges
                var prereqs = kv.Value.InPort.connections
                    .Select(e => (e.output?.node as SkillNodeView)?.Node.NodeId)
                    .Where(pid => pid != null)
                    .ToList();
                node.PrerequisiteIds = prereqs;
                EditorUtility.SetDirty(node);
            }

            // Order nodes in the SO array to match current view order (stable)
            so.Nodes = _views.Values.Select(v => v.Node).ToArray();
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(p =>
            {
                if (p == startPort)           return false;
                if (p.node == startPort.node) return false;
                if (p.direction == startPort.direction) return false;
                return true;
            }).ToList();
        }

        private void OnDeleteSelection(string op, AskUser ask)
        {
            var delEdges = selection.OfType<Edge>().ToList();
            var delNodes = selection.OfType<SkillNodeView>().ToList();

            foreach (var e in delEdges)
            {
                e.input?.Disconnect(e);
                e.output?.Disconnect(e);
                RemoveElement(e);
            }
            foreach (var n in delNodes)
            {
                edges.ToList()
                    .Where(e => e.input?.node == n || e.output?.node == n)
                    .ToList()
                    .ForEach(e => { e.input?.Disconnect(e); e.output?.Disconnect(e); RemoveElement(e); });
                _views.Remove(n.Node.NodeId);
                RemoveElement(n);
            }
            if (delEdges.Count > 0 || delNodes.Count > 0) _window.MarkDirty();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if ((change.edgesToCreate?.Count > 0) ||
                (change.elementsToRemove?.Count > 0) ||
                (change.movedElements?.Count > 0))
                _window.MarkDirty();
            return change;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Node View
    // ══════════════════════════════════════════════════════════════════════════════

    public class SkillNodeView : Node
    {
        public SkillNodeConfigSO Node;
        public Port              InPort;
        public Port              OutPort;

        private readonly SkillTreeEditorWindow _window;
        private Label _costLabel;
        private Label _effectLabel;

        // Rarity-style colors by effect type
        private static readonly Dictionary<SkillEffectType, Color> EffectColors = new()
        {
            { SkillEffectType.StatMultiplier, new Color(0.24f, 0.71f, 1.00f) },  // blue
            { SkillEffectType.StatAdditive,   new Color(0.24f, 0.78f, 0.47f) },  // green
            { SkillEffectType.UnlockFeature,  new Color(0.71f, 0.31f, 1.00f) },  // purple
            { SkillEffectType.IncomeBonus,    new Color(1.00f, 0.80f, 0.20f) },  // gold
        };

        public SkillNodeView(SkillNodeConfigSO node, SkillTreeEditorWindow window)
        {
            Node    = node;
            _window = window;
            title   = node.DisplayName;
            style.width     = 170;
            style.minHeight = 110;

            // Title bar color (first effect type drives color, or default grey)
            Color titleCol = new Color(0.3f, 0.3f, 0.3f);
            if (node.Effects?.Count > 0 && EffectColors.TryGetValue(node.Effects[0].Type, out var ec))
                titleCol = new Color(ec.r * 0.4f, ec.g * 0.4f, ec.b * 0.4f);

            var titleBar = this.Q<VisualElement>("title");
            if (titleBar != null) titleBar.style.backgroundColor = titleCol;

            // Cost label
            _costLabel = new Label($"Cost: {node.PointCost} pt{(node.PointCost != 1 ? "s" : "")}");
            _costLabel.style.fontSize  = 9;
            _costLabel.style.color     = new Color(0.9f, 0.75f, 0.3f);
            _costLabel.style.marginLeft = 4;
            _costLabel.style.marginTop  = 2;
            extensionContainer.Add(_costLabel);

            // Effect summary
            _effectLabel = new Label(BuildEffectSummary(node));
            _effectLabel.style.fontSize  = 9;
            _effectLabel.style.color     = new Color(0.75f, 0.75f, 0.75f);
            _effectLabel.style.marginLeft  = 4;
            _effectLabel.style.marginBottom = 4;
            _effectLabel.style.whiteSpace   = WhiteSpace.Normal;
            extensionContainer.Add(_effectLabel);

            // Ports
            InPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input,
                Port.Capacity.Multi, typeof(bool));
            InPort.portName = "";
            inputContainer.Add(InPort);

            OutPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output,
                Port.Capacity.Multi, typeof(bool));
            OutPort.portName = "";
            outputContainer.Add(OutPort);

            RefreshExpandedState();
            RefreshPorts();
        }

        public void RefreshDisplay()
        {
            title = Node.DisplayName;
            _costLabel.text   = $"Cost: {Node.PointCost} pt{(Node.PointCost != 1 ? "s" : "")}";
            _effectLabel.text = BuildEffectSummary(Node);

            Color titleCol = new Color(0.3f, 0.3f, 0.3f);
            if (Node.Effects?.Count > 0 && EffectColors.TryGetValue(Node.Effects[0].Type, out var ec))
                titleCol = new Color(ec.r * 0.4f, ec.g * 0.4f, ec.b * 0.4f);
            var titleBar = this.Q<VisualElement>("title");
            if (titleBar != null) titleBar.style.backgroundColor = titleCol;
        }

        private static string BuildEffectSummary(SkillNodeConfigSO node)
        {
            if (node.Effects == null || node.Effects.Count == 0) return "(no effects)";
            var parts = node.Effects.Select(e => e.Type switch
            {
                SkillEffectType.StatMultiplier => $"×{e.Value:F2} {e.TargetId}",
                SkillEffectType.StatAdditive   => $"+{e.Value:F1} {e.TargetId}",
                SkillEffectType.UnlockFeature  => $"Unlock: {e.TargetId}",
                SkillEffectType.IncomeBonus    => $"+{e.Value:F1}/s {e.TargetId}",
                _                              => e.TargetId
            });
            return string.Join("\n", parts);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Inspector
    // ══════════════════════════════════════════════════════════════════════════════

    public class SkillNodeInspector : VisualElement
    {
        private readonly SkillTreeEditorWindow _window;
        private SkillNodeView _current;

        private TextField    _fNodeId, _fDisplayName, _fDescription;
        private IntegerField _fPointCost;
        private Toggle       _fRefundable;
        private Label        _fPrereqLabel;
        private Label        _fEffectsLabel;
        private ScrollView   _scroll;

        public SkillNodeInspector(SkillTreeEditorWindow window)
        {
            _window = window;
            style.overflow = Overflow.Hidden;

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;
            _scroll.contentContainer.style.paddingTop    = 10;
            _scroll.contentContainer.style.paddingBottom = 10;
            _scroll.contentContainer.style.paddingLeft   = 10;
            _scroll.contentContainer.style.paddingRight  = 10;
            base.Add(_scroll);

            BuildFields();
            Clear();
        }

        public new void Add(VisualElement e) => _scroll?.contentContainer.Add(e);

        private void BuildFields()
        {
            Add(Hdr("Skill Node Inspector"));

            Add(Sec("Identity"));
            _fNodeId      = F(new TextField("Node ID"));
            _fDisplayName = F(new TextField("Display Name"));
            _fDescription = F(new TextField("Description") { multiline = true });
            _fDescription.style.height = 52;

            Add(Sec("Cost & Refund"));
            _fPointCost  = F(new IntegerField("Point Cost"));
            _fRefundable = F(new Toggle("Refundable"));

            Add(Sec("Prerequisites (edit via graph edges)"));
            _fPrereqLabel = new Label("(none)");
            _fPrereqLabel.style.color     = new Color(0.5f, 0.5f, 0.5f);
            _fPrereqLabel.style.fontSize  = 10;
            _fPrereqLabel.style.whiteSpace = WhiteSpace.Normal;
            _fPrereqLabel.style.marginLeft = 4;
            Add(_fPrereqLabel);

            Add(Sec("Effects (edit SO directly)"));
            _fEffectsLabel = new Label("");
            _fEffectsLabel.style.color     = new Color(0.7f, 0.7f, 0.7f);
            _fEffectsLabel.style.fontSize  = 9;
            _fEffectsLabel.style.whiteSpace = WhiteSpace.Normal;
            _fEffectsLabel.style.marginLeft = 4;
            Add(_fEffectsLabel);

            var openBtn = new Button(OpenInInspector) { text = "Open SO in Inspector" };
            openBtn.style.marginTop = 8;
            Add(openBtn);

            // Callbacks
            _fNodeId.RegisterValueChangedCallback(e =>
            {
                if (_current == null) return;
                _current.Node.NodeId = e.newValue;
                _window.MarkDirty();
            });
            _fDisplayName.RegisterValueChangedCallback(e =>
            {
                if (_current == null) return;
                _current.Node.DisplayName = e.newValue;
                _current.RefreshDisplay();
                _window.MarkDirty();
            });
            _fDescription.RegisterValueChangedCallback(e =>
            {
                if (_current == null) return;
                _current.Node.Description = e.newValue;
                _window.MarkDirty();
            });
            _fPointCost.RegisterValueChangedCallback(e =>
            {
                if (_current == null) return;
                _current.Node.PointCost = Mathf.Max(0, e.newValue);
                _current.RefreshDisplay();
                _window.MarkDirty();
            });
            _fRefundable.RegisterValueChangedCallback(e =>
            {
                if (_current == null) return;
                _current.Node.Refundable = e.newValue;
                _window.MarkDirty();
            });
        }

        private void OpenInInspector()
        {
            if (_current?.Node != null)
                Selection.activeObject = _current.Node;
        }

        public void ShowNode(SkillNodeView view)
        {
            if (_current == view) return;
            _current = view;
            var n = view.Node;
            SetEnabled(true);
            _fNodeId.SetValueWithoutNotify(n.NodeId ?? "");
            _fDisplayName.SetValueWithoutNotify(n.DisplayName ?? "");
            _fDescription.SetValueWithoutNotify(n.Description ?? "");
            _fPointCost.SetValueWithoutNotify(n.PointCost);
            _fRefundable.SetValueWithoutNotify(n.Refundable);

            _fPrereqLabel.text = n.PrerequisiteIds?.Count > 0
                ? string.Join(", ", n.PrerequisiteIds)
                : "(none)";

            if (n.Effects?.Count > 0)
            {
                _fEffectsLabel.text = string.Join("\n", n.Effects.Select(e =>
                    $"[{e.Type}] {e.TargetId}: {e.Value}"));
            }
            else
            {
                _fEffectsLabel.text = "(none — click 'Open SO in Inspector' to add effects)";
            }
        }

        public new void Clear()
        {
            _current = null;
            SetEnabled(false);
        }

        private static Label Hdr(string t)
        {
            var l = new Label(t);
            l.style.fontSize = 13;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = new Color(0.85f, 0.85f, 0.85f);
            l.style.marginTop = 4; l.style.marginBottom = 8;
            return l;
        }

        private static Label Sec(string t)
        {
            var l = new Label(t.ToUpper());
            l.style.fontSize = 9;
            l.style.color    = new Color(0.5f, 0.5f, 0.5f);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 8; l.style.marginBottom = 2;
            l.style.borderBottomWidth = 1;
            l.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);
            return l;
        }

        private T F<T>(T field) where T : VisualElement
        {
            field.style.marginBottom = 2;
            Add(field);
            return field;
        }
    }
}
