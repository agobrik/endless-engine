using System;
using UnityEngine;

namespace EndlessEngine.Input
{
    /// <summary>
    /// The only input contract visible to gameplay systems.
    /// All gameplay systems must receive this via dependency injection;
    /// they must never reference <c>UnityEngine.InputSystem</c> directly.
    ///
    /// Frame-polled reads should be called from <c>Update()</c>.
    /// <see cref="OnPausePressed"/> is raised with a 0.2s debounce by implementors.
    ///
    /// ADR: ADR-0007 — Input Abstraction Layer
    /// </summary>
    public interface IInputProvider
    {
        // ── Frame-polled reads ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the current movement direction as a normalized vector.
        /// Returns <see cref="Vector2.zero"/> when no movement input is active.
        /// </summary>
        Vector2 GetMoveVector();

        /// <summary>True only on the frame the Confirm action was pressed.</summary>
        bool GetConfirmPressed();

        /// <summary>True only on the frame the Cancel action was pressed.</summary>
        bool GetCancelPressed();

        /// <summary>True on the frame the Pause action was pressed (raw, no debounce).</summary>
        bool GetPausePressed();

        // ── Mouse / Pointer ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the mouse/pointer position in world space (using Camera.main).
        /// Returns <see cref="Vector2.zero"/> when no pointer device is active.
        /// Used by CursorYieldService and ZoneSystem.
        /// </summary>
        Vector2 GetMouseWorldPosition();

        /// <summary>
        /// Returns the mouse/pointer screen-space delta this frame (pixels moved).
        /// Returns <see cref="Vector2.zero"/> when the pointer is stationary.
        /// Used by CursorYieldService to measure activity speed.
        /// </summary>
        Vector2 GetMouseScreenDelta();

        /// <summary>
        /// True on the frame the primary pointer button (left mouse / touch) was pressed.
        /// Used by ClickYieldService.
        /// </summary>
        bool GetPointerClickedThisFrame();

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised with a 0.2s debounce when the Pause action is pressed.
        /// Subscribe in <c>Awake()</c> or <c>Start()</c>; unsubscribe in <c>OnDestroy()</c>.
        /// </summary>
        event Action OnPausePressed;
    }
}
