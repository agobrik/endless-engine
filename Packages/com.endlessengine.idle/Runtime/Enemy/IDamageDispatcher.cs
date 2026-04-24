using UnityEngine;

namespace EndlessEngine.Enemy
{
    /// <summary>
    /// Narrow interface EnemyManager uses to dispatch enemy attacks to DamageSystem.
    /// Decouples the enemy update loop from the concrete DamageSystem MonoBehaviour.
    ///
    /// ADR: ADR-0005 — Damage Event Bus (AutoBattleController / enemy attacks → DamageSystem)
    /// ADR: ADR-0006 — Enemy Update Loop
    /// </summary>
    public interface IDamageDispatcher
    {
        /// <summary>Dispatch an enemy auto-attack hit to the damage resolution pipeline.</summary>
        void DispatchEnemyAttack(EnemyAgent agent, Vector2 playerPosition);
    }
}
