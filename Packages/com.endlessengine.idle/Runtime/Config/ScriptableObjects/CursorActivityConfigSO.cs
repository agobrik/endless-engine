using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Module: Cursor Activity
    /// Configures how mouse/pointer movement translates into gold yield.
    ///
    /// Supports three yield models — select one via <see cref="YieldModel"/>:
    ///   Speed     — faster movement = higher multiplier (Tiny Biomes style)
    ///   Hover     — staying in a region earns over time (point-and-dwell style)
    ///   Distance  — total pixels traveled per second drives income
    ///
    /// The module is additive: its yield stacks with Generator passive income.
    /// Disable the component to disable the module entirely.
    ///
    /// Wire up: Bootstrap creates CursorYieldService and calls Initialize().
    /// </summary>
    [CreateAssetMenu(fileName = "CursorActivityConfig",
                     menuName = "Endless Engine/Modules/Cursor Activity Config")]
    public class CursorActivityConfigSO : ScriptableObject
    {
        [Header("Yield Model")]
        [Tooltip("How cursor movement maps to income.")]
        public CursorYieldModel YieldModel = CursorYieldModel.Speed;

        [Header("Base Yield")]
        [Tooltip("Gold per second at maximum activity (speed cap or full hover).")]
        [Min(0.01f)]
        public float MaxYieldPerSecond = 50f;

        [Tooltip("Gold per second when cursor is completely still. 0 = no income while idle.")]
        [Min(0f)]
        public float IdleYieldPerSecond = 0f;

        [Header("Speed Model — only used when YieldModel = Speed")]
        [Tooltip("Mouse speed (pixels/second) at which full MaxYieldPerSecond is reached.")]
        [Min(1f)]
        public float FullSpeedThreshold = 400f;

        [Tooltip("Mouse speed (pixels/second) below which income drops to IdleYieldPerSecond. " +
                 "Movement slower than this is treated as idle.")]
        [Min(0f)]
        public float IdleSpeedThreshold = 20f;

        [Tooltip("How quickly the yield multiplier responds to speed changes. " +
                 "Higher = snappier. Lower = smoother. 0 = instant.")]
        [Range(0f, 20f)]
        public float SmoothingSpeed = 5f;

        [Header("Distance Model — only used when YieldModel = Distance")]
        [Tooltip("Pixels of cumulative mouse travel that equals 1 gold. " +
                 "Lower = more rewarding per pixel. E.g. 10 = 1 gold per 10px.")]
        [Min(0.01f)]
        public float PixelsPerGold = 10f;

        [Header("Hover Model — only used when YieldModel = Hover")]
        [Tooltip("Seconds the cursor must remain within HoverRadius of a point before yielding starts.")]
        [Min(0f)]
        public float HoverWarmupSeconds = 0.5f;

        [Tooltip("World-space radius within which cursor movement still counts as 'hovering'.")]
        [Min(0.01f)]
        public float HoverRadius = 0.5f;

        [Header("Multiplier Caps")]
        [Tooltip("Global multiplier applied to all cursor yield before adding to economy. " +
                 "Stack with prestige multipliers in Bootstrap.")]
        [Min(0f)]
        public float GlobalMultiplier = 1f;

        [Tooltip("If true, cursor yield is reduced by the same RunConfig.ActiveRunPassiveModifier " +
                 "that applies to generator income during an active run.")]
        public bool ApplyRunModifier = true;
    }

    public enum CursorYieldModel
    {
        /// <summary>Yield scales with pointer movement speed.</summary>
        Speed,
        /// <summary>Yield accumulates while pointer stays in one area.</summary>
        Hover,
        /// <summary>Yield accumulates from total distance traveled.</summary>
        Distance,
    }
}
