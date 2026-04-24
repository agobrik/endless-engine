using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Player base stat configuration. These are the starting values before any
    /// upgrade or prestige multipliers are applied.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerBaseStatConfig", menuName = "Endless Engine/Config/Player Base Stats")]
    public class PlayerBaseStatConfigSO : ScriptableObject
    {
        [Header("Combat")]
        [Tooltip("Base max HP at prestige 0 before upgrades. Valid range: 1–1000000.")]
        public float BaseMaxHP = 200f;

        [Tooltip("Base attack damage per hit. Valid range: 1–100000.")]
        public float BaseAttackDamage = 10f;

        [Tooltip("Seconds between auto-attacks. Valid range: 0.05–10.")]
        public float BaseAttackInterval = 1f;

        [Tooltip("Auto-attack range in world units.")]
        public float BaseAttackRange = 5f;

        [Tooltip("Seconds between nearest-enemy target recomputations. 0.1 = 10 times/second. Avoids per-frame sort of 200 enemies.")]
        [Range(0.05f, 1f)]
        public float AttackTargetUpdateInterval = 0.1f;

        [Tooltip("Crit chance [0, 1]. 0 = never crit, 1 = always crit.")]
        public float BaseCritChance = 0.1f;

        [Tooltip("Damage multiplier on crit. Valid range: 1–10.")]
        public float BaseCritMultiplier = 2f;

        [Header("Movement")]
        [Tooltip("Movement speed in world units per second. Valid range: 0.5–20.")]
        public float BaseMoveSpeed = 5f;

        [Header("I-Frames")]
        [Tooltip("Duration of invincibility frames after taking a hit, in seconds.")]
        public float InvincibilityFramesDuration = 1f;

        [Tooltip("Seconds after death before entering Idle Recovery state.")]
        public float DeathTransitionDelaySeconds = 2f;
    }
}
