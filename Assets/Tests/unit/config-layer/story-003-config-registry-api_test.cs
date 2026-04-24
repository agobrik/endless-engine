// Tests for Story S1-02: Config Layer — ConfigRegistry Service Locator API
// Type: Logic (Unit/EditMode)
// Story: production/epics/config-layer/story-003-config-registry-api.md
//
// These tests run in EditMode (no Unity runtime required).
// They validate the ConfigNotLoadedException guard, InjectForTesting partial injection,
// ClearForTesting reset, and EnableBaseFallback pass-through (AC-CFG-03, AC-CFG-09).
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.ConfigLayer

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.Tests.Unit.ConfigLayer
{
    /// <summary>
    /// Unit tests for ConfigRegistry service locator API (S1-02 / Story 003).
    /// Validates AC-CFG-03 (base SO fallback), AC-CFG-09 (InjectForTesting),
    /// and ConfigNotLoadedException guard behavior.
    /// </summary>
    [TestFixture]
    public class ConfigRegistryApiTests
    {
        // ── SetUp / TearDown ──────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ConfigRegistry.ClearForTesting();
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ConfigRegistry.ClearForTesting();
#endif
        }

        // ── ConfigNotLoadedException guard ────────────────────────────────────────

        [Test]
        [Description("ConfigRegistry.Enemy throws ConfigNotLoadedException before any injection.")]
        public void Enemy_AccessedBeforeLoad_ThrowsConfigNotLoadedException()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.IsFalse(ConfigRegistry.IsLoaded, "Precondition: IsLoaded must be false");
            Assert.Throws<ConfigNotLoadedException>(() =>
            {
                var _ = ConfigRegistry.Enemy;
            }, "ConfigRegistry.Enemy must throw ConfigNotLoadedException before IsLoaded=true");
#else
            Assert.Ignore("ConfigNotLoadedException guard only active in Editor/Development builds.");
#endif
        }

        [Test]
        [Description("ConfigRegistry.Wave throws ConfigNotLoadedException before any injection.")]
        public void Wave_AccessedBeforeLoad_ThrowsConfigNotLoadedException()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<ConfigNotLoadedException>(() =>
            {
                var _ = ConfigRegistry.Wave;
            }, "ConfigRegistry.Wave must throw ConfigNotLoadedException before IsLoaded=true");
#else
            Assert.Ignore("ConfigNotLoadedException guard only active in Editor/Development builds.");
#endif
        }

        [Test]
        [Description("ConfigRegistry.Upgrades throws ConfigNotLoadedException before any injection.")]
        public void Upgrades_AccessedBeforeLoad_ThrowsConfigNotLoadedException()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<ConfigNotLoadedException>(() =>
            {
                var _ = ConfigRegistry.Upgrades;
            }, "ConfigRegistry.Upgrades must throw ConfigNotLoadedException before IsLoaded=true");
#else
            Assert.Ignore("ConfigNotLoadedException guard only active in Editor/Development builds.");
#endif
        }

        [Test]
        [Description("ConfigNotLoadedException message includes the accessor name.")]
        public void ConfigNotLoadedException_Message_ContainsAccessorName()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var ex = Assert.Throws<ConfigNotLoadedException>(() =>
            {
                var _ = ConfigRegistry.Economy;
            });
            StringAssert.Contains("Economy", ex.Message,
                "ConfigNotLoadedException message must name the accessor (Economy) to aid debugging");
#else
            Assert.Ignore("ConfigNotLoadedException guard only active in Editor/Development builds.");
#endif
        }

        // ── AC-CFG-09: InjectForTesting ───────────────────────────────────────────

        [Test]
        [Description("AC-CFG-09: InjectForTesting sets Enemy; ConfigRegistry.Enemy returns the mock.")]
        public void InjectForTesting_Enemy_ReturnsMock()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var mock = ScriptableObject.CreateInstance<EnemyStatConfigSO>();

            ConfigRegistry.InjectForTesting(enemy: mock);

            Assert.AreSame(mock, ConfigRegistry.Enemy,
                "ConfigRegistry.Enemy must return the exact mock instance after injection");
#endif
        }

        [Test]
        [Description("AC-CFG-09: Partial injection sets only specified types; others throw before load.")]
        public void InjectForTesting_Partial_OnlyInjectedTypeAccessible()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var wave = ScriptableObject.CreateInstance<WaveConfigSO>();
            ConfigRegistry.InjectForTesting(wave: wave);

            // Wave is set
            Assert.AreSame(wave, ConfigRegistry.Wave, "Wave should return the injected mock");

            // Enemy was not injected — accessing it after IsLoaded=true returns null (no guard fires when loaded)
            Assert.IsNull(ConfigRegistry.Enemy, "Enemy should be null when not injected (partial injection)");
#endif
        }

        [Test]
        [Description("AC-CFG-09: InjectForTesting sets IsLoaded=true even for partial injection.")]
        public void InjectForTesting_SetsIsLoadedTrue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ConfigRegistry.InjectForTesting(economy: ScriptableObject.CreateInstance<EconomyConfigSO>());
            Assert.IsTrue(ConfigRegistry.IsLoaded, "IsLoaded must be true after any InjectForTesting call");
