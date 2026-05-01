using System;
using UnityEngine;
using EndlessEngine.Config;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Damage
{
    /// <summary>
    /// Stateless static damage resolution service.
    /// All HP reductions in the game must flow through <see cref="ResolveDamage"/>.
    /// Direct modification of HealthComponent.CurrentHP is forbidden.
    ///
    /// ADR: ADR-0005 — Damage System Event Bus Architecture
    /// </summary>
    public static class DamageSystem
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires when damage is successfully resolved and will be applied to the target.
        /// Subscribers: HealthComponent, VFX system, audio system, combat log.
        /// </summary>
        public static event Action<DamageHit> OnDamageResolved;

        /// <summary>
        /// Fires when a damage hit is blocked by player i-frames.
        /// <c>OnDamageResolved</c> does NOT fire for the same hit.
        /// </summary>
        public static event Action<DamageHit> OnDamageBlocked;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves a raw damage value into a <see cref="DamageHit"/> and fires the
        /// appropriate event. Crit rolls apply only when <paramref name="attacker"/> is
        /// <see cref="AttackerType.Player"/>. Enemies never crit.
        /// Minimum <see cref="DamageHit.FinalDamage"/> is 1.
        /// </summary>
        /// <param name="rawDamage">Pre-crit damage value from the attacker's stats.</param>
        /// <param name="attacker">Source side (Player or Enemy).</param>
        /// <param name="damageType">Attack or Contact.</param>
        /// <param name="targetId">Instance ID of the target object.</param>
        /// <param name="hitPos">World-space hit position for VFX placement.</param>
        /// <param name="isPlayerInvincible">When true, enemy attacks fire <see cref="OnDamageBlocked"/> instead.</param>
        public static void ResolveDamage(
            float        rawDamage,
            AttackerType attacker,
            DamageType   damageType,
            int          targetId,
            Vector2      hitPos,
            bool         isPlayerInvincible)
        {
            // I-frame guard: block enemy attacks during player invincibility window
            if (attacker == AttackerType.Enemy && isPlayerInvincible)
            {
                var blocked = new DamageHit(attacker, rawDamage, false, 0L,
                                            damageType, targetId, hitPos);
                OnDamageBlocked?.Invoke(blocked);
                return;
            }

            // Crit roll — enemies never crit (ADR-0005 rule)
            bool  isCrit = false;
            float damage = rawDamage;

            if (attacker == AttackerType.Player)
            {
                float critChance = ConfigRegistry.Player.BaseCritChance;
                isCrit = UnityEngine.Random.value < critChance;
                if (isCrit)
                    damage *= ConfigRegistry.Player.BaseCritMultiplier;
            }

            // Floor to long and enforce minimum damage of 1
            long finalDamage = Math.Max(1L, (long)Mathf.Floor(damage));

            if (rawDamage <= 0f)
                Debug.LogWarning($"[DamageSystem] ResolveDamage called with zero/negative rawDamage ({rawDamage}). Minimum damage floor 1 applied.");

            var hit = new DamageHit(attacker, rawDamage, isCrit, finalDamage,
                                    damageType, targetId, hitPos);
            OnDamageResolved?.Invoke(hit);
        }

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Clears all event subscribers. Call in test TearDown to prevent cross-test
        /// subscriber bleed.
        /// </summary>
        public static void ClearSubscribersForTesting()
        {
            OnDamageResolved = null;
            OnDamageBlocked  = null;
        }

        /// <summary>
        /// Directly invokes <see cref="OnDamageResolved"/> for unit testing.
        /// Bypasses ResolveDamage logic (crit roll, i-frame guard) so tests
        /// can precisely control the hit values delivered to subscribers.
        /// </summary>
        public static void InvokeOnDamageResolvedForTesting(DamageHit hit)
            => OnDamageResolved?.Invoke(hit);
#endif
    }
}
