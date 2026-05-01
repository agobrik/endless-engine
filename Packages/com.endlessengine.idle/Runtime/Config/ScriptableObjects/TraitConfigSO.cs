using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// A single trait the player can choose at prestige.
    /// Traits are permanent — they survive resets and stack across prestiges.
    ///
    /// Create via: Tools → Endless Engine → Create Trait Config
    /// </summary>
    [CreateAssetMenu(menuName = "Endless Engine/Trait/Trait Config", fileName = "TraitConfig")]
    public class TraitConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique trait ID. Never change after release.")]
        public string TraitId = "";

        [Tooltip("Player-facing display name.")]
        public string DisplayName = "Trait";

        [TextArea(2, 3)]
        public string Description = "";

        public Sprite Icon;

        [Header("Tier")]
        [Tooltip("Tier 0 = available from prestige 1. Higher tiers unlock at higher prestige counts.")]
        [Min(0)]
        public int Tier = 0;

        [Tooltip("Prestige count required to unlock this tier.")]
        [Min(1)]
        public int PrestigeRequired = 1;

        [Header("Effects")]
        [Tooltip("Effects applied permanently when this trait is chosen.")]
        public List<SkillEffect> Effects = new List<SkillEffect>();

        [Header("Exclusivity")]
        [Tooltip("IDs of traits that cannot be chosen if this trait is active (mutually exclusive group).")]
        public List<string> ExclusiveWith = new List<string>();
    }
}
