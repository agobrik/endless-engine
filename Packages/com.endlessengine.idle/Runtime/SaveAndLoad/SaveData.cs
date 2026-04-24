using System;
using System.Collections.Generic;
using EndlessEngine.Generator;
using EndlessEngine.Modules;
using EndlessEngine.Building;

namespace EndlessEngine.SaveAndLoad
{
    /// <summary>
    /// The persisted state schema for Endless Engine.
    /// All gameplay state that must survive application quit lives here.
    /// Schema migrations are handled by the IMigration chain — see ADR-0002.
    ///
    /// DO NOT add enum fields — serialize as string for forward-compat (Newtonsoft.Json).
    /// DO NOT remove fields in a patch — mark as [Obsolete] and add a migration first.
    ///
    /// ADR: ADR-0002 — Save Serialization Format and Atomic Write Pattern
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Schema version at time of write. Drives IMigration chain on load.</summary>
        public int SchemaVersion;

        /// <summary>UTC timestamp of last session end. Used for offline time calculation.</summary>
        public DateTime LastSessionTimestamp;

        /// <summary>Duration of last play session in seconds. For analytics.</summary>
        public float SessionDurationSeconds;

        // ── Economy ──────────────────────────────────────────────────────────────

        /// <summary>Player's current resource count (Gold/Bits).</summary>
        public long CurrentResources;

        // ── Upgrade Tree ─────────────────────────────────────────────────────────

        /// <summary>Maps upgrade node ID → current purchased rank (0 = not purchased).</summary>
        public Dictionary<string, int> UpgradeNodeStates;

        // ── Generators ───────────────────────────────────────────────────────────

        /// <summary>Maps generator ID → runtime state (count, multiplier).</summary>
        public Dictionary<string, GeneratorState> GeneratorStates;

        // ── Prestige ─────────────────────────────────────────────────────────────

        /// <summary>Total number of completed prestiges.</summary>
        public int PrestigeCount;

        /// <summary>
        /// Per-player earned multiplier per prestige. Stored in save (not read from config)
        /// so config changes don't retroactively alter earned multipliers. (OQ-SAV-04)
        /// </summary>
        public float BaseMultiplierPerPrestige;

        // ── Wave & Combat ────────────────────────────────────────────────────────

        /// <summary>Current wave number within the run.</summary>
        public int WaveNumber;

        /// <summary>
        /// Current run state. Serialized as string for forward-compat.
        /// Valid values: "Active", "IdleRecovery".
        /// </summary>
        public string CurrentRunState;

        // ── Realm ────────────────────────────────────────────────────────────────

        /// <summary>Slug of the currently active realm.</summary>
        public string CurrentRealmSlug;

        /// <summary>List of realm slugs the player has unlocked.</summary>
        public List<string> UnlockedRealmSlugs;

        // ── Skill Tree ───────────────────────────────────────────────────────────

        /// <summary>
        /// Unlocked skill node IDs. Key = treeId:nodeId composite key.
        /// Null in saves created before skill tree was added.
        /// </summary>
        public System.Collections.Generic.HashSet<string> UnlockedSkillNodes;

        /// <summary>
        /// Available skill points to spend.
        /// </summary>
        public int SkillPoints;

        // ── Inventory ────────────────────────────────────────────────────────────

        /// <summary>
        /// Serialized inventory slots. Each entry is itemId→count.
        /// Null in saves created before inventory was added.
        /// </summary>
        public Dictionary<string, int> InventoryItems;

        // ── Ascension (Multi-Layer Prestige) ─────────────────────────────────────

        /// <summary>
        /// Per-layer trigger counts for the ascension system.
        /// Key = layer index (int as string, e.g. "0", "1", "2").
        /// Null in saves created before ascension was added (schema migration safe).
        /// Layer 0 maps to PrestigeCount for backward compatibility.
        /// </summary>
        public Dictionary<string, int> AscensionCounts;

        // ── Research ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Completed research node IDs (composite key "treeId:nodeId").
        /// Null in saves created before research was added.
        /// </summary>
        public System.Collections.Generic.HashSet<string> CompletedResearchNodes;

        /// <summary>
        /// Research queue — ordered list of "treeId:nodeId" composite keys awaiting completion.
        /// Active research is queue[0] with its tick progress stored in ResearchActiveTicks.
        /// </summary>
        public System.Collections.Generic.List<string> ResearchQueue;

        /// <summary>Ticks already spent on the head-of-queue research node.</summary>
        public int ResearchActiveTicks;

        // ── Statistics ───────────────────────────────────────────────────────────

        /// <summary>
        /// Lifetime statistics. Key = StatDefinitionSO.StatId, Value = accumulated or peak value.
        /// Null in saves created before statistics were added (schema migration safe).
        /// </summary>
        public Dictionary<string, double> StatisticsValues;

        // ── Milestones ───────────────────────────────────────────────────────────

