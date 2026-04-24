using System;
using UnityEngine;

namespace EndlessEngine.Damage
{
    /// <summary>
    /// Wave-scaled combat stats for a single enemy instance.
    /// Populated by Wave Spawning at wave start and cached — never recomputed per damage event.
    ///
    /// ADR: ADR-0005 — Damage System Event Bus Architecture
    /// ADR: ADR-0011 — Wave Spawning
    /// </summary>
    public struct EnemyRuntimeData
    {
        /// <summary>Instance ID of the enemy GameObject (used by DamageSystem for routing).</summary>
        public int EntityID;

        /// <summary>Cached HP for this wave. Computed via <see cref="WaveScalingCalculator.ComputeScaledValue"/>.</summary>
        public long ScaledHP;

        /// <summary>Cached auto-attack damage for this wave.</summary>
        public long ScaledDamage;

        /// <summary>Cached body-contact damage for this wave.</summary>
        public long ScaledContactDamage;

        /// <summary>Exponent used for scaling; stored for diagnostics/validation.</summary>
        public float ScalingExponent;
    }

    /// <summary>
    /// Stateless utility for computing wave-scaled values.
    /// Called once per wave by Wave Spawning to populate <see cref="EnemyRuntimeData"/>.
    ///
    /// Formula: <c>Max(1, Floor(baseValue × waveNumber^scalingExponent))</c>
    /// </summary>
    public static class WaveScalingCalculator
    {
        /// <summary>
        /// Computes the wave-scaled value for a single stat.
        /// Returns 1 when <paramref name="waveNumber"/> is 0 or negative (avoids zero result).
        /// </summary>
        /// <param name="baseValue">Base stat value from config.</param>
        /// <param name="waveNumber">Current wave number (1-indexed in normal play).</param>
        /// <param name="scalingExponent">Exponent for the power law curve.</param>
        public static long ComputeScaledValue(float baseValue, int waveNumber, float scalingExponent)
        {
            if (waveNumber <= 0) return 1L;
            float scaled = baseValue * Mathf.Pow(waveNumber, scalingExponent);
            return Math.Max(1L, (long)Mathf.Floor(scaled));
        }
    }
}
