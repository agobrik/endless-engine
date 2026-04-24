using UnityEngine;
using EndlessEngine.Damage;

namespace EndlessEngine.Enemy
{
    /// <summary>
    /// Runtime state for a single active enemy instance.
    /// Managed by EnemyManager — not a MonoBehaviour.
    /// EnemyRuntimeData holds cached wave-scaled stats (HP, damage).
    /// EnemyAgent holds per-instance state (position, attack timer, state).
    ///
    /// ADR: ADR-0006 — Enemy Update Loop (flat list, no per-enemy MonoBehaviour)
    /// ADR: ADR-0008 — Physics 2D Movement (Rigidbody2D.MovePosition in FixedUpdate)
    /// </summary>
    public class EnemyAgent
    {
        // ── Identity ──────────────────────────────────────────────────────────────

        /// <summary>Instance ID — matches Rigidbody2D.GetInstanceID() for physics lookup.</summary>
        public int InstanceId;

        /// <summary>Rigidbody2D used for physics movement via MovePosition.</summary>
        public Rigidbody2D Rigidbody;

        // ── Combat Stats (cached from EnemyRuntimeData at spawn) ──────────────────

        /// <summary>Pre-computed wave-scaled stats (HP, damage, gold drop, etc.).</summary>
        public EnemyRuntimeData RuntimeData;

        /// <summary>Movement speed in units per second (from EnemyStatConfigSO.MoveSpeed).</summary>
        public float MoveSpeed;

        /// <summary>Time in seconds between auto-attacks (from EnemyStatConfigSO).</summary>
        public float AttackInterval;

        /// <summary>Distance threshold for attack trigger. MVP default: contact radius.</summary>
        public float AttackRange;

        /// <summary>Gold dropped on death. Computed by Wave Spawning from F1 formula.</summary>
        public long GoldDropAmount;

        // ── Per-Instance State ────────────────────────────────────────────────────

        /// <summary>Countdown timer for next attack. Initialized to AttackInterval at spawn.</summary>
        public float AttackTimer;

        /// <summary>Current behavior state.</summary>
        public EnemyState State;

        // ── Computed This Frame ───────────────────────────────────────────────────

        /// <summary>Current world position — updated from Rigidbody2D.position each frame.</summary>
        public Vector2 Position;
    }

    /// <summary>Enemy behavior states per GDD state machine.</summary>
    public enum EnemyState
    {
        /// <summary>In pool — not participating in the simulation.</summary>
        Inactive,

        /// <summary>Moving toward target position.</summary>
        Moving,

        /// <summary>Paused — player is in IdleRecovery state.</summary>
        Idle,

        /// <summary>Dead — pending reclaim by pool.</summary>
        Dead,
    }
}
