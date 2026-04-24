using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Damage;
using EndlessEngine.Enemy;
using EndlessEngine.Wave;

namespace EndlessEngine.Combat
{
    /// <summary>
    /// Orchestrates the real-time auto-battle loop. Sequencing conductor only —
    /// does not own combat math; delegates to DamageSystem, EnemyManager, WaveSpawnManager.
    ///
    /// State machine:
    ///   Inactive → Active ↔ WaveTransition ↔ UpgradeSelection
    ///                     ↕
    ///                CombatPaused
    ///
    /// ADR: ADR-0005 — Damage Event Bus Architecture
    /// GDD: design/gdd/auto-battle-combat.md
    /// </summary>
    public class AutoBattleController : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when an enemy HP reaches 0 during active combat. Parameters: killed agent.</summary>
        public static event Action<EnemyAgent> OnEnemyKilled;

        /// <summary>Fires when all enemies in the current wave are dead. Parameters: completed wave number.</summary>
        public static event Action<int> OnWaveComplete;

        /// <summary>Fires after WaveTransitionDelaySeconds when combat resumes. Parameters: next wave number.</summary>
        public static event Action<int> OnWaveTransitionComplete;

        /// <summary>Fires at upgrade-selection milestones (every UpgradeSelectionWaveInterval waves).</summary>
        public static event Action OnUpgradeSelectionTriggered;

        /// <summary>Fires when the player entity dies (HP ≤ 0).</summary>
        public static event Action OnPlayerDied;

        // ── Dependencies ──────────────────────────────────────────────────────────

        private EnemyManager       _enemyManager;
        private WaveSpawnManager   _waveSpawnManager;
        private IUpgradeStatProvider _statProvider;
        private PlayerBaseStatConfigSO _playerConfig;
        private WaveConfigSO           _waveConfig;
        private int                    _playerId;     // entity ID used by DamageSystem for the player

        // ── State machine ─────────────────────────────────────────────────────────

        private CombatState _state = CombatState.Inactive;

        // ── Runtime attack state ──────────────────────────────────────────────────

        private float      _attackTimer;
        private float      _targetUpdateTimer;
        private EnemyAgent _currentTarget;     // nearest enemy — null when no target

        // ── Cached effective stats (refreshed at wave start / upgrade selection) ──

        private float _effectiveAttackDamage;
        private float _effectiveAttackInterval;
        private float _effectiveCritChance;
        private float _effectiveCritMultiplier;

        // ── Wave transition state ─────────────────────────────────────────────────

        private int   _lastCompletedWave;
        private bool  _upgradeSelectionInProgress;   // EC-ABC-05 guard
        private Coroutine _waveTransitionCoroutine;

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Wire all dependencies before the first Update tick.
        /// <paramref name="playerId"/> is the entity instance ID that <see cref="DamageSystem"/> uses for the player.
        /// </summary>
        public void Initialize(
            EnemyManager         enemyManager,
            WaveSpawnManager     waveSpawnManager,
            IUpgradeStatProvider statProvider,
            PlayerBaseStatConfigSO playerConfig,
            WaveConfigSO           waveConfig,
            int                    playerId)
        {
            _enemyManager     = enemyManager;
            _waveSpawnManager = waveSpawnManager;
            _statProvider     = statProvider;
            _playerConfig     = playerConfig;
            _waveConfig       = waveConfig;
            _playerId         = playerId;

            // Subscribe to upstream events
            WaveSpawnManager.OnWaveComplete      += HandleWaveComplete;
            Health.PlayerHealthComponent.OnPlayerEnteredIdleRecovery += HandlePlayerIdleRecovery;

            // Upgrade selection returns to combat
            // (OnUpgradeSelected not yet raised by any system; wired here for future use)
        }