        /// <summary>
        /// Set of milestone IDs that have been completed.
        /// Null in saves created before milestones were added (schema migration safe).
        /// </summary>
        public System.Collections.Generic.HashSet<string> CompletedMilestones;

        // ── Multi-Currency ───────────────────────────────────────────────────────

        /// <summary>
        /// Balances for secondary currencies managed by CurrencyService.
        /// Key = CurrencyConfigSO.CurrencyId, Value = balance (double for BigNumber support).
        /// Null in saves created before multi-currency was added (schema migration safe).
        /// </summary>
        public Dictionary<string, double> CurrencyBalances;

        // ── Building System ──────────────────────────────────────────────────────

        /// <summary>
        /// Placed building instances. Key = instance GUID, Value = serialized state.
        /// Null in saves created before buildings were added.
        /// </summary>
        public Dictionary<string, BuildingSaveEntry> PlacedBuildings;

        // ── Pet / Companion System ────────────────────────────────────────────────

        /// <summary>
        /// Equipped pet id. Empty string = no pet equipped.
        /// </summary>
        public string EquippedPetId;

        /// <summary>
        /// Per-pet level. Key = PetConfigSO.PetId, Value = current level (0-based).
        /// Null in saves created before pets were added.
        /// </summary>
        public Dictionary<string, int> PetLevels;

        // ── Unlock / Discovery Log ────────────────────────────────────────────────

        /// <summary>
        /// All unlocked entry ids (buildings, pets, items, milestones, systems).
        /// Null in saves created before unlock log was added.
        /// </summary>
        public System.Collections.Generic.HashSet<string> UnlockLogEntries;

        // ── Modules ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Zone unlock and upgrade state. Populated by ZoneSystem if the module is active.
        /// Null if the game does not use the Zone module.
        /// </summary>
        public Dictionary<string, ZoneRuntimeState> ZoneStates;

        /// <summary>
        /// Click module state: total clicks and auto-click rate override.
        /// Null if the game does not use the Click module.
        /// </summary>
        public ClickModuleSaveState ClickState;

        // ── Prestige Crash-Safety Snapshot ───────────────────────────────────────
        // Written in the first prestige save (before reset). Rolled back if second
        // save never completes. ADR-0002 §5 / ADR-0010.

        /// <summary>True if a prestige sequence started but has not completed its second save.</summary>
        public bool PrestigeInProgress;

        /// <summary>Resource count before prestige reset (snapshot).</summary>
        public long PrePrestigeResources;

        /// <summary>Upgrade node states before prestige reset (snapshot).</summary>
        public Dictionary<string, int> PrePrestigeUpgradeNodeStates;

        /// <summary>Wave number before prestige reset (snapshot).</summary>
        public int PrePrestigeWaveNumber;

        // ── Schema Migration Safety ───────────────────────────────────────────────

        /// <summary>
        /// Ensures all collection fields are non-null after deserialization.
        /// Old save files may omit fields introduced in later schema versions —
        /// Newtonsoft.Json leaves them null rather than default-constructing them.
        /// Called by SaveService.ApplyLoadGuards() before any provider reads the data.
        ///
        /// ADR: ADR-0002 §4 — Load Guards
        /// </summary>
        public void EnsureDefaults()
        {
            UpgradeNodeStates              ??= new Dictionary<string, int>();
            GeneratorStates                ??= new Dictionary<string, GeneratorState>();
            UnlockedRealmSlugs             ??= new List<string>();
            ZoneStates                     ??= new Dictionary<string, ZoneRuntimeState>();
            PrePrestigeUpgradeNodeStates   ??= new Dictionary<string, int>();
            CurrencyBalances               ??= new Dictionary<string, double>();
            CompletedMilestones            ??= new System.Collections.Generic.HashSet<string>();
            AscensionCounts                ??= new Dictionary<string, int>();
            InventoryItems                 ??= new Dictionary<string, int>();
            UnlockedSkillNodes             ??= new System.Collections.Generic.HashSet<string>();
            StatisticsValues               ??= new Dictionary<string, double>();
            CompletedResearchNodes         ??= new System.Collections.Generic.HashSet<string>();
            ResearchQueue                  ??= new System.Collections.Generic.List<string>();
            PlacedBuildings                ??= new Dictionary<string, BuildingSaveEntry>();
            PetLevels                      ??= new Dictionary<string, int>();
            UnlockLogEntries               ??= new System.Collections.Generic.HashSet<string>();
            EquippedPetId                  ??= string.Empty;

            if (string.IsNullOrEmpty(CurrentRunState))
                CurrentRunState = "Active";

            if (string.IsNullOrEmpty(CurrentRealmSlug))
                CurrentRealmSlug = "default";

            if (BaseMultiplierPerPrestige <= 0f)
                BaseMultiplierPerPrestige = 1.5f;

            if (WaveNumber <= 0)
                WaveNumber = 1;
        }
    }
}
