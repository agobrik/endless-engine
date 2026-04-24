using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Defines a pet/companion. Pets provide passive bonuses and can evolve to higher tiers.
    ///
    /// Effects use the same SkillEffect type as SkillNodeConfigSO for consistency.
    /// </summary>
    [CreateAssetMenu(menuName = "Endless Engine/Pet Config", fileName = "PetConfig")]
    public class PetConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public string PetId;
        public string DisplayName;
        [TextArea(2, 3)]
        public string Description;

        [Header("Leveling")]
        public int MaxLevel = 5;
        /// <summary>Gold cost to level up at each level index (0 = Level 1→2, etc.).</summary>
        public long[] LevelUpCosts = new long[0];

        [Header("Base Passive Effects (Level 1)")]
        public List<SkillEffect> BaseEffects = new List<SkillEffect>();

        [Header("Level Bonuses")]
        /// <summary>
        /// Additional effects unlocked at each level tier.
        /// Index 0 = bonus at Level 2, index 1 = bonus at Level 3, etc.
        /// </summary>
        public List<PetLevelBonus> LevelBonuses = new List<PetLevelBonus>();

        [Header("Evolution")]
        /// <summary>PetId of the evolved form (empty = max evolution).</summary>
        public string EvolvesToPetId;
        /// <summary>Level required to evolve (0 = no evolution).</summary>
        [Min(0)] public int EvolveAtLevel = 0;
        public long EvolutionCost;
    }

    [System.Serializable]
    public class PetLevelBonus
    {
        public string          Label;
        public List<SkillEffect> AdditionalEffects = new List<SkillEffect>();
    }
}
