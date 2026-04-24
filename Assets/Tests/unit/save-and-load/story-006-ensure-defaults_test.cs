// Tests for Sprint 6 — S6-01: SaveData.EnsureDefaults() null-safety
// Type: Logic (Unit/EditMode)
//
// Verifies that SaveData.EnsureDefaults() initializes all collection and value
// fields to safe defaults after deserialization of an old/partial save file.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.SaveAndLoad

using System.Collections.Generic;
using NUnit.Framework;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Modules;

namespace EndlessEngine.Tests.Unit.SaveAndLoad
{
    /// <summary>
    /// Unit tests for SaveData.EnsureDefaults() — Sprint 6 S6-01.
    /// </summary>
    [TestFixture]
    public class EnsureDefaultsTests
    {
        // ── Collection null-safety ────────────────────────────────────────────────

        [Test]
        [Description("EnsureDefaults: UpgradeNodeStates null → initialized to empty dict.")]
        public void EnsureDefaults_NullUpgradeNodeStates_InitializesToEmptyDict()
        {
            var data = new SaveData { UpgradeNodeStates = null };
            data.EnsureDefaults();
            Assert.IsNotNull(data.UpgradeNodeStates);
            Assert.AreEqual(0, data.UpgradeNodeStates.Count);
        }

        [Test]
        [Description("EnsureDefaults: GeneratorStates null → initialized to empty dict.")]
        public void EnsureDefaults_NullGeneratorStates_InitializesToEmptyDict()
        {
            var data = new SaveData { GeneratorStates = null };
            data.EnsureDefaults();
            Assert.IsNotNull(data.GeneratorStates);
            Assert.AreEqual(0, data.GeneratorStates.Count);
        }

        [Test]
        [Description("EnsureDefaults: UnlockedRealmSlugs null → initialized to empty list.")]
        public void EnsureDefaults_NullUnlockedRealmSlugs_InitializesToEmptyList()
        {
            var data = new SaveData { UnlockedRealmSlugs = null };
            data.EnsureDefaults();
            Assert.IsNotNull(data.UnlockedRealmSlugs);
            Assert.AreEqual(0, data.UnlockedRealmSlugs.Count);
        }

        [Test]
        [Description("EnsureDefaults: ZoneStates null → initialized to empty dict.")]
        public void EnsureDefaults_NullZoneStates_InitializesToEmptyDict()
        {
            var data = new SaveData { ZoneStates = null };
            data.EnsureDefaults();
            Assert.IsNotNull(data.ZoneStates);
            Assert.AreEqual(0, data.ZoneStates.Count);
        }

        [Test]
        [Description("EnsureDefaults: PrePrestigeUpgradeNodeStates null → initialized to empty dict.")]
        public void EnsureDefaults_NullPrePrestigeUpgradeNodeStates_InitializesToEmptyDict()
        {
            var data = new SaveData { PrePrestigeUpgradeNodeStates = null };
            data.EnsureDefaults();
            Assert.IsNotNull(data.PrePrestigeUpgradeNodeStates);
        }

        // ── Value field defaults ──────────────────────────────────────────────────

        [Test]
        [Description("EnsureDefaults: empty CurrentRunState → set to 'Active'.")]
        public void EnsureDefaults_EmptyCurrentRunState_SetsActive()
        {
            var data = new SaveData { CurrentRunState = null };
            data.EnsureDefaults();
            Assert.AreEqual("Active", data.CurrentRunState);
        }

        [Test]
        [Description("EnsureDefaults: empty string CurrentRunState → set to 'Active'.")]
        public void EnsureDefaults_EmptyStringCurrentRunState_SetsActive()
        {
            var data = new SaveData { CurrentRunState = "" };
            data.EnsureDefaults();
            Assert.AreEqual("Active", data.CurrentRunState);
        }

        [Test]
        [Description("EnsureDefaults: non-empty CurrentRunState → preserved.")]
        public void EnsureDefaults_ExistingRunState_Preserved()
        {
            var data = new SaveData { CurrentRunState = "IdleRecovery" };
            data.EnsureDefaults();
            Assert.AreEqual("IdleRecovery", data.CurrentRunState);
        }

        [Test]
        [Description("EnsureDefaults: null CurrentRealmSlug → set to 'default'.")]
        public void EnsureDefaults_NullRealmSlug_SetsDefault()
        {
            var data = new SaveData { CurrentRealmSlug = null };
            data.EnsureDefaults();
            Assert.AreEqual("default", data.CurrentRealmSlug);
        }

        [Test]
        [Description("EnsureDefaults: BaseMultiplierPerPrestige=0 → set to 1.5.")]
        public void EnsureDefaults_ZeroMultiplier_SetsDefault()
        {
            var data = new SaveData { BaseMultiplierPerPrestige = 0f };
            data.EnsureDefaults();
            Assert.AreEqual(1.5f, data.BaseMultiplierPerPrestige, 0.001f);
        }

        [Test]
        [Description("EnsureDefaults: WaveNumber=0 → set to 1.")]
        public void EnsureDefaults_ZeroWaveNumber_SetsOne()
        {
            var data = new SaveData { WaveNumber = 0 };
            data.EnsureDefaults();
            Assert.AreEqual(1, data.WaveNumber);
        }

        // ── Idempotent — existing values are preserved ────────────────────────────

        [Test]
        [Description("EnsureDefaults: all fields already populated → values unchanged.")]
        public void EnsureDefaults_AllFieldsPopulated_ValuesUnchanged()
        {
            var data = new SaveData
            {
                UpgradeNodeStates            = new Dictionary<string, int> { ["node_01"] = 2 },
                GeneratorStates              = new Dictionary<string, EndlessEngine.Generator.GeneratorState>(),
                UnlockedRealmSlugs           = new System.Collections.Generic.List<string> { "fire" },
                ZoneStates                   = new Dictionary<string, ZoneRuntimeState>(),
                PrePrestigeUpgradeNodeStates = new Dictionary<string, int>(),
                CurrentRunState              = "Active",
                CurrentRealmSlug             = "fire",
                BaseMultiplierPerPrestige    = 2.0f,
                WaveNumber                   = 5,
            };

            data.EnsureDefaults();

            Assert.AreEqual(1, data.UpgradeNodeStates.Count);
            Assert.AreEqual(1, data.UnlockedRealmSlugs.Count, "UnlockedRealmSlugs must not be reset");
            Assert.AreEqual("fire", data.CurrentRealmSlug);
            Assert.AreEqual(2.0f, data.BaseMultiplierPerPrestige, 0.001f);
            Assert.AreEqual(5, data.WaveNumber);
        }
    }
}
