using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Trait Tree Editor — visualizes all TraitConfigSO assets organized by tier.
    /// Open: Tools → Endless Engine → Trait Tree Editor
    ///
    /// Features:
    ///   - Tier-based grid layout (columns = tiers)
    ///   - Exclusivity group highlighting (hover to see conflicting traits)
    ///   - Click to select and inspect in Inspector
    ///   - "Create Trait" button for quick SO creation
    /// </summary>
    public class TraitTreeEditorWindow : EditorWindow
    {
        [MenuItem("Tools/Endless Engine/Trait Tree Editor")]
        public static void Open() => GetWindow<TraitTreeEditorWindow>("Trait Tree");

        // ── State ─────────────────────────────────────────────────────────────────

        private List<TraitConfigSO> _traits = new();
        private Vector2             _scroll;
        private TraitConfigSO       _hovered;
        private string              _searchFilter = "";

        private const float CARD_W = 180f;
        private const float CARD_H = 90f;
        private const float PAD    = 10f;

        // ── GUI ───────────────────────────────────────────────────────────────────

        private void OnEnable() => Reload();

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4);

            if (_traits.Count == 0)
            {
                EditorGUILayout.HelpBox("No TraitConfigSO assets found in the project.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawTierColumns();
            EditorGUILayout.EndScrollView();
        }

        // ── Toolbar ───────────────────────────────────────────────────────────────

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Trait Tree", EditorStyles.boldLabel, GUILayout.Width(80));
                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                _searchFilter = EditorGUILayout.TextField(_searchFilter,
                    EditorStyles.toolbarSearchField, GUILayout.Width(180));
                if (EditorGUI.EndChangeCheck()) Repaint();

                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    Reload();

                if (GUILayout.Button("+ Trait", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    CreateNewTrait();
            }
        }

        // ── Tier columns ──────────────────────────────────────────────────────────

        private void DrawTierColumns()
        {
            var filtered = string.IsNullOrEmpty(_searchFilter)
                ? _traits
                : _traits.Where(t => t.DisplayName.IndexOf(_searchFilter,
                    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.TraitId.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                  .ToList();

            int maxTier = filtered.Count > 0 ? filtered.Max(t => t.Tier) : 0;

            var tierWidth = CARD_W + PAD * 2;
            float totalWidth = (maxTier + 1) * tierWidth + PAD;

            var layoutRect = GUILayoutUtility.GetRect(totalWidth, 600f, GUILayout.ExpandHeight(true));

            // Draw tier headers
            for (int tier = 0; tier <= maxTier; tier++)
            {
                var headerRect = new Rect(layoutRect.x + tier * tierWidth + PAD,
                                          layoutRect.y, CARD_W, 20f);
                EditorGUI.LabelField(headerRect, $"Tier {tier}", EditorStyles.boldLabel);
            }

            // Draw trait cards per tier
            var tierCounts = new int[maxTier + 1];
            foreach (var trait in filtered)
            {
                int tier = Mathf.Clamp(trait.Tier, 0, maxTier);
                int row  = tierCounts[tier]++;

                float x = layoutRect.x + tier * tierWidth + PAD;
                float y = layoutRect.y + 24f + row * (CARD_H + PAD);

                DrawTraitCard(new Rect(x, y, CARD_W, CARD_H), trait);
            }
        }

        private void DrawTraitCard(Rect rect, TraitConfigSO trait)
        {
            bool isHovered = _hovered == trait;
            bool isExcluded = _hovered != null && _hovered != trait &&
                              _hovered.ExclusiveWith != null &&
                              _hovered.ExclusiveWith.Contains(trait.TraitId);

            // Background
            Color bg = isExcluded  ? new Color(0.6f, 0.1f, 0.1f, 0.9f)
                     : isHovered   ? new Color(0.2f, 0.5f, 0.8f, 0.9f)
                     :               new Color(0.22f, 0.22f, 0.22f, 1f);
            EditorGUI.DrawRect(rect, bg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), Color.gray);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), Color.gray);

            // Icon
            float iconSize = 32f;
            var iconRect   = new Rect(rect.x + 6, rect.y + 6, iconSize, iconSize);
            if (trait.Icon != null)
                GUI.DrawTexture(iconRect, trait.Icon.texture, ScaleMode.ScaleToFit);
            else
                EditorGUI.DrawRect(iconRect, new Color(0.35f, 0.35f, 0.35f));

            // Name + ID
            var nameRect = new Rect(rect.x + iconSize + 12, rect.y + 6, rect.width - iconSize - 18, 18);
            EditorGUI.LabelField(nameRect, trait.DisplayName, EditorStyles.boldLabel);

            var idRect   = new Rect(rect.x + iconSize + 12, rect.y + 22, rect.width - iconSize - 18, 14);
            var idStyle  = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
            EditorGUI.LabelField(idRect, trait.TraitId, idStyle);

            // Prestige requirement
            var prestRect = new Rect(rect.x + 6, rect.yMax - 22, rect.width - 12, 14);
            EditorGUI.LabelField(prestRect, $"Prestige ≥ {trait.PrestigeRequired}", idStyle);

            // Effects count
            int fxCount = trait.Effects?.Count ?? 0;
            var fxRect  = new Rect(rect.x + 6, rect.yMax - 38, rect.width - 12, 14);
            EditorGUI.LabelField(fxRect, $"{fxCount} effect{(fxCount != 1 ? "s" : "")}", idStyle);

            // Hover detection
            if (rect.Contains(Event.current.mousePosition))
            {
                if (_hovered != trait) { _hovered = trait; Repaint(); }
            }
            else if (_hovered == trait)
            {
                _hovered = null; Repaint();
            }

            // Click to select
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                Selection.activeObject = trait;
                Event.current.Use();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void Reload()
        {
            _traits = AssetDatabase.FindAssets("t:TraitConfigSO")
                .Select(guid => AssetDatabase.LoadAssetAtPath<TraitConfigSO>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(t => t != null)
                .OrderBy(t => t.Tier)
                .ThenBy(t => t.PrestigeRequired)
                .ToList();
            Repaint();
        }

        private static void CreateNewTrait()
        {
            var so = ScriptableObject.CreateInstance<TraitConfigSO>();
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Trait Config", "NewTrait", "asset",
                "Choose where to save the TraitConfigSO.");
            if (string.IsNullOrEmpty(path)) return;
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = so;
        }
    }
}
