using EndlessEngine.Config;

namespace EndlessEngine.Combat
{
    /// <summary>
    /// Fallback <see cref="IUpgradeStatProvider"/> that reads directly from
    /// <see cref="PlayerBaseStatConfigSO"/> with no upgrade modifiers.
    /// Used in MVP / VS scene before UpgradeApplicationSystem is implemented.
    ///
    /// Replace with UpgradeApplicationSystem when the upgrade tree is wired.
    /// </summary>
    public sealed class BaseStatUpgradeProvider : IUpgradeStatProvider
    {
        private readonly PlayerBaseStatConfigSO _config;

        public BaseStatUpgradeProvider(PlayerBaseStatConfigSO config)
        {
            _config = config;
        }

        public float GetAttackDamage()      => _config.BaseAttackDamage;
        public float GetAttackInterval()    => _config.BaseAttackInterval;
        public float GetCritChance()        => _config.BaseCritChance;
        public float GetCritMultiplier()    => _config.BaseCritMultiplier;
        public float GetMoveSpeed()         => _config.BaseMoveSpeed;
    }
}
