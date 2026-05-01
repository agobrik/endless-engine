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

        private double  _lastPauseTime = double.MinValue;
        private Vector2 _prevMouseScreenPos;
        private const double PauseDebounceSeconds = 0.2;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (_playerInput == null)
                _playerInput = GetComponent<PlayerInput>();

            BindFromPlayerInput();
        }

        private void BindFromPlayerInput()
        {
            // PlayerInput.actions is the CLONED asset (Instantiate'd internally).
            // Read it only after PlayerInput has fully initialized (OnEnable/Start).
            // In production this is called from Awake which runs after PlayerInput.Awake.
            if (_playerInput?.actions == null) return;
            BindActions(_playerInput.actions);
        }

        private void BindActions(InputActionAsset asset)
        {
            // Unbind previous pause handler before rebinding
            if (_pauseAction != null)
                _pauseAction.performed -= HandlePausePerformed;

            _moveAction    = asset.FindAction("Move",    throwIfNotFound: false);
            _confirmAction = asset.FindAction("Confirm", throwIfNotFound: false);
            _cancelAction  = asset.FindAction("Cancel",  throwIfNotFound: false);
            _pauseAction   = asset.FindAction("Pause",   throwIfNotFound: false);

            if (_pauseAction != null)
                _pauseAction.performed += HandlePausePerformed;
        }

        private void OnDisable()
        {
            if (_pauseAction != null)
                _pauseAction.performed -= HandlePausePerformed;
        }

        private void OnEnable()
        {
            // After a disable/enable cycle, re-subscribe once (guard against double-sub).
            if (_pauseAction != null)
            {
                _pauseAction.performed -= HandlePausePerformed;
                _pauseAction.performed += HandlePausePerformed;
            }
        }

        private void HandlePausePerformed(InputAction.CallbackContext ctx)
        {
            if (ctx.time - _lastPauseTime < PauseDebounceSeconds) return;
            _lastPauseTime = ctx.time;
            OnPausePressed?.Invoke();
        }

        /// <summary>
        /// Binds directly to the provided asset, bypassing PlayerInput's internal clone.
        /// Call from test setup with the same asset that InputTestFixture drives.
        /// The asset must already be enabled.
        /// </summary>
        public void RebindActionsForTesting(InputActionAsset asset)
        {
            BindActions(asset);
        }

        private void Update()
        {
            _prevMouseScreenPos = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : Vector2.zero;
        }

        // ── IInputProvider ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>Returns the normalized 2D movement vector. Zero when no input.</remarks>
        public Vector2 GetMoveVector() => _moveAction != null ? _moveAction.ReadValue<Vector2>().normalized : Vector2.zero;

        /// <inheritdoc/>
        public bool GetConfirmPressed() => _confirmAction != null && _confirmAction.WasPressedThisFrame();

        /// <inheritdoc/>
        public bool GetCancelPressed() => _cancelAction != null && _cancelAction.WasPressedThisFrame();

        /// <inheritdoc/>
        public bool GetPausePressed() => _pauseAction != null && _pauseAction.WasPressedThisFrame();

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
