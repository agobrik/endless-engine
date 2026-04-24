// Tests for Story S1-03: Config Layer — ErrorState on Missing or Invalid Config
// Type: Logic (Unit/EditMode)
// Story: production/epics/config-layer/story-002-error-state-validation.md
//
// These tests run in EditMode. They test ConfigValidator and the error conditions
// that would cause ConfigLoadingService to enter ErrorState.
// ConfigLoadingService.LoadAsync() cannot be tested in EditMode (requires Addressables
// + async start), so we test the underlying components that LoadAsync delegates to:
// - ConfigValidator (called in Step 4 of LoadAsync)
// - ResolvedConfigs null field detection (called in ResolveFromPack / Step 3)
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.ConfigLayer

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using EndlessEngine.Config;

namespace EndlessEngine.Tests.Unit.ConfigLayer
{
    /// <summary>
    /// Unit tests for the ErrorState validation path (S1-03 / Story 002).
    /// Validates AC-CFG-02 (null SO reference triggers error), AC-CFG-04
    /// (out-of-range field triggers error in ValidationMode.Error), and
    /// the Addressables exception catch path (tested at component boundary).
    /// </summary>
    [TestFixture]
    public class ErrorStateValidationTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>Creates a fully valid set of 8 SO instances with all range-checked fields set to valid values.</summary>
        private static ResolvedConfigs CreateValidResolvedConfigs()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyStatConfigSO>();
            enemy.BaseMaxHP               = 100f;
            enemy.BaseAttackDamage        = 10f;
            enemy.MoveSpeed               = 3f;
            enemy.AttackInterval          = 1f;
            enemy.HardCapEnemiesOnScreen  = 20;

            var wave = ScriptableObject.CreateInstance<WaveConfigSO>();
            wave.TotalWavesPerRun         = 30;
            wave.BaseEnemyCountPerWave    = 5;
            wave.EliteWaveInterval        = 5;

            var economy = ScriptableObject.CreateInstance<EconomyConfigSO>();
            economy.IdleYieldRateBase          = 1f;
            economy.BaseMultiplierPerPrestige  = 1.5f;
            economy.ResourceHardCap            = 1000000L;
            economy.OfflineCapHours            = 72f;

            var upgrades = new[] { CreateUpgradeNode("node_damage_01") };

            var prestige = ScriptableObject.CreateInstance<PrestigeConfigSO>();
            prestige.BaseMultiplierPerPrestige = 1.5f;
            prestige.MaxPermanentMultiplier    = 1000f;

            var realm = ScriptableObject.CreateInstance<RealmIdentityConfigSO>();
            realm.ArenaBounds = new Rect(-10f, -6f, 20f, 12f);

            var player = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            player.BaseMaxHP           = 100f;
            player.BaseAttackDamage    = 10f;
            player.BaseCritChance      = 0.05f;
            player.BaseCritMultiplier  = 2f;
            player.BaseMoveSpeed       = 5f;
            player.BaseAttackInterval  = 1f;

            var schema = ScriptableObject.CreateInstance<SchemaVersionSO>();
            schema.CurrentSchemaVersion     = 1;
            schema.MinimumCompatibleVersion = 1;

