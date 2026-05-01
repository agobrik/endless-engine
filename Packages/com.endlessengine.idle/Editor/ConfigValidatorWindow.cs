using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Editor window that runs ConfigValidator against any RealmPackSO asset selected
    /// in the Project window and displays field-level results inline.
    ///
    /// Open: Tools → Endless Engine → Validate Configs
    /// </summary>
    public class ConfigValidatorWindow : EditorWindow
    {
        [MenuItem("Tools/Endless Engine/Validate Configs")]
        public static void Open() => GetWindow<ConfigValidatorWindow>("Config Validator");

        // ── State ─────────────────────────────────────────────────────────────────

        private RealmPackSO              _realmPack;
        private ConfigValidator.ValidationMode _mode = ConfigValidator.ValidationMode.Warning;
        private bool                     _hasRun;
        private bool                     _lastResult;
        private readonly List<string>    _errors   = new();
        private readonly List<string>    _warnings = new();
        private Vector2                  _scroll;

        // ── GUI ───────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Config Validator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select a RealmPackSO and click Validate to check all config fields " +
                "for range violations, duplicate IDs, missing prerequisites, and cycles.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            _realmPack = (RealmPackSO)EditorGUILayout.ObjectField(
                "Realm Pack", _realmPack, typeof(RealmPackSO), allowSceneObjects: false);

            _mode = (ConfigValidator.ValidationMode)EditorGUILayout.EnumPopup("Mode", _mode);

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(_realmPack == null))
            {
                if (GUILayout.Button("Validate", GUILayout.Height(28)))
                    RunValidation();
            }

            if (_realmPack == null)
            {
                EditorGUILayout.HelpBox("Assign a RealmPackSO to validate.", MessageType.Warning);
                return;
            }

            if (!_hasRun) return;

            EditorGUILayout.Space(6);
            DrawResultHeader();

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            if (_errors.Count == 0 && _warnings.Count == 0)
            {
                EditorGUILayout.HelpBox("All checks passed — no issues found.", MessageType.Info);
            }
            else
            {
                foreach (string err in _errors)
                    EditorGUILayout.HelpBox(err, MessageType.Error);
                foreach (string warn in _warnings)
                    EditorGUILayout.HelpBox(warn, MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }

        // ── Validation ────────────────────────────────────────────────────────────

        private void RunValidation()
        {
            _errors.Clear();
            _warnings.Clear();
            _hasRun = false;

            if (_realmPack == null) return;

            // Intercept Debug.Log* so we can display results in the window
            Application.logMessageReceived += CaptureLog;

            var resolved = new ResolvedConfigs(
                enemy:            _realmPack.EnemyStatConfig,
                wave:             _realmPack.WaveConfig,
                economy:          _realmPack.EconomyConfig,
                upgrades:         _realmPack.UpgradeNodeConfigs,
                prestige:         _realmPack.PrestigeConfig,
                realm:            _realmPack.RealmIdentityConfig,
                player:           _realmPack.PlayerBaseStatConfig,
                schema:           _realmPack.SchemaVersion,
                realmSlug:        _realmPack.RealmSlug,
                upgradeSelection: _realmPack.UpgradeSelectionConfig);
            _lastResult = ConfigValidator.Validate(resolved, _mode);

            Application.logMessageReceived -= CaptureLog;
            _hasRun = true;
            Repaint();
        }

        private void CaptureLog(string condition, string stackTrace, LogType type)
        {
            // ConfigValidator prefixes all messages with "[ConfigValidator]"
            if (!condition.Contains("[ConfigValidator]")) return;

            string stripped = condition.Replace("[ConfigValidator] ", "");
            if (type == LogType.Error || type == LogType.Exception)
                _errors.Add(stripped);
            else
                _warnings.Add(stripped);
        }

        private void DrawResultHeader()
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = _lastResult
                    ? new Color(0.2f, 0.8f, 0.2f)
                    : new Color(0.9f, 0.2f, 0.2f) }
            };

            string label = _lastResult
                ? $"✓ PASSED — {_warnings.Count} warning(s)"
                : $"✗ FAILED — {_errors.Count} error(s), {_warnings.Count} warning(s)";

            EditorGUILayout.LabelField(label, style);
        }
    }
}
