using UnityEngine;

namespace EndlessEngine.Damage
{
    /// <summary>
    /// Immutable value type representing a single resolved damage event.
    /// Zero heap allocation — passed by value through the event bus.
    ///
    /// ADR: ADR-0005 — Damage System Event Bus Architecture
    /// </summary>
    public readonly struct DamageHit
    {
        /// <summary>Whether the attacker was the player or an enemy.</summary>
        public readonly AttackerType AttackerType;

        /// <summary>Raw damage before crit or floor application.</summary>
        public readonly float BaseDamage;

        /// <summary>True when the player's crit roll succeeded. Always false for enemies.</summary>
        public readonly bool IsCrit;

        /// <summary>
        /// Final damage applied to the target after crit multiplier and minimum floor.
        /// Minimum value is 1.
        /// </summary>
        public readonly long FinalDamage;

        /// <summary>Whether this was an auto-attack hit or a contact-damage hit.</summary>
        public readonly DamageType DamageType;

        /// <summary>Instance ID of the target that received (or blocked) this hit.</summary>
        public readonly int TargetID;

        /// <summary>World-space position where the hit occurred (for VFX placement).</summary>
        public readonly Vector2 HitPosition;

        public DamageHit(
            AttackerType attacker,
            float        baseDamage,
            bool         isCrit,
            long         finalDamage,
            DamageType   damageType,
            int          targetId,
            Vector2      hitPos)
        {
            AttackerType = attacker;
            BaseDamage   = baseDamage;
            IsCrit       = isCrit;
            FinalDamage  = finalDamage;
            DamageType   = damageType;
            TargetID     = targetId;
            HitPosition  = hitPos;
        }
    }

    /// <summary>Identifies the source side of a damage event.</summary>
    public enum AttackerType
    {
        Player,
        Enemy,
    }

    /// <summary>Identifies how contact was made.</summary>
    public enum DamageType
    {
        /// <summary>Standard auto-attack hit.</summary>
        Attack,

        /// <summary>Body-contact damage (enemy walking into player).</summary>
        Contact,
    }
}
