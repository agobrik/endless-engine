using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Damage;

namespace EndlessEngine.Health
{
    /// <summary>
    /// Bridges <see cref="DamageSystem.OnDamageResolved"/> to the correct
    /// <see cref="HealthComponent"/> instance by entity ID.
    ///
    /// Register enemy components via <see cref="Register"/> at spawn;
    /// unregister via <see cref="Unregister"/> at pool return.
    ///
    /// ADR: ADR-0005 — Damage Event Bus Architecture
    /// </summary>
    public class HealthSystem : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires when any entity (enemy) reaches zero HP.
        /// Parameters: (entityInstanceId, vfxTag, worldPosition).
        /// Subscribers: Wave Spawning (pool return), Economy System (resource drop), VFX System.
        /// </summary>
        public static event Action<int, string, Vector2> OnEntityDied;

        // ── Registry ──────────────────────────────────────────────────────────────

        private readonly Dictionary<int, HealthComponent> _registry
            = new Dictionary<int, HealthComponent>();

        private void Awake()
        {
            DamageSystem.OnDamageResolved += HandleDamageResolved;
        }

        private void OnDestroy()
        {
            DamageSystem.OnDamageResolved -= HandleDamageResolved;
        }

        /// <summary>Registers an enemy HealthComponent so damage events can reach it.</summary>
        public void Register(HealthComponent component)
        {
            _registry[component.EntityID] = component;
        }

        /// <summary>Unregisters an enemy on pool return.</summary>
        public void Unregister(int entityId)
        {
            _registry.Remove(entityId);
        }

        // ── Damage routing ────────────────────────────────────────────────────────

        private void HandleDamageResolved(DamageHit hit)
        {
            if (_registry.TryGetValue(hit.TargetID, out var component))
                component.ApplyDamage(hit.FinalDamage, hit.HitPosition);
        }

        // ── Internal API ──────────────────────────────────────────────────────────

        /// <summary>Internal raise method — allows HealthComponent to fire the event.</summary>
        internal static void RaiseEntityDied(int entityId, string vfxTag, Vector2 pos)
            => OnEntityDied?.Invoke(entityId, vfxTag, pos);

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Clears all static event subscribers. Call in test TearDown.</summary>
        public static void ClearStaticSubscribersForTesting() => OnEntityDied = null;
#endif
    }
}
