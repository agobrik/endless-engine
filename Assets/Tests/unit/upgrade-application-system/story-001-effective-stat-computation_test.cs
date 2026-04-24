// Tests for Story S2-01: Upgrade Application System — Effective Stat Computation
// Type: Logic (Unit/EditMode)
// Story: production/epics/upgrade-application-system/story-001-effective-stat-computation.md
//
// Acceptance Criteria: AC-UAS-01 through AC-UAS-08
// Formula: (BaseStat + ΣAdditiveFlat) × (1 + ΣAdditivePercent) × PermanentMultiplier
// PermanentMultiplier applies to Damage and MaxHP only.
// Percent bonuses stack additively.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.UpgradeApplicationSystem

using System;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;

namespace EndlessEngine.Tests.Unit.UpgradeApplicationSystemTests
{
    /// <summary>
    /// Unit tests for UpgradeApplicationSystem effective stat formula (S2-01).
    /// Validates AC-UAS-01 through AC-UAS-08.
    /// </summary>
    [TestFixture]
    public class EffectiveStatComputationTests
    {
        private PlayerBaseStatConfigSO _playerConfig;
        private EconomyConfigSO        _economyConfig;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _playerConfig = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            _playerConfig.BaseAttackDamage = 10f;
            _playerConfig.BaseMaxHP        = 100f;
            _playerConfig.BaseMoveSpeed    = 5f;
            _playerConfig.BaseCritChance   = 0.05f;
            _playerConfig.BaseCritMultiplier = 2f;
            _playerConfig.BaseAttackInterval = 1f;
            _playerConfig.BaseAttackRange    = 5f;

            _economyConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            _economyConfig.IdleYieldRateBase = 10f;

            ConfigRegistry.InjectForTesting(player: _playerConfig, economy: _economyConfig);
            UpgradeApplicationSystem.ResetForTesting();
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpgradeApplicationSystem.ResetForTesting();
            ConfigRegistry.ClearForTesting();
#endif
        }

        // ── AC-UAS-01: Baseline ───────────────────────────────────────────────────

        [Test]
        [Description("AC-UAS-01: No upgrades applied — GetEffectiveStat returns base stat.")]
        public void GetEffectiveStat_NoUpgrades_ReturnsBaseStat()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: SetUp provides base 10 damage, no effects applied

            // Act
            float result = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);

            // Assert
            Assert.AreEqual(10f, result, 0.001f,
                "AC-UAS-01: No upgrades — effective stat must equal ConfigRegistry.Player.BaseAttackDamage");
#endif
        }

        // ── AC-UAS-02: Flat bonus ─────────────────────────────────────────────────

        [Test]
        [Description("AC-UAS-02: Flat +5 on base 10 → GetEffectiveStat returns 15.")]
        public void GetEffectiveStat_SingleFlatBonus_AddsToBase()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.Damage, 5f, EffectType.AdditiveFlat);

            // Act
            float result = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);

            // Assert
            Assert.AreEqual(15f, result, 0.001f,
                "AC-UAS-02: (10 + 5) × 1.0 × 1.0 = 15");
#endif
        }

        // ── AC-UAS-03: Flat + percent ─────────────────────────────────────────────

        [Test]
        [Description("AC-UAS-03: Flat +5 and percent +0.20 on base 10 → (10+5)×1.20 = 18.")]
        public void GetEffectiveStat_FlatPlusPercent_UsesCorrectFormula()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.Damage, 5f,    EffectType.AdditiveFlat);
            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.Damage, 0.20f, EffectType.AdditivePercent);

            // Act
            float result = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);

            // Assert: (10 + 5) × (1 + 0.20) × 1.0 = 18.0
            Assert.AreEqual(18f, result, 0.001f,
                "AC-UAS-03: Formula (Base + ΣFlat) × (1 + ΣPercent) × PermanentMult must equal 18");
