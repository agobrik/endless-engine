using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;

namespace EndlessEngine.Editor
{
    public class UpgradeTreeEditorWindow : EditorWindow
    {
        private UpgradeTreeGraphView _graphView;
        private UpgradeNodeInspector _inspector;
        private UpgradeTreeConfigSO  _target;
        private bool                 _dirty;
        private Label                _assetLabel;
        private Toggle               _progressiveRevealToggle;

        [MenuItem("Tools/Endless Engine/Upgrade Tree Editor")]
        public static void Open()
        {
            var win = GetWindow<UpgradeTreeEditorWindow>("Upgrade Tree Editor");
            win.minSize = new Vector2(1100, 600);
        }

        private void OnEnable() => BuildUI();

        private void Update()
        {
            if (_graphView == null) return;
            var node = _graphView.selection.OfType<UpgradeNodeView>().FirstOrDefault();
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

            _progressiveRevealToggle = new Toggle("Progressive Reveal");
            _progressiveRevealToggle.style.marginLeft  = 16;
            _progressiveRevealToggle.style.color       = new Color(0.7f, 0.7f, 0.7f);
            _progressiveRevealToggle.SetEnabled(false);
            _progressiveRevealToggle.RegisterValueChangedCallback(e =>
            {
                if (_target == null) return;
                _target.ProgressiveReveal = e.newValue;
                MarkDirty();
            });
            toolbar.Add(_progressiveRevealToggle);

            var spacer = new VisualElement(); spacer.style.flexGrow = 1;
            toolbar.Add(spacer);
            toolbar.Add(Btn("Save", new Color(0.2f, 0.5f, 0.2f), OnSaveClicked));
            rootVisualElement.Add(toolbar);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexGrow      = 1;
            rootVisualElement.Add(row);

            _graphView = new UpgradeTreeGraphView(this);
            _graphView.style.flexGrow = 1;
            row.Add(_graphView);

            _inspector = new UpgradeNodeInspector(this);
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/UpgradeTreeEditor.uss");
            if (uss != null) _inspector.styleSheets.Add(uss);
            _inspector.style.width           = 290;
            _inspector.style.flexShrink      = 0;
            _inspector.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
            _inspector.style.borderLeftWidth = 1;
            _inspector.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);
            row.Add(_inspector);

