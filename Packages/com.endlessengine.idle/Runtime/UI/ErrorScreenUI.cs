#pragma warning disable CS0414
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Minimal error screen displayed when boot config loading fails.
    /// Lives in the Boot scene — does not depend on any gameplay systems
    /// (those have not loaded yet when this fires).
    ///
    /// Usage:
    ///   ErrorScreenUI.Show("Config load failed: EconomyConfigSO not found.");
    ///
    /// If a UIDocument with a VisualElement named "error-root" exists in the scene,
    /// it will be populated with the error message and made visible.
    /// Otherwise falls back to a full-screen IMGUI overlay drawn until the app quits.
    /// </summary>
    public static class ErrorScreenUI
    {
        private static bool   _active;
        private static string _reason;

        /// <summary>
        /// Display an unrecoverable boot error. Game cannot continue.
        /// Attempts to show a UI Toolkit overlay; falls back to IMGUI if none is found.
        /// </summary>
        /// <param name="reason">Human-readable error description for diagnosis.</param>
        public static void Show(string reason)
        {
            _active = true;
            _reason = reason;

            Debug.LogError($"[ErrorScreenUI] FATAL: {reason}");

            // Try to populate a UIDocument error panel if one exists in the scene
            TryShowUIToolkitOverlay(reason);

            // Register IMGUI fallback via a persistent helper object
            if (Object.FindFirstObjectByType<ErrorScreenIMGUI>() == null)
            {
                var go = new GameObject("[ErrorScreenUI]");
                Object.DontDestroyOnLoad(go);
                var helper = go.AddComponent<ErrorScreenIMGUI>();
                helper.Message = reason;
            }
        }

        private static void TryShowUIToolkitOverlay(string reason)
        {
            var doc = Object.FindFirstObjectByType<UIDocument>();
            if (doc?.rootVisualElement == null) return;

            var root = doc.rootVisualElement.Q("error-root");
            if (root == null)
            {
                // Build a minimal overlay inline
                root = new VisualElement { name = "error-root" };
                root.style.position         = Position.Absolute;
                root.style.left             = 0; root.style.top    = 0;
                root.style.right            = 0; root.style.bottom = 0;
                root.style.backgroundColor  = new StyleColor(new Color(0.05f, 0f, 0f, 0.92f));
                root.style.justifyContent   = Justify.Center;
                root.style.alignItems       = Align.Center;
                doc.rootVisualElement.Add(root);
            }

            root.Clear();

            var title = new Label("FATAL ERROR — GAME CANNOT START");
            title.style.color      = new StyleColor(Color.red);
            title.style.fontSize   = 22;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 16;

            var msg = new Label(reason);
            msg.style.color        = new StyleColor(Color.white);
            msg.style.fontSize     = 14;
            msg.style.whiteSpace   = WhiteSpace.Normal;
            msg.style.maxWidth     = 600;
            msg.style.unityTextAlign = TextAnchor.UpperCenter;

            var hint = new Label("Check the Unity Console and Player.log for details.");
            hint.style.color      = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            hint.style.fontSize   = 11;
            hint.style.marginTop  = 12;

            root.Add(title);
            root.Add(msg);
            root.Add(hint);

            root.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// IMGUI fallback — drawn on top of everything when no UIDocument is available.
        /// Attached to a DontDestroyOnLoad GameObject by ErrorScreenUI.Show().
        /// </summary>
        private class ErrorScreenIMGUI : MonoBehaviour
        {
            public string Message;

            private readonly GUIStyle _boxStyle  = new GUIStyle();
            private readonly GUIStyle _textStyle = new GUIStyle();
            private bool _stylesInit;

            private void OnGUI()
            {
                if (!_stylesInit)
                {
                    _boxStyle.normal.background = Texture2D.blackTexture;
                    _textStyle.normal.textColor = Color.red;
                    _textStyle.fontSize         = 18;
                    _textStyle.wordWrap         = true;
                    _textStyle.alignment        = TextAnchor.MiddleCenter;
                    _stylesInit = true;
                }

                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, _boxStyle);
                GUI.Label(
                    new Rect(40, Screen.height * 0.35f, Screen.width - 80, Screen.height * 0.3f),
                    $"FATAL ERROR — GAME CANNOT START\n\n{Message}\n\nCheck the console and Player.log for details.",
                    _textStyle);
            }
        }
    }
}