#endif
        }

        [Test]
        [Description("AC-UAS-03 (additive stacking): Two percent bonuses stack additively, not multiplicatively.")]
        public void GetEffectiveStat_TwoPercentBonuses_StackAdditively()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: +20% and +30% = +50% total (NOT +56%)
            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.Damage, 0.20f, EffectType.AdditivePercent);
            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.Damage, 0.30f, EffectType.AdditivePercent);

            // Act
            float result = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);

            // Assert: (10 + 0) × (1 + 0.20 + 0.30) × 1.0 = 15.0 (not 10 × 1.2 × 1.3 = 15.6)
            Assert.AreEqual(15f, result, 0.001f,
                "Percent bonuses must stack additively: (Base) × (1 + ΣPercent), not multiplicatively");
#endif
        }

        // ── AC-UAS-04: CritChance cap ─────────────────────────────────────────────

        [Test]
        [Description("AC-UAS-04: CritChance capped at 1.0 even when upgrades total > 1.0.")]
        public void GetEffectiveStat_CritChance_CappedAtOne()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: base 0.05 + flat +2.0 would exceed 1.0
            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.CritChance, 2.0f, EffectType.AdditiveFlat);

            // Act
            float result = UpgradeApplicationSystem.GetEffectiveStat(StatType.CritChance);

            // Assert
            Assert.AreEqual(1.0f, result, 0.001f,
                "AC-UAS-04: CritChance must be clamped to [0, 1]");
#endif
        }

        // ── AC-UAS-05: Prestige clears run effects ────────────────────────────────

        [Test]
        [Description("AC-UAS-05: ClearRunEffects removes run effects; permanent effects survive.")]
        public void ClearRunEffects_RemovesRunEffects_PermanentSurvives()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: run +5 flat, permanent +3 flat
            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.Damage, 5f, EffectType.AdditiveFlat, isPermanent: false);
            UpgradeApplicationSystem.ApplyPermanentEffectForTesting(StatType.Damage, 3f, EffectType.AdditiveFlat);

            // Pre-condition: should be 10 + 5 + 3 = 18
            float before = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);
            Assert.AreEqual(18f, before, 0.001f, "Precondition: run + permanent applied = 18");

            // Act
            UpgradeApplicationSystem.ClearRunEffects();

            // Assert: only permanent remains → 10 + 3 = 13
            float after = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);
            Assert.AreEqual(13f, after, 0.001f,
                "AC-UAS-05: After ClearRunEffects, only permanent effect remains (base 10 + permanent 3 = 13)");
#endif
        }

        // ── AC-UAS-06: Save data restore ──────────────────────────────────────────

        [Test]
        [Description("AC-UAS-06: Effects loaded from save state are applied at correct cumulative rank.")]
        public void ApplyUpgradeEffect_MultipleRankEffects_CumulativelyApplied()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: node_atk_1 at rank 2 means 2 flat +5 effects applied
            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.Damage, 5f, EffectType.AdditiveFlat);
            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.Damage, 5f, EffectType.AdditiveFlat);

            // Act
            float result = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);

            // Assert: base 10 + (5 × 2 ranks) = 20
            Assert.AreEqual(20f, result, 0.001f,
                "AC-UAS-06: Rank 2 node applies 2× per-rank effect cumulatively");
#endif
        }

        // ── AC-UAS-07: SimulateEffect does not mutate ─────────────────────────────

        [Test]
        [Description("AC-UAS-07: SimulateEffect returns projected value; live stat is unchanged.")]
        public void SimulateEffect_DoesNotMutateLiveState()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: apply flat +5 → live stat = 15
            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.Damage, 5f, EffectType.AdditiveFlat);
            float liveBefore = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);
            Assert.AreEqual(15f, liveBefore, 0.001f, "Precondition: live stat is 15");

            // Create an UpgradeNodeConfigSO mock for simulation
            var nodeConfig = ScriptableObject.CreateInstance<UpgradeNodeConfigSO>();
            nodeConfig.NodeId         = "node_atk_sim";
            nodeConfig.AffectedStat   = StatType.Damage;
            nodeConfig.EffectType     = UpgradeEffectType.FlatBonus;
            nodeConfig.EffectPerRank  = 8f;
            nodeConfig.MaxRank        = 5;

            ConfigRegistry.InjectForTesting(upgrades: new[] { nodeConfig });

            // Act
            float projected = UpgradeApplicationSystem.SimulateEffect("node_atk_sim");

            // Assert: projected = (10 + 5 + 8) × 1.0 × 1.0 = 23
            Assert.AreEqual(23f, projected, 0.001f,
                "AC-UAS-07: Projected value must be live stat + simulated effect");

            // Live stat unchanged
            float liveAfter = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);
            Assert.AreEqual(15f, liveAfter, 0.001f,
                "AC-UAS-07: SimulateEffect must NOT mutate live stat");
