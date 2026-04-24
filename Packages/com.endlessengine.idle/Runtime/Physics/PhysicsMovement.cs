using UnityEngine;

namespace EndlessEngine.Physics
{
    /// <summary>
    /// Static utility providing frame-rate-independent, arena-bounded position computation
    /// for all moving entities (player and enemies). Callers apply the result via
    /// Rigidbody2D.MovePosition(). Never modifies Rigidbody2D directly.
    ///
    /// ADR: ADR-0008 — Physics 2D Movement Strategy
    /// </summary>
    public static class PhysicsMovement
    {
        /// <summary>
        /// Computes a new world-space position for a constant-speed entity and clamps it
        /// to <paramref name="arenaBounds"/>. Caller must call <c>rigidbody.MovePosition(result)</c>.
        /// Returns <paramref name="currentPos"/> unchanged when direction is zero.
        /// </summary>
        /// <param name="currentPos">Entity's current Rigidbody2D.position.</param>
        /// <param name="direction">Desired movement direction (normalized or zero).</param>
        /// <param name="speed">Movement speed in world units per second.</param>
        /// <param name="deltaTime">Time.deltaTime — frame time in seconds.</param>
        /// <param name="arenaBounds">Hard clamp boundary (from ConfigRegistry.Realm.ArenaBounds).</param>
        public static Vector2 ComputeNewPosition(
            Vector2 currentPos,
            Vector2 direction,
            float   speed,
            float   deltaTime,
            Rect    arenaBounds)
        {
            // Zero-movement optimization: skip displacement when no input (ADR-0008 guideline 6)
            if (direction.sqrMagnitude < 0.01f)
                return currentPos;

            Vector2 rawPos = currentPos + direction.normalized * speed * deltaTime;
            return ClampToArenaBounds(rawPos, arenaBounds);
        }

        /// <summary>
        /// Clamps <paramref name="pos"/> to the given <paramref name="bounds"/> rectangle.
        /// Applied as secondary guard before every MovePosition call (ADR-0008 guideline 5).
        /// Zero-allocation — operates on value types only.
        /// </summary>
        public static Vector2 ClampToArenaBounds(Vector2 pos, Rect bounds)
        {
            return new Vector2(
                Mathf.Clamp(pos.x, bounds.xMin, bounds.xMax),
                Mathf.Clamp(pos.y, bounds.yMin, bounds.yMax)
            );
        }
    }
}
