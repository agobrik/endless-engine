using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Visual Generator Editor — Idle Game Toolset
    /// Open: Tools → Endless Engine → Generator Editor
    /// </summary>
    public class GeneratorEditorWindow : EditorWindow
    {
        private GeneratorDatabaseSO _database;
        private bool                _dirty;
        private Label               _assetLabel;
        private ListView            _list;
        private GeneratorInspector  _inspector;
        private Toggle              _progressiveUnlockToggle;

        [MenuItem("Tools/Endless Engine/Generator Editor")]
        public static void Open()
        {
            var win = GetWindow<GeneratorEditorWindow>("Generator Editor");
            win.minSize = new Vector2(800, 500);
        }

        private void OnEnable() => BuildUI();

        private void OnDisable()
        {
            if (_dirty && _database != null &&
                EditorUtility.DisplayDialog("Unsaved Changes", "Save before closing?", "Save", "Discard"))
                SaveDatabase();
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            // ── Toolbar ──────────────────────────────────────────────────────────
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

            toolbar.Add(Btn("Load Database", null,                           OnLoadClicked));
            toolbar.Add(Btn("New Database",  null,                           OnNewClicked));
            toolbar.Add(Btn("+ Add",         new Color(0.2f, 0.35f, 0.55f), OnAddClicked));

            _assetLabel = new Label("No database loaded");
            _assetLabel.style.color      = new Color(0.6f, 0.6f, 0.6f);
            _assetLabel.style.marginLeft = 12;
            _assetLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            toolbar.Add(_assetLabel);

            var spacer = new VisualElement(); spacer.style.flexGrow = 1;
            toolbar.Add(spacer);
            toolbar.Add(Btn("Save", new Color(0.2f, 0.5f, 0.2f), OnSaveClicked));
            rootVisualElement.Add(toolbar);

            // ── Main row ─────────────────────────────────────────────────────────
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexGrow      = 1;
            rootVisualElement.Add(row);

            // Left: generator list
            var listPanel = new VisualElement();
            listPanel.style.width           = 220;
            listPanel.style.flexShrink      = 0;
            listPanel.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            listPanel.style.borderRightWidth = 1;
            listPanel.style.borderRightColor = new Color(0.1f, 0.1f, 0.1f);
            row.Add(listPanel);

            var listHeader = new Label("GENERATORS");
            listHeader.style.fontSize   = 9;
            listHeader.style.color      = new Color(0.5f, 0.5f, 0.5f);
            listHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            listHeader.style.marginTop    = 8;
            listHeader.style.marginBottom = 4;
            listHeader.style.marginLeft   = 10;
            listPanel.Add(listHeader);

            _list = new ListView
            {
                makeItem      = MakeListItem,
                bindItem      = BindListItem,
                selectionType = SelectionType.Single,
                fixedItemHeight = 36,
            };
            _list.style.flexGrow = 1;
            _list.selectionChanged += OnSelectionChanged;
            listPanel.Add(_list);

            // List footer buttons
            var listFooter = new VisualElement();
            listFooter.style.flexDirection    = FlexDirection.Row;
            listFooter.style.borderTopWidth   = 1;
            listFooter.style.borderTopColor   = new Color(0.1f, 0.1f, 0.1f);
            listFooter.style.paddingTop       = 4;
            listFooter.style.paddingBottom    = 4;
            listFooter.style.paddingLeft      = 6;
            listFooter.style.paddingRight     = 6;
            listPanel.Add(listFooter);

            listFooter.Add(Btn("↑", null, MoveUp));
            listFooter.Add(Btn("↓", null, MoveDown));
            var delBtn = Btn("Delete", new Color(0.5f, 0.15f, 0.15f), DeleteSelected);
            delBtn.style.marginLeft = 6;
            listFooter.Add(delBtn);

            // Right: inspector
            _inspector = new GeneratorInspector(this);
            _inspector.style.flexGrow        = 1;
            _inspector.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
            row.Add(_inspector);

            // Auto-load — prefer Assets/Configs/, fall back to first found
            var guids = AssetDatabase.FindAssets("t:GeneratorDatabaseSO");
            if (guids.Length > 0)
            {
                string preferred = null;
                foreach (var g in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (p.StartsWith("Assets/Configs/")) { preferred = p; break; }
                }
                preferred ??= AssetDatabase.GUIDToAssetPath(guids[0]);
                LoadDatabase(AssetDatabase.LoadAssetAtPath<GeneratorDatabaseSO>(preferred));
            }
        }

        // ── List item UI ──────────────────────────────────────────────────────────

        private static VisualElement MakeListItem()
        {
            var root = new VisualElement();
            root.style.flexDirection  = FlexDirection.Row;
            root.style.alignItems     = Align.Center;
            root.style.paddingLeft    = 10;
            root.style.paddingRight   = 6;
            root.style.paddingTop     = 4;
            root.style.paddingBottom  = 4;

            var index = new Label();
            index.name = "index";
            index.style.fontSize  = 9;
            index.style.color     = new Color(0.4f, 0.4f, 0.4f);
            index.style.width     = 18;
            root.Add(index);

            var col = new VisualElement();
            col.name = "colorbar";
            col.style.width              = 3;
            col.style.height             = 24;
            col.style.marginRight        = 6;
            col.style.borderTopLeftRadius     = 2;
            col.style.borderBottomLeftRadius  = 2;
            col.style.borderTopRightRadius    = 2;
            col.style.borderBottomRightRadius = 2;
            root.Add(col);

            var texts = new VisualElement();
            texts.style.flexGrow = 1;
            root.Add(texts);

            var name = new Label();
            name.name = "name";
            name.style.fontSize = 11;
            name.style.color    = new Color(0.9f, 0.9f, 0.9f);
            texts.Add(name);

            var sub = new Label();
            sub.name = "sub";
            sub.style.fontSize = 9;
            sub.style.color    = new Color(0.5f, 0.5f, 0.5f);
            texts.Add(sub);

            return root;
        }

        private void BindListItem(VisualElement el, int i)
        {
            if (_database == null || i >= _database.Generators.Length) return;
            var gen = _database.Generators[i];
            if (gen == null) return;

            el.Q<Label>("index").text = $"#{i + 1}";
            el.Q<Label>("name").text  = gen.DisplayName;
            el.Q<Label>("sub").text   = $"{gen.BaseYieldPerSecond:F1}/s  •  cost {gen.BaseCost}";

            // Color bar — cycle through palette
            var colors = new[]
            {
                new Color(1.00f, 0.55f, 0.20f),
                new Color(0.24f, 0.71f, 1.00f),
                new Color(0.24f, 0.78f, 0.47f),
                new Color(0.86f, 0.24f, 0.24f),
                new Color(0.71f, 0.31f, 1.00f),
            };
            el.Q<VisualElement>("colorbar").style.backgroundColor = colors[i % colors.Length];
        }

        private void OnSelectionChanged(IEnumerable<object> _)
        {
            int idx = _list.selectedIndex;
            if (idx < 0 || _database == null || idx >= _database.Generators.Length)
            { _inspector.Clear(); return; }
            _inspector.Show(_database.Generators[idx], idx);
        }

        // ── Operations ────────────────────────────────────────────────────────────

        private void OnLoadClicked()
        {
            var path = EditorUtility.OpenFilePanel("Select GeneratorDatabaseSO", "Assets/Configs", "asset");
            if (string.IsNullOrEmpty(path)) return;
            path = "Assets" + path.Substring(Application.dataPath.Length);
            var db = AssetDatabase.LoadAssetAtPath<GeneratorDatabaseSO>(path);
            if (db != null) LoadDatabase(db);
        }

        private void OnNewClicked()
        {
            var path = EditorUtility.SaveFilePanelInProject("New Generator Database", "GeneratorDatabase", "asset", "");
            if (string.IsNullOrEmpty(path)) return;
            var db = ScriptableObject.CreateInstance<GeneratorDatabaseSO>();
            db.Generators = new GeneratorConfigSO[0];
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            LoadDatabase(db);
        }

        private void OnAddClicked()
        {
            if (_database == null) { EditorUtility.DisplayDialog("No Database", "Load a database first.", "OK"); return; }

            // Create new GeneratorConfigSO asset alongside database
            var dbPath = AssetDatabase.GetAssetPath(_database);
            var dir    = System.IO.Path.GetDirectoryName(dbPath);
            var name   = $"gen_{_database.Generators.Length + 1:D2}";
            var path   = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{name}.asset");

            var gen = ScriptableObject.CreateInstance<GeneratorConfigSO>();
            gen.GeneratorId        = name;
            gen.DisplayName        = "New Generator";
            gen.BaseYieldPerSecond = 1f;
            gen.BaseCost           = 100;
            gen.CostScalingFactor  = 1.15f;
            gen.MaxCount           = -1;
            AssetDatabase.CreateAsset(gen, path);
            AssetDatabase.Refresh();

            var list = _database.Generators.ToList();
            list.Add(gen);
            _database.Generators = list.ToArray();
            EditorUtility.SetDirty(_database);

            RefreshList();
            _list.selectedIndex = list.Count - 1;
            MarkDirty();
        }

        private void OnSaveClicked() => SaveDatabase();

        private void MoveUp()
        {
            int i = _list.selectedIndex;
            if (i <= 0 || _database == null) return;
            var arr = _database.Generators;
            (arr[i], arr[i - 1]) = (arr[i - 1], arr[i]);
            RefreshList();
            _list.selectedIndex = i - 1;
            MarkDirty();
        }

        private void MoveDown()
        {
            int i = _list.selectedIndex;
            if (_database == null || i < 0 || i >= _database.Generators.Length - 1) return;
            var arr = _database.Generators;
            (arr[i], arr[i + 1]) = (arr[i + 1], arr[i]);
            RefreshList();
            _list.selectedIndex = i + 1;
            MarkDirty();
        }

        private void DeleteSelected()
        {
            int i = _list.selectedIndex;
            if (_database == null || i < 0) return;
            if (!EditorUtility.DisplayDialog("Delete Generator",
                $"Delete '{_database.Generators[i].DisplayName}'? The .asset file will NOT be deleted.", "Delete", "Cancel")) return;

            var list = _database.Generators.ToList();
            list.RemoveAt(i);
            _database.Generators = list.ToArray();
            RefreshList();
            _inspector.Clear();
            MarkDirty();
        }

        public void LoadDatabase(GeneratorDatabaseSO db)
        {
            _database = db;
            _dirty    = false;
            _assetLabel.text = db.name;
            _assetLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            RefreshList();
            _inspector.Clear();
        }

        public void SaveDatabase()
        {
            if (_database == null) return;
            EditorUtility.SetDirty(_database);
            // Also dirty all generator SOs
            if (_database.Generators != null)
                foreach (var g in _database.Generators)
                    if (g != null) EditorUtility.SetDirty(g);
            AssetDatabase.SaveAssets();
            _dirty = false;
            _assetLabel.text = _database.name;
            _assetLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        }

        public void MarkDirty()
        {
            _dirty = true;
            _assetLabel.text = (_database?.name ?? "?") + " *";
            _assetLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
        }

        public void RefreshList()
        {
            _list.itemsSource = _database?.Generators?.ToList() ?? new List<GeneratorConfigSO>();
            _list.Rebuild();
        }

        public void RefreshCurrentItem()
        {
            _list.RefreshItem(_list.selectedIndex);
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

        public GeneratorDatabaseSO Database => _database;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Generator Inspector
    // ══════════════════════════════════════════════════════════════════════════════

    public class GeneratorInspector : VisualElement
    {
        private readonly GeneratorEditorWindow _window;
        private GeneratorConfigSO _current;
        private int               _currentIndex;

        private TextField    _fId, _fName, _fDesc;
        private FloatField   _fYield, _fCostScale;
        private LongField    _fBaseCost;
        private IntegerField _fMaxCount, _fUnlockReq;
        private ObjectField  _fUnlockPrereq;
        private VisualElement _costChart;

        public GeneratorInspector(GeneratorEditorWindow window)
        {
            _window = window;
            style.overflow = Overflow.Hidden;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.contentContainer.style.paddingTop    = 16;
            scroll.contentContainer.style.paddingBottom = 16;
            scroll.contentContainer.style.paddingLeft   = 20;
            scroll.contentContainer.style.paddingRight  = 20;
            Add(scroll);

            scroll.Add(Hdr("Generator Inspector"));

            scroll.Add(Sec("Identity"));
            _fId   = AF(scroll, new TextField("Generator ID"));
            _fName = AF(scroll, new TextField("Display Name"));
            _fDesc = AF(scroll, new TextField("Description") { multiline = true });
            _fDesc.style.height = 56;

            scroll.Add(Sec("Economy"));
            _fYield     = AF(scroll, new FloatField("Yield / Second"));
            _fBaseCost  = AF(scroll, new LongField("Base Cost"));
            _fCostScale = AF(scroll, new FloatField("Cost Scale Factor"));
            _fMaxCount  = AF(scroll, new IntegerField("Max Count (-1 = ∞)"));

            scroll.Add(Sec("Unlock"));
            _fUnlockReq    = AF(scroll, new IntegerField("Unlock Requirement"));
            _fUnlockPrereq = AF(scroll, new ObjectField("Prerequisite Generator") { objectType = typeof(GeneratorConfigSO), allowSceneObjects = false });

            // Cost curve chart
            scroll.Add(Sec("Cost Curve  (first 10 copies)"));
            _costChart = new VisualElement();
            _costChart.style.height          = 120;
            _costChart.style.marginTop       = 6;
            _costChart.style.marginBottom    = 12;
            _costChart.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            _costChart.style.borderTopLeftRadius     = 4;
            _costChart.style.borderTopRightRadius    = 4;
            _costChart.style.borderBottomLeftRadius  = 4;
            _costChart.style.borderBottomRightRadius = 4;
            _costChart.generateVisualContent += DrawCostChart;
            scroll.Add(_costChart);

            // Callbacks
            _fId.RegisterValueChangedCallback(e        => Mutate(g => { g.GeneratorId = e.newValue; }));
            _fName.RegisterValueChangedCallback(e      => Mutate(g => { g.DisplayName = e.newValue; }, refresh: true));
            _fDesc.RegisterValueChangedCallback(e      => Mutate(g => { g.Description = e.newValue; }));
            _fYield.RegisterValueChangedCallback(e     => Mutate(g => { g.BaseYieldPerSecond = e.newValue; }, chart: true, refresh: true));
            _fBaseCost.RegisterValueChangedCallback(e  => Mutate(g => { g.BaseCost = e.newValue; }, chart: true, refresh: true));
            _fCostScale.RegisterValueChangedCallback(e => Mutate(g => { g.CostScalingFactor = e.newValue; }, chart: true));
            _fMaxCount.RegisterValueChangedCallback(e  => Mutate(g => { g.MaxCount = e.newValue; }));
            _fUnlockReq.RegisterValueChangedCallback(e => Mutate(g => { g.UnlockRequirement = e.newValue; }));
            _fUnlockPrereq.RegisterValueChangedCallback(e => Mutate(g => { g.UnlockPrerequisite = e.newValue as GeneratorConfigSO; }));

            Clear();
        }

        private void Mutate(System.Action<GeneratorConfigSO> fn, bool chart = false, bool refresh = false)
        {
            if (_current == null) return;
            fn(_current);
            if (chart) _costChart.MarkDirtyRepaint();
            if (refresh) _window.RefreshCurrentItem();
            _window.MarkDirty();
        }

        public void Show(GeneratorConfigSO gen, int index)
        {
            _current      = gen;
            _currentIndex = index;
            SetEnabled(true);

            _fId.SetValueWithoutNotify(gen.GeneratorId ?? "");
            _fName.SetValueWithoutNotify(gen.DisplayName ?? "");
            _fDesc.SetValueWithoutNotify(gen.Description ?? "");
            _fYield.SetValueWithoutNotify(gen.BaseYieldPerSecond);
            _fBaseCost.SetValueWithoutNotify(gen.BaseCost);
            _fCostScale.SetValueWithoutNotify(gen.CostScalingFactor);
            _fMaxCount.SetValueWithoutNotify(gen.MaxCount);
            _fUnlockReq.SetValueWithoutNotify(gen.UnlockRequirement);
            _fUnlockPrereq.SetValueWithoutNotify(gen.UnlockPrerequisite);

            _costChart.MarkDirtyRepaint();
        }

        public new void Clear()
        {
            _current = null;
            SetEnabled(false);
        }

        // ── Cost Chart ────────────────────────────────────────────────────────────

        private void DrawCostChart(MeshGenerationContext ctx)
        {
            if (_current == null) return;

            const int copies = 10;
            var costs = new long[copies];
            for (int i = 0; i < copies; i++)
                costs[i] = _current.CostForCopy(i);

            float maxCost = costs.Max();
            if (maxCost <= 0) return;

            float w = _costChart.resolvedStyle.width;
            float h = _costChart.resolvedStyle.height;
            if (w < 1 || h < 1) return;

            float padL = 8, padR = 8, padT = 8, padB = 24;
            float chartW = w - padL - padR;
            float chartH = h - padT - padB;
            float barW   = chartW / copies * 0.6f;
            float gap    = chartW / copies;

            var painter = ctx.painter2D;

            // Bars
            for (int i = 0; i < copies; i++)
            {
                float barH  = (costs[i] / maxCost) * chartH;
                float x     = padL + i * gap + gap * 0.2f;
                float y     = padT + chartH - barH;

                float t = (float)i / (copies - 1);
                var col = Color.Lerp(new Color(0.24f, 0.71f, 1.00f), new Color(1.00f, 0.55f, 0.20f), t);

                painter.fillColor = col;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, y));
                painter.LineTo(new Vector2(x + barW, y));
                painter.LineTo(new Vector2(x + barW, padT + chartH));
                painter.LineTo(new Vector2(x, padT + chartH));
                painter.ClosePath();
                painter.Fill();

                // Label
                ctx.DrawText($"#{i + 1}", new Vector2(x, padT + chartH + 4), 8,
                    new Color(0.5f, 0.5f, 0.5f));
            }

            // Max cost label
            ctx.DrawText(FormatCost(maxCost), new Vector2(padL, padT - 2), 8,
                new Color(0.6f, 0.6f, 0.6f));
        }

        private static string FormatCost(float v)
        {
            if (v >= 1_000_000) return $"{v / 1_000_000:F1}M";
            if (v >= 1_000)     return $"{v / 1_000:F1}K";
            return $"{v:F0}";
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static T AF<T>(VisualElement parent, T field) where T : VisualElement
        {
            field.style.marginBottom = 4;
            parent.Add(field);
            return field;
        }

        private static Label Hdr(string t)
        {
            var l = new Label(t);
            l.style.fontSize = 13;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = new Color(0.85f, 0.85f, 0.85f);
            l.style.marginBottom = 10;
            return l;
        }

        private static Label Sec(string t)
        {
            var l = new Label(t.ToUpper());
            l.style.fontSize = 9;
            l.style.color    = new Color(0.5f, 0.5f, 0.5f);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 10; l.style.marginBottom = 4;
            l.style.borderBottomWidth = 1;
            l.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);
            return l;
        }
    }
}