#endif
        }

        [Test]
        [Description("AC-CFG-09: ClearForTesting resets IsLoaded=false and all accessors throw.")]
        public void ClearForTesting_ResetsIsLoadedAndAllAccessors()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: inject something
            ConfigRegistry.InjectForTesting(enemy: ScriptableObject.CreateInstance<EnemyStatConfigSO>());
            Assert.IsTrue(ConfigRegistry.IsLoaded, "Precondition: should be loaded");

            // Act
            ConfigRegistry.ClearForTesting();

            // Assert: IsLoaded false
            Assert.IsFalse(ConfigRegistry.IsLoaded, "ClearForTesting must set IsLoaded=false");

            // Assert: accessors throw again
            Assert.Throws<ConfigNotLoadedException>(() => { var _ = ConfigRegistry.Enemy; },
                "Enemy must throw ConfigNotLoadedException after ClearForTesting");
            Assert.Throws<ConfigNotLoadedException>(() => { var _ = ConfigRegistry.Wave; },
                "Wave must throw ConfigNotLoadedException after ClearForTesting");
#endif
        }

        [Test]
        [Description("AC-CFG-09: Second InjectForTesting call replaces only the specified fields.")]
        public void InjectForTesting_SecondCall_OverridesOnlySpecifiedFields()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var wave1 = ScriptableObject.CreateInstance<WaveConfigSO>();
            var wave2 = ScriptableObject.CreateInstance<WaveConfigSO>();
            var enemy = ScriptableObject.CreateInstance<EnemyStatConfigSO>();

            ConfigRegistry.InjectForTesting(wave: wave1, enemy: enemy);
            ConfigRegistry.InjectForTesting(wave: wave2); // replaces wave only

            Assert.AreSame(wave2, ConfigRegistry.Wave, "Second injection should override Wave");
            Assert.AreSame(enemy, ConfigRegistry.Enemy, "Enemy should remain from first injection");
#endif
        }

        // ── AC-CFG-03: EnableBaseFallback (pass-through) ──────────────────────────
        // Note: AC-CFG-03 refers to the behavior that when a base SO is injected (no realm
        // override), ConfigRegistry returns it directly without error. This is the natural
        // behavior of InjectForTesting — there is no special fallback flag in ConfigRegistry
        // itself; the "fallback" is that ConfigLoadingService passes the base SO from RealmPackSO.
        // These tests confirm the pass-through works for all 8 types.

        [Test]
        [Description("AC-CFG-03: All 8 types with base SO injection — all return their injected mock.")]
        public void InjectForTesting_All8Types_AllReturnInjectedMocks()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var enemy    = ScriptableObject.CreateInstance<EnemyStatConfigSO>();
            var wave     = ScriptableObject.CreateInstance<WaveConfigSO>();
            var economy  = ScriptableObject.CreateInstance<EconomyConfigSO>();
            var upgrades = new[] { ScriptableObject.CreateInstance<UpgradeNodeConfigSO>() };
            var prestige = ScriptableObject.CreateInstance<PrestigeConfigSO>();
            var realm    = ScriptableObject.CreateInstance<RealmIdentityConfigSO>();
            var player   = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            var schema   = ScriptableObject.CreateInstance<SchemaVersionSO>();

            ConfigRegistry.InjectForTesting(
                enemy: enemy, wave: wave, economy: economy, upgrades: upgrades,
                prestige: prestige, realm: realm, player: player, schema: schema);

            Assert.AreSame(enemy,   ConfigRegistry.Enemy,   "Enemy mismatch");
            Assert.AreSame(wave,    ConfigRegistry.Wave,    "Wave mismatch");
            Assert.AreSame(economy, ConfigRegistry.Economy, "Economy mismatch");
            Assert.AreSame(upgrades, ConfigRegistry.Upgrades, "Upgrades mismatch");
            Assert.AreSame(prestige, ConfigRegistry.Prestige, "Prestige mismatch");
            Assert.AreSame(realm,   ConfigRegistry.Realm,   "Realm mismatch");
            Assert.AreSame(player,  ConfigRegistry.Player,  "Player mismatch");
            Assert.AreSame(schema,  ConfigRegistry.Schema,  "Schema mismatch");
#endif
        }

        // ── IsLoaded state machine ────────────────────────────────────────────────

        [Test]
        [Description("IsLoaded is false at start of test (after ClearForTesting in SetUp).")]
        public void IsLoaded_IsFalseBeforeInjection()
        {
            Assert.IsFalse(ConfigRegistry.IsLoaded, "IsLoaded must be false before any injection");
        }

        [Test]
        [Description("IsLoaded is true after InjectForTesting and false after ClearForTesting.")]
        public void IsLoaded_TransitionsCorrectly()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.IsFalse(ConfigRegistry.IsLoaded, "Should start false");

            ConfigRegistry.InjectForTesting(enemy: ScriptableObject.CreateInstance<EnemyStatConfigSO>());
            Assert.IsTrue(ConfigRegistry.IsLoaded, "Should be true after inject");

            ConfigRegistry.ClearForTesting();
            Assert.IsFalse(ConfigRegistry.IsLoaded, "Should be false after clear");
#endif
        }
    }
}
