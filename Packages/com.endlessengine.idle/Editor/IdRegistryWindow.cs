using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// ID Registry — Tools → Endless Engine → ID Registry
    ///
    /// Scans all ScriptableObjects in the project and collects every string field
    /// whose name ends with "Id" or "ID" (case-sensitive). Reports:
    ///   • Duplicate IDs across assets of the same type
    ///   • Empty / whitespace IDs
    ///   • Prerequisite references that point to a non-existent ID (orphan refs)
    ///
    /// Uses Unity 6.3 TypeCache + AssetDatabase for fast enumeration.
    /// All work is Editor-only; zero runtime overhead.
    /// </summary>
    public class IdRegistryWindow : EditorWindow
    {
        // ── Types ─────────────────────────────────────────────────────────────────

        private enum FilterMode { All, Duplicates, Empty, Orphans }

        private struct IdEntry
        {
            public string   TypeName;
            public string   AssetPath;
            public string   FieldName;
            public string   IdValue;
            public bool     IsDuplicate;
            public bool     IsEmpty;
            public bool     IsOrphan;      // only set for prerequisite-type fields
        }

        // ── State ─────────────────────────────────────────────────────────────────

        private List<IdEntry>   _entries       = new();
        private List<IdEntry>   _filtered      = new();
        private FilterMode      _filter        = FilterMode.All;
        private string          _searchText    = "";
        private Vector2         _scroll;
        private bool            _scanned       = false;
        private int             _dupCount;
        private int             _emptyCount;
        private int             _orphanCount;

        // ── Menu ──────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Endless Engine/ID Registry", priority = 20)]
        public static void Open()
        {
            var win = GetWindow<IdRegistryWindow>(utility: false, title: "ID Registry");
            win.minSize = new Vector2(700, 450);
        }

        // ── GUI ───────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawToolbar();
            DrawSummaryBar();
            DrawFilterBar();
            DrawTable();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Scan All SOs", EditorStyles.toolbarButton, GUILayout.Width(110)))
                Scan();

            GUILayout.FlexibleSpace();

            GUILayout.Label("Search:", EditorStyles.label, GUILayout.Width(50));
            string newSearch = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField,
                GUILayout.Width(200));
            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                ApplyFilter();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummaryBar()
        {
            if (!_scanned) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Total IDs: {_entries.Count}", GUILayout.Width(120));

            var dupStyle = new GUIStyle(EditorStyles.label);
            dupStyle.normal.textColor = _dupCount > 0 ? new Color(1f, 0.4f, 0.4f) : new Color(0.5f, 0.9f, 0.5f);
            GUILayout.Label($"Duplicates: {_dupCount}", dupStyle, GUILayout.Width(110));

            var emptyStyle = new GUIStyle(EditorStyles.label);
            emptyStyle.normal.textColor = _emptyCount > 0 ? new Color(1f, 0.8f, 0.2f) : new Color(0.5f, 0.9f, 0.5f);
            GUILayout.Label($"Empty: {_emptyCount}", emptyStyle, GUILayout.Width(90));

            var orphanStyle = new GUIStyle(EditorStyles.label);
            orphanStyle.normal.textColor = _orphanCount > 0 ? new Color(1f, 0.6f, 0.2f) : new Color(0.5f, 0.9f, 0.5f);
            GUILayout.Label($"Orphan Refs: {_orphanCount}", orphanStyle, GUILayout.Width(100));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilterBar()
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("Filter:", GUILayout.Width(45));

            FilterMode[] modes = (FilterMode[])Enum.GetValues(typeof(FilterMode));
            foreach (var mode in modes)
            {
                bool selected = _filter == mode;
                bool clicked  = GUILayout.Toggle(selected, mode.ToString(),
                    selected ? "Button" : "Button", GUILayout.Height(20));
                if (clicked && !selected)
                {
                    _filter = mode;
                    ApplyFilter();
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        private void DrawTable()
        {
            if (!_scanned)
            {
                GUILayout.Space(20);
                EditorGUILayout.HelpBox("Click 'Scan All SOs' to collect IDs from the project.", MessageType.Info);
                return;
            }

            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Type",       EditorStyles.toolbarButton, GUILayout.Width(160));
            GUILayout.Label("Field",      EditorStyles.toolbarButton, GUILayout.Width(130));
            GUILayout.Label("ID Value",   EditorStyles.toolbarButton, GUILayout.Width(180));
            GUILayout.Label("Asset",      EditorStyles.toolbarButton, GUILayout.ExpandWidth(true));
            GUILayout.Label("Issues",     EditorStyles.toolbarButton, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var entry in _filtered)
            {
                var issues = BuildIssueString(entry);
                Color rowColor = issues.Length > 0
                    ? (entry.IsDuplicate ? new Color(0.35f, 0.1f, 0.1f)
                                         : new Color(0.35f, 0.3f, 0.05f))
                    : new Color(0.18f, 0.18f, 0.18f);

                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = rowColor;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUI.backgroundColor = oldColor;

                EditorGUILayout.LabelField(entry.TypeName,  GUILayout.Width(160));
                EditorGUILayout.LabelField(entry.FieldName, GUILayout.Width(130));

                var idStyle = new GUIStyle(EditorStyles.label);
                idStyle.normal.textColor = entry.IsEmpty
                    ? new Color(1f, 0.6f, 0f)
                    : new Color(0.85f, 0.95f, 0.85f);
                EditorGUILayout.LabelField(
                    entry.IsEmpty ? "(empty)" : entry.IdValue,
                    idStyle, GUILayout.Width(180));

                if (GUILayout.Button(System.IO.Path.GetFileName(entry.AssetPath),
                    EditorStyles.linkLabel, GUILayout.ExpandWidth(true)))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(entry.AssetPath);
                    if (obj != null)
                    {
                        EditorGUIUtility.PingObject(obj);
                        Selection.activeObject = obj;
                    }
                }

                var issueStyle = new GUIStyle(EditorStyles.label);
                issueStyle.normal.textColor = issues.Length > 0
                    ? new Color(1f, 0.4f, 0.4f)
                    : new Color(0.5f, 0.9f, 0.5f);
                EditorGUILayout.LabelField(issues.Length > 0 ? issues : "OK",
                    issueStyle, GUILayout.Width(120));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (_filtered.Count == 0)
                EditorGUILayout.HelpBox("No entries match the current filter.", MessageType.Info);
        }

        private static string BuildIssueString(IdEntry e)
        {
            var parts = new List<string>(3);
            if (e.IsDuplicate) parts.Add("DUPLICATE");
            if (e.IsEmpty)     parts.Add("EMPTY");
            if (e.IsOrphan)    parts.Add("ORPHAN REF");
            return string.Join(", ", parts);
        }

        // ── Scanning ──────────────────────────────────────────────────────────────

        private void Scan()
        {
            _entries.Clear();
            _dupCount = _emptyCount = _orphanCount = 0;

            // 1. Collect all Endless Engine SO types (those with an ID field)
            var soTypes = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("EndlessEngine"))
                .ToArray();

            // 2. Load all SO assets
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");

            // Per-type ID → list of asset paths (for duplicate detection)
            var idMap = new Dictionary<string, Dictionary<string, List<string>>>();
            // type name → (id → [paths])

            var rawEntries = new List<IdEntry>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;

                Type type = so.GetType();
                if (type.Namespace == null || !type.Namespace.StartsWith("EndlessEngine"))
                    continue;

                // Find ID fields: public string fields whose name ends with Id or ID
                var idFields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => f.FieldType == typeof(string)
                             && (f.Name.EndsWith("Id") || f.Name.EndsWith("ID")))
                    .ToArray();

                foreach (var field in idFields)
                {
                    string value = field.GetValue(so) as string ?? "";
                    bool isEmpty = string.IsNullOrWhiteSpace(value);

                    var entry = new IdEntry
                    {
                        TypeName  = type.Name,
                        AssetPath = path,
                        FieldName = field.Name,
                        IdValue   = value,
                        IsEmpty   = isEmpty,
                    };
                    rawEntries.Add(entry);

                    if (!isEmpty)
                    {
                        if (!idMap.TryGetValue(type.Name, out var typeMap))
                        {
                            typeMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                            idMap[type.Name] = typeMap;
                        }
                        if (!typeMap.TryGetValue(value, out var pathList))
                        {
                            pathList = new List<string>();
                            typeMap[value] = pathList;
                        }
                        pathList.Add(path);
                    }
                }
            }

            // 3. Mark duplicates
            for (int i = 0; i < rawEntries.Count; i++)
            {
                var e = rawEntries[i];
                if (!e.IsEmpty
                    && idMap.TryGetValue(e.TypeName, out var tmap)
                    && tmap.TryGetValue(e.IdValue, out var paths)
                    && paths.Count > 1)
                {
                    e.IsDuplicate = true;
                    rawEntries[i] = e;
                }
            }

            // 4. Orphan check: PrerequisiteNodeIDs in UpgradeNodeConfigSO
            var allNodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in rawEntries)
                if (e.TypeName == nameof(UpgradeNodeConfigSO) && e.FieldName == "NodeId" && !e.IsEmpty)
                    allNodeIds.Add(e.IdValue);

            // Scan string[] PrerequisiteNodeIDs fields
            string[] upgradeGuids = AssetDatabase.FindAssets("t:UpgradeNodeConfigSO");
            var orphanPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in upgradeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<UpgradeNodeConfigSO>(path);
                if (so?.PrerequisiteNodeIDs == null) continue;
                foreach (string prereq in so.PrerequisiteNodeIDs)
                {
                    if (!string.IsNullOrWhiteSpace(prereq) && !allNodeIds.Contains(prereq))
                        orphanPaths.Add(path + "::" + prereq);
                }
            }

            // Add orphan pseudo-entries
            foreach (string key in orphanPaths)
            {
                int sep = key.LastIndexOf("::", StringComparison.Ordinal);
                string assetPath  = key.Substring(0, sep);
                string missingId  = key.Substring(sep + 2);
                rawEntries.Add(new IdEntry
                {
                    TypeName  = nameof(UpgradeNodeConfigSO),
                    AssetPath = assetPath,
                    FieldName = "PrerequisiteNodeIDs",
                    IdValue   = missingId,
                    IsOrphan  = true,
                });
            }

            // 5. Tally
            _entries    = rawEntries;
            _dupCount   = _entries.Count(e => e.IsDuplicate);
            _emptyCount = _entries.Count(e => e.IsEmpty);
            _orphanCount = _entries.Count(e => e.IsOrphan);
            _scanned    = true;

            ApplyFilter();

            if (_dupCount == 0 && _emptyCount == 0 && _orphanCount == 0)
                Debug.Log($"[IdRegistry] Scan complete — {_entries.Count} IDs found, no issues.");
            else
                Debug.LogWarning($"[IdRegistry] Scan complete — {_entries.Count} IDs, " +
                                 $"{_dupCount} duplicates, {_emptyCount} empty, {_orphanCount} orphan refs.");
        }

        private void ApplyFilter()
        {
            IEnumerable<IdEntry> src = _entries;

            src = _filter switch
            {
                FilterMode.Duplicates => src.Where(e => e.IsDuplicate),
                FilterMode.Empty      => src.Where(e => e.IsEmpty),
                FilterMode.Orphans    => src.Where(e => e.IsOrphan),
                _                     => src,
            };

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string q = _searchText.ToLowerInvariant();
                src = src.Where(e =>
                    e.TypeName.ToLowerInvariant().Contains(q) ||
                    e.IdValue.ToLowerInvariant().Contains(q) ||
                    e.AssetPath.ToLowerInvariant().Contains(q));
            }

            _filtered = src.OrderBy(e => e.TypeName).ThenBy(e => e.IdValue).ToList();
            Repaint();
        }
    }
}