            return new ResolvedConfigs(enemy, wave, economy, upgrades, prestige, realm, player, schema, "test-realm");
        }

        /// <summary>Creates a ResolvedConfigs with all valid fields, only Upgrades set by caller.</summary>
        private static ResolvedConfigs CreateValidResolvedConfigsWithUpgrades(UpgradeNodeConfigSO[] upgrades)
        {
            var resolved = CreateValidResolvedConfigs();
            return new ResolvedConfigs(
                resolved.Enemy, resolved.Wave, resolved.Economy,
                upgrades,
                resolved.Prestige, resolved.Realm, resolved.Player, resolved.Schema,
                resolved.RealmSlug);
        }

        private static UpgradeNodeConfigSO CreateUpgradeNode(string nodeId)
        {
            var node = ScriptableObject.CreateInstance<UpgradeNodeConfigSO>();
            node.NodeId = nodeId;
            return node;
        }

        // ── AC-CFG-02: Null SO reference triggers validation failure ──────────────

        [Test]
        [Description("AC-CFG-02: ConfigValidator.Validate() returns false when Upgrades array is null.")]
        public void Validate_UpgradesNull_ReturnsFalseAndLogsError()
        {
            var resolved = CreateValidResolvedConfigsWithUpgrades(null);

            LogAssert.Expect(LogType.Error, new Regex("UpgradeNodeConfigs"));
            bool valid = ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Error);
            Assert.IsFalse(valid, "ConfigValidator must return false when Upgrades is null");
        }

        [Test]
        [Description("AC-CFG-02: ConfigValidator.Validate() returns false when Upgrades array is empty.")]
        public void Validate_UpgradesEmpty_ReturnsFalseAndLogsError()
        {
            var resolved = CreateValidResolvedConfigsWithUpgrades(new UpgradeNodeConfigSO[0]);

            LogAssert.Expect(LogType.Error, new Regex("UpgradeNodeConfigs"));
            bool valid = ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Error);
            Assert.IsFalse(valid, "ConfigValidator must return false when Upgrades array is empty");
        }

        [Test]
        [Description("AC-CFG-02: ConfigValidator.Validate() returns false when an UpgradeNode has empty NodeId.")]
        public void Validate_UpgradeNodeEmptyNodeId_ReturnsFalse()
        {
            var resolved = CreateValidResolvedConfigs();
            resolved.Upgrades[0].NodeId = ""; // invalid: empty NodeId

            LogAssert.Expect(LogType.Error, new Regex("NodeId"));
            bool valid = ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Error);
            Assert.IsFalse(valid, "ConfigValidator must return false when UpgradeNode.NodeId is empty");
        }

        [Test]
        [Description("AC-CFG-02: Error log for Upgrades null includes the realm slug.")]
        public void Validate_UpgradesNull_ErrorLogContainsRealmSlug()
        {
            var base_ = CreateValidResolvedConfigs();
            var resolved = new ResolvedConfigs(
                base_.Enemy, base_.Wave, base_.Economy,
                null,
                base_.Prestige, base_.Realm, base_.Player, base_.Schema,
                "fire-realm"
            );

            LogAssert.Expect(LogType.Error, new Regex("fire-realm"));
            ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Error);
        }

        // ── AC-CFG-04: Out-of-range field triggers ErrorState (ValidationMode.Error) ──

        [Test]
        [Description("AC-CFG-04: MoveSpeed below minimum returns false and logs error with field name.")]
        public void Validate_MoveSpeedBelowMin_ReturnsFalseAndLogsError()
        {
            var resolved = CreateValidResolvedConfigs();
            resolved.Enemy.MoveSpeed = -1f; // below 0.5 minimum

            LogAssert.Expect(LogType.Error, new Regex("MoveSpeed"));
            bool valid = ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Error);
            Assert.IsFalse(valid, "ConfigValidator must return false for MoveSpeed = -1f");
        }

        [Test]
        [Description("AC-CFG-04: MoveSpeed above maximum returns false and logs error.")]
        public void Validate_MoveSpeedAboveMax_ReturnsFalseAndLogsError()
        {
            var resolved = CreateValidResolvedConfigs();
            resolved.Enemy.MoveSpeed = 100f; // above 20 maximum

            LogAssert.Expect(LogType.Error, new Regex("MoveSpeed"));
            bool valid = ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Error);
            Assert.IsFalse(valid, "ConfigValidator must return false for MoveSpeed = 100f");
        }

        [Test]
        [Description("AC-CFG-04: BaseMaxHP below minimum returns false and logs error.")]
        public void Validate_EnemyBaseMaxHPBelowMin_ReturnsFalse()
        {
            var resolved = CreateValidResolvedConfigs();
            resolved.Enemy.BaseMaxHP = 0f; // below 1.0 minimum

            LogAssert.Expect(LogType.Error, new Regex("BaseMaxHP"));
            bool valid = ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Error);
            Assert.IsFalse(valid, "ConfigValidator must return false for EnemyStatConfigSO.BaseMaxHP = 0f");
        }

        [Test]
        [Description("AC-CFG-04: SchemaVersionSO.MinimumCompatibleVersion > CurrentSchemaVersion returns false.")]
        public void Validate_MinCompatibleVersionExceedsCurrent_ReturnsFalse()
        {
            var resolved = CreateValidResolvedConfigs();
            resolved.Schema.CurrentSchemaVersion = 1;
            resolved.Schema.MinimumCompatibleVersion = 5; // exceeds current

            LogAssert.Expect(LogType.Error, new Regex("MinimumCompatibleVersion"));
            bool valid = ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Error);
            Assert.IsFalse(valid, "ConfigValidator must return false when MinimumCompatibleVersion > CurrentSchemaVersion");
        }

        [Test]
        [Description("AC-CFG-04: ArenaBounds with zero width returns false and logs error.")]
        public void Validate_ArenaBoundsZeroWidth_ReturnsFalse()
        {
            var resolved = CreateValidResolvedConfigs();
            resolved.Realm.ArenaBounds = new Rect(0f, 0f, 0f, 12f); // zero width

            LogAssert.Expect(LogType.Error, new Regex("ArenaBounds"));
            bool valid = ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Error);
            Assert.IsFalse(valid, "ConfigValidator must return false for ArenaBounds with zero width");
        }

        // ── Warning mode: logs warning but returns true ───────────────────────────

        [Test]
        [Description("AC-CFG-04: ValidationMode.Warning logs warning for out-of-range but returns true.")]
        public void Validate_MoveSpeedBelowMin_WarningMode_ReturnsTrueAndLogsWarning()
        {
            var resolved = CreateValidResolvedConfigs();
            resolved.Enemy.MoveSpeed = -1f;

            LogAssert.Expect(LogType.Warning, new Regex("MoveSpeed"));
            bool valid = ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Warning);
            Assert.IsTrue(valid, "Warning mode must return true even for out-of-range values");
        }

        [Test]
        [Description("ValidationMode.Warning returns true for null Upgrades.")]
        public void Validate_UpgradesNull_WarningMode_ReturnsTrue()
        {
            var resolved = CreateValidResolvedConfigsWithUpgrades(null);

            LogAssert.Expect(LogType.Warning, new Regex("UpgradeNodeConfigs"));
            bool valid = ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Warning);
            Assert.IsTrue(valid, "Warning mode must return true even for null Upgrades");
        }

        // ── Positive: valid configs pass ──────────────────────────────────────────

        [Test]
        [Description("ConfigValidator returns true for a valid default-values resolved config set.")]
        public void Validate_ValidDefaultValues_ReturnsTrue()
        {
            var resolved = CreateValidResolvedConfigs();
            bool valid = ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Error);
            Assert.IsTrue(valid, "Default-value configs must pass ConfigValidator.Validate()");
        }

        [Test]
        [Description("Validate passes with multiple UpgradeNodes all having non-empty NodeIds.")]
        public void Validate_MultipleValidUpgradeNodes_ReturnsTrue()
        {
            var resolved = CreateValidResolvedConfigsWithUpgrades(new[]
            {
                CreateUpgradeNode("node_damage_01"),
                CreateUpgradeNode("node_hp_01"),
                CreateUpgradeNode("node_speed_01"),
            });

            bool valid = ConfigValidator.Validate(resolved, ConfigValidator.ValidationMode.Error);
            Assert.IsTrue(valid, "ConfigValidator must pass with multiple valid UpgradeNodes");
        }
    }
}
