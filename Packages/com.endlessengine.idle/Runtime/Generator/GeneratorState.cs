using System;

namespace EndlessEngine.Generator
{
    /// <summary>
    /// Runtime state for a single generator type.
    /// Plain C# class — serialized to save data by GeneratorSystem.
    /// </summary>
    [Serializable]
    public class GeneratorState
    {
        /// <summary>Stable generator ID matching GeneratorConfigSO.GeneratorId.</summary>
        public string GeneratorId;

        /// <summary>How many copies the player currently owns.</summary>
        public int Count;

        /// <summary>Effective yield multiplier from upgrades (1.0 = unmodified).</summary>
        public float UpgradeMultiplier = 1f;

        /// <summary>Total yield per second after multipliers.</summary>
        public float EffectiveYieldPerSecond(float baseYield) =>
            Count * baseYield * UpgradeMultiplier;
    }
}
