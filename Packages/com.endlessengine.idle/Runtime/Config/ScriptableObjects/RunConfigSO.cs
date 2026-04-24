using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Configuration for a single timed run session.
    /// Controls run duration and active-mode yield modifiers.
    /// </summary>
    [CreateAssetMenu(fileName = "RunConfig", menuName = "Endless Engine/Config/Run Config")]
    public class RunConfigSO : ScriptableObject
    {
        [Header("Run Timer")]
        [Tooltip("How long a single run lasts in seconds.")]
        public float RunDurationSeconds = 120f;

        [Tooltip("Seconds shown on post-run summary screen before auto-returning to menu. 0 = wait for player input.")]
        public float PostRunSummaryAutoReturnSeconds = 0f;

        [Header("Yield Modifiers")]
        [Tooltip("Multiplier applied to passive income while a run is active. " +
                 "Active run already earns from enemies, so passive is reduced. E.g. 0.5 = half passive during run.")]
        [Range(0f, 2f)]
        public float ActiveRunPassiveModifier = 0.5f;

        [Tooltip("Multiplier applied to all gold earned from enemies during a run. E.g. 3.0 = 3x gold from kills.")]
        [Range(1f, 10f)]
        public float ActiveRunEnemyGoldMultiplier = 3f;
    }
}
