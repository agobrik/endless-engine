using System;
using UnityEngine;

namespace EndlessEngine.Health
{
    /// <summary>
    /// Plain C# enemy health component. Not a MonoBehaviour — managed by Wave Spawning's object pool.
    /// Call <see cref="Initialize"/> on pool check-out; call <see cref="Reset"/> on pool return.
    ///
    /// ADR: ADR-0005 — Damage Event Bus Architecture
    /// </summary>
    public class HealthComponent
    {
        /// <summary>Current HP. Never below 0.</summary>
        public float CurrentHP { get; private set; }

        /// <summary>Max HP set during <see cref="Initialize"/>.</summary>
        public float MaxHP { get; private set; }

        /// <summary>Instance ID of the enemy (used for DamageSystem routing).</summary>
        public int EntityID { get; private set; }

        /// <summary>VFX tag passed to <see cref="HealthSystem.OnEntityDied"/> on death.</summary>
        public string DeathVFXTag { get; private set; }

        private bool _isDead;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Configures this component for a freshly pooled enemy.
        /// Called by Wave Spawning at pool check-out.
        /// </summary>
        public void Initialize(int entityId, float maxHP, string deathVFXTag)
        {
            EntityID    = entityId;
            MaxHP       = maxHP;
            CurrentHP   = maxHP;
            DeathVFXTag = deathVFXTag;
            _isDead     = false;
        }

        /// <summary>
        /// Resets HP to max and clears death state.
        /// Called by Wave Spawning on pool return so the component can be reused.
        /// </summary>
        public void Reset()
        {
            CurrentHP = MaxHP;
            _isDead   = false;
        }

        // ── Damage ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies <paramref name="finalDamage"/> to <see cref="CurrentHP"/>.
        /// Fires <see cref="HealthSystem.OnEntityDied"/> at zero HP (once per life).
        /// Called by <see cref="HealthSystem"/> which bridges <c>DamageSystem.OnDamageResolved</c>
        /// to the correct <c>HealthComponent</c> instance.
        /// </summary>
        public void ApplyDamage(long finalDamage, Vector2 hitPos)
        {
            if (_isDead) return;

            CurrentHP = Mathf.Max(0f, CurrentHP - finalDamage);

            if (CurrentHP <= 0f)
            {
                _isDead = true;
                HealthSystem.RaiseEntityDied(EntityID, DeathVFXTag, hitPos);
            }
        }
    }
}
