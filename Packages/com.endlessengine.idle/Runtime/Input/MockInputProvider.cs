// Available in Editor and Development builds only — not shipped in production.
#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using UnityEngine;

namespace EndlessEngine.Input
{
    /// <summary>
    /// Test and editor-tooling implementation of <see cref="IInputProvider"/>.
    /// Set public fields directly to control what each method returns.
    /// Call <see cref="SimulatePausePress"/> to trigger the <see cref="OnPausePressed"/> event.
    ///
    /// Usage in tests:
    /// <code>
    /// var input = new MockInputProvider { MoveVector = Vector2.right };
    /// </code>
    ///
    /// ADR: ADR-0007 — Input Abstraction Layer
    /// </summary>
    public class MockInputProvider : IInputProvider
    {
        // ── Configurable return values ────────────────────────────────────────────

        /// <summary>Value returned by <see cref="GetMoveVector"/>. Defaults to zero (no movement).</summary>
        public Vector2 MoveVector     = Vector2.zero;

        /// <summary>Value returned by <see cref="GetConfirmPressed"/>.</summary>
        public bool ConfirmPressed    = false;

        /// <summary>Value returned by <see cref="GetCancelPressed"/>.</summary>
        public bool CancelPressed     = false;

        /// <summary>Value returned by <see cref="GetPausePressed"/>.</summary>
        public bool PausePressed = false;

        /// <summary>Value returned by <see cref="GetMouseWorldPosition"/>.</summary>
        public Vector2 MouseWorldPosition = Vector2.zero;

        /// <summary>Value returned by <see cref="GetMouseScreenDelta"/>.</summary>
        public Vector2 MouseScreenDelta = Vector2.zero;

        /// <summary>Value returned by <see cref="GetPointerClickedThisFrame"/>.</summary>
        public bool PointerClicked = false;

        // ── IInputProvider ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public event Action OnPausePressed;

        /// <inheritdoc/>
        public Vector2 GetMoveVector()  => MoveVector;

        /// <inheritdoc/>
        public bool GetConfirmPressed() => ConfirmPressed;

        /// <inheritdoc/>
        public bool GetCancelPressed()  => CancelPressed;

        /// <inheritdoc/>
        public bool GetPausePressed()   => PausePressed;

        /// <inheritdoc/>
        public Vector2 GetMouseWorldPosition()     => MouseWorldPosition;

        /// <inheritdoc/>
        public Vector2 GetMouseScreenDelta()       => MouseScreenDelta;

        /// <inheritdoc/>
        public bool GetPointerClickedThisFrame()   => PointerClicked;

        // ── Test helpers ──────────────────────────────────────────────────────────

        /// <summary>Directly fires the <see cref="OnPausePressed"/> event.</summary>
        public void SimulatePausePress() => OnPausePressed?.Invoke();

        /// <summary>Simulates a single click frame, then resets to false.</summary>
        public void SimulateClick() { PointerClicked = true; }
    }
}
#endif
