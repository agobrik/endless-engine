using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Content Pack Wizard — Tools → Endless Engine → Content Pack Wizard
    ///
    /// Creates a complete RealmPack in one click:
    ///   1. User enters a slug (e.g. "fire-realm") and display name.
    ///   2. Wizard creates all 9 canonical config SOs under Assets/[OutputFolder]/[Slug]/
    ///   3. Creates and populates a RealmPackSO referencing all 9 SOs.
    ///   4. Registers the new RealmEntry in the project's RealmRegistrySO (if found).
    ///
    /// Existing RealmRegistrySO in the project is auto-detected via AssetDatabase.
    /// If none exists, one is created at the same output path.
    ///
    /// The wizard does NOT overwrite existing assets — safe to re-run after partial failures.
    /// </summary>
    public class ContentPackWizard : EditorWindow
    {
        // ── State ─────────────────────────────────────────────────────────────────

        private string _slug        = "new-realm";
        private string _displayName = "New Realm";
        private string _outputRoot  = "Assets/Configs/Realms";
        private bool   _isDefault   = false;
        private int    _unlockPrestige = 0;

        private Label  _statusLabel;
        private Label  _previewLabel;

        // ── Menu ──────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Endless Engine/Content Pack Wizard", priority = 10)]
        public static void Open()
        {
            var win = GetWindow<ContentPackWizard>(utility: true, title: "Content Pack Wizard");
            win.minSize = new Vector2(500, 480);
            win.maxSize = new Vector2(680, 580);
        }

        // ── GUI ───────────────────────────────────────────────────────────────────

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop    = 12;
            root.style.paddingBottom = 12;
            root.style.paddingLeft   = 16;
            root.style.paddingRight  = 16;

            // Header
            var header = new Label("Content Pack Wizard");
            header.style.fontSize = 18;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 4;
            root.Add(header);

            var sub = new Label(
                "Creates all 9 canonical config SOs for a new Realm Pack and registers it " +
                "in the project's RealmRegistrySO.");
            sub.style.fontSize    = 11;
            sub.style.color       = new Color(0.6f, 0.6f, 0.6f);
            sub.style.whiteSpace  = WhiteSpace.Normal;
            sub.style.marginBottom = 14;
            root.Add(sub);

            // ── Fields ────────────────────────────────────────────────────────────
            root.Add(SectionLabel("Realm Identity"));
            root.Add(FieldRow("Slug",         _slug,        v => { _slug = v;        RefreshPreview(); }));
            root.Add(FieldRow("Display Name", _displayName, v => { _displayName = v; RefreshPreview(); }));
            root.Add(FieldRow("Output Folder", _outputRoot, v => { _outputRoot = v;  RefreshPreview(); }));

            root.Add(SectionLabel("Registry Settings"));

            // Default realm toggle
            var defaultRow = new VisualElement();
            defaultRow.style.flexDirection = FlexDirection.Row;
            defaultRow.style.alignItems    = Align.Center;
            defaultRow.style.marginBottom  = 6;

            var defaultLabel = new Label("Is Default Realm");
            defaultLabel.style.width = 140;
            defaultLabel.style.fontSize = 12;
            var defaultToggle = new Toggle { value = _isDefault };
            defaultToggle.RegisterValueChangedCallback(evt => _isDefault = evt.newValue);
            defaultRow.Add(defaultLabel);
            defaultRow.Add(defaultToggle);
            root.Add(defaultRow);

            // Unlock prestige threshold
            var unlockRow = new VisualElement();
            unlockRow.style.flexDirection = FlexDirection.Row;
            unlockRow.style.alignItems    = Align.Center;
            unlockRow.style.marginBottom  = 6;

            var unlockLabel = new Label("Unlock Prestige");
            unlockLabel.style.width   = 140;
            unlockLabel.style.fontSize = 12;
            var unlockField = new IntegerField { value = _unlockPrestige };
            unlockField.style.width = 80;
            unlockField.RegisterValueChangedCallback(evt => _unlockPrestige = Mathf.Max(0, evt.newValue));
            unlockRow.Add(unlockLabel);
            unlockRow.Add(unlockField);
            root.Add(unlockRow);

            // ── Preview ────────────────────────────────────────────────────────────
            root.Add(SectionLabel("Assets to Create"));

            var previewBox = new ScrollView();
            previewBox.style.height          = 130;
            previewBox.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            previewBox.style.borderTopLeftRadius     = 4;
            previewBox.style.borderTopRightRadius    = 4;
            previewBox.style.borderBottomLeftRadius  = 4;
            previewBox.style.borderBottomRightRadius = 4;
            previewBox.style.paddingTop    = 6;
            previewBox.style.paddingLeft   = 8;
            previewBox.style.paddingBottom = 6;
            previewBox.style.marginBottom  = 12;

            _previewLabel = new Label();
            _previewLabel.style.fontSize   = 10;
            _previewLabel.style.color      = new Color(0.75f, 0.85f, 0.75f);
            _previewLabel.style.whiteSpace = WhiteSpace.Normal;
            previewBox.Add(_previewLabel);
            root.Add(previewBox);

            // ── Status ─────────────────────────────────────────────────────────────
            _statusLabel = new Label();
            _statusLabel.style.fontSize    = 11;
            _statusLabel.style.whiteSpace  = WhiteSpace.Normal;
            _statusLabel.style.marginBottom = 8;
            root.Add(_statusLabel);

            // ── Buttons ────────────────────────────────────────────────────────────
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop     = 4;

            var createBtn = new Button(CreatePack) { text = "Create Realm Pack" };
            createBtn.style.height    = 34;
            createBtn.style.fontSize  = 13;
            createBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            createBtn.style.backgroundColor = new Color(0.15f, 0.42f, 0.15f);
            createBtn.style.color           = Color.white;
            createBtn.style.flexGrow        = 1;
            ApplyRadius(createBtn, 4);

            var findBtn = new Button(FindAndPingRegistry) { text = "Find Registry" };
            findBtn.style.height    = 34;
            findBtn.style.fontSize  = 11;
            findBtn.style.width     = 110;
            findBtn.style.marginLeft = 6;
            ApplyRadius(findBtn, 4);

            btnRow.Add(createBtn);
            btnRow.Add(findBtn);
            root.Add(btnRow);

            RefreshPreview();
        }

        // ── Preview ───────────────────────────────────────────────────────────────

        private void RefreshPreview()
        {
            if (_previewLabel == null) return;
            string folder = GetPackFolder();
            _previewLabel.text = string.Join("\n", new[]
            {
                $"{folder}/EnemyStatConfig_{_slug}.asset",
                $"{folder}/WaveConfig_{_slug}.asset",
                $"{folder}/EconomyConfig_{_slug}.asset",
                $"{folder}/UpgradeSelectionConfig_{_slug}.asset",
                $"{folder}/PrestigeConfig_{_slug}.asset",
                $"{folder}/RealmIdentityConfig_{_slug}.asset",
                $"{folder}/PlayerBaseStatConfig_{_slug}.asset",
                $"{folder}/SchemaVersion_{_slug}.asset",
                $"{folder}/RealmPack_{_slug}.asset  ← assembles all 9 SOs",
                "",
                "RealmRegistrySO → new RealmEntry registered automatically",
            });
        }

        // ── Creation ──────────────────────────────────────────────────────────────

        private void CreatePack()
        {
            string slug = SanitizeSlug(_slug);
            if (string.IsNullOrEmpty(slug))
            {
                SetStatus("Slug cannot be empty.", error: true);
                return;
            }
            if (string.IsNullOrWhiteSpace(_displayName))
            {
                SetStatus("Display name cannot be empty.", error: true);
                return;
            }

            string folder = GetPackFolder();
            EnsureDirectory(folder);

            // 1. Create the 9 canonical SOs
            var enemy    = CreateSO<EnemyStatConfigSO>(folder,      $"EnemyStatConfig_{slug}");
            var wave     = CreateSO<WaveConfigSO>(folder,           $"WaveConfig_{slug}");
            var economy  = CreateSO<EconomyConfigSO>(folder,        $"EconomyConfig_{slug}");
            var upgSel   = CreateSO<UpgradeSelectionConfigSO>(folder,$"UpgradeSelectionConfig_{slug}");
            var prestige = CreateSO<PrestigeConfigSO>(folder,       $"PrestigeConfig_{slug}");
            var realm    = CreateSO<RealmIdentityConfigSO>(folder,   $"RealmIdentityConfig_{slug}");
            var player   = CreateSO<PlayerBaseStatConfigSO>(folder,  $"PlayerBaseStatConfig_{slug}");
            var schema   = CreateSO<SchemaVersionSO>(folder,         $"SchemaVersion_{slug}");

            // Set identity on RealmIdentityConfigSO
            if (realm != null)
            {
                realm.RealmSlug   = slug;
                realm.DisplayName = _displayName;
                EditorUtility.SetDirty(realm);
            }

            // 2. Create or load RealmPackSO
            string packPath = $"{folder}/RealmPack_{slug}.asset";
            RealmPackSO pack = AssetDatabase.LoadAssetAtPath<RealmPackSO>(packPath);
            bool packWasNew = pack == null;
            if (packWasNew)
            {
                pack = ScriptableObject.CreateInstance<RealmPackSO>();
                AssetDatabase.CreateAsset(pack, packPath);
            }

            // Wire all 9 fields
            pack.RealmSlug              = slug;
            pack.EnemyStatConfig        = LoadOrUse(enemy,   folder, $"EnemyStatConfig_{slug}");
            pack.WaveConfig             = LoadOrUse(wave,    folder, $"WaveConfig_{slug}");
            pack.EconomyConfig          = LoadOrUse(economy, folder, $"EconomyConfig_{slug}");
            pack.UpgradeNodeConfigs     = new UpgradeNodeConfigSO[0]; // populated by designer
            pack.UpgradeSelectionConfig = LoadOrUse(upgSel,  folder, $"UpgradeSelectionConfig_{slug}");
            pack.PrestigeConfig         = LoadOrUse(prestige,folder, $"PrestigeConfig_{slug}");
            pack.RealmIdentityConfig    = LoadOrUse(realm,   folder, $"RealmIdentityConfig_{slug}");
            pack.PlayerBaseStatConfig   = LoadOrUse(player,  folder, $"PlayerBaseStatConfig_{slug}");
            pack.SchemaVersion          = LoadOrUse(schema,  folder, $"SchemaVersion_{slug}");

            EditorUtility.SetDirty(pack);

            // 3. Register in RealmRegistrySO
            RegisterInRegistry(pack, slug, _displayName, _isDefault, _unlockPrestige);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SetStatus(
                packWasNew
                    ? $"Realm pack '{slug}' created at {folder}/"
                    : $"Realm pack '{slug}' updated at {folder}/",
                error: false);

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = pack;
            EditorGUIUtility.PingObject(pack);

            Debug.Log($"[ContentPackWizard] Realm pack '{slug}' ready at {packPath}");
        }

        private void RegisterInRegistry(
            RealmPackSO pack, string slug, string displayName,
            bool isDefault, int unlockPrestige)
        {
            // Find existing RealmRegistrySO
            string[] guids = AssetDatabase.FindAssets("t:RealmRegistrySO");
            RealmRegistrySO registry = null;
            string registryPath = null;

            if (guids.Length > 0)
            {
                registryPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                registry = AssetDatabase.LoadAssetAtPath<RealmRegistrySO>(registryPath);
            }

            if (registry == null)
            {
                // Create one next to the pack folder
                string parent = _outputRoot;
                EnsureDirectory(parent);
                registryPath = $"{parent}/RealmRegistry.asset";
                registry = ScriptableObject.CreateInstance<RealmRegistrySO>();
                AssetDatabase.CreateAsset(registry, registryPath);
                Debug.Log($"[ContentPackWizard] Created new RealmRegistrySO at {registryPath}");
            }

            // Check if slug already registered
            bool alreadyRegistered = false;
            var entries = new System.Collections.Generic.List<RealmEntry>(registry.Realms ?? new RealmEntry[0]);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Slug == slug)
                {
                    // Update existing entry
                    entries[i] = new RealmEntry
                    {
                        Slug                    = slug,
                        DisplayName             = displayName,
                        IsDefaultRealm          = isDefault,
                        UnlockPrestigeThreshold = unlockPrestige,
                        Pack                    = pack,
                    };
                    alreadyRegistered = true;
                    break;
                }
            }

            if (!alreadyRegistered)
            {
                entries.Add(new RealmEntry
                {
                    Slug                    = slug,
                    DisplayName             = displayName,
                    IsDefaultRealm          = isDefault,
                    UnlockPrestigeThreshold = unlockPrestige,
                    Pack                    = pack,
                });
            }

            registry.Realms = entries.ToArray();
            EditorUtility.SetDirty(registry);

            Debug.Log($"[ContentPackWizard] {(alreadyRegistered ? "Updated" : "Registered")} " +
                      $"realm '{slug}' in {registryPath}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string GetPackFolder()
            => $"{_outputRoot.TrimEnd('/')}/{SanitizeSlug(_slug)}";

        private void FindAndPingRegistry()
        {
            string[] guids = AssetDatabase.FindAssets("t:RealmRegistrySO");
            if (guids.Length == 0)
            {
                SetStatus("No RealmRegistrySO found in project. It will be created on pack generation.", error: false);
                return;
            }
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var so = AssetDatabase.LoadAssetAtPath<RealmRegistrySO>(path);
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = so;
            EditorGUIUtility.PingObject(so);
            SetStatus($"Registry: {path}", error: false);
        }

        private static T CreateSO<T>(string folder, string assetName) where T : ScriptableObject
        {
            string path = $"{folder}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return null; // already exists
            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        /// <summary>Returns the newly-created SO if non-null, otherwise loads the existing asset.</summary>
        private static T LoadOrUse<T>(T created, string folder, string assetName)
            where T : ScriptableObject
        {
            if (created != null) return created;
            return AssetDatabase.LoadAssetAtPath<T>($"{folder}/{assetName}.asset");
        }

        private static void EnsureDirectory(string assetRelativePath)
        {
            string full = Path.Combine(Application.dataPath, "..",
                assetRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full))
                Directory.CreateDirectory(full);
        }

        private static string SanitizeSlug(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var sb = new System.Text.StringBuilder();
            foreach (char c in raw.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if ((c == '-' || c == '_' || c == ' ') && sb.Length > 0) sb.Append('-');
            }
            // trim trailing dash
            string s = sb.ToString().TrimEnd('-');
            return s;
        }

        // ── UI helpers ────────────────────────────────────────────────────────────

        private VisualElement FieldRow(string label, string initial, System.Action<string> onChange)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems    = Align.Center;
            row.style.marginBottom  = 6;

            var lbl = new Label(label);
            lbl.style.width   = 140;
            lbl.style.fontSize = 12;

            var field = new TextField { value = initial };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));

            row.Add(lbl);
            row.Add(field);
            return row;
        }

        private static Label SectionLabel(string text)
        {
            var lbl = new Label(text);
            lbl.style.fontSize   = 11;
            lbl.style.color      = new Color(0.55f, 0.75f, 1f);
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.marginTop  = 8;
            lbl.style.marginBottom = 4;
            return lbl;
        }

        private static void ApplyRadius(VisualElement el, float r)
        {
            el.style.borderTopLeftRadius     = r;
            el.style.borderTopRightRadius    = r;
            el.style.borderBottomLeftRadius  = r;
            el.style.borderBottomRightRadius = r;
        }

        private void SetStatus(string msg, bool error)
        {
            if (_statusLabel == null) return;
            _statusLabel.text = msg;
            _statusLabel.style.color = error
                ? new Color(1f, 0.4f, 0.4f)
                : new Color(0.4f, 1f, 0.5f);
        }
    }
}
