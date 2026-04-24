// Unit tests for PhysicsMovement static utility.
// PlayerMovementSystem itself requires a Rigidbody2D MonoBehaviour — those tests
// belong in integration; only the pure math utility (PhysicsMovement) is covered here.
//
// ADR: ADR-0008 — Physics 2D Movement Strategy
// Story: B3 — Wire player movement (Release blocker resolution)

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Physics;

namespace EndlessEngine.Tests.Unit.PhysicsMovement
{
    [TestFixture]
    public class PhysicsMovementTests
    {
        private static readonly Rect Arena = new Rect(-10f, -6f, 20f, 12f);  // xMin=-10, xMax=10, yMin=-6, yMax=6

        // ── ComputeNewPosition ────────────────────────────────────────────────────

        [Test]
        public void ComputeNewPosition_ZeroDirection_ReturnsSamePosition()
        {
            var start = new Vector2(1f, 2f);
            Vector2 result = global::EndlessEngine.Physics.PhysicsMovement.ComputeNewPosition(
                start, Vector2.zero, speed: 5f, deltaTime: 0.016f, arenaBounds: Arena);
            Assert.AreEqual(start, result, "Zero direction must return currentPos unchanged.");
        }

        [Test]
        public void ComputeNewPosition_RightDirection_MovesCorrectDistance()
        {
            var start = new Vector2(0f, 0f);
            float speed = 5f;
            float dt    = 0.1f;
            Vector2 result = global::EndlessEngine.Physics.PhysicsMovement.ComputeNewPosition(
                start, Vector2.right, speed, dt, Arena);
            Assert.AreEqual(0.5f, result.x, 0.0001f, "x should advance by speed * deltaTime.");
            Assert.AreEqual(0f,   result.y, 0.0001f, "y should not change for right-direction move.");
        }

        [Test]
        public void ComputeNewPosition_DiagonalDirection_NormalizesBeforeMoving()
        {
            // Un-normalized diagonal (2,2) should produce the same displacement as normalized (1,1)*speed*dt
            var start      = new Vector2(0f, 0f);
            float speed    = 4f;
            float dt       = 0.25f;
            var rawDiag    = new Vector2(2f, 2f);                      // magnitude = sqrt(8) ≠ 1
            var normDiag   = new Vector2(0.7071f, 0.7071f);            // normalized approximation
            float expected = speed * dt * 0.7071f;

            Vector2 result = global::EndlessEngine.Physics.PhysicsMovement.ComputeNewPosition(
                start, rawDiag, speed, dt, Arena);

            Assert.AreEqual(expected, result.x, 0.001f, "Diagonal x displacement should be normalized.");
            Assert.AreEqual(expected, result.y, 0.001f, "Diagonal y displacement should be normalized.");
        }

        [Test]
        public void ComputeNewPosition_AtRightBoundary_ClampsPastWall()
        {
            // Place entity just inside right wall; move right — must clamp at xMax
            var start  = new Vector2(9.9f, 0f);
            Vector2 result = global::EndlessEngine.Physics.PhysicsMovement.ComputeNewPosition(
                start, Vector2.right, speed: 10f, deltaTime: 0.1f, arenaBounds: Arena);
            Assert.LessOrEqual(result.x, Arena.xMax, "x must not exceed Arena.xMax after right-wall clamp.");
        }

        [Test]
        public void ComputeNewPosition_AtTopBoundary_ClampsPastWall()
        {
            var start = new Vector2(0f, 5.9f);
            Vector2 result = global::EndlessEngine.Physics.PhysicsMovement.ComputeNewPosition(
                start, Vector2.up, speed: 10f, deltaTime: 0.1f, arenaBounds: Arena);
            Assert.LessOrEqual(result.y, Arena.yMax, "y must not exceed Arena.yMax after top-wall clamp.");
        }

        [Test]
        public void ComputeNewPosition_AtLeftBoundary_ClampsPastWall()
        {
            var start = new Vector2(-9.9f, 0f);
            Vector2 result = global::EndlessEngine.Physics.PhysicsMovement.ComputeNewPosition(
                start, Vector2.left, speed: 10f, deltaTime: 0.1f, arenaBounds: Arena);
            Assert.GreaterOrEqual(result.x, Arena.xMin, "x must not fall below Arena.xMin after left-wall clamp.");
        }

        // ── ClampToArenaBounds ────────────────────────────────────────────────────

        [Test]
        public void ClampToArenaBounds_InsideBounds_Unchanged()
        {
            var pos    = new Vector2(3f, -2f);
            Vector2 result = global::EndlessEngine.Physics.PhysicsMovement.ClampToArenaBounds(pos, Arena);
            Assert.AreEqual(pos, result, "Position already inside bounds should be returned unchanged.");
        }

        [Test]
        public void ClampToArenaBounds_OutsideAllCorners_ClampsToCorner()
        {
            // Far beyond top-right corner
            var pos    = new Vector2(50f, 50f);
            Vector2 result = global::EndlessEngine.Physics.PhysicsMovement.ClampToArenaBounds(pos, Arena);
            Assert.AreEqual(Arena.xMax, result.x, 0.0001f);
            Assert.AreEqual(Arena.yMax, result.y, 0.0001f);
        }

        [Test]
        public void ClampToArenaBounds_OnBoundaryEdge_Unchanged()
        {
            // Exactly on xMax, yMin — should not be altered
            var pos = new Vector2(Arena.xMax, Arena.yMin);
            Vector2 result = global::EndlessEngine.Physics.PhysicsMovement.ClampToArenaBounds(pos, Arena);
            Assert.AreEqual(Arena.xMax, result.x, 0.0001f);
            Assert.AreEqual(Arena.yMin, result.y, 0.0001f);
        }
    }
}