            var guids = AssetDatabase.FindAssets("t:UpgradeTreeConfigSO");
            if (guids.Length == 1)
                LoadAsset(AssetDatabase.LoadAssetAtPath<UpgradeTreeConfigSO>(
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

        private void OnLoadClicked()
        {
            var path = EditorUtility.OpenFilePanel("Select UpgradeTreeConfigSO", "Assets/Configs", "asset");
            if (string.IsNullOrEmpty(path)) return;
            path = "Assets" + path.Substring(Application.dataPath.Length);
            var so = AssetDatabase.LoadAssetAtPath<UpgradeTreeConfigSO>(path);
            if (so != null) LoadAsset(so);
        }

        private void OnNewClicked()
        {
            var path = EditorUtility.SaveFilePanelInProject("New Upgrade Tree", "UpgradeTreeConfig", "asset", "");
            if (string.IsNullOrEmpty(path)) return;
            var so = ScriptableObject.CreateInstance<UpgradeTreeConfigSO>();
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

        public void LoadAsset(UpgradeTreeConfigSO so)
        {
            _target = so; _dirty = false;
            _assetLabel.text = so.name;
            _progressiveRevealToggle.SetEnabled(true);
            _progressiveRevealToggle.SetValueWithoutNotify(so.ProgressiveReveal);
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

        public UpgradeTreeConfigSO  Target    => _target;
        public UpgradeNodeInspector Inspector => _inspector;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Graph View
    // ══════════════════════════════════════════════════════════════════════════════

    public class UpgradeTreeGraphView : GraphView
    {
        private const float GridSize = 120f;

        private readonly UpgradeTreeEditorWindow             _window;
        private readonly Dictionary<string, UpgradeNodeView> _views = new();

        public UpgradeTreeGraphView(UpgradeTreeEditorWindow window)
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

            // Load edge style override (--control-point-ratio: 0 = straight lines)
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Editor/UpgradeTreeEditor.uss"));

            deleteSelection   = OnDeleteSelection;
            graphViewChanged += OnGraphViewChanged;
        }

        public void Populate(UpgradeTreeConfigSO so)
        {
            graphElements.ForEach(RemoveElement);
            _views.Clear();

            foreach (var def in so.Nodes)
                CreateNodeView(def);

            schedule.Execute(() =>
            {
                foreach (var def in so.Nodes)
                {
                    if (def.PrerequisiteNodeIDs == null) continue;
                    if (!_views.TryGetValue(def.NodeId, out var toView)) continue;
                    foreach (var pid in def.PrerequisiteNodeIDs)
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
            int idx = _window.Target.Nodes.Count + 1;
            string id = $"node_{idx:D3}";
            while (_window.Target.Nodes.Any(n => n.NodeId == id)) id = $"node_{++idx:D3}";

            var def = new UpgradeNodeDefinition
            {
                NodeId = id, DisplayName = "New Node",
                Category = UpgradeCategory.Production, AffectedStat = StatType.Damage,
                EffectPerRank = 0.1f, EffectType = UpgradeEffectType.PercentBonus,
                MaxRank = 5, BaseCost = 100f, CostScalingFactor = 1.5f, SelectionWeight = 10f,
                GridX = 5, GridY = 5, MaxOutgoingEdges = 0,
            };
            _window.Target.Nodes.Add(def);
            _window.MarkDirty();

            var view = CreateNodeView(def);
            ClearSelection();
            AddToSelection(view);
        }

        private UpgradeNodeView CreateNodeView(UpgradeNodeDefinition def)
        {
            var view = new UpgradeNodeView(def, _window);
            view.SetPosition(new Rect(def.GridX * GridSize, def.GridY * GridSize, 160, 100));
            AddElement(view);
            _views[def.NodeId] = view;
            return view;
        }

        public void FlushToSO(UpgradeTreeConfigSO so)
        {
            var updated = new List<UpgradeNodeDefinition>();
            foreach (var kv in _views)
            {
                var def = kv.Value.Def;
                var pos = kv.Value.GetPosition();
                def.GridX = Mathf.RoundToInt(pos.x / GridSize);
                def.GridY = Mathf.RoundToInt(pos.y / GridSize);

                var prereqs = kv.Value.InPort.connections
                    .Select(e => (e.output?.node as UpgradeNodeView)?.Def.NodeId)
                    .Where(id => id != null).ToArray();
                def.PrerequisiteNodeIDs = prereqs.Length > 0 ? prereqs : null;
                updated.Add(def);
            }
            so.Nodes = updated;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(p =>
            {
                if (p == startPort) return false;
                if (p.node == startPort.node) return false;
                if (p.direction == startPort.direction) return false;

                // Enforce MaxOutgoingEdges on the output port's node
                var outPort = startPort.direction == Direction.Output ? startPort : p;
                if (outPort.node is UpgradeNodeView outNode)
                {
                    int max = outNode.Def.MaxOutgoingEdges;
                    if (max > 0 && outPort.connections.Count() >= max) return false;
                }
                return true;
            }).ToList();
        }

        private void OnDeleteSelection(string op, AskUser ask)
        {
            var delEdges = selection.OfType<Edge>().ToList();
            var delNodes = selection.OfType<UpgradeNodeView>().ToList();

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
                _views.Remove(n.Def.NodeId);
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

    public class UpgradeNodeView : Node
    {
        public UpgradeNodeDefinition Def;
        public Port InPort;
        public Port OutPort;

        private readonly UpgradeTreeEditorWindow _window;
        private Label _catLabel;
        private Label _effectLabel;

        private static readonly Dictionary<UpgradeCategory, Color> CatColors = new()
        {
            { UpgradeCategory.Production, new Color(1.00f, 0.55f, 0.20f) },
            { UpgradeCategory.Combat,     new Color(0.86f, 0.24f, 0.24f) },
            { UpgradeCategory.Survival,   new Color(0.24f, 0.78f, 0.47f) },
            { UpgradeCategory.Economy,    new Color(0.24f, 0.71f, 1.00f) },
            { UpgradeCategory.Prestige,   new Color(0.71f, 0.31f, 1.00f) },
        };

        public UpgradeNodeView(UpgradeNodeDefinition def, UpgradeTreeEditorWindow window)
        {
            Def     = def;
            _window = window;
            title   = def.DisplayName;
            style.width     = 160;
            style.minHeight = 100;

            // Title bar color
            var titleBar = this.Q<VisualElement>("title");
            if (titleBar != null && CatColors.TryGetValue(def.Category, out var col))
                titleBar.style.backgroundColor = new Color(col.r * 0.4f, col.g * 0.4f, col.b * 0.4f);

            // Category + effect labels
            _catLabel = new Label(def.Category.ToString());
            _catLabel.style.fontSize = 9;
            _catLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _catLabel.style.marginLeft = 4;
            _catLabel.style.marginTop  = 2;
            _catLabel.style.color = CatColors.TryGetValue(def.Category, out var c2) ? c2 : Color.white;
            extensionContainer.Add(_catLabel);

            _effectLabel = new Label(EffectSummary(def));
            _effectLabel.style.fontSize    = 9;
            _effectLabel.style.color       = new Color(0.8f, 0.8f, 0.8f);
            _effectLabel.style.marginLeft  = 4;
            _effectLabel.style.marginBottom = 4;
            _effectLabel.style.whiteSpace  = WhiteSpace.Normal;
            extensionContainer.Add(_effectLabel);

            // Ports — before RefreshExpandedState
            int outCap = def.MaxOutgoingEdges > 0 ? def.MaxOutgoingEdges : 999;
            InPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input,
                Port.Capacity.Multi, typeof(bool));
            InPort.portName = "";
            inputContainer.Add(InPort);

            OutPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output,
                outCap == 1 ? Port.Capacity.Single : Port.Capacity.Multi, typeof(bool));
            OutPort.portName = "";
            outputContainer.Add(OutPort);

            RefreshExpandedState();
            RefreshPorts();

            // Style the edge lines straight (orthogonal-ish via USS override)
            // GraphView draws bezier by default; we override edge color via stylesheet below
        }

        public void RefreshDisplay()
        {
            title = Def.DisplayName;
            _catLabel.text    = Def.Category.ToString();
            _effectLabel.text = EffectSummary(Def);

            if (CatColors.TryGetValue(Def.Category, out var col))
            {
                var titleBar = this.Q<VisualElement>("title");
                if (titleBar != null)
                    titleBar.style.backgroundColor = new Color(col.r * 0.4f, col.g * 0.4f, col.b * 0.4f);
                _catLabel.style.color = col;
            }

            // Rebuild OutPort capacity if MaxOutgoingEdges changed
            int outCap = Def.MaxOutgoingEdges > 0 ? Def.MaxOutgoingEdges : 999;
            // Port.capacity is read-only; MaxOutgoingEdges is enforced via GetCompatiblePorts
        }

        private static string EffectSummary(UpgradeNodeDefinition d)
        {
            string val = d.EffectType == UpgradeEffectType.PercentBonus
                ? $"+{d.EffectPerRank * 100f:F0}%"
                : $"+{d.EffectPerRank:F1}";
            return $"{val} {d.AffectedStat}  (x{d.MaxRank})";
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Inspector
    // ══════════════════════════════════════════════════════════════════════════════

    public class UpgradeNodeInspector : VisualElement
    {
        private readonly UpgradeTreeEditorWindow _window;
        private UpgradeNodeView _current;

        private TextField    _fNodeId, _fDisplayName, _fDescription;
        private EnumField    _fCategory, _fAffectedStat, _fEffectType;
        private FloatField   _fEffectPerRank, _fBaseCost, _fCostScaling, _fSelectionWeight;
        private IntegerField _fMaxRank, _fPrestigeGate, _fGridX, _fGridY, _fMaxOutEdges;
        private ObjectField  _fCustomData;
        private Label        _prereqLabel;
        private IconPickerField _fIconPicker;
        private TextField    _fStatDisplayName;

        private ScrollView _scroll;

        public UpgradeNodeInspector(UpgradeTreeEditorWindow window)
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

        // Route Add() calls into the scroll view
        public new void Add(VisualElement e) => _scroll?.contentContainer.Add(e);

        private void BuildFields()
        {
            Add(Hdr("Node Inspector"));

            Add(Sec("Identity"));
            _fNodeId      = F(new TextField("Node ID"));
            _fDisplayName = F(new TextField("Display Name"));
            _fDescription = F(new TextField("Description") { multiline = true });
            _fDescription.style.height = 48;

            Add(Sec("Stats"));
            _fCategory      = F(new EnumField("Category",    UpgradeCategory.Production));
            _fAffectedStat  = F(new EnumField("Stat",        StatType.Damage));
            _fEffectType    = F(new EnumField("Effect Type", UpgradeEffectType.PercentBonus));
            _fEffectPerRank = F(new FloatField("Per Rank"));
            _fMaxRank       = F(new IntegerField("Max Rank"));

            Add(Sec("Economy"));
            _fBaseCost        = F(new FloatField("Base Cost"));
            _fCostScaling     = F(new FloatField("Cost Scale"));
            _fSelectionWeight = F(new FloatField("Card Weight"));
            _fPrestigeGate    = F(new IntegerField("Prestige Gate"));

            Add(Sec("Tree Behaviour"));
            _fMaxOutEdges = F(new IntegerField("Max Out Edges"));

            Add(Sec("Display"));
            _fIconPicker      = F(new IconPickerField());
            _fStatDisplayName = F(new TextField("Stat Label"));

            Add(Sec("Layout"));
            _fGridX = F(new IntegerField("Grid X"));
            _fGridY = F(new IntegerField("Grid Y"));

            Add(Sec("Custom Data"));
            _fCustomData = F(new ObjectField("SO Reference") { objectType = typeof(ScriptableObject), allowSceneObjects = false });

            Add(Sec("Prerequisites"));
            _prereqLabel = new Label("(none)");
            _prereqLabel.style.color     = new Color(0.5f, 0.5f, 0.5f);
            _prereqLabel.style.fontSize  = 10;
            _prereqLabel.style.whiteSpace = WhiteSpace.Normal;
            _prereqLabel.style.marginLeft = 4;
            Add(_prereqLabel);

            _fNodeId.RegisterValueChangedCallback(e              => Set(d => { d.NodeId = e.newValue; return d; }));
            _fDisplayName.RegisterValueChangedCallback(e         => Set(d => { d.DisplayName = e.newValue; return d; }, true));
            _fDescription.RegisterValueChangedCallback(e         => Set(d => { d.Description = e.newValue; return d; }));
            _fCategory.RegisterValueChangedCallback(e            => Set(d => { d.Category = (UpgradeCategory)e.newValue; return d; }, true));
            _fAffectedStat.RegisterValueChangedCallback(e        => Set(d => { d.AffectedStat = (StatType)e.newValue; return d; }, true));
            _fEffectType.RegisterValueChangedCallback(e          => Set(d => { d.EffectType = (UpgradeEffectType)e.newValue; return d; }, true));
            _fEffectPerRank.RegisterValueChangedCallback(e       => Set(d => { d.EffectPerRank = e.newValue; return d; }, true));
            _fMaxRank.RegisterValueChangedCallback(e             => Set(d => { d.MaxRank = e.newValue; return d; }));
            _fBaseCost.RegisterValueChangedCallback(e            => Set(d => { d.BaseCost = e.newValue; return d; }));
            _fCostScaling.RegisterValueChangedCallback(e         => Set(d => { d.CostScalingFactor = e.newValue; return d; }));
            _fSelectionWeight.RegisterValueChangedCallback(e     => Set(d => { d.SelectionWeight = e.newValue; return d; }));
            _fPrestigeGate.RegisterValueChangedCallback(e        => Set(d => { d.PrestigeGateRequirement = e.newValue; return d; }));
            _fMaxOutEdges.RegisterValueChangedCallback(e => Set(d => { d.MaxOutgoingEdges = e.newValue; return d; }, true));
            _fIconPicker.OnIconSelected = unicode => Set(d => { d.IconUnicode = unicode; return d; });
            _fStatDisplayName.RegisterValueChangedCallback(e     => Set(d => { d.StatDisplayName = e.newValue; return d; }));
            _fGridX.RegisterValueChangedCallback(e               => Set(d => { d.GridX = e.newValue; return d; }));
            _fGridY.RegisterValueChangedCallback(e               => Set(d => { d.GridY = e.newValue; return d; }));
            _fCustomData.RegisterValueChangedCallback(e          => Set(d => { d.CustomData = e.newValue as ScriptableObject; return d; }));
        }

        private void Set(System.Func<UpgradeNodeDefinition, UpgradeNodeDefinition> fn, bool refresh = false)
        {
            if (_current == null) return;
            _current.Def = fn(_current.Def);
            if (refresh) _current.RefreshDisplay();
            _window.MarkDirty();
        }

        public void ShowNode(UpgradeNodeView view)
        {
            if (_current == view) return;
            _current = view;
            var d = view.Def;
            SetEnabled(true);
            _fNodeId.SetValueWithoutNotify(d.NodeId ?? "");
            _fDisplayName.SetValueWithoutNotify(d.DisplayName ?? "");
            _fDescription.SetValueWithoutNotify(d.Description ?? "");
            _fCategory.SetValueWithoutNotify(d.Category);
            _fAffectedStat.SetValueWithoutNotify(d.AffectedStat);
            _fEffectType.SetValueWithoutNotify(d.EffectType);
            _fEffectPerRank.SetValueWithoutNotify(d.EffectPerRank);
            _fMaxRank.SetValueWithoutNotify(d.MaxRank);
            _fBaseCost.SetValueWithoutNotify(d.BaseCost);
            _fCostScaling.SetValueWithoutNotify(d.CostScalingFactor);
            _fSelectionWeight.SetValueWithoutNotify(d.SelectionWeight);
            _fPrestigeGate.SetValueWithoutNotify(d.PrestigeGateRequirement);
            _fMaxOutEdges.SetValueWithoutNotify(d.MaxOutgoingEdges);
            _fIconPicker.SetSelected(d.IconUnicode ?? "");
            _fStatDisplayName.SetValueWithoutNotify(d.StatDisplayName ?? "");
            _fGridX.SetValueWithoutNotify(d.GridX);
            _fGridY.SetValueWithoutNotify(d.GridY);
            _fCustomData.SetValueWithoutNotify(d.CustomData);

            var prereqs = d.PrerequisiteNodeIDs;
            _prereqLabel.text = prereqs?.Length > 0 ? string.Join(", ", prereqs) : "(none)";
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

    // ══════════════════════════════════════════════════════════════════════════════
    // Icon Picker Field
    // ══════════════════════════════════════════════════════════════════════════════

    public class IconPickerField : VisualElement
    {
        public System.Action<string> OnIconSelected;

        private string _selected = "";
        private Label  _preview;

        private static Font _faFont;
        private static Font FaFont
        {
            get
            {
                if (_faFont == null)
                {
                    _faFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/fa-solid-900.ttf");
                    if (_faFont == null)
                        _faFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/fa-solid-900.ttf");
                }
                return _faFont;
            }
        }

        // (name, unicode) pairs — extend freely
        private static readonly (string Name, string Unicode)[] Icons =
        {
            // Combat
            ("Sword",        "\uf6cf"),
            ("Lightning",    "\uf0e7"),
            ("Target",       "\uf140"),
            ("Star",         "\uf005"),
            ("Explosion",    "\uf1e2"),
            ("Crosshair",    "\uf05b"),
            ("Fire",         "\uf06d"),
            ("Skull",        "\uf54c"),
            ("Bomb",         "\uf1e2"),
            ("Shield Hit",   "\uf3ed"),
            // Survival
            ("Heart",        "\uf004"),
            ("Shield",       "\uf132"),
            ("HP Regen",     "\uf309"),
            ("Medkit",       "\uf0fa"),
            ("Lock",         "\uf023"),
            // Movement
            ("Boot",         "\uf70c"),
            ("Wind",         "\uf72e"),
            ("Feather",      "\uf52d"),
            // Economy
            ("Coin",         "\uf51e"),
            ("Magnet",       "\uf076"),
            ("Trophy",       "\uf091"),
            ("Gem",          "\uf3d1"),
            ("Wallet",       "\uf555"),
            ("Chart",        "\uf201"),
            ("Dollar",       "\uf155"),
            // Production
            ("Gear",         "\uf013"),
            ("Factory",      "\uf1ad"),
            ("Clock",        "\uf017"),
            ("Moon",         "\uf186"),
            ("Copy",         "\uf24d"),
            ("Play",         "\uf144"),
            ("Bolt",         "\uf0e7"),
            ("Wrench",       "\uf0ad"),
            ("Cog",          "\uf085"),
            ("Industry",     "\uf275"),
            // Prestige
            ("Crown",        "\uf521"),
            ("Diamond",      "\uf219"),
            ("Magic",        "\uf0d0"),
            ("Infinity",     "\uf534"),
            ("Eye",          "\uf06e"),
            ("Sun",          "\uf185"),
            // Misc
            ("Leaf",         "\uf06c"),
            ("Snowflake",    "\uf2dc"),
            ("Anchor",       "\uf13d"),
            ("Map Pin",      "\uf276"),
            ("Compass",      "\uf14e"),
            ("Hourglass",    "\uf254"),
        };

        public IconPickerField()
        {
            style.marginTop    = 4;
            style.marginBottom = 4;

            // Section label
            var header = new VisualElement();
            header.style.flexDirection  = FlexDirection.Row;
            header.style.alignItems     = Align.Center;
            header.style.marginBottom   = 6;
            Add(header);

            var lbl = new Label("Icon");
            lbl.style.fontSize  = 10;
            lbl.style.color     = new Color(0.7f, 0.7f, 0.7f);
            lbl.style.width     = 80;
            header.Add(lbl);

            // Preview of selected icon
            _preview = new Label("?");
            _preview.style.fontSize  = 22;
            _preview.style.width     = 32;
            _preview.style.height    = 32;
            _preview.style.unityTextAlign = TextAnchor.MiddleCenter;
            _preview.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            _preview.style.borderTopLeftRadius     = 4;
            _preview.style.borderTopRightRadius    = 4;
            _preview.style.borderBottomLeftRadius  = 4;
            _preview.style.borderBottomRightRadius = 4;
            if (FaFont != null) _preview.style.unityFontDefinition = FontDefinition.FromFont(FaFont);
            header.Add(_preview);

            // Clear button
            var clearBtn = new Button(() => SelectIcon("")) { text = "✕" };
            clearBtn.style.marginLeft  = 6;
            clearBtn.style.width       = 22;
            clearBtn.style.height      = 22;
            clearBtn.style.fontSize    = 10;
            clearBtn.style.paddingLeft = clearBtn.style.paddingRight = 0;
            clearBtn.tooltip = "Clear icon (use stat default)";
            header.Add(clearBtn);

            // Grid of icon buttons
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap      = Wrap.Wrap;
            Add(grid);

            foreach (var (name, unicode) in Icons)
            {
                var btn = new Button(() => SelectIcon(unicode));
                btn.style.width   = 32;
                btn.style.height  = 32;
                btn.style.marginRight  = 3;
                btn.style.marginBottom = 3;
                btn.style.paddingLeft = btn.style.paddingRight =
                btn.style.paddingTop  = btn.style.paddingBottom = 0;
                btn.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
                btn.style.borderTopLeftRadius     = 3;
                btn.style.borderTopRightRadius    = 3;
                btn.style.borderBottomLeftRadius  = 3;
                btn.style.borderBottomRightRadius = 3;

                var iconLbl = new Label(unicode);
                iconLbl.style.fontSize = 14;
                iconLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                iconLbl.style.flexGrow = 1;
                iconLbl.pickingMode = PickingMode.Ignore;
                if (FaFont != null) iconLbl.style.unityFontDefinition = FontDefinition.FromFont(FaFont);
                btn.Add(iconLbl);

                btn.tooltip = name;
                grid.Add(btn);
            }
        }

        public void SetSelected(string unicode)
        {
            _selected = unicode ?? "";
            _preview.text = string.IsNullOrEmpty(_selected) ? "?" : _selected;
        }

        private void SelectIcon(string unicode)
        {
            SetSelected(unicode);
            OnIconSelected?.Invoke(unicode);
        }
    }
}