        private void OnDestroy()
        {
            WaveSpawnManager.OnWaveComplete      -= HandleWaveComplete;
            Health.PlayerHealthComponent.OnPlayerEnteredIdleRecovery -= HandlePlayerIdleRecovery;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by the bootstrap or save-load system once save data is loaded.
        /// Transitions from Inactive → Active and caches stats.
        /// </summary>
        public void StartCombat()
        {
            if (_state != CombatState.Inactive) return;
            CacheStats();
            _attackTimer       = _effectiveAttackInterval;
            _targetUpdateTimer = 0f;           // compute target immediately
            _state             = CombatState.Active;
        }

        /// <summary>
        /// Called by UpgradeSelectionUI (or a future skill) when the player selects an upgrade card.
        /// Refreshes cached stats and resumes the wave transition.
        /// </summary>
        public void NotifyUpgradeSelected()
        {
            if (_state != CombatState.UpgradeSelection) return;
            _upgradeSelectionInProgress = false;
            CacheStats();
            // Re-enter wave transition to trigger the next wave
            BeginWaveTransition(_lastCompletedWave);
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Update()
        {
            if (_state != CombatState.Active) return;

            float dt = Time.deltaTime;

            // Target recompute on interval (GDD Rule 2 / F2)
            _targetUpdateTimer -= dt;
            if (_targetUpdateTimer <= 0f)
            {
                _targetUpdateTimer = _playerConfig.AttackTargetUpdateInterval;
                RecomputeTarget();
            }

            // Auto-attack timer (GDD Rule 3 / F1)
            _attackTimer -= dt;
            if (_attackTimer <= 0f)
            {
                if (_currentTarget != null && _currentTarget.State != EnemyState.Dead)
                    FireAutoAttack();
                // Timer always resets — no "saved" attack (GDD Rule 3)
                _attackTimer = _effectiveAttackInterval;
            }
        }

        // ── Target selection ──────────────────────────────────────────────────────

        private void RecomputeTarget()
        {
            if (_enemyManager == null) { _currentTarget = null; return; }

            IReadOnlyList<EnemyAgent> enemies = _enemyManager.ActiveEnemies;
            if (enemies.Count == 0) { _currentTarget = null; return; }

            Vector2 playerPos = GetPlayerPosition();
            float   bestSqDist = float.MaxValue;
            EnemyAgent best = default;
            bool found = false;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyAgent e = enemies[i];
                if (e.State == EnemyState.Dead) continue;
                // AC-ABC-11: SqrMagnitude only — no sqrt
                float sqDist = (e.Position - playerPos).sqrMagnitude;
                if (sqDist < bestSqDist) { bestSqDist = sqDist; best = e; found = true; }
            }

            _currentTarget = found ? best : null;
        }

        // ── Attack ────────────────────────────────────────────────────────────────

        private void FireAutoAttack()
        {
            DamageSystem.ResolveDamage(
                rawDamage:          _effectiveAttackDamage,
                attacker:           AttackerType.Player,
                damageType:         DamageType.Attack,
                targetId:           _currentTarget.InstanceId,
                hitPos:             _currentTarget.Position,
                isPlayerInvincible: false
            );
        }

        // ── Enemy-kill routing ────────────────────────────────────────────────────

        /// <summary>
        /// Called when EnemyManager.OnEnemyKilled fires.
        /// Wired by the bootstrap/scene: EnemyManager.OnEnemyKilled += HandleEnemyKilledByManager.
        /// </summary>
        public void HandleEnemyKilledByManager(EnemyAgent agent)
        {
            // EC-ABC-07: ignore kills while combat is paused (enemy was despawned, not killed)
            if (_state == CombatState.CombatPaused) return;
            if (_state == CombatState.Inactive)     return;

            // AC-ABC-04: raise OnEnemyKilled, notify wave spawning, clear from target
            OnEnemyKilled?.Invoke(agent);
            _waveSpawnManager?.OnEnemyDied();

            if (_currentTarget != null && _currentTarget.InstanceId == agent.InstanceId)
                _currentTarget = null;
        }

        // ── Player death ──────────────────────────────────────────────────────────

        /// <summary>Subscribe to PlayerHealthComponent.OnEntityDied to handle player death.</summary>
        public void HandlePlayerDied(int entityId, string vfxTag, Vector2 worldPos)
        {
            if (entityId != _playerId) return;
            if (_state == CombatState.CombatPaused) return;

            // AC-ABC-08: freeze combat
            _state = CombatState.CombatPaused;
            OnPlayerDied?.Invoke();
        }

        // ── Idle recovery return ──────────────────────────────────────────────────

        private void HandlePlayerIdleRecovery()
        {
            // AC-ABC-09: idle recovery completes → player returns to combat
            if (_state != CombatState.CombatPaused) return;
            CacheStats();
            _state = CombatState.Active;
        }

        // ── Wave lifecycle ────────────────────────────────────────────────────────

        private void HandleWaveComplete(int waveNumber)
        {
            if (_state != CombatState.Active) return;

            _lastCompletedWave = waveNumber;
            _state = CombatState.WaveTransition;

            OnWaveComplete?.Invoke(waveNumber);

            // AC-ABC-07: upgrade milestone check (EC-ABC-05 guard)
            if (!_upgradeSelectionInProgress &&
                _waveConfig != null &&
                waveNumber % _waveConfig.UpgradeSelectionWaveInterval == 0)
            {
                _upgradeSelectionInProgress = true;
                _state = CombatState.UpgradeSelection;
                OnUpgradeSelectionTriggered?.Invoke();
                // Combat waits for NotifyUpgradeSelected() to resume transition
                return;
            }

            BeginWaveTransition(waveNumber);
        }

        private void BeginWaveTransition(int completedWave)
        {
            _state = CombatState.WaveTransition;
            if (_waveTransitionCoroutine != null)
                StopCoroutine(_waveTransitionCoroutine);
            _waveTransitionCoroutine = StartCoroutine(WaveTransitionCoroutine(completedWave));
        }

        private IEnumerator WaveTransitionCoroutine(int completedWave)
        {
            float delay = _waveConfig != null ? _waveConfig.WaveTransitionDelaySeconds : 1.5f;
            yield return new WaitForSeconds(delay);

            int nextWave = completedWave + 1;
            _state = CombatState.Active;
            CacheStats();
            _attackTimer = _effectiveAttackInterval;

            OnWaveTransitionComplete?.Invoke(nextWave);
            _waveTransitionCoroutine = null;
        }

        // ── Stat caching ──────────────────────────────────────────────────────────

        private void CacheStats()
        {
            if (_statProvider == null) return;
            _effectiveAttackDamage    = _statProvider.GetAttackDamage();
            _effectiveAttackInterval  = Mathf.Max(0.05f, _statProvider.GetAttackInterval());
            _effectiveCritChance      = _statProvider.GetCritChance();
            _effectiveCritMultiplier  = _statProvider.GetCritMultiplier();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private Vector2 GetPlayerPosition()
        {
            // PlayerHealthComponent implements IPlayerQuery — EnemyManager caches it.
            // For ABC, read from EnemyManager's player query reference.
            // In tests this is injected; in runtime the VS scene wires it.
            return _playerQueryForPosition?.Position ?? Vector2.zero;
        }

        // ABC needs player position for targeting but doesn't own IPlayerQuery.
        // Bootstrap injects it via SetPlayerQuery().
        private IPlayerQuery _playerQueryForPosition;

        /// <summary>Provide the player's position source. Called by bootstrap.</summary>
        public void SetPlayerQuery(IPlayerQuery playerQuery)
        {
            _playerQueryForPosition = playerQuery;
        }

        // ── State accessor ────────────────────────────────────────────────────────

        /// <summary>Current state machine state. Read by integration tests.</summary>
        public CombatState State => _state;

        // ── Test support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Returns the current nearest-enemy target (may be null/default).</summary>
        public EnemyAgent GetCurrentTargetForTesting() => _currentTarget;

        /// <summary>Forces state for testing without going through event wiring.</summary>
        public void SetStateForTesting(CombatState state) => _state = state;

        /// <summary>Forces the upgrade-selection-in-progress guard for EC-ABC-05 testing.</summary>
        public void SetUpgradeSelectionInProgressForTesting(bool value) => _upgradeSelectionInProgress = value;

        /// <summary>Fires HandleWaveComplete from a test without touching WaveSpawnManager events.</summary>
        public void SimulateWaveCompleteForTesting(int waveNumber) => HandleWaveComplete(waveNumber);

        /// <summary>Fires HandleEnemyKilledByManager from a test.</summary>
        public void SimulateEnemyKilledForTesting(EnemyAgent agent) => HandleEnemyKilledByManager(agent);

        /// <summary>Fires HandlePlayerDied from a test.</summary>
        public void SimulatePlayerDiedForTesting(int entityId) => HandlePlayerDied(entityId, "player_death", Vector2.zero);

        /// <summary>Exposes CacheStats() for stat-injection tests.</summary>
        public void CacheStatsForTesting() => CacheStats();

        /// <summary>
        /// Forces one target recompute + one attack fire without waiting for Update().
        /// Call after StartCombat() and after manually seeding enemies via EnemyManager.
        /// </summary>
        public void SimulateAttackTickForTesting()
        {
            RecomputeTarget();
            if (_currentTarget != null && _currentTarget.State != EnemyState.Dead)
                FireAutoAttack();
        }

        /// <summary>Clears all static event subscribers. Call in test TearDown.</summary>
        public static void ClearStaticSubscribersForTesting()
        {
            OnEnemyKilled              = null;
            OnWaveComplete             = null;
            OnWaveTransitionComplete   = null;
            OnUpgradeSelectionTriggered = null;
            OnPlayerDied               = null;
        }
#endif
    }

    /// <summary>State machine states for <see cref="AutoBattleController"/>.</summary>
    public enum CombatState
    {
        Inactive,
        Active,
        WaveTransition,
        UpgradeSelection,
        CombatPaused
    }
}
