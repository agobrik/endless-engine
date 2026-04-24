// NOTE: This file is the ONLY file in the project permitted to import UnityEngine.InputSystem.
// All other gameplay code must use IInputProvider — never UnityEngine.InputSystem directly.
// ADR: ADR-0007 — Input Abstraction Layer

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EndlessEngine.Input
{
    /// <summary>
    /// Production implementation of <see cref="IInputProvider"/> using Unity's new Input System.
    /// Wraps a <see cref="PlayerInput"/> component wired to <c>Assets/Input/GameInputActions.inputactions</c>.
    ///
    /// Attach to the Player GameObject alongside <see cref="PlayerInput"/>.
    /// Inject as <see cref="IInputProvider"/> into gameplay systems — never reference this class directly.
    ///
    /// ADR: ADR-0007 — Input Abstraction Layer
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class InputProviderUnity : MonoBehaviour, IInputProvider
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField]
        [Tooltip("PlayerInput component on this GameObject (auto-populated in Awake if null).")]
        private PlayerInput _playerInput;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public event Action OnPausePressed;

        // ── Private state ─────────────────────────────────────────────────────────

        private InputAction _moveAction;
        private InputAction _confirmAction;
        private InputAction _cancelAction;
        private InputAction _pauseAction;

        private float   _pauseDebounceTimer;
        private Vector2 _prevMouseScreenPos;
        private const float PauseDebounceSeconds = 0.2f;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (_playerInput == null)
                _playerInput = GetComponent<PlayerInput>();

            _moveAction    = _playerInput.actions["Move"];
            _confirmAction = _playerInput.actions["Confirm"];
            _cancelAction  = _playerInput.actions["Cancel"];
            _pauseAction   = _playerInput.actions["Pause"];
        }

        private void Update()
        {
            // Decrement debounce timer
            _pauseDebounceTimer = Mathf.Max(0f, _pauseDebounceTimer - Time.deltaTime);

            // Fire OnPausePressed only once per debounce window
            if (_pauseAction.WasPressedThisFrame() && _pauseDebounceTimer <= 0f)
            {
                _pauseDebounceTimer = PauseDebounceSeconds;
                OnPausePressed?.Invoke();
            }

            // Track previous mouse position for delta calculation
            _prevMouseScreenPos = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : Vector2.zero;
        }

        // ── IInputProvider ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>Returns the normalized 2D movement vector. Zero when no input.</remarks>
        public Vector2 GetMoveVector() => _moveAction.ReadValue<Vector2>().normalized;

        /// <inheritdoc/>
        public bool GetConfirmPressed() => _confirmAction.WasPressedThisFrame();

        /// <inheritdoc/>
        public bool GetCancelPressed() => _cancelAction.WasPressedThisFrame();

        /// <inheritdoc/>
        public bool GetPausePressed() => _pauseAction.WasPressedThisFrame();

        /// <inheritdoc/>
        public Vector2 GetMouseWorldPosition()
        {
            if (Mouse.current == null || Camera.main == null) return Vector2.zero;
            Vector3 screen = Mouse.current.position.ReadValue();
            screen.z = 0f;
            return Camera.main.ScreenToWorldPoint(screen);
        }

        /// <inheritdoc/>
        public Vector2 GetMouseScreenDelta()
        {
            if (Mouse.current == null) return Vector2.zero;
            return Mouse.current.delta.ReadValue();
        }

        /// <inheritdoc/>
        public bool GetPointerClickedThisFrame()
        {
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }
    }
}
