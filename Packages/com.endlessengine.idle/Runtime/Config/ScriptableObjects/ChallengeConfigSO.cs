using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>Type of restriction or modifier a challenge applies.</summary>
    public enum ChallengeModifierType
    {
        /// <summary>Overrides a named stat by a fixed value.</summary>
        StatOverride,
        /// <summary>Multiplies a named stat by a factor.</summary>
        StatMultiplier,
        /// <summary>Disables a named game system (string = system ID, e.g. "generators", "upgrades").</summary>
        DisableSystem,
        /// <summary>Imposes a time limit in seconds. 0 = no limit.</summary>
        TimeLimit,
        /// <summary>Enemy HP/damage multiplier on top of wave scaling.</summary>
        EnemyDifficultyScale,
    }

    /// <summary>A single modifier applied while a challenge is active.</summary>
    [Serializable]
    public class ChallengeModifier
    {
        [Tooltip("What this modifier does.")]
        public ChallengeModifierType Type = ChallengeModifierType.StatMultiplier;

        [Tooltip("ID of the stat or system this targets (e.g. 'gold_multiplier', 'generators').")]
        public string TargetId = "";

        [Tooltip("Numeric value (multiplier, override amount, or time limit in seconds).")]
        public float Value = 1f;
    }

    /// <summary>
    /// Defines a playable challenge run with stat restrictions and a reward multiplier.
    ///
    /// Challenges are optional run modifiers — the player activates one before starting a run.
    /// Completing the challenge (reaching the required wave within constraints) applies the
    /// reward multiplier to the gold earned.
    ///
    /// Create via: Tools → Endless Engine → Create Challenge Config
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Challenge/Challenge Config",
        fileName = "ChallengeConfig")]
    public class ChallengeConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique challenge ID. Never change after release.")]
        public string ChallengeId = "";

        [Tooltip("Player-facing display name.")]
        public string DisplayName = "Challenge";

        [Tooltip("Short description of the challenge restrictions.")]
        [TextArea(2, 4)]
        public string Description = "";

        [Tooltip("Optional icon.")]
        public Sprite Icon;

        [Header("Modifiers")]
        [Tooltip("All modifiers applied during this challenge.")]
        public List<ChallengeModifier> Modifiers = new List<ChallengeModifier>();

        [Header("Victory Condition")]
        [Tooltip("Wave number the player must survive to complete the challenge. 0 = survive until time limit.")]
        [Min(0)]
        public int RequiredWave = 10;

        [Tooltip("Time limit in seconds. 0 = no time limit (use RequiredWave as the sole condition).")]
        [Min(0)]
        public float TimeLimitSeconds = 0f;

        [Header("Reward")]
        [Tooltip("Multiplier applied to gold earned when the challenge is completed successfully.")]
        [Min(1f)]
        public float RewardMultiplier = 2f;

        [Tooltip("Bonus skill points awarded on completion.")]
        [Min(0)]
        public int BonusSkillPoints = 0;
    }
}
