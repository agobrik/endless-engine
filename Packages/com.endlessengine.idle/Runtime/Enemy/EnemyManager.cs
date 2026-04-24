using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Damage;

namespace EndlessEngine.Enemy
{
    /// <summary>
    /// Updates all active enemies in a single O(n) loop per frame.
    /// Owns the flat active enemy list (pre-allocated to HardCapEnemiesOnScreen).
    ///
    /// Responsibilities:
    ///   - Process pending spawn queue at frame start
    ///   - Read player position once per frame (cached to _playerPositionThisFrame)
    ///   - Move each enemy toward player via Rigidbody2D.MovePosition
    ///   - Count down AttackTimer and fire attacks when timer reaches 0 and in range
    ///   - Raise OnEnemyKilled when HP reaches 0
    ///   - Pause all behavior when player is in IdleRecovery
    ///
    /// No LINQ, no per-frame heap allocations in the hot path.
    ///
    /// ADR: ADR-0006 — Enemy Update Loop (flat list, single MonoBehaviour loop)
    /// ADR: ADR-0008 — Physics 2D Movement (Rigidbody2D.MovePosition)
    /// </summary>
    public class EnemyManager : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when an enemy reaches HP 0. Subscribers: Economy (gold drop), VFX, Wave Spawning (pool reclaim).</summary>
        public static event Action<EnemyAgent> OnEnemyKilled;

        // ── Dependencies (set via Initialize) ─────────────────────────────────────

        private IPlayerQuery     _playerQuery;
        private IDamageDispatcher _damageDispatcher;

        // ── Active enemy list ─────────────────────────────────────────────────────

        /// <summary>Flat list of active enemies. Pre-allocated to HardCapEnemiesOnScreen capacity.</summary>
        private readonly List<EnemyAgent> _activeEnemies;

        /// <summary>Spawn requests queued mid-frame. Flushed at start of next frame (EC-AI-04).</summary>
        private readonly Queue<EnemyAgent> _pendingSpawns;

        /// <summary>Temporary list for enemies to remove after the main loop (avoids modify-during-iterate).</summary>
        private readonly List<EnemyAgent> _deadThisFrame;

        // ── Cached frame data ─────────────────────────────────────────────────────

        private Vector2 _playerPositionThisFrame;

        // ── Constructor ───────────────────────────────────────────────────────────

        public EnemyManager()
        {
            _activeEnemies = new List<EnemyAgent>(200);
            _pendingSpawns = new Queue<EnemyAgent>(32);
            _deadThisFrame = new List<EnemyAgent>(16);
        }

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>Inject dependencies. Call before the first Update tick.</summary>
        public void Initialize(IPlayerQuery playerQuery, IDamageDispatcher damageDispatcher)
        {
            _playerQuery      = playerQuery;
            _damageDispatcher = damageDispatcher;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Queue an enemy for addition to the active list.
        /// Thread-safe from Wave Spawning — flushed at start of next frame.
        /// </summary>
        public void SpawnEnemy(EnemyAgent agent)
        {
            agent.State       = EnemyState.Moving;
            agent.AttackTimer = agent.AttackInterval;
            _pendingSpawns.Enqueue(agent);
        }

        /// <summary>All active enemies. Read-only for external systems (AutoBattle target selection).</summary>
        public IReadOnlyList<EnemyAgent> ActiveEnemies => _activeEnemies;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Update()
        {
            // 1. Flush pending spawn queue (EC-AI-04: no list mutation during iteration)
            while (_pendingSpawns.Count > 0)
                _activeEnemies.Add(_pendingSpawns.Dequeue());

            // 2. Read player state once per frame
            bool playerInIdleRecovery = _playerQuery != null && _playerQuery.IsInIdleRecovery;
            _playerPositionThisFrame  = _playerQuery?.Position ?? Vector2.zero;

            float dt = Time.deltaTime;
            _deadThisFrame.Clear();

            // 3. Main enemy update loop — O(n), zero allocation
            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                EnemyAgent agent = _activeEnemies[i];

                if (agent.State == EnemyState.Dead)
                {
                    _deadThisFrame.Add(agent);
                    continue;
                }

                // Pause all behavior during IdleRecovery (GDD Rule 9)
                if (playerInIdleRecovery)
                {
                    agent.State = EnemyState.Idle;
                    _activeEnemies[i] = agent;
                    continue;
                }

                if (agent.State == EnemyState.Idle)
                    agent.State = EnemyState.Moving;

                // 3a. Movement (GDD Rule 2, Formula: MoveVector)
                Vector2 toPlayer = _playerPositionThisFrame - agent.Position;
                if (toPlayer.sqrMagnitude > 0.0001f) // EC-AI-01: avoid zero-vector normalize
                {
                    Vector2 dir      = toPlayer.normalized;
                    Vector2 newPos   = agent.Position + dir * (agent.MoveSpeed * dt);
                    agent.Rigidbody?.MovePosition(newPos);
                    agent.Position = agent.Rigidbody != null ? agent.Rigidbody.position : newPos;
                }

                // 3b. Attack timer countdown (GDD Rule 6)
                agent.AttackTimer -= dt;
                if (agent.AttackTimer <= 0f)
                {
                    float distSq = toPlayer.sqrMagnitude;
                    float rangeSq = agent.AttackRange * agent.AttackRange;
                    if (distSq <= rangeSq)
                    {
                        // Fire attack — DamageSystem is the sole processor
                        _damageDispatcher?.DispatchEnemyAttack(agent, _playerPositionThisFrame);
                        agent.AttackTimer = agent.AttackInterval;
                    }
                    // If out of range, AttackTimer stays ≤ 0 — fires on first frame range is entered
                }

                _activeEnemies[i] = agent;
            }

            // 4. Remove dead enemies (wave spawning reclaims via OnEnemyKilled)
            for (int i = 0; i < _deadThisFrame.Count; i++)
            {
                _activeEnemies.Remove(_deadThisFrame[i]);
                OnEnemyKilled?.Invoke(_deadThisFrame[i]);
            }
        }

