using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;
using EndlessEngine.Input;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Physics
{
    /// <summary>
    /// Reads movement input via IInputProvider and drives the player Rigidbody2D via
    /// Rigidbody2D.MovePosition() — the sole approved movement API per ADR-0008.
    ///
    /// Attach to the Player GameObject alongside Rigidbody2D and InputProviderUnity.
    /// Call Initialize() from the bootstrap before StartCombat() fires.
    ///
    /// Rigidbody2D prefab requirements (enforced by Initialize assertions in dev builds):
    ///   bodyType               = Kinematic
    ///   interpolation          = Extrapolate
    ///   collisionDetectionMode = Continuous
    ///   gravityScale           = 0
    ///
    /// ADR: ADR-0008 — Physics 2D Movement Strategy
    /// ADR: ADR-0007 — Input Abstraction Layer
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovementSystem : MonoBehaviour
    {
        // ── Private state ─────────────────────────────────────────────────────────

        private Rigidbody2D    _rb;
        private IInputProvider _input;
        private bool           _initialized;

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Wire input provider. Call once from the bootstrap after ConfigRegistry is loaded.
        /// Movement silently no-ops until initialized (safe before first Update).
        /// </summary>
        public void Initialize(IInputProvider inputProvider)
        {
            _rb    = GetComponent<Rigidbody2D>();
            _input = inputProvider;

            // Kinematic bodies ignore gravity but the field retains its inspector value.
            // Force to 0 so the validation warning below never fires.
            _rb.gravityScale = 0f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Validate Rigidbody2D configuration per ADR-0008
            if (_rb.bodyType != RigidbodyType2D.Kinematic)
                Debug.LogWarning("[PlayerMovementSystem] Rigidbody2D.bodyType should be Kinematic. Current: " + _rb.bodyType);
            if (_rb.interpolation != RigidbodyInterpolation2D.Extrapolate)
                Debug.LogWarning("[PlayerMovementSystem] Rigidbody2D.interpolation should be Extrapolate. Current: " + _rb.interpolation);
            if (_rb.collisionDetectionMode != CollisionDetectionMode2D.Continuous)
                Debug.LogWarning("[PlayerMovementSystem] Rigidbody2D.collisionDetectionMode should be Continuous. Current: " + _rb.collisionDetectionMode);
            if (!Mathf.Approximately(_rb.gravityScale, 0f))
                Debug.LogWarning("[PlayerMovementSystem] Rigidbody2D.gravityScale should be 0. Current: " + _rb.gravityScale);
#endif

            _initialized = true;
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Update()
        {
            if (!_initialized || _input == null) return;

            Vector2 dir = _input.GetMoveVector();   // normalized or zero (ADR-0007)

            // Zero-movement optimization: skip MovePosition call entirely (ADR-0008 guideline 6)
            if (dir.sqrMagnitude < 0.01f) return;

            // Read current effective speed — not cached across frames (upgrades can change it)
            float speed = UpgradeApplicationSystem.GetEffectiveStat(StatType.MoveSpeed);

            // Arena bounds — use Realm config if loaded, otherwise default to a large arena for VS
            Rect bounds;
            try { bounds = ConfigRegistry.Realm.ArenaBounds; }
            catch { bounds = new Rect(-50f, -50f, 100f, 100f); }

            Vector2 newPos = PhysicsMovement.ComputeNewPosition(_rb.position, dir, speed, Time.deltaTime, bounds);

            _rb.MovePosition(newPos);
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Returns the Rigidbody2D position. Used by integration tests to assert movement.
        /// </summary>
        public Vector2 GetPositionForTesting() => _rb != null ? _rb.position : (Vector2)transform.position;

        /// <summary>
        /// Directly sets the Rigidbody2D position. Used by integration tests to seed start position.
        /// </summary>
        public void SetPositionForTesting(Vector2 pos)
        {
            if (_rb != null)
                _rb.position = pos;
            else
                transform.position = pos;
        }
#endif
    }
}
