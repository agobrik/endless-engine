namespace EndlessEngine.Combat
{
    /// <summary>
    /// Minimal interface for querying runtime-effective player stats.
    /// Implemented by UpgradeApplicationSystem when that system is built.
    /// AutoBattleController depends on this interface — not on the concrete UAS class.
    ///
    /// Until UAS exists, <see cref="BaseStatUpgradeProvider"/> wraps
    /// <see cref="EndlessEngine.Config.PlayerBaseStatConfigSO"/> directly.
    ///
    /// ADR: ADR-0005 — Damage Event Bus Architecture (stat provider pattern)
    /// </summary>
    public interface IUpgradeStatProvider
    {
        float GetAttackDamage();
        float GetAttackInterval();
        float GetCritChance();
        float GetCritMultiplier();
        float GetMoveSpeed();
    }
}