        /// <summary>Mark an enemy as dead by entity ID. Called by HealthSystem when enemy HP reaches 0.</summary>
        public void MarkDead(int entityId)
        {
            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                if (_activeEnemies[i].InstanceId == entityId)
                {
                    EnemyAgent a = _activeEnemies[i];
                    a.State = EnemyState.Dead;
                    _activeEnemies[i] = a;
                    return;
                }
            }
        }

        // ── Test injection ────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Exposes active enemy list for unit tests.</summary>
        public List<EnemyAgent> ActiveEnemiesForTesting => _activeEnemies;

        /// <summary>Fires OnEnemyKilled directly for integration testing. Simulates enemy HP reaching 0.</summary>
        public static void FireEnemyKilledForTesting(EnemyAgent agent)
        {
            OnEnemyKilled?.Invoke(agent);
        }

        /// <summary>Clears all static event subscribers. Call in TearDown to prevent test bleed.</summary>
        public static void ClearStaticSubscribersForTesting()
        {
            OnEnemyKilled = null;
        }

        /// <summary>Runs one update tick with supplied player state for deterministic unit testing.</summary>
        public void TickForTesting(Vector2 playerPos, bool inIdleRecovery, float deltaTime)
        {
            while (_pendingSpawns.Count > 0)
                _activeEnemies.Add(_pendingSpawns.Dequeue());

            _playerPositionThisFrame = playerPos;
            _deadThisFrame.Clear();

            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                EnemyAgent agent = _activeEnemies[i];

                if (agent.State == EnemyState.Dead)
                {
                    _deadThisFrame.Add(agent);
                    continue;
                }

                if (inIdleRecovery)
                {
                    agent.State = EnemyState.Idle;
                    _activeEnemies[i] = agent;
                    continue;
                }

                if (agent.State == EnemyState.Idle)
                    agent.State = EnemyState.Moving;

                Vector2 toPlayer = playerPos - agent.Position;
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    Vector2 dir    = toPlayer.normalized;
                    Vector2 newPos = agent.Position + dir * (agent.MoveSpeed * deltaTime);
                    agent.Position = newPos;
                    agent.Rigidbody?.MovePosition(newPos);
                }

                agent.AttackTimer -= deltaTime;
                if (agent.AttackTimer <= 0f)
                {
                    float distSq  = toPlayer.sqrMagnitude;
                    float rangeSq = agent.AttackRange * agent.AttackRange;
                    if (distSq <= rangeSq)
                    {
                        _damageDispatcher?.DispatchEnemyAttack(agent, playerPos);
                        agent.AttackTimer = agent.AttackInterval;
                    }
                }

                _activeEnemies[i] = agent;
            }

            for (int i = 0; i < _deadThisFrame.Count; i++)
            {
                _activeEnemies.Remove(_deadThisFrame[i]);
                OnEnemyKilled?.Invoke(_deadThisFrame[i]);
            }
        }
#endif
    }
}
