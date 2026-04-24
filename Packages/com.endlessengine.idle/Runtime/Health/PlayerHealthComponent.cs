using System;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Damage;
using EndlessEngine.Enemy;

namespace EndlessEngine.Health
{
    /// <summary>
    /// Owns the player's HP, i-frame window, and death → idle-recovery transition.
    /// Subscribes to <see cref="DamageSystem.OnDamageResolved"/> and filters by entity ID.
    ///
    /// HP is stored as float; all UI must display <c>Mathf.Floor(CurrentHP)</c>.
    /// I-frame window is set after each surviving hit; timer decrements in <c>Update()</c>.
    ///
    /// ADR: ADR-0005 — Damage Event Bus Architecture
    /// </summary>
    public class PlayerHealthComponent : MonoBehaviour, IPlayerQuery
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires when HP changes (damage taken or heal applied).
        /// Parameters: (currentHP, maxHP). Subscribe for HUD updates.
        /// </summary>
        public static event Action<float, float> OnPlayerHPChanged;

        /// <summary>
        /// Fires once when HP reaches 0. Parameters: (entityInstanceId, vfxTag, worldPosition).
        /// The entity is NOT immediately destroyed — wait for <see cref="OnPlayerEnteredIdleRecovery"/>.
        /// </summary>
        public static event Action<int, string, Vector2> OnEntityDied;

        /// <summary>
        /// Fires after <see cref="PlayerBaseStatConfigSO.DeathTransitionDelaySeconds"/> following death.
        /// Triggers the Idle Recovery state transition.
        /// </summary>
        public static event Action OnPlayerEnteredIdleRecovery;

        // ── State ─────────────────────────────────────────────────────────────────

        /// <summary>Current HP. Never below 0.</summary>
        public float CurrentHP { get; private set; }

        /// <summary>Max HP initialised from <see cref="PlayerBaseStatConfigSO.BaseMaxHP"/>.</summary>
        public float MaxHP { get; private set; }

        /// <summary>True while i-frame timer is active — caller must pass this to ResolveDamage.</summary>
        public bool IsInvincible => _iframeTimer > 0f;

        /// <summary>True after HP reaches 0 and the player enters idle recovery state.</summary>
        public bool IsInIdleRecovery { get; private set; }

        /// <summary>Current world position for EnemyManager target tracking.</summary>
        public Vector2 Position => (Vector2)transform.position;

        private int   _entityId;
        private float _iframeTimer;
        private bool  _isDead;
        private float _deathTransitionTimer;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _entityId = gameObject.GetInstanceID();
            DamageSystem.OnDamageResolved += HandleDamageResolved;
            InitialiseFromConfig();
        }

        private void InitialiseFromConfig()
        {
            PlayerBaseStatConfigSO cfg;
            try { cfg = ConfigRegistry.Player; }
            catch { cfg = null; }
            if (cfg == null) return;
            MaxHP = CurrentHP = cfg.BaseMaxHP;
            _isDead = false;
            _iframeTimer = 0f;
            IsInIdleRecovery = false;
            OnPlayerHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        private void OnDestroy()
        {
            DamageSystem.OnDamageResolved -= HandleDamageResolved;
        }

        private void Update()
        {
            if (_iframeTimer > 0f)
                _iframeTimer -= Time.deltaTime;

            if (_isDead)
            {
                _deathTransitionTimer -= Time.deltaTime;
                if (_deathTransitionTimer <= 0f)
                {
                    _isDead               = false;
                    _deathTransitionTimer = 0f;
                    IsInIdleRecovery      = true;
                    OnPlayerEnteredIdleRecovery?.Invoke();
                }
            }
        }

        // ── Damage Handling ───────────────────────────────────────────────────────

        private void HandleDamageResolved(DamageHit hit)
        {
            if (hit.TargetID != _entityId) return; // not targeting this entity
            if (_isDead) return;                    // dead entities ignore further damage

            CurrentHP = Mathf.Max(0f, CurrentHP - hit.FinalDamage);
            OnPlayerHPChanged?.Invoke(CurrentHP, MaxHP);

            if (CurrentHP <= 0f)
            {
                _isDead               = true;
                _iframeTimer          = 0f;
                PlayerBaseStatConfigSO cfg;
                try { cfg = ConfigRegistry.Player; } catch { cfg = null; }
                _deathTransitionTimer = cfg != null ? cfg.DeathTransitionDelaySeconds : 2f;
                OnEntityDied?.Invoke(_entityId, "player_death", transform.position);
            }
            else
            {
                PlayerBaseStatConfigSO cfg;
                try { cfg = ConfigRegistry.Player; } catch { cfg = null; }
                _iframeTimer = cfg != null ? cfg.InvincibilityFramesDuration : 1f;
            }
        }

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Directly sets HP state for testing without going through the damage event.
        /// Does not fire <see cref="OnPlayerHPChanged"/>.
        /// </summary>
        public void SetHPForTesting(float current, float max)
        {
            CurrentHP = current;
            MaxHP     = max;
        }

        /// <summary>Returns the entity instance ID used for damage routing.</summary>
        public int GetEntityIdForTesting() => _entityId;

        /// <summary>
        /// Re-runs config initialisation and re-subscribes to DamageSystem events.
        /// Call in test SetUp after AddComponent and ConfigRegistry.InjectForTesting.
        /// </summary>
        public void InitialiseFromConfigForTesting()
        {
            // Re-subscribe in case Awake ran before ConfigRegistry was injected and threw
            DamageSystem.OnDamageResolved -= HandleDamageResolved;
            DamageSystem.OnDamageResolved += HandleDamageResolved;
            InitialiseFromConfig();
        }

        /// <summary>Manually starts the i-frame window for testing.</summary>
        public void SetIframeTimerForTesting(float duration) => _iframeTimer = duration;

        /// <summary>
        /// Simulates one Update tick with a given deltaTime for deterministic timer tests.
        /// </summary>
        public void SimulateUpdateForTesting(float deltaTime)
        {
            float saved = Time.deltaTime; // read-only in runtime but we use direct field below

            if (_iframeTimer > 0f)
                _iframeTimer -= deltaTime;

            if (_isDead)
            {
                _deathTransitionTimer -= deltaTime;
                if (_deathTransitionTimer <= 0f)
                {
                    _isDead               = false;
                    _deathTransitionTimer = 0f;
                    OnPlayerEnteredIdleRecovery?.Invoke();
                }
            }
        }

        /// <summary>Clears all static event subscribers. Call in test TearDown.</summary>
        public static void ClearStaticSubscribersForTesting()
        {
            OnPlayerHPChanged           = null;
            OnEntityDied                = null;
            OnPlayerEnteredIdleRecovery = null;
        }

        /// <summary>Fires OnPlayerEnteredIdleRecovery for integration testing.</summary>
        public static void RaisePlayerEnteredIdleRecoveryForTesting()
            => OnPlayerEnteredIdleRecovery?.Invoke();
#endif
    }
}
