using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>Type of minigame triggered by an active skill.</summary>
    public enum MinigameType
    {
        /// <summary>Rapid tap/click for a short duration to maximize reward.</summary>
        TapFrenzy,
        /// <summary>Time a single button press to hit a moving target for a bonus.</summary>
        TimedPress,
        /// <summary>Pattern matching — press buttons in sequence.</summary>
        Sequence,
    }

    /// <summary>
    /// Configuration for an active skill that triggers a minigame.
    ///
    /// Active skills are player-triggered abilities with a cooldown.
    /// Triggering starts a minigame; completing it awards the configured reward.
    ///
    /// Create via: Tools → Endless Engine → Create Active Skill Config
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Minigame/Active Skill Config",
        fileName = "ActiveSkillConfig")]
    public class ActiveSkillConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public string SkillId      = "";
        public string DisplayName  = "Active Skill";
        public Sprite Icon;

        [Header("Cooldown")]
        [Tooltip("Cooldown in seconds before this skill can be triggered again.")]
        [Min(1f)]
        public float CooldownSeconds = 30f;

        [Header("Minigame")]
        [Tooltip("Which minigame is triggered when this skill is activated.")]
        public MinigameType MinigameType = MinigameType.TapFrenzy;

        [Tooltip("Duration of the minigame in seconds.")]
        [Min(1f)]
        public float MinigameDurationSeconds = 10f;

        [Header("Reward")]
        [Tooltip("Base gold reward for completing the minigame.")]
        [Min(0)]
        public long BaseGoldReward = 1000;

        [Tooltip("Multiplier applied per successful action (tap, hit, etc.) above baseline.")]
        [Min(1f)]
        public float PerActionBonus = 0.1f;

        [Tooltip("Maximum total reward multiplier (caps the per-action bonus).")]
        [Min(1f)]
        public float MaxRewardMultiplier = 5f;
    }
}
