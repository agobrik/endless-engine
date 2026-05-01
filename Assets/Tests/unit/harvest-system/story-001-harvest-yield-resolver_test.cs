// Tests for Story HAR-01: Harvest System — Yield Resolver
// Type: Unit (EditMode)
//
// Acceptance Criteria:
//   AC-HAR-01: Tick yield = (BaseYield / MaxHP × damage) × yieldMult × comboMult
//   AC-HAR-02: Depletion yield = BaseYield × yieldMult × comboMult
//   AC-HAR-03: Multi-node bonus applies for count > 1
//   AC-HAR-04: HarvestYieldMultiplier stat scales yield correctly
//   AC-HAR-05: HarvestMultiNodeBonus stat scales multi-node bonus correctly
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.HarvestSystem

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;
using EndlessEngine.Harvest;

namespace EndlessEngine.Tests.Unit.HarvestSystem
{
    [TestFixture]
    public class HarvestYieldResolverTests
    {
        private HarvestNodeConfigSO _config;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpgradeApplicationSystem.ResetForTesting();
#endif
            _config = ScriptableObject.CreateInstance<HarvestNodeConfigSO>();
            _config.NodeId          = "test_tree";
            _config.MaxHP           = 10f;
            _config.DamagePerTick   = 1f;
            _config.BaseYield       = 5f;
            _config.AwardYieldPerTick = true;
            _config.RespawnSeconds  = 5f;
            _config.ComboContribution = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
                Object.DestroyImmediate(_config);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpgradeApplicationSystem.ResetForTesting();
#endif
        }

        // ── AC-HAR-01: Tick yield formula ─────────────────────────────────────────

        [Test]
        [Description("AC-HAR-01: Tick yield = (BaseYield/MaxHP × damage) × 1 × 1 with no upgrades")]
        public void ResolveTickYield_NoUpgrades_ReturnsCorrectBase()
        {
            float damage     = 1f;
            float expected   = (_config.BaseYield / _config.MaxHP) * damage; // 0.5

            float result = HarvestYieldResolver.ResolveTickYield(_config, damage, comboMultiplier: 1f);

            Assert.AreEqual(expected, result, 0.001f,
                "Tick yield must equal BaseYield/MaxHP × damage when no upgrades active");
        }

        [Test]
        [Description("AC-HAR-01: Tick yield scales with damage amount")]
        public void ResolveTickYield_FullDamage_ReturnsFullYield()
        {
            float damage   = _config.MaxHP; // deplete in one hit
            float expected = _config.BaseYield;

            float result = HarvestYieldResolver.ResolveTickYield(_config, damage, comboMultiplier: 1f);

            Assert.AreEqual(expected, result, 0.001f,
                "Applying full MaxHP damage in one tick must yield full BaseYield");
        }

        // ── AC-HAR-02: Depletion yield formula ───────────────────────────────────

        [Test]
        [Description("AC-HAR-02: Depletion yield = BaseYield × yieldMult × comboMult with no upgrades")]
        public void ResolveDepletionYield_NoUpgrades_ReturnsBaseYield()
        {
            float result = HarvestYieldResolver.ResolveDepletionYield(_config, comboMultiplier: 1f);

            Assert.AreEqual(_config.BaseYield, result, 0.001f,
                "Depletion yield must equal BaseYield with no upgrades and no combo");
        }

        [Test]
        [Description("AC-HAR-02: Depletion yield scales with combo multiplier")]
        public void ResolveDepletionYield_WithCombo_ScalesCorrectly()
        {
            float combo    = 2.5f;
            float expected = _config.BaseYield * combo;

            float result = HarvestYieldResolver.ResolveDepletionYield(_config, comboMultiplier: combo);

            Assert.AreEqual(expected, result, 0.001f,
                "Depletion yield must scale linearly with combo multiplier");
        }

        // ── AC-HAR-03: Multi-node bonus ───────────────────────────────────────────

        [Test]
        [Description("AC-HAR-03: Single node — no multi-node bonus applied")]
        public void ResolveTickYield_SingleNode_NoBonusApplied()
        {
            float single = HarvestYieldResolver.ResolveTickYield(_config, 1f, 1f, simultaneousNodes: 1);
            float multi  = HarvestYieldResolver.ResolveTickYield(_config, 1f, 1f, simultaneousNodes: 1);

            Assert.AreEqual(single, multi, 0.001f,
                "Single node count must not apply any multi-node scaling");
        }

        [Test]
        [Description("AC-HAR-03: Two nodes with no HarvestMultiNodeBonus stat → no bonus (stat = 0)")]
        public void ResolveTickYield_TwoNodes_NoStatBonus_NoExtraYield()
        {
            float single = HarvestYieldResolver.ResolveTickYield(_config, 1f, 1f, simultaneousNodes: 1);
            float dual   = HarvestYieldResolver.ResolveTickYield(_config, 1f, 1f, simultaneousNodes: 2);

            // HarvestMultiNodeBonus stat = 0 → bonus = 1 + 0×(2-1) = 1 → no change
            Assert.AreEqual(single, dual, 0.001f,
                "Without HarvestMultiNodeBonus stat, two nodes must yield same as one");
        }

        // ── AC-HAR-04: HarvestYieldMultiplier stat ────────────────────────────────

        [Test]
        [Description("AC-HAR-04: HarvestYieldMultiplier +0.5 → yield ×1.5")]
        public void ResolveTickYield_WithYieldMultiplierStat_ScalesYield()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpgradeApplicationSystem.ApplyUpgradeEffect(
                StatType.HarvestYieldMultiplier, 0.5f, EffectType.AdditivePercent);
#endif
            float expected = (_config.BaseYield / _config.MaxHP) * 1f * 1.5f;
            float result   = HarvestYieldResolver.ResolveTickYield(_config, 1f, comboMultiplier: 1f);

            Assert.AreEqual(expected, result, 0.001f,
                "HarvestYieldMultiplier +0.5 must multiply yield by 1.5");
        }

        // ── AC-HAR-05: HarvestMultiNodeBonus stat ─────────────────────────────────

        [Test]
        [Description("AC-HAR-05: HarvestMultiNodeBonus +0.5, two nodes → yield × 1.5")]
        public void ResolveTickYield_WithMultiNodeStat_TwoNodes_ScalesYield()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpgradeApplicationSystem.ApplyUpgradeEffect(
                StatType.HarvestMultiNodeBonus, 0.5f, EffectType.AdditivePercent);
#endif
            float baseYield = (_config.BaseYield / _config.MaxHP) * 1f; // no yieldMult
            float expected  = baseYield * (1f + 0.5f * (2 - 1));        // ×1.5

            float result = HarvestYieldResolver.ResolveTickYield(_config, 1f, 1f, simultaneousNodes: 2);

            Assert.AreEqual(expected, result, 0.001f,
                "HarvestMultiNodeBonus +0.5 with 2 nodes must multiply yield by 1.5");
        }
    }
}
