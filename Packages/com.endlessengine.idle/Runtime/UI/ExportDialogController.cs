using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Export;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Controls the export/import dialog: export button copies base64 code to clipboard,
    /// import reads from the text field, validates, and applies the save.
    ///
    /// Attach to a UIDocument whose Source Asset is ExportDialog.uxml.
    /// Wire ExportService and SaveService in Inspector.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ExportDialogController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ExportService _exportService;
        [SerializeField] private SaveService   _saveService;

        // ── UI Elements ──────────────────────────────────────────────────────────

        private VisualElement _root;
        private Button        _closeButton;
        private Button        _exportButton;
        private Label         _exportSuccessLabel;
        private TextField     _importCodeField;
        private Button        _importButton;
        private Label         _importErrorLabel;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            var doc     = GetComponent<UIDocument>();
            var docRoot = doc.rootVisualElement;

            _root               = docRoot.Q<VisualElement>("export-root");
            _closeButton        = docRoot.Q<Button>("close-button");
            _exportButton       = docRoot.Q<Button>("export-btn");
            _exportSuccessLabel = docRoot.Q<Label>("export-success-label");
            _importCodeField    = docRoot.Q<TextField>("import-code-field");
            _importButton       = docRoot.Q<Button>("import-btn");
            _importErrorLabel   = docRoot.Q<Label>("import-error-label");

            _closeButton?.RegisterCallback<ClickEvent>(_ => Hide());
            _exportButton?.RegisterCallback<ClickEvent>(_ => OnExport());
            _importButton?.RegisterCallback<ClickEvent>(_ => OnImport());

            ExportService.OnExportComplete += OnExportComplete;
            ExportService.OnImportFailed   += OnImportFailed;
        }

        private void OnDisable()
        {
            ExportService.OnExportComplete -= OnExportComplete;
            ExportService.OnImportFailed   -= OnImportFailed;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        public void Show()
        {
            ClearMessages();
            _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
        }

        // ── Export ───────────────────────────────────────────────────────────────

        private void OnExport()
        {
            if (_exportService == null) return;
            ClearMessages();
            _exportService.ExportCurrentSave();
        }

        private void OnExportComplete(string _code)
        {
            if (_exportSuccessLabel == null) return;
            _exportSuccessLabel.style.display = DisplayStyle.Flex;
            CancelInvoke(nameof(HideExportSuccess));
            Invoke(nameof(HideExportSuccess), 3f);
        }

        private void HideExportSuccess()
        {
            if (_exportSuccessLabel != null)
                _exportSuccessLabel.style.display = DisplayStyle.None;
        }

        // ── Import ───────────────────────────────────────────────────────────────

        private void OnImport()
        {
            if (_exportService == null || _saveService == null || _importCodeField == null) return;
            ClearMessages();

            string code = _importCodeField.value?.Trim();
            if (string.IsNullOrEmpty(code))
            {
                ShowError("Paste an export code first.");
                return;
            }

            if (_exportService.TryImportFromCode(code, out var saveData))
            {
                _saveService.ApplyImportedSaveData(saveData);
                Hide();
            }
        }

        private void OnImportFailed(string reason)
        {
            ShowError(reason switch
            {
                "EmptyCode"             => "No code provided.",
                "InvalidBase64"         => "Invalid code — could not decode.",
                "NullAfterDeserialize"  => "Code parsed to empty data.",
                "InvalidSchemaVersion"  => "Save code is from an incompatible version.",
                "DeserializeError"      => "Code is corrupted or malformed.",
                _                       => "Import failed: " + reason
            });
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void ShowError(string message)
        {
            if (_importErrorLabel == null) return;
            _importErrorLabel.text            = message;
            _importErrorLabel.style.display   = DisplayStyle.Flex;
        }

        private void ClearMessages()
        {
            if (_exportSuccessLabel != null)
                _exportSuccessLabel.style.display = DisplayStyle.None;
            if (_importErrorLabel != null)
                _importErrorLabel.style.display   = DisplayStyle.None;
        }
    }
}
