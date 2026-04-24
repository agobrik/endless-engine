using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Module: Zone / Region System
    /// Defines a named world-space zone that produces gold while the cursor is inside it,
    /// or while the zone is "activated" by the player.
    ///
    /// Zones are independent of Wave/Combat. They work for:
    ///   - Cursor hover zones (mouse enters → yield starts)
    ///   - Region idle games (unlock zones to add passive income streams)
    ///   - Map-based idle (biomes, islands, districts)
    ///
    /// A ZoneDatabaseSO holds the full ordered list of zones (analogous to GeneratorDatabaseSO).
    ///
    /// Wire up: Bootstrap creates ZoneSystem and calls Initialize().
    /// </summary>
    [CreateAssetMenu(fileName = "ZoneConfig_",
                     menuName = "Endless Engine/Modules/Zone Config")]
    public class ZoneConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique key. Never change after first save.")]
        public string ZoneId = "zone_01";

        [Tooltip("Display name shown in UI.")]
        public string DisplayName = "New Zone";

        [TextArea(1, 3)]
        public string Description = "";

        [Header("World Position")]
        [Tooltip("Center of this zone in world space.")]
        public Vector2 WorldCenter = Vector2.zero;

        [Tooltip("Zone shape.")]
        public ZoneShape Shape = ZoneShape.Circle;

        [Tooltip("For Circle: radius. For Rect: half-width and half-height.")]
        public Vector2 Size = new Vector2(2f, 2f);

        [Header("Yield")]
        [Tooltip("Gold per second while zone is active (cursor inside or auto-active).")]
        [Min(0f)]
        public float YieldPerSecond = 5f;

        [Tooltip("Multiplier applied to yield when cursor is actively hovering inside this zone. " +
                 "1.0 = no bonus. 2.0 = double yield while hovering.")]
        [Min(1f)]
        public float ActiveHoverMultiplier = 2f;

        [Tooltip("If true, zone produces yield even without cursor inside (passive mode). " +
                 "If false, zone only yields when cursor is hovering.")]
        public bool PassiveMode = true;

        [Header("Unlock")]
        [Tooltip("Purchase cost to unlock this zone. 0 = available from start.")]
        [Min(0)]
        public long UnlockCost = 0;

        [Tooltip("Zone ID that must be unlocked before this one. Empty = no prerequisite.")]
        public string PrerequisiteZoneId = "";

        [Tooltip("Prestige count required to unlock this zone. 0 = always available.")]
        [Min(0)]
        public int PrestigeRequirement = 0;

        [Header("Upgrade Scaling")]
        [Tooltip("Base cost multiplier applied per upgrade level. 1.5 = 50% more per level.")]
        [Range(1f, 3f)]
        public float UpgradeCostScalingFactor = 1.5f;

        [Tooltip("Yield multiplier added per upgrade level. E.g. 0.2 = +20% yield per level.")]
        [Range(0f, 2f)]
        public float YieldMultiplierPerUpgrade = 0.25f;

        [Tooltip("Maximum upgrade levels for this zone. 0 = unlimited.")]
        [Min(0)]
        public int MaxUpgradeLevel = 10;

        [Header("Visual")]
        [Tooltip("Color shown for this zone in editor overlays and in-game maps.")]
        public Color ZoneColor = new Color(0.3f, 0.7f, 1.0f, 0.4f);
    }

    public enum ZoneShape
    {
        Circle,
        Rectangle,
    }
}
