using UnityEngine;
using EndlessEngine.Enemy;

namespace EndlessEngine.Damage
{
    /// <summary>
    /// MonoBehaviour adapter that bridges the static DamageSystem to the IDamageDispatcher
    /// interface used by EnemyManager.
    ///
    /// Attach to a GameObject in the VS scene and assign to EnemyManager.Initialize().
    /// This is the concrete implementation — EnemyManager depends only on IDamageDispatcher.
    ///
    /// ADR: ADR-0005 — Damage Event Bus (AutoBattleController/EnemyManager is sole caller of ResolveDamage)
    /// </summary>
    public class DamageDispatchAdapter : MonoBehaviour, IDamageDispatcher
    {
        /// <inheritdoc />
        [SerializeField] private Health.PlayerHealthComponent _playerHealth;

        /// <inheritdoc />
        public void DispatchEnemyAttack(EnemyAgent agent, Vector2 playerPosition)
        {
            bool isPlayerInvincible = _playerHealth != null && _playerHealth.IsInvincible;
            DamageSystem.ResolveDamage(
                rawDamage:          agent.RuntimeData.ScaledDamage,
                attacker:           AttackerType.Enemy,
                damageType:         DamageType.Attack,
                targetId:           _playerHealth != null ? _playerHealth.gameObject.GetInstanceID() : 0,
                hitPos:             playerPosition,
                isPlayerInvincible: isPlayerInvincible
            );
        }
    }
}