#endif
        }

        // ── AC-UAS-08: Unknown node ID tolerance ──────────────────────────────────

        [Test]
        [Description("AC-UAS-08: SimulateEffect with unknown node ID returns 0, no exception.")]
        public void SimulateEffect_UnknownNodeId_ReturnsZeroNoException()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: upgrades array has no "ghost_node"
            ConfigRegistry.InjectForTesting(upgrades: new UpgradeNodeConfigSO[0]);

            // Act / Assert: no exception
            float result = 0f;
            Assert.DoesNotThrow(
                () => { result = UpgradeApplicationSystem.SimulateEffect("ghost_node"); },
                "AC-UAS-08: SimulateEffect with unknown ID must not throw");

            Assert.AreEqual(0f, result, 0.001f,
                "AC-UAS-08: SimulateEffect with unknown node ID must return 0");
#endif
        }

        // ── PermanentMultiplier: MoveSpeed excluded ───────────────────────────────

        [Test]
        [Description("MoveSpeed is NOT amplified by PermanentMultiplier regardless of prestige count.")]
        public void GetEffectiveStat_MoveSpeed_NotAmplifiedByPermanentMultiplier()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: set permanent multiplier > 1
            UpgradeApplicationSystem.SetPermanentMultiplier(5.0625f); // 1.5^4

            // Act
            float result = UpgradeApplicationSystem.GetEffectiveStat(StatType.MoveSpeed);

            // Assert: MoveSpeed = base only, multiplier not applied
            Assert.AreEqual(_playerConfig.BaseMoveSpeed, result, 0.001f,
                "MoveSpeed must equal base stat — PermanentMultiplier must NOT be applied to MoveSpeed");
#endif
        }

        [Test]
        [Description("Damage IS amplified by PermanentMultiplier.")]
        public void GetEffectiveStat_Damage_AmplifiedByPermanentMultiplier()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: base damage 10, permanent multiplier 2.0
            UpgradeApplicationSystem.SetPermanentMultiplier(2.0f);

            // Act
            float result = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);

            // Assert: (10 + 0) × 1.0 × 2.0 = 20
            Assert.AreEqual(20f, result, 0.001f,
                "Damage must be amplified by PermanentMultiplier: (base) × 1.0 × 2.0 = 20");
#endif
        }

        // ── Cache dirty-flag behaviour ────────────────────────────────────────────

        [Test]
        [Description("GetEffectiveStat returns updated value after second ApplyUpgradeEffect call.")]
        public void GetEffectiveStat_AfterSecondEffect_ReturnsFreshValue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.Damage, 5f, EffectType.AdditiveFlat);
            float first = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage); // 15, now cached

            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.Damage, 5f, EffectType.AdditiveFlat);

            // Act
            float second = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);

            // Assert: cache should be invalidated and re-computed
            Assert.AreEqual(20f, second, 0.001f,
                "Cache must be invalidated after ApplyUpgradeEffect — second call must return updated value");
#endif
        }

        [Test]
        [Description("OnEffectiveStatChanged fires when stat is recomputed after dirty flag set.")]
        public void GetEffectiveStat_WhenDirty_FiresOnEffectiveStatChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            StatType firedStat  = default;
            float    firedValue = 0f;
            UpgradeApplicationSystem.OnEffectiveStatChanged += (s, v) => { firedStat = s; firedValue = v; };

            UpgradeApplicationSystem.ApplyUpgradeEffect(StatType.Damage, 5f, EffectType.AdditiveFlat);

            // Act
            float _ = UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage);

            // Assert
            Assert.AreEqual(StatType.Damage, firedStat,
                "OnEffectiveStatChanged must fire with the correct StatType");
            Assert.AreEqual(15f, firedValue, 0.001f,
                "OnEffectiveStatChanged must fire with the recomputed value");
#endif
        }
    }
}
