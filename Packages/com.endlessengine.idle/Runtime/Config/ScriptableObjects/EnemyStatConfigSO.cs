using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Base enemy stat configuration. Values are scaled per-wave by WaveScalingCalculator.
    /// All fields are read-only at runtime — Roslyn analyzer enforces no mutation.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyStatConfig", menuName = "Endless Engine/Config/Enemy Stats")]
    public class EnemyStatConfigSO : ScriptableObject
    {
        [Header("Base Stats")]
        [Tooltip("Base HP before wave scaling. Valid range: 1–100000.")]
        public float BaseMaxHP = 50f;

        [Tooltip("Base attack damage before wave scaling. Valid range: 1–10000.")]
        public float BaseAttackDamage = 5f;

        [Tooltip("Base contact damage per tick before wave scaling. Valid range: 0–10000.")]
        public float BaseContactDamage = 2f;

        [Tooltip("Base movement speed. Valid range: 0.5–20.")]
        public float MoveSpeed = 3f;

        [Tooltip("Base attack range in world units. Valid range: 0.1–50.")]
        public float AttackRange = 2f;

        [Tooltip("Seconds between attacks. Valid range: 0.1–10.")]
        public float AttackInterval = 1.5f;

        [Header("Wave Scaling")]
        [Tooltip("Exponent applied to wave number for HP/damage scaling.")]
        public float WaveScalingExponent = 1.5f;

        [Header("Hard Cap")]
        [Tooltip("Maximum simultaneous enemies on screen.")]
        public int HardCapEnemiesOnScreen = 200;
    }
}
